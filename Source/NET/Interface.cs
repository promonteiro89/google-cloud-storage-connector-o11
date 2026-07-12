using System;
using System.Collections;
using System.Data;
using OutSystems.HubEdition.RuntimePlatform;

namespace OutSystems.NssGoogleCloudStorage_ext {

	public interface IssGoogleCloudStorage_ext {

		/// <summary>
		/// Uploads a file (Binary Data) to a specific Google Cloud Storage bucket. If the file exists, it will be overwritten.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssContent">The actual file content to be uploaded or the content retrieved during download.</param>
		/// <param name="ssContentType">The MIME type of the file (e.g., application/pdf, image/png). This helps browsers handle the file correctly when downloaded.</param>
		void MssObject_Upload(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, byte[] ssContent, string ssContentType);

		/// <summary>
		/// Downloads the content and metadata of a specific object from a Google Cloud Storage bucket.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssContent">The actual file data retrieved from Google Cloud Storage.</param>
		/// <param name="ssContentType">The MIME type of the file (e.g., image/jpeg), as stored in Google Cloud.</param>
		void MssObject_Download(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out byte[] ssContent, out string ssContentType);

		/// <summary>
		/// Returns a list of all objects within a bucket. Use the Prefix parameter to filter by &quot;folders&quot; or file name patterns.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssPrefix">Optional filter to only return objects whose names start with this string.</param>
		/// <param name="ssMaxResults">Maximum number of objects to return in this call. 0 (default) returns all objects. When greater than 0, use NextPageToken to fetch the following page.</param>
		/// <param name="ssPageToken">The NextPageToken returned by a previous call. Leave empty to start from the first page.</param>
		/// <param name="ssDelimiter">Set to &quot;/&quot; for folder-style navigation: objects in nested &quot;subfolders&quot; are grouped into PrefixList instead of being returned individually. Leave empty to list all objects recursively.</param>
		/// <param name="ssObjectList">A list of records containing the names, sizes, and metadata of the objects found in the bucket.</param>
		/// <param name="ssNextPageToken">Non-empty when more results exist — pass it as PageToken in the next call. Empty when the listing is complete.</param>
		/// <param name="ssPrefixList">The &quot;folders&quot; found directly under Prefix when Delimiter is set.</param>
		void MssObject_List(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssPrefix, int ssMaxResults, string ssPageToken, string ssDelimiter, out RLGCS_ObjectRecordList ssObjectList, out string ssNextPageToken, out RLGCS_PrefixRecordList ssPrefixList);

		/// <summary>
		/// Permanently deletes an object from a bucket.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		void MssObject_Delete(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName);

		/// <summary>
		/// Checks if an object exists in the specified bucket without downloading its content.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssExists">Returns True if the object was found in the bucket, and False if it does not exist.</param>
		void MssObject_Exists(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out bool ssExists);

		/// <summary>
		/// Generates a temporary, secure URL to access a private file. This allows users to download files directly from Google.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssOperation">The operation the signed URL will permit: &apos;Download&apos; (GET) to read the object, &apos;Upload&apos; (PUT) to create/overwrite it, or &apos;Delete&apos; (DELETE) to remove it. Leave empty to default to Download.</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssObjectName">The full path/name of the file (e.g., &apos;images/profile.jpg&apos;).</param>
		/// <param name="ssExpirationMinutes">How long (in minutes) the signed URL should remain valid.</param>
		/// <param name="ssContentType">Optional, for Upload URLs: the exact Content-Type the client will send in the PUT request. It becomes part of the signature, so Google rejects uploads with a different Content-Type. Leave empty to allow any.</param>
		/// <param name="ssURL">The temporary, secure URL generated by Google. Users can use this URL to access the file directly via a browser.</param>
		void MssObject_GetSignedUrl(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssOperation, string ssBucketName, string ssObjectName, int ssExpirationMinutes, string ssContentType, out string ssURL);

		/// <summary>
		/// Returns the list of all buckets in the project, including location, storage class, and creation date.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketList">A list of all buckets in the project, including their location, storage class, and creation date.</param>
		void MssBucket_List(string ssProjectId, string ssClientEmail, string ssPrivateKey, out RLGCS_BucketRecordList ssBucketList);

		/// <summary>
		/// Creates a new bucket in the specified project and location (e.g., &apos;US&apos;, &apos;EU&apos;).
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssLocation">The geographic location where the bucket data will be stored. Examples: US (United States), EU (European Union), ASIA (Asia), or specific regions like us-central1.</param>
		void MssBucket_Create(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssLocation);

		/// <summary>
		/// Deletes a bucket. Note: The bucket must be empty before it can be deleted.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		void MssBucket_Delete(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName);

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
		void MssObject_GetMetadata(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, string ssObjectName, out bool ssExists, out RCGCS_ObjectMetadataRecord ssMetadata);

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
		void MssObject_Copy(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName);

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
		void MssObject_Move(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssSourceBucketName, string ssSourceObjectName, string ssDestinationBucketName, string ssDestinationObjectName);

		/// <summary>
		/// Checks whether a bucket exists and is accessible to the service account, without listing its contents.
		/// </summary>
		/// <param name="ssProjectId">The unique ID of your Google Cloud Project (found in the GCS Console).</param>
		/// <param name="ssClientEmail">The &apos;client_email&apos; found in your Service Account JSON key.</param>
		/// <param name="ssPrivateKey">The &apos;private_key&apos; string from your Service Account JSON (including the BEGIN/END headers).</param>
		/// <param name="ssBucketName">The globally unique name of the storage bucket.</param>
		/// <param name="ssExists">True if the bucket exists and the service account can access it.</param>
		void MssBucket_Exists(string ssProjectId, string ssClientEmail, string ssPrivateKey, string ssBucketName, out bool ssExists);

	} // IssGoogleCloudStorage_ext

} // OutSystems.NssGoogleCloudStorage_ext
