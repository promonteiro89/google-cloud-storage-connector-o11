// Live lost-response test against real Google Cloud Storage, with GitHub Actions as the identity provider.
// Runs with -Category Live; skipped outside GitHub Actions. C# 5 only.
//
// A handler between the extension and Google forwards every request. In "drop" mode it lets Google store
// an upload, then tells the extension 503 (as if the response was lost) and keeps failing that upload
// session, so the extension's whole-upload retry and OnlyIfNotExists recovery run against real Google.
// Objects live under E2E/ci/ in GCP_LIVE_BUCKET; the CI identity can touch nothing else.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Http;
using OutSystems.NssGoogleCloudStorage_ext;

public sealed class LostResponseInterceptor : DelegatingHandler
{
    public static string Mode = "pass"; // pass | drop-after-store | fail-first-start
    public static int SessionStarts, MetadataGets, DroppedStored;
    private static readonly HashSet<string> Poisoned = new HashSet<string>();
    private static readonly object Gate = new object();

    public LostResponseInterceptor(HttpMessageHandler inner) : base(inner) { }

    public static void Reset(string mode)
    {
        lock (Gate) { Mode = mode; SessionStarts = 0; MetadataGets = 0; DroppedStored = 0; Poisoned.Clear(); }
    }

    private static string UploadId(Uri u)
    {
        foreach (string kv in u.Query.TrimStart('?').Split('&'))
            if (kv.StartsWith("upload_id=", StringComparison.Ordinal)) return kv.Substring(10);
        return null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri u = request.RequestUri;
        string path = u.AbsolutePath, id = UploadId(u);
        bool isUpload = path.StartsWith("/upload/storage/v1/b/", StringComparison.Ordinal);
        bool isStart = isUpload && id == null && u.Query.Contains("uploadType=resumable");
        bool failThisStart = false;
        lock (Gate)
        {
            if (isStart) SessionStarts++;
            if (request.Method == HttpMethod.Get && path.StartsWith("/storage/v1/b/", StringComparison.Ordinal) && path.Contains("/o/")) MetadataGets++;
            if (id != null && Poisoned.Contains(id)) return new HttpResponseMessage((HttpStatusCode)503);
            failThisStart = isStart && Mode == "fail-first-start" && SessionStarts == 1;
        }
        if (failThisStart) return new HttpResponseMessage((HttpStatusCode)503);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        lock (Gate)
        {
            if (Mode == "drop-after-store" && isUpload && id != null && (int)response.StatusCode == 200 && DroppedStored == 0)
            {
                DroppedStored++;
                Poisoned.Add(id);
                response.Dispose();
                return new HttpResponseMessage((HttpStatusCode)503);
            }
        }
        return response;
    }
}

/// <summary>Google's real HTTP stack with the interceptor on top of the transport.</summary>
public sealed class LostResponseHttpClientFactory : HttpClientFactory
{
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args)
    {
        return new LostResponseInterceptor(base.CreateHandler(args));
    }
}

public static class LiveLostResponseTests
{
    private const string L1 = "LIVE: lost response after Google stored the upload: OnlyIfNotExists call succeeds with Google's generation";
    private const string L1b = "LIVE: ... the response really was dropped and the upload was retried";
    private const string L2 = "LIVE: retry meets another writer's object: 'already exists', that object unchanged";
    private const string L3 = "LIVE: lost response without OnlyIfNotExists: retry overwrites and returns the current generation";

    private static string Env(string name) { return Environment.GetEnvironmentVariable(name); }

    public static void Run()
    {
        string requestUrl = Env("ACTIONS_ID_TOKEN_REQUEST_URL"), requestToken = Env("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        string provider = Env("GCP_LIVE_WIF_PROVIDER"), serviceAccount = Env("GCP_LIVE_SERVICE_ACCOUNT"), bucket = Env("GCP_LIVE_BUCKET");
        var missing = new List<string>();
        if (string.IsNullOrEmpty(requestUrl)) missing.Add("ACTIONS_ID_TOKEN_REQUEST_URL");
        if (string.IsNullOrEmpty(requestToken)) missing.Add("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        if (string.IsNullOrEmpty(provider)) missing.Add("GCP_LIVE_WIF_PROVIDER");
        if (string.IsNullOrEmpty(serviceAccount)) missing.Add("GCP_LIVE_SERVICE_ACCOUNT");
        if (string.IsNullOrEmpty(bucket)) missing.Add("GCP_LIVE_BUCKET");
        if (missing.Count > 0)
        {
            string reason = "live tests run only in GitHub Actions; missing: " + string.Join(", ", missing);
            foreach (string name in new[] { L1, L1b, L2, L3 }) TestLog.Skip(name, reason);
            return;
        }

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Type sut = typeof(CssGoogleCloudStorage_ext);
        PropertyInfo factoryProperty = sut.GetProperty("HttpClientFactoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
        PropertyInfo sleepProperty = sut.GetProperty("RetrySleepOverride", BindingFlags.NonPublic | BindingFlags.Static);
        object savedFactory = factoryProperty.GetValue(null, null), savedSleep = sleepProperty.GetValue(null, null);
        string savedEmulator = EmulatorSeam.Get();
        EmulatorSeam.Set(null);
        factoryProperty.SetValue(null, new LostResponseHttpClientFactory(), null);
        sleepProperty.SetValue(null, null, null); // real retry delays against real Google

        var auth = new RCGCS_AuthenticationRecord(null);
        auth.ssSTGCS_Authentication.ssAuthenticationMethod = "WorkloadIdentityFederation";
        auth.ssSTGCS_Authentication.ssWorkloadIdentityProvider = provider;
        auth.ssSTGCS_Authentication.ssServiceAccountEmail = serviceAccount;
        var ext = new CssGoogleCloudStorage_ext();
        string prefix = "E2E/ci/lost-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "/";
        var created = new List<string>();
        try
        {
            try { auth.ssSTGCS_Authentication.ssSubjectToken = LiveFederationTests.GetGitHubOidcToken(requestUrl, requestToken, provider); }
            catch (Exception e)
            {
                foreach (string name in new[] { L1, L1b, L2, L3 }) TestLog.Check(name, false, "could not obtain the GitHub OIDC token: " + e.Message);
                return;
            }
            var none = new RLGCS_MetadataEntryRecordList();
            long generation;

            // L1
            byte[] mine = Encoding.UTF8.GetBytes("lost response, CI " + Guid.NewGuid().ToString("N"));
            string n1 = prefix + "l1.txt"; created.Add(n1);
            LostResponseInterceptor.Reset("drop-after-store");
            try
            {
                ext.MssObject_Upload(auth, bucket, n1, mine, "text/plain", none, true, "", "", "", out generation);
                LostResponseInterceptor.Reset("pass");
                STGCS_ObjectMetadataStructure m = Metadata(ext, auth, bucket, n1);
                string md5 = Convert.ToBase64String(MD5.Create().ComputeHash(mine));
                TestLog.Check(L1, generation > 0 && generation == m.ssGeneration && m.ssMD5Hash == md5 && m.ssSize == mine.Length,
                    "returned " + generation + ", stored " + m.ssGeneration + ", md5 " + m.ssMD5Hash);
            }
            catch (Exception e) { TestLog.Check(L1, false, e.Message); }
            TestLog.Check(L1b, LostResponseInterceptor.DroppedStored == 1 && LostResponseInterceptor.SessionStarts == 2 && LostResponseInterceptor.MetadataGets >= 1,
                "dropped=" + LostResponseInterceptor.DroppedStored + " sessionStarts=" + LostResponseInterceptor.SessionStarts + " metadataGets=" + LostResponseInterceptor.MetadataGets);

            // L2: same length, different bytes, so the MD5 comparison decides.
            string n2 = prefix + "l2.txt"; created.Add(n2);
            LostResponseInterceptor.Reset("pass");
            try
            {
                ext.MssObject_Upload(auth, bucket, n2, Encoding.UTF8.GetBytes("someone else's object!!"), "text/plain", none, false, "", "", "", out generation);
                long theirs = generation;
                LostResponseInterceptor.Reset("fail-first-start");
                string message = null;
                try { ext.MssObject_Upload(auth, bucket, n2, Encoding.UTF8.GetBytes("this call's own object!!"), "text/plain", none, true, "", "", "", out generation); }
                catch (Exception e) { message = e.Message; }
                int starts = LostResponseInterceptor.SessionStarts;
                LostResponseInterceptor.Reset("pass");
                STGCS_ObjectMetadataStructure m = Metadata(ext, auth, bucket, n2);
                TestLog.Check(L2, message != null && message.Contains("already exists") && message.Contains("OnlyIfNotExists is True") && m.ssGeneration == theirs && starts == 2,
                    (message ?? "no exception") + " | generation " + m.ssGeneration + " vs " + theirs + ", sessionStarts=" + starts);
            }
            catch (Exception e) { TestLog.Check(L2, false, e.Message); }

            // L3
            string n3 = prefix + "l3.txt"; created.Add(n3);
            LostResponseInterceptor.Reset("drop-after-store");
            try
            {
                ext.MssObject_Upload(auth, bucket, n3, Encoding.UTF8.GetBytes("overwrite after a lost response"), "text/plain", none, false, "", "", "", out generation);
                int dropped = LostResponseInterceptor.DroppedStored;
                LostResponseInterceptor.Reset("pass");
                STGCS_ObjectMetadataStructure m = Metadata(ext, auth, bucket, n3);
                TestLog.Check(L3, dropped == 1 && generation == m.ssGeneration, "returned " + generation + ", stored " + m.ssGeneration + ", dropped=" + dropped);
            }
            catch (Exception e) { TestLog.Check(L3, false, e.Message); }
        }
        finally
        {
            LostResponseInterceptor.Reset("pass");
            foreach (string name in created) { try { ext.MssObject_Delete(auth, bucket, name); } catch (Exception) { } }
            factoryProperty.SetValue(null, savedFactory, null);
            sleepProperty.SetValue(null, savedSleep, null);
            EmulatorSeam.Set(savedEmulator);
        }
    }

    private static STGCS_ObjectMetadataStructure Metadata(CssGoogleCloudStorage_ext ext, RCGCS_AuthenticationRecord auth, string bucket, string name)
    {
        bool exists;
        RCGCS_ObjectMetadataRecord metadata;
        RLGCS_MetadataEntryRecordList custom;
        ext.MssObject_GetMetadata(auth, bucket, name, out exists, out metadata, out custom);
        return metadata.ssSTGCS_ObjectMetadata;
    }
}
