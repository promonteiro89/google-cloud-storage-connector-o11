// Safety tests, all offline against the in-process fake of Google Storage and the identity provider:
// the empty-name guards, the generation preconditions and retries of Move, Copy, DeleteByPrefix and
// UpdateMetadata, the translated error messages, and the bounds of the credential cache.
// C# 5 only. Expected values come from the written behaviour, never from what the code printed.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OutSystems.NssGoogleCloudStorage_ext;
using Rec = FakeFederationBackend.Recorded;

public static class SafetyTests
{
    private static readonly Type Sut = typeof(CssGoogleCloudStorage_ext);

    private const string SrcBucket = "src-bucket";
    private const string SrcObject = "in/x.txt";
    private const string DstBucket = "dst-bucket";
    private const string DstObject = "out/x.txt";

    public static void Run(string email, string pem)
    {
        string savedEmulator = EmulatorSeam.Get();
        EmulatorSeam.Set(null); // real (faked) Google endpoints only
        try
        {
            UpdateMetadataPatchBody();
            NameGuards();
            StatusZeroMessage();
            DeleteByPrefix();
            Move();
            Copy();
            IdentityProviderMessages();
            DownloadHashMismatch();
            CacheBounds();
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

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    /// <summary>A Google-style error: {"error":{"code":..,"message":..,"errors":[..]}}.</summary>
    private static HttpResponseMessage GoogleError(int code, string message)
    {
        var body = new JObject(new JProperty("error", new JObject(
            new JProperty("code", code),
            new JProperty("message", message),
            new JProperty("errors", new JArray(new JObject(new JProperty("message", message), new JProperty("domain", "global"), new JProperty("reason", "test")))))));
        return JsonResponse((HttpStatusCode)code, body.ToString(Formatting.None));
    }

    // A real HttpClient response always has a (possibly empty) Content, and the Google client reads it.
    private static HttpResponseMessage NoContent() { return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent("") }; }

    private static HttpResponseMessage RewriteDone(string bucket, string name)
    {
        var body = new JObject(
            new JProperty("kind", "storage#rewriteResponse"),
            new JProperty("done", true),
            new JProperty("resource", new JObject(
                new JProperty("kind", "storage#object"), new JProperty("bucket", bucket), new JProperty("name", name),
                new JProperty("generation", "5000"), new JProperty("metageneration", "1"))));
        return JsonResponse(HttpStatusCode.OK, body.ToString(Formatting.None));
    }

    /// <summary>The URL path split into unescaped segments. Read from OriginalString so an escaped slash (%2F) in an object name stays one segment.</summary>
    private static string[] Segments(Uri uri)
    {
        string raw = uri.OriginalString;
        int q = raw.IndexOf('?');
        if (q >= 0) raw = raw.Substring(0, q);
        int scheme = raw.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            int slash = raw.IndexOf('/', scheme + 3);
            raw = slash < 0 ? "" : raw.Substring(slash);
        }
        return raw.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(s => Uri.UnescapeDataString(s)).ToArray();
    }

    private static string Query(Uri uri, string name)
    {
        string raw = uri.OriginalString;
        int q = raw.IndexOf('?');
        if (q < 0) return null;
        foreach (var pair in raw.Substring(q + 1).Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split(new[] { '=' }, 2);
            if (Uri.UnescapeDataString(kv[0]) == name) return kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : "";
        }
        return null;
    }

    private static bool IsStorage(Rec r) { return r.Uri.Host == "storage.googleapis.com" && r.Uri.AbsolutePath.StartsWith("/storage/v1/b/", StringComparison.Ordinal); }

    /// <summary>GET/DELETE/PATCH of one object: /storage/v1/b/{bucket}/o/{name}</summary>
    private static bool IsObjectRequest(Rec r, HttpMethod method)
    {
        if (!IsStorage(r) || r.Method != method) return false;
        var s = Segments(r.Uri);
        return s.Length == 6 && s[4] == "o";
    }

    private static bool IsRewrite(Rec r)
    {
        if (!IsStorage(r) || r.Method != HttpMethod.Post) return false;
        var s = Segments(r.Uri);
        return s.Length == 11 && s[4] == "o" && s[6] == "rewriteTo";
    }

    private static bool IsList(Rec r)
    {
        if (!IsStorage(r) || r.Method != HttpMethod.Get) return false;
        var s = Segments(r.Uri);
        return s.Length == 5 && s[4] == "o";
    }

    private static string ObjectName(Rec r) { return Segments(r.Uri)[5]; }

    private static List<Rec> Where(FakeFederationBackend backend, Func<Rec, bool> predicate)
    {
        lock (backend.Requests) return backend.Requests.Where(predicate).ToList();
    }

    private static List<Rec> Deletes(FakeFederationBackend backend) { return Where(backend, r => IsObjectRequest(r, HttpMethod.Delete)); }
    private static List<Rec> Rewrites(FakeFederationBackend backend) { return Where(backend, IsRewrite); }

    /// <summary>What a failed check should print: the exception chain and the Storage requests the fake saw.</summary>
    private static string Detail(Exception ex, FakeFederationBackend backend)
    {
        var sb = new StringBuilder();
        sb.Append(ex == null ? "no exception" : UploadIntegrityTests.Chain(ex));
        sb.Append(" || requests: ");
        foreach (var r in Where(backend, IsStorage))
            sb.Append(r.Method + " " + r.Uri.OriginalString.Replace("https://storage.googleapis.com/storage/v1/b/", "") + "; ");
        return sb.ToString();
    }

    private static bool Has(string haystack, string needle) { return FederationTests.Has(haystack, needle); }

    private static RLGCS_MetadataEntryRecordList Meta(params string[] kvs)
    {
        var list = new RLGCS_MetadataEntryRecordList();
        for (int i = 0; i < kvs.Length; i += 2)
        {
            var rec = new RCGCS_MetadataEntryRecord(null);
            rec.ssSTGCS_MetadataEntry.ssKey = kvs[i];
            rec.ssSTGCS_MetadataEntry.ssValue = kvs[i + 1];
            list.Append(rec);
        }
        return list;
    }

    private static CssGoogleCloudStorage_ext Ext() { return new CssGoogleCloudStorage_ext(); }

    // ---- 1. UpdateMetadata sends a PATCH ---------------------------------------------------------

    private static void UpdateMetadataPatchBody()
    {
        var backend = NewBackend(null);
        backend.ObjectMetageneration = 7;
        var ex = FederationTests.Throws(() => Ext().MssObject_UpdateMetadata(UploadIntegrityTests.Federated(), "my-bucket", "docs/a.txt",
            "text/csv", "", "", "", Meta("department", "legal", "owner", "")));

        var patches = Where(backend, r => r.Method == FakeFederationBackend.Patch);
        var puts = Where(backend, r => r.Method == HttpMethod.Put && IsStorage(r));
        TestLog.Check("UpdateMetadata sends exactly one PATCH and no PUT", ex == null && patches.Count == 1 && puts.Count == 0, Detail(ex, backend));
        if (patches.Count != 1) return;

        JObject body = null;
        try { body = JObject.Parse(patches[0].Body); } catch (Exception e) { TestLog.Check("UpdateMetadata PATCH body is JSON", false, e.Message + ": " + patches[0].Body); return; }
        var metadata = body["metadata"] as JObject;
        var owner = metadata == null ? null : metadata.Property("owner");

        TestLog.Check("UpdateMetadata PATCH body sets metadata.department to 'legal'",
            metadata != null && (string)metadata["department"] == "legal", patches[0].Body);
        TestLog.Check("UpdateMetadata PATCH body sends a removed custom key (empty Value) as a JSON null",
            owner != null && owner.Value.Type == JTokenType.Null, patches[0].Body);
        TestLog.Check("UpdateMetadata PATCH body carries contentType 'text/csv'", (string)body["contentType"] == "text/csv", patches[0].Body);
        TestLog.Check("UpdateMetadata PATCH body has no acl and no contentEncoding",
            body.Property("acl") == null && body.Property("contentEncoding") == null, patches[0].Body);
        var names = body.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        TestLog.Check("UpdateMetadata PATCH body has only bucket, name, the changed field and the metadata map",
            string.Join(",", names) == "bucket,contentType,metadata,name", string.Join(",", names));
        TestLog.Check("UpdateMetadata PATCH query carries ifGenerationMatch=1001 and the GET's metageneration (7)",
            Query(patches[0].Uri, "ifGenerationMatch") == "1001" && Query(patches[0].Uri, "ifMetagenerationMatch") == "7", patches[0].Uri.OriginalString);
    }

    // ---- 3. Empty names are rejected before any request --------------------------------------------

    private static void Guard(string row, string parameter, Action<CssGoogleCloudStorage_ext, RCGCS_AuthenticationRecord> call)
    {
        var backend = NewBackend(null);
        var ext = Ext();
        var auth = UploadIntegrityTests.Federated();
        var ex = FederationTests.Throws(() => call(ext, auth));
        int requests;
        lock (backend.Requests) requests = backend.Requests.Count;
        TestLog.Check("Guard: " + row,
            ex != null && ex.GetType() == typeof(ArgumentException) && ex.Message == parameter + " is required." && requests == 0,
            (ex == null ? "no exception" : ex.GetType().Name + ": " + ex.Message) + "; requests=" + requests);
    }

    private static void NameGuards()
    {
        bool exists; byte[] bytes; string text; string token; long count;
        RCGCS_ObjectMetadataRecord md; RLGCS_MetadataEntryRecordList custom;
        RLGCS_ObjectRecordList list; RLGCS_PrefixRecordList prefixes;
        var noMeta = new RLGCS_MetadataEntryRecordList();
        var data = new byte[] { 1 };

        Guard("Bucket_Exists empty BucketName", "BucketName", (e, a) => e.MssBucket_Exists(a, "", out exists));
        Guard("Bucket_Create empty BucketName", "BucketName", (e, a) => e.MssBucket_Create(a, "", "EU"));
        Guard("Bucket_Delete empty BucketName", "BucketName", (e, a) => e.MssBucket_Delete(a, ""));

        Guard("Object_Delete empty BucketName", "BucketName", (e, a) => e.MssObject_Delete(a, "", "o"));
        Guard("Object_Delete empty ObjectName", "ObjectName", (e, a) => e.MssObject_Delete(a, "b", ""));
        Guard("Object_Exists empty BucketName", "BucketName", (e, a) => e.MssObject_Exists(a, "", "o", out exists));
        Guard("Object_Exists empty ObjectName", "ObjectName", (e, a) => e.MssObject_Exists(a, "b", "", out exists));
        Guard("Object_GetMetadata empty BucketName", "BucketName", (e, a) => e.MssObject_GetMetadata(a, "", "o", out exists, out md, out custom));
        Guard("Object_GetMetadata empty ObjectName", "ObjectName", (e, a) => e.MssObject_GetMetadata(a, "b", "", out exists, out md, out custom));
        Guard("Object_UpdateMetadata empty BucketName", "BucketName", (e, a) => e.MssObject_UpdateMetadata(a, "", "o", "text/plain", "", "", "", noMeta));
        Guard("Object_UpdateMetadata empty ObjectName", "ObjectName", (e, a) => e.MssObject_UpdateMetadata(a, "b", "", "text/plain", "", "", "", noMeta));
        Guard("Object_Upload empty BucketName", "BucketName", (e, a) => e.MssObject_Upload(a, "", "o", data, "text/plain", noMeta));
        Guard("Object_Upload empty ObjectName", "ObjectName", (e, a) => e.MssObject_Upload(a, "b", "", data, "text/plain", noMeta));
        Guard("Object_Download empty BucketName", "BucketName", (e, a) => e.MssObject_Download(a, "", "o", out bytes, out text));
        Guard("Object_Download empty ObjectName", "ObjectName", (e, a) => e.MssObject_Download(a, "b", "", out bytes, out text));
        Guard("Object_List empty BucketName", "BucketName", (e, a) => e.MssObject_List(a, "", "", 0, "", "", out list, out token, out prefixes));
        Guard("Object_DeleteByPrefix empty BucketName", "BucketName", (e, a) => e.MssObject_DeleteByPrefix(a, "", "p/", out count));

        Guard("Object_Copy empty SourceBucketName", "SourceBucketName", (e, a) => e.MssObject_Copy(a, "", "so", "db", "do"));
        Guard("Object_Copy empty SourceObjectName", "SourceObjectName", (e, a) => e.MssObject_Copy(a, "sb", "", "db", "do"));
        Guard("Object_Copy empty DestinationBucketName", "DestinationBucketName", (e, a) => e.MssObject_Copy(a, "sb", "so", "", "do"));
        Guard("Object_Copy empty DestinationObjectName", "DestinationObjectName", (e, a) => e.MssObject_Copy(a, "sb", "so", "db", ""));
        Guard("Object_Move empty SourceBucketName", "SourceBucketName", (e, a) => e.MssObject_Move(a, "", "so", "db", "do"));
        Guard("Object_Move empty SourceObjectName", "SourceObjectName", (e, a) => e.MssObject_Move(a, "sb", "", "db", "do"));
        Guard("Object_Move empty DestinationBucketName", "DestinationBucketName", (e, a) => e.MssObject_Move(a, "sb", "so", "", "do"));
        Guard("Object_Move empty DestinationObjectName", "DestinationObjectName", (e, a) => e.MssObject_Move(a, "sb", "so", "db", ""));

        Guard("Object_GetSignedUrl whitespace BucketName", "BucketName", (e, a) => e.MssObject_GetSignedUrl(a, "Download", "   ", "o", 5, "", out token));
        Guard("Object_Exists whitespace BucketName", "BucketName", (e, a) => e.MssObject_Exists(a, "   ", "o", out exists));
    }

    // ---- 4. Status 0 ---------------------------------------------------------------------------------

    private static void StatusZeroMessage()
    {
        var api = new Google.GoogleApiException("storage", "Parameter validation failed for \"object\"");
        var friendly = (Exception)Sut.GetMethod("FriendlyException", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { api, null, "b", "o" });
        TestLog.Check("A client-side rejection (HTTP status 0) says the library rejected it before it was sent",
            api.HttpStatusCode == 0 && friendly.Message.StartsWith("The request was rejected by the Google Cloud Storage client library before it was sent:", StringComparison.Ordinal)
            && !Has(friendly.Message, "(0 0)") && Has(friendly.Message, "Parameter validation failed"), friendly.Message);
    }

    // ---- 5. DeleteByPrefix ---------------------------------------------------------------------------

    private static readonly string[] PrefixNames = { "p/a", "p/b", "p/c" };
    private static readonly long[] PrefixGenerations = { 11, 12, 13 };

    private static HttpResponseMessage ListThree()
    {
        var items = new JArray();
        for (int i = 0; i < PrefixNames.Length; i++)
            items.Add(new JObject(new JProperty("kind", "storage#object"), new JProperty("bucket", "my-bucket"), new JProperty("name", PrefixNames[i]),
                new JProperty("generation", PrefixGenerations[i].ToString()), new JProperty("size", "1")));
        return JsonResponse(HttpStatusCode.OK, new JObject(new JProperty("kind", "storage#objects"), new JProperty("items", items)).ToString(Formatting.None));
    }

    /// <summary>A fake that lists p/a, p/b and p/c and answers each DELETE through the given function (name, nth request for that name).</summary>
    private static FakeFederationBackend PrefixBackend(Func<string, int, HttpResponseMessage> delete)
    {
        var counts = new Dictionary<string, int>();
        return NewBackend(r =>
        {
            if (IsList(r)) return ListThree();
            if (IsObjectRequest(r, HttpMethod.Delete))
            {
                string name = ObjectName(r);
                int n;
                counts.TryGetValue(name, out n);
                counts[name] = ++n;
                return delete(name, n);
            }
            return null;
        });
    }

    private static void DeleteByPrefix()
    {
        long deleted = -1;
        Exception ex = null;
        var backend = PrefixBackend((name, n) =>
        {
            if (name == "p/b") return GoogleError(412, "At least one of the pre-conditions you specified did not hold.");
            if (name == "p/c" && n == 1) return GoogleError(503, "Backend Error");
            return NoContent();
        });
        try { Ext().MssObject_DeleteByPrefix(UploadIntegrityTests.Federated(), "my-bucket", "p/", out deleted); }
        catch (Exception e) { ex = e; }

        var deletes = Deletes(backend);
        TestLog.Check("DeleteByPrefix: a 412 (object replaced after listing) is skipped and a 503 is retried; DeletedCount is 2 and nothing throws",
            ex == null && deleted == 2, (ex == null ? "DeletedCount=" + deleted : Detail(ex, backend)));
        bool allGuarded = deletes.Count > 0;
        foreach (var d in deletes)
        {
            int index = Array.IndexOf(PrefixNames, ObjectName(d));
            allGuarded &= index >= 0 && Query(d.Uri, "ifGenerationMatch") == PrefixGenerations[index].ToString();
        }
        TestLog.Check("DeleteByPrefix: every DELETE carries ifGenerationMatch equal to the generation from the listing", allGuarded, Detail(ex, backend));
        TestLog.Check("DeleteByPrefix: p/c is retried after the 503 (2 DELETEs) and p/a is deleted once",
            deletes.Count(d => ObjectName(d) == "p/c") == 2 && deletes.Count(d => ObjectName(d) == "p/a") == 1, Detail(ex, backend));
        TestLog.Check("DeleteByPrefix: a 412 is not retried (p/b gets one DELETE)", deletes.Count(d => ObjectName(d) == "p/b") == 1, Detail(ex, backend));

        // A 404 (already gone) is skipped and not counted either.
        backend = PrefixBackend((name, n) => name == "p/b" ? GoogleError(404, "No such object: my-bucket/p/b") : NoContent());
        deleted = -1;
        ex = null;
        try { Ext().MssObject_DeleteByPrefix(UploadIntegrityTests.Federated(), "my-bucket", "p/", out deleted); }
        catch (Exception e) { ex = e; }
        TestLog.Check("DeleteByPrefix: a 404 on one object is skipped, not counted, and does not fail the action",
            ex == null && deleted == 2, ex == null ? "DeletedCount=" + deleted : Detail(ex, backend));
    }

    // ---- 6. Move and 7. Copy -------------------------------------------------------------------------

    /// <summary>
    /// A fake for Move and Copy: the source's GET answers generation 77, the rewrite and the DELETE answer through
    /// the given functions (nth request, 1-based; null = success), and probe answers a GET of the source when set.
    /// </summary>
    private static FakeFederationBackend ObjectOpsBackend(Func<int, HttpResponseMessage> rewrite, Func<int, HttpResponseMessage> delete, Func<HttpResponseMessage> probe)
    {
        int rewrites = 0, deletes = 0;
        var backend = NewBackend(r =>
        {
            if (IsRewrite(r))
            {
                var response = rewrite == null ? null : rewrite(++rewrites);
                return response ?? RewriteDone(DstBucket, DstObject);
            }
            if (IsObjectRequest(r, HttpMethod.Delete))
            {
                var response = delete == null ? null : delete(++deletes);
                return response ?? NoContent();
            }
            if (probe != null && IsObjectRequest(r, HttpMethod.Get)) return probe();
            return null;
        });
        backend.ObjectGeneration = 77;
        backend.CurrentGeneration = 77;
        return backend;
    }

    private static Exception RunMove(FakeFederationBackend backend)
    {
        return FederationTests.Throws(() => Ext().MssObject_Move(UploadIntegrityTests.Federated(), SrcBucket, SrcObject, DstBucket, DstObject));
    }

    private static Exception RunCopy(FakeFederationBackend backend)
    {
        return FederationTests.Throws(() => Ext().MssObject_Copy(UploadIntegrityTests.Federated(), SrcBucket, SrcObject, DstBucket, DstObject));
    }

    private static void Move()
    {
        // (a) happy path
        var backend = ObjectOpsBackend(null, null, null);
        var ex = RunMove(backend);
        var rewrites = Rewrites(backend);
        var deletes = Deletes(backend);
        TestLog.Check("Move (a): succeeds", ex == null, Detail(ex, backend));
        TestLog.Check("Move (a): the rewrite carries ifSourceGenerationMatch=77 (the generation read before the copy)",
            rewrites.Count == 1 && Query(rewrites[0].Uri, "ifSourceGenerationMatch") == "77", Detail(ex, backend));
        TestLog.Check("Move (a): the delete carries ifGenerationMatch=77",
            deletes.Count == 1 && Query(deletes[0].Uri, "ifGenerationMatch") == "77", Detail(ex, backend));
        TestLog.Check("Move (a): the rewrite goes from the source to the destination object",
            rewrites.Count == 1 && Segments(rewrites[0].Uri)[3] == SrcBucket && Segments(rewrites[0].Uri)[5] == SrcObject
            && Segments(rewrites[0].Uri)[8] == DstBucket && Segments(rewrites[0].Uri)[10] == DstObject, Detail(ex, backend));

        // (b) the source changed during the move: nothing is copied over, so nothing is deleted
        backend = ObjectOpsBackend(n => GoogleError(412, "At least one of the pre-conditions you specified did not hold."), null, null);
        ex = RunMove(backend);
        TestLog.Check("Move (b): a 412 on the rewrite says the source changed and nothing was moved",
            ex != null && Has(ex.Message, "changed while it was being moved") && Has(ex.Message, "nothing was moved"), Detail(ex, backend));
        TestLog.Check("Move (b): no DELETE is sent after a failed rewrite", Deletes(backend).Count == 0, Detail(ex, backend));

        // (c) the source was replaced after the copy: it is not deleted, and the message says both exist
        backend = ObjectOpsBackend(null, n => GoogleError(412, "At least one of the pre-conditions you specified did not hold."), null);
        ex = RunMove(backend);
        TestLog.Check("Move (c): a 412 on the delete says the source was not deleted and both objects exist",
            ex != null && Has(ex.Message, "was not deleted") && Has(ex.Message, "both objects exist"), Detail(ex, backend));

        // (d) the source is already gone
        backend = ObjectOpsBackend(null, n => GoogleError(404, "No such object: " + SrcBucket + "/" + SrcObject), null);
        ex = RunMove(backend);
        TestLog.Check("Move (d): a 404 on the delete (source already gone) is success", ex == null, Detail(ex, backend));

        // (e) a transient 503 on the rewrite is retried
        backend = ObjectOpsBackend(n => n == 1 ? GoogleError(503, "Backend Error") : null, null, null);
        ex = RunMove(backend);
        TestLog.Check("Move (e): a 503 on the rewrite is retried and the move succeeds (2 rewrite requests)",
            ex == null && Rewrites(backend).Count == 2, Detail(ex, backend));

        // (f) a transient 503 on the delete is retried
        backend = ObjectOpsBackend(null, n => n == 1 ? GoogleError(503, "Backend Error") : null, null);
        ex = RunMove(backend);
        TestLog.Check("Move (f): a 503 on the delete is retried and the move succeeds (2 DELETE requests)",
            ex == null && Deletes(backend).Count == 2, Detail(ex, backend));
    }

    private static void Copy()
    {
        // (a) a transient 503 on the rewrite is retried
        var backend = ObjectOpsBackend(n => n == 1 ? GoogleError(503, "Backend Error") : null, null, null);
        var ex = RunCopy(backend);
        TestLog.Check("Copy (a): a 503 on the rewrite is retried and the copy succeeds (2 rewrite requests)",
            ex == null && Rewrites(backend).Count == 2, Detail(ex, backend));

        // (b) the probe's own 404 says the source object is missing
        backend = ObjectOpsBackend(n => GoogleError(404, "Not Found"), null, () => GoogleError(404, "No such object: " + SrcBucket + "/" + SrcObject));
        ex = RunCopy(backend);
        TestLog.Check("Copy (b): rewrite 404 and probe 404 name the missing source object",
            ex != null && Has(ex.Message, "Object 'in/x.txt' was not found in bucket 'src-bucket'"), Detail(ex, backend));

        // (c) the probe fails for another reason: neutral message naming both candidates
        backend = ObjectOpsBackend(n => GoogleError(404, "Not Found"), null, () => GoogleError(403, "Caller does not have storage.objects.get access to the Google Cloud Storage object."));
        ex = RunCopy(backend);
        TestLog.Check("Copy (c): rewrite 404 and probe 403 give the neutral message naming source and destination bucket, with the access error",
            ex != null && Has(ex.Message, "src-bucket/in/x.txt") && Has(ex.Message, "'dst-bucket'") && Has(ex.Message, "Access denied")
            && !ex.Message.StartsWith("Object '", StringComparison.Ordinal), Detail(ex, backend));

        // (d) the probe finds the source: the destination bucket is the missing one
        backend = ObjectOpsBackend(n => GoogleError(404, "The specified bucket does not exist."), null, null);
        ex = RunCopy(backend);
        TestLog.Check("Copy (d): rewrite 404 and a probe that finds the source say the destination bucket does not exist",
            ex != null && Has(ex.Message, "Bucket 'dst-bucket' does not exist"), Detail(ex, backend));
    }

    // ---- 8. Identity provider messages ---------------------------------------------------------------

    private static RCGCS_AuthenticationRecord IdpAuth()
    {
        var auth = FederationTests.ClientCredentials(null);
        auth.ssSTGCS_Authentication.ssTokenEndpoint = "https://idp.example.test/oauth2/token?tenant=secret-q-value";
        return auth;
    }

    private static void IdentityProviderMessages()
    {
        bool exists;

        var backend = NewBackend(r => { if (r.Uri.Host == "idp.example.test") throw new HttpRequestException("The remote name could not be resolved"); return null; });
        var ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(), "my-bucket", out exists));
        TestLog.Check("IdP unreachable: the message shows the token endpoint without its query string",
            ex != null && Has(ex.Message, "https://idp.example.test/oauth2/token") && !Has(ex.Message, "secret-q-value"), ex == null ? "no exception" : FederationTests.AllMessages(ex));

        backend = NewBackend(r => { if (r.Uri.Host == "idp.example.test") throw new TaskCanceledException("The operation was canceled."); return null; });
        ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(), "my-bucket", out exists));
        TestLog.Check("IdP timeout: the message shows the token endpoint without its query string",
            ex != null && Has(ex.Message, "https://idp.example.test/oauth2/token") && !Has(ex.Message, "secret-q-value"), ex == null ? "no exception" : FederationTests.AllMessages(ex));

        backend = NewBackend(r =>
        {
            if (r.Uri.Host == "idp.example.test")
                return JsonResponse(HttpStatusCode.BadRequest, "{\"error\":\"invalid_request\",\"error_description\":\"The scope is invalid.\"}");
            return null;
        });
        ex = FederationTests.Throws(() => Ext().MssBucket_Exists(IdpAuth(), "my-bucket", out exists));
        TestLog.Check("IdP rejection whose detail ends with '.' reads '...invalid. Check TokenEndpoint' without a doubled period",
            ex != null && Has(ex.Message, "The scope is invalid. Check TokenEndpoint") && !Has(ex.Message, ".."), ex == null ? "no exception" : FederationTests.AllMessages(ex));
    }

    // ---- 9. Downloaded data that fails its checksum --------------------------------------------------

    private static void DownloadHashMismatch()
    {
        var data = new byte[1000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
        var different = (byte[])data.Clone();
        different[500] ^= 0x01;

        Func<byte[], FakeFederationBackend> serve = hashedBytes => NewBackend(r =>
        {
            if (IsObjectRequest(r, HttpMethod.Get) && ObjectName(r) == "docs/d.bin" && Query(r.Uri, "alt") == "media")
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Headers.TryAddWithoutValidation("x-goog-hash", "crc32c=" + Crc32c.Base64(hashedBytes));
                return response;
            }
            return null; // the object's metadata GET uses the fake's built-in route
        });

        var backend = serve(different);
        byte[] content = null; string contentType = null;
        var ex = FederationTests.Throws(() => Ext().MssObject_Download(UploadIntegrityTests.Federated(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: data that fails its x-goog-hash CRC32C is reported as a checksum mismatch to retry, with no content",
            ex != null && Has(ex.Message, "did not match the object's checksum") && Has(ex.Message, "retry") && content == null, Detail(ex, backend) + " content=" + (content == null ? "null" : content.Length.ToString()));

        // Control: the same bytes with the matching hash download fine, so the failure above is the hash and not the fake.
        backend = serve(data);
        content = null; contentType = null;
        ex = FederationTests.Throws(() => Ext().MssObject_Download(UploadIntegrityTests.Federated(), "my-bucket", "docs/d.bin", out content, out contentType));
        TestLog.Check("Download: the same 1,000 bytes with a matching x-goog-hash are returned unchanged",
            ex == null && content != null && content.SequenceEqual(data), ex == null ? "content length " + (content == null ? "null" : content.Length.ToString()) : Detail(ex, backend));
    }

    // ---- 10. ExpiringCache bounds --------------------------------------------------------------------

    /// <summary>The private nested ExpiringCache&lt;string&gt; driven through reflection.</summary>
    private sealed class CacheProbe
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _cache;
        private readonly Type _type;

        public CacheProbe()
        {
            var open = Sut.GetNestedType("ExpiringCache`1", BindingFlags.NonPublic);
            _type = open.MakeGenericType(typeof(string));
            _cache = Activator.CreateInstance(_type, true);
        }

        public string GetOrAdd(string key, Func<string, string> factory, DateTime absoluteExpiryUtc)
        {
            try { return (string)_type.GetMethod("GetOrAdd", Any).Invoke(_cache, new object[] { key, factory, absoluteExpiryUtc }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        public int Count { get { return (int)_type.GetProperty("Count", Any).GetValue(_cache, null); } }

        private IDictionary Entries { get { return (IDictionary)_type.GetField("_entries", Any).GetValue(_cache); } }

        public bool Contains(string key) { return Entries.Contains(key); }

        public void SetLastUsed(string key, DateTime utc)
        {
            object entry = Entries[key];
            entry.GetType().GetProperty("LastUsedUtc", Any).SetValue(entry, utc, null);
        }

        public void SetNextSweep(DateTime utc) { _type.GetField("_nextSweepUtc", Any).SetValue(_cache, utc); }
    }

    private static void CacheBounds()
    {
        int calls;
        Func<string, string> factory;

        // (a) a live entry is reused
        var cache = new CacheProbe();
        calls = 0;
        factory = k => "v" + (++calls);
        string first = cache.GetOrAdd("k", factory, DateTime.MaxValue);
        string second = cache.GetOrAdd("k", factory, DateTime.MaxValue);
        TestLog.Check("Cache (a): a live entry is reused (factory called once, same value)", calls == 1 && first == "v1" && second == "v1", calls + " calls: " + first + ", " + second);

        // (b) absolute expiry
        cache = new CacheProbe();
        calls = 0;
        cache.GetOrAdd("k", factory = k => "v" + (++calls), DateTime.UtcNow.AddSeconds(-1));
        string again = cache.GetOrAdd("k", factory, DateTime.MaxValue);
        TestLog.Check("Cache (b): an entry past its absolute expiry is rebuilt by the next call", calls == 2 && again == "v2", calls + " calls: " + again);

        // (c) sliding expiry: 60 minutes without a hit
        cache = new CacheProbe();
        calls = 0;
        factory = k => "v" + (++calls);
        cache.GetOrAdd("k", factory, DateTime.MaxValue);
        cache.SetLastUsed("k", DateTime.UtcNow.AddMinutes(-61));
        string slid = cache.GetOrAdd("k", factory, DateTime.MaxValue);
        TestLog.Check("Cache (c): an entry unused for 61 minutes is rebuilt", calls == 2 && slid == "v2", calls + " calls: " + slid);

        cache = new CacheProbe();
        calls = 0;
        factory = k => "v" + (++calls);
        cache.GetOrAdd("k", factory, DateTime.MaxValue);
        cache.SetLastUsed("k", DateTime.UtcNow.AddMinutes(-59));
        string kept = cache.GetOrAdd("k", factory, DateTime.MaxValue);
        TestLog.Check("Cache (c): an entry unused for 59 minutes is reused", calls == 1 && kept == "v1", calls + " calls: " + kept);

        // (d) the cap holds at every insert: a full cache drops its least recently used entry for a new one
        cache = new CacheProbe();
        for (int i = 0; i < 500; i++) cache.GetOrAdd("k" + i, k => "v", DateTime.MaxValue);
        DateTime baseTime = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 0; i < 500; i++) cache.SetLastUsed("k" + i, baseTime.AddSeconds(i));
        TestLog.Check("Cache (d): 500 entries fit (Count 500)", cache.Count == 500, "Count=" + cache.Count);
        cache.GetOrAdd("new", k => "v", DateTime.MaxValue);
        bool restKept = true;
        for (int i = 1; i < 500; i++) restKept &= cache.Contains("k" + i);
        TestLog.Check("Cache (d): a 501st entry evicts the least recently used (k0); Count stays 500",
            cache.Count == 500 && !cache.Contains("k0") && cache.Contains("new"), "Count=" + cache.Count + ", k0 present=" + cache.Contains("k0"));
        TestLog.Check("Cache (d): k1..k499 stay", restKept);

        cache = new CacheProbe();
        int maxSeen = 0;
        for (int i = 0; i < 1200; i++)
        {
            cache.GetOrAdd("burst" + i, k => "v", DateTime.MaxValue);
            maxSeen = Math.Max(maxSeen, cache.Count);
        }
        TestLog.Check("Cache (d): 1,200 new identities within the same minute never hold more than 500 entries", maxSeen == 500 && cache.Count == 500, "max=" + maxSeen + ", Count=" + cache.Count);

        // (e) expired entries are swept even when the cache is under the cap
        cache = new CacheProbe();
        for (int i = 0; i < 3; i++) cache.GetOrAdd("e" + i, k => "v", DateTime.UtcNow.AddSeconds(-1));
        int before = cache.Count;
        cache.SetNextSweep(DateTime.MinValue);
        cache.GetOrAdd("other", k => "v", DateTime.MaxValue);
        TestLog.Check("Cache (e): expired entries are swept below the cap (3 expired + 1 new -> Count 1)", before == 3 && cache.Count == 1, "before=" + before + ", after=" + cache.Count);

        // (f) a throwing factory leaves nothing behind
        cache = new CacheProbe();
        cache.GetOrAdd("keep", k => "v", DateTime.MaxValue);
        int countBefore = cache.Count;
        Exception thrown = null;
        try { cache.GetOrAdd("bad", k => { throw new InvalidOperationException("factory failed"); }, DateTime.MaxValue); }
        catch (Exception e) { thrown = e; }
        TestLog.Check("Cache (f): a factory that throws propagates its exception and caches nothing",
            thrown is InvalidOperationException && thrown.Message == "factory failed" && cache.Count == countBefore && !cache.Contains("bad"),
            thrown == null ? "no exception" : thrown.GetType().Name + ", Count=" + cache.Count);
    }
}
