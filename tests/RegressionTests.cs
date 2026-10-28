using System;
using System.Net;
using System.Reflection;
using System.Text;
using OutSystems.NssGoogleCloudStorage_ext;

/// <summary>
/// v1.7.0 regression checks: private helpers called through reflection (for Google responses the
/// emulator does not reproduce) and emulator round trips for the empty-text and empty-input fixes.
/// </summary>
public static class RegressionTests
{
    private static object CallPrivate(string name, params object[] args)
    {
        return typeof(CssGoogleCloudStorage_ext).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }

    /// <summary>A 404 shaped like the ones the Google client raises, with Google's message text.</summary>
    private static Google.GoogleApiException NotFound(string message)
    {
        var e = new Google.GoogleApiException("storage", message);
        e.HttpStatusCode = HttpStatusCode.NotFound;
        e.Error = new Google.Apis.Requests.RequestError { Code = 404, Message = message };
        return e;
    }

    public static void RunOffline()
    {

        // A. IsBucketNotFound
        TestLog.Check("IsBucketNotFound: object 404 in a bucket named like 'acme-bucket' is not a bucket 404",
            (bool)CallPrivate("IsBucketNotFound", NotFound("No such object: acme-bucket/docs/a.txt")) == false,
            "expected false");
        TestLog.Check("IsBucketNotFound: object 404 in 'test-bucket-alpha' is not a bucket 404",
            (bool)CallPrivate("IsBucketNotFound", NotFound("No such object: test-bucket-alpha/x.txt")) == false,
            "expected false");
        TestLog.Check("IsBucketNotFound: Google's bucket 404 is detected",
            (bool)CallPrivate("IsBucketNotFound", NotFound("The specified bucket does not exist.")) == true,
            "expected true");
        TestLog.Check("IsBucketNotFound: a bare 'Not Found' is not a bucket 404",
            (bool)CallPrivate("IsBucketNotFound", NotFound("Not Found")) == false,
            "expected false");

        // B. ParseTimestamp
        DateTime expected1 = new DateTime(2026, 10, 28, 10, 0, 0);
        DateTime r1 = (DateTime)CallPrivate("ParseTimestamp", "2026-10-28T10:00:00Z");
        TestLog.Check("ParseTimestamp: Z timestamp",
            r1.Kind != DateTimeKind.Local && r1.Ticks == expected1.Ticks,
            "got " + r1 + " kind=" + r1.Kind);

        DateTime expected2 = new DateTime(2026, 10, 28, 10, 0, 0);
        DateTime r2 = (DateTime)CallPrivate("ParseTimestamp", "2026-10-28T10:00:00");
        TestLog.Check("ParseTimestamp: timestamp without offset is UTC, not local",
            r2.Kind != DateTimeKind.Local && r2.Ticks == expected2.Ticks,
            "got " + r2 + " kind=" + r2.Kind);

        DateTime expected3 = new DateTime(2026, 10, 28, 10, 0, 0);
        DateTime r3 = (DateTime)CallPrivate("ParseTimestamp", "2026-10-28T12:00:00+02:00");
        TestLog.Check("ParseTimestamp: +02:00 offset converted to UTC",
            r3.Kind != DateTimeKind.Local && r3.Ticks == expected3.Ticks,
            "got " + r3 + " kind=" + r3.Kind);

        DateTime expected4 = new DateTime(1900, 1, 1, 0, 0, 0);
        DateTime r4 = (DateTime)CallPrivate("ParseTimestamp", "");
        TestLog.Check("ParseTimestamp: empty gives the OutSystems null date",
            r4.Ticks == expected4.Ticks,
            "got " + r4);

        DateTime expected5 = new DateTime(1900, 1, 1, 0, 0, 0);
        DateTime r5 = (DateTime)CallPrivate("ParseTimestamp", "not-a-date");
        TestLog.Check("ParseTimestamp: garbage gives the OutSystems null date",
            r5.Ticks == expected5.Ticks,
            "got " + r5);
    }

    public static void RunEmulator(CssGoogleCloudStorage_ext ext, RCGCS_AuthenticationRecord auth, string bucket)
    {

        // C. Upload with null content
        ext.MssObject_Upload(auth, bucket, "regr/null-content.bin", null, "application/octet-stream", null);
        byte[] content;
        string contentType;
        ext.MssObject_Download(auth, bucket, "regr/null-content.bin", out content, out contentType);
        TestLog.Check("Upload with null content stores an empty object",
            content != null && content.Length == 0,
            "content length=" + (content == null ? "null" : content.Length.ToString()));

        // D. Upload with empty ContentType
        ext.MssObject_Upload(auth, bucket, "regr/no-type.txt", Encoding.UTF8.GetBytes("x"), "", null);
        byte[] content2;
        string contentType2;
        ext.MssObject_Download(auth, bucket, "regr/no-type.txt", out content2, out contentType2);
        TestLog.Check("Upload with empty ContentType succeeds and Download returns a non-null ContentType",
            contentType2 != null,
            "contentType=" + (contentType2 == null ? "null" : contentType2));

        // E. GetMetadata returns empty text, not null, for unset fields
        bool exists;
        RCGCS_ObjectMetadataRecord md;
        RLGCS_MetadataEntryRecordList custom;
        ext.MssObject_GetMetadata(auth, bucket, "regr/no-type.txt", out exists, out md, out custom);
        TestLog.Check("GetMetadata returns empty text, not null, for unset fields",
            exists && md.ssSTGCS_ObjectMetadata.ssContentEncoding != null
                && md.ssSTGCS_ObjectMetadata.ssContentDisposition != null
                && md.ssSTGCS_ObjectMetadata.ssCacheControl != null
                && md.ssSTGCS_ObjectMetadata.ssMediaLink != null,
            "exists=" + exists + " encoding=" + (md.ssSTGCS_ObjectMetadata.ssContentEncoding == null ? "null" : md.ssSTGCS_ObjectMetadata.ssContentEncoding)
                + " disposition=" + (md.ssSTGCS_ObjectMetadata.ssContentDisposition == null ? "null" : md.ssSTGCS_ObjectMetadata.ssContentDisposition)
                + " cacheControl=" + (md.ssSTGCS_ObjectMetadata.ssCacheControl == null ? "null" : md.ssSTGCS_ObjectMetadata.ssCacheControl)
                + " mediaLink=" + (md.ssSTGCS_ObjectMetadata.ssMediaLink == null ? "null" : md.ssSTGCS_ObjectMetadata.ssMediaLink));

        // F. Object_List returns no null Name or ContentType
        RLGCS_ObjectRecordList list;
        string next;
        RLGCS_PrefixRecordList prefixes;
        ext.MssObject_List(auth, bucket, "regr/", 0, "", "", out list, out next, out prefixes);
        RCGCS_ObjectRecord[] records = list.ToArray(delegate(RCGCS_ObjectRecord r) { return r; });
        bool allValid = records.Length >= 2;
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].ssSTGCS_Object.ssName == null || records[i].ssSTGCS_Object.ssContentType == null)
            {
                allValid = false;
                break;
            }
        }
        TestLog.Check("Object_List returns no null Name or ContentType",
            allValid,
            "count=" + records.Length);

        // G. Clean up
        ext.MssObject_Delete(auth, bucket, "regr/null-content.bin");
        ext.MssObject_Delete(auth, bucket, "regr/no-type.txt");
    }
}
