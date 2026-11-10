# Google Cloud Storage Connector for OutSystems 11

[![CI](https://github.com/promonteiro89/google-cloud-storage-connector-o11/actions/workflows/ci.yml/badge.svg)](https://github.com/promonteiro89/google-cloud-storage-connector-o11/actions/workflows/ci.yml)
[![Platform](https://img.shields.io/badge/Platform-OutSystems_11-red.svg)](https://www.outsystems.com/)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GCS SDK](https://img.shields.io/badge/SDK-Google_Cloud_Storage-green.svg)](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Storage.V1/latest)

An Integration Studio extension (.NET Framework 4.8) that gives OutSystems 11 apps access to Google Cloud Storage: buckets, objects and signed URLs, through the official `Google.Cloud.Storage.V1` SDK.

## Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Authentication](#authentication)
- [Action Reference](#action-reference)
  - [Object Operations](#object-operations)
  - [Bucket Operations](#bucket-operations)
- [Data Structures](#data-structures)
- [Build and Deployment](#build-and-deployment)
- [Testing](#testing)
- [Best Practices](#best-practices)
- [Contributing](#contributing)
- [License](#license)
- [Third-party components](#third-party-components)

---

## Architecture

```
Source/NET/
├── GoogleCloudStorage_ext.csproj   # Project definition (.NET Framework 4.8)
├── Interface.cs                    # Action signatures (IssGoogleCloudStorage_ext)
├── GoogleCloudStorage_ext.cs       # Implementation
├── AssemblyInfo.cs
├── Structures.cs / Records.cs / RecordLists.cs / Entities.cs   # Generated OutSystems data types
└── Bin/                            # Compiled output + referenced assemblies
```

### Design notes

- Two authentication methods: Workload Identity Federation (keyless, works with any OIDC identity provider) and service account keys. See [Authentication](#authentication).
- Every action takes the same `GCS_Authentication` record, so changing the authentication method changes the record, not the calls.
- `StorageClient` and `UrlSigner` instances are cached per credential and reused across requests. The cache key is a SHA-256 hash, never the secret itself, and the two methods never share an entry. Entries expire after 60 minutes without use (a supplied `SubjectToken`'s at the token's `exp`), and each cache holds at most about 500 entries. Identity-provider tokens are refreshed a minute before they expire.
- `Object_Upload` sends the CRC32C checksum of the exact bytes with the upload, and Google rejects the upload before storing anything if the data it received doesn't match. A corrupted object is never created, and an existing object is never overwritten with bad data.
- Signed URLs (V4, GOOG4-RSA-SHA256) are signed locally with a service account key, or through the IAM `signBlob` API with Workload Identity Federation. Large transfers then go straight between the browser and GCS.
- Credentials are action inputs, not extension settings, so one module can serve several projects and service accounts.

---

## Prerequisites

- OutSystems 11 with Integration Studio (matching your environment version)
- .NET Framework 4.8 targeting pack
- A Google Cloud project with billing enabled
- A service account with the IAM roles your operations need:
  - `Storage Object Admin` for object read/write/delete
  - `Storage Admin` for bucket management (create/delete/list)
  - `Bucket_Exists` needs `storage.buckets.get`, which `Storage Object Admin` doesn't include: use `Storage Admin`, or grant `Storage Legacy Bucket Reader` on the bucket. Without it the action raises an "Access denied" error
- With Workload Identity Federation, the federated identity also needs `Workload Identity User` on the service account, plus `Service Account Token Creator` if you generate signed URLs. With a service account key, signed URLs are signed locally and need no extra role.

---

## Authentication

Two methods, selected with `AuthenticationMethod`:

| Method | Google's guidance | Google credential stored | Signed URLs |
|---|---|---|---|
| `WorkloadIdentityFederation` | Recommended for workloads outside Google Cloud | None, only short-lived tokens | Signed by the service account through the IAM `signBlob` API |
| `ServiceAccountKey` (default when empty) | Last resort | A long-lived private key | Signed locally with the key |

### Passing credentials to an action

Every action takes one mandatory `Authentication` input, a `GCS_Authentication` record. Leave `AuthenticationMethod` empty to use a service account key, or set it to `WorkloadIdentityFederation`. The method is a plain value, so you can read it from a Site Property and switch one environment at a time. A small server action of your own that builds the record from your Site Properties saves repeating it on every call.

`ProjectId` is required by `Bucket_List` and `Bucket_Create`; the other actions don't use it.

Before 1.6.0 the actions took flat `ProjectId`, `ClientEmail` and `PrivateKey` inputs. The [CHANGELOG](CHANGELOG.md) has the migration steps.

Keep `PrivateKey` and `ClientSecret` out of the module's code and out of logs. A Site Property is not encrypted: its value is stored as plain text and anyone with access to Service Center can read it. Store the secrets encrypted in the database, or read them from a secrets manager, and decrypt them just before the call.

### Workload Identity Federation

The extension holds no Google key. When it needs a token (roughly hourly) it:

1. gets a JWT from your identity provider with the OAuth 2.0 client-credentials grant, which needs no user interaction and so works in server actions and timers. Alternatively, pass a JWT you already have in `SubjectToken`;
2. exchanges the JWT with Google's Security Token Service for a federated token;
3. impersonates your service account to get a short-lived access token for the Storage calls.

Any OIDC identity provider that issues signed JWTs (RS256/ES256) works, for example Microsoft Entra ID, Okta, Auth0, Keycloak, Ping or ADFS. The OutSystems server must be able to reach your provider's token endpoint, `sts.googleapis.com` and `iamcredentials.googleapis.com`.

| `Authentication` field | Description |
|---|---|
| `AuthenticationMethod` | `WorkloadIdentityFederation` |
| `ProjectId` | Your Google Cloud project ID (used by `Bucket_List` / `Bucket_Create`) |
| `WorkloadIdentityProvider` | `//iam.googleapis.com/projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/POOL/providers/PROVIDER` (the `https://iam.googleapis.com/...` and `projects/...` forms are also accepted) |
| `ServiceAccountEmail` | The service account the extension acts as. Letters, digits and `@ . - _ +` only |
| `TokenEndpoint` | Your identity provider's OAuth 2.0 token endpoint. Must be `https://`, without a `user:password@` part. Redirects aren't followed, so use the final token URL. The provider has 30 seconds to answer |
| `ClientId` / `ClientSecret` | Your app registration at the identity provider |
| `Scope` | Optional `scope` for the token request |
| `Audience` | Optional `audience` for the token request (some providers require it) |
| `SubjectToken` | Optional JWT you obtained yourself. When set, `TokenEndpoint`, `ClientId` and `ClientSecret` are not needed. Each token gets its own cached credentials, which expire at the token's `exp`. A value that isn't a JWT (three base64url parts separated by dots, at most 32,768 characters, no `Bearer ` prefix), or whose `exp` passed more than 30 seconds ago (the allowance for clock difference), is rejected before anything is cached or sent to Google |

Client authentication uses `client_secret_post`. If the provider rejects the client (`invalid_client`), the extension retries once with HTTP Basic (`client_secret_basic`) and keeps using whichever worked.

Provider examples (check your tokens' `iss` and `aud` claims by decoding one, e.g. at [jwt.ms](https://jwt.ms)):

| Provider | `TokenEndpoint` | `Scope` / `Audience` | Notes |
|---|---|---|---|
| Microsoft Entra ID | `https://login.microsoftonline.com/TENANT_ID/oauth2/v2.0/token` | `Scope` = `api://YOUR_APP_ID_URI/.default` | Request a token for your own app registration, not Microsoft Graph. The issuer is `https://sts.windows.net/TENANT_ID/` for v1 tokens (the default) or `https://login.microsoftonline.com/TENANT_ID/v2.0` for v2. |
| Okta | `https://YOUR_ORG.okta.com/oauth2/AUTH_SERVER_ID/v1/token` | `Scope` = your custom scope | Needs a custom authorization server; the org server doesn't issue client-credentials tokens with custom scopes. |
| Auth0 | `https://YOUR_TENANT.auth0.com/oauth/token` | `Audience` = your API identifier | Machine-to-machine application. |
| Keycloak | `https://HOST/realms/REALM/protocol/openid-connect/token` | | Enable Service accounts on the client. |

Google Cloud setup (one time; IAM, STS and IAM Credentials are free):

```bash
PROJECT_ID=my-project
PROJECT_NUMBER=$(gcloud projects describe $PROJECT_ID --format='value(projectNumber)')
SA=gcs-connector@$PROJECT_ID.iam.gserviceaccount.com

gcloud services enable iam.googleapis.com iamcredentials.googleapis.com sts.googleapis.com --project=$PROJECT_ID
gcloud iam service-accounts create gcs-connector --project=$PROJECT_ID

# Pool + OIDC provider. ISSUER and AUDIENCE are the 'iss' and 'aud' claims of your provider's tokens;
# APP_SUBJECT is their 'sub' claim (for Entra ID client credentials: the service principal's object ID).
gcloud iam workload-identity-pools create outsystems-apps --location=global --project=$PROJECT_ID
gcloud iam workload-identity-pools providers create-oidc my-idp \
  --location=global --workload-identity-pool=outsystems-apps --project=$PROJECT_ID \
  --issuer-uri="ISSUER" --allowed-audiences="AUDIENCE" \
  --attribute-mapping="google.subject=assertion.sub" \
  --attribute-condition="assertion.sub == 'APP_SUBJECT'"

# Let that identity act as the service account (+ sign URLs), and give the service account bucket access.
MEMBER="principal://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/outsystems-apps/subject/APP_SUBJECT"
gcloud iam service-accounts add-iam-policy-binding $SA --role=roles/iam.workloadIdentityUser --member="$MEMBER" --project=$PROJECT_ID
gcloud iam service-accounts add-iam-policy-binding $SA --role=roles/iam.serviceAccountTokenCreator --member="$MEMBER" --project=$PROJECT_ID
gcloud storage buckets add-iam-policy-binding gs://MY_BUCKET --role=roles/storage.objectAdmin --member="serviceAccount:$SA"
```

Then set `WorkloadIdentityProvider` to `//iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/outsystems-apps/providers/my-idp` and `ServiceAccountEmail` to `$SA`. If your identity provider isn't reachable from the internet, upload its public keys (JWKS) to the Google provider instead of relying on its discovery URL. The extension still needs to reach the provider's token endpoint.

### Service account key

Set these in the `GCS_Authentication` record, with `AuthenticationMethod` empty or `ServiceAccountKey`:

| Field | Source in the service account JSON key | Description |
|---|---|---|
| `ProjectId` | `project_id` | Your Google Cloud project ID |
| `ClientEmail` | `client_email` | Service account email |
| `PrivateKey` | `private_key` | The full RSA private key, including the `BEGIN`/`END` lines. JSON-escaped `\n` newlines are accepted. |

---

## Action Reference

Every action's first input is `Authentication` (a `GCS_Authentication` record, see [Authentication](#authentication)). It is listed once here rather than in each table.

An empty bucket or object name is rejected with an "is required" error before anything is sent to Google. So is a bucket name with characters Google doesn't allow (uppercase letters, spaces, line breaks), with an "is not a valid bucket name" error.

### Retries and time limits

- Google's 408, 429, 500, 502, 503 and 504 answers and network failures (a reset connection, DNS, TLS) are repeated, up to 3 attempts, on every action. An unconditional write is never repeated blindly: see `Object_Delete`, `Object_Move`, `Object_UpdateMetadata`, `Bucket_Create` and `Bucket_Delete`.
- Each Storage request has 100 seconds to answer, including any sign-in it needs first, and the identity provider has 30 seconds. A Storage request that times out is not repeated. Within that time, Google's library repeats a slow sign-in request by itself (it allows the first attempt 12 seconds).
- There is no time limit on a whole action, because the caller's own limit differs too much (a screen request is cut off long before a timer is). An action that makes several requests can therefore outlast the caller: a first call with Workload Identity Federation signs in at the identity provider, at Google's token service and at the IAM Credentials API before the Storage request, and `Object_Copy` or `Object_Move` of a large object between locations or storage classes takes several requests, each of which can run for a while. Run long operations from a timer.

### Object Operations

#### `Object_Upload`
Uploads binary content to a bucket. Overwrites the object if it already exists.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Destination bucket |
| `ObjectName` | In | Text | Full path and name in the bucket |
| `Content` | In | Binary Data | File content |
| `ContentType` | In | Text | MIME type, e.g. `application/pdf`. Printable ASCII only: no line breaks, control characters or accented letters |
| `Metadata` | In | List of `GCS_MetadataEntry` | Optional custom key-value metadata to store with the object |

A transient Google error (408, 429, 500, 502, 503, 504, or a dropped connection) repeats the whole upload, up to 3 attempts, about 1 and then 2 seconds apart. Repeating is safe because the same bytes are written again. Timeouts are not repeated.

The upload carries a CRC32C checksum of `Content`. If the data Google receives doesn't match, the action fails with an "Upload rejected by Google Cloud Storage ... retry the upload" error, nothing is stored, and any existing object with that name is left unchanged.

#### `Object_Download`
Downloads an object's content and content type.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path and name |
| `Content` | Out | Binary Data | File content |
| `ContentType` | Out | Text | Stored MIME type |

The download is checked against the object's stored checksum; a mismatch fails with a "did not match the object's checksum ... retry" error and returns no content. An object stored with `Content-Encoding: gzip` comes back decompressed, as its original bytes, and is the one case that is not checked: the stored checksum is of the compressed bytes.

The action reads the object's size first (one extra request per download). An object over the limit is refused before any content is transferred, and the memory for the content is allocated once, at its final size.

A transient Google error, a dropped connection or a checksum mismatch repeats the download, up to 3 attempts, about 1 and then 2 seconds apart. Timeouts are not repeated. If Google stops sending data for 60 seconds in the middle of a download, the action fails with a "stalled" error instead of waiting; a slow download that keeps receiving data is not cut off.

`Content` is held in the server's memory, so large objects can exhaust it well before the hard limit of about 2 GB, which fails with an error saying so. For large objects, give the client a signed URL instead (`Object_GetSignedUrl`, Operation `Download`).

#### `Object_List`
Lists objects in a bucket, optionally filtered by prefix, with pagination and folder-style listing.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket to list |
| `Prefix` | In | Text | Optional: only objects whose names start with this |
| `MaxResults` | In | Integer | Results per call: objects plus `PrefixList` entries together. Google returns at most 1,000 per call, so a larger value still returns up to 1,000, with `NextPageToken` set when more exist. `0` (default) returns everything in one call, however many objects match: on a large bucket that takes one request per 1,000 objects, with no limit on time or memory, so pass a `MaxResults` and page there. Negative values are rejected |
| `PageToken` | In | Text | `NextPageToken` from the previous call; empty for the first page |
| `Delimiter` | In | Text | Usually `/`. Objects in deeper "folders" are returned as `PrefixList` entries instead. Empty lists recursively |
| `ObjectList` | Out | List of `GCS_Object` | The objects found |
| `NextPageToken` | Out | Text | Non-empty when more results exist (only when `MaxResults` > 0). Pass it as `PageToken` in the next call |
| `PrefixList` | Out | List of `GCS_Prefix` | The "folders" directly under `Prefix`, when `Delimiter` is set |

#### `Object_Exists`
Checks whether an object exists by reading its metadata (no download).

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |
| `Exists` | Out | Boolean | True if the object exists. A missing bucket raises an error instead of returning False |

If Google's 404 doesn't say whether the bucket or the object is missing, the action lists at most one object in the bucket to find out (the same check is used by `Object_GetMetadata`).

#### `Object_GetMetadata`
Reads an object's metadata (size, content type, hashes, generation, storage class, timestamps) without downloading it. Returns `Exists = False`, with `Metadata` empty, if the object doesn't exist.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |
| `Exists` | Out | Boolean | True if the object was found |
| `Metadata` | Out | `GCS_ObjectMetadata` | The metadata; empty when `Exists` is False |
| `CustomMetadata` | Out | List of `GCS_MetadataEntry` | The object's custom key-value metadata; empty when there is none |

#### `Object_UpdateMetadata`
Changes an object's metadata without re-uploading its content. Only the fields you pass change, and at least one change is required; everything else, including the object's access control list, is left as it is. The write is guarded by the generation and metageneration it read, so if someone else changed the metadata or replaced the content in the meantime, the call fails instead of overwriting their change.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |
| `ContentType` / `ContentEncoding` / `ContentDisposition` / `CacheControl` | In | Text | New value; empty leaves the field unchanged |
| `Metadata` | In | List of `GCS_MetadataEntry` | Custom metadata changes. An entry with an empty `Value` removes that key; other entries add or overwrite. An empty list leaves custom metadata unchanged |

#### `Object_DeleteByPrefix`
Deletes every object whose name starts with the prefix: a "folder" and everything under it.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `Prefix` | In | Text | Required and not blank, so a call can't empty the whole bucket. E.g. `uploads/2025/` |
| `DeletedCount` | Out | Long Integer | Number of objects deleted |

Each object is deleted only if it is still the version that was listed: an object replaced after the listing is left in place and not counted, and so is one that someone else deleted in the meantime. Transient Google errors and network failures are retried, for the listing and for each delete. If a delete still fails partway through, the error says how many objects were already deleted; `DeletedCount` is not returned in that case.

The action lists every matching object first, then deletes them one at a time. A very large prefix (tens of thousands of objects) can outlast the caller's timeout; run it again to delete the rest. Deletion is permanent only on buckets without Object Versioning or soft delete; see `Object_Delete`.

#### `Object_Delete`
Deletes an object. Deleting an object that doesn't exist raises an "Object ... was not found" error.

The action reads the object's generation first and deletes that generation, so transient Google errors are retried (one extra request per call, using `storage.objects.get`). If another request replaces the object in between, the new generation is read and the delete repeated, up to 3 times. With a role that can delete but not read objects, the object is deleted without the read and without retries. If a request that is repeated after a lost answer or a 500, 502, 503 or 504 finds the generation no longer current, the earlier request is taken to have deleted it, and a newer object written in the meantime is left in place. After a 408 or 429, which mean Google refused the request, the object is read again as usual.

Whether the data can be recovered depends on the bucket: with Object Versioning on, the deleted object is kept as a noncurrent version, and with soft delete on (Google's default for new buckets, 7 days), it can be restored until the retention period ends. Only without both is the deletion permanent.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |

#### `Object_Copy`
Copies an object within a bucket or to another bucket, without downloading it. Overwrites the destination if it exists. If the copy fails because something is missing, the error names the missing source object or destination bucket. A large object copied to another location or storage class is copied by Google in several steps and can take minutes; see [Retries and time limits](#retries-and-time-limits).

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `SourceBucketName` | In | Text | Bucket that holds the object |
| `SourceObjectName` | In | Text | Full path and name of the object |
| `DestinationBucketName` | In | Text | Bucket to copy into (can be the source bucket) |
| `DestinationObjectName` | In | Text | Full path and name of the copy |

#### `Object_Move`
Moves an object within a bucket or to another bucket. Use the same bucket with a different name to rename. Overwrites the destination if it exists. Source and destination must be different objects.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `SourceBucketName` | In | Text | Bucket that holds the object |
| `SourceObjectName` | In | Text | Full path and name of the object |
| `DestinationBucketName` | In | Text | Bucket to move into (can be the source bucket) |
| `DestinationObjectName` | In | Text | New full path and name |

A move is a copy followed by a delete, so it isn't atomic. Both steps are guarded by the source's generation: if another writer replaces the source during the move, nothing is deleted and the error says so. If the delete fails, the error says that both objects now exist.

#### `Object_GetSignedUrl`
Creates a time-limited V4 signed URL that lets a browser or other client read, write or delete one object directly, without credentials.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `Operation` | In | Text | `Download` (GET), `Upload` (PUT) or `Delete` (DELETE), case-insensitive. Optional in Service Studio, where it defaults to `Download`; an empty text is rejected |
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Required: full path and name (an empty name would sign a URL for the whole bucket) |
| `ExpirationMinutes` | In | Integer | How long the URL is valid: 1 to 10080 minutes (7 days, the V4 maximum). With Workload Identity Federation, use 720 (12 hours) or less; see below |
| `ContentType` | In | Text | Optional, `Upload` only: the exact `Content-Type` the client will send. It becomes part of the signature, so an upload with a different type is rejected. Empty allows any. Printable ASCII only. Ignored for `Download` and `Delete` |
| `URL` | Out | Text | The signed URL. For `Upload`, the client sends an HTTP PUT with the file as the body |

A signed URL covers one object path, so request one `Upload` URL per file. An `Upload` URL doesn't limit the size of what is uploaded, and it overwrites an existing object with that name.

With Workload Identity Federation the URL is signed with a key that Google manages for the service account and rotates. Google's documentation says such a key "may not remain valid after 12 hours", so a URL with a longer `ExpirationMinutes` can stop working before it expires. The action accepts up to 7 days with both methods; with federation, keep it at 720 minutes or less. With a service account key the full 7 days apply.

A browser on another origin (your app's domain) can only use the URL if the bucket's CORS configuration allows that origin and method. The extension doesn't manage CORS; set it once on the bucket, for example with `gcloud storage buckets update gs://MY_BUCKET --cors-file=cors.json`.

The client uses an `Upload` URL directly, without the extension, so `Object_Upload`'s automatic CRC32C check doesn't apply to it. To validate direct uploads end to end, follow Google's [data validation guide](https://cloud.google.com/storage/docs/data-validation) in the client.

---

### Bucket Operations

#### `Bucket_List`
Lists the buckets in the project given by `Authentication.ProjectId`.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketList` | Out | List of `GCS_Bucket` | The buckets |

#### `Bucket_Create`
Creates a bucket in the project given by `Authentication.ProjectId`.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Name; must be unique across all of Google Cloud Storage |
| `Location` | In | Text | Region or multi-region, e.g. `US`, `EU`, `asia-east1` |

The bucket gets Google's default settings for the project. The action doesn't turn on uniform bucket-level access or public access prevention; set those on the bucket, or enforce them with an organization policy.

If the create request is repeated after a lost answer or a 5xx and Google then reports the name as taken, the action checks whether the bucket was created by the first request (it is readable and was created after the call started, allowing one minute for clock difference) and succeeds if so.

#### `Bucket_Exists`
Checks whether a bucket exists and the service account can access it, without listing its contents.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket name |
| `Exists` | Out | Boolean | True if the bucket exists. Permission and authentication problems raise an error instead of returning False |

Needs the `storage.buckets.get` permission (see [Prerequisites](#prerequisites)).

#### `Bucket_Delete`
Deletes an empty bucket.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket to delete; it must be empty |

If the delete request is repeated after a lost answer or a 5xx and the bucket is then gone, the first request deleted it and the action succeeds. Deleting a bucket that doesn't exist is still an error.

---

## Data Structures

### `GCS_Authentication`
The credentials passed in every action's `Authentication` input. See [Authentication](#authentication).
- `ProjectId`: Text (mandatory in the record; used by `Bucket_List` / `Bucket_Create`)
- `AuthenticationMethod`: Text: `WorkloadIdentityFederation` or `ServiceAccountKey` (empty = `ServiceAccountKey`)
- `ClientEmail`, `PrivateKey`: Text (ServiceAccountKey only)
- `WorkloadIdentityProvider`, `ServiceAccountEmail`, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope`, `Audience`, `SubjectToken`: Text (WorkloadIdentityFederation only)

### `GCS_Object`
An object in an `Object_List` result.
- `Name`: Text (full path)
- `Size`: Long Integer
- `ContentType`: Text
- `Updated`: Date Time (UTC)

### `GCS_MetadataEntry`
One custom metadata key-value pair of an object.
- `Key`: Text (e.g. `department`)
- `Value`: Text (in `Object_UpdateMetadata`, an empty Value removes the key)

### `GCS_Prefix`
A folder-style entry returned by `Object_List` when `Delimiter` is set: a common prefix shared by the objects grouped under it.
- `Prefix`: Text (e.g. `images/2026/`)

### `GCS_Bucket`
A bucket in a `Bucket_List` result.
- `Name`: Text
- `Location`: Text
- `StorageClass`: Text
- `Created`: Date Time (UTC)

### `GCS_ObjectMetadata`
All metadata of one object, returned by `Object_GetMetadata`.
- `Name`: Text (full path)
- `Bucket`: Text
- `Size`: Long Integer
- `ContentType`: Text
- `ContentEncoding`: Text
- `ContentDisposition`: Text
- `CacheControl`: Text
- `MD5Hash`: Text
- `Crc32c`: Text
- `ETag`: Text
- `Generation`: Long Integer
- `Metageneration`: Long Integer
- `StorageClass`: Text
- `MediaLink`: Text
- `TimeCreated`: Date Time (UTC)
- `Updated`: Date Time (UTC)

---

## Build and Deployment

The project has no `packages.config` or `PackageReference`: Integration Studio needs referenced assemblies as plain DLLs in `Bin\`, referenced through `<HintPath>`. The third-party DLLs (`Google.*`, `Newtonsoft.Json`, `System.*`, `Microsoft.*`) are committed under `Source/NET/Bin/`.

The OutSystems platform assemblies are not in the repo; they are proprietary and come with Integration Studio. Copy these into `Source/NET/Bin/` from your Integration Studio installation, or from the `Bin\` folder of any extension in your environment:

- `OutSystems.RuntimeCommon.dll`
- `OutSystems.HubEdition.RuntimePlatform.dll`
- `OutSystems.HubEdition.DatabaseAbstractionLayer.dll`
- `OutSystems.REST.API.dll`
- `OutSystems.SOAP.API.dll`

To publish the released extension:

1. Download `GoogleCloudStorage_ext.xif` from the [latest release](https://github.com/promonteiro89/google-cloud-storage-connector-o11/releases/latest).
2. Open the `.xif` in Integration Studio (it opens extension files, not the `.sln`).
3. Run Verify, which compiles the .NET code.
4. Run 1-Click Publish to publish to your environment.

To publish your own change, open the `.xif`, choose Edit Source Code, replace the files under the extension's `Source\NET\` folder with the ones from this repository's `Source/NET/`, and then Verify and publish. The `.sln` is for editing and building the code in Visual Studio or with MSBuild.

---

## Testing

The test suite in [tests/](tests/) covers every action and needs no Google account:

- integration tests against the [fake-gcs-server](https://github.com/fsouza/fake-gcs-server) emulator, through an internal test seam that only the test harness sets (no environment variable or server setting can redirect the extension);
- offline tests for signing, validation and caching;
- upload integrity tests: uploads carry the CRC32C of the exact bytes, and data corrupted in transit is rejected with a clear error, against an in-process fake of Google's resumable-upload endpoint;
- contract tests of the Workload Identity Federation protocol against an in-process fake identity provider, STS, IAM Credentials and Storage.

A separate CI job runs the federation chain against real Google Cloud, with GitHub's OIDC token as the identity provider.

```powershell
cd tests
.\run-tests.ps1 -Download   # the first run downloads the emulator
```

See [tests/README.md](tests/README.md) for details.

---

## Best Practices

- Use Workload Identity Federation where you can, so there is no long-lived Google key to leak or rotate.
- Store `PrivateKey` and `ClientSecret` encrypted in the database or in a secrets manager, not in a Site Property, which is plain text. Don't hard-code or log them.
- For large files, use `Object_GetSignedUrl` so the transfer goes directly between the browser and GCS instead of through the OutSystems server.
- Bucket names are 3 to 63 characters of lowercase letters, numbers, hyphens (`-`), underscores (`_`) and dots (`.`), and must start and end with a letter or number. Names with dots can be up to 222 characters but need domain-ownership verification; Google recommends avoiding them.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, build steps and coding conventions.

---

## License

MIT. See [LICENSE](LICENSE).

---

## Third-party components

The extension is distributed with the assemblies it needs, in `Source/NET/Bin/` and inside the `.xif`. Each keeps its own licence: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists them with their versions, and the licence texts are in [licenses/](licenses/).
