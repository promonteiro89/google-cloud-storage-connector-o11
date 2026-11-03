// 1.7.3 tests, all offline against the in-process fake of Google Storage and the identity provider:
// retries of Object_Delete, Object_Download and Object_Upload, the download size cap, the bucket-vs-object
// 404 check, GetSignedUrl's Operation check, trimmed credentials, and the identity provider limits.
// C# 5 only. Expected values come from the written behaviour (CHANGELOG 1.7.3), never from what the code printed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using OutSystems.NssGoogleCloudStorage_ext;
using Rec = FakeFederationBackend.Recorded;

public static class RetryTests
{
    private static readonly Type Sut = typeof(CssGoogleCloudStorage_ext);
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    private static readonly List<TimeSpan> Sleeps = new List<TimeSpan>();

    public static void Run()
    {
        string savedEmulator = EmulatorSeam.Get();
        var savedSleep = (Action<TimeSpan>)Sut.GetProperty("RetrySleepOverride", Static).GetValue(null, null);
        EmulatorSeam.Set(null);
        SetSleep(delegate(TimeSpan t) { lock (Sleeps) Sleeps.Add(t); });
        try
        {
            Delete();
            Download();
            DownloadTooLarge();
            Upload();
            RetryDelays();
            NotFoundClassification();
            SignedUrlOperationFirst();
            TrimmedCredentials();
            TokenEndpointEcho();
            IdentityProviderErrorFields();
            IdentityProviderResponseSize();
        }
        finally
        {
            SetSleep(savedSleep);
            Sut.GetField("MaxDownloadBytes", Static).SetValue(null, 0x7FFFFFC7L);
            FederationTests.SetFactory(null);
            EmulatorSeam.Set(savedEmulator);
        }
    }

    // ---- plumbing ----------------------------------------------------------------------------

    private static void SetSleep(Action<TimeSpan> sleep)
    {
        Sut.GetProperty("RetrySleepOverride", Static).SetValue(null, sleep, null);
    }

    private static FakeFederationBackend NewBackend(Func<Rec, HttpResponseMessage> handler)
    {
        var backend = new FakeFederationBackend();
        backend.Override = handler;
        FederationTests.SetFactory(new FakeHttpClientFactory(backend));
        lock (Sleeps) Sleeps.Clear();
        return backend;
    }

    private static HttpResponseMessage GoogleError(int code, string message)
    {
        string json = "{\"error\":{\"code\":" + code + ",\"message\":\"" + message + "\",\"errors\":[{\"message\":\"" + message + "\",\"domain\":\"global\",\"reason\":\"test\"}]}}";
        return new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage NoContent() { return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent("") }; }

    private static bool IsStorage(Rec r) { return r.Uri.Host == "storage.googleapis.com" && r.Uri.AbsolutePath.StartsWith("/storage/v1/b/", StringComparison.Ordinal); }
    private static bool IsObject(Rec r) { var s = r.Uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); return IsStorage(r) && s.Length == 6 && s[4] == "o"; }
    private static bool IsMedia(Rec r) { return IsObject(r) && r.Method == HttpMethod.Get && r.Uri.Query.Contains("alt=media"); }
    private static bool IsMetaGet(Rec r) { return IsObject(r) && r.Method == HttpMethod.Get && !r.Uri.Query.Contains("alt=media"); }
    private static bool IsDelete(Rec r) { return IsObject(r) && r.Method == HttpMethod.Delete; }
    private static bool IsList(Rec r) { var s = r.Uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); return IsStorage(r) && r.Method == HttpMethod.Get && s.Length == 5 && s[4] == "o"; }
    private static bool IsSessionStart(Rec r) { return r.Uri.Host == "storage.googleapis.com" && r.Uri.AbsolutePath.StartsWith("/upload/storage/v1/b/", StringComparison.Ordinal) && r.Method == HttpMethod.Post; }

    private static int Count(FakeFederationBackend b, Func<Rec, bool> p) { lock (b.Requests) return b.Requests.Count(p); }
    private static List<Rec> Where(FakeFederationBackend b, Func<Rec, bool> p) { lock (b.Requests) return b.Requests.Where(p).ToList(); }

    private static string Detail(Exception ex, FakeFederationBackend b)
    {
        var sb = new StringBuilder(ex == null ? "no exception" : UploadIntegrityTests.Chain(ex));
        sb.Append(" || requests: ");
        lock (b.Requests)
            foreach (var r in b.Requests.Where(x => x.Uri.Host == "storage.googleapis.com"))
                sb.Append(r.Method + " " + r.Uri.PathAndQuery + "; ");
        return sb.ToString();
    }

    private static bool Has(string s, string n) { return FederationTests.Has(s, n); }
    private static CssGoogleCloudStorage_ext Ext() { return new CssGoogleCloudStorage_ext(); }
    private static RCGCS_AuthenticationRecord Auth() { return UploadIntegrityTests.Federated(); }

    /// <summary>Serves the given bytes as the object's media, with a matching CRC32C.</summary>
    private static HttpResponseMessage Media(byte[] data)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        response.Headers.TryAddWithoutValidation("x-goog-hash", "crc32c=" + Crc32c.Base64(data));
        return response;
    }

    // ---- 1. Object_Delete ---------------------------------------------------------------------------

    private static void Delete()
    {
        // (a) a single 503 on the delete is retried, and the delete carries the generation read first
        int deletes = 0;
        var backend = NewBackend(r => { if (IsDelete(r)) return ++deletes == 1 ? GoogleError(503, "Service Unavailable") : NoContent(); return null; });
        var ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        var del = Where(backend, IsDelete);
        TestLog.Check("Delete (a): one 503 is retried and the delete succeeds (1 GET, 2 DELETEs)",
            ex == null && Count(backend, IsMetaGet) == 1 && del.Count == 2, Detail(ex, backend));
        TestLog.Check("Delete (a): every DELETE carries ifGenerationMatch=1001 from the GET",
            del.Count > 0 && del.All(r => r.Uri.Query.Contains("ifGenerationMatch=1001")), Detail(null, backend));

        // (b) the retried delete finds the object gone (the first attempt had worked): success
        deletes = 0;
        backend = NewBackend(r => { if (IsDelete(r)) return ++deletes == 1 ? GoogleError(503, "Service Unavailable") : GoogleError(404, "No such object: my-bucket/docs/a.txt"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete (b): a 404 on the retry after a 503 counts as deleted (no error)", ex == null, Detail(ex, backend));

        // (c) replaced between the read and the delete once: read again, delete again
        deletes = 0;
        backend = NewBackend(r => { if (IsDelete(r)) return ++deletes == 1 ? GoogleError(412, "At least one of the pre-conditions you specified did not hold.") : NoContent(); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete (c): one 412 reads the generation again and deletes (2 GETs, 2 DELETEs)",
            ex == null && Count(backend, IsMetaGet) == 2 && Count(backend, IsDelete) == 2, Detail(ex, backend));

        // (d) replaced on every attempt: 3 attempts, then a clear error
        backend = NewBackend(r => { if (IsDelete(r)) return GoogleError(412, "At least one of the pre-conditions you specified did not hold."); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete (d): a 412 on all 3 attempts fails with 'was replaced ... 3 attempts ... not deleted'",
            ex != null && Has(ex.Message, "was replaced by other requests on each of 3 attempts") && Has(ex.Message, "not deleted") && Count(backend, IsDelete) == 3, Detail(ex, backend));

        // (e) a missing object is still an error, as before 1.7.3, and nothing is deleted
        backend = NewBackend(r => { if (IsMetaGet(r)) return GoogleError(404, "No such object: my-bucket/docs/a.txt"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete (e): a missing object fails with \"Object 'docs/a.txt' was not found\" and sends no DELETE",
            ex != null && ex.Message.StartsWith("Object 'docs/a.txt' was not found", StringComparison.Ordinal) && Count(backend, IsDelete) == 0, Detail(ex, backend));

        // (f) a permanent error is not retried
        backend = NewBackend(r => { if (IsDelete(r)) return GoogleError(403, "Permission denied"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Delete(Auth(), "my-bucket", "docs/a.txt"));
        TestLog.Check("Delete (f): a 403 is not retried (1 DELETE) and says Access denied",
            ex != null && Has(ex.Message, "Access denied") && Count(backend, IsDelete) == 1, Detail(ex, backend));
    }

    // ---- 2. Object_Download -------------------------------------------------------------------------

    private static void Download()
    {
        var data = Encoding.UTF8.GetBytes("hello, retried world");
        byte[] content; string contentType;

        int gets = 0;
        var backend = NewBackend(r => { if (IsMedia(r)) return ++gets == 1 ? GoogleError(503, "Service Unavailable") : Media(data); return null; });
        content = null;
        var ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download (a): one 503 is retried and the 20 bytes come back unchanged (2 media GETs)",
            ex == null && content != null && content.SequenceEqual(data) && Count(backend, IsMedia) == 2, Detail(ex, backend));

        gets = 0;
        backend = NewBackend(r => { if (IsMedia(r)) { if (++gets == 1) throw new HttpRequestException("The connection was reset"); return Media(data); } return null; });
        content = null;
        ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download (b): a network failure is retried and the content comes back",
            ex == null && content != null && content.SequenceEqual(data), Detail(ex, backend));

        backend = NewBackend(r => { if (IsMedia(r)) return GoogleError(503, "Service Unavailable"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download (c): 503 on every attempt stops after 3 media GETs with the 503 error",
            ex != null && Has(ex.Message, "(503") && Count(backend, IsMedia) == 3, Detail(ex, backend));

        backend = NewBackend(r => { if (IsMedia(r)) return GoogleError(404, "No such object: my-bucket/docs/d.bin"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download (d): a 404 is not retried (1 media GET) and says the object was not found",
            ex != null && Has(ex.Message, "was not found") && Count(backend, IsMedia) == 1, Detail(ex, backend));
    }

    private static void DownloadTooLarge()
    {
        var data = new byte[1000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
        var cap = Sut.GetField("MaxDownloadBytes", Static);
        byte[] content = null; string contentType;

        var backend = NewBackend(r => { if (IsMedia(r)) return Media(data); return null; });
        cap.SetValue(null, 100L);
        Exception ex;
        try { ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/big.bin", out content, out contentType)); }
        finally { cap.SetValue(null, 0x7FFFFFC7L); }
        TestLog.Check("Download too large: 1,000 bytes over a 100-byte cap fail with 'larger than ... retrying will not help ... Object_GetSignedUrl'",
            ex != null && Has(ex.Message, "is larger than") && Has(ex.Message, "retrying will not help") && Has(ex.Message, "Object_GetSignedUrl"), Detail(ex, backend));
        TestLog.Check("Download too large: it is not retried (1 media GET) and returns no content",
            Count(backend, IsMedia) == 1 && content == null, Detail(ex, backend));

        // Control: the same object exactly at the cap downloads.
        backend = NewBackend(r => { if (IsMedia(r)) return Media(data); return null; });
        cap.SetValue(null, 1000L);
        try { ex = FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/big.bin", out content, out contentType)); }
        finally { cap.SetValue(null, 0x7FFFFFC7L); }
        TestLog.Check("Download too large: an object exactly at the cap (1,000 bytes) downloads",
            ex == null && content != null && content.SequenceEqual(data), Detail(ex, backend));
    }

    // ---- 3. Object_Upload -----------------------------------------------------------------------------

    private static void Upload()
    {
        var data = Encoding.UTF8.GetBytes("upload me");
        var noMeta = new RLGCS_MetadataEntryRecordList();

        int posts = 0;
        var backend = NewBackend(r => { if (IsSessionStart(r) && ++posts == 1) return GoogleError(503, "Service Unavailable"); return null; });
        var ex = FederationTests.Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "up/a.txt", data, "text/plain", noMeta));
        byte[] stored;
        bool found;
        lock (backend.StoredObjects) found = backend.StoredObjects.TryGetValue("my-bucket/up/a.txt", out stored);
        TestLog.Check("Upload (a): a 503 on the request that opens the session is retried and the object is stored (2 session POSTs)",
            ex == null && found && stored.SequenceEqual(data) && Count(backend, IsSessionStart) == 2, Detail(ex, backend));

        backend = NewBackend(r => { if (IsSessionStart(r)) return GoogleError(403, "Permission denied"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "up/a.txt", data, "text/plain", noMeta));
        TestLog.Check("Upload (b): a 403 is not retried (1 session POST)",
            ex != null && Has(ex.Message, "Access denied") && Count(backend, IsSessionStart) == 1, Detail(ex, backend));

        backend = NewBackend(null);
        ex = FederationTests.Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "up/a.txt", data, "text/plain\r\nX-Injected: 1", noMeta));
        int requests;
        lock (backend.Requests) requests = backend.Requests.Count;
        TestLog.Check("Upload (c): a ContentType with a line break is rejected with 'ContentType must not contain line breaks.' before any request",
            ex != null && ex.GetType() == typeof(ArgumentException) && ex.Message == "ContentType must not contain line breaks." && requests == 0,
            (ex == null ? "no exception" : ex.GetType().Name + ": " + ex.Message) + "; requests=" + requests);
    }

    // ---- 4. Waits between attempts ------------------------------------------------------------------

    private static void RetryDelays()
    {
        byte[] content; string contentType;
        NewBackend(r => { if (IsMedia(r)) return GoogleError(503, "Service Unavailable"); return null; });
        FederationTests.Throws(() => Ext().MssObject_Download(Auth(), "my-bucket", "docs/d.bin", out content, out contentType));
        List<TimeSpan> waits;
        lock (Sleeps) waits = Sleeps.ToList();
        TestLog.Check("Retry waits: 3 attempts wait twice, first 1.0-1.5 s, then 2.0-2.5 s",
            waits.Count == 2
            && waits[0] >= TimeSpan.FromSeconds(1) && waits[0] < TimeSpan.FromSeconds(1.5)
            && waits[1] >= TimeSpan.FromSeconds(2) && waits[1] < TimeSpan.FromSeconds(2.5),
            string.Join(", ", waits.Select(w => w.TotalMilliseconds.ToString()).ToArray()));
    }

    // ---- 5. Missing bucket vs missing object ------------------------------------------------------

    private static void NotFoundClassification()
    {
        bool exists;
        RCGCS_ObjectMetadataRecord md; RLGCS_MetadataEntryRecordList custom;

        // An unclear 404 text, and the bucket really is missing (the one-object listing also 404s).
        var backend = NewBackend(r => { if (IsMetaGet(r) || IsList(r)) return GoogleError(404, "Not Found"); return null; });
        var ex = FederationTests.Throws(() => Ext().MssObject_Exists(Auth(), "gone-bucket", "a.txt", out exists));
        TestLog.Check("404 (a): Object_Exists with an unclear 404 and a missing bucket raises \"Bucket 'gone-bucket' does not exist\"",
            ex != null && Has(ex.Message, "Bucket 'gone-bucket' does not exist") && Count(backend, IsList) == 1, Detail(ex, backend));
        backend = NewBackend(r => { if (IsMetaGet(r) || IsList(r)) return GoogleError(404, "Not Found"); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_GetMetadata(Auth(), "gone-bucket", "a.txt", out exists, out md, out custom));
        TestLog.Check("404 (b): Object_GetMetadata, same case, raises \"Bucket 'gone-bucket' does not exist\"",
            ex != null && Has(ex.Message, "Bucket 'gone-bucket' does not exist"), Detail(ex, backend));

        // An unclear 404 text, and the bucket exists: the object is missing.
        backend = NewBackend(r =>
        {
            if (IsMetaGet(r)) return GoogleError(404, "Not Found");
            if (IsList(r)) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"kind\":\"storage#objects\"}", Encoding.UTF8, "application/json") };
            return null;
        });
        exists = true;
        ex = FederationTests.Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "a.txt", out exists));
        TestLog.Check("404 (c): Object_Exists with an unclear 404 and an existing bucket returns False",
            ex == null && !exists && Count(backend, IsList) == 1, Detail(ex, backend));

        // Google's own wording settles it without the extra listing.
        backend = NewBackend(r => { if (IsMetaGet(r)) return GoogleError(404, "No such object: my-bucket/a.txt"); return null; });
        exists = true;
        ex = FederationTests.Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "a.txt", out exists));
        TestLog.Check("404 (d): 'No such object' returns False with no listing request",
            ex == null && !exists && Count(backend, IsList) == 0, Detail(ex, backend));
        backend = NewBackend(r => { if (IsMetaGet(r)) return GoogleError(404, "The specified bucket does not exist."); return null; });
        ex = FederationTests.Throws(() => Ext().MssObject_Exists(Auth(), "gone-bucket", "a.txt", out exists));
        TestLog.Check("404 (e): 'The specified bucket does not exist.' raises the bucket error with no listing request",
            ex != null && Has(ex.Message, "Bucket 'gone-bucket' does not exist") && Count(backend, IsList) == 0, Detail(ex, backend));

        // The listing check itself fails (no list permission): the text decides, as before 1.7.3.
        backend = NewBackend(r => { if (IsMetaGet(r)) return GoogleError(404, "Not Found"); if (IsList(r)) return GoogleError(403, "Permission denied"); return null; });
        exists = true;
        ex = FederationTests.Throws(() => Ext().MssObject_Exists(Auth(), "my-bucket", "a.txt", out exists));
        TestLog.Check("404 (f): when the listing check is denied (403), an unclear 404 falls back to the text and returns False",
            ex == null && !exists, Detail(ex, backend));
    }

    // ---- 6. GetSignedUrl checks Operation before any token exchange --------------------------------

    private static void SignedUrlOperationFirst()
    {
        string url;
        var backend = NewBackend(null);
        var ex = FederationTests.Throws(() => Ext().MssObject_GetSignedUrl(FederationTests.ClientCredentials(null), "downloads", "my-bucket", "a.txt", 5, "", out url));
        int requests;
        lock (backend.Requests) requests = backend.Requests.Count;
        TestLog.Check("GetSignedUrl: Operation 'downloads' fails with 'Invalid Operation' and contacts nobody (0 requests)",
            ex is ArgumentException && Has(ex.Message, "Invalid Operation 'downloads'") && requests == 0,
            (ex == null ? "no exception" : ex.GetType().Name + ": " + ex.Message) + "; requests=" + requests);
    }

    // ---- 7. Trimmed credentials -----------------------------------------------------------------------

    private static void TrimmedCredentials()
    {
        var record = new RCGCS_AuthenticationRecord(null);
        record.ssSTGCS_Authentication.ssProjectId = "  my-project\r\n";
        record.ssSTGCS_Authentication.ssClientEmail = " sa@my-project.iam.gserviceaccount.com \n";
        record.ssSTGCS_Authentication.ssServiceAccountEmail = "\tother@my-project.iam.gserviceaccount.com ";
        object config = Sut.GetMethod("FromRecord", Static).Invoke(null, new object[] { record });
        Type t = config.GetType();
        string project = (string)t.GetField("ProjectId").GetValue(config);
        string email = (string)t.GetField("ClientEmail").GetValue(config);
        string sa = (string)t.GetField("ServiceAccountEmail").GetValue(config);
        TestLog.Check("Trim: ProjectId, ClientEmail and ServiceAccountEmail lose surrounding spaces, tabs and line breaks",
            project == "my-project" && email == "sa@my-project.iam.gserviceaccount.com" && sa == "other@my-project.iam.gserviceaccount.com",
            "[" + project + "] [" + email + "] [" + sa + "]");
    }

    // ---- 8. Identity provider -----------------------------------------------------------------------

    private static RCGCS_AuthenticationRecord IdpAuth(string endpoint)
    {
        var auth = FederationTests.ClientCredentials(null);
        auth.ssSTGCS_Authentication.ssTokenEndpoint = endpoint;
        return auth;
    }

    private static void TokenEndpointEcho()
    {
        bool exists;
        NewBackend(null);
        var ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth("idp.example.test/oauth2/token?client_secret=FAKE-SECRET-1"), "my-bucket", out exists));
        string all = ex == null ? "" : FederationTests.AllMessages(ex);
        TestLog.Check("TokenEndpoint without a scheme: the error says it is not an absolute URL and echoes none of the value",
            ex != null && Has(all, "not an absolute http(s) URL") && !Has(all, "FAKE-SECRET-1") && !Has(all, "idp.example.test"), all);

        ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth("user:FAKE-SECRET-2@idp.example.test/token"), "my-bucket", out exists));
        all = ex == null ? "" : FederationTests.AllMessages(ex);
        TestLog.Check("TokenEndpoint 'user:password@host' without a scheme: the password is not echoed",
            ex != null && !Has(all, "FAKE-SECRET-2"), all);
    }

    private static void IdentityProviderErrorFields()
    {
        bool exists;
        string longError = new string('e', 150) + "\r\nFORGED LOG LINE";
        string json = "{\"error\":\"" + longError.Replace("\r", "\\r").Replace("\n", "\\n") + "\",\"error_description\":\"bad\\nthing\"}";
        NewBackend(r =>
        {
            if (r.Uri.Host == "idp.example.test") return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            return null;
        });
        var ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(FakeFederationBackend.IdpTokenEndpoint), "my-bucket", out exists));
        string msg = ex == null ? "" : ex.Message;
        TestLog.Check("IdP error fields: no line breaks reach the message",
            ex != null && msg.IndexOf('\r') < 0 && msg.IndexOf('\n') < 0 && Has(msg, "bad thing"), msg);
        TestLog.Check("IdP error fields: 'error' is cut to 100 characters plus '...', so the forged line is gone",
            Has(msg, new string('e', 100) + "...") && !Has(msg, new string('e', 101)) && !Has(msg, "FORGED"), msg);

        var clean = Sut.GetMethod("CleanForMessage", Static);
        TestLog.Check("CleanForMessage: null stays null; 'a\\tb' becomes 'a b'; 5 chars cut to 3 give 'abc...'; 3 chars at 3 stay",
            clean.Invoke(null, new object[] { null, 10 }) == null
            && (string)clean.Invoke(null, new object[] { "a\tb", 10 }) == "a b"
            && (string)clean.Invoke(null, new object[] { "abcde", 3 }) == "abc..."
            && (string)clean.Invoke(null, new object[] { "abc", 3 }) == "abc");
    }

    /// <summary>A response body whose length is not declared up front (like a chunked response).</summary>
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _data;
        public UnknownLengthContent(byte[] data) { _data = data; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) { return stream.WriteAsync(_data, 0, _data.Length); }
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() { return Task.FromResult<Stream>(new MemoryStream(_data)); }
    }

    private static void IdentityProviderResponseSize()
    {
        bool exists = false;
        var big = new byte[2 * 1024 * 1024];
        for (int i = 0; i < big.Length; i++) big[i] = (byte)'a';

        NewBackend(r => { if (r.Uri.Host == "idp.example.test") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(big) }; return null; });
        var ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(FakeFederationBackend.IdpTokenEndpoint), "my-bucket", out exists));
        TestLog.Check("IdP response: a 2 MB body with its length declared fails with 'larger than 1 MB'",
            ex != null && Has(ex.Message, "larger than 1 MB"), ex == null ? "no exception" : FederationTests.AllMessages(ex));

        NewBackend(r => { if (r.Uri.Host == "idp.example.test") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(big) }; return null; });
        ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(FakeFederationBackend.IdpTokenEndpoint), "my-bucket", out exists));
        TestLog.Check("IdP response: a 2 MB body of undeclared length fails with 'larger than 1 MB'",
            ex != null && Has(ex.Message, "larger than 1 MB"), ex == null ? "no exception" : FederationTests.AllMessages(ex));

        // Control: the normal token response (a few hundred bytes) still works end to end.
        var backend = NewBackend(null);
        ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(FakeFederationBackend.IdpTokenEndpoint), "my-bucket", out exists));
        TestLog.Check("IdP response: a normal token response still signs in (Bucket_Exists succeeds)", ex == null && exists, Detail(ex, backend));
    }
}
