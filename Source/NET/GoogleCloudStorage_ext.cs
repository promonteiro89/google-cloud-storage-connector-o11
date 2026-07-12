using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using OutSystems.HubEdition.RuntimePlatform;

namespace OutSystems.NssGoogleCloudStorage_ext
{
	public class CssGoogleCloudStorage_ext : IssGoogleCloudStorage_ext
	{

		/// <summary>
		/// Checks whether a bucket exists and is accessible to the service account, without listing its contents.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssExists">True if the bucket exists and the service account can access it.</param>
		public void MssBucket_Exists(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, out bool ssExists) {
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);

			try
			{
				storageClient.GetBucket(ssBucketName);
				ssExists = true;
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssBucket_Exists

		/// <summary>
		/// Copies an object to another location, within the same bucket or across buckets, without downloading its content. If the destination object exists, it will be overwritten.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssSourceBucketName">The bucket that currently contains the object.</param>
		/// <param name="ssSourceObjectName">The full path/name of the source object (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssDestinationBucketName">The bucket to copy the object into (can be the same as the source).</param>
		/// <param name="ssDestinationObjectName">The full path/name for the destination object.</param>
		public void MssObject_Copy(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName) {
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			try
			{
				storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssSourceBucketName, ssSourceObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_Copy

		/// <summary>
		/// Moves an object to another location (copy + delete of the source), within the same bucket or across buckets. Use the same source and destination bucket to rename an object. If the destination exists, it will be overwritten.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssSourceBucketName">The bucket that currently contains the object.</param>
		/// <param name="ssSourceObjectName">The full path/name of the source object (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssDestinationBucketName">The bucket to move the object into (can be the same as the source).</param>
		/// <param name="ssDestinationObjectName">The full path/name for the destination object.</param>
		public void MssObject_Move(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName) {
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			try
			{
				storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssSourceBucketName, ssSourceObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }

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
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssExists">Returns True if the object was found in the bucket, and False if it does not exist. When False, the Metadata record is returned empty.</param>
		/// <param name="ssMetadata">The metadata of the object (size, content type, hashes, version identifiers, storage class, and timestamps), retrieved without downloading its content. Only populated when Exists is True.</param>
		public void MssObject_GetMetadata(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out bool ssExists, out RCGCS_ObjectMetadataRecord ssMetadata) {
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			ssExists = false;
			ssMetadata = new RCGCS_ObjectMetadataRecord(null);

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
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsBucketNotFound(e))
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_GetMetadata
		/// <summary>
		/// Caches of StorageClient/UrlSigner instances per service account. Extension actions run on every
		/// request, and creating a StorageClient per call parses the RSA private key and allocates a new
		/// HttpClient each time (latency + socket exhaustion under load). Statics survive across requests in
		/// the app domain, and both StorageClient and UrlSigner are thread-safe, so they are safe to share.
		/// Keys are SHA-256 hashes of the credentials, so raw private keys are never retained as cache keys.
		/// In practice an application uses one or few service accounts, so these caches stay small.
		/// </summary>
		private static readonly ConcurrentDictionary<string, StorageClient> storageClientCache = new ConcurrentDictionary<string, StorageClient>();
		private static readonly ConcurrentDictionary<string, UrlSigner> urlSignerCache = new ConcurrentDictionary<string, UrlSigner>();

		/// <summary>
		/// Computes a cache key from the service account credentials.
		/// </summary>
		private static string GetCredentialCacheKey(string clientEmail, string privateKey)
		{
			using (var sha = SHA256.Create())
			{
				var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(clientEmail + "\n" + privateKey));
				return Convert.ToBase64String(hash);
			}
		}

		/// <summary>
		/// Creates a ServiceAccountCredential from individual parameters.
		/// </summary>
		private static ServiceAccountCredential GetServiceAccountCredential(string clientEmail, string privateKey)
		{
			try
			{
				var initializer = new ServiceAccountCredential.Initializer(clientEmail)
				{
					Scopes = new[] { StorageService.Scope.CloudPlatform }
				}.FromPrivateKey(privateKey.Replace("\\n", "\n"));

				return new ServiceAccountCredential(initializer);
			}
			catch (Exception e)
			{
				throw new ArgumentException("The PrivateKey could not be parsed. Provide the full 'private_key' value from the service account JSON key, including the -----BEGIN PRIVATE KEY----- and -----END PRIVATE KEY----- lines.", e);
			}
		}

		/// <summary>
		/// Returns a cached StorageClient for the given service account, creating it on first use.
		/// Honors the standard STORAGE_EMULATOR_HOST environment variable (never set on a real
		/// OutSystems server): when present, connects unauthenticated to a local GCS emulator
		/// such as fake-gcs-server, enabling integration tests without Google credentials.
		/// </summary>
		private static StorageClient GetStorageClient(string projectId, string clientEmail, string privateKey)
		{
			string emulatorHost = Environment.GetEnvironmentVariable("STORAGE_EMULATOR_HOST");
			if (!string.IsNullOrEmpty(emulatorHost))
			{
				string baseUri = (emulatorHost.Contains("://") ? emulatorHost : "http://" + emulatorHost).TrimEnd('/') + "/storage/v1/";
				return storageClientCache.GetOrAdd(
					"emulator|" + baseUri,
					_ => new StorageClientBuilder { BaseUri = baseUri, UnauthenticatedAccess = true }.Build());
			}

			return storageClientCache.GetOrAdd(
				GetCredentialCacheKey(clientEmail, privateKey),
				_ => StorageClient.Create(GetServiceAccountCredential(clientEmail, privateKey).ToGoogleCredential()));
		}

		/// <summary>
		/// Returns a cached UrlSigner for the given service account, creating it on first use.
		/// </summary>
		private static UrlSigner GetUrlSigner(string clientEmail, string privateKey)
		{
			return urlSignerCache.GetOrAdd(
				GetCredentialCacheKey(clientEmail, privateKey),
				_ => UrlSigner.FromCredential(GetServiceAccountCredential(clientEmail, privateKey)));
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
		private static Exception FriendlyException(Google.GoogleApiException e, string clientEmail, string bucketName, string objectName)
		{
			string details = e.Error != null && !string.IsNullOrEmpty(e.Error.Message) ? e.Error.Message : e.Message;

			if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && bucketName != null)
			{
				if (objectName != null && !IsBucketNotFound(e))
					return new Exception("Object '" + objectName + "' was not found in bucket '" + bucketName + "'. Details: " + details, e);
				return new Exception("Bucket '" + bucketName + "' does not exist (names are case-sensitive and must match exactly). Details: " + details, e);
			}
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
				return new Exception("Access denied for service account '" + clientEmail + "'. Grant it the required IAM role in Google Cloud (Storage Object Admin for object operations, Storage Admin for bucket operations). Details: " + details, e);
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
				return new Exception("Google rejected the request as unauthenticated. Check that ClientEmail and PrivateKey belong to the same service account and that the key has not been revoked. Details: " + details, e);
			if (e.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
			{
				if (details != null && details.IndexOf("not empty", StringComparison.OrdinalIgnoreCase) >= 0)
					return new Exception("Bucket '" + bucketName + "' is not empty. Delete all objects in it before deleting the bucket. Details: " + details, e);
				return new Exception("Conflict: " + details + " (for Bucket_Create this usually means the name is already taken - bucket names are global across all of Google Cloud Storage).", e);
			}
			return new Exception("Google Cloud Storage error (" + (int)e.HttpStatusCode + " " + e.HttpStatusCode + "): " + details, e);
		}

		/// <summary>
		/// Translates a token endpoint failure (typically 'invalid_grant') into an actionable message.
		/// </summary>
		private static Exception FriendlyAuthException(TokenResponseException e, string clientEmail)
		{
			return new Exception("Google rejected the service account credentials for '" + clientEmail + "' (ClientEmail/PrivateKey mismatch, deleted service account, revoked key, or server clock skew). Details: " + e.Message, e);
		}

		/// <summary>
		/// Lists all buckets in the specified project.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketList"></param>
		public void MssBucket_List(string ssProjectId, string ssClientEmail, string ssPrivateKey, out RLGCS_BucketRecordList ssBucketList)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			ssBucketList = new RLGCS_BucketRecordList();

			try
			{
				var buckets = storageClient.ListBuckets(ssProjectId);

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
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, null, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssBucket_List

		/// <summary>
		/// Creates a new bucket in the specified project.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssLocation"></param>
		public void MssBucket_Create(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssLocation)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);

			try
			{
				storageClient.CreateBucket(
					ssProjectId,
					new Bucket
					{
						Name = ssBucketName,
						Location = ssLocation
					}
				);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssBucket_Create

		/// <summary>
		/// Deletes a bucket. The bucket must be empty.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		public void MssBucket_Delete(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			try
			{
				storageClient.DeleteBucket(ssBucketName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssBucket_Delete

		/// <summary>
		/// Deletes an object from a bucket.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		public void MssObject_Delete(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			try
			{
				storageClient.DeleteObject(ssBucketName, ssObjectName);
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_Delete

		/// <summary>
		/// Checks whether an object exists in a bucket.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssExists"></param>
		public void MssObject_Exists(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out bool ssExists)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);

			try
			{
				storageClient.GetObject(ssBucketName, ssObjectName);
				ssExists = true;
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound && !IsBucketNotFound(e))
			{
				ssExists = false;
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_Exists

        /// <summary>
        /// Generates a signed URL for an object.
        /// </summary>
        /// <param name="ssProjectId"></param>
        /// <param name="ssClientEmail"></param>
        /// <param name="ssPrivateKey"></param>
        /// <param name="ssOperation"></param>
        /// <param name="ssBucketName"></param>
        /// <param name="ssObjectName"></param>
        /// <param name="ssExpirationMinutes"></param>
        /// <param name="ssContentType">Optional; for Upload URLs, the exact Content-Type the client must send. Becomes part of the signature.</param>
        /// <param name="ssURL"></param>
        public void MssObject_GetSignedUrl(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssOperation, string ssBucketName, string ssObjectName, int ssExpirationMinutes, string ssContentType, out string ssURL)
		{
			if (ssExpirationMinutes <= 0)
				throw new ArgumentException("ExpirationMinutes must be greater than zero (received " + ssExpirationMinutes + ").");
			if (ssExpirationMinutes > 10080)
				throw new ArgumentException("ExpirationMinutes cannot exceed 10080 minutes (7 days), the maximum validity of a Google Cloud V4 signed URL (received " + ssExpirationMinutes + ").");

			var urlSigner = GetUrlSigner(ssClientEmail, ssPrivateKey);

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

			ssURL = urlSigner.Sign(template, UrlSigner.Options.FromDuration(TimeSpan.FromMinutes(ssExpirationMinutes)));
		} // MssObject_GetSignedUrl

		/// <summary>
		/// Uploads an object to a bucket.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssContent"></param>
		/// <param name="ssContentType"></param>
		public void MssObject_Upload(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, byte[] ssContent, string ssContentType)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);

			try
			{
				using (var stream = new MemoryStream(ssContent))
				{
					storageClient.UploadObject(ssBucketName, ssObjectName, ssContentType, stream);
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_Upload

		/// <summary>
		/// Downloads an object from a bucket.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssObjectName"></param>
		/// <param name="ssContent"></param>
		/// <param name="ssContentType"></param>
		public void MssObject_Download(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out byte[] ssContent, out string ssContentType)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);

			try
			{
				using (var stream = new MemoryStream())
				{
					var obj = storageClient.DownloadObject(ssBucketName, ssObjectName, stream);
					ssContent = stream.ToArray();
					ssContentType = obj.ContentType;
				}
			}
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, ssObjectName); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_Download

		/// <summary>
		/// Lists objects in a bucket with an optional prefix filter, with support for
		/// pagination (MaxResults/PageToken) and folder-style navigation (Delimiter).
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssPrefix"></param>
		/// <param name="ssMaxResults">Maximum number of results for this call; 0 returns everything.</param>
		/// <param name="ssPageToken">Continuation token from a previous call's NextPageToken.</param>
		/// <param name="ssDelimiter">Typically "/"; groups nested objects into PrefixList.</param>
		/// <param name="ssObjectList"></param>
		/// <param name="ssNextPageToken">Non-empty when more results exist (only in paged mode).</param>
		/// <param name="ssPrefixList">The "folders" directly under Prefix when Delimiter is set.</param>
		public void MssObject_List(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssPrefix, int ssMaxResults, string ssPageToken, string ssDelimiter, out RLGCS_ObjectRecordList ssObjectList, out string ssNextPageToken, out RLGCS_PrefixRecordList ssPrefixList)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
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
			catch (Google.GoogleApiException e) { throw FriendlyException(e, ssClientEmail, ssBucketName, null); }
			catch (TokenResponseException e) { throw FriendlyAuthException(e, ssClientEmail); }
		} // MssObject_List

	} // CssGoogleCloudStorage_ext

} // OutSystems.NssGoogleCloudStorage_ext

