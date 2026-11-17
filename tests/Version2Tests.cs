// 2.0.0 tests, all offline against the in-process fake of Google Storage: secure bucket defaults,
// OnlyIfNotExists uploads (including the lost-response repeat), content headers, the new Object_List
// fields, the required AuthenticationMethod and the signed-URL limit with Workload Identity Federation.
// C# 5 only. Expected values come from SPEC-2.0.0.md, never from what the code printed.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using OutSystems.NssGoogleCloudStorage_ext;
using Rec = FakeFederationBackend.Recorded;

public static class Version2Tests
{
    public static void Run(string email, string pem)
    {
        string savedEmulator = EmulatorSeam.Get();
        EmulatorSeam.Set(null); // real (faked) Google endpoints only
        try
        {
            OnlyIfNotExists();
            ContentHeaders();
            BucketDefaults();
            ListingFields();
            RequiredMethod(email, pem);
            SignedUrlLimits(email, pem);
        }
        finally
        {
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

    private static HttpResponseMessage Json(HttpStatusCode status, object value)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json") };
    }

    private static string[] Segments(Rec r) { return r.Uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries); }
    private static bool IsStorage(Rec r) { return r.Uri.Host == "storage.googleapis.com" && (r.Uri.AbsolutePath + "/").StartsWith("/storage/v1/b/", StringComparison.Ordinal); }
    private static bool IsObject(Rec r) { var s = Segments(r); return IsStorage(r) && s.Length == 6 && s[4] == "o"; }
    private static bool IsMetaGet(Rec r) { return IsObject(r) && r.Method == HttpMethod.Get && !r.Uri.Query.Contains("alt=media"); }
    private static bool IsList(Rec r) { var s = Segments(r); return IsStorage(r) && r.Method == HttpMethod.Get && s.Length == 5 && s[4] == "o"; }
    private static bool IsBucketCreate(Rec r) { return IsStorage(r) && r.Method == HttpMethod.Post && Segments(r).Length == 3; }
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

    private static RCGCS_AuthenticationRecord Key(string email, string pem, string method)
    {
        var r = new RCGCS_AuthenticationRecord(null);
        r.ssSTGCS_Authentication.ssProjectId = "proj";
        r.ssSTGCS_Authentication.ssAuthenticationMethod = method;
        r.ssSTGCS_Authentication.ssClientEmail = email;
        r.ssSTGCS_Authentication.ssPrivateKey = pem;
        return r;
    }

    /// <summary>One value of a JSON body, or null when any step of the path is missing.</summary>
    private static JToken At(JObject body, params string[] path)
    {
        JToken token = body;
        foreach (string step in path)
        {
            var current = token as JObject;
            if (current == null) return null;
            token = current[step];
            if (token == null) return null;
        }
        return token;
    }

    private static string ObjectNameOf(Rec sessionStart)
    {
        return (string)JObject.Parse(sessionStart.Body)["name"];
    }

    // ---- U1-U6: Object_Upload with OnlyIfNotExists ---------------------------------------------

    private static void OnlyIfNotExists()
    {
        var noMeta = new RLGCS_MetadataEntryRecordList();
        byte[] data = Encoding.UTF8.GetBytes("only if not exists: this call's bytes");
        // Same length as data, so U5 is decided by the MD5, not by the size.
        byte[] other = Encoding.UTF8.GetBytes("only if not exists: OTHER call bytes!");

        // U1: an empty bucket stores the object under the ifGenerationMatch=0 precondition.
        var backend = NewBackend(null);
        long generation = 0;
        var ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/only.txt", data, "text/plain", noMeta, true, "", "", "", out generation));
        var starts = Where(backend, IsSessionStart);
        string precondition = starts.Count == 1 ? Query(starts[0].Uri, "ifGenerationMatch") : null;
        byte[] stored;
        bool found = backend.StoredObjects.TryGetValue("my-bucket/docs/only.txt", out stored);
        long assigned = backend.StoredGeneration("my-bucket", "docs/only.txt");
        TestLog.Check("U1: OnlyIfNotExists=True sends ifGenerationMatch=0 when it opens the session",
            ex == null && starts.Count == 1 && precondition == "0", Detail(ex, backend));
        TestLog.Check("U1: the object is stored and Generation is the fake's generation (> 0)",
            ex == null && found && stored.SequenceEqual(data) && generation > 0 && generation == assigned,
            Detail(ex, backend) + "; generation=" + generation + ", stored generation=" + assigned);

        // U2: an existing object is refused with the exact message and left untouched.
        backend = NewBackend(null);
        backend.Store("my-bucket", "docs/taken.txt", other, null);
        ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/taken.txt", data, "text/plain", noMeta, true, "", "", "", out generation));
        found = backend.StoredObjects.TryGetValue("my-bucket/docs/taken.txt", out stored);
        TestLog.Check("U2: an existing object gives exactly the 'already exists ... left unchanged' error",
            ex != null && ex.Message == "Object 'docs/taken.txt' already exists in bucket 'my-bucket'. OnlyIfNotExists is True, so the existing object was left unchanged." && generation == 0,
            Detail(ex, backend));
        TestLog.Check("U2: the existing object's bytes are unchanged", found && stored.SequenceEqual(other), Detail(null, backend));

        // U3: OnlyIfNotExists=False sends no precondition and overwrites.
        backend = NewBackend(null);
        backend.Store("my-bucket", "docs/taken.txt", other, null);
        ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/taken.txt", data, "text/plain", noMeta, false, "", "", "", out generation));
        starts = Where(backend, IsSessionStart);
        found = backend.StoredObjects.TryGetValue("my-bucket/docs/taken.txt", out stored);
        TestLog.Check("U3: OnlyIfNotExists=False sends no ifGenerationMatch and overwrites the object",
            ex == null && starts.Count == 1 && Query(starts[0].Uri, "ifGenerationMatch") == null && found && stored.SequenceEqual(data),
            Detail(ex, backend));

        // U4: the first session start stored the object but answered 503; the repeat sees it (412) and
        // the metadata read shows it is the one this call stored, so the call succeeds.
        int posts = 0;
        FakeFederationBackend u4 = null;
        u4 = NewBackend(delegate(Rec r)
        {
            if (IsSessionStart(r) && ++posts == 1)
            {
                u4.Store("my-bucket", ObjectNameOf(r), data, null);
                return GoogleError(503, "Service Unavailable");
            }
            return null;
        });
        ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/lost.txt", data, "text/plain", noMeta, true, "", "", "", out generation));
        long lostGeneration = u4.StoredGeneration("my-bucket", "docs/lost.txt");
        TestLog.Check("U4: after a lost response the repeat's 412 is adopted when the stored object is this call's (2 session starts, 1 metadata GET)",
            ex == null && Count(u4, IsSessionStart) == 2 && Count(u4, IsMetaGet) == 1, Detail(ex, u4));
        TestLog.Check("U4: Generation is the stored object's generation",
            ex == null && generation > 0 && generation == lostGeneration,
            Detail(ex, u4) + "; generation=" + generation + ", stored generation=" + lostGeneration);

        // U5: the stored object has different bytes, so the repeat's 412 is someone else's object.
        posts = 0;
        FakeFederationBackend u5 = null;
        u5 = NewBackend(delegate(Rec r)
        {
            if (IsSessionStart(r) && ++posts == 1)
            {
                u5.Store("my-bucket", ObjectNameOf(r), other, null);
                return GoogleError(503, "Service Unavailable");
            }
            return null;
        });
        ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/lost.txt", data, "text/plain", noMeta, true, "", "", "", out generation));
        TestLog.Check("U5: a repeated upload whose stored object has different bytes is 'already exists', not adopted",
            ex != null
                && ex.Message == "Object 'docs/lost.txt' already exists in bucket 'my-bucket'. OnlyIfNotExists is True, so the existing object was left unchanged."
                && Count(u5, IsSessionStart) == 2 && Count(u5, IsMetaGet) == 1,
            Detail(ex, u5));

        // U6: same as U4, but the stored object was created two hours before the call: not this call's.
        posts = 0;
        FakeFederationBackend u6 = null;
        string twoHoursAgo = DateTime.UtcNow.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        u6 = NewBackend(delegate(Rec r)
        {
            if (IsSessionStart(r) && ++posts == 1)
            {
                u6.Store("my-bucket", ObjectNameOf(r), data, twoHoursAgo);
                return GoogleError(503, "Service Unavailable");
            }
            return null;
        });
        ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/lost.txt", data, "text/plain", noMeta, true, "", "", "", out generation));
        TestLog.Check("U6: a stored object created two hours before the call is not adopted ('already exists')",
            ex != null && Has(ex.Message, "already exists") && Count(u6, IsMetaGet) == 1 && generation == 0,
            Detail(ex, u6));
    }

    // ---- U7-U8: content headers ----------------------------------------------------------------

    private static void ContentHeaders()
    {
        var noMeta = new RLGCS_MetadataEntryRecordList();
        byte[] data = Encoding.UTF8.GetBytes("headers");

        // U7: the three new headers reach the upload session's metadata.
        var backend = NewBackend(null);
        long generation;
        var ex = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/hdr.pdf", data, "application/pdf", noMeta, false,
            "attachment; filename=\"a.pdf\"", "no-store", "gzip", out generation));
        var starts = Where(backend, IsSessionStart);
        JObject body = starts.Count == 1 ? JObject.Parse(starts[0].Body) : null;
        string disposition = body == null ? null : (string)At(body, "contentDisposition");
        string cacheControl = body == null ? null : (string)At(body, "cacheControl");
        string encoding = body == null ? null : (string)At(body, "contentEncoding");
        TestLog.Check("U7: ContentDisposition, CacheControl and ContentEncoding are sent in the session-start JSON",
            ex == null && disposition == "attachment; filename=\"a.pdf\"" && cacheControl == "no-store" && encoding == "gzip",
            Detail(ex, backend) + "; contentDisposition=" + (disposition ?? "(missing)") + ", cacheControl=" + (cacheControl ?? "(missing)") + ", contentEncoding=" + (encoding ?? "(missing)"));

        // U8: header values are validated before any request.
        backend = NewBackend(null);
        var lineBreak = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/h.txt", data, "text/plain", noMeta, false, "", "no\r\ncache", "", out generation));
        TestLog.Check("U8: CacheControl with a line break is rejected with the exact message and no request",
            lineBreak is ArgumentException && lineBreak.Message == "CacheControl must not contain line breaks." && Total(backend) == 0,
            Detail(lineBreak, backend));

        var nonAsciiDisposition = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/h.txt", data, "text/plain", noMeta, false, "attachment; filename=\"r\u00e9port.pdf\"", "", "", out generation));
        TestLog.Check("U8: a non-ASCII ContentDisposition is rejected with the exact message and no request",
            nonAsciiDisposition is ArgumentException
                && nonAsciiDisposition.Message == "ContentDisposition must contain only printable ASCII characters (no control characters, accented letters or other non-ASCII characters)."
                && Total(backend) == 0,
            Detail(nonAsciiDisposition, backend));

        var nonAsciiEncoding = Throws(() => Ext().MssObject_Upload(Auth(), "my-bucket", "docs/h.txt", data, "text/plain", noMeta, false, "", "", "gzip\u00e9", out generation));
        TestLog.Check("U8: a non-ASCII ContentEncoding is rejected with the exact message and no request",
            nonAsciiEncoding is ArgumentException
                && nonAsciiEncoding.Message == "ContentEncoding must contain only printable ASCII characters (no control characters, accented letters or other non-ASCII characters)."
                && Total(backend) == 0,
            Detail(nonAsciiEncoding, backend));
    }

    // ---- B1-B3: Bucket_Create secure defaults and StorageClass ---------------------------------

    private static void BucketDefaults()
    {
        // B1: the safe defaults: uniform access on, public access prevention enforced, no storage class.
        var backend = NewBackend(r => IsBucketCreate(r)
            ? Json(HttpStatusCode.OK, new { kind = "storage#bucket", name = "my-bucket", id = "my-bucket" })
            : null);
        var ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU", "", false, false));
        var creates = Where(backend, IsBucketCreate);
        JObject body = creates.Count == 1 ? JObject.Parse(creates[0].Body) : null;
        bool? enabled = (bool?)At(body, "iamConfiguration", "uniformBucketLevelAccess", "enabled");
        string prevention = (string)At(body, "iamConfiguration", "publicAccessPrevention");
        TestLog.Check("B1: Bucket_Create defaults send uniformBucketLevelAccess.enabled=true, publicAccessPrevention='enforced' and no storageClass",
            ex == null && enabled == true && prevention == "enforced" && (string)At(body, "storageClass") == null,
            Detail(ex, backend) + "; enabled=" + enabled + ", publicAccessPrevention=" + (prevention ?? "(missing)"));

        // B2: the permissive options: NEARLINE, fine-grained ACLs and inherited public access policy.
        backend = NewBackend(r => IsBucketCreate(r)
            ? Json(HttpStatusCode.OK, new { kind = "storage#bucket", name = "my-bucket", id = "my-bucket" })
            : null);
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU", "nearline", true, true));
        creates = Where(backend, IsBucketCreate);
        body = creates.Count == 1 ? JObject.Parse(creates[0].Body) : null;
        enabled = (bool?)At(body, "iamConfiguration", "uniformBucketLevelAccess", "enabled");
        prevention = (string)At(body, "iamConfiguration", "publicAccessPrevention");
        string storageClass = (string)At(body, "storageClass");
        TestLog.Check("B2: Bucket_Create('nearline', true, true) sends storageClass='NEARLINE', enabled=false and publicAccessPrevention='inherited'",
            ex == null && storageClass == "NEARLINE" && enabled == false && prevention == "inherited",
            Detail(ex, backend) + "; storageClass=" + (storageClass ?? "(missing)") + ", enabled=" + enabled + ", publicAccessPrevention=" + (prevention ?? "(missing)"));

        // B3: an unknown storage class is rejected before any request, as is.
        backend = NewBackend(null);
        ex = Throws(() => Ext().MssBucket_Create(Auth(), "my-bucket", "EU", "GLACIER", false, false));
        TestLog.Check("B3: StorageClass 'GLACIER' is rejected with the exact message and no request",
            ex is ArgumentException
                && ex.Message == "Invalid StorageClass 'GLACIER'. Use STANDARD, NEARLINE, COLDLINE or ARCHIVE, or leave it empty for STANDARD."
                && Total(backend) == 0,
            Detail(ex, backend));
    }

    // ---- L1: Object_List's Generation, MD5Hash and Crc32c --------------------------------------

    private static void ListingFields()
    {
        var backend = NewBackend(delegate(Rec r)
        {
            if (IsList(r))
                return Json(HttpStatusCode.OK, new
                {
                    kind = "storage#objects",
                    items = new object[]
                    {
                        new { kind = "storage#object", name = "docs/a.bin", size = "12", contentType = "application/octet-stream", generation = "1700000000000001", md5Hash = "XrY7u+Ae7tCTyyK7j1rNww==", crc32c = "yZRlqg==" },
                        new { kind = "storage#object", name = "docs/b.bin", size = "1", contentType = "text/plain", generation = "1700000000000002" }
                    }
                });
            return null;
        });
        RLGCS_ObjectRecordList list = null;
        string next = null;
        RLGCS_PrefixRecordList prefixes = null;
        var ex = Throws(() => Ext().MssObject_List(Auth(), "my-bucket", "", 0, "", "", out list, out next, out prefixes));
        RCGCS_ObjectRecord[] records = list == null
            ? new RCGCS_ObjectRecord[0]
            : list.ToArray(delegate(RCGCS_ObjectRecord r) { return r; });
        TestLog.Check("L1: Object_List fills Generation, MD5Hash and Crc32c from the listed object",
            ex == null && records.Length == 2
                && records[0].ssSTGCS_Object.ssGeneration == 1700000000000001L
                && records[0].ssSTGCS_Object.ssMD5Hash == "XrY7u+Ae7tCTyyK7j1rNww=="
                && records[0].ssSTGCS_Object.ssCrc32c == "yZRlqg==",
            Detail(ex, backend) + (records.Length > 0 ? "; generation=" + records[0].ssSTGCS_Object.ssGeneration + ", md5=" + records[0].ssSTGCS_Object.ssMD5Hash + ", crc32c=" + records[0].ssSTGCS_Object.ssCrc32c : ""));
        TestLog.Check("L1: an object without md5Hash or crc32c gives empty strings",
            ex == null && records.Length == 2
                && records[1].ssSTGCS_Object.ssMD5Hash == ""
                && records[1].ssSTGCS_Object.ssCrc32c == "",
            Detail(ex, backend));
    }

    // ---- A1-A3: AuthenticationMethod is required -----------------------------------------------

    private static void RequiredMethod(string email, string pem)
    {
        var backend = NewBackend(null);
        bool exists;

        var empty = Throws(() => Ext().MssObject_Exists(Key(email, pem, ""), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A1: an empty AuthenticationMethod is rejected with the exact required message and no request",
            empty is ArgumentException
                && empty.Message == "AuthenticationMethod is required. Set it to 'WorkloadIdentityFederation' or 'ServiceAccountKey'."
                && Total(backend) == 0,
            Detail(empty, backend));

        var spaces = Throws(() => Ext().MssObject_Exists(Key(email, pem, "   "), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A2: a whitespace AuthenticationMethod gives the same required message and no request",
            spaces is ArgumentException
                && spaces.Message == "AuthenticationMethod is required. Set it to 'WorkloadIdentityFederation' or 'ServiceAccountKey'."
                && Total(backend) == 0,
            Detail(spaces, backend));

        var invalid = Throws(() => Ext().MssObject_Exists(Key(email, pem, "Key"), "my-bucket", "docs/a.txt", out exists));
        TestLog.Check("A3: AuthenticationMethod 'Key' gives the exact invalid-value message and no request",
            invalid is ArgumentException
                && invalid.Message == "Invalid AuthenticationMethod 'Key'. Use 'WorkloadIdentityFederation' or 'ServiceAccountKey'."
                && Total(backend) == 0,
            Detail(invalid, backend));
    }

    // ---- S1-S3: signed-URL limits per authentication method ------------------------------------

    private static void SignedUrlLimits(string email, string pem)
    {
        string url = null;

        // S1: a federated URL over 12 hours is refused before any token exchange or signBlob request.
        var backend = NewBackend(null);
        var ex = Throws(() => Ext().MssObject_GetSignedUrl(FederationTests.ClientCredentials(null), "Download", "my-bucket", "docs/a.pdf", 721, "", out url));
        TestLog.Check("S1: 721 minutes with Workload Identity Federation gives the exact 720-minute message and no request",
            ex is ArgumentException
                && ex.Message == "ExpirationMinutes cannot exceed 720 minutes (12 hours) with Workload Identity Federation: Google signs these URLs with a key it rotates, so a longer-lived URL could stop working before it expires (received 721)."
                && Total(backend) == 0,
            Detail(ex, backend));

        // S2: exactly 720 minutes is accepted and signed through signBlob.
        backend = NewBackend(null);
        ex = Throws(() => Ext().MssObject_GetSignedUrl(FederationTests.ClientCredentials(null), "Download", "my-bucket", "docs/a.pdf", 720, "", out url));
        TestLog.Check("S2: 720 minutes with Workload Identity Federation succeeds",
            ex == null && Has(url, "X-Goog-Signature=")
                && Count(backend, r => r.Uri.AbsolutePath.EndsWith(":signBlob", StringComparison.Ordinal)) == 1,
            Detail(ex, backend) + "; url=" + (url == null ? "(null)" : url.Length + " chars"));

        // S3: a service account key keeps the V4 maximum of 7 days (10080 minutes).
        backend = NewBackend(null);
        ex = Throws(() => Ext().MssObject_GetSignedUrl(Key(email, pem, "ServiceAccountKey"), "Download", "my-bucket", "docs/a.pdf", 10080, "", out url));
        TestLog.Check("S3: 10080 minutes with a service account key succeeds",
            ex == null && Has(url, "X-Goog-Signature=") && Total(backend) == 0,
            Detail(ex, backend) + "; url=" + (url == null ? "(null)" : url.Length + " chars"));
    }
}
