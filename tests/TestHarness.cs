// Test harness for the Google Cloud Storage Connector (OutSystems 11 extension).
// Compiled by tests\run-tests.ps1 with Add-Type against the built extension DLL.
// C# 5 syntax only (Windows PowerShell 5.1 uses the .NET Framework compiler).
//
// Exercises the extension exactly as OutSystems developers consume it: through the
// public Mss* action methods. Integration tests target a local fake-gcs-server via
// GCSCONNECTOR_EMULATOR_HOST; signing/caching/validation tests are fully offline.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OutSystems.NssGoogleCloudStorage_ext;

public class GcsTestResult
{
    public string Log;
    public int Passed;
    public int Failed;
}

public static class GcsExtensionTestSuite
{
    private static StringBuilder log = new StringBuilder();
    private static int passed = 0;
    private static int failed = 0;

    private static void OK(string name, bool cond, string detail)
    {
        if (cond) { passed++; log.AppendLine("  PASS  " + name); }
        else { failed++; log.AppendLine("  FAIL  " + name + (detail == null ? "" : " -> " + Trunc(detail, 160))); }
    }

    private static void OK(string name, bool cond) { OK(name, cond, null); }

    private static string Trunc(string s, int len)
    {
        if (s == null) return "(null)";
        return s.Length <= len ? s : s.Substring(0, len) + "...";
    }

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

    private static Dictionary<string, string> ToDict(RLGCS_MetadataEntryRecordList list)
    {
        var d = new Dictionary<string, string>();
        foreach (var r in list.ToArray(delegate(RCGCS_MetadataEntryRecord x) { return x; }))
            d[r.ssSTGCS_MetadataEntry.ssKey] = r.ssSTGCS_MetadataEntry.ssValue;
        return d;
    }

    public static GcsTestResult Run(string binPath, string pem, bool emulator)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            string p = System.IO.Path.Combine(binPath, name + ".dll");
            return System.IO.File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        return RunCore(pem, emulator);
    }

    private static GcsTestResult RunCore(string pem, bool emulator)
    {
        var ext = new CssGoogleCloudStorage_ext();
        string email = "test-sa@fake-project.iam.gserviceaccount.com";
        string proj = "fake-project";

        log.AppendLine("=== Offline: signed URLs, validation, caching ===");
        OfflineTests(ext, proj, email, pem, emulator);

        if (emulator)
        {
            log.AppendLine();
            log.AppendLine("=== Integration: full action lifecycle against fake-gcs-server ===");
            IntegrationTests(ext, proj, email, pem);
        }
        else
        {
            log.AppendLine();
            log.AppendLine("(emulator skipped: integration tests not run)");
        }

        var r = new GcsTestResult();
        r.Log = log.ToString();
        r.Passed = passed;
        r.Failed = failed;
        return r;
    }

    // ------------------------------------------------------------------ offline ----

    private static void OfflineTests(CssGoogleCloudStorage_ext ext, string proj, string email, string pem, bool emulator)
    {
        string url;
        ext.MssObject_GetSignedUrl(proj, email, pem, "Download", "test-bucket", "docs/report.pdf", 15, "", out url);
        OK("SignedUrl Download returns V4 URL", url != null && url.Contains("test-bucket") && url.Contains("X-Goog-Signature"), url);

        string urlUp;
        ext.MssObject_GetSignedUrl(proj, email, pem, "upload", "test-bucket", "docs/report.pdf", 60, "", out urlUp);
        OK("SignedUrl operation is case-insensitive", !string.IsNullOrEmpty(urlUp));
        OK("No ContentType -> content-type not in signed headers", !urlUp.ToLowerInvariant().Contains("content-type"));

        string urlCt;
        ext.MssObject_GetSignedUrl(proj, email, pem, "Upload", "test-bucket", "docs/new.pdf", 30, "application/pdf", out urlCt);
        OK("ContentType becomes part of the signature", urlCt != null && urlCt.ToLowerInvariant().Contains("content-type"), urlCt);

        try { string u; ext.MssObject_GetSignedUrl(proj, email, pem, "Banana", "b", "o", 5, "", out u); OK("Invalid Operation rejected", false, "no exception"); }
        catch (ArgumentException) { OK("Invalid Operation rejected", true); }

        try { string u; ext.MssObject_GetSignedUrl(proj, email, pem, "Download", "b", "o", 0, "", out u); OK("ExpirationMinutes <= 0 rejected", false, "no exception"); }
        catch (ArgumentException) { OK("ExpirationMinutes <= 0 rejected", true); }

        try { string u; ext.MssObject_GetSignedUrl(proj, email, pem, "Download", "b", "o", 20000, "", out u); OK("ExpirationMinutes > 7 days rejected", false, "no exception"); }
        catch (ArgumentException) { OK("ExpirationMinutes > 7 days rejected", true); }

        string url7d;
        ext.MssObject_GetSignedUrl(proj, email, pem, "Download", "b", "o", 10080, "", out url7d);
        OK("Exactly 7 days accepted", !string.IsNullOrEmpty(url7d));

        string urlEsc;
        ext.MssObject_GetSignedUrl(proj, email, pem.Replace("\n", "\\n"), "Download", "b", "x.txt", 5, "", out urlEsc);
        OK("JSON-escaped (\\n) private key parses", !string.IsNullOrEmpty(urlEsc));

        try
        {
            string u; ext.MssObject_GetSignedUrl(proj, email, "not-a-key", "Download", "b", "o", 5, "", out u);
            OK("Garbage key -> friendly parse error", false, "no exception");
        }
        catch (Exception ex)
        {
            OK("Garbage key -> friendly parse error", ex is ArgumentException && ex.Message.Contains("PrivateKey could not be parsed"), ex.Message);
        }

        try
        {
            RLGCS_ObjectRecordList objs; RLGCS_PrefixRecordList prefixes; string next;
            ext.MssObject_List(proj, email, pem, "b", "", -1, "", "", out objs, out next, out prefixes);
            OK("Negative MaxResults rejected", false, "no exception");
        }
        catch (ArgumentException) { OK("Negative MaxResults rejected", true); }

        // Cache identity (via the private helpers, since clients are internal)
        Type t = ext.GetType();
        MethodInfo gsc = t.GetMethod("GetStorageClient", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo gus = t.GetMethod("GetUrlSigner", BindingFlags.NonPublic | BindingFlags.Static);
        object c1 = gsc.Invoke(null, new object[] { proj, email, pem });
        object c2 = gsc.Invoke(null, new object[] { proj, email, pem });
        OK("StorageClient cached (same instance reused)", object.ReferenceEquals(c1, c2));

        object c3 = gsc.Invoke(null, new object[] { proj, "other-sa@fake-project.iam.gserviceaccount.com", pem });
        if (emulator)
            OK("Emulator mode: one shared client regardless of credentials", object.ReferenceEquals(c1, c3));
        else
            OK("Distinct credentials get distinct clients", !object.ReferenceEquals(c1, c3));

        object s1 = gus.Invoke(null, new object[] { email, pem });
        object s2 = gus.Invoke(null, new object[] { email, pem });
        OK("UrlSigner cached (same instance reused)", object.ReferenceEquals(s1, s2));
    }

    // -------------------------------------------------------------- integration ----

    private static void IntegrationTests(CssGoogleCloudStorage_ext ext, string proj, string email, string pem)
    {
        string b1 = "test-bucket-alpha";
        string b2 = "test-bucket-beta";
        bool exists;

        // --- bucket lifecycle ---
        ext.MssBucket_Exists(proj, email, pem, b1, out exists);
        OK("Bucket_Exists on missing bucket -> False", !exists);

        ext.MssBucket_Create(proj, email, pem, b1, "US");
        ext.MssBucket_Exists(proj, email, pem, b1, out exists);
        OK("Bucket_Create + Bucket_Exists -> True", exists);

        try { ext.MssBucket_Create(proj, email, pem, b1, "US"); OK("Duplicate Bucket_Create raises error", false, "no exception"); }
        catch (Exception ex) { OK("Duplicate Bucket_Create raises error", true, ex.Message); }

        ext.MssBucket_Create(proj, email, pem, b2, "EU");

        RLGCS_BucketRecordList buckets;
        ext.MssBucket_List(proj, email, pem, out buckets);
        var bucketNames = buckets.ToArray(delegate(RCGCS_BucketRecord r) { return r.ssSTGCS_Bucket.ssName; });
        var bucketCreated = buckets.ToArray(delegate(RCGCS_BucketRecord r) { return r.ssSTGCS_Bucket.ssCreated; });
        OK("Bucket_List returns both buckets", bucketNames.Contains(b1) && bucketNames.Contains(b2), string.Join(",", bucketNames));
        OK("Bucket_List Created timestamps populated", bucketCreated.All(delegate(DateTime d) { return d.Year > 2000; }));

        // --- object upload / download ---
        var rnd = new Random(42);
        byte[] big = new byte[5 * 1024 * 1024];
        rnd.NextBytes(big);
        byte[] photo = new byte[8 * 1024];
        rnd.NextBytes(photo);
        string unicodeName = "docs/relat\u00f3rio se\u00e7\u00e3o.pdf"; // accented name via escapes (source stays ASCII-safe)

        var files = new Dictionary<string, byte[]>();
        files["root.txt"] = Encoding.UTF8.GetBytes("root content");
        files["empty.bin"] = new byte[0];
        files["big.bin"] = big;
        files["docs/a.txt"] = Encoding.UTF8.GetBytes("content A");
        files["docs/b.txt"] = Encoding.UTF8.GetBytes("content B");
        files["docs/sub/c.txt"] = Encoding.UTF8.GetBytes("content C");
        files[unicodeName] = Encoding.UTF8.GetBytes("pdf-ish");
        files["img/photo.png"] = photo;

        foreach (var kv in files)
            ext.MssObject_Upload(proj, email, pem, b1, kv.Key, kv.Value, kv.Key.EndsWith(".txt") ? "text/plain" : "application/octet-stream", null);
        OK("Object_Upload of " + files.Count + " objects (incl. empty, 5MB, unicode names)", true);

        ext.MssObject_Exists(proj, email, pem, b1, "root.txt", out exists);
        OK("Object_Exists -> True after upload", exists);
        ext.MssObject_Exists(proj, email, pem, b1, "nope.txt", out exists);
        OK("Object_Exists on missing object -> False", !exists);

        byte[] content; string contentType;
        ext.MssObject_Download(proj, email, pem, b1, "root.txt", out content, out contentType);
        OK("Object_Download round-trips content", Encoding.UTF8.GetString(content) == "root content", Encoding.UTF8.GetString(content));
        OK("Object_Download round-trips ContentType", contentType == "text/plain", contentType);

        ext.MssObject_Download(proj, email, pem, b1, "img/photo.png", out content, out contentType);
        OK("Object_Download binary is byte-exact", content.SequenceEqual(photo));

        ext.MssObject_Download(proj, email, pem, b1, "empty.bin", out content, out contentType);
        OK("Object_Download of empty object -> 0 bytes", content != null && content.Length == 0);

        ext.MssObject_Download(proj, email, pem, b1, "big.bin", out content, out contentType);
        OK("Object_Download 5MB is byte-exact", content.SequenceEqual(big));

        ext.MssObject_Download(proj, email, pem, b1, unicodeName, out content, out contentType);
        OK("Unicode object names round-trip", Encoding.UTF8.GetString(content) == "pdf-ish");

        // --- metadata ---
        bool mdExists; RCGCS_ObjectMetadataRecord md; RLGCS_MetadataEntryRecordList custom;
        ext.MssObject_GetMetadata(proj, email, pem, b1, "docs/a.txt", out mdExists, out md, out custom);
        OK("Object_GetMetadata -> Exists True", mdExists);
        OK("GetMetadata Name/Bucket/Size/ContentType",
            md.ssSTGCS_ObjectMetadata.ssName == "docs/a.txt" && md.ssSTGCS_ObjectMetadata.ssBucket == b1 &&
            md.ssSTGCS_ObjectMetadata.ssSize == 9 && md.ssSTGCS_ObjectMetadata.ssContentType == "text/plain",
            md.ssSTGCS_ObjectMetadata.ssName + "|" + md.ssSTGCS_ObjectMetadata.ssBucket + "|" + md.ssSTGCS_ObjectMetadata.ssSize + "|" + md.ssSTGCS_ObjectMetadata.ssContentType);
        OK("GetMetadata Generation/timestamps populated",
            md.ssSTGCS_ObjectMetadata.ssGeneration > 0 && md.ssSTGCS_ObjectMetadata.ssTimeCreated.Year > 2000 && md.ssSTGCS_ObjectMetadata.ssUpdated.Year > 2000);
        OK("GetMetadata hashes populated", !string.IsNullOrEmpty(md.ssSTGCS_ObjectMetadata.ssMD5Hash) && !string.IsNullOrEmpty(md.ssSTGCS_ObjectMetadata.ssCrc32c),
            md.ssSTGCS_ObjectMetadata.ssMD5Hash + "|" + md.ssSTGCS_ObjectMetadata.ssCrc32c);

        OK("GetMetadata CustomMetadata empty when none set", custom.Length == 0);

        ext.MssObject_GetMetadata(proj, email, pem, b1, "nope.txt", out mdExists, out md, out custom);
        OK("Object_GetMetadata on missing object -> Exists False", !mdExists);

        // --- listing: flat, prefix, delimiter, pagination ---
        RLGCS_ObjectRecordList objs; RLGCS_PrefixRecordList prefixes; string next;

        ext.MssObject_List(proj, email, pem, b1, "", 0, "", "", out objs, out next, out prefixes);
        var allNames = objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssName; });
        var allSizes = objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssSize; });
        OK("Object_List returns all " + files.Count + " objects", allNames.Length == files.Count && files.Keys.All(delegate(string k) { return allNames.Contains(k); }), string.Join(",", allNames));
        OK("Object_List sizes are correct (incl. 5MB as Long)", allSizes.Sum() == files.Values.Sum(delegate(byte[] v) { return (long)v.Length; }));
        OK("Object_List full mode -> empty NextPageToken", next == "");

        ext.MssObject_List(proj, email, pem, b1, "docs/", 0, "", "", out objs, out next, out prefixes);
        OK("Object_List with Prefix filters correctly", objs.Length == 4, objs.Length.ToString());

        ext.MssObject_List(proj, email, pem, b1, "", 0, "", "/", out objs, out next, out prefixes);
        var topNames = objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssName; });
        var topPrefixes = prefixes.ToArray(delegate(RCGCS_PrefixRecord r) { return r.ssSTGCS_Prefix.ssPrefix; });
        OK("Delimiter: only root objects returned", topNames.Length == 3 && topNames.Contains("root.txt") && topNames.Contains("empty.bin") && topNames.Contains("big.bin"), string.Join(",", topNames));
        OK("Delimiter: folders returned as PrefixList", topPrefixes.Length == 2 && topPrefixes.Contains("docs/") && topPrefixes.Contains("img/"), string.Join(",", topPrefixes));

        ext.MssObject_List(proj, email, pem, b1, "docs/", 0, "", "/", out objs, out next, out prefixes);
        var docNames = objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssName; });
        var docPrefixes = prefixes.ToArray(delegate(RCGCS_PrefixRecord r) { return r.ssSTGCS_Prefix.ssPrefix; });
        OK("Prefix+Delimiter: direct children only", docNames.Length == 3 && docPrefixes.Length == 1 && docPrefixes[0] == "docs/sub/",
            string.Join(",", docNames) + " | " + string.Join(",", docPrefixes));

        var paged = new HashSet<string>();
        string token = "";
        int pages = 0;
        do
        {
            ext.MssObject_List(proj, email, pem, b1, "", 3, token, "", out objs, out next, out prefixes);
            foreach (var n in objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssName; })) paged.Add(n);
            token = next;
            pages++;
            if (pages > 10) break;
        } while (token != "");
        OK("Pagination: MaxResults=3 walks all objects via NextPageToken", paged.Count == files.Count && pages == 3, paged.Count + " objects in " + pages + " pages");

        // --- copy / move / delete ---
        ext.MssObject_Copy(proj, email, pem, b1, "docs/a.txt", b2, "copied/a.txt");
        ext.MssObject_Exists(proj, email, pem, b1, "docs/a.txt", out exists);
        bool destExists;
        ext.MssObject_Exists(proj, email, pem, b2, "copied/a.txt", out destExists);
        ext.MssObject_Download(proj, email, pem, b2, "copied/a.txt", out content, out contentType);
        OK("Object_Copy across buckets (source kept, content equal)", exists && destExists && Encoding.UTF8.GetString(content) == "content A");

        ext.MssObject_Move(proj, email, pem, b1, "docs/b.txt", b2, "moved/b.txt");
        ext.MssObject_Exists(proj, email, pem, b1, "docs/b.txt", out exists);
        ext.MssObject_Exists(proj, email, pem, b2, "moved/b.txt", out destExists);
        OK("Object_Move across buckets (source removed)", !exists && destExists);

        ext.MssObject_Delete(proj, email, pem, b2, "moved/b.txt");
        ext.MssObject_Exists(proj, email, pem, b2, "moved/b.txt", out exists);
        OK("Object_Delete removes the object", !exists);

        // --- error paths ---
        try { byte[] c2; string ct2; ext.MssObject_Download(proj, email, pem, b1, "does-not-exist.txt", out c2, out ct2); OK("Download of missing object raises error", false, "no exception"); }
        catch (Exception ex) { OK("Download of missing object raises error", true, ex.Message); }

        try { ext.MssObject_Upload(proj, email, pem, "no-such-bucket-xyz", "f.txt", new byte[] { 1 }, "text/plain", null); OK("Upload to missing bucket raises error", false, "no exception"); }
        catch (Exception ex) { OK("Upload to missing bucket raises error", true, ex.Message); }

        try { ext.MssBucket_Delete(proj, email, pem, b1); OK("Bucket_Delete on non-empty bucket raises error", false, "no exception"); }
        catch (Exception ex) { OK("Bucket_Delete on non-empty bucket raises error", true, ex.Message); }

        // --- custom metadata (v1.5.0) ---
        ext.MssObject_Upload(proj, email, pem, b1, "meta/tagged.txt", Encoding.UTF8.GetBytes("tagged"), "text/plain",
            Meta("department", "finance", "owner", "ricardo"));
        ext.MssObject_GetMetadata(proj, email, pem, b1, "meta/tagged.txt", out mdExists, out md, out custom);
        var tags = ToDict(custom);
        OK("Upload with custom metadata round-trips", mdExists && tags.Count == 2 && tags["department"] == "finance" && tags["owner"] == "ricardo",
            "count=" + tags.Count);

        ext.MssObject_UpdateMetadata(proj, email, pem, b1, "meta/tagged.txt",
            "application/json", "", "attachment; filename=\"tagged.json\"", "public, max-age=3600",
            Meta("env", "prod", "owner", ""));
        ext.MssObject_GetMetadata(proj, email, pem, b1, "meta/tagged.txt", out mdExists, out md, out custom);
        tags = ToDict(custom);
        OK("UpdateMetadata sets fields without re-upload",
            md.ssSTGCS_ObjectMetadata.ssContentType == "application/json" &&
            md.ssSTGCS_ObjectMetadata.ssCacheControl == "public, max-age=3600" &&
            md.ssSTGCS_ObjectMetadata.ssContentDisposition.Contains("tagged.json") &&
            md.ssSTGCS_ObjectMetadata.ssSize == 6,
            md.ssSTGCS_ObjectMetadata.ssContentType + "|" + md.ssSTGCS_ObjectMetadata.ssCacheControl + "|" + md.ssSTGCS_ObjectMetadata.ssSize);
        OK("UpdateMetadata adds and removes custom keys",
            tags.Count == 2 && tags["department"] == "finance" && tags["env"] == "prod" && !tags.ContainsKey("owner"),
            string.Join(",", tags.Keys));

        byte[] rt; string rtCt;
        ext.MssObject_Download(proj, email, pem, b1, "meta/tagged.txt", out rt, out rtCt);
        OK("UpdateMetadata leaves content untouched", Encoding.UTF8.GetString(rt) == "tagged");

        try
        {
            ext.MssObject_UpdateMetadata(proj, email, pem, b1, "meta/tagged.txt", "", "", "", "", null);
            OK("UpdateMetadata with nothing to update rejected", false, "no exception");
        }
        catch (ArgumentException) { OK("UpdateMetadata with nothing to update rejected", true); }

        try
        {
            ext.MssObject_UpdateMetadata(proj, email, pem, b1, "meta/missing.txt", "text/plain", "", "", "", null);
            OK("UpdateMetadata on missing object raises error", false, "no exception");
        }
        catch (Exception ex) { OK("UpdateMetadata on missing object raises error", true, ex.Message); }

        // --- delete by prefix (v1.5.0) ---
        ext.MssObject_Upload(proj, email, pem, b1, "tmp/x1.txt", Encoding.UTF8.GetBytes("1"), "text/plain", null);
        ext.MssObject_Upload(proj, email, pem, b1, "tmp/x2.txt", Encoding.UTF8.GetBytes("2"), "text/plain", null);
        ext.MssObject_Upload(proj, email, pem, b1, "tmp/deep/x3.txt", Encoding.UTF8.GetBytes("3"), "text/plain", null);

        long deletedCount;
        ext.MssObject_DeleteByPrefix(proj, email, pem, b1, "tmp/", out deletedCount);
        ext.MssObject_Exists(proj, email, pem, b1, "tmp/deep/x3.txt", out exists);
        bool rootStill;
        ext.MssObject_Exists(proj, email, pem, b1, "root.txt", out rootStill);
        OK("DeleteByPrefix deletes the whole 'folder' and counts", deletedCount == 3 && !exists, "count=" + deletedCount);
        OK("DeleteByPrefix leaves unrelated objects alone", rootStill);

        ext.MssObject_DeleteByPrefix(proj, email, pem, b1, "tmp/", out deletedCount);
        OK("DeleteByPrefix on empty match -> count 0", deletedCount == 0, "count=" + deletedCount);

        try
        {
            ext.MssObject_DeleteByPrefix(proj, email, pem, b1, "  ", out deletedCount);
            OK("DeleteByPrefix rejects empty prefix", false, "no exception");
        }
        catch (ArgumentException) { OK("DeleteByPrefix rejects empty prefix", true); }

        // --- cleanup via the extension's own actions ---
        foreach (string bucket in new[] { b1, b2 })
        {
            ext.MssObject_List(proj, email, pem, bucket, "", 0, "", "", out objs, out next, out prefixes);
            foreach (var n in objs.ToArray(delegate(RCGCS_ObjectRecord r) { return r.ssSTGCS_Object.ssName; }))
                ext.MssObject_Delete(proj, email, pem, bucket, n);
            ext.MssBucket_Delete(proj, email, pem, bucket);
            ext.MssBucket_Exists(proj, email, pem, bucket, out exists);
            OK("Emptied and deleted bucket '" + bucket + "'", !exists);
        }
    }
}
