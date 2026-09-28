// Workload Identity Federation tests against an in-process fake of the identity provider, Google STS,
// IAM Credentials and Storage: validation, the method switch, cache isolation and the protocol. Offline.
// C# 5 only.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Newtonsoft.Json;
using OutSystems.NssGoogleCloudStorage_ext;

/// <summary>
/// Plays every party in the federation chain (IdP token endpoint, STS, IAM Credentials
/// generateAccessToken and signBlob, Storage) and records each request for the tests to check.
/// </summary>
public sealed class FakeFederationBackend : HttpMessageHandler
{
    public const string IdpTokenEndpoint = "https://idp.example.test/oauth2/token";
    public const string StsToken = "sts-federated-token";
    public const string ServiceAccountToken = "sa-impersonated-token";
    public static readonly byte[] SignatureBytes = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    public sealed class Recorded
    {
        public HttpMethod Method;
        public Uri Uri;
        public string Authorization;
        public string Body;

        public Dictionary<string, string> Form
        {
            get
            {
                var d = new Dictionary<string, string>();
                foreach (var kv in Body.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = kv.Split(new[] { '=' }, 2);
                    d[WebUtility.UrlDecode(p[0])] = p.Length > 1 ? WebUtility.UrlDecode(p[1]) : "";
                }
                return d;
            }
        }
    }

    public readonly List<Recorded> Requests = new List<Recorded>();

    /// <summary>JWT the fake identity provider issues.</summary>
    public string IdpToken = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ3b3JrbG9hZCJ9.c2ln";
    /// <summary>When true, the identity provider rejects client_secret_post (only HTTP Basic works).</summary>
    public bool IdpRequiresBasicAuth;
    /// <summary>When true, the identity provider rejects every request with invalid_client.</summary>
    public bool IdpRejectsClient;
    /// <summary>Optional STS failure body (returned with 400).</summary>
    public string StsError;
    /// <summary>Optional signBlob failure status.</summary>
    public HttpStatusCode? SignBlobFailure;

    public List<Recorded> To(string hostOrPathFragment)
    {
        lock (Requests) return Requests.Where(r => r.Uri.ToString().IndexOf(hostOrPathFragment, StringComparison.Ordinal) >= 0).ToList();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync();
        string auth = request.Headers.Authorization == null ? null : request.Headers.Authorization.ToString();
        lock (Requests) Requests.Add(new Recorded { Method = request.Method, Uri = request.RequestUri, Authorization = auth, Body = body });

        string url = request.RequestUri.ToString();

        if (url.StartsWith(IdpTokenEndpoint, StringComparison.Ordinal))
        {
            bool usedBasic = auth != null && auth.StartsWith("Basic ", StringComparison.Ordinal);
            if (IdpRejectsClient || (IdpRequiresBasicAuth && !usedBasic))
                return Json(HttpStatusCode.Unauthorized, new { error = "invalid_client", error_description = "Client authentication failed." });
            return Json(HttpStatusCode.OK, new { access_token = IdpToken, token_type = "Bearer", expires_in = 3600 });
        }

        if (url.StartsWith("https://sts.googleapis.com/v1/token", StringComparison.Ordinal))
        {
            if (StsError != null)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(StsError, Encoding.UTF8, "application/json") };
            return Json(HttpStatusCode.OK, new
            {
                access_token = StsToken,
                issued_token_type = "urn:ietf:params:oauth:token-type:access_token",
                token_type = "Bearer",
                expires_in = 3600
            });
        }

        if (url.StartsWith("https://iamcredentials.googleapis.com/", StringComparison.Ordinal) && url.EndsWith(":generateAccessToken", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, new
            {
                accessToken = ServiceAccountToken,
                expireTime = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        if (url.StartsWith("https://iamcredentials.googleapis.com/", StringComparison.Ordinal) && url.EndsWith(":signBlob", StringComparison.Ordinal))
        {
            if (SignBlobFailure.HasValue)
            {
                var status = SignBlobFailure.Value;
                return Json(status, new { error = new { code = (int)status, message = "Permission 'iam.serviceAccounts.signBlob' denied.", status = "PERMISSION_DENIED" } });
            }
            return Json(HttpStatusCode.OK, new { keyId = "fake-key", signedBlob = Convert.ToBase64String(SignatureBytes) });
        }

        if (url.StartsWith("https://storage.googleapis.com/storage/v1/b/", StringComparison.Ordinal) && request.Method == HttpMethod.Get)
        {
            var bucket = request.RequestUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[3];
            return Json(HttpStatusCode.OK, new { kind = "storage#bucket", name = bucket, id = bucket });
        }

        return Json(HttpStatusCode.NotFound, new { error = new { code = 404, message = "FakeFederationBackend has no route for " + request.Method + " " + url } });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object payload)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json") };
    }
}

/// <summary>Google HttpClientFactory whose innermost handler is the fake backend.</summary>
public sealed class FakeHttpClientFactory : HttpClientFactory
{
    private readonly FakeFederationBackend _backend;
    public FakeHttpClientFactory(FakeFederationBackend backend) { _backend = backend; }
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) { return new NonDisposingHandler(_backend); }

    /// <summary>ConfigurableHttpClient disposes its handler chain; keep the shared fake alive.</summary>
    private sealed class NonDisposingHandler : DelegatingHandler
    {
        public NonDisposingHandler(HttpMessageHandler inner) : base(inner) { }
        protected override void Dispose(bool disposing) { /* the fake backend outlives any one client */ }
    }
}

public static class FederationTests
{
    private const string Provider = "//iam.googleapis.com/projects/123456789/locations/global/workloadIdentityPools/pool/providers/prov";

    private static readonly Type Sut = typeof(CssGoogleCloudStorage_ext);
    private static FakeFederationBackend _backend;

    // ---- plumbing --------------------------------------------------------------------------

    private static void SetFactory(IHttpClientFactory factory)
    {
        Sut.GetProperty("HttpClientFactoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, factory, null);
    }

    /// <summary>Fresh fake backend per test; the extension's HTTP goes through it.</summary>
    private static CssGoogleCloudStorage_ext Fresh()
    {
        _backend = new FakeFederationBackend();
        SetFactory(new FakeHttpClientFactory(_backend));
        return new CssGoogleCloudStorage_ext();
    }

    /// <summary>A unique service account per test keeps the extension's static caches from leaking between tests.</summary>
    private static string UniqueServiceAccount()
    {
        return ("sa-" + Guid.NewGuid().ToString("N")).Substring(0, 20) + "@proj.iam.gserviceaccount.com";
    }

    private static RCGCS_AuthenticationRecord Empty() { return new RCGCS_AuthenticationRecord(null); }

    private static RCGCS_AuthenticationRecord ClientCredentials(string serviceAccount)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        r.ssSTGCS_Authentication.ssWorkloadIdentityProvider = Provider;
        r.ssSTGCS_Authentication.ssServiceAccountEmail = serviceAccount ?? UniqueServiceAccount();
        r.ssSTGCS_Authentication.ssTokenEndpoint = FakeFederationBackend.IdpTokenEndpoint;
        r.ssSTGCS_Authentication.ssClientId = "my-client";
        r.ssSTGCS_Authentication.ssClientSecret = "my-secret";
        r.ssSTGCS_Authentication.ssScope = "api://gcs-connector/.default";
        return r;
    }

    private static RCGCS_AuthenticationRecord SuppliedToken(string token)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        r.ssSTGCS_Authentication.ssWorkloadIdentityProvider = Provider;
        r.ssSTGCS_Authentication.ssServiceAccountEmail = UniqueServiceAccount();
        r.ssSTGCS_Authentication.ssSubjectToken = token;
        return r;
    }

    private static RCGCS_AuthenticationRecord KeyRecord(string email, string pem, string method)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = method;
        r.ssSTGCS_Authentication.ssClientEmail = email;
        r.ssSTGCS_Authentication.ssPrivateKey = pem;
        return r;
    }

    private static object Invoke(string method, params object[] args)
    {
        var m = Sut.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        try { return m.Invoke(null, args); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    /// <summary>The extension's own record-to-credentials mapping, then its StorageClient factory.</summary>
    private static object StorageClientFor(RCGCS_AuthenticationRecord r)
    {
        return Invoke("GetStorageClient", Invoke("FromRecord", r));
    }

    private static Exception Throws(Action a)
    {
        try { a(); } catch (Exception e) { return e; }
        return null;
    }

    private static string AllMessages(Exception e)
    {
        var parts = new List<string>();
        for (Exception x = e; x != null; x = x.InnerException) parts.Add(x.Message);
        var agg = e as AggregateException;
        if (agg != null) parts.AddRange(agg.Flatten().InnerExceptions.Select(i => i.Message));
        return string.Join(" | ", parts);
    }

    private static Exception Innermost(Exception e, string containing)
    {
        for (Exception x = e; x != null; x = x.InnerException)
            if (x.Message.IndexOf(containing, StringComparison.Ordinal) >= 0) return x;
        var agg = e as AggregateException;
        if (agg != null)
            foreach (var i in agg.Flatten().InnerExceptions)
                if (i.Message.IndexOf(containing, StringComparison.Ordinal) >= 0) return i;
        return null;
    }

    private static bool Has(string haystack, string needle) { return haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0; }

    // ---- entry point -------------------------------------------------------------------------

    public static void Run(string email, string pem)
    {
        string savedEmulator = Environment.GetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", null); // real (faked) Google endpoints only
        try
        {
            CredentialInputs(email, pem);
            MethodSwitchAndValidation(email, pem);
            Protocol();
            SignedUrls();
        }
        finally
        {
            SetFactory(null);
            Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", savedEmulator);
        }
    }

    // ---- O11: the Authentication record is the only credential input ---------------------------

    private static void CredentialInputs(string email, string pem)
    {
        var ext = Fresh();
        bool exists;

        var none = Throws(() => ext.MssBucket_Exists(Empty(), "b", out exists));
        TestLog.Check("An empty Authentication record names the missing key fields",
            none is ArgumentException && Has(none.Message, "missing: ClientEmail, PrivateKey"), none == null ? "no exception" : none.Message);

        string url;
        ext.MssObject_GetSignedUrl(KeyRecord(email, pem, ""), "Download", "b", "o", 5, "", out url);
        TestLog.Check("A key in the record signs as that service account", CredentialParam(url) == email, url);
        TestLog.Check("Key-mode record signs locally (no network)", _backend.Requests.Count == 0, _backend.Requests.Count + " requests");

        RLGCS_BucketRecordList buckets;
        var noPid = KeyRecord(email, pem, "");
        noPid.ssSTGCS_Authentication.ssProjectId = "";
        var noProject = Throws(() => ext.MssBucket_List(noPid, out buckets));
        TestLog.Check("Bucket_List without ProjectId says ProjectId is required",
            noProject is ArgumentException && Has(noProject.Message, "requires Authentication.ProjectId"), noProject == null ? "no exception" : noProject.Message);
    }

    private static string CredentialParam(string url)
    {
        if (url == null) return null;
        foreach (var p in new Uri(url).Query.TrimStart('?').Split('&'))
            if (p.StartsWith("X-Goog-Credential=", StringComparison.Ordinal))
            {
                var v = Uri.UnescapeDataString(p.Substring("X-Goog-Credential=".Length));
                return v.Substring(0, v.IndexOf('/')); // the signing identity, without the date scope
            }
        return null;
    }

    // ---- method switch & validation -----------------------------------------

    private static void MethodSwitchAndValidation(string email, string pem)
    {
        var ext = Fresh();
        bool exists;

        var bad = KeyRecord(email, pem, "Kerberos");
        var ex = Throws(() => ext.MssBucket_Exists(bad, "b", out exists));
        TestLog.Check("Invalid method is rejected with the valid values", ex is ArgumentException && Has(ex.Message, "WorkloadIdentityFederation") && Has(ex.Message, "ServiceAccountKey"), ex == null ? "no exception" : ex.Message);

        string url;
        ext.MssObject_GetSignedUrl(KeyRecord(email, pem, "serviceaccountkey"), "Download", "b", "o", 5, "", out url);
        TestLog.Check("Explicit ServiceAccountKey (case-insensitive) behaves like the default", Has(url, "X-Goog-Signature") && _backend.Requests.Count == 0);

        var noKey = KeyRecord(email, "", "");
        ex = Throws(() => ext.MssBucket_Exists(noKey, "b", out exists));
        TestLog.Check("ServiceAccountKey without key material names the missing field", ex is ArgumentException && Has(ex.Message, "missing: PrivateKey"), ex == null ? "no exception" : ex.Message);

        var wifEmpty = Empty();
        wifEmpty.ssSTGCS_Authentication.ssProjectId = "proj";
        wifEmpty.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        ex = Throws(() => ext.MssBucket_Exists(wifEmpty, "b", out exists));
        bool namesAll = ex is ArgumentException && new[] { "WorkloadIdentityProvider", "ServiceAccountEmail", "TokenEndpoint", "ClientId", "ClientSecret" }.All(f => Has(ex.Message, f));
        TestLog.Check("Federation without required fields names every missing field", namesAll, ex == null ? "no exception" : ex.Message);

        TestLog.Check("Federation with a supplied token needs no client credentials (and no network yet)", StorageClientFor(SuppliedToken("a.b.c")) != null && _backend.Requests.Count == 0);

        var malformed = ClientCredentials(null);
        malformed.ssSTGCS_Authentication.ssWorkloadIdentityProvider = "my-pool";
        ex = Throws(() => StorageClientFor(malformed));
        TestLog.Check("Malformed provider is rejected with the expected format", ex is ArgumentException && Has(ex.Message, "workloadIdentityPools"), ex == null ? "no exception" : ex.Message);

        foreach (var form in new[] {
            "//iam.googleapis.com/projects/1/locations/global/workloadIdentityPools/p/providers/x",
            "https://iam.googleapis.com/projects/1/locations/global/workloadIdentityPools/p/providers/x",
            "projects/1/locations/global/workloadIdentityPools/p/providers/x" })
        {
            var normalized = (string)Invoke("NormalizeWorkloadIdentityProvider", form);
            TestLog.Check("Provider accepted as '" + form.Substring(0, 12) + "...'", normalized == "//iam.googleapis.com/projects/1/locations/global/workloadIdentityPools/p/providers/x", normalized);
        }

        var plainHttp = ClientCredentials(null);
        plainHttp.ssSTGCS_Authentication.ssTokenEndpoint = "http://idp.example.com/token";
        ex = Throws(() => StorageClientFor(plainHttp));
        var loopback = ClientCredentials(null);
        loopback.ssSTGCS_Authentication.ssTokenEndpoint = "http://localhost:8080/token";
        TestLog.Check("Plain http token endpoint is rejected unless loopback", ex is ArgumentException && Has(ex.Message, "https://") && StorageClientFor(loopback) != null, ex == null ? "no exception" : ex.Message);

        var key = StorageClientFor(KeyRecord(email, pem, ""));
        var sa = UniqueServiceAccount();
        var wif1 = StorageClientFor(ClientCredentials(sa));
        var wif2 = StorageClientFor(ClientCredentials(sa));
        var other = StorageClientFor(ClientCredentials(null));
        TestLog.Check("Key and federation never share a cached client; federation cached per identity",
            !ReferenceEquals(key, wif1) && ReferenceEquals(wif1, wif2) && !ReferenceEquals(wif1, other));
    }

    // ---- protocol contract: client credentials -> STS -> impersonation -> Storage ------------

    private static void Protocol()
    {
        bool exists;

        // Client credentials, end to end
        var ext = Fresh();
        var a = ClientCredentials(null);
        ext.MssBucket_Exists(a, "my-bucket", out exists);

        var idp = _backend.To("idp.example.test");
        var sts = _backend.To("sts.googleapis.com");
        var imp = _backend.To(":generateAccessToken");
        var storage = _backend.To("storage.googleapis.com");
        string saEmail = a.ssSTGCS_Authentication.ssServiceAccountEmail;

        TestLog.Check("Client credentials flow: bucket reported as existing", exists);
        TestLog.Check("  1) IdP: standard client-credentials grant (client_secret_post)",
            idp.Count == 1 && idp[0].Form["grant_type"] == "client_credentials" && idp[0].Form["client_id"] == "my-client"
            && idp[0].Form["client_secret"] == "my-secret" && idp[0].Form["scope"] == "api://gcs-connector/.default", idp.Count + " IdP requests");
        TestLog.Check("  2) STS: RFC 8693 token exchange of the provider's JWT",
            sts.Count == 1 && sts[0].Form["grant_type"] == "urn:ietf:params:oauth:grant-type:token-exchange" && sts[0].Form["audience"] == Provider
            && sts[0].Form["subject_token"] == _backend.IdpToken && sts[0].Form["subject_token_type"] == "urn:ietf:params:oauth:token-type:jwt"
            && sts[0].Form["requested_token_type"] == "urn:ietf:params:oauth:token-type:access_token", sts.Count + " STS requests");
        TestLog.Check("  3) IAM Credentials: impersonates the service account with the federated token",
            imp.Count == 1 && (Has(imp[0].Uri.ToString(), saEmail) || Has(imp[0].Uri.AbsoluteUri, Uri.EscapeDataString(saEmail)))
            && imp[0].Authorization == "Bearer " + FakeFederationBackend.StsToken, imp.Count == 1 ? imp[0].Uri + " / " + imp[0].Authorization : imp.Count + " requests");
        TestLog.Check("  4) Storage is called with the short-lived service account token, never a key",
            storage.Count == 1 && storage[0].Authorization == "Bearer " + FakeFederationBackend.ServiceAccountToken, storage.Count == 1 ? storage[0].Authorization : storage.Count + " requests");

        // Supplied token
        ext = Fresh();
        ext.MssBucket_Exists(SuppliedToken("header.payload.signature"), "my-bucket", out exists);
        sts = _backend.To("sts.googleapis.com");
        TestLog.Check("Supplied token skips the identity provider entirely",
            exists && _backend.To("idp.example.test").Count == 0 && sts.Count == 1 && sts[0].Form["subject_token"] == "header.payload.signature");

        // client_secret_basic fallback
        ext = Fresh();
        _backend.IdpRequiresBasicAuth = true;
        ext.MssBucket_Exists(ClientCredentials(null), "my-bucket", out exists);
        var attempts = _backend.To("idp.example.test");
        TestLog.Check("Falls back to HTTP Basic client auth when the provider requires it",
            exists && attempts.Count == 2 && attempts[0].Authorization == null && attempts[1].Authorization != null
            && attempts[1].Authorization.StartsWith("Basic ", StringComparison.Ordinal) && !attempts[1].Form.ContainsKey("client_secret"),
            attempts.Count + " attempts");

        // Rejected client credentials
        ext = Fresh();
        _backend.IdpRejectsClient = true;
        var ex = Throws(() => ext.MssBucket_Exists(ClientCredentials(null), "my-bucket", out exists));
        var root = ex == null ? null : Innermost(ex, "rejected the client-credentials");
        TestLog.Check("Rejected client credentials give an actionable error",
            root != null && Has(root.Message, "invalid_client") && Has(root.Message, "ClientSecret"), ex == null ? "no exception" : AllMessages(ex));

        // Non-JWT token
        ext = Fresh();
        _backend.IdpToken = "opaque-reference-token";
        ex = Throws(() => ext.MssBucket_Exists(ClientCredentials(null), "my-bucket", out exists));
        TestLog.Check("A non-JWT token from the provider is explained and never sent to Google",
            ex != null && Innermost(ex, "not a JWT") != null && _backend.To("sts.googleapis.com").Count == 0, ex == null ? "no exception" : AllMessages(ex));

        // STS rejection
        ext = Fresh();
        _backend.StsError = "{\"error\":\"unauthorized_client\",\"error_description\":\"The given credential is rejected by the attribute condition.\"}";
        ex = Throws(() => ext.MssBucket_Exists(ClientCredentials(null), "my-bucket", out exists));
        var msg = ex == null ? "" : AllMessages(ex);
        TestLog.Check("A token rejected by Google STS is explained", Has(msg, "attribute condition") && Has(msg, "Workload Identity Federation failed"), ex == null ? "no exception" : msg);
    }

    // ---- signed URLs via signBlob ------------------------------------------------------------

    private static void SignedUrls()
    {
        var ext = Fresh();
        var a = ClientCredentials(null);
        string url;
        ext.MssObject_GetSignedUrl(a, "Download", "my-bucket", "docs/report.pdf", 15, "", out url);
        var signBlob = _backend.To(":signBlob");
        string hex = string.Concat(FakeFederationBackend.SignatureBytes.Select(b => b.ToString("x2")));
        TestLog.Check("Signed URLs are signed by the service account through signBlob",
            signBlob.Count == 1 && signBlob[0].Authorization != null && signBlob[0].Authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            && Has(url, Uri.EscapeDataString(a.ssSTGCS_Authentication.ssServiceAccountEmail)) && Has(url, "X-Goog-Signature=" + hex), url);

        ext = Fresh();
        _backend.SignBlobFailure = HttpStatusCode.Forbidden;
        var ex = Throws(() => { string u; ext.MssObject_GetSignedUrl(ClientCredentials(null), "Download", "b", "o", 15, "", out u); });
        TestLog.Check("Missing Token Creator role on signBlob is explained", ex != null && Has(AllMessages(ex), "Service Account Token Creator"), ex == null ? "no exception" : AllMessages(ex));
    }
}

