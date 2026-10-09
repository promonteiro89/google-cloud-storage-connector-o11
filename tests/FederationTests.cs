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
        public byte[] Content;
        public Dictionary<string, string> Headers;

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
    /// <summary>
    /// When true, the resumable-upload endpoint flips one bit of the received data, as a transit
    /// error would. Like the real server, it then rejects the upload if the x-goog-hash CRC32C no
    /// longer matches.
    /// </summary>
    public bool CorruptUploadInTransit;
    /// <summary>Objects stored by the fake upload endpoint, by "bucket/name".</summary>
    public readonly Dictionary<string, byte[]> StoredObjects = new Dictionary<string, byte[]>();

    private readonly Dictionary<string, string[]> _uploadSessions = new Dictionary<string, string[]>();

    public List<Recorded> To(string hostOrPathFragment)
    {
        lock (Requests) return Requests.Where(r => r.Uri.ToString().IndexOf(hostOrPathFragment, StringComparison.Ordinal) >= 0).ToList();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[] content = request.Content == null ? new byte[0] : await request.Content.ReadAsByteArrayAsync();
        if (request.Content != null && request.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            // Google's client gzips JSON request bodies (e.g. the upload session's metadata).
            using (var gzip = new System.IO.Compression.GZipStream(new System.IO.MemoryStream(content), System.IO.Compression.CompressionMode.Decompress))
            using (var plain = new System.IO.MemoryStream())
            {
                gzip.CopyTo(plain);
                content = plain.ToArray();
            }
        }
        string body = Encoding.UTF8.GetString(content);
        string auth = request.Headers.Authorization == null ? null : request.Headers.Authorization.ToString();
        var headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
        lock (Requests) Requests.Add(new Recorded { Method = request.Method, Uri = request.RequestUri, Authorization = auth, Body = body, Content = content, Headers = headers });

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

        if (url.StartsWith("https://storage.googleapis.com/upload/storage/v1/b/", StringComparison.Ordinal))
            return ResumableUpload(request, content, headers);

        if (url.StartsWith("https://storage.googleapis.com/storage/v1/b/", StringComparison.Ordinal) && request.Method == HttpMethod.Get)
        {
            var bucket = request.RequestUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[3];
            return Json(HttpStatusCode.OK, new { kind = "storage#bucket", name = bucket, id = bucket });
        }

        return Json(HttpStatusCode.NotFound, new { error = new { code = 404, message = "FakeFederationBackend has no route for " + request.Method + " " + url } });
    }

    /// <summary>
    /// Google's resumable upload: a POST starts a session (metadata JSON, Location header back), then
    /// a PUT carries the bytes. The client sends x-goog-hash on the final request; a mismatch gets 400
    /// with the real service's message, and nothing is stored.
    /// </summary>
    private HttpResponseMessage ResumableUpload(HttpRequestMessage request, byte[] content, Dictionary<string, string> headers)
    {
        if (request.Method == HttpMethod.Post)
        {
            var bucket = request.RequestUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[4];
            var name = (string)Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(content))["name"];
            var session = Guid.NewGuid().ToString("N");
            lock (_uploadSessions) _uploadSessions[session] = new[] { bucket, name };
            var started = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
            started.Headers.Location = new Uri("https://storage.googleapis.com/upload/storage/v1/b/" + bucket + "/o?uploadType=resumable&upload_id=" + session);
            return started;
        }

        var uploadId = request.RequestUri.Query.TrimStart('?').Split('&')
            .Where(p => p.StartsWith("upload_id=", StringComparison.Ordinal)).Select(p => Uri.UnescapeDataString(p.Substring("upload_id=".Length))).First();
        string[] target;
        lock (_uploadSessions) target = _uploadSessions[uploadId];

        byte[] received = (byte[])content.Clone();
        if (CorruptUploadInTransit && received.Length > 0) received[received.Length / 2] ^= 0x01;
        string calculated = Crc32c.Base64(received);

        string hash;
        if (headers.TryGetValue("x-goog-hash", out hash))
        {
            var crc = hash.Split(',').Select(h => h.Trim()).FirstOrDefault(h => h.StartsWith("crc32c=", StringComparison.Ordinal));
            string provided = crc == null ? null : crc.Substring("crc32c=".Length);
            if (provided != null && provided != calculated)
            {
                var message = "Provided CRC32C \"" + provided + "\" doesn't match calculated CRC32C \"" + calculated + "\".";
                return Json(HttpStatusCode.BadRequest, new { error = new { code = 400, message = message, errors = new[] { new { message = message, domain = "global", reason = "invalid" } } } });
            }
        }

        lock (StoredObjects) StoredObjects[target[0] + "/" + target[1]] = received;
        return Json(HttpStatusCode.OK, new { kind = "storage#object", bucket = target[0], name = target[1], size = received.Length.ToString(), crc32c = calculated });
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


/// <summary>
/// Reference CRC32C (Castagnoli, reflected polynomial 0x82F63B78), as Google Cloud Storage computes
/// it: base64 of the big-endian 32-bit value. Independent of the SDK, so the tests check the wire value.
/// </summary>
public static class Crc32c
{
    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(i =>
    {
        uint c = (uint)i;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0x82F63B78u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    public static uint Compute(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    public static string Base64(byte[] data)
    {
        uint v = Compute(data);
        return Convert.ToBase64String(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
    }
}

/// <summary>
/// Upload integrity (Google.Cloud.Storage.V1 5.0+): Object_Upload sends the CRC32C of the exact bytes
/// in x-goog-hash, so Google rejects data corrupted in transit before storing it. Runs against the
/// fake resumable-upload endpoint. The HTTP seam is wired into the federated client, so the uploads
/// use WorkloadIdentityFederation with a SubjectToken. Offline.
/// </summary>
public static class UploadIntegrityTests
{
    private static RCGCS_AuthenticationRecord Federated()
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        r.ssSTGCS_Authentication.ssWorkloadIdentityProvider = "//iam.googleapis.com/projects/1/locations/global/workloadIdentityPools/p/providers/x";
        r.ssSTGCS_Authentication.ssServiceAccountEmail = ("sa-" + Guid.NewGuid().ToString("N")).Substring(0, 20) + "@proj.iam.gserviceaccount.com";
        r.ssSTGCS_Authentication.ssSubjectToken = "header.payload.signature";
        return r;
    }

    private static byte[] SampleBytes(int length)
    {
        var data = new byte[length];
        new Random(42).NextBytes(data);
        return data;
    }

    private static void SetFactory(IHttpClientFactory factory)
    {
        typeof(CssGoogleCloudStorage_ext).GetProperty("HttpClientFactoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, factory, null);
    }

    private static string Chain(Exception e)
    {
        var parts = new List<string>();
        for (Exception x = e; x != null; x = x.InnerException) parts.Add(x.GetType().Name + ": " + x.Message);
        return string.Join(" | ", parts);
    }

    public static void Run()
    {
        string savedEmulator = Environment.GetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", null);
        try
        {
            // CRC-32C check value for "123456789" (RFC 3720, appendix B.4).
            uint check = Crc32c.Compute(Encoding.ASCII.GetBytes("123456789"));
            TestLog.Check("CRC32C reference matches the published check value (0xE3069283)", check == 0xE3069283u, "0x" + check.ToString("X8"));

            // Upload sends the CRC32C of the exact bytes, and they are stored unchanged.
            var backend = new FakeFederationBackend();
            SetFactory(new FakeHttpClientFactory(backend));
            var data = SampleBytes(300000);
            Exception ex = null;
            try { new CssGoogleCloudStorage_ext().MssObject_Upload(Federated(), "my-bucket", "docs/report.bin", data, "application/octet-stream", new RLGCS_MetadataEntryRecordList()); }
            catch (Exception e) { ex = e; }
            var puts = backend.To("/upload/storage/v1/b/my-bucket/o").Where(r => r.Method == HttpMethod.Put).ToList();
            string hash = null;
            if (puts.Count > 0) puts[puts.Count - 1].Headers.TryGetValue("x-goog-hash", out hash);
            byte[] stored;
            bool storedOk = backend.StoredObjects.TryGetValue("my-bucket/docs/report.bin", out stored) && stored.SequenceEqual(data);
            TestLog.Check("Upload sends x-goog-hash with the CRC32C of the exact bytes and stores them unchanged",
                ex == null && hash != null && hash.Contains("crc32c=" + Crc32c.Base64(data)) && storedOk,
                ex != null ? Chain(ex) : "x-goog-hash=" + (hash ?? "(not sent)") + ", expected crc32c=" + Crc32c.Base64(data) + ", stored=" + storedOk);

            // Data corrupted in transit is rejected by Google and explained; nothing is stored.
            backend = new FakeFederationBackend();
            backend.CorruptUploadInTransit = true;
            SetFactory(new FakeHttpClientFactory(backend));
            ex = null;
            try { new CssGoogleCloudStorage_ext().MssObject_Upload(Federated(), "my-bucket", "docs/report.bin", SampleBytes(50000), "application/octet-stream", new RLGCS_MetadataEntryRecordList()); }
            catch (Exception e) { ex = e; }
            TestLog.Check("Data corrupted in transit is rejected with a 'nothing was stored, retry' error and nothing is stored",
                ex != null && ex.Message.Contains("CRC32C") && ex.Message.Contains("nothing was stored") && ex.Message.Contains("retry") && backend.StoredObjects.Count == 0,
                ex == null ? "no exception; stored objects: " + backend.StoredObjects.Count : Chain(ex) + "; stored objects: " + backend.StoredObjects.Count);
        }
        finally
        {
            SetFactory(null);
            Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", savedEmulator);
        }
    }
}
