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

				try
				{
					// Setting any precondition turns off the SDK's automatic ones, so both are explicit: the
					// generation guards against a content replacement (which resets metageneration to 1).
					storageClient.UpdateObject(obj, new UpdateObjectOptions { IfGenerationMatch = obj.Generation, IfMetagenerationMatch = obj.Metageneration });
				}
				catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed)
				{
					throw new Exception("The metadata of '" + ssBucketName + "/" + ssObjectName + "' was changed by another request during this update, so nothing was changed. Retry the update.", e);
				}
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }

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
				catch (Exception e) when (IsTranslatable(e))
				{
					// TranslateException gives the same message a whole-object call would give, so the
					// partial-progress error carries an actionable cause instead of Google's raw text.
					throw new Exception("Deleted " + deleted + " of " + names.Count + " objects under prefix '" + ssPrefix + "', then failed on '" + name + "': " + TranslateException(e, auth, ssBucketName, name).Message, e);
				}
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }
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
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				throw CopyNotFoundException(storageClient, e, auth, ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName);
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssSourceBucketName, ssSourceObjectName); }
		} // MssObject_Copy

		/// <summary>
		/// Moves an object to another location (copy + delete of the source), within the same bucket or across buckets. Use the same source and destination bucket with a different destination name to rename an object. Source and destination must be different objects. If the destination exists, it will be overwritten. A move is not atomic (copy, then delete), so a failed delete leaves both objects in place; it is guarded against concurrent changes of the source, so a source that is replaced during the move is not deleted.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssSourceBucketName">The bucket that currently contains the object.</param>
		/// <param name="ssSourceObjectName">The full path/name of the source object (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssDestinationBucketName">The bucket to move the object into (can be the same as the source).</param>
		/// <param name="ssDestinationObjectName">The full path/name for the destination object. Must differ from the source object name when both buckets are the same.</param>
		public void MssObject_Move(RCGCS_AuthenticationRecord ssAuthentication, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName) {
			// Copying an object onto itself and then deleting the source would destroy the only copy.
			if (string.Equals(ssSourceBucketName, ssDestinationBucketName, StringComparison.Ordinal)
				&& string.Equals(ssSourceObjectName, ssDestinationObjectName, StringComparison.Ordinal))
				throw new ArgumentException("Source and destination are the same object ('" + ssSourceBucketName + "/" + ssSourceObjectName + "'); Object_Move would delete it. Use a different destination name.");

			var auth = FromRecord(ssAuthentication);
			var storageClient = GetStorageClient(auth);

			// Read the source before touching it: its generation guards both the copy and the delete,
			// so a source replaced by someone else mid-move is reported instead of being lost.
			// Nullable on purpose: a generation of 0 would mean "only if the object does not exist".
			long? generation = null;
			try
			{
				generation = storageClient.GetObject(ssSourceBucketName, ssSourceObjectName).Generation;
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssSourceBucketName, ssSourceObjectName); }

			try
			{
				storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName,
					new CopyObjectOptions { IfSourceGenerationMatch = generation });
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				throw new Exception("The source object '" + ssSourceBucketName + "/" + ssSourceObjectName + "' changed while it was being moved; nothing was moved. Retry the move.", e);
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				throw CopyNotFoundException(storageClient, e, auth, ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName);
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssSourceBucketName, ssSourceObjectName); }

			// Move is copy-then-delete and is not atomic: if the delete fails, both objects exist.
			// Surface that state explicitly instead of a generic error. The delete is guarded by the
			// generation read before the copy, so an overwritten source (newer data) is left alone.
			try
			{
				storageClient.DeleteObject(ssSourceBucketName, ssSourceObjectName,
					new DeleteObjectOptions { IfGenerationMatch = generation });
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				// Already gone (deleted concurrently) - the move's end state is reached.
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				throw new Exception("The object was copied to '" + ssDestinationBucketName + "/" + ssDestinationObjectName + "' (generation " + generation + "), but the source '" + ssSourceBucketName + "/" + ssSourceObjectName + "' was modified during the move and was not deleted - both objects exist and the source holds newer data.", e);
			}
			catch (Exception e) when (IsTranslatable(e))
			{
				throw new Exception("The object was copied to '" + ssDestinationBucketName + "/" + ssDestinationObjectName + "' but the source '" + ssSourceBucketName + "/" + ssSourceObjectName + "' could not be deleted - both objects currently exist. Cause: " + TranslateException(e, auth, ssSourceBucketName, ssSourceObjectName).Message, e);
			}
		} // MssObject_Move

		/// <summary>
		/// A 404 from a copy (an object rewrite) means either the source object or the destination bucket
		/// is missing, and the message wording differs between Google and emulators. Asks again whether
		/// the source exists and names whichever side is missing.
		/// </summary>
		private static Exception CopyNotFoundException(StorageClient storageClient, Google.GoogleApiException e, AuthConfig auth, string sourceBucketName, string sourceObjectName, string destinationBucketName)
		{
			bool sourceExists = true;
			try { storageClient.GetObject(sourceBucketName, sourceObjectName); }
			catch (Exception probe) when (IsTranslatable(probe)) { sourceExists = false; }
			return sourceExists
				? FriendlyException(e, auth, destinationBucketName, null)
				: FriendlyException(e, auth, sourceBucketName, sourceObjectName);
		}

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

				ssMetadata.ssSTGCS_ObjectMetadata.ssName = obj.Name ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssBucket = obj.Bucket ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssSize = (long)Math.Min(obj.Size ?? 0, long.MaxValue);
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentType = obj.ContentType ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentEncoding = obj.ContentEncoding ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssContentDisposition = obj.ContentDisposition ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssCacheControl = obj.CacheControl ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssMD5Hash = obj.Md5Hash ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssCrc32c = obj.Crc32c ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssETag = obj.ETag ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssGeneration = obj.Generation ?? 0;
				ssMetadata.ssSTGCS_ObjectMetadata.ssMetageneration = obj.Metageneration ?? 0;
				ssMetadata.ssSTGCS_ObjectMetadata.ssStorageClass = obj.StorageClass ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssMediaLink = obj.MediaLink ?? "";
				ssMetadata.ssSTGCS_ObjectMetadata.ssTimeCreated = ParseTimestamp(obj.TimeCreatedRaw);
				ssMetadata.ssSTGCS_ObjectMetadata.ssUpdated = ParseTimestamp(obj.UpdatedRaw);

				if (obj.Metadata != null)
				{
					foreach (var kv in obj.Metadata)
					{
						var entry = new RCGCS_MetadataEntryRecord(null);
						entry.ssSTGCS_MetadataEntry.ssKey = kv.Key;
						entry.ssSTGCS_MetadataEntry.ssValue = kv.Value ?? "";
						ssCustomMetadata.Append(entry);
					}
				}
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsBucketNotFound(e))
			{
				ssExists = false;
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
		} // MssObject_GetMetadata
		// ---- Authentication --------------------------------------------------------------
		// WorkloadIdentityFederation: IdP JWT (client credentials or SubjectToken) -> Google STS ->
		// service account impersonation; URLs signed through IAM signBlob.
		// ServiceAccountKey (default when AuthenticationMethod is empty): ClientEmail + PrivateKey;
		// URLs signed locally.

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
		/// Per-credential caches (the clients are thread-safe). Creating a client per call re-parses the
		/// key or re-runs the token exchange and opens a new HttpClient, which exhausts sockets under load.
		/// Keys are SHA-256 hashes with a per-method prefix, so no secret is ever a dictionary key, but the
		/// cached credentials necessarily keep key material in memory. Entries expire after 60 minutes
		/// without use (a supplied token's entries at the token's exp instead), and each cache keeps at most
		/// ~500 entries, dropping the least recently used, so a server serving many identities stays bounded.
		/// </summary>
		private static readonly ExpiringCache<StorageClient> StorageClientCache = new ExpiringCache<StorageClient>();
		private static readonly ExpiringCache<UrlSigner> UrlSignerCache = new ExpiringCache<UrlSigner>();
		private static readonly ExpiringCache<FederatedIdentity> FederatedIdentityCache = new ExpiringCache<FederatedIdentity>();
		private static readonly ExpiringCache<ServiceAccountCredential> ServiceAccountCredentialCache = new ExpiringCache<ServiceAccountCredential>();

		// Production identity-provider client: no redirect following (a 3xx would leak the client
		// secret to the redirect target and silently swap the token endpoint), 30 s timeout.
		private static readonly Lazy<HttpClient> IdentityProviderHttp = new Lazy<HttpClient>(() =>
			new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) });

		/// <summary>
		/// A bounded cache with a sliding lifetime: an entry is dropped after 60 minutes without a hit, or
		/// at its absolute expiry when it has one (a supplied token's exp), whichever comes first. Values
		/// are never disposed when they leave the cache: another request may still be using them.
		/// </summary>
		private sealed class ExpiringCache<T> where T : class
		{
			private const int MaxEntries = 500;
			private static readonly TimeSpan SlidingLifetime = TimeSpan.FromMinutes(60);
			private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

			/// <summary>One cached value, with its absolute expiry (DateTime.MaxValue when none) and last use.</summary>
			private sealed class Entry
			{
				public Entry(T value, DateTime absoluteExpiryUtc)
				{
					Value = value;
					AbsoluteExpiryUtc = absoluteExpiryUtc;
					LastUsedUtc = DateTime.UtcNow;
				}

				public T Value { get; private set; }
				public DateTime AbsoluteExpiryUtc { get; private set; }
				public DateTime LastUsedUtc { get; set; }

				public bool IsExpired(DateTime utcNow)
				{
					return utcNow >= AbsoluteExpiryUtc || utcNow - LastUsedUtc >= SlidingLifetime;
				}
			}

			private readonly ConcurrentDictionary<string, Entry> _entries = new ConcurrentDictionary<string, Entry>();
			private readonly object _missLock = new object();
			private readonly object _sweepLock = new object();
			private DateTime _nextSweepUtc;

			/// <summary>Number of entries held, including ones not swept yet (test seam).</summary>
			public int Count { get { return _entries.Count; } }

			/// <summary>
			/// Returns the live value for the key, or creates one with the factory and the given absolute
			/// expiry (DateTime.MaxValue for none). A factory that throws leaves no entry behind.
			/// </summary>
			public T GetOrAdd(string key, Func<string, T> factory, DateTime absoluteExpiryUtc)
			{
				SweepIfDue();

				Entry entry = FindLive(key, DateTime.UtcNow);
				if (entry != null) return entry.Value;

				// Misses are rare (once per credential per lifetime), so one lock is enough to stop two
				// callers from building the same value or installing a stale entry over a live one.
				lock (_missLock)
				{
					entry = FindLive(key, DateTime.UtcNow);
					if (entry != null) return entry.Value;

					T value = factory(key); // may throw: nothing has been cached yet
					_entries[key] = new Entry(value, absoluteExpiryUtc);
					return value;
				}
			}

			/// <summary>Returns the live entry for the key and marks it used; drops the entry when it has expired.</summary>
			private Entry FindLive(string key, DateTime utcNow)
			{
				Entry entry;
				if (!_entries.TryGetValue(key, out entry)) return null;

				if (entry.IsExpired(utcNow))
				{
					Remove(key, entry);
					return null;
				}

				entry.LastUsedUtc = utcNow;
				return entry;
			}

			/// <summary>At most once a minute, from any access: drop the expired entries, then the least recently used surplus.</summary>
			private void SweepIfDue()
			{
				DateTime now = DateTime.UtcNow;
				lock (_sweepLock)
				{
					if (now < _nextSweepUtc) return;
					_nextSweepUtc = now + SweepInterval;
				}
				Sweep(now);
			}

			private void Sweep(DateTime utcNow)
			{
				var live = new List<KeyValuePair<string, Entry>>();
				foreach (var pair in _entries)
				{
					if (!pair.Value.IsExpired(utcNow))
					{
						live.Add(pair);
						continue;
					}

					// Values are not disposed: another request may still be using an evicted client.
					Remove(pair.Key, pair.Value);
				}

				if (live.Count <= MaxEntries) return;
				foreach (var pair in live.OrderBy(p => p.Value.LastUsedUtc).Take(live.Count - MaxEntries))
					Remove(pair.Key, pair.Value);
			}

			/// <summary>
			/// Removes the key only while it still maps to this entry (an atomic compare-and-remove), so a
			/// fresh entry another thread has just installed under the same key is never dropped.
			/// </summary>
			private void Remove(string key, Entry entry)
			{
				((ICollection<KeyValuePair<string, Entry>>)_entries).Remove(new KeyValuePair<string, Entry>(key, entry));
			}
		}

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

		/// <summary>
		/// Requires an absolute https:// token endpoint. Plain http is accepted only for loopback
		/// hosts and only while the test override is set (never in production). URLs carrying user
		/// information (https://user:password@host) are always rejected, and the error never echoes
		/// that part. Intranet hosts are allowed: on-premises Keycloak/ADFS is supported.
		/// </summary>
		private static void ValidateTokenEndpoint(string tokenEndpoint)
		{
			string trimmed = (tokenEndpoint ?? string.Empty).Trim();
			Uri uri;
			bool absolute = Uri.TryCreate(trimmed, UriKind.Absolute, out uri);
			// Echo the endpoint without user info or query, so credentials never appear in the error.
			string display = absolute ? uri.GetLeftPart(UriPartial.Path) : trimmed;

			if (absolute && !string.IsNullOrEmpty(uri.UserInfo))
				throw new ArgumentException("TokenEndpoint must not contain user information (a 'user:password@' part). Remove the credentials from the URL and let ClientId/ClientSecret authenticate the request.");

			bool ok = absolute
				&& (uri.Scheme == Uri.UriSchemeHttps
					|| (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback && HttpClientFactoryOverride != null));
			if (!ok)
				throw new ArgumentException("TokenEndpoint '" + display + "' must be an absolute https:// URL (for example 'https://login.microsoftonline.com/TENANT_ID/oauth2/v2.0/token').");
		}

		/// <summary>
		/// A cache key that never exposes the secret. Every part is length-prefixed before hashing, so a
		/// part that contains the separator (a PEM private key contains newlines) cannot shift the
		/// boundary and make two different credentials produce the same key.
		/// </summary>
		private static string CacheKey(string prefix, params string[] parts)
		{
			using (var sha = SHA256.Create())
			{
				string canonical = string.Join("\n", parts.Select(p =>
				{
					string part = p ?? string.Empty;
					return part.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + part;
				}));
				var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
				return prefix + "|" + Convert.ToBase64String(hash);
			}
		}

		/// <summary>
		/// The cache key for a federated identity. With a supplied token the token itself is hashed into
		/// the key (and TokenEndpoint, ClientId, ClientSecret, Scope and Audience are left out, since they
		/// are not used): a caller that supplies a different token must never get another caller's cached
		/// exchange or impersonated token. Client-credentials entries are keyed by the whole configuration.
		/// </summary>
		private static string FederatedCacheKey(AuthConfig a)
		{
			string supplied = SuppliedToken(a);
			if (supplied != null)
				return CacheKey("wif", NormalizeWorkloadIdentityProvider(a.WorkloadIdentityProvider), a.ServiceAccountEmail.Trim(), "supplied-token", supplied);

			return CacheKey("wif",
				NormalizeWorkloadIdentityProvider(a.WorkloadIdentityProvider),
				a.ServiceAccountEmail.Trim(),
				"client-credentials",
				a.TokenEndpoint == null ? null : a.TokenEndpoint.Trim(), a.ClientId, a.ClientSecret, a.Scope, a.Audience);
		}

		// ---- Client factories --------------------------------------------------------------

		/// <summary>
		/// Returns the cached StorageClient for the credentials. When GCSCONNECTOR_EMULATOR_HOST is set
		/// (tests only), connects unauthenticated to that emulator instead. The name is deliberately not
		/// Google's STORAGE_EMULATOR_HOST, so a variable set for other tools can't redirect production.
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
					_ => new StorageClientBuilder { BaseUri = baseUri, UnauthenticatedAccess = true }.Build(),
					DateTime.MaxValue);
			}

			if (method == AuthMethod.WorkloadIdentityFederation)
			{
				var identity = GetFederatedIdentity(a);
				return StorageClientCache.GetOrAdd(FederatedCacheKey(a), _ =>
				{
					var builder = new StorageClientBuilder { GoogleCredential = identity.Credential };
					if (HttpClientFactoryOverride != null) builder.HttpClientFactory = HttpClientFactoryOverride;
					return builder.Build();
				}, identity.ExpiresUtc);
			}

			return StorageClientCache.GetOrAdd(
				CacheKey("key", a.ClientEmail, a.PrivateKey),
				_ => StorageClient.Create(GetServiceAccountCredential(a).ToGoogleCredential()),
				DateTime.MaxValue);
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
				return UrlSignerCache.GetOrAdd(FederatedCacheKey(a), _ => UrlSigner.FromCredential(identity.Credential), identity.ExpiresUtc);
			}

			return UrlSignerCache.GetOrAdd(
				CacheKey("key", a.ClientEmail, a.PrivateKey),
				_ => UrlSigner.FromCredential(GetServiceAccountCredential(a)),
				DateTime.MaxValue);
		}

		/// <summary>
		/// The one ServiceAccountCredential for these key credentials, parsed once and shared by the
		/// StorageClient and the UrlSigner: each only reads the key, so they cannot interfere, and the
		/// PEM is not parsed again for every action.
		/// </summary>
		private static ServiceAccountCredential GetServiceAccountCredential(AuthConfig a)
		{
			return ServiceAccountCredentialCache.GetOrAdd(
				CacheKey("key", a.ClientEmail, a.PrivateKey),
				_ => CreateServiceAccountCredential(a),
				DateTime.MaxValue);
		}

		private static ServiceAccountCredential CreateServiceAccountCredential(AuthConfig a)
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

		/// <summary>The caller-supplied JWT, trimmed, or null when the client-credentials grant is used.</summary>
		private static string SuppliedToken(AuthConfig a)
		{
			string token = (a.SubjectToken ?? string.Empty).Trim();
			return token.Length == 0 ? null : token;
		}

		/// <summary>
		/// The absolute expiry of the cached federated entries: the supplied token's exp, so nothing is
		/// reused past the identity it was built from, or five minutes from now when the token has no
		/// readable exp. Client-credentials entries expire on the sliding window only.
		/// </summary>
		private static DateTime FederatedExpiry(AuthConfig a)
		{
			string supplied = SuppliedToken(a);
			if (supplied == null) return DateTime.MaxValue;

			DateTime expiry;
			return TryReadJwtExpiry(supplied, out expiry) ? expiry : DateTime.UtcNow.AddMinutes(5);
		}

		/// <summary>
		/// Rejects a supplied token whose exp has already passed (allowing 30 seconds of clock skew),
		/// before it reaches Google STS. A token without a readable exp is left for Google to judge.
		/// </summary>
		private static void ValidateSubjectTokenExpiry(string token)
		{
			DateTime expiry;
			if (!TryReadJwtExpiry(token, out expiry)) return;
			if (expiry < DateTime.UtcNow.AddSeconds(-30))
				throw new ArgumentException("The SubjectToken has expired (exp " + expiry.ToString("u") + "). Supply a fresh token.");
		}

		/// <summary>
		/// Reads the exp claim of a JWT payload (Unix seconds, integer or float) without validating the
		/// signature. Returns false, never throws, when the token is not a readable three-part JWT with a
		/// JSON payload and a numeric exp.
		/// </summary>
		internal static bool TryReadJwtExpiry(string jwt, out DateTime expiresUtc)
		{
			expiresUtc = default(DateTime);
			if (string.IsNullOrEmpty(jwt)) return false;

			// header.payload.signature: only the payload is needed, and the signature is not checked.
			var parts = jwt.Split('.');
			if (parts.Length != 3) return false;

			try
			{
				// base64url: '-' and '_' instead of '+' and '/', and the padding removed.
				string payload = parts[1].Replace('-', '+').Replace('_', '/');
				int padding = payload.Length % 4;
				if (padding == 1) return false;
				if (padding == 2) payload += "==";
				else if (padding == 3) payload += "=";

				var root = ParseJsonObject(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
				object exp;
				if (root == null || !root.TryGetValue("exp", out exp) || !(exp is long || exp is double)) return false;

				double seconds = Convert.ToDouble(exp, System.Globalization.CultureInfo.InvariantCulture);
				// Outside this range AddSeconds would overflow or produce a pre-1970 date.
				if (double.IsNaN(seconds) || seconds < 0 || seconds > 253402300799d) return false;

				expiresUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
				return true;
			}
			catch (Exception)
			{
				return false; // not base64, not UTF-8, not JSON, ...
			}
		}

		/// <summary>
		/// Parses a JSON object with Google's own serializer (Newtonsoft.Json underneath, the copy the
		/// Google libraries already load), so this assembly needs no compile-time reference to
		/// Newtonsoft.Json: Integration Studio replaces that DLL with its own build when it generates the
		/// project. Values are string, long, double or bool (nested objects and arrays are neither).
		/// Returns null, never throws, when the text is not a JSON object.
		/// </summary>
		internal static IDictionary<string, object> ParseJsonObject(string json)
		{
			if (string.IsNullOrWhiteSpace(json)) return null;
			try
			{
				Google.Apis.Json.IJsonSerializer serializer = Google.Apis.Json.NewtonsoftJsonSerializer.Instance;
				return serializer.Deserialize<Dictionary<string, object>>(json);
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>The field's value when it is a JSON string; null when it is absent or of another type.</summary>
		internal static string JsonString(IDictionary<string, object> root, string field)
		{
			object value;
			return root != null && root.TryGetValue(field, out value) ? value as string : null;
		}

		/// <summary>
		/// A federated credential plus the source of its identity-provider token. The credential chain is
		/// external account (subject token -> Google STS) -> impersonated service account (IAM Credentials).
		/// The SDK refreshes both automatically, calling back into the token source only when needed.
		/// </summary>
		private sealed class FederatedIdentity
		{
			public FederatedIdentity(GoogleCredential credential, SubjectTokenSource tokenSource, DateTime expiresUtc)
			{
				Credential = credential;
				TokenSource = tokenSource;
				ExpiresUtc = expiresUtc;
			}

			public GoogleCredential Credential { get; private set; }
			public SubjectTokenSource TokenSource { get; private set; }

			/// <summary>Absolute expiry shared with the cached StorageClient and UrlSigner (DateTime.MaxValue when none).</summary>
			public DateTime ExpiresUtc { get; private set; }
		}

		private static FederatedIdentity GetFederatedIdentity(AuthConfig a)
		{
			// The expiry check runs before the cache lookup and before any network call: an expired
			// token must never be exchanged, and a cached identity built from it must not be reused.
			string supplied = SuppliedToken(a);
			if (supplied != null) ValidateSubjectTokenExpiry(supplied);

			return FederatedIdentityCache.GetOrAdd(FederatedCacheKey(a), _ => CreateFederatedIdentity(a), FederatedExpiry(a));
		}

		private static FederatedIdentity CreateFederatedIdentity(AuthConfig a)
		{
			var tokenSource = new SubjectTokenSource(SuppliedToken(a), a.TokenEndpoint == null ? null : a.TokenEndpoint.Trim(), a.ClientId, a.ClientSecret, a.Scope, a.Audience);

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

			return new FederatedIdentity(federated.Impersonate(impersonationInitializer), tokenSource, FederatedExpiry(a));
		}

		/// <summary>
		/// Supplies the JWT for the STS exchange: the caller-supplied SubjectToken, fixed when this source
		/// is created and never replaced (so one caller's token is never exchanged under another caller's
		/// identity), or one from the OAuth 2.0 client-credentials grant (RFC 6749 4.4), cached until
		/// shortly before it expires.
		/// </summary>
		private sealed class SubjectTokenSource : ProgrammaticExternalAccountCredential.ISubjectTokenProvider
		{
			private readonly string _suppliedToken, _tokenEndpoint, _clientId, _clientSecret, _scope, _audience;
			private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
			private string _cachedToken;
			private DateTimeOffset _cachedUntil;
			private bool _useBasicClientAuth;

			public SubjectTokenSource(string suppliedToken, string tokenEndpoint, string clientId, string clientSecret, string scope, string audience)
			{
				_suppliedToken = suppliedToken;
				_tokenEndpoint = tokenEndpoint;
				_clientId = clientId;
				_clientSecret = clientSecret;
				_scope = scope;
				_audience = audience;
			}

			public async Task<string> GetSubjectTokenAsync(ProgrammaticExternalAccountCredential caller, CancellationToken taskCancellationToken)
			{
				if (!string.IsNullOrEmpty(_suppliedToken))
					return _suppliedToken;

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
				HttpClient http;
				if (HttpClientFactoryOverride != null)
				{
					// Test seam: the factory returns a Google client whose message handler follows
					// redirects by default; mirror the production client, which never follows them.
					var client = (ConfigurableHttpClient)HttpClientFactoryOverride.CreateHttpClient(new CreateHttpClientArgs());
					client.MessageHandler.FollowRedirect = false;
					http = client;
				}
				else
					http = IdentityProviderHttp.Value;

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
							// A cancelled caller token is not a timeout: propagate it unchanged.
							if (ct.IsCancellationRequested)
								throw;
							if (e is TaskCanceledException)
								throw new IdentityProviderException("The identity provider token endpoint '" + _tokenEndpoint + "' did not respond within 30 seconds.", e);
							throw new IdentityProviderException("Could not reach the identity provider token endpoint '" + _tokenEndpoint + "': " + e.Message, e);
						}

						using (response)
						{
							string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

							// The production client follows no redirects (and the test one does not need to):
							// a 3xx means TokenEndpoint points at a redirecting URL.
							int statusCode = (int)response.StatusCode;
							if (statusCode >= 300 && statusCode < 400)
								throw new IdentityProviderException("The identity provider token endpoint redirected (" + statusCode + "). Configure the final token URL in TokenEndpoint.");

							if (response.IsSuccessStatusCode)
							{
								int expiresIn;
								string token = ParseTokenResponse(body, out expiresIn);
								if (!LooksLikeJwt(token))
									throw new IdentityProviderException("The identity provider returned a token that is not a JWT, but Workload Identity Federation requires a signed JWT. For Entra ID, request a token for your own app registration (Scope 'api://YOUR_APP_ID/.default'), not Microsoft Graph; for Okta, use a custom authorization server.");
								if (basic) _useBasicClientAuth = true;

								// expires_in can outlive the JWT itself, and STS rejects a token that is about
								// to expire: use the earlier of the two, with a 30-second floor for the retry.
								TimeSpan lifetime = TimeSpan.FromSeconds(expiresIn);
								DateTime tokenExpiry;
								if (TryReadJwtExpiry(token, out tokenExpiry))
								{
									TimeSpan untilExpiry = tokenExpiry - DateTime.UtcNow;
									if (untilExpiry < lifetime) lifetime = untilExpiry;
									if (lifetime < TimeSpan.FromSeconds(30)) lifetime = TimeSpan.FromSeconds(30);
								}
								return new IdpToken { Token = token, Lifetime = lifetime };
							}

							// Detail is the OAuth error fields when the body is JSON; otherwise only
							// metadata. The raw body is never echoed (it may contain secrets or HTML).
							string error, description;
							string detail;
							if (ParseOAuthError(body, out error, out description))
							{
								if (description != null && description.Length > 300) description = description.Substring(0, 300) + "...";
								detail = error == null && description == null
									? "(the response body contained no 'error' or 'error_description')"
									: error == null ? description : description == null ? error : error + ": " + description;
							}
							else
							{
								detail = "(non-JSON response, " + Encoding.UTF8.GetByteCount(body) + " bytes, Content-Type "
									+ (response.Content.Headers.ContentType != null ? response.Content.Headers.ContentType.ToString() : "none") + ")";
							}

							bool invalidClient = response.StatusCode == HttpStatusCode.Unauthorized || error == "invalid_client";
							if (!basic && invalidClient && attempt == 0)
								continue;

							throw new IdentityProviderException("The identity provider rejected the client-credentials token request (" + (int)response.StatusCode + " " + (error ?? response.StatusCode.ToString()) + "): " + detail + ". Check TokenEndpoint, ClientId, ClientSecret, and Scope/Audience.");
						}
					}
				}
			}

			private static string ParseTokenResponse(string body, out int expiresIn)
			{
				var root = ParseJsonObject(body);
				if (root == null)
					throw new IdentityProviderException("The identity provider's token response was not valid JSON.");

				// Read the tokens as strings only: an access_token that is a JSON object, array or
				// number is treated as missing, giving the actionable error instead of a cast failure.
				string token = JsonString(root, "access_token");
				if (string.IsNullOrEmpty(token)) token = JsonString(root, "id_token");
				if (string.IsNullOrEmpty(token))
					throw new IdentityProviderException("The identity provider's token response did not contain an 'access_token'.");

				expiresIn = 3600;
				object ei;
				int n;
				// A number, or a numeric string as some providers send it.
				if (root.TryGetValue("expires_in", out ei) && ei != null
					&& int.TryParse(Convert.ToString(ei, System.Globalization.CultureInfo.InvariantCulture), out n))
					expiresIn = n;
				expiresIn = Math.Max(expiresIn, 30);
				return token;
			}

			/// <summary>Reads the OAuth error fields; returns false when the body is not JSON.</summary>
			private static bool ParseOAuthError(string body, out string error, out string description)
			{
				error = null;
				description = null;
				var root = ParseJsonObject(body);
				if (root == null) return false; // not JSON: the caller reports a non-JSON response
				error = JsonString(root, "error");
				description = JsonString(root, "error_description");
				return true;
			}

			private static bool LooksLikeJwt(string token)
			{
				var parts = token.Split('.');
				return parts.Length == 3 && parts.All(p => p.Length > 0);
			}
		}

		/// <summary>
		/// A failure obtaining a token from the identity provider. TranslateException rethrows it
		/// with the same message (this exception kept as the inner one), so the actionable detail
		/// reaches the caller unchanged.
		/// </summary>
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

		/// <summary>
		/// Translates a failure while signing a URL (federation: the signBlob call) into an actionable
		/// message. The Token Creator hint is only right for the 403 that means the role is missing;
		/// other statuses get the status code and Google's error message instead.
		/// </summary>
		private static Exception FriendlySigningException(Exception e, AuthConfig a)
		{
			if (ResolveAuthMethodSafe(a) == AuthMethod.WorkloadIdentityFederation)
			{
				var api = e as Google.GoogleApiException;
				if (api == null)
					return new Exception("Signing the URL failed for service account '" + a.ServiceAccountEmail + "': " + InnermostMessage(e), e);
				if (api.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
					return new Exception("Signing the URL failed: service account '" + a.ServiceAccountEmail + "' could not sign through the IAM Credentials API. Grant the federated identity 'Service Account Token Creator' on the service account. Details: " + (api.Error != null && !string.IsNullOrEmpty(api.Error.Message) ? api.Error.Message : api.Message), e);
				string details = api.Error != null && !string.IsNullOrEmpty(api.Error.Message) ? api.Error.Message : api.Message;
				return new Exception("Signing the URL failed for service account '" + a.ServiceAccountEmail + "' (" + (int)api.HttpStatusCode + "): " + details, e);
			}
			return new Exception("Signing the URL failed for service account '" + a.ClientEmail + "'. Details: " + e.Message, e);
		}

		/// <summary>
		/// Parses an RFC 3339 timestamp. The SDK's *DateTimeOffset properties only accept GCS's exact
		/// format and fail on others (e.g. fake-gcs-server's microseconds with a local offset).
		/// Returns 1900-01-01, the OutSystems null date, when missing or unparseable.
		/// </summary>
		private static DateTime ParseTimestamp(string raw)
		{
			DateTimeOffset dto;
			if (!string.IsNullOrEmpty(raw) && DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out dto))
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
		/// The object message names the bucket, so it is checked first.
		/// </summary>
		private static bool IsBucketNotFound(Google.GoogleApiException e)
		{
			string msg = (e.Error != null ? e.Error.Message : null) ?? e.Message ?? "";
			if (msg.IndexOf("No such object", StringComparison.OrdinalIgnoreCase) >= 0)
				return false;
			return msg.IndexOf("bucket", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Since Google.Cloud.Storage.V1 5.0, uploads send the client-computed CRC32C in an x-goog-hash
		/// header and the server rejects a mismatch with 400, e.g. 'Provided CRC32C "..." doesn't match
		/// calculated CRC32C "...".'
		/// </summary>
		private static bool IsChecksumMismatch(string details)
		{
			if (details == null) return false;
			return (details.IndexOf("CRC32C", StringComparison.OrdinalIgnoreCase) >= 0 || details.IndexOf("MD5", StringComparison.OrdinalIgnoreCase) >= 0)
				&& details.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// True for every exception type the actions translate into an actionable message: a Google API
		/// error, a token-endpoint failure, a subject-token failure, an identity provider failure, or
		/// the network/timeout failures of a request to Google.
		/// </summary>
		private static bool IsTranslatable(Exception e)
		{
			return e is Google.GoogleApiException
				|| e is TokenResponseException
				|| e is Google.Apis.Auth.OAuth2.SubjectTokenException
				|| e is IdentityProviderException
				|| e is HttpRequestException
				|| e is TaskCanceledException;
		}

		/// <summary>
		/// Single exception translator for all actions: keeps the per-status Google storage messages,
		/// unwraps the SDK's SubjectTokenException (which would otherwise hide the identity provider's
		/// actionable message), and adds clear messages for network and timeout failures. The original
		/// exception is kept as the InnerException.
		/// </summary>
		private static Exception TranslateException(Exception e, AuthConfig a, string bucketName, string objectName)
		{
			var api = e as Google.GoogleApiException;
			if (api != null) return FriendlyException(api, a, bucketName, objectName);

			var token = e as TokenResponseException;
			if (token != null) return FriendlyAuthException(token, a);

			var subject = e as Google.Apis.Auth.OAuth2.SubjectTokenException;
			if (subject != null)
			{
				// The SDK wraps every subject-token failure in SubjectTokenException, hiding our
				// IdentityProviderException behind a generic message; unwrap it when present.
				var identityProvider = FindInChain<IdentityProviderException>(e);
				if (identityProvider != null)
					return new Exception(identityProvider.Message, e);
				return new Exception("Workload Identity Federation could not obtain the identity provider token: " + InnermostMessage(e), e);
			}

			if (e is IdentityProviderException)
				return new Exception(e.Message, e);

			if (e is HttpRequestException)
				return new Exception("Could not reach Google Cloud: " + InnermostMessage(e) + " Check the server's outbound HTTPS access to *.googleapis.com.", e);

			// TaskCanceledException without caller cancellation: the request timed out.
			return new Exception("The request to Google Cloud timed out.", e);
		}

		/// <summary>Finds the first exception of the given type in the InnerException chain.</summary>
		private static T FindInChain<T>(Exception e) where T : Exception
		{
			for (Exception x = e; x != null; x = x.InnerException)
			{
				var found = x as T;
				if (found != null) return found;
			}
			return null;
		}

		/// <summary>The message of the deepest exception in the InnerException chain.</summary>
		private static string InnermostMessage(Exception e)
		{
			Exception x = e;
			while (x.InnerException != null) x = x.InnerException;
			return x.Message;
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
			if (e.HttpStatusCode == System.Net.HttpStatusCode.BadRequest && IsChecksumMismatch(details))
				return new Exception("Upload rejected by Google Cloud Storage: the data it received did not match the CRC32C checksum computed by the connector, so nothing was stored and any existing object was left unchanged. This is usually a transient network error - retry the upload. Details: " + details, e);
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
							ssName = b.Name ?? "",
							ssLocation = b.Location ?? "",
							ssStorageClass = b.StorageClass ?? "",
							ssCreated = ParseTimestamp(b.TimeCreatedRaw)
						}
					};

					ssBucketList.Append(record);
				}
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, null, null); }
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
						Location = string.IsNullOrWhiteSpace(ssLocation) ? null : ssLocation.Trim()
					}
				);
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
		} // MssObject_Exists

		/// <summary>
		/// Generates a signed URL for an object.
		/// </summary>
		/// <param name="ssAuthentication">Google Cloud credentials for this call. Leave AuthenticationMethod empty (or set &apos;ServiceAccountKey&apos;) to use ClientEmail + PrivateKey, or set &apos;WorkloadIdentityFederation&apos; for keyless access.</param>
		/// <param name="ssOperation">&apos;Download&apos;, &apos;Upload&apos; or &apos;Delete&apos; (case-insensitive).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the object the URL grants access to. Required: a URL without an object name would grant access to the bucket itself.</param>
		/// <param name="ssExpirationMinutes">How long the URL stays valid: 1 to 10080 minutes (7 days, the maximum for a V4 signed URL).</param>
		/// <param name="ssContentType">Upload only: the exact Content-Type the client must send, which becomes part of the signature. Ignored for Download and Delete.</param>
		/// <param name="ssURL">The V4 signed URL.</param>
		public void MssObject_GetSignedUrl(RCGCS_AuthenticationRecord ssAuthentication, string ssOperation, string ssBucketName, string ssObjectName, int ssExpirationMinutes, string ssContentType, out string ssURL)
		{
			// Validate before signing: UrlSigner signs a bucket-level URL when ObjectName is empty, which
			// would grant access to every object in the bucket instead of one.
			if (string.IsNullOrWhiteSpace(ssBucketName))
				throw new ArgumentException("BucketName is required.");
			if (string.IsNullOrEmpty(ssObjectName))
				throw new ArgumentException("ObjectName is required; a signed URL without an object name would grant access to the bucket itself.");
			if (!string.IsNullOrEmpty(ssContentType) && (ssContentType.IndexOf('\r') >= 0 || ssContentType.IndexOf('\n') >= 0))
				throw new ArgumentException("ContentType must not contain line breaks.");

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

			// Only Upload signs the ContentType: the browser sends that header with the PUT. For
			// Download and Delete a signed Content-Type is never sent, so Google would reject the
			// request with SignatureDoesNotMatch.
			if (method == HttpMethod.Put && !string.IsNullOrEmpty(ssContentType))
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
			// A Google API error while signing comes from signBlob (federation); token, network and
			// timeout failures get the same messages as every other action.
			catch (Google.GoogleApiException e) { throw FriendlySigningException(e, auth); }
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
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
					ContentType = string.IsNullOrEmpty(ssContentType) ? null : ssContentType,
					Metadata = ToMetadataDictionary(ssMetadata)
				};

				using (var stream = new MemoryStream(ssContent ?? new byte[0]))
				{
					storageClient.UploadObject(gcsObject, stream);
				}
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }
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
					// Automatic skips the hash check when Google serves the object transcoded (e.g. Content-Encoding: gzip), where it would fail on valid data.
					var obj = storageClient.DownloadObject(ssBucketName, ssObjectName, stream, new DownloadObjectOptions { DownloadValidationMode = DownloadValidationMode.Automatic });
					ssContent = stream.ToArray();
					ssContentType = obj.ContentType ?? "";
				}
			}
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, ssObjectName); }
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
							record.ssSTGCS_Object.ssName = obj.Name ?? "";
							record.ssSTGCS_Object.ssSize = (long)Math.Min(obj.Size ?? 0, long.MaxValue);
							record.ssSTGCS_Object.ssContentType = obj.ContentType ?? "";
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
			catch (Exception e) when (IsTranslatable(e)) { throw TranslateException(e, auth, ssBucketName, null); }
		} // MssObject_List

	} // CssGoogleCloudStorage_ext

} // OutSystems.NssGoogleCloudStorage_ext

