// Live Workload Identity Federation against real Google Cloud, with GitHub Actions as the identity
// provider. Runs with -Category Live; skipped outside GitHub Actions. C# 5 only.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;
using OutSystems.NssGoogleCloudStorage_ext;

public static class LiveFederationTests
{
    private static string Env(string name) { return Environment.GetEnvironmentVariable(name); }

    private const string LiveStorage = "LIVE: Google accepts the federated identity for Storage calls";
    private const string LiveCredential = "LIVE: X-Goog-Credential names the service account";
    private const string LiveSignature = "LIVE: signature verifies against the service account's published certificates";

    public static void Run()
    {
        string requestUrl = Env("ACTIONS_ID_TOKEN_REQUEST_URL"), requestToken = Env("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        string provider = Env("GCP_WIF_PROVIDER"), serviceAccount = Env("GCP_SERVICE_ACCOUNT"), projectId = Env("GCP_PROJECT_ID");

        var missing = new List<string>();
        if (string.IsNullOrEmpty(requestUrl)) missing.Add("ACTIONS_ID_TOKEN_REQUEST_URL");
        if (string.IsNullOrEmpty(requestToken)) missing.Add("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        if (string.IsNullOrEmpty(provider)) missing.Add("GCP_WIF_PROVIDER");
        if (string.IsNullOrEmpty(serviceAccount)) missing.Add("GCP_SERVICE_ACCOUNT");
        if (missing.Count > 0)
        {
            // Outside GitHub Actions (with id-token: write and the GCP_* variables) the live checks
            // are reported as SKIPPED, never passed. In CI, the 'Check live configuration' step fails
            // the job before this point, so a green live job always means the checks really ran.
            string reason = "live tests run only in GitHub Actions; missing: " + string.Join(", ", missing);
            TestLog.Skip(LiveStorage, reason);
            TestLog.Skip(LiveCredential, reason);
            TestLog.Skip(LiveSignature, reason);
            return;
        }

        // Google endpoints require TLS 1.2+; don't depend on the process's default protocol set.
        System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

        string savedEmulator = Env("GCSCONNECTOR_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", null); // real Google endpoints only
        try
        {
            var auth = new RCGCS_AuthenticationRecord(null);
            auth.ssSTGCS_Authentication.ssProjectId = projectId ?? "";
            auth.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
            auth.ssSTGCS_Authentication.ssWorkloadIdentityProvider = provider;
            auth.ssSTGCS_Authentication.ssServiceAccountEmail = serviceAccount;
            try { auth.ssSTGCS_Authentication.ssSubjectToken = GetGitHubOidcToken(requestUrl, requestToken, provider); }
            catch (Exception e)
            {
                string why = "could not obtain the GitHub OIDC token: " + Chain(e);
                TestLog.Check(LiveStorage, false, why);
                TestLog.Check(LiveCredential, false, why);
                TestLog.Check(LiveSignature, false, why);
                return;
            }

            var ext = new CssGoogleCloudStorage_ext();

            // A bucket that cannot exist. A 404 (Exists = false) proves the whole chain worked: GitHub
            // token -> STS -> impersonated service account -> Storage. A broken chain throws 401/403.
            bool exists = true;
            Exception ex = null;
            try { ext.MssBucket_Exists(auth, "wif-probe-" + Guid.NewGuid().ToString("N"), out exists); }
            catch (Exception e) { ex = e; }
            TestLog.Check(LiveStorage, ex == null && !exists, ex == null ? "Exists=" + exists : Chain(ex));

            string url = null;
            ex = null;
            try { ext.MssObject_GetSignedUrl(auth, "Download", "wif-probe-bucket", "probe/report.pdf", 15, "", out url); }
            catch (Exception e) { ex = e; }
            if (ex != null)
            {
                TestLog.Check(LiveCredential, false, "signing failed: " + Chain(ex));
                TestLog.Check(LiveSignature, false, "signing failed, so there is no URL to verify");
                return;
            }
            TestLog.Check(LiveCredential, url.IndexOf(Uri.EscapeDataString(serviceAccount), StringComparison.Ordinal) >= 0, url);

            List<RSA> keys;
            try { keys = GetServiceAccountPublicKeys(serviceAccount); }
            catch (Exception e)
            {
                TestLog.Check(LiveSignature, false, "could not fetch the service account's public certificates: " + Chain(e));
                return;
            }
            TestLog.Check(LiveSignature + " (" + keys.Count + " certificates)", keys.Count > 0 && VerifyV4SignedUrl(url, keys), url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", savedEmulator);
        }
    }

    /// <summary>Requests a GitHub Actions OIDC token whose audience is the workload identity provider.</summary>
    private static string GetGitHubOidcToken(string requestUrl, string requestToken, string provider)
    {
        var normalize = typeof(CssGoogleCloudStorage_ext).GetMethod("NormalizeWorkloadIdentityProvider",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        string audience = "https:" + (string)normalize.Invoke(null, new object[] { provider });

        using (var http = new HttpClient())
        using (var request = new HttpRequestMessage(HttpMethod.Get, requestUrl + "&audience=" + Uri.EscapeDataString(audience)))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestToken);
            using (var response = http.SendAsync(request).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                return (string)JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())["value"];
            }
        }
    }

    /// <summary>Google publishes every service account's public signing certificates (no auth needed).</summary>
    private static List<RSA> GetServiceAccountPublicKeys(string serviceAccount)
    {
        using (var http = new HttpClient())
        {
            string json = http.GetStringAsync("https://www.googleapis.com/service_accounts/v1/metadata/x509/" + Uri.EscapeDataString(serviceAccount)).GetAwaiter().GetResult();
            var keys = new List<RSA>();
            foreach (var p in JObject.Parse(json).Properties())
            {
                string pem = (string)p.Value;
                string b64 = pem.Replace("-----BEGIN CERTIFICATE-----", "").Replace("-----END CERTIFICATE-----", "").Replace("\r", "").Replace("\n", "").Trim();
                keys.Add(new X509Certificate2(Convert.FromBase64String(b64)).GetRSAPublicKey());
            }
            return keys;
        }
    }

    /// <summary>
    /// Rebuilds the V4 string-to-sign from the URL and verifies the RSA-SHA256 signature, as GCS does.
    /// </summary>
    public static bool VerifyV4SignedUrl(string url, IEnumerable<RSA> publicKeys)
    {
        var uri = new Uri(url);
        var pairs = uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        Func<string, string> value = name => Uri.UnescapeDataString(pairs.First(p => p.StartsWith(name + "=", StringComparison.Ordinal)).Split(new[] { '=' }, 2)[1]);

        string canonicalQuery = string.Join("&", pairs
            .Where(p => !p.StartsWith("X-Goog-Signature=", StringComparison.Ordinal))
            .OrderBy(p => p.Split('=')[0], StringComparer.Ordinal));
        string credential = value("X-Goog-Credential");                 // SA/DATE/auto/storage/goog4_request
        string scope = credential.Substring(credential.IndexOf('/') + 1);
        string canonicalRequest = string.Join("\n",
            "GET", uri.AbsolutePath, canonicalQuery, "host:" + uri.Host + "\n", value("X-Goog-SignedHeaders"), "UNSIGNED-PAYLOAD");
        string stringToSign = string.Join("\n",
            "GOOG4-RSA-SHA256", value("X-Goog-Date"), scope, Hex(Sha256(Encoding.UTF8.GetBytes(canonicalRequest))));

        byte[] signature = FromHex(value("X-Goog-Signature"));
        byte[] data = Encoding.UTF8.GetBytes(stringToSign);
        return publicKeys.Any(k => k.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    /// <summary>
    /// Offline check of the verifier: a URL signed with the test key verifies, a tampered one doesn't.
    /// </summary>
    public static void VerifierSelfTest(string email, string pem)
    {
        var key = new ServiceAccountCredential(new ServiceAccountCredential.Initializer(email).FromPrivateKey(pem)).Key;
        var publicOnly = new RSACryptoServiceProvider();
        publicOnly.ImportParameters(key.ExportParameters(false));
        var keys = new[] { (RSA)publicOnly };

        // Key-mode signing is local RSA: no network, so the emulator setting is irrelevant here.
        string url;
        var keyAuth = new RCGCS_AuthenticationRecord(null);
        keyAuth.ssSTGCS_Authentication.ssProjectId = "proj";
        keyAuth.ssSTGCS_Authentication.ssClientEmail = email;
        keyAuth.ssSTGCS_Authentication.ssPrivateKey = pem;
        new CssGoogleCloudStorage_ext().MssObject_GetSignedUrl(keyAuth, "Download", "my-bucket", "folder/report 2025.pdf", 15, "", out url);
        TestLog.Check("Signed-URL verifier accepts a genuine V4 URL", VerifyV4SignedUrl(url, keys), url);
        TestLog.Check("Signed-URL verifier rejects a tampered V4 URL", !VerifyV4SignedUrl(url.Replace("my-bucket", "other-bucket"), keys));
    }

    private static byte[] Sha256(byte[] data) { using (var sha = SHA256.Create()) return sha.ComputeHash(data); }
    private static string Hex(byte[] b) { return string.Concat(b.Select(x => x.ToString("x2"))); }
    private static byte[] FromHex(string s)
    {
        var bytes = new byte[s.Length / 2];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static string Chain(Exception e)
    {
        var parts = new List<string>();
        for (Exception x = e; x != null; x = x.InnerException) parts.Add(x.GetType().Name + ": " + x.Message);
        return string.Join(" | ", parts);
    }
}
