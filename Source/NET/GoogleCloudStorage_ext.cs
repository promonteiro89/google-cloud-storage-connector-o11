using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using OutSystems.HubEdition.RuntimePlatform;

namespace OutSystems.NssGoogleCloudStorage_ext
{
	public class CssGoogleCloudStorage_ext : IssGoogleCloudStorage_ext
	{

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
			storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
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
			storageClient.CopyObject(ssSourceBucketName, ssSourceObjectName, ssDestinationBucketName, ssDestinationObjectName);
			storageClient.DeleteObject(ssSourceBucketName, ssSourceObjectName);
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
				ssMetadata.ssSTGCS_ObjectMetadata.ssTimeCreated = obj.TimeCreatedDateTimeOffset?.UtcDateTime ?? new DateTime(1900, 1, 1);
				ssMetadata.ssSTGCS_ObjectMetadata.ssUpdated = obj.UpdatedDateTimeOffset?.UtcDateTime ?? new DateTime(1900, 1, 1);
			}
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				ssExists = false;
			}
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
			var initializer = new ServiceAccountCredential.Initializer(clientEmail)
			{
				Scopes = new[] { StorageService.Scope.CloudPlatform }
			}.FromPrivateKey(privateKey.Replace("\\n", "\n"));

			return new ServiceAccountCredential(initializer);
		}

		/// <summary>
		/// Returns a cached StorageClient for the given service account, creating it on first use.
		/// </summary>
		private static StorageClient GetStorageClient(string projectId, string clientEmail, string privateKey)
		{
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
						ssCreated = b.TimeCreatedDateTimeOffset?.UtcDateTime ?? new DateTime(1900, 1, 1)
					}
				};

				ssBucketList.Append(record);
			}
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

			storageClient.CreateBucket(
				ssProjectId,
				new Bucket
				{
					Name = ssBucketName,
					Location = ssLocation
				}
			);
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
			storageClient.DeleteBucket(ssBucketName);
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
			storageClient.DeleteObject(ssBucketName, ssObjectName);
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
			catch (Google.GoogleApiException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
			{
				ssExists = false;
			}
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
        /// <param name="ssURL"></param>
        public void MssObject_GetSignedUrl(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssOperation, string ssBucketName, string ssObjectName, int ssExpirationMinutes, out string ssURL)
		{
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

			ssURL = urlSigner.Sign(
				ssBucketName,
				ssObjectName,
				TimeSpan.FromMinutes(ssExpirationMinutes),
				method
			);
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

			using (var stream = new MemoryStream(ssContent))
			{
				storageClient.UploadObject(ssBucketName, ssObjectName, ssContentType, stream);
			}
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

			using (var stream = new MemoryStream())
			{
				var obj = storageClient.DownloadObject(ssBucketName, ssObjectName, stream);
				ssContent = stream.ToArray();
				ssContentType = obj.ContentType;
			}
		} // MssObject_Download

		/// <summary>
		/// Lists objects in a bucket with an optional prefix filter.
		/// </summary>
		/// <param name="ssProjectId"></param>
		/// <param name="ssClientEmail"></param>
		/// <param name="ssPrivateKey"></param>
		/// <param name="ssBucketName"></param>
		/// <param name="ssPrefix"></param>
		/// <param name="ssObjectList"></param>
		public void MssObject_List(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssPrefix, out RLGCS_ObjectRecordList ssObjectList)
		{
			var storageClient = GetStorageClient(ssProjectId, ssClientEmail, ssPrivateKey);
			ssObjectList = new RLGCS_ObjectRecordList();
			var objects = storageClient.ListObjects(ssBucketName, ssPrefix);

			foreach (var obj in objects)
			{
				var record = new RCGCS_ObjectRecord(null);
				record.ssSTGCS_Object.ssName = obj.Name;
				record.ssSTGCS_Object.ssSize = (long)Math.Min(obj.Size ?? 0, long.MaxValue);
				record.ssSTGCS_Object.ssContentType = obj.ContentType;
				record.ssSTGCS_Object.ssUpdated = obj.UpdatedDateTimeOffset?.UtcDateTime ?? new DateTime(1900, 1, 1);
				ssObjectList.Append(record);
			}
		} // MssObject_List

	} // CssGoogleCloudStorage_ext

} // OutSystems.NssGoogleCloudStorage_ext

