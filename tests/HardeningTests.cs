// 1.7.5 tests, all offline against the in-process fake of Google Storage, Google's token endpoints and the
// identity provider: which repeats count as "may have been applied", bucket-name and ContentType checks,
// assembly-load errors, the shared HTTP client factory, Bucket_Create / Bucket_Delete repeats, error
// details, the download stall guard and size read, network retries, and the federation input checks.
// C# 5 only. Expected values come from the written behaviour (CHANGELOG 1.7.5), never from what the code printed.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using Newtonsoft.Json;
using OutSystems.NssGoogleCloudStorage_ext;
using Rec = FakeFederationBackend.Recorded;

public static class HardeningTests
{
    private static readonly Type Sut = typeof(CssGoogleCloudStorage_ext);
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    private const string Precondition = "At least one of the pre-conditions you specified did not hold.";

    public static void Run(string email, string pem)
    {
        string savedEmulator = EmulatorSeam.Get();
        EmulatorSeam.Set(null);
        try
        {
            ResendClassification();
            BucketNames(email, pem);
            ContentTypes(email, pem);
            AssemblyLoadErrors();
            HttpClientFactories(pem);
            BucketCreateAndDelete();
            ErrorDetails();
            DownloadSizeRead();
            DownloadStall();
            NetworkRetries();
            TransientStatuses();
            DeleteByPrefixRepeats();
            SignedUrlRepeat();
            FederationInputs();
            SecretWireForm();
            ImpersonationDenied();
            ParallelSignIn();
            MessageCleaner();
            ReviewFollowUps();
            SlowIdentityProvider();
        }
        finally
        {
            Sut.GetField("MaxDownloadBytes", Static).SetValue(null, 0x7FFFFFC7L);
            Sut.GetField("DownloadStallTimeout", Static).SetValue(null, TimeSpan.FromSeconds(60));
            FederationTests.SetFactory(null);
            EmulatorSeam.Set(savedEmulator);
        }
    }

    // ---- plumbing ----------------------------------------------------------------------------

    private static FakeFederationBackend NewBackend(Func<Rec, HttpResponseMessage> handler)
    {
        var backend = new FakeFederationBackend();
        backend.Override = handler;
        FederationTests.SetFactory(new FakeHttpClientFactory(backend));
        return backend;
    }

    private static HttpResponseMessage GoogleError(int code, string message)
    {
        string json = "{\"error\":{\"code\":" + code + ",\"message\":\"" + message + "\",\"errors\":[{\"message\":\"" + message + "\",\"domain\":\"global\",\"reason\":\"test\"}]}}";
        return new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage Text(int code, string body, string mediaType)
    {
        return new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
    }

    private static HttpResponseMessage NoContent() { return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent("") }; }
    private static HttpResponseMessage Json(object value) { return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json") }; }

    private static string[] Segments(Rec r) { return r.Uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); }
    private static bool IsStorage(Rec r) { return r.Uri.Host == "storage.googleapis.com" && (r.Uri.AbsolutePath + "/").StartsWith("/storage/v1/b/", StringComparison.Ordinal); }
    private static bool IsObject(Rec r) { var s = Segments(r); return IsStorage(r) && s.Length == 6 && s[4] == "o"; }
    private static bool IsMedia(Rec r) { return IsObject(r) && r.Method == HttpMethod.Get && r.Uri.Query.Contains("alt=media"); }
    private static bool IsMetaGet(Rec r) { return IsObject(r) && r.Method == HttpMethod.Get && !r.Uri.Query.Contains("alt=media"); }
    private static bool IsDelete(Rec r) { return IsObject(r) && r.Method == HttpMethod.Delete; }
    private static bool IsPatch(Rec r) { return IsObject(r) && r.Method == FakeFederationBackend.Patch; }
    private static bool IsRewrite(Rec r) { var s = Segments(r); return r.Method == HttpMethod.Post && s.Length == 11 && s[4] == "o" && s[6] == "rewriteTo"; }
    private static bool IsList(Rec r) { var s = Segments(r); return IsStorage(r) && r.Method == HttpMethod.Get && s.Length == 5 && s[4] == "o"; }
    private static bool IsBucketCreate(Rec r) { return IsStorage(r) && r.Method == HttpMethod.Post && Segments(r).Length == 3; }
    private static bool IsBucketList(Rec r) { return IsStorage(r) && r.Method == HttpMethod.Get && Segments(r).Length == 3; }
    private static bool IsBucketGet(Rec r) { return IsStorage(r) && r.Method == HttpMethod.Get && Segments(r).Length == 4; }
    private static bool IsBucketDelete(Rec r) { return IsStorage(r) && r.Method == HttpMethod.Delete && Segments(r).Length == 4; }
    private static bool IsSignBlob(Rec r) { return r.Uri.Host == "iamcredentials.googleapis.com" && r.Uri.AbsolutePath.EndsWith(":signBlob", StringComparison.Ordinal); }
    private static bool IsGenerateAccessToken(Rec r) { return r.Uri.Host == "iamcredentials.googleapis.com" && r.Uri.AbsolutePath.EndsWith(":generateAccessToken", StringComparison.Ordinal); }
    private static bool IsSts(Rec r) { return r.Uri.Host == "sts.googleapis.com"; }
    private static bool IsGoogleTokenEndpoint(Rec r) { return r.Uri.Host == "oauth2.googleapis.com"; }
    private static bool IsSessionStart(Rec r) { return r.Uri.Host == "storage.googleapis.com" && r.Uri.AbsolutePath.StartsWith("/upload/storage/v1/b/", StringComparison.Ordinal) && r.Method == HttpMethod.Post; }

    private static string Query(Uri uri, string name)
    {
        string query = uri.Query;
        if (string.IsNullOrEmpty(query)) return null;
        foreach (string pair in query.TrimStart('?').Split('&'))
        {
            string[] parts = pair.Split(new[] { '=' }, 2);
            if (Uri.UnescapeDataString(parts[0]) == name) return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "";
        }
        return null;
    }

    private static int Count(FakeFederationBackend b, Func<Rec, bool> p) { lock (b.Requests) return b.Requests.Count(p); }
    private static List<Rec> Where(FakeFederationBackend b, Func<Rec, bool> p) { lock (b.Requests) return b.Requests.Where(p).ToList(); }
    private static int Total(FakeFederationBackend b) { lock (b.Requests) return b.Requests.Count; }

    private static string Detail(Exception ex, FakeFederationBackend b)
    {
        var sb = new StringBuilder(ex == null ? "no exception" : UploadIntegrityTests.Chain(ex));
        if (b == null) return sb.ToString();
        sb.Append(" || requests: ");
        lock (b.Requests)
            foreach (var r in b.Requests)
                sb.Append(r.Method + " " + r.Uri.Host + r.Uri.PathAndQuery + "; ");
        return sb.ToString();
    }

    private static bool Has(string s, string n) { return FederationTests.Has(s, n); }
    private static CssGoogleCloudStorage_ext Ext() { return new CssGoogleCloudStorage_ext(); }
    private static RCGCS_AuthenticationRecord Auth() { return UploadIntegrityTests.Federated(); }
    private static Exception Throws(Action a) { return FederationTests.Throws(a); }

    private static RCGCS_AuthenticationRecord Key(string email, string pem)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssClientEmail = email;
        r.ssSTGCS_Authentication.ssPrivateKey = pem;
        return r;
    }

    private static string UniqueEmail()
    {
        return ("key-" + Guid.NewGuid().ToString("N")).Substring(0, 20) + "@proj.iam.gserviceaccount.com";
    }

    /// <summary>Serves the given bytes as the object's media, with a matching CRC32C.</summary>
    private static HttpResponseMessage Media(byte[] data)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        response.Headers.TryAddWithoutValidation("x-goog-hash", "crc32c=" + Crc32c.Base64(data));
        return response;
    }

    private static HttpResponseMessage MediaFrom(Stream body, byte[] hashOf)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        if (hashOf != null) response.Headers.TryAddWithoutValidation("x-goog-hash", "crc32c=" + Crc32c.Base64(hashOf));
        return response;
    }

    private static object ObjectJson(string size, string contentEncoding)
    {
        return new { kind = "storage#object", bucket = "my-bucket", name = "docs/d.bin", generation = "1001", metageneration = "1", contentType = "text/plain", size = size, contentEncoding = contentEncoding };
    }

    private static byte[] Sample(int length)
    {
        var data = new byte[length];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
        return data;
    }

    // ---- 1. A repeat after 408 or 429 is not "may have been applied" ---------------------------------

    private static void ResendClassification()
    {
        var applied = Sut.GetMethod("MayHaveBeenApplied", Static);
        Func<int, Exception> api = code => new Google.GoogleApiException("storage", "x") { HttpStatusCode = (HttpStatusCode)code };
        var table = new[]
        {
            new KeyValuePair<Exception, bool>(api(408), false), new KeyValuePair<Exception, bool>(api(429), false),
            new KeyValuePair<Exception, bool>(api(400), false),
            new KeyValuePair<Exception, bool>(api(500), true), new KeyValuePair<Exception, bool>(api(502), true),
            new KeyValuePair<Exception, bool>(api(503), true), new KeyValuePair<Exception, bool>(api(504), true),
            new KeyValuePair<Exception, bool>(new HttpRequestException("reset"), true), new KeyValuePair<Exception, bool>(new IOException("broken"), true)
        };
        var wrong = table.Where(t => (bool)applied.Invoke(null, new object[] { t.Key }) != t.Value).Select(t => t.Key.GetType().Name + " " + t.Key.Message).ToList();
        TestLog.Check("MayHaveBeenApplied: false for 408, 429 and 400; true for 500, 502, 503, 504 and a network failure", wrong.Count == 0, string.Join(", ", wrong));

        foreach (int refused in new[] { 429, 408 })
        {
            // Object_Delete: the refused send was not applied, so the 412 on the repeat means someone
            // replaced the object: read the new generation and delete it, as for a first-send 412.
            int deletes = 0, gets = 0;
            var backend = NewBackend(r =>
            {
                if (IsMetaGet(r) && ++gets == 2) return Json(new { kind = "storage#object", bucket = "my-bucket", name = "docs/a.txt", generation = "1002", metageneration = "1" });
                if (IsDelete(r)) { deletes++; return deletes == 1 ? GoogleError(refused, "refused") : deletes == 2 ? GoogleError(412, Precondition) : NoContent(); }
                return null;
            });
            var ex = Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
            var ds = Where(backend, IsDelete);
            TestLog.Check("Delete: a " + refused + " then a 412 re-reads and deletes the new generation (2 GETs; DELETEs 1001, 1001, 1002)",
                ex == null && Count(backend, IsMetaGet) == 2 && ds.Count == 3 && Query(ds[0].Uri, "ifGenerationMatch") == "1001" && Query(ds[1].Uri, "ifGenerationMatch") == "1001" && Query(ds[2].Uri, "ifGenerationMatch") == "1002",
                Detail(ex, backend));

            // Object_Move: the source was changed by someone else, so it must be reported, not swallowed.
            int moveDeletes = 0;
            backend = NewBackend(r =>
            {
                if (IsRewrite(r)) return Json(new { kind = "storage#rewriteResponse", done = true, resource = new { kind = "storage#object", bucket = "dst-bucket", name = "out/x.txt", generation = "5000", metageneration = "1" } });
                if (IsDelete(r)) return ++moveDeletes == 1 ? GoogleError(refused, "refused") : GoogleError(412, Precondition);
                return null;
            });
            ex = Throws(() => Ext().MssObject_Move(Auth(), "src-bucket", "in/x.txt", "dst-bucket", "out/x.txt"));
            TestLog.Check("Move: a " + refused + " then a 412 on the source delete reports 'was modified during the move ... both objects exist'",
                ex != null && Has(ex.Message, "was modified during the move") && Has(ex.Message, "both objects exist") && Count(backend, IsDelete) == 2, Detail(ex, backend));

            // Object_UpdateMetadata: nothing was applied, so the plain conflict message is the true one.
            int patches = 0;
            backend = NewBackend(r => IsPatch(r) ? (++patches == 1 ? GoogleError(refused, "refused") : GoogleError(412, Precondition)) : null);
            ex = Throws(() => Ext().MssObject_UpdateMetadata(Auth(), "my-bucket", "docs/a.txt", "text/csv", "", "", "", new RLGCS_MetadataEntryRecordList()));
            TestLog.Check("UpdateMetadata: a " + refused + " then a 412 says 'nothing was changed', not 'may already have been applied'",
                ex != null && Has(ex.Message, "nothing was changed") && !Has(ex.Message, "may already have been applied") && Count(backend, IsPatch) == 2, Detail(ex, backend));
        }

        // Control: a mix. The 503 may have been applied, so a later 412 still counts as deleted even with a 429 in between.
        int mixed = 0;
        var mixedBackend = NewBackend(r =>
        {
            if (IsDelete(r)) { mixed++; return mixed == 1 ? GoogleError(503, "Service Unavailable") : mixed == 2 ? GoogleError(429, "refused") : GoogleError(412, Precondition); }
            return null;
        });
        var mixedEx = Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete: a 503, a 429, then a 412 counts as deleted without a re-read (1 GET, 3 DELETEs)",
            mixedEx == null && Count(mixedBackend, IsMetaGet) == 1 && Count(mixedBackend, IsDelete) == 3, Detail(mixedEx, mixedBackend));

        // The Move message names the generation as the source's, not the destination's.
        var labelBackend = NewBackend(r =>
        {
            if (IsRewrite(r)) return Json(new { kind = "storage#rewriteResponse", done = true, resource = new { kind = "storage#object", bucket = "dst-bucket", name = "out/x.txt", generation = "5000", metageneration = "1" } });
            if (IsDelete(r)) return GoogleError(412, Precondition);
            return null;
        });
        var labelEx = Throws(() => Ext().MssObject_Move(Auth(), "src-bucket", "in/x.txt", "dst-bucket", "out/x.txt"));
        TestLog.Check("Move: the 'modified during the move' message puts the generation (1001) on the source, not after the destination",
            labelEx != null && Has(labelEx.Message, "'src-bucket/in/x.txt' (generation 1001 when the move started)") && !Has(labelEx.Message, "'dst-bucket/out/x.txt' (generation"), Detail(labelEx, labelBackend));
    }

    // ---- 2. Bucket names -------------------------------------------------------------------------------

    private static void BucketNames(string email, string pem)
    {
        bool exists; byte[] content; string contentType; string url; long deleted;
        RLGCS_ObjectRecordList objects; string next; RLGCS_PrefixRecordList prefixes;
        var none = new RLGCS_MetadataEntryRecordList();
        string lineBreak = "my-bucket\nINJECTED: forged log line";
        string overrideChar = "my\u202Ebucket";

        var backend = NewBackend(r => null);
        var calls = new List<KeyValuePair<string, Action>>
        {
            new KeyValuePair<string, Action>("Object_Exists", () => Ext().MssObject_Exists(Auth(), lineBreak, "o", out exists)),
            new KeyValuePair<string, Action>("Object_Upload", () => Ext().MssObject_Upload(Auth(), lineBreak, "o", new byte[1], "text/plain", none)),
            new KeyValuePair<string, Action>("Object_Download", () => Ext().MssObject_Download(Auth(), lineBreak, "o", out content, out contentType)),
            new KeyValuePair<string, Action>("Object_Delete", () => Ext().MssObject_Delete(Auth(), lineBreak, "o")),
            new KeyValuePair<string, Action>("Object_List", () => Ext().MssObject_List(Auth(), lineBreak, "", 0, "", "", out objects, out next, out prefixes)),
            new KeyValuePair<string, Action>("Object_Copy (destination)", () => Ext().MssObject_Copy(Auth(), "src-bucket", "o", lineBreak, "o")),
            new KeyValuePair<string, Action>("Object_Move (source)", () => Ext().MssObject_Move(Auth(), lineBreak, "o", "dst-bucket", "o")),
            new KeyValuePair<string, Action>("Object_UpdateMetadata", () => Ext().MssObject_UpdateMetadata(Auth(), lineBreak, "o", "text/csv", "", "", "", none)),
            new KeyValuePair<string, Action>("Object_DeleteByPrefix", () => Ext().MssObject_DeleteByPrefix(Auth(), lineBreak, "p/", out deleted)),
            new KeyValuePair<string, Action>("Object_GetSignedUrl", () => Ext().MssObject_GetSignedUrl(Auth(), "Download", lineBreak, "o", 5, "", out url)),
            new KeyValuePair<string, Action>("Bucket_Exists", () => Ext().MssBucket_Exists(Auth(), lineBreak, out exists)),
            new KeyValuePair<string, Action>("Bucket_Create", () => Ext().MssBucket_Create(Auth(), lineBreak, "EU")),
            new KeyValuePair<string, Action>("Bucket_Delete", () => Ext().MssBucket_Delete(Auth(), lineBreak))
        };
        var failed = new List<string>();
        foreach (var call in calls)
        {
            var ex = Throws(call.Value);
            bool ok = ex is ArgumentException && Has(ex.Message, "is not a valid bucket name") && ex.Message.IndexOf('\n') < 0 && ex.Message.IndexOf('\r') < 0;
            if (!ok) failed.Add(call.Key + ": " + (ex == null ? "no exception" : ex.GetType().Name + ": " + ex.Message.Replace("\n", "\\n")));
        }
        TestLog.Check("Bucket name with a line break: all 13 actions that take one fail with 'is not a valid bucket name', with no line break in the message",
            failed.Count == 0, string.Join(" || ", failed));
        TestLog.Check("Bucket name with a line break: no request is sent by any of them", Total(backend) == 0, Detail(null, backend));

        var bidi = Throws(() => Ext().MssObject_Exists(Auth(), overrideChar, "o", out exists));
        TestLog.Check("Bucket name with U+202E: rejected, and the character does not reach the message",
            bidi is ArgumentException && Has(bidi.Message, "is not a valid bucket name") && bidi.Message.IndexOf('\u202E') < 0, Detail(bidi, null));

        var upper = Throws(() => Ext().MssObject_Exists(Auth(), "MyBucket", "o", out exists));
        TestLog.Check("Bucket name 'MyBucket' (uppercase): rejected with \"BucketName 'MyBucket' is not a valid bucket name\"",
            upper is ArgumentException && upper.Message.StartsWith("BucketName 'MyBucket' is not a valid bucket name", StringComparison.Ordinal), Detail(upper, null));

        // A name ending in a line feed passes the client library's own pattern ('$' matches before a final \n).
        url = null;
        var trailing = Throws(() => Ext().MssObject_GetSignedUrl(Key(email, pem), "Download", "my-bucket\n", "o", 5, "", out url));
        TestLog.Check("Bucket name ending in a line feed: Object_GetSignedUrl rejects it and returns no URL",
            trailing is ArgumentException && Has(trailing.Message, "is not a valid bucket name") && url == null, Detail(trailing, null));

        var longName = Throws(() => Ext().MssObject_Exists(Auth(), new string('a', 223), "o", out exists));
        TestLog.Check("Bucket name of 223 characters: rejected", longName is ArgumentException && Has(longName.Message, "is not a valid bucket name"), Detail(longName, null));

        // Control: every character class the library allows is accepted and reaches Google.
        backend = NewBackend(r => null);
        exists = false;
        var valid = Throws(() => Ext().MssObject_Exists(Auth(), "a.b-c_d1", "o", out exists));
        TestLog.Check("Bucket name 'a.b-c_d1' (dot, hyphen, underscore, digit) is accepted and the request is sent",
            valid == null && exists && Count(backend, IsMetaGet) == 1, Detail(valid, backend));
        valid = Throws(() => Ext().MssObject_Exists(Auth(), new string('a', 222), "o", out exists));
        TestLog.Check("Bucket name of 222 characters is accepted", valid == null, Detail(valid, backend));
    }

    // ---- 3. ContentType must be a sendable header value ------------------------------------------------

    private static void ContentTypes(string email, string pem)
    {
        string url;
        var none = new RLGCS_MetadataEntryRecordList();
        var backend = NewBackend(r => null);
        var bad = new[]
        {
            new KeyValuePair<string, string>("an accented letter", "text/plain; name=caf\u00E9"),
            new KeyValuePair<string, string>("a NUL", "text/plain\0"),
            new KeyValuePair<string, string>("a tab", "text/plain\tx"),
            new KeyValuePair<string, string>("a DEL", "text/plain\u007F")
        };
        foreach (var item in bad)
        {
            var ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "o", new byte[1], item.Value, none));
            TestLog.Check("Upload ContentType with " + item.Key + ": ArgumentException 'only printable ASCII'",
                ex is ArgumentException && Has(ex.Message, "ContentType must contain only printable ASCII characters"), Detail(ex, null));
            url = null;
            ex = Throws(() => Ext().MssObject_GetSignedUrl(Key(email, pem), "Upload", "my-bucket", "o", 5, item.Value, out url));
            TestLog.Check("GetSignedUrl ContentType with " + item.Key + ": ArgumentException 'only printable ASCII', no URL",
                ex is ArgumentException && Has(ex.Message, "ContentType must contain only printable ASCII characters") && url == null, Detail(ex, null));
        }
        TestLog.Check("Upload ContentType checks send no request (not retried, not reported as a network error)", Total(backend) == 0, Detail(null, backend));

        var breakEx = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "o", new byte[1], "text/plain\r\nX-Evil: 1", none));
        TestLog.Check("Upload ContentType with a line break keeps the 1.7.4 message 'must not contain line breaks'",
            breakEx is ArgumentException && Has(breakEx.Message, "ContentType must not contain line breaks"), Detail(breakEx, null));

        // Control: every printable ASCII character is accepted.
        url = null;
        var printable = new StringBuilder();
        for (char c = ' '; c <= '~'; c++) printable.Append(c);
        var ok = Throws(() => Ext().MssObject_GetSignedUrl(Key(email, pem), "Upload", "my-bucket", "o", 5, "application/json; charset=utf-8", out url));
        TestLog.Check("GetSignedUrl ContentType 'application/json; charset=utf-8' is accepted", ok == null && !string.IsNullOrEmpty(url), Detail(ok, null));
        var check = Sut.GetMethod("RequireHeaderValue", Static);
        Exception all = null;
        try { check.Invoke(null, new object[] { printable.ToString(), "ContentType" }); } catch (TargetInvocationException e) { all = e.InnerException; }
        TestLog.Check("All 95 printable ASCII characters (0x20-0x7E) pass the ContentType check", all == null, Detail(all, null));
    }

    // ---- 4. An assembly that cannot be loaded is not a network error -----------------------------------

    private static void AssemblyLoadErrors()
    {
        var transient = Sut.GetMethod("IsTransient", Static);
        var table = new[]
        {
            new KeyValuePair<Exception, bool>(new FileNotFoundException("Could not load file or assembly 'System.Memory'"), false),
            new KeyValuePair<Exception, bool>(new FileLoadException("Could not load file or assembly 'System.Buffers'"), false),
            new KeyValuePair<Exception, bool>(new DirectoryNotFoundException("no such directory"), false),
            new KeyValuePair<Exception, bool>(new IOException("Unable to read data from the transport connection"), true),
            new KeyValuePair<Exception, bool>(new EndOfStreamException("truncated"), true),
            new KeyValuePair<Exception, bool>(new HttpRequestException("reset"), true)
        };
        var wrong = table.Where(t => (bool)transient.Invoke(null, new object[] { t.Key }) != t.Value).Select(t => t.Key.GetType().Name).ToList();
        TestLog.Check("IsTransient: false for FileNotFoundException, FileLoadException and DirectoryNotFoundException; true for IOException, EndOfStreamException and HttpRequestException",
            wrong.Count == 0, string.Join(", ", wrong));

        foreach (var load in new Exception[] { new FileNotFoundException("Could not load file or assembly 'System.Memory, Version=4.0.1.2'"), new FileLoadException("Could not load file or assembly 'System.Buffers, Version=4.0.3.0'") })
        {
            byte[] content; string contentType;
            Exception thrown = load;
            var backend = NewBackend(r => { if (IsMedia(r)) throw thrown; return null; });
            var ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
            TestLog.Check("Download failing with " + load.GetType().Name + ": the error names the assembly, is not retried (1 media GET) and is not called a network error",
                ex != null && Has(UploadIntegrityTests.Chain(ex), "Could not load file or assembly") && Count(backend, IsMedia) == 1
                && !Has(ex.Message, "connection to Google Cloud was interrupted") && !Has(ex.Message, "Could not reach Google Cloud"), Detail(ex, backend));
        }
    }

    // ---- 5. One HTTP client factory, with the certificate check, for every Google client --------------

    private static void CollectHandlers(object handler, List<HttpClientHandler> found, HashSet<object> seen)
    {
        if (handler == null || !seen.Add(handler)) return;
        var client = handler as HttpClientHandler;
        if (client != null) { found.Add(client); return; }
        var delegating = handler as DelegatingHandler;
        if (delegating != null) CollectHandlers(delegating.InnerHandler, found, seen);
        for (Type t = handler.GetType(); t != null && t != typeof(object); t = t.BaseType)
            foreach (var field in t.GetFields(Instance | BindingFlags.DeclaredOnly))
                if (typeof(HttpMessageHandler).IsAssignableFrom(field.FieldType))
                    CollectHandlers(field.GetValue(handler), found, seen);
    }

    private static void HttpClientFactories(string pem)
    {
        FederationTests.SetFactory(null);
        var production = (IHttpClientFactory)Sut.GetProperty("GoogleHttpClientFactory", Static).GetValue(null, null);
        var handlers = new List<HttpClientHandler>();
        using (var client = production.CreateHttpClient(new CreateHttpClientArgs { GZipEnabled = true }))
            CollectHandlers(client.MessageHandler, handlers, new HashSet<object>());
        bool allCheck = handlers.Count > 0 && handlers.All(h => h.ServerCertificateCustomValidationCallback != null
            && h.ServerCertificateCustomValidationCallback(null, null, null, System.Net.Security.SslPolicyErrors.None)
            && !h.ServerCertificateCustomValidationCallback(null, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch)
            && !h.ServerCertificateCustomValidationCallback(null, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
        TestLog.Check("Production Google HTTP factory: every transport handler checks the certificate itself (accepts no error, rejects a name mismatch and a chain error)",
            allCheck, "handlers found: " + handlers.Count);

        // Key requests go through the same factory: with the test seam set, they reach the fake. The Storage
        // library signs in with a JWT it signs itself with the key (issuer and subject are the client e-mail,
        // with the scope as a claim), so there is no token request to Google at all.
        string email = UniqueEmail();
        var backend = NewBackend(r => null);
        bool exists = false;
        var ex = Throws(() => Ext().MssObject_Exists(Key(email, pem), "my-bucket", "docs/a.txt", out exists));
        var gets = Where(backend, IsMetaGet);
        string bearer = gets.Count == 1 && gets[0].Authorization != null && gets[0].Authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? gets[0].Authorization.Substring(7) : "";
        string[] jwt = bearer.Split('.');
        IDictionary<string, object> claims = null;
        if (jwt.Length == 3)
        {
            string payload = jwt[1].Replace('-', '+').Replace('_', '/');
            payload += new string('=', (4 - payload.Length % 4) % 4);
            claims = JsonConvert.DeserializeObject<Dictionary<string, object>>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        }
        TestLog.Check("Key sign-in: the request reaches the test seam, carrying a JWT signed with the key (iss and sub = ClientEmail, scope cloud-platform), with no token request to Google",
            ex == null && exists && claims != null && (string)claims["iss"] == email && (string)claims["sub"] == email
            && Has((string)claims["scope"], "https://www.googleapis.com/auth/cloud-platform") && Count(backend, IsGoogleTokenEndpoint) == 0 && Total(backend) == 1,
            Detail(ex, backend) + "; claims=" + (claims == null ? "none" : string.Join(",", claims.Keys)));

        backend = NewBackend(r => IsMetaGet(r) ? GoogleError(401, "Invalid Credentials") : null);
        ex = Throws(() => Ext().MssObject_Exists(Key(UniqueEmail(), pem), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("Key sign-in rejected by Google (401): 'rejected the request as unauthenticated. Check that ClientEmail and PrivateKey belong to the same service account'",
            ex != null && Has(ex.Message, "Google rejected the request as unauthenticated. Check that ClientEmail and PrivateKey belong to the same service account"), Detail(ex, backend));
    }

    // ---- 6. Bucket_Create and Bucket_Delete repeats ----------------------------------------------------

    private static void BucketCreateAndDelete()
    {
        // Delete: the first send deleted the bucket but answered 503; the repeat finds it gone.
        int deletes = 0;
        var backend = NewBackend(r => IsBucketDelete(r) ? (++deletes == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(404, "The specified bucket does not exist.")) : null);
        var ex = Throws(() => Ext().MssBucket_Delete(Auth(), "my-bucket"));
        TestLog.Check("Bucket_Delete: a 503 then a 404 on the repeat counts as deleted (no error, 2 DELETEs)", ex == null && Count(backend, IsBucketDelete) == 2, Detail(ex, backend));

        backend = NewBackend(r => IsBucketDelete(r) ? GoogleError(404, "The specified bucket does not exist.") : null);
        ex = Throws(() => Ext().MssBucket_Delete(Auth(), "my-bucket"));
        TestLog.Check("Bucket_Delete: a 404 on the first send is still \"Bucket 'my-bucket' does not exist\" (1 DELETE)",
            ex != null && ex.Message.StartsWith("Bucket 'my-bucket' does not exist", StringComparison.Ordinal) && Count(backend, IsBucketDelete) == 1, Detail(ex, backend));

        deletes = 0;
        backend = NewBackend(r => IsBucketDelete(r) ? (++deletes == 1 ? GoogleError(429, "refused") : GoogleError(404, "The specified bucket does not exist.")) : null);
        ex = Throws(() => Ext().MssBucket_Delete(Auth(), "my-bucket"));
        TestLog.Check("Bucket_Delete: a 429 (refused) then a 404 is still 'does not exist'",
            ex != null && ex.Message.StartsWith("Bucket 'my-bucket' does not exist", StringComparison.Ordinal), Detail(ex, backend));

        deletes = 0;
        backend = NewBackend(r => IsBucketDelete(r) ? (++deletes == 1 ? GoogleError(503, "Service Unavailable") : NoContent()) : null);
        ex = Throws(() => Ext().MssBucket_Delete(Auth(), "my-bucket"));
        TestLog.Check("Bucket_Delete: a 503 then success deletes the bucket (2 DELETEs)", ex == null && Count(backend, IsBucketDelete) == 2, Detail(ex, backend));

        backend = NewBackend(r => IsBucketDelete(r) ? GoogleError(503, "Service Unavailable") : null);
        ex = Throws(() => Ext().MssBucket_Delete(Auth(), "my-bucket"));
        TestLog.Check("Bucket_Delete: a 503 on every attempt stops after 3 DELETEs with the 503 error", ex != null && Has(ex.Message, "(503") && Count(backend, IsBucketDelete) == 3, Detail(ex, backend));

        // Create: the first send created the bucket but answered 503; the repeat is told the name is taken.
        string taken = "Your previous request to create the named bucket succeeded and you already own it.";
        Func<string, object> bucket = created => new { kind = "storage#bucket", name = "my-bucket", id = "my-bucket", timeCreated = created };
        int creates = 0;
        backend = NewBackend(r =>
        {
            if (IsBucketCreate(r)) return ++creates == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(409, taken);
            if (IsBucketGet(r)) return Json(bucket(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));
            return null;
        });
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        TestLog.Check("Bucket_Create: a 503 then a 409, with the bucket created just now, counts as created (no error, 2 POSTs, 1 bucket GET)",
            ex == null && Count(backend, IsBucketCreate) == 2 && Count(backend, IsBucketGet) == 1, Detail(ex, backend));

        creates = 0;
        backend = NewBackend(r =>
        {
            if (IsBucketCreate(r)) return ++creates == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(409, taken);
            if (IsBucketGet(r)) return Json(bucket("2020-01-01T00:00:00.000Z"));
            return null;
        });
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        TestLog.Check("Bucket_Create: a 503 then a 409, with a bucket created in 2020, is a Conflict (it existed before this call)",
            ex != null && ex.Message.StartsWith("Conflict:", StringComparison.Ordinal), Detail(ex, backend));

        // A bucket created shortly before the call existed before it: only one minute is allowed for clock difference.
        foreach (int secondsBefore in new[] { 180, 30 })
        {
            creates = 0;
            string createdAt = DateTime.UtcNow.AddSeconds(-secondsBefore).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            backend = NewBackend(r =>
            {
                if (IsBucketCreate(r)) return ++creates == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(409, taken);
                if (IsBucketGet(r)) return Json(bucket(createdAt));
                return null;
            });
            ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
            if (secondsBefore > 60)
                TestLog.Check("Bucket_Create: a 503 then a 409, with a bucket created 3 minutes before the call, is a Conflict",
                    ex != null && ex.Message.StartsWith("Conflict:", StringComparison.Ordinal), Detail(ex, backend));
            else
                TestLog.Check("Bucket_Create: a 503 then a 409, with a creation time 30 seconds before the call (within the 1 minute allowed for clock difference), counts as created",
                    ex == null, Detail(ex, backend));
        }

        creates = 0;
        backend = NewBackend(r =>
        {
            if (IsBucketCreate(r)) return ++creates == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(409, "The requested bucket name is not available.");
            if (IsBucketGet(r)) return GoogleError(403, "Forbidden");
            return null;
        });
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        TestLog.Check("Bucket_Create: a 503 then a 409, with a bucket these credentials cannot read, is a Conflict (someone else's bucket)",
            ex != null && ex.Message.StartsWith("Conflict:", StringComparison.Ordinal), Detail(ex, backend));

        backend = NewBackend(r =>
        {
            if (IsBucketCreate(r)) return GoogleError(409, taken);
            if (IsBucketGet(r)) return Json(bucket(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));
            return null;
        });
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        TestLog.Check("Bucket_Create: a 409 on the first send is a Conflict, without asking about the bucket (1 POST, 0 bucket GETs)",
            ex != null && ex.Message.StartsWith("Conflict:", StringComparison.Ordinal) && Count(backend, IsBucketCreate) == 1 && Count(backend, IsBucketGet) == 0, Detail(ex, backend));

        creates = 0;
        backend = NewBackend(r =>
        {
            if (IsBucketCreate(r)) return ++creates == 1 ? GoogleError(429, "refused") : GoogleError(409, taken);
            if (IsBucketGet(r)) return Json(bucket(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));
            return null;
        });
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        TestLog.Check("Bucket_Create: a 429 (refused) then a 409 is a Conflict, without asking about the bucket",
            ex != null && ex.Message.StartsWith("Conflict:", StringComparison.Ordinal) && Count(backend, IsBucketGet) == 0, Detail(ex, backend));

        creates = 0;
        backend = NewBackend(r => IsBucketCreate(r) ? (++creates == 1 ? GoogleError(503, "Service Unavailable") : Json(bucket("2026-01-01T00:00:00.000Z"))) : null);
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU"));
        var posts = Where(backend, IsBucketCreate);
        TestLog.Check("Bucket_Create: a 503 then success creates the bucket (2 POSTs, both for project 'proj' with the name and location)",
            ex == null && posts.Count == 2 && posts.All(p => Query(p.Uri, "project") == "proj" && Has(p.Body, "\"name\":\"my-bucket\"") && Has(p.Body, "\"location\":\"EU\"")), Detail(ex, backend));
    }

    // ---- 7. Error details: plain-text bodies, a page in place of Google's answer, the 429 label -------

    private static void ErrorDetails()
    {
        bool exists = true;
        // A 404 whose body is plain text: Google's own wording decides, with no extra listing request.
        var backend = NewBackend(r => IsMetaGet(r) ? Text(404, "No such object: my-bucket/docs/a.txt", "text/plain") : null);
        var ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("Exists: a plain-text 404 'No such object' gives Exists = False with no listing request",
            ex == null && !exists && Count(backend, IsList) == 0, Detail(ex, backend));

        backend = NewBackend(r => IsMetaGet(r) ? Text(404, "The specified bucket does not exist.", "text/plain") : null);
        ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("Exists: a plain-text 404 'The specified bucket does not exist.' is reported as the missing bucket, with no listing request",
            ex != null && ex.Message.StartsWith("Bucket 'my-bucket' does not exist", StringComparison.Ordinal) && Count(backend, IsList) == 0, Detail(ex, backend));

        backend = NewBackend(r => IsMetaGet(r) ? Text(403, "Request blocked by organization policy ORG-POLICY-17.", "text/plain") : null);
        ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A plain-text 403 body reaches the message ('Access denied ... Details: Request blocked by organization policy ORG-POLICY-17.')",
            ex != null && Has(ex.Message, "Access denied") && Has(ex.Message, "Details: Request blocked by organization policy ORG-POLICY-17."), Detail(ex, backend));

        string page = "<html><body>" + new string('x', 5000) + "</body></html>";
        backend = NewBackend(r => IsMetaGet(r) ? Text(403, page, "text/html") : null);
        ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A 5,000-character non-JSON error body is cut: the message stays under 700 characters",
            ex != null && Has(ex.Message, "Access denied") && ex.Message.Length < 700, ex == null ? "no exception" : "length " + ex.Message.Length);

        // A 200 that is not Google's JSON: a proxy or sign-in page answered.
        backend = NewBackend(r => IsMetaGet(r) ? Text(200, "<html><body>Sign in to the corporate proxy</body></html>", "text/html") : null);
        ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A 200 with an HTML page: 'The answer to the request was not from Google Cloud Storage', not 'rejected ... before it was sent'",
            ex != null && ex.Message.StartsWith("The answer to the request was not from Google Cloud Storage", StringComparison.Ordinal) && !Has(ex.Message, "before it was sent"), Detail(ex, backend));

        // .NET Framework has no name for status 429.
        backend = NewBackend(r => IsDelete(r) ? GoogleError(429, "Rate limit exceeded") : null);
        ex = Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("A 429 on every attempt: 'Google Cloud Storage error (429 TooManyRequests)', not '429 429'",
            ex != null && Has(ex.Message, "Google Cloud Storage error (429 TooManyRequests): Rate limit exceeded") && !Has(ex.Message, "429 429"), Detail(ex, backend));

        backend = NewBackend(r => IsDelete(r) ? GoogleError(418, "teapot") : null);
        ex = Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("A status .NET has no name for (418) is shown once: '(418)'", ex != null && Has(ex.Message, "Google Cloud Storage error (418): teapot"), Detail(ex, backend));

        backend = NewBackend(r => IsDelete(r) ? GoogleError(410, "gone") : null);
        ex = Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("A named status keeps its name: '(410 Gone)'", ex != null && Has(ex.Message, "Google Cloud Storage error (410 Gone): gone"), Detail(ex, backend));
    }

    // ---- 8. Object_Download reads the size first --------------------------------------------------------

    private static void DownloadSizeRead()
    {
        var data = Sample(1000);
        var cap = Sut.GetField("MaxDownloadBytes", Static);
        byte[] content = null; string contentType = null;
        Exception ex;

        // Over the limit by its stored size: refused before any content is requested.
        var backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("1000", null)); if (IsMedia(r)) return Media(data); return null; });
        cap.SetValue(null, 100L);
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { cap.SetValue(null, 0x7FFFFFC7L); }
        TestLog.Check("Download: an object of 1,000 bytes over a 100-byte limit fails with 'larger than' before any content request (1 metadata GET, 0 media GETs)",
            ex != null && Has(ex.Message, "is larger than") && Count(backend, IsMetaGet) == 1 && Count(backend, IsMedia) == 0 && content == null, Detail(ex, backend));

        // The size is only a hint: an object replaced by a larger one after the read still downloads in full.
        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("5", null)); if (IsMedia(r)) return Media(data); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: metadata says 5 bytes, the content is 1,000 bytes (replaced after the read): all 1,000 bytes come back",
            ex == null && content != null && content.SequenceEqual(data), Detail(ex, backend));

        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("4000", null)); if (IsMedia(r)) return Media(data); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: metadata says 4,000 bytes, the content is 1,000 bytes (replaced by a smaller one): exactly 1,000 bytes come back",
            ex == null && content != null && content.Length == 1000 && content.SequenceEqual(data), Detail(ex, backend));

        // An object stored gzip-compressed is served decompressed: its stored size is not its delivered size.
        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("10", "gzip")); if (IsMedia(r)) return Media(data); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: a gzip-stored object of 10 stored bytes that is served as 1,000 bytes comes back in full",
            ex == null && content != null && content.SequenceEqual(data), Detail(ex, backend));

        // The exact size: the same bytes, and the content type is the download's, not the metadata read's.
        content = null; contentType = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("1000", null)); if (IsMedia(r)) return Media(data); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: with the exact size, the 1,000 bytes come back and ContentType is the download's ('application/octet-stream'), not the metadata read's ('text/plain')",
            ex == null && content != null && content.SequenceEqual(data) && contentType == "application/octet-stream", Detail(ex, backend) + "; contentType=" + contentType);

        // 3 MB through a 1 MB read buffer: still one request, byte for byte.
        var big = Sample(3 * 1024 * 1024 + 17);
        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson(big.Length.ToString(), null)); if (IsMedia(r)) return Media(big); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        var media = Where(backend, IsMedia);
        TestLog.Check("Download: 3 MB + 17 bytes come back unchanged in one media GET with no Range header",
            ex == null && content != null && content.SequenceEqual(big) && media.Count == 1 && !media[0].Headers.ContainsKey("range"), Detail(ex, backend));

        // Empty object.
        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("0", null)); if (IsMedia(r)) return Media(new byte[0]); return null; });
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: an empty object comes back as 0 bytes", ex == null && content != null && content.Length == 0, Detail(ex, backend));

        // A missing object is reported from the size read; no content request is made.
        backend = NewBackend(r => IsMetaGet(r) ? GoogleError(404, "No such object: my-bucket/docs/d.bin") : null);
        ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: a missing object fails with \"Object 'docs/d.bin' was not found\" and sends no media GET",
            ex != null && ex.Message.StartsWith("Object 'docs/d.bin' was not found", StringComparison.Ordinal) && Count(backend, IsMedia) == 0, Detail(ex, backend));
    }

    // ---- 9. Download stall guard ------------------------------------------------------------------------

    /// <summary>Delivers its first bytes, then never completes a read until it is closed, like a stalled connection.</summary>
    private sealed class StallingStream : Stream
    {
        private readonly byte[] _first;
        private int _position;
        private readonly TaskCompletionSource<int> _never = new TaskCompletionSource<int>();
        private readonly bool _closeEndsRead;
        public volatile bool Closed;

        /// <param name="first">The bytes delivered before the stall.</param>
        /// <param name="closeEndsRead">False for a body whose pending read stays pending even after it is closed.</param>
        public StallingStream(byte[] first, bool closeEndsRead = true) { _first = first; _closeEndsRead = closeEndsRead; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // The token is ignored on purpose: .NET Framework's response stream does not observe it mid-read.
            if (_position < _first.Length)
            {
                int n = Math.Min(count, _first.Length - _position);
                Array.Copy(_first, _position, buffer, offset, n);
                _position += n;
                return Task.FromResult(n);
            }
            return _never.Task;
        }

        public override int Read(byte[] buffer, int offset, int count) { return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult(); }
        protected override void Dispose(bool disposing) { Closed = true; if (_closeEndsRead) _never.TrySetException(new ObjectDisposedException("response stream")); base.Dispose(disposing); }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    /// <summary>Delivers the data in small pieces with a pause before each: slow, but never stalled.</summary>
    private sealed class SlowStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _piece, _pauseMilliseconds;
        private int _position;

        public SlowStream(byte[] data, int piece, int pauseMilliseconds) { _data = data; _piece = piece; _pauseMilliseconds = pauseMilliseconds; }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _data.Length) return 0;
            await Task.Delay(_pauseMilliseconds).ConfigureAwait(false);
            int n = Math.Min(Math.Min(count, _piece), _data.Length - _position);
            Array.Copy(_data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count) { return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult(); }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    private static void DownloadStall()
    {
        var stall = Sut.GetField("DownloadStallTimeout", Static);
        TestLog.Check("Download stall limit is 60 seconds by default", (TimeSpan)stall.GetValue(null) == TimeSpan.FromSeconds(60), stall.GetValue(null).ToString());

        byte[] content = null; string contentType;
        Exception ex;
        StallingStream body = null;
        var backend = NewBackend(r => { if (IsMedia(r)) { body = new StallingStream(Sample(10)); return MediaFrom(body, null); } return null; });
        var watch = Stopwatch.StartNew();
        stall.SetValue(null, TimeSpan.FromSeconds(1));
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { stall.SetValue(null, TimeSpan.FromSeconds(60)); watch.Stop(); }
        TestLog.Check("Download: a body that stops after 10 bytes fails with 'stalled: Google Cloud sent no data for 1 seconds' after about 1 second",
            ex != null && Has(ex.Message, "stalled: Google Cloud sent no data for 1 seconds") && Has(ex.Message, "'docs/d.bin'") && watch.Elapsed >= TimeSpan.FromMilliseconds(900) && watch.Elapsed < TimeSpan.FromSeconds(10),
            Detail(ex, backend) + "; elapsed=" + (int)watch.Elapsed.TotalMilliseconds + " ms");
        TestLog.Check("Download: the stalled download is not repeated (1 media GET), returns no content and closes the connection",
            Count(backend, IsMedia) == 1 && content == null && body != null && body.Closed, Detail(ex, backend));

        // Even when closing the body does not end the pending read, the action returns.
        body = null;
        backend = NewBackend(r => { if (IsMedia(r)) { body = new StallingStream(Sample(10), false); return MediaFrom(body, null); } return null; });
        watch = Stopwatch.StartNew();
        stall.SetValue(null, TimeSpan.FromSeconds(1));
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { stall.SetValue(null, TimeSpan.FromSeconds(60)); watch.Stop(); }
        TestLog.Check("Download: a stalled body whose read never ends, even after it is closed, still fails with 'stalled' after about 1 second",
            ex != null && Has(ex.Message, "stalled: Google Cloud sent no data for 1 seconds") && watch.Elapsed < TimeSpan.FromSeconds(10) && body != null && body.Closed,
            Detail(ex, backend) + "; elapsed=" + (int)watch.Elapsed.TotalMilliseconds + " ms");

        // An error answer whose body stalls is guarded too.
        body = null;
        backend = NewBackend(r =>
        {
            if (!IsMedia(r)) return null;
            body = new StallingStream(Encoding.UTF8.GetBytes("{\"error\":"));
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StreamContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });
        watch = Stopwatch.StartNew();
        stall.SetValue(null, TimeSpan.FromSeconds(1));
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { stall.SetValue(null, TimeSpan.FromSeconds(60)); watch.Stop(); }
        TestLog.Check("Download: an error answer whose body stalls also fails with 'stalled' instead of hanging",
            ex != null && Has(ex.Message, "stalled") && watch.Elapsed < TimeSpan.FromSeconds(10) && body != null && body.Closed, Detail(ex, backend) + "; elapsed=" + (int)watch.Elapsed.TotalMilliseconds + " ms");

        // Slow but steady: 12 pieces 250 ms apart take about 3 seconds in all, three times the 1-second limit.
        var data = Sample(1200);
        content = null;
        backend = NewBackend(r => { if (IsMedia(r)) return MediaFrom(new SlowStream(data, 100, 250), data); return null; });
        watch = Stopwatch.StartNew();
        stall.SetValue(null, TimeSpan.FromSeconds(1));
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { stall.SetValue(null, TimeSpan.FromSeconds(60)); watch.Stop(); }
        TestLog.Check("Download: a slow body (12 pieces, 250 ms apart, about 3 s in all) under a 1-second stall limit completes: the limit is per wait, not for the whole download",
            ex == null && content != null && content.SequenceEqual(data) && watch.Elapsed >= TimeSpan.FromSeconds(2.5), Detail(ex, backend) + "; elapsed=" + (int)watch.Elapsed.TotalMilliseconds + " ms");

        // Only object downloads are wrapped.
        bool exists = false;
        backend = NewBackend(r => null);
        ex = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("Control: a metadata request is unaffected by the stall guard", ex == null && exists, Detail(ex, backend));
    }

    // ---- 10. Network failures are repeated on every action ---------------------------------------------

    private static void NetworkRetries()
    {
        bool exists = false; long deleted;
        RLGCS_ObjectRecordList objects; string next; RLGCS_PrefixRecordList prefixes; RLGCS_BucketRecordList buckets;
        RCGCS_ObjectMetadataRecord metadata; RLGCS_MetadataEntryRecordList custom;
        var none = new RLGCS_MetadataEntryRecordList();
        var rewriteDone = new { kind = "storage#rewriteResponse", done = true, resource = new { kind = "storage#object", bucket = "dst-bucket", name = "out/x.txt", generation = "5000", metageneration = "1" } };
        var bucketList = new { kind = "storage#buckets", items = new[] { new { kind = "storage#bucket", name = "b1", id = "b1" } } };

        // One reset connection on the first request of each action, then normal answers.
        var cases = new List<Tuple<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>>
        {
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_Exists", IsMetaGet, () => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists), null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_GetMetadata", IsMetaGet, () => Ext().MssObject_GetMetadata(Auth(), "my-bucket", "docs/a.txt", out exists, out metadata, out custom), null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Bucket_Exists", IsBucketGet, () => Ext().MssBucket_Exists(Auth(), "my-bucket", out exists), null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Bucket_List", IsBucketList, () => Ext().MssBucket_List(Auth(), out buckets), r => IsBucketList(r) ? Json(bucketList) : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_List", IsList, () => Ext().MssObject_List(Auth(), "my-bucket", "", 0, "", "", out objects, out next, out prefixes), r => IsList(r) ? Json(new { kind = "storage#objects" }) : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_Copy", IsRewrite, () => Ext().MssObject_Copy(Auth(), "src-bucket", "in/x.txt", "dst-bucket", "out/x.txt"), r => IsRewrite(r) ? Json(rewriteDone) : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_UpdateMetadata (its read)", IsMetaGet, () => Ext().MssObject_UpdateMetadata(Auth(), "my-bucket", "docs/a.txt", "text/csv", "", "", "", none), null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_Delete (its read)", IsMetaGet, () => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"), r => IsDelete(r) ? NoContent() : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_Move (its read)", IsMetaGet, () => Ext().MssObject_Move(Auth(), "src-bucket", "in/x.txt", "dst-bucket", "out/x.txt"), r => IsRewrite(r) ? Json(rewriteDone) : IsDelete(r) ? NoContent() : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_Move (its copy)", IsRewrite, () => Ext().MssObject_Move(Auth(), "src-bucket", "in/x.txt", "dst-bucket", "out/x.txt"), r => IsRewrite(r) ? Json(rewriteDone) : IsDelete(r) ? NoContent() : null),
            Tuple.Create<string, Func<Rec, bool>, Action, Func<Rec, HttpResponseMessage>>("Object_DeleteByPrefix (its listing)", IsList, () => Ext().MssObject_DeleteByPrefix(Auth(), "my-bucket", "p/", out deleted), r => IsList(r) ? Json(new { kind = "storage#objects" }) : null)
        };
        foreach (var c in cases)
        {
            int hits = 0;
            Func<Rec, bool> target = c.Item2;
            Func<Rec, HttpResponseMessage> rest = c.Item4;
            var backend = NewBackend(r =>
            {
                if (target(r) && ++hits == 1) throw new HttpRequestException("The connection was reset");
                return rest == null ? null : rest(r);
            });
            var ex = Throws(c.Item3);
            TestLog.Check(c.Item1 + ": one reset connection is repeated and the action succeeds (2 requests)", ex == null && Count(backend, target) == 2, Detail(ex, backend));
        }

        // A failure on every attempt: three sends, then the network message.
        var down = NewBackend(r => { if (IsMetaGet(r)) throw new HttpRequestException("The remote name could not be resolved: 'storage.googleapis.com'"); return null; });
        var downEx = Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("Object_Exists: a network failure on every attempt stops after 3 requests with 'Could not reach Google Cloud'",
            downEx != null && downEx.Message.StartsWith("Could not reach Google Cloud:", StringComparison.Ordinal) && Count(down, IsMetaGet) == 3, Detail(downEx, down));

        // A listing of two pages: a failure on the second page repeats that page only.
        int listed = 0;
        var paged = NewBackend(r =>
        {
            if (!IsList(r)) return null;
            listed++;
            if (Query(r.Uri, "pageToken") == null) return Json(new { kind = "storage#objects", nextPageToken = "page-2", items = new[] { new { kind = "storage#object", name = "a.txt", bucket = "my-bucket", generation = "1", size = "1" } } });
            if (listed == 2) throw new HttpRequestException("The connection was reset");
            return Json(new { kind = "storage#objects", items = new[] { new { kind = "storage#object", name = "b.txt", bucket = "my-bucket", generation = "2", size = "1" } } });
        });
        objects = null; next = null;
        var pagedEx = Throws(() => Ext().MssObject_List(Auth(), "my-bucket", "", 0, "", "", out objects, out next, out prefixes));
        var lists = Where(paged, IsList);
        TestLog.Check("Object_List (all pages): a reset on page 2 repeats page 2 only (3 requests: page 1 once, page 2 twice) and returns both objects once",
            pagedEx == null && objects != null && objects.Length == 2 && lists.Count == 3 && Query(lists[0].Uri, "pageToken") == null
            && Query(lists[1].Uri, "pageToken") == "page-2" && Query(lists[2].Uri, "pageToken") == "page-2" && next == "", Detail(pagedEx, paged));

        // Paged mode still returns exactly one page and its token.
        var one = NewBackend(r => IsList(r) ? Json(new { kind = "storage#objects", nextPageToken = "page-2", items = new[] { new { kind = "storage#object", name = "a.txt", bucket = "my-bucket", generation = "1", size = "1" } } }) : null);
        objects = null; next = null;
        var oneEx = Throws(() => Ext().MssObject_List(Auth(), "my-bucket", "p/", 1, "page-1", "/", out objects, out next, out prefixes));
        var oneList = Where(one, IsList);
        TestLog.Check("Object_List (MaxResults 1): one request carrying maxResults=1, pageToken, prefix and delimiter; NextPageToken 'page-2' is returned",
            oneEx == null && objects != null && objects.Length == 1 && next == "page-2" && oneList.Count == 1 && Query(oneList[0].Uri, "maxResults") == "1"
            && Query(oneList[0].Uri, "pageToken") == "page-1" && Query(oneList[0].Uri, "prefix") == "p/" && Query(oneList[0].Uri, "delimiter") == "/", Detail(oneEx, one));
    }

    // ---- 11. Every transient status is repeated by the extension's own retry ---------------------------

    private static void TransientStatuses()
    {
        var data = Encoding.UTF8.GetBytes("retried content");
        foreach (int status in new[] { 408, 429, 500, 502, 503, 504 })
        {
            byte[] content = null; string contentType;
            int gets = 0;
            var backend = NewBackend(r => { if (IsMedia(r)) return ++gets == 1 ? GoogleError(status, "transient") : Media(data); return null; });
            var ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
            TestLog.Check("Download: one " + status + " is repeated and the content comes back (2 media GETs)",
                ex == null && content != null && content.SequenceEqual(data) && Count(backend, IsMedia) == 2, Detail(ex, backend));

            int starts = 0;
            backend = NewBackend(r => IsSessionStart(r) && ++starts == 1 ? GoogleError(status, "transient") : null);
            ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/u.bin", data, "text/plain", new RLGCS_MetadataEntryRecordList()));
            byte[] stored;
            lock (backend.StoredObjects) backend.StoredObjects.TryGetValue("my-bucket/docs/u.bin", out stored);
            TestLog.Check("Upload: one " + status + " on the request that opens the session is repeated and the bytes are stored",
                ex == null && stored != null && stored.SequenceEqual(data) && Count(backend, IsSessionStart) == 2, Detail(ex, backend));
        }

        // 401 is left out: Google's credential code answers a 401 by signing in again and repeating the request once.
        foreach (int status in new[] { 400, 403, 404, 409 })
        {
            byte[] content; string contentType;
            var backend = NewBackend(r => IsMedia(r) ? GoogleError(status, "permanent") : null);
            var ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
            TestLog.Check("Download: a " + status + " is not repeated (1 media GET)", ex != null && Count(backend, IsMedia) == 1, Detail(ex, backend));
        }
    }

    // ---- 12. Object_DeleteByPrefix: repeats and the count ----------------------------------------------

    private static void DeleteByPrefixRepeats()
    {
        var listing = new
        {
            kind = "storage#objects",
            items = new[]
            {
                new { kind = "storage#object", name = "p/a.txt", bucket = "my-bucket", generation = "11" },
                new { kind = "storage#object", name = "p/b.txt", bucket = "my-bucket", generation = "12" },
                new { kind = "storage#object", name = "p/c.txt", bucket = "my-bucket", generation = "13" }
            }
        };
        Func<Rec, string> nameOf = r => Uri.UnescapeDataString(Segments(r)[5]);

        // a: lost answer, then 404 on the repeat (this call deleted it). b: 503, then 412 on the repeat (same). c: plain success.
        var sends = new Dictionary<string, int>();
        var backend = NewBackend(r =>
        {
            if (IsList(r)) return Json(listing);
            if (!IsDelete(r)) return null;
            string name = nameOf(r);
            int n; sends.TryGetValue(name, out n); sends[name] = ++n;
            if (name == "p/a.txt") { if (n == 1) throw new HttpRequestException("The connection was reset"); return GoogleError(404, "No such object: my-bucket/p/a.txt"); }
            if (name == "p/b.txt") return n == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(412, Precondition);
            return NoContent();
        });
        long deleted = -1;
        var ex = Throws(() => Ext().MssObject_DeleteByPrefix(Auth(), "my-bucket", "p/", out deleted));
        var ds = Where(backend, IsDelete);
        TestLog.Check("DeleteByPrefix: objects whose repeated delete finds them gone (404) or changed (412) were deleted by this call: DeletedCount = 3",
            ex == null && deleted == 3 && ds.Count == 5, Detail(ex, backend) + "; deleted=" + deleted);
        TestLog.Check("DeleteByPrefix: every delete carries the listed generation (11, 12, 13)",
            ds.All(r => Query(r.Uri, "ifGenerationMatch") == (nameOf(r) == "p/a.txt" ? "11" : nameOf(r) == "p/b.txt" ? "12" : "13")), Detail(null, backend));

        // First-send 404 and 412 are still skipped and not counted; a refused send (429) does not change that.
        sends.Clear();
        backend = NewBackend(r =>
        {
            if (IsList(r)) return Json(listing);
            if (!IsDelete(r)) return null;
            string name = nameOf(r);
            int n; sends.TryGetValue(name, out n); sends[name] = ++n;
            if (name == "p/a.txt") return GoogleError(404, "No such object: my-bucket/p/a.txt");
            if (name == "p/b.txt") return n == 1 ? GoogleError(429, "refused") : GoogleError(412, Precondition);
            return NoContent();
        });
        deleted = -1;
        ex = Throws(() => Ext().MssObject_DeleteByPrefix(Auth(), "my-bucket", "p/", out deleted));
        TestLog.Check("DeleteByPrefix: an object already gone (404) and one replaced after the listing (412, after a refused 429) are skipped: DeletedCount = 1",
            ex == null && deleted == 1, Detail(ex, backend) + "; deleted=" + deleted);

        // A failure on every attempt reports the progress.
        backend = NewBackend(r =>
        {
            if (IsList(r)) return Json(listing);
            if (IsDelete(r) && nameOf(r) == "p/b.txt") throw new HttpRequestException("The connection was reset");
            return IsDelete(r) ? NoContent() : null;
        });
        ex = Throws(() => Ext().MssObject_DeleteByPrefix(Auth(), "my-bucket", "p/", out deleted));
        TestLog.Check("DeleteByPrefix: a network failure on all 3 attempts for the second object fails with \"Deleted 1 of 3 objects under prefix 'p/', then failed on 'p/b.txt'\"",
            ex != null && ex.Message.StartsWith("Deleted 1 of 3 objects under prefix 'p/', then failed on 'p/b.txt': Could not reach Google Cloud", StringComparison.Ordinal)
            && Count(backend, r => IsDelete(r) && nameOf(r) == "p/b.txt") == 3, Detail(ex, backend));
    }

    // ---- 13. Signing through signBlob is repeated on a transient failure -------------------------------

    private static void SignedUrlRepeat()
    {
        string url = null;
        int signs = 0;
        var backend = NewBackend(r => IsSignBlob(r) && ++signs == 1 ? GoogleError(503, "Service Unavailable") : null);
        var ex = Throws(() => Ext().MssObject_GetSignedUrl(Auth(), "Download", "my-bucket", "docs/a.txt", 5, "", out url));
        TestLog.Check("GetSignedUrl (federation): one 503 from signBlob is repeated (by Google's credential code) and the URL is returned (2 signBlob requests)",
            ex == null && !string.IsNullOrEmpty(url) && Has(url, "X-Goog-Signature=") && Count(backend, IsSignBlob) == 2, Detail(ex, backend));

        signs = 0;
        backend = NewBackend(r => { if (IsSignBlob(r) && ++signs == 1) throw new HttpRequestException("The connection was reset"); return null; });
        url = null;
        ex = Throws(() => Ext().MssObject_GetSignedUrl(Auth(), "Download", "my-bucket", "docs/a.txt", 5, "", out url));
        TestLog.Check("GetSignedUrl (federation): one reset connection to signBlob is repeated and the URL is returned (2 signBlob requests)",
            ex == null && !string.IsNullOrEmpty(url) && Count(backend, IsSignBlob) == 2, Detail(ex, backend));

        signs = 0;
        backend = NewBackend(r => IsSignBlob(r) && ++signs == 1 ? GoogleError(429, "Rate limit exceeded") : null);
        url = null;
        ex = Throws(() => Ext().MssObject_GetSignedUrl(Auth(), "Download", "my-bucket", "docs/a.txt", 5, "", out url));
        TestLog.Check("GetSignedUrl (federation): one 429 from signBlob is repeated and the URL is returned (2 signBlob requests)",
            ex == null && !string.IsNullOrEmpty(url) && Count(backend, IsSignBlob) == 2, Detail(ex, backend));

        // A signBlob request that never connects is given up after exactly 3 requests.
        backend = NewBackend(r => { if (IsSignBlob(r)) throw new HttpRequestException("The connection was reset"); return null; });
        url = null;
        ex = Throws(() => Ext().MssObject_GetSignedUrl(Auth(), "Download", "my-bucket", "docs/a.txt", 5, "", out url));
        TestLog.Check("GetSignedUrl (federation): a signBlob request that never connects fails with 'Could not reach Google Cloud' after exactly 3 requests",
            ex != null && ex.Message.StartsWith("Could not reach Google Cloud:", StringComparison.Ordinal) && Count(backend, IsSignBlob) == 3 && url == null, Detail(ex, backend) + "; signBlob requests=" + Count(backend, IsSignBlob));

        // The extension's retry and the library's do not multiply: the extension does not treat a 503 from signBlob as its own to repeat.
        var notRepeated = Sut.GetMethod("IsTransientNotRepeatedBySigner", Static);
        Func<int, Exception> api = code => new Google.GoogleApiException("iamcredentials", "x") { HttpStatusCode = (HttpStatusCode)code };
        var table = new[]
        {
            new KeyValuePair<Exception, bool>(api(408), true), new KeyValuePair<Exception, bool>(api(429), true),
            new KeyValuePair<Exception, bool>(api(500), false), new KeyValuePair<Exception, bool>(api(502), false),
            new KeyValuePair<Exception, bool>(api(503), false), new KeyValuePair<Exception, bool>(api(504), false),
            new KeyValuePair<Exception, bool>(api(403), false), new KeyValuePair<Exception, bool>(new HttpRequestException("reset"), true)
        };
        var wrong = table.Where(t => (bool)notRepeated.Invoke(null, new object[] { t.Key }) != t.Value)
            .Select(t => t.Key is Google.GoogleApiException ? ((int)((Google.GoogleApiException)t.Key).HttpStatusCode).ToString() : t.Key.GetType().Name).ToList();
        TestLog.Check("Signing: the extension repeats a network failure, a 408 and a 429 itself, and leaves 500, 502, 503 and 504 to Google's credential code (which repeats them)",
            wrong.Count == 0, string.Join(", ", wrong));

        backend = NewBackend(r => IsSignBlob(r) ? GoogleError(403, "Permission 'iam.serviceAccounts.signBlob' denied.") : null);
        ex = Throws(() => Ext().MssObject_GetSignedUrl(Auth(), "Download", "my-bucket", "docs/a.txt", 5, "", out url));
        TestLog.Check("GetSignedUrl (federation): a 403 from signBlob is not repeated (1 request) and names 'Service Account Token Creator'",
            ex != null && Has(ex.Message, "Service Account Token Creator") && Count(backend, IsSignBlob) == 1, Detail(ex, backend));
    }

    // ---- 14. Federation inputs are checked before anything is cached or sent ----------------------------

    private static int CacheCount(string field)
    {
        object cache = Sut.GetField(field, Static).GetValue(null);
        return (int)cache.GetType().GetProperty("Count").GetValue(cache, null);
    }

    private static void FederationInputs()
    {
        bool exists;
        var backend = NewBackend(r => null);
        int identitiesBefore = CacheCount("FederatedIdentityCache"), clientsBefore = CacheCount("StorageClientCache");

        var notJwt = new[]
        {
            new KeyValuePair<string, string>("plain text", "this is not a token"),
            new KeyValuePair<string, string>("two parts", "header.payload"),
            new KeyValuePair<string, string>("four parts", "a.b.c.d"),
            new KeyValuePair<string, string>("an empty part", "header..signature"),
            new KeyValuePair<string, string>("a 'Bearer ' prefix", "Bearer header.payload.signature"),
            new KeyValuePair<string, string>("a line break inside", "header.pay\nload.signature"),
            new KeyValuePair<string, string>("a '+' (base64, not base64url)", "head+er.payload.signature")
        };
        foreach (var item in notJwt)
        {
            var auth = Auth();
            auth.ssSTGCS_Authentication.ssSubjectToken = item.Value;
            var ex = Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
            TestLog.Check("SubjectToken with " + item.Key + ": ArgumentException 'is not a JWT', and the token is not in the message",
                ex is ArgumentException && Has(ex.Message, "The SubjectToken is not a JWT") && !Has(ex.Message, item.Value), Detail(ex, null));
        }

        // Many distinct garbage tokens, and a very long one: nothing is cached, nothing is sent.
        for (int i = 0; i < 50; i++)
        {
            var auth = Auth();
            auth.ssSTGCS_Authentication.ssSubjectToken = "garbage token number " + i;
            Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
        }
        var longAuth = Auth();
        longAuth.ssSTGCS_Authentication.ssSubjectToken = new string('a', 20000) + "." + new string('b', 20000) + ".c";
        var longEx = Throws(() => Ext().MssBucket_Exists(longAuth, "my-bucket", out exists));
        TestLog.Check("SubjectToken of 40,003 characters: ArgumentException 'is 40003 characters long'",
            longEx is ArgumentException && Has(longEx.Message, "The SubjectToken is 40003 characters long") && longEx.Message.Length < 300, Detail(longEx, null));
        TestLog.Check("58 rejected SubjectTokens leave the credential caches unchanged and send no request",
            CacheCount("FederatedIdentityCache") == identitiesBefore && CacheCount("StorageClientCache") == clientsBefore && Total(backend) == 0,
            "identities " + identitiesBefore + " -> " + CacheCount("FederatedIdentityCache") + ", clients " + clientsBefore + " -> " + CacheCount("StorageClientCache") + ", requests " + Total(backend));

        // Control: a token at the length limit, in JWT shape with padding characters, is accepted.
        var okAuth = Auth();
        okAuth.ssSTGCS_Authentication.ssSubjectToken = new string('a', 32768 - 9) + ".b-_=.c12";
        var overAuth = Auth();
        overAuth.ssSTGCS_Authentication.ssSubjectToken = new string('a', 32768 - 8) + ".b-_=.c12";
        var overEx = Throws(() => Ext().MssBucket_Exists(overAuth, "my-bucket", out exists));
        TestLog.Check("SubjectToken of 32,769 characters (one over the limit) is rejected", overEx is ArgumentException && Has(overEx.Message, "is 32769 characters long"), Detail(overEx, null));
        var okEx = Throws(() => Ext().MssBucket_Exists(okAuth, "my-bucket", out exists));
        TestLog.Check("SubjectToken of exactly 32,768 base64url characters in three parts is accepted", okEx == null, Detail(okEx, backend));

        // ServiceAccountEmail becomes part of the IAM Credentials URL.
        backend = NewBackend(r => null);
        foreach (string bad in new[] { "a/../../../../v1/other/x", "sa@proj.iam.gserviceaccount.com?x=1", "sa@proj.iam.gserviceaccount.com#frag", "sa name@proj.iam.gserviceaccount.com", "sa%2F@proj.iam.gserviceaccount.com" })
        {
            var auth = Auth();
            auth.ssSTGCS_Authentication.ssServiceAccountEmail = bad;
            var ex = Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
            TestLog.Check("ServiceAccountEmail '" + bad + "': ArgumentException 'is not a service account e-mail address'",
                ex is ArgumentException && Has(ex.Message, "is not a service account e-mail address"), Detail(ex, null));
        }
        TestLog.Check("Rejected ServiceAccountEmail values send no request", Total(backend) == 0, Detail(null, backend));

        foreach (string good in new[] { "name@project-id.iam.gserviceaccount.com", "123456789-compute@developer.gserviceaccount.com", "name@project.example.com.iam.gserviceaccount.com", "my_sa+test@proj.iam.gserviceaccount.com" })
        {
            var auth = Auth();
            auth.ssSTGCS_Authentication.ssServiceAccountEmail = Guid.NewGuid().ToString("N").Substring(0, 6) + good;
            var ex = Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
            TestLog.Check("ServiceAccountEmail of the form '" + good + "' is accepted", ex == null, Detail(ex, null));
        }
    }

    // ---- 15. ClientSecret is masked in the form it is sent in -------------------------------------------

    private static void SecretWireForm()
    {
        bool exists;
        string secret = "p@ss word!*(x)";
        var auth = FederationTests.ClientCredentials(null);
        auth.ssSTGCS_Authentication.ssClientSecret = secret;
        var backend = NewBackend(null);
        // Whatever the token request carried for client_secret, the provider echoes back in its error.
        backend.Override = r =>
        {
            if (!r.Uri.ToString().StartsWith(FakeFederationBackend.IdpTokenEndpoint, StringComparison.Ordinal)) return null;
            string sent = r.Body.Split('&').Where(p => p.StartsWith("client_secret=", StringComparison.Ordinal)).Select(p => p.Substring("client_secret=".Length)).FirstOrDefault() ?? "";
            return Text(400, "{\"error\":\"invalid_request\",\"error_description\":\"cannot parse client_secret=" + sent + " here\"}", "application/json");
        };
        var ex = Throws(() => Ext().MssBucket_Exists(auth, "b", out exists));
        var request = Where(backend, r => r.Uri.ToString().StartsWith(FakeFederationBackend.IdpTokenEndpoint, StringComparison.Ordinal)).FirstOrDefault();
        string wire = request == null ? null : request.Body.Split('&').Where(p => p.StartsWith("client_secret=", StringComparison.Ordinal)).Select(p => p.Substring("client_secret=".Length)).FirstOrDefault();
        TestLog.Check("ClientSecret 'p@ss word!*(x)' echoed by the provider exactly as it was sent is masked: '[redacted]', and neither the sent form nor the secret is in the message",
            ex != null && !string.IsNullOrEmpty(wire) && Has(ex.Message, "client_secret=[redacted] here") && !Has(ex.Message, wire) && !Has(ex.Message, secret) && !Has(ex.Message, "word"),
            (ex == null ? "no exception" : ex.Message) + " || sent as: " + wire);
    }

    // ---- 16. generateAccessToken refused ---------------------------------------------------------------

    private static void ImpersonationDenied()
    {
        bool exists;
        var auth = Auth();
        string serviceAccount = auth.ssSTGCS_Authentication.ssServiceAccountEmail;
        var backend = NewBackend(r => IsGenerateAccessToken(r)
            ? Text(403, "{\"error\":{\"code\":403,\"message\":\"Permission 'iam.serviceAccounts.getAccessToken' denied on resource (or it may not exist).\",\"status\":\"PERMISSION_DENIED\"}}", "application/json")
            : null);
        var ex = Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
        TestLog.Check("Impersonation refused (403 on generateAccessToken): the message names the service account and 'Workload Identity User', and no Storage request is sent",
            ex != null && Has(ex.Message, serviceAccount) && Has(ex.Message, "Workload Identity User") && Count(backend, IsStorage) == 0, Detail(ex, backend));
    }

    // ---- 17. Parallel first use of one credential -------------------------------------------------------

    private static void ParallelSignIn()
    {
        var auth = Auth();
        var backend = NewBackend(r => null);
        var errors = new List<Exception>();
        int succeeded = 0;
        var start = new ManualResetEventSlim(false);
        var threads = new List<Thread>();
        for (int i = 0; i < 12; i++)
        {
            var t = new Thread(() =>
            {
                start.Wait();
                bool exists = false;
                var ex = Throws(() => Ext().MssObject_Exists(auth, "my-bucket", "docs/a.txt", out exists));
                lock (errors) { if (ex != null) errors.Add(ex); else if (exists) succeeded++; }
            });
            t.Start();
            threads.Add(t);
        }
        start.Set();
        foreach (var t in threads) t.Join();
        TestLog.Check("12 parallel first calls with one credential all succeed with one token exchange and one impersonation (1 STS, 1 generateAccessToken, 12 Storage GETs)",
            errors.Count == 0 && succeeded == 12 && Count(backend, IsSts) == 1 && Count(backend, IsGenerateAccessToken) == 1 && Count(backend, IsMetaGet) == 12,
            (errors.Count == 0 ? "no errors" : UploadIntegrityTests.Chain(errors[0])) + "; succeeded=" + succeeded + ", STS=" + Count(backend, IsSts) + ", generateAccessToken=" + Count(backend, IsGenerateAccessToken) + ", GETs=" + Count(backend, IsMetaGet));
    }

    // ---- 18. Message cleaner: the remaining direction marks ---------------------------------------------

    private static void MessageCleaner()
    {
        var clean = Sut.GetMethod("CleanForMessage", Static);
        string input = "a\u200Eb\u200Fc\u061Cd\u2028e\u202Ef";
        string output = (string)clean.Invoke(null, new object[] { input, 100 });
        TestLog.Check("CleanForMessage replaces U+200E, U+200F and U+061C (direction marks) with spaces, like U+2028 and U+202E", output == "a b c d e f", output);
        string plain = "Caf\u00E9 \u65E5\u672C\u8A9E ok";
        TestLog.Check("CleanForMessage leaves ordinary non-ASCII text unchanged", (string)clean.Invoke(null, new object[] { plain, 100 }) == plain);
    }

    // ---- 19. A slow identity provider -------------------------------------------------------------------

    private static void SlowIdentityProvider()
    {
        // Google's sign-in code gives its first attempt 12 seconds. A provider that answers after 13 must
        // still work: the identity provider request has its own 30-second limit. This check takes 13 seconds.
        bool exists = false;
        var auth = FederationTests.ClientCredentials(null);
        var backend = NewBackend(null);
        backend.IdpDelayMilliseconds = 13000;
        var watch = Stopwatch.StartNew();
        var ex = Throws(() => Ext().MssBucket_Exists(auth, "my-bucket", out exists));
        watch.Stop();
        int idp = Count(backend, r => r.Uri.ToString().StartsWith(FakeFederationBackend.IdpTokenEndpoint, StringComparison.Ordinal));
        TestLog.Check("An identity provider that answers after 13 seconds (past Google's 12-second first attempt) still signs in, with one token request",
            ex == null && exists && idp == 1 && watch.Elapsed >= TimeSpan.FromSeconds(13) && watch.Elapsed < TimeSpan.FromSeconds(25),
            Detail(ex, null) + "; elapsed=" + (int)watch.Elapsed.TotalMilliseconds + " ms; token requests=" + idp);
    }

    // ---- 20. Findings of the independent review of this release ------------------------------------------

    private static string JwtWithExpiry(DateTime expiresUtc)
    {
        long seconds = (long)(expiresUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"sub\":\"app\",\"exp\":" + seconds + "}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "eyJhbGciOiJSUzI1NiJ9." + payload + ".c2ln";
    }

    private static void ReviewFollowUps()
    {
        bool exists;
        Exception ex;

        // (a) The stall timer only acts on a read that is pending and has waited the whole limit.
        Type guardType = Sut.GetNestedType("StallGuardStream", BindingFlags.NonPublic);
        var onStall = guardType.GetMethod("OnStall", Instance);
        var idle = new StallingStream(new byte[0]);
        var guard = (Stream)Activator.CreateInstance(guardType, new object[] { idle, TimeSpan.FromSeconds(1) });
        onStall.Invoke(guard, new object[] { null });
        TestLog.Check("Stall guard: a timer callback that arrives when no read is pending does not close the body", !idle.Closed);
        var pending = guard.ReadAsync(new byte[16], 0, 16, CancellationToken.None);
        onStall.Invoke(guard, new object[] { null });
        TestLog.Check("Stall guard: a timer callback left over from an earlier read does not close a body whose current read has only just started", !idle.Closed && !pending.IsCompleted);
        Exception stalled = null;
        try { pending.Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException e) { stalled = e.InnerException; }
        TestLog.Check("Stall guard: that read still fails once it has really waited the 1-second limit, and the body is closed",
            stalled != null && stalled.GetType().Name == "DownloadStalledException" && idle.Closed, stalled == null ? "no exception" : stalled.GetType().Name + ": " + stalled.Message);
        guard.Dispose();

        // (b) A compressed object is never refused by its stored size: 101 stored bytes, 100 delivered, limit 100.
        var data = Sample(100);
        var cap = Sut.GetField("MaxDownloadBytes", Static);
        byte[] content = null; string contentType;
        var backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("101", "gzip")); if (IsMedia(r)) return Media(data); return null; });
        cap.SetValue(null, 100L);
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { cap.SetValue(null, 0x7FFFFFC7L); }
        TestLog.Check("Download: a gzip-stored object of 101 stored bytes that is served as 100 bytes downloads under a 100-byte limit",
            ex == null && content != null && content.SequenceEqual(data), Detail(ex, backend));

        content = null;
        backend = NewBackend(r => { if (IsMetaGet(r)) return Json(ObjectJson("50", "gzip")); if (IsMedia(r)) return Media(Sample(101)); return null; });
        cap.SetValue(null, 100L);
        try { ex = Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType)); }
        finally { cap.SetValue(null, 0x7FFFFFC7L); }
        TestLog.Check("Download: a gzip-stored object of 50 stored bytes that is served as 101 bytes is still refused by the 100-byte limit ('larger than')",
            ex != null && Has(ex.Message, "is larger than") && content == null, Detail(ex, backend));

        // (c) The HTTP Basic credentials, echoed by the provider, are masked.
        string secret = "p@ss word!*(x)";
        var auth = FederationTests.ClientCredentials(null);
        auth.ssSTGCS_Authentication.ssClientSecret = secret;
        backend = NewBackend(null);
        backend.Override = r =>
        {
            if (!r.Uri.ToString().StartsWith(FakeFederationBackend.IdpTokenEndpoint, StringComparison.Ordinal)) return null;
            if (r.Authorization == null) return Text(401, "{\"error\":\"invalid_client\",\"error_description\":\"Client authentication failed.\"}", "application/json");
            return Text(400, "{\"error\":\"invalid_request\",\"error_description\":\"echo " + r.Authorization + " end\"}", "application/json");
        };
        ex = Throws(() => Ext().MssBucket_Exists(auth, "b", out exists));
        string basic = Where(backend, r => r.Authorization != null && r.Authorization.StartsWith("Basic ", StringComparison.Ordinal)).Select(r => r.Authorization.Substring(6)).FirstOrDefault();
        TestLog.Check("The provider echoes the Authorization header of the HTTP Basic attempt: the Base64 credentials are masked ('echo Basic [redacted] end')",
            ex != null && !string.IsNullOrEmpty(basic) && Has(ex.Message, "echo Basic [redacted] end") && !Has(ex.Message, basic), (ex == null ? "no exception" : ex.Message) + " || sent: " + basic);

        // (d) A secret repeated in the Content-Type of a non-JSON answer is masked.
        auth = FederationTests.ClientCredentials(null);
        auth.ssSTGCS_Authentication.ssClientSecret = secret;
        backend = NewBackend(null);
        backend.Override = r =>
        {
            if (!r.Uri.ToString().StartsWith(FakeFederationBackend.IdpTokenEndpoint, StringComparison.Ordinal)) return null;
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<html/>")) };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "text/html; secret=\"" + secret + "\"");
            return response;
        };
        ex = Throws(() => Ext().MssBucket_Exists(auth, "b", out exists));
        TestLog.Check("The provider repeats the ClientSecret in the Content-Type of a non-JSON answer: it is masked",
            ex != null && Has(ex.Message, "non-JSON response") && Has(ex.Message, "[redacted]") && !Has(ex.Message, secret) && !Has(ex.Message, "word"), ex == null ? "no exception" : ex.Message);

        // (e) A token repeated by Google's token service is masked: a supplied JWT, a supplied token of another shape, and the identity provider's token.
        string jwt = JwtWithExpiry(DateTime.UtcNow.AddMinutes(30));
        foreach (string token in new[] { jwt, "header.payload.signature" })
        {
            var supplied = Auth();
            supplied.ssSTGCS_Authentication.ssSubjectToken = token;
            backend = NewBackend(null);
            backend.StsError = "{\"error\":\"invalid_grant\",\"error_description\":\"rejected " + token + " as given\"}";
            ex = Throws(() => Ext().MssBucket_Exists(supplied, "b", out exists));
            TestLog.Check("Google's token service repeats the supplied SubjectToken (" + (token == jwt ? "a JWT" : "not starting with 'eyJ'") + "): the message has '[token]' and not the token",
                ex != null && Has(ex.Message, "Workload Identity Federation failed") && Has(ex.Message, "rejected [token] as given") && !Has(ex.Message, token), ex == null ? "no exception" : ex.Message);
        }
        var fetched = FederationTests.ClientCredentials(null);
        backend = NewBackend(null);
        backend.IdpToken = jwt;
        backend.StsError = "{\"error\":\"invalid_grant\",\"error_description\":\"rejected " + jwt + " as given\"}";
        ex = Throws(() => Ext().MssBucket_Exists(fetched, "b", out exists));
        TestLog.Check("Google's token service repeats the identity provider's token: the message has '[token]' and not the token",
            ex != null && Has(ex.Message, "rejected [token] as given") && !Has(ex.Message, jwt), ex == null ? "no exception" : ex.Message);

        // (f) The expiry check allows 30 seconds for clock difference.
        var justExpired = Auth();
        justExpired.ssSTGCS_Authentication.ssSubjectToken = JwtWithExpiry(DateTime.UtcNow.AddSeconds(-10));
        backend = NewBackend(r => null);
        ex = Throws(() => Ext().MssBucket_Exists(justExpired, "b", out exists));
        TestLog.Check("A SubjectToken whose exp passed 10 seconds ago (within the 30-second allowance) is not refused by the extension: it is sent to Google",
            (ex == null || !Has(ex.Message, "has expired")) && Count(backend, IsSts) == 1, Detail(ex, backend));
        var longExpired = Auth();
        longExpired.ssSTGCS_Authentication.ssSubjectToken = JwtWithExpiry(DateTime.UtcNow.AddSeconds(-60));
        backend = NewBackend(r => null);
        ex = Throws(() => Ext().MssBucket_Exists(longExpired, "b", out exists));
        TestLog.Check("A SubjectToken whose exp passed 60 seconds ago is refused ('has expired') and nothing is sent",
            ex is ArgumentException && Has(ex.Message, "The SubjectToken has expired") && Total(backend) == 0, Detail(ex, backend));

        // (g) The production Storage client: the stall guard sits directly under Google's handler, over certificate-checking transports.
        FederationTests.SetFactory(null);
        var production = (IHttpClientFactory)Sut.GetProperty("GoogleHttpClientFactory", Static).GetValue(null, null);
        Type storageFactoryType = Sut.GetNestedType("StorageHttpClientFactory", BindingFlags.NonPublic);
        var storageFactory = (IHttpClientFactory)Activator.CreateInstance(storageFactoryType, new object[] { production });
        var handlers = new List<HttpClientHandler>();
        string innerName;
        using (var client = storageFactory.CreateHttpClient(new CreateHttpClientArgs { GZipEnabled = true }))
        {
            innerName = client.MessageHandler.InnerHandler.GetType().Name;
            CollectHandlers(client.MessageHandler, handlers, new HashSet<object>());
        }
        TestLog.Check("Production Storage HTTP client: the download stall guard is the handler directly under Google's, and every transport under it checks the certificate",
            innerName == "DownloadStallHandler" && handlers.Count > 0 && handlers.All(h => h.ServerCertificateCustomValidationCallback != null
                && !h.ServerCertificateCustomValidationCallback(null, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)),
            "inner handler " + innerName + ", transports " + handlers.Count);
    }
}
