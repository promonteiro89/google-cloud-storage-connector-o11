// Cache isolation and supplied-token tests: a caller's SubjectToken is part of the cache key, an
// expired token is rejected before any exchange, a private key is parsed once, and the shared caches
// stay bounded and expiring. Offline, against the federation fakes in FederationTests.cs.
// C# 5 only.

using System;
using System.Reflection;
using System.Text;
using Google.Apis.Http;
using OutSystems.NssGoogleCloudStorage_ext;

public static class CacheIsolationTests
{
    private const string Provider = "//iam.googleapis.com/projects/123456789/locations/global/workloadIdentityPools/pool/providers/prov";

    private static readonly Type Sut = typeof(CssGoogleCloudStorage_ext);
    private static FakeFederationBackend _backend;

    // ---- plumbing (the same seams FederationTests uses) --------------------------------------

    private static void SetFactory(IHttpClientFactory factory)
    {
        Sut.GetProperty("HttpClientFactoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, factory, null);
    }

    /// <summary>Fresh fake backend per test; the extension's HTTP goes through it.</summary>
    private static void Fresh()
    {
        _backend = new FakeFederationBackend();
        SetFactory(new FakeHttpClientFactory(_backend));
    }

    /// <summary>A unique service account per test keeps the extension's static caches from leaking between tests.</summary>
    private static string UniqueServiceAccount()
    {
        return ("sa-" + Guid.NewGuid().ToString("N")).Substring(0, 20) + "@proj.iam.gserviceaccount.com";
    }

    private static string Base64Url(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>An unsigned fake JWT (base64url JSON) whose payload carries the given exp, in Unix seconds.</summary>
    private static string Jwt(long expiresAtUnix)
    {
        string header = Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url("{\"iss\":\"https://idp.example.test/\",\"sub\":\"app\",\"exp\":" + expiresAtUnix + "}");
        return header + "." + payload + ".c2ln"; // dummy signature segment
    }

    private static long Unix(DateTime utc)
    {
        return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    private static RCGCS_AuthenticationRecord SuppliedToken(string serviceAccount, string token)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        r.ssSTGCS_Authentication.ssWorkloadIdentityProvider = Provider;
        r.ssSTGCS_Authentication.ssServiceAccountEmail = serviceAccount;
        r.ssSTGCS_Authentication.ssSubjectToken = token;
        return r;
    }

    private static RCGCS_AuthenticationRecord KeyRecord(string email, string pem)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
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

    private static bool Has(string haystack, string needle) { return haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0; }

    /// <summary>Calls the extension's internal static helper and returns [readable, expiresUtc].</summary>
    private static object[] ReadExpiry(string jwt)
    {
        var args = new object[] { jwt, null };
        bool ok = (bool)Invoke("TryReadJwtExpiry", args);
        return new object[] { ok, args[1] };
    }

    // ---- entry point -------------------------------------------------------------------------

    public static void Run(string email, string pem)
    {
        string savedEmulator = Environment.GetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", null); // real (faked) Google endpoints only
        try
        {
            SuppliedTokenIsolation();
            ExpiredSuppliedToken();
            JwtExpiryReader();
            CacheKeyLengthPrefix();
            KeyParsedOnce(email, pem);
        }
        finally
        {
            SetFactory(null);
            Environment.SetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST", savedEmulator);
        }
    }

    // ---- (a)(b) one caller's token per cache entry -------------------------------------------

    private static void SuppliedTokenIsolation()
    {
        var ext = new CssGoogleCloudStorage_ext();
        bool exists;
        int n;

        // (a) Two different SubjectTokens for the same service account: each must be exchanged on
        // its own, and must get its own StorageClient.
        Fresh();
        string sa = UniqueServiceAccount();
        string first = Jwt(Unix(DateTime.UtcNow.AddHours(1)));
        string second = Jwt(Unix(DateTime.UtcNow.AddHours(1)) + 1);

        ext.MssBucket_Exists(SuppliedToken(sa, first), "my-bucket", out exists);
        ext.MssBucket_Exists(SuppliedToken(sa, second), "my-bucket", out exists);

        var sts = _backend.To("sts.googleapis.com");
        var firstClient = StorageClientFor(SuppliedToken(sa, first));
        var secondClient = StorageClientFor(SuppliedToken(sa, second));
        TestLog.Check("Two different SubjectTokens are exchanged separately and never share a StorageClient",
            exists && sts.Count == 2
            && sts[0].Form["subject_token"] == first && sts[1].Form["subject_token"] == second
            && !ReferenceEquals(firstClient, secondClient),
            sts.Count + " STS exchange(s)");

        // The reverse order must describe the same two entries, not a shared "latest token wins" one.
        var stsAfter = _backend.To("sts.googleapis.com").Count;
        StorageClientFor(SuppliedToken(sa, second));
        StorageClientFor(SuppliedToken(sa, first));
        TestLog.Check("Each supplied token keeps its own cached identity (order does not matter)",
            _backend.To("sts.googleapis.com").Count == stsAfter && !ReferenceEquals(
                StorageClientFor(SuppliedToken(sa, first)), StorageClientFor(SuppliedToken(sa, second))),
            (stsAfter) + " -> " + _backend.To("sts.googleapis.com").Count + " STS exchange(s)");

        // (b) The same token twice: one exchange, one client.
        Fresh();
        sa = UniqueServiceAccount();
        string token = Jwt(Unix(DateTime.UtcNow.AddHours(1)));
        ext.MssBucket_Exists(SuppliedToken(sa, token), "my-bucket", out exists);
        ext.MssBucket_Exists(SuppliedToken(sa, token), "my-bucket", out exists);
        sts = _backend.To("sts.googleapis.com");
        n = _backend.To("idp.example.test").Count;
        TestLog.Check("The same SubjectToken is exchanged once and reuses its StorageClient",
            exists && sts.Count == 1 && n == 0
            && ReferenceEquals(StorageClientFor(SuppliedToken(sa, token)), StorageClientFor(SuppliedToken(sa, token))),
            sts.Count + " STS exchange(s), " + n + " IdP request(s)");
    }

    // ---- (c) an expired supplied token -------------------------------------------------------

    private static void ExpiredSuppliedToken()
    {
        Fresh();
        string expired = Jwt(Unix(DateTime.UtcNow.AddMinutes(-10)));
        var ex = Throws(() =>
        {
            bool e;
            new CssGoogleCloudStorage_ext().MssBucket_Exists(SuppliedToken(UniqueServiceAccount(), expired), "my-bucket", out e);
        });
        TestLog.Check("An expired SubjectToken is rejected before any network call",
            ex is ArgumentException && Has(ex.Message, "expired") && _backend.To("sts.googleapis.com").Count == 0,
            ex == null ? "no exception" : ex.GetType().Name + ": " + ex.Message);
    }

    // ---- (d) the exp reader ------------------------------------------------------------------

    private static void JwtExpiryReader()
    {
        var expected = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var readable = ReadExpiry(Jwt(4102444800));
        TestLog.Check("TryReadJwtExpiry reads exp 4102444800 as 2100-01-01T00:00:00Z",
            (bool)readable[0] && (DateTime)readable[1] == expected,
            (bool)readable[0] ? ((DateTime)readable[1]).ToString("u") : "unreadable");

        readable = ReadExpiry("not-a-jwt");
        TestLog.Check("TryReadJwtExpiry returns false for 'not-a-jwt'", !(bool)readable[0], readable[0].ToString());

        // Three parts, but the middle segment is not JSON: false, never an exception.
        readable = ReadExpiry(Base64Url("{\"alg\":\"none\"}") + "." + Base64Url("this is not json") + ".c2ln");
        TestLog.Check("TryReadJwtExpiry returns false for a non-JSON payload without throwing",
            !(bool)readable[0], readable[0].ToString());
    }

    // ---- (e) unambiguous cache keys ----------------------------------------------------------

    private static void CacheKeyLengthPrefix()
    {
        string whereKeyMeetsValue = (string)Invoke("CacheKey", "key", new[] { "a", "b\nc" });
        string asOnePart = (string)Invoke("CacheKey", "key", new[] { "a\nb", "c" });
        string again = (string)Invoke("CacheKey", "key", new[] { "x", "y" });
        TestLog.Check("CacheKey parts are length-prefixed: ('a','b\\nc') and ('a\\nb','c') do not collide",
            whereKeyMeetsValue != asOnePart);
        TestLog.Check("CacheKey is stable for the same parts", again == (string)Invoke("CacheKey", "key", new[] { "x", "y" }));
    }

    // ---- (f) one parsed key per credential ---------------------------------------------------

    private static void KeyParsedOnce(string email, string pem)
    {
        Fresh();
        object cache = Sut.GetField("ServiceAccountCredentialCache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        int before = CredentialCacheCount(cache);

        // A fresh email gives a cache key of its own, whatever earlier tests parsed.
        string uniqueEmail = ("cache-" + Guid.NewGuid().ToString("N")).Substring(0, 18) + "@fake-project.iam.gserviceaccount.com";
        var ext = new CssGoogleCloudStorage_ext();

        StorageClientFor(KeyRecord(uniqueEmail, pem)); // the storage-action path
        string url;
        ext.MssObject_GetSignedUrl(KeyRecord(uniqueEmail, pem), "Download", "my-bucket", "docs/report.pdf", 5, "", out url);

        int after = CredentialCacheCount(cache);
        TestLog.Check("Key mode parses the private key once for the StorageClient and the UrlSigner",
            after - before == 1 && Has(url, "X-Goog-Signature"),
            "credential cache " + before + " -> " + after);
    }

    /// <summary>Reflection on the extension's credential cache (a private nested ExpiringCache type).</summary>
    private static int CredentialCacheCount(object cache)
    {
        return (int)cache.GetType().GetProperty("Count").GetValue(cache, null);
    }
}
