using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Http;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json.Linq;
using OutSystems.HubEdition.RuntimePlatform;

namespace OutSystems.NssGoogleCloudStorage_ext
{
	public class CssGoogleCloudStorage_ext : IssGoogleCloudStorage_ext
	{

		/// <summary>
		/// Updates an object&apos;s metadata without re-uploading its content. Only the provided fields are changed: empty text inputs leave the corresponding field untouched, and an empty Metadata list leaves custom metadata untouched. Within Metadata, an entry with an empty Value removes that key.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the object to update.</param>
		/// <param name="ssContentType">New MIME type. Empty = unchanged.</param>
		/// <param name="ssContentEncoding">New content encoding (e.g. &apos;gzip&apos;). Empty = unchanged.</param>
		/// <param name="ssContentDisposition">New content disposition (e.g. &apos;attachment; filename=&quot;report.pdf&quot;&apos;). Empty = unchanged.</param>
		/// <param name="ssCacheControl">New cache control (e.g. &apos;public, max-age=3600&apos;). Empty = unchanged.</param>
		/// <param name="ssMetadata">Custom metadata changes. Empty list = unchanged. An entry with empty Value removes that key; others are set/overwritten.</param>
		public void MssObject_UpdateMetadata(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName, string ssContentType, string ssContentEncoding, string ssContentDisposition, string ssCacheControl, RLGCS_MetadataEntryRecordList ssMetadata) {
			var changes = ToMetadataDictionary(ssMetadata);
			bool hasFieldChange = !string.IsNullOrEmpty(ssContentType) || !string.IsNullOrEmpty(ssContentEncoding)
				|| !string.IsNullOrEmpty(ssContentDisposition) || !string.IsNullOrEmpty(ssCacheControl);
			if (!hasFieldChange && changes == null)
				throw new ArgumentException("Nothing to update: provide at least one of ContentType, ContentEncoding, ContentDisposition, CacheControl, or a non-empty Metadata list.");

			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			try
			{
				// Read-modify-write: fetch the current object, apply only the provided changes, and
				// write it back guarded by a metageneration precondition so a concurrent metadata
				// change fails cleanly (412) instead of being silently overwritten.
				var obj = storageClient.GetObject(ssBucketName, ssObjectName);

				if (!string.IsNullOrEmpty(ssContentType)) obj.ContentType = ssContentType;
				if (!string.IsNullOrEmpty(ssContentEncoding)) obj.ContentEncoding = ssContentEncoding;
				if (!string.IsNullOrEmpty(ssContentDisposition)) obj.ContentDisposition = ssContentDisposition;
				if (!string.IsNullOrEmpty(ssCacheControl)) obj.CacheControl = ssCacheControl;

				if (changes != null)
				{
					var merged = obj.Metadata != null ? new Dictionary<string, string>(obj.Metadata) : new Dictionary<string, string>();
					foreach (var kv in changes)
					{
						if (kv.Value.Length == 0) merged.Remove(kv.Key);
						else merged[kv.Key] = kv.Value;
					}
					obj.Metadata = merged;
				}

				storageClient.UpdateObject(obj, new UpdateObjectOptions { IfMetagenerationMatch = obj.Metageneration });
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_UpdateMetadata

		/// <summary>
		/// Deletes all objects whose names start with the given prefix (a &apos;folder&apos; and everything under it). The Prefix is mandatory and cannot be empty, as a safety measure against accidentally wiping an entire bucket. Returns the number of objects deleted.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssPrefix">All objects whose names start with this prefix are deleted (e.g. &apos;uploads/2025/&apos;). Cannot be empty.</param>
		/// <param name="ssDeletedCount">Number of objects that were deleted.</param>
		public void MssObject_DeleteByPrefix(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssPrefix, out long ssDeletedCount) {
			ssDeletedCount = 0;
			if (string.IsNullOrWhiteSpace(ssPrefix))
				throw new ArgumentException("Prefix cannot be empty - it would delete every object in the bucket. To do that intentionally, delete the bucket or list and delete the objects explicitly.");

			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			// Materialize the names first so deletions can't interfere with listing pagination.
			var names = new List<string>();
			try
			{
				foreach (var obj in storageClient.ListObjects(ssBucketName, ssPrefix))
					names.Add(obj.Name);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }

			long deleted = 0;
			foreach (var name in names)
			{
				try
				{
					storageClient.DeleteObject(ssBucketName, name);
					deleted++;
				}
				catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
				{
					// Already gone (deleted concurrently) - the desired state is reached, keep going.
				}
				catch (Google.GoogleApiException e)
				{
					throw new Exception("Deleted " + deleted + " of " + names.Count + " objects under prefix '" + ssPrefix + "', then failed on '" + name + "': " + e.Message, e);
				}
				catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
			}

			ssDeletedCount = deleted;
		} // MssObject_DeleteByPrefix

		/// <summary>
		/// Checks whether a bucket exists and is accessible to the service account, without listing its contents.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssExists">True if the bucket exists and the service account can access it.</param>
		public void MssBucket_Exists(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, out bool ssExists) {
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			try
			{
				storageClient.GetBucket(ssBucketName);
				ssExists = true;
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssBucket_Exists

		/// <summary>
		/// Copies an object to another location, within the same bucket or across buckets, without downloading its content. If the destination object exists, it will be overwritten.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssSourceBucketName">The bucket that currently contains the object.</param>
		/// <param name="ssSourceObjectName">The full path/name of the source object (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssDestinationBucketName">The bucket to copy the object into (can be the same as the source).</param>
		/// <param name="ssDestinationObjectName">The full path/name for the destination object.</param>
		public void MssObject_Copy(RCGCS_AuthenticationRecord ssAuthentication, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName) {
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			try
			{
				storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssSourceBucketName, ssSourceObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_Copy

		/// <summary>
		/// Moves an object to another location (copy + delete of the source), within the same bucket or across buckets. Use the same source and destination bucket to rename an object. If the destination exists, it will be overwritten.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssSourceBucketName">The bucket that currently contains the object.</param>
		/// <param name="ssSourceObjectName">The full path/name of the source object (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssDestinationBucketName">The bucket to move the object into (can be the same as the source).</param>
		/// <param name="ssDestinationObjectName">The full path/name for the destination object.</param>
		public void MssObject_Move(RCGCS_AuthenticationRecord ssAuthentication, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName) {
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			try
			{
				storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssSourceBucketName, ssSourceObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }

			// Move is copy-then-delete and is not atomic: if the delete fails, both objects exist.
			// Surface that state explicitly instead of a generic error.
			try
			{
				storageClient.DeleteObject(ssSourceBucketName, ssSourceObjectName);
			}
			catch (Google.GoogleApiException e)
			{
				throw new Exception("The object was copied to '" + ssDestinationBucketName + "/" + ssDestinationObjectName + "' but the source '" + ssSourceBucketName + "/" + ssSourceObjectName + "' could not be deleted - both objects currently exist. Cause: " + e.Message, e);
			}
		} // MssObject_Move

		/// <summary>
		/// Retrieves the metadata of a specific object (size, content type, hashes, generation, timestamps, storage class) without downloading its content. Returns Exists = False if the object does not exist.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssExists">Returns True if the object was found in the bucket, and False if it does not exist. When False, the Metadata record is returned empty.</param>
		/// <param name="ssMetadata">The metadata of the object (size, content type, hashes, version identifiers, storage class, and timestamps), retrieved without downloading its content. Only populated when Exists is True.</param>
		/// <param name="ssCustomMetadata">The object&apos;s custom key-value metadata. Empty when the object has none or does not exist.</param>
		public void MssObject_GetMetadata(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName, out bool ssExists, out RCGCS_ObjectMetadataRecord ssMetadata, out RLGCS_MetadataEntryRecordList ssCustomMetadata) {
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			ssExists = false;
			ssMetadata = new RCGCS_ObjectMetadataRecord(null);
			ssCustomMetadata = new RLGCS_MetadataEntryRecordList();

			try
			{
				var obj = storageClient.GetObject(ssBucketName, ssObjectName);
				ssExists = true;

				ssMetadata.ssSTGCS_ObjectMetadata.ssName = obj.Name;
				ssMetadata.ssSTGCS_ObjectMetadata.ssBucket = obj.Bucket;
				ssMetadata.ssSTGCS_ObjectMetadata.ssSize = (long)Math.Min(obj.Size ?? 0, long.MaxValue);
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentType = obj.ContentType;
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentEncoding = obj.ContentEncoding;
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentDisposition = obj.ContentDisposition;
				ssMetadata.ssSTGCS_ObjectMetadata.ssCacheControl = obj.CacheControl;
				ssMetadata.ssSTGCS_ObjectMetadata.ssMD5Hash = obj.Md5Hash;
				ssMetadata.ssSTGCS_ObjectMetadata.ssCrc32c = obj.Crc32c;
				ssMetadata.ssSTGCS_ObjectMetadata.ssETag = obj.ETag;
				ssMetadata.ssSTGCS_ObjectMetadata.ssGeneration = obj.Generation ?? 0;
				ssMetadata.ssSTGCS_ObjectMetadata.ssMetageneration = obj.Metageneration ?? 0;
				ssMetadata.ssSTGCS_ObjectMetadata.ssStorageClass = obj.StorageClass;
				ssMetadata.ssSTGCS_ObjectMetadata.ssMediaLink = obj.MediaLink;
				ssMetadata.ssSTGCS_ObjectMetadata.ssTimeCreated = ParseTimestamp(obj.TimeCreatedRaw);
				ssMetadata.ssSTGCS_ObjectMetadata.ssUpdated = ParseTimestamp(obj.UpdatedRaw);

				if (obj.Metadata != null)
				{
					foreach (var kv in obj.Metadata)
					{
						var entry = new RCGCS_MetadataEntryRecord(null);
						entry.ssSTGCS_MetadataEntry.ssKey = kv.Key;
						entry.ssSTGCS_MetadataEntry.ssValue = kv.Value;
						ssCustomMetadata.Append(entry);
					}
				}
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsBucketNotFound(e))
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_GetMetadata
		// =====================================================================================
		// Authentication
		//
		// Two methods, identical to the ODC connector:
		//  - WorkloadIdentityFederation (recommended): no Google key exists anywhere. The extension
		//    obtains a JWT from any OIDC identity provider (OAuth 2.0 client-credentials grant, or a
		//    token the caller supplies), exchanges it with Google's Security Token Service, and
		//    impersonates a service account for a short-lived access token. Signed URLs are signed
		//    through the IAM Credentials signBlob API.
		//  - ServiceAccountKey (legacy, Google's last resort): ClientEmail + PrivateKey from a service
		//    account JSON key. Signed URLs are signed locally. This remains the default.
		//
		// Every action takes one Authentication record (Authentication), field for field the
		// ODC connector's Authentication structure. Leave AuthenticationMethod empty for
		// ServiceAccountKey.
		// =====================================================================================

		internal const string MethodServiceAccountKey = "ServiceAccountKey";
		internal const string MethodWorkloadIdentityFederation = "WorkloadIdentityFederation";

		private const string StsTokenUrl = "https://sts.googleapis.com/v1/token";
		private const string JwtSubjectTokenType = "urn:ietf:params:oauth:token-type:jwt";

		/// <summary>
		/// The credentials an action runs with: a plain copy of the Authentication record, so the
		/// auth layer doesn't depend on Integration Studio's generated types.
		/// </summary>
		internal sealed class AuthConfig
		{
			public string ProjectId = "";
			public string AuthenticationMethod = "";
			public string ClientEmail = "";
			public string PrivateKey = "";
			public string WorkloadIdentityProvider = "";
			public string ServiceAccountEmail = "";
			public string TokenEndpoint = "";
			public string ClientId = "";
			public string ClientSecret = "";
			public string Scope = "";
			public string Audience = "";
			public string SubjectToken = "";
		}

		/// <summary>Copies the action's Authentication record into the auth layer's own type.</summary>
		private static AuthConfig FromRecord(RCGCS_AuthenticationRecord record)
		{
			var s = record.ssSTGCS_Authentication;
			return new AuthConfig
			{
				ProjectId = s.ssProjectId ?? "",
				AuthenticationMethod = s.ssAuthenticationMethod ?? "",
				ClientEmail = s.ssClientEmail ?? "",
				PrivateKey = s.ssPrivateKey ?? "",
				WorkloadIdentityProvider = s.ssWorkloadIdentityProvider ?? "",
				ServiceAccountEmail = s.ssServiceAccountEmail ?? "",
				TokenEndpoint = s.ssTokenEndpoint ?? "",
				ClientId = s.ssClientId ?? "",
				ClientSecret = s.ssClientSecret ?? "",
				Scope = s.ssScope ?? "",
				Audience = s.ssAudience ?? "",
				SubjectToken = s.ssSubjectToken ?? ""
			};
		}

		/// <summary>
		/// ProjectId is only used by the project-scoped bucket actions; checking it there gives a clear
		/// message instead of Google's generic "invalid project" error.
		/// </summary>
		private static void RequireProjectId(AuthConfig a, string action)
		{
			if (string.IsNullOrWhiteSpace(a.ProjectId))
				throw new ArgumentException(action + " requires Authentication.ProjectId.");
		}

		/// <summary>
		/// Caches of StorageClient/UrlSigner instances per credential. Extension actions run on every
		/// request, and creating a client per call re-parses keys (or re-runs the token exchange) and
		/// allocates a new HttpClient each time (latency + socket exhaustion under load). Statics survive
		/// across requests in the app domain, and StorageClient, UrlSigner and the Google credentials are
		/// thread-safe. Keys are SHA-256 hashes, so raw secrets are never retained as cache keys. The two
		/// methods use distinct key prefixes, so they can never share a cached client.
		/// </summary>
		private static readonly ConcurrentDictionary<string, StorageClient> StorageClientCache = new ConcurrentDictionary<string, StorageClient>();
		private static readonly ConcurrentDictionary<string, UrlSigner> UrlSignerCache = new ConcurrentDictionary<string, UrlSigner>();
		private static readonly ConcurrentDictionary<string, FederatedIdentity> FederatedIdentityCache = new ConcurrentDictionary<string, FederatedIdentity>();

		private static readonly Lazy<HttpClient> IdentityProviderHttp = new Lazy<HttpClient>(() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

		/// <summary>
		/// Test seam: when set, all Google auth/Storage HTTP traffic and identity provider token requests
		/// in WorkloadIdentityFederation mode go through this factory. Never set in production.
		/// </summary>
		internal static IHttpClientFactory HttpClientFactoryOverride { get; set; }

		private enum AuthMethod { ServiceAccountKey, WorkloadIdentityFederation }

		// ---- Method resolution and validation ----------------------------------------------

		private static AuthMethod ResolveAuthMethod(AuthConfig a)
		{
			string method = (a.AuthenticationMethod ?? string.Empty).Trim();
			AuthMethod resolved;
			if (method.Length == 0 || method.Equals(MethodServiceAccountKey, StringComparison.OrdinalIgnoreCase))
				resolved = AuthMethod.ServiceAccountKey;
			else if (method.Equals(MethodWorkloadIdentityFederation, StringComparison.OrdinalIgnoreCase))
				resolved = AuthMethod.WorkloadIdentityFederation;
			else
				throw new ArgumentException("Invalid AuthenticationMethod '" + a.AuthenticationMethod + "'. Use '" + MethodWorkloadIdentityFederation + "' or '" + MethodServiceAccountKey + "' (or leave it empty for " + MethodServiceAccountKey + ").");

			var missing = new List<string>();
			if (resolved == AuthMethod.ServiceAccountKey)
			{
				if (string.IsNullOrWhiteSpace(a.ClientEmail)) missing.Add("ClientEmail");
				if (string.IsNullOrWhiteSpace(a.PrivateKey)) missing.Add("PrivateKey");
				if (missing.Count > 0)
					throw new ArgumentException(MethodServiceAccountKey + " authentication requires ClientEmail and PrivateKey (missing: " + string.Join(", ", missing) + ").");
				return resolved;
			}

			if (string.IsNullOrWhiteSpace(a.WorkloadIdentityProvider)) missing.Add("WorkloadIdentityProvider");
			if (string.IsNullOrWhiteSpace(a.ServiceAccountEmail)) missing.Add("ServiceAccountEmail");
			if (string.IsNullOrWhiteSpace(a.SubjectToken))
			{
				if (string.IsNullOrWhiteSpace(a.TokenEndpoint)) missing.Add("TokenEndpoint");
				if (string.IsNullOrWhiteSpace(a.ClientId)) missing.Add("ClientId");
				if (string.IsNullOrWhiteSpace(a.ClientSecret)) missing.Add("ClientSecret");
			}
			if (missing.Count > 0)
				throw new ArgumentException(MethodWorkloadIdentityFederation + " authentication requires WorkloadIdentityProvider, ServiceAccountEmail, and either SubjectToken or TokenEndpoint + ClientId + ClientSecret (missing: " + string.Join(", ", missing) + ").");

			NormalizeWorkloadIdentityProvider(a.WorkloadIdentityProvider); // throws on a malformed value
			if (string.IsNullOrWhiteSpace(a.SubjectToken))
				ValidateTokenEndpoint(a.TokenEndpoint);
			return resolved;
		}

		/// <summary>The identity whose permissions apply, for error messages.</summary>
		private static string IdentityOf(AuthConfig a)
		{
			return ResolveAuthMethodSafe(a) == AuthMethod.WorkloadIdentityFederation ? a.ServiceAccountEmail : a.ClientEmail;
		}

		private static AuthMethod ResolveAuthMethodSafe(AuthConfig a)
		{
			return (a.AuthenticationMethod ?? string.Empty).Trim().Equals(MethodWorkloadIdentityFederation, StringComparison.OrdinalIgnoreCase)
				? AuthMethod.WorkloadIdentityFederation
				: AuthMethod.ServiceAccountKey;
		}

		/// <summary>
		/// Accepts the provider as '//iam.googleapis.com/projects/..', 'https://iam.googleapis.com/projects/..'
		/// or 'projects/..' and returns the STS audience form '//iam.googleapis.com/projects/..'.
		/// </summary>
		internal static string NormalizeWorkloadIdentityProvider(string provider)
		{
			string p = (provider ?? string.Empty).Trim();
			if (p.StartsWith("https:", StringComparison.OrdinalIgnoreCase)) p = p.Substring("https:".Length);
			if (p.StartsWith("projects/", StringComparison.Ordinal)) p = "//iam.googleapis.com/" + p;

			bool valid = p.StartsWith("//iam.googleapis.com/projects/", StringComparison.Ordinal)
				&& p.IndexOf("/locations/", StringComparison.Ordinal) >= 0
				&& p.IndexOf("/workloadIdentityPools/", StringComparison.Ordinal) >= 0
				&& p.IndexOf("/providers/", StringComparison.Ordinal) >= 0;
			if (!valid)
				throw new ArgumentException("WorkloadIdentityProvider '" + provider + "' is not a workload identity provider resource name. Expected '//iam.googleapis.com/projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/POOL_ID/providers/PROVIDER_ID'.");
			return p;
		}

		private static void ValidateTokenEndpoint(string tokenEndpoint)
		{
			Uri uri;
			bool ok = Uri.TryCreate((tokenEndpoint ?? string.Empty).Trim(), UriKind.Absolute, out uri)
				&& (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
			if (!ok)
				throw new ArgumentException("TokenEndpoint '" + tokenEndpoint + "' must be an absolute https:// URL (for example 'https://login.microsoftonline.com/TENANT_ID/oauth2/v2.0/token').");
		}

		private static string CacheKey(string prefix, params string[] parts)
		{
			using (var sha = SHA256.Create())
			{
				var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", parts.Select(p => p ?? string.Empty))));
				return prefix + "|" + Convert.ToBase64String(hash);
			}
		}

		private static string FederatedCacheKey(AuthConfig a)
		{
			return CacheKey("wif",
				NormalizeWorkloadIdentityProvider(a.WorkloadIdentityProvider),
				a.ServiceAccountEmail.Trim(),
				string.IsNullOrWhiteSpace(a.SubjectToken) ? "client-credentials" : "supplied-token",
				a.TokenEndpoint == null ? null : a.TokenEndpoint.Trim(), a.ClientId, a.ClientSecret, a.Scope, a.Audience);
		}

		// ---- Client factories --------------------------------------------------------------

		/// <summary>
		/// Returns a cached StorageClient for the given credentials, creating it on first use.
		/// Honors the GCSCONNECTOR_EMULATOR_HOST environment variable (never set on a real
		/// OutSystems server): when present, connects unauthenticated to a local GCS emulator
		/// such as fake-gcs-server, enabling integration tests without Google credentials.
		/// The name is deliberately extension-specific (not Google's STORAGE_EMULATOR_HOST) so a
		/// machine-wide variable set for other tooling can never silently redirect this extension.
		/// </summary>
		private static StorageClient GetStorageClient(AuthConfig a)
		{
			var method = ResolveAuthMethod(a);

			string emulatorHost = Environment.GetEnvironmentVariable("GCSCONNECTOR_EMULATOR_HOST");
			if (!string.IsNullOrEmpty(emulatorHost))
			{
				string baseUri = (emulatorHost.Contains("://") ? emulatorHost : "http://" + emulatorHost).TrimEnd('/') + "/storage/v1/";
				return StorageClientCache.GetOrAdd(
					"emulator|" + baseUri,
					_ => new StorageClientBuilder { BaseUri = baseUri, UnauthenticatedAccess = true }.Build());
			}

			if (method == AuthMethod.WorkloadIdentityFederation)
			{
				var identity = GetFederatedIdentity(a);
				return StorageClientCache.GetOrAdd(FederatedCacheKey(a), _ =>
				{
					var builder = new StorageClientBuilder { GoogleCredential = identity.Credential };
					if (HttpClientFactoryOverride != null) builder.HttpClientFactory = HttpClientFactoryOverride;
					return builder.Build();
				});
			}

			return StorageClientCache.GetOrAdd(
				CacheKey("key", a.ClientEmail, a.PrivateKey),
				_ => StorageClient.Create(GetServiceAccountCredential(a).ToGoogleCredential()));
		}

		/// <summary>
		/// Returns a cached UrlSigner for the given credentials, creating it on first use. With a
		/// service account key the signature is computed locally; with Workload Identity Federation it
		/// is produced by the IAM Credentials signBlob API as the impersonated service account.
		/// </summary>
		private static UrlSigner GetUrlSigner(AuthConfig a)
		{
			if (ResolveAuthMethod(a) == AuthMethod.WorkloadIdentityFederation)
			{
				var identity = GetFederatedIdentity(a);
				return UrlSignerCache.GetOrAdd(FederatedCacheKey(a), _ => UrlSigner.FromCredential(identity.Credential));
			}

			return UrlSignerCache.GetOrAdd(
				CacheKey("key", a.ClientEmail, a.PrivateKey),
				_ => UrlSigner.FromCredential(GetServiceAccountCredential(a)));
		}

		private static ServiceAccountCredential GetServiceAccountCredential(AuthConfig a)
		{
			try
			{
				var initializer = new ServiceAccountCredential.Initializer(a.ClientEmail)
				{
					Scopes = new[] { StorageService.Scope.CloudPlatform }
				}.FromPrivateKey(a.PrivateKey.Replace("\\n", "\n"));

				return new ServiceAccountCredential(initializer);
			}
			catch (Exception e)
			{
				throw new ArgumentException("The PrivateKey could not be parsed. Provide the full 'private_key' value from the service account JSON key, including the -----BEGIN PRIVATE KEY----- and -----END PRIVATE KEY----- lines.", e);
			}
		}

		// ---- Workload Identity Federation --------------------------------------------------

		/// <summary>
		/// A federated credential plus the source of its identity-provider token. The credential chain is
		/// external account (subject token -> Google STS) -> impersonated service account (IAM Credentials).
		/// The SDK refreshes both automatically, calling back into the token source only when needed.
		/// </summary>
		private sealed class FederatedIdentity
		{
			public FederatedIdentity(GoogleCredential credential, SubjectTokenSource tokenSource)
			{
				Credential = credential;
				TokenSource = tokenSource;
			}

			public GoogleCredential Credential { get; private set; }
			public SubjectTokenSource TokenSource { get; private set; }
		}

		private static FederatedIdentity GetFederatedIdentity(AuthConfig a)
		{
			var identity = FederatedIdentityCache.GetOrAdd(FederatedCacheKey(a), _ => CreateFederatedIdentity(a));
			if (!string.IsNullOrWhiteSpace(a.SubjectToken))
				identity.TokenSource.Supply(a.SubjectToken.Trim()); // latest caller-supplied token wins
			return identity;
		}

		private static FederatedIdentity CreateFederatedIdentity(AuthConfig a)
		{
			var tokenSource = new SubjectTokenSource(a.TokenEndpoint == null ? null : a.TokenEndpoint.Trim(), a.ClientId, a.ClientSecret, a.Scope, a.Audience);

			var externalInitializer = new ProgrammaticExternalAccountCredential.Initializer(
				StsTokenUrl, NormalizeWorkloadIdentityProvider(a.WorkloadIdentityProvider), JwtSubjectTokenType, tokenSource)
			{
				Scopes = new[] { StorageService.Scope.CloudPlatform }
			};
			if (HttpClientFactoryOverride != null) externalInitializer.HttpClientFactory = HttpClientFactoryOverride;
			var federated = GoogleCredential.FromProgrammaticExternalAccountCredential(new ProgrammaticExternalAccountCredential(externalInitializer));

			var impersonationInitializer = new ImpersonatedCredential.Initializer(a.ServiceAccountEmail.Trim())
			{
				Scopes = new[] { StorageService.Scope.CloudPlatform }
			};
			if (HttpClientFactoryOverride != null) impersonationInitializer.HttpClientFactory = HttpClientFactoryOverride;

			return new FederatedIdentity(federated.Impersonate(impersonationInitializer), tokenSource);
		}

		/// <summary>
		/// Supplies the identity-provider JWT to Google's STS exchange: either the token the caller passed
		/// in (SubjectToken), or one obtained with the standard OAuth 2.0 client-credentials grant
		/// (RFC 6749 section 4.4), which is the same call on Entra ID, Okta, Auth0, Keycloak and others.
		/// Tokens are cached until shortly before they expire.
		/// </summary>
		private sealed class SubjectTokenSource : ProgrammaticExternalAccountCredential.ISubjectTokenProvider
		{
			private readonly string _tokenEndpoint, _clientId, _clientSecret, _scope, _audience;
			private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
			private volatile string _suppliedToken;
			private string _cachedToken;
			private DateTimeOffset _cachedUntil;
			private bool _useBasicClientAuth;

			public SubjectTokenSource(string tokenEndpoint, string clientId, string clientSecret, string scope, string audience)
			{
				_tokenEndpoint = tokenEndpoint;
				_clientId = clientId;
				_clientSecret = clientSecret;
				_scope = scope;
				_audience = audience;
			}

			public void Supply(string token) { _suppliedToken = token; }

			public async Task<string> GetSubjectTokenAsync(ProgrammaticExternalAccountCredential caller, CancellationToken taskCancellationToken)
			{
				string supplied = _suppliedToken;
				if (!string.IsNullOrEmpty(supplied))
					return supplied;

				await _lock.WaitAsync(taskCancellationToken).ConfigureAwait(false);
				try
				{
					if (_cachedToken != null && DateTimeOffset.UtcNow < _cachedUntil)
						return _cachedToken;

					var result = await RequestClientCredentialsTokenAsync(taskCancellationToken).ConfigureAwait(false);
					_cachedToken = result.Token;
					// Refresh a minute early (or halfway through a very short lifetime).
					var margin = result.Lifetime > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(1) : TimeSpan.FromTicks(result.Lifetime.Ticks / 2);
					_cachedUntil = DateTimeOffset.UtcNow + result.Lifetime - margin;
					return result.Token;
				}
				finally
				{
					_lock.Release();
				}
			}

			private sealed class IdpToken
			{
				public string Token;
				public TimeSpan Lifetime;
			}

			private async Task<IdpToken> RequestClientCredentialsTokenAsync(CancellationToken ct)
			{
				HttpClient http = HttpClientFactoryOverride != null
					? HttpClientFactoryOverride.CreateHttpClient(new CreateHttpClientArgs())
					: IdentityProviderHttp.Value;

				// client_secret_post first (Entra ID, Auth0, Keycloak); if the provider rejects the client,
				// retry once with HTTP Basic client authentication (Okta's default).
				for (int attempt = 0; ; attempt++)
				{
					bool basic = _useBasicClientAuth || attempt > 0;
					using (var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint))
					{
						var form = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("grant_type", "client_credentials") };
						if (basic)
						{
							var raw = Uri.EscapeDataString(_clientId ?? string.Empty) + ":" + Uri.EscapeDataString(_clientSecret ?? string.Empty);
							request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
						}
						else
						{
							form.Add(new KeyValuePair<string, string>("client_id", _clientId ?? string.Empty));
							form.Add(new KeyValuePair<string, string>("client_secret", _clientSecret ?? string.Empty));
						}
						if (!string.IsNullOrWhiteSpace(_scope)) form.Add(new KeyValuePair<string, string>("scope", _scope));
						if (!string.IsNullOrWhiteSpace(_audience)) form.Add(new KeyValuePair<string, string>("audience", _audience));
						request.Content = new FormUrlEncodedContent(form);

						HttpResponseMessage response;
						try
						{
							response = await http.SendAsync(request, ct).ConfigureAwait(false);
						}
						catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
						{
							throw new IdentityProviderException("Could not reach the identity provider token endpoint '" + _tokenEndpoint + "': " + e.Message, e);
						}

						using (response)
						{
							string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
							if (response.IsSuccessStatusCode)
							{
								int expiresIn;
								string token = ParseTokenResponse(body, out expiresIn);
								if (!LooksLikeJwt(token))
									throw new IdentityProviderException("The identity provider returned a token that is not a JWT, but Workload Identity Federation requires a signed JWT. For Entra ID, request a token for your own app registration (Scope 'api://YOUR_APP_ID/.default'), not Microsoft Graph; for Okta, use a custom authorization server.");
								if (basic) _useBasicClientAuth = true;
								return new IdpToken { Token = token, Lifetime = TimeSpan.FromSeconds(expiresIn) };
							}

							string error, description;
							ParseOAuthError(body, out error, out description);
							bool invalidClient = response.StatusCode == HttpStatusCode.Unauthorized || error == "invalid_client";
							if (!basic && invalidClient && attempt == 0)
								continue;

							string detail = description ?? (body.Length > 300 ? body.Substring(0, 300) + "..." : body);
							throw new IdentityProviderException("The identity provider rejected the client-credentials token request (" + (int)response.StatusCode + " " + (error ?? response.StatusCode.ToString()) + "): " + detail + ". Check TokenEndpoint, ClientId, ClientSecret, and Scope/Audience.");
						}
					}
				}
			}

			private static string ParseTokenResponse(string body, out int expiresIn)
			{
				JObject root;
				try { root = JObject.Parse(body); }
				catch (Newtonsoft.Json.JsonException e) { throw new IdentityProviderException("The identity provider's token response was not valid JSON.", e); }

				string token = (string)root["access_token"];
				if (string.IsNullOrEmpty(token)) token = (string)root["id_token"];
				if (string.IsNullOrEmpty(token))
					throw new IdentityProviderException("The identity provider's token response did not contain an 'access_token'.");

				expiresIn = 3600;
				JToken ei = root["expires_in"];
				int n;
				if (ei != null && int.TryParse(ei.ToString(), out n)) expiresIn = n;
				expiresIn = Math.Max(expiresIn, 30);
				return token;
			}

			private static void ParseOAuthError(string body, out string error, out string description)
			{
				error = null;
				description = null;
				try
				{
					var root = JObject.Parse(body);
					error = (string)root["error"];
					description = (string)root["error_description"];
				}
				catch (Exception) { /* not JSON: the caller falls back to the raw body */ }
			}

			private static bool LooksLikeJwt(string token)
			{
				var parts = token.Split('.');
				return parts.Length == 3 && parts.All(p => p.Length > 0);
			}
		}

		/// <summary>A failure obtaining a token from the identity provider (surfaced unchanged to the caller).</summary>
		internal sealed class IdentityProviderException : Exception
		{
			public IdentityProviderException(string message, Exception inner = null) : base(message, inner) { }
		}

		// ---- Auth-aware error translation --------------------------------------------------

		private static string UnauthenticatedHint(AuthConfig a)
		{
			return ResolveAuthMethodSafe(a) == AuthMethod.WorkloadIdentityFederation
				? "Check the Workload Identity Federation setup (provider, attribute condition) and that the federated identity has 'Workload Identity User' on '" + a.ServiceAccountEmail + "'."
				: "Check that ClientEmail and PrivateKey belong to the same service account and that the key has not been revoked.";
		}

		/// <summary>
		/// Translates a token endpoint failure (key: typically 'invalid_grant'; federation: the STS
		/// exchange or the impersonation was rejected) into an actionable message.
		/// </summary>
		private static Exception FriendlyAuthException(TokenResponseException e, AuthConfig a)
		{
			if (ResolveAuthMethodSafe(a) == AuthMethod.WorkloadIdentityFederation)
				return new Exception("Workload Identity Federation failed for service account '" + a.ServiceAccountEmail + "': Google rejected the token exchange or impersonation. Common causes: a wrong WorkloadIdentityProvider value, a token whose issuer or audience does not match the provider, a token rejected by the attribute condition, or a federated identity without 'Workload Identity User' on the service account. Details: " + e.Message, e);

			return new Exception("Google rejected the service account credentials for '" + a.ClientEmail + "' (ClientEmail/PrivateKey mismatch, deleted service account, revoked key, or server clock skew). Details: " + e.Message, e);
		}

		/// <summary>Translates a failure while signing a URL (federation: the signBlob call) into an actionable message.</summary>
		private static Exception FriendlySigningException(Exception e, AuthConfig a)
		{
			if (ResolveAuthMethodSafe(a) == AuthMethod.WorkloadIdentityFederation)
				return new Exception("Signing the URL failed: service account '" + a.ServiceAccountEmail + "' could not sign through the IAM Credentials API. Grant the federated identity 'Service Account Token Creator' on the service account. Details: " + e.Message, e);
			return new Exception("Signing the URL failed for service account '" + a.ClientEmail + "'. Details: " + e.Message, e);
		}

		/// <summary>
		/// Parses a GCS RFC3339 timestamp defensively. The SDK's *DateTimeOffset properties use a
		/// strict format (exactly what production GCS emits); parsing the raw string with a flexible
		/// parser also tolerates emulators (e.g. fake-gcs-server emits microsecond precision with a
		/// local UTC offset) and any future format drift. Returns 1900-01-01 (the OutSystems null
		/// date) when missing or unparseable.
		/// </summary>
		private static DateTime ParseTimestamp(string raw)
		{
			DateTimeOffset dto;
			if (!string.IsNullOrEmpty(raw) && DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dto))
				return dto.UtcDateTime;
			return new DateTime(1900, 1, 1);
		}

		/// <summary>
		/// Converts a GCS_MetadataEntry record list to a dictionary. Entries with an empty Key are
		/// ignored; empty Values are preserved (Object_UpdateMetadata interprets them as key removal).
		/// Returns null when the list has no usable entries.
		/// </summary>
		private static Dictionary<string, string> ToMetadataDictionary(RLGCS_MetadataEntryRecordList list)
		{
			if (list == null || list.Length == 0) return null;
			var dict = new Dictionary<string, string>();
			foreach (var entry in list.ToArray(r => r))
			{
				string key = entry.ssSTGCS_MetadataEntry.ssKey;
				if (string.IsNullOrEmpty(key)) continue;
				dict[key] = entry.ssSTGCS_MetadataEntry.ssValue ?? "";
			}
			return dict.Count > 0 ? dict : null;
		}

		/// <summary>
		/// True when a 404 from GCS refers to the bucket itself rather than an object inside it
		/// (Google reports "The specified bucket does not exist." vs "No such object: ...").
		/// </summary>
		private static bool IsBucketNotFound(Google.GoogleApiException e)
		{
			string msg = (e.Error != null ? e.Error.Message : null) ?? e.Message ?? "";
			return msg.IndexOf("bucket", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Translates a GoogleApiException into an exception with an actionable message for
		/// OutSystems logs, instead of Google's raw API error. The original exception is kept
		/// as InnerException.
		/// </summary>
		private static Exception FriendlyException(Google.GoogleApiException e, AuthConfig a, string bucketName, string objectName)
		{
			string details = e.Error != null && !string.IsNullOrEmpty(e.Error.Message) ? e.Error.Message : e.Message;

			if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && bucketName != null)
			{
				if (objectName != null && !IsBucketNotFound(e))
					return new Exception("Object '" + objectName + "' was not found in bucket '" + bucketName + "'. Details: " + details, e);
				return new Exception("Bucket '" + bucketName + "' does not exist (names are case-sensitive and must match exactly). Details: " + details, e);
			}
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
				return new Exception("Access denied for service account '" + IdentityOf(a) + "'. Grant it the required IAM role in Google Cloud (Storage Object Admin for object operations, Storage Admin for bucket operations). Details: " + details, e);
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
				return new Exception("Google rejected the request as unauthenticated. " + UnauthenticatedHint(a) + " Details: " + details, e);
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
			{
				if (details != null && details.IndexOf("not empty", StringComparison.OrdinalIgnoreCase) >= 0)
					return new Exception("Bucket '" + bucketName + "' is not empty. Delete all objects in it before deleting the bucket. Details: " + details, e);
				return new Exception("Conflict: " + details + " (for Bucket_Create this usually means the name is already taken - bucket names are global across all of Google Cloud Storage).", e);
			}
			return new Exception("Google Cloud Storage error (" + (int)e.HttpStatusCode + " " + e.HttpStatusCode + "): " + details, e);
		}

		/// <summary>
		/// Lists all buckets in the specified project.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketList"></param>
		public void MssBucket_List(RCGCS_AuthenticationRecord ssAuthentication, out RLGCS_BucketRecordList ssBucketList)
		{
			var auth = FromRecord(ssAuthentication);
			RequireProjectId(auth, "Bucket_List");
			var storageClient = GetStorageClient(auth);
			ssBucketList = new RLGCS_BucketRecordList();

			try
			{
				var buckets = storageClient.ListBuckets(auth.ProjectId);

				foreach (var b in buckets)
				{
					var record = new RCGCS_BucketRecord(null)
					{
						ssSTGCS_Bucket =
						{
							ssName = b.Name,
							ssLocation = b.Location,
							ssStorageClass = b.StorageClass,
							ssCreated = ParseTimestamp(b.TimeCreatedRaw)
						}
					};

					ssBucketList.Append(record);
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, null, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssBucket_List

		/// <summary>
		/// Creates a new bucket in the specified project.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssLocation"></param>
		public void MssBucket_Create(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssLocation)
		{
			var auth = FromRecord(ssAuthentication);
			RequireProjectId(auth, "Bucket_Create");
			var storageClient = GetStorageClient(auth);

			try
			{
				storageClient.CreateBucket(
					auth.ProjectId,
					new Bucket
					{
						Name = ssBucketName,
						Location = ssLocation
					}
				);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssBucket_Create

		/// <summary>
		/// Deletes a bucket. The bucket must be empty.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		public void MssBucket_Delete(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			try
			{
				storageClient.DeleteBucket(ssBucketName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssBucket_Delete

		/// <summary>
		/// Deletes an object from a bucket.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		public void MssObject_Delete(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			try
			{
				storageClient.DeleteObject(ssBucketName, ssObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_Delete

		/// <summary>
		/// Checks whether an object exists in a bucket.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssExists"></param>
		public void MssObject_Exists(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName, out bool ssExists)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			try
			{
				storageClient.GetObject(ssBucketName, ssObjectName);
				ssExists = true;
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsBucketNotFound(e))
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_Exists

        /// <summary>
        /// Generates a signed URL for an object.
        /// </summary>
        /// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
        /// <param name="ssOperation"></param>
        /// <param name="ssBucketName"></param>
        /// <param name="ssObjectName"></param>
        /// <param name="ssExpirationMinutes"></param>
        /// <param name="ssContentType">Optional; for Upload URLs, the exact Content-Type the client must send. Becomes part of the signature.</param>
        /// <param name="ssURL"></param>
        public void MssObject_GetSignedUrl(RCGCS_AuthenticationRecord ssAuthentication, string ssOperation, string ssBucketName, string ssObjectName, int ssExpirationMinutes, string ssContentType, out string ssURL)
		{
			if (ssExpirationMinutes <= 0)
				throw new ArgumentException("ExpirationMinutes must be greater than zero (received " + ssExpirationMinutes + ").");
			if (ssExpirationMinutes > 10080)
				throw new ArgumentException("ExpirationMinutes cannot exceed 10080 minutes (7 days), the maximum validity of a Google Cloud V4 signed URL (received " + ssExpirationMinutes + ").");

			var auth = FromRecord(ssAuthentication);
			var urlSigner = GetUrlSigner(auth);

			HttpMethod method;
			switch ((ssOperation ?? "").Trim().ToUpperInvariant())
			{
				case "DOWNLOAD":
					method = HttpMethod.Get;
					break;
				case "UPLOAD":
					method = HttpMethod.Put;
					break;
				case "DELETE":
					method = HttpMethod.Delete;
					break;
				default:
					throw new ArgumentException("Invalid Operation '" + ssOperation + "'. Use 'Download', 'Upload', or 'Delete'.");
			}

			var template = UrlSigner.RequestTemplate
				.FromBucket(ssBucketName)
				.WithObjectName(ssObjectName)
				.WithHttpMethod(method);

			// When a ContentType is provided it becomes part of the signature, so Google
			// rejects requests whose Content-Type header does not match (relevant for Upload).
			if (!string.IsNullOrEmpty(ssContentType))
			{
				template = template.WithContentHeaders(new Dictionary<string, IEnumerable<string>>
				{
					{ "Content-Type", new[] { ssContentType } }
				});
			}

			try
			{
				ssURL = urlSigner.Sign(template, UrlSigner.Options.FromDuration(TimeSpan.FromMinutes(ssExpirationMinutes)));
			}
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
			catch (Google.GoogleApiException e) { throw FriendlySigningException(e, auth); }
		} // MssObject_GetSignedUrl

		/// <summary>
		/// Uploads an object to a bucket.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssContent"></param>
		/// <param name="ssContentType"></param>
		/// <param name="ssMetadata">Optional custom key-value metadata to store with the object. Retrievable via Object_GetMetadata.</param>
		public void MssObject_Upload(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName, byte[] ssContent, string ssContentType, RLGCS_MetadataEntryRecordList ssMetadata)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			try
			{
				var gcsObject = new Google.Apis.Storage.v1.Data.Object
				{
					Bucket = ssBucketName,
					Name = ssObjectName,
					ContentType = ssContentType,
					Metadata = ToMetadataDictionary(ssMetadata)
				};

				using (var stream = new MemoryStream(ssContent))
				{
					storageClient.UploadObject(gcsObject, stream);
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_Upload

		/// <summary>
		/// Downloads an object from a bucket.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssContent"></param>
		/// <param name="ssContentType"></param>
		public void MssObject_Download(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssObjectName, out byte[] ssContent, out string ssContentType)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			try
			{
				using (var stream = new MemoryStream())
				{
					var obj = storageClient.DownloadObject(ssBucketName, ssObjectName, stream);
					ssContent = stream.ToArray();
					ssContentType = obj.ContentType;
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_Download

		/// <summary>
		/// Lists objects in a bucket with an optional prefix filter, with support for
		/// pagination (MaxResults/PageToken) and folder-style navigation (Delimiter).
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssPrefix"></param>
		/// <param name="ssMaxResults">Maximum number of results for this call; 0 returns everything.</param>
		/// <param name="ssPageToken">Continuation token from a previous call's NextPageToken.</param>
		/// <param name="ssDelimiter">Typically "/"; groups nested objects into PrefixList.</param>
		/// <param name="ssObjectList"></param>
		/// <param name="ssNextPageToken">Non-empty when more results exist (only in paged mode).</param>
		/// <param name="ssPrefixList">The "folders" directly under Prefix when Delimiter is set.</param>
		public void MssObject_List(RCGCS_AuthenticationRecord ssAuthentication, string ssBucketName, string ssPrefix, int ssMaxResults, string ssPageToken, string ssDelimiter, out RLGCS_ObjectRecordList ssObjectList, out string ssNextPageToken, out RLGCS_PrefixRecordList ssPrefixList)
		{
			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);
			ssObjectList = new RLGCS_ObjectRecordList();
			ssPrefixList = new RLGCS_PrefixRecordList();
			ssNextPageToken = "";

			if (ssMaxResults < 0)
				throw new ArgumentException("MaxResults cannot be negative (received " + ssMaxResults + "). Use 0 to return all objects.");

			try
			{
				var options = new ListObjectsOptions();
				if (ssMaxResults > 0) options.PageSize = ssMaxResults;
				if (!string.IsNullOrEmpty(ssPageToken)) options.PageToken = ssPageToken;
				if (!string.IsNullOrEmpty(ssDelimiter)) options.Delimiter = ssDelimiter;

				// Iterate the raw API responses (one per HTTP request) so the continuation
				// token and the common prefixes ("folders") are available, not just the items.
				var seenPrefixes = new HashSet<string>();
				foreach (var page in storageClient.ListObjects(ssBucketName, ssPrefix, options).AsRawResponses())
				{
					if (page.Items != null)
					{
						foreach (var obj in page.Items)
						{
							var record = new RCGCS_ObjectRecord(null);
							record.ssSTGCS_Object.ssName = obj.Name;
							record.ssSTGCS_Object.ssSize = (long)Math.Min(obj.Size ?? 0, long.MaxValue);
							record.ssSTGCS_Object.ssContentType = obj.ContentType;
							record.ssSTGCS_Object.ssUpdated = ParseTimestamp(obj.UpdatedRaw);
							ssObjectList.Append(record);
						}
					}

					if (page.Prefixes != null)
					{
						foreach (var prefix in page.Prefixes)
						{
							if (!seenPrefixes.Add(prefix)) continue;
							var record = new RCGCS_PrefixRecord(null);
							record.ssSTGCS_Prefix.ssPrefix = prefix;
							ssPrefixList.Append(record);
						}
					}

					if (ssMaxResults > 0)
					{
						// Paged mode: return exactly one page and hand back the continuation token.
						ssNextPageToken = page.NextPageToken ?? "";
						break;
					}
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, auth, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, auth); }
		} // MssObject_List

	} // CssGoogleCloudStorage_ext

} // OutSystems.NssGoogleCloudStorage_ext

