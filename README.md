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
- `StorageClient` and `UrlSigner` instances are cached per credential and reused across requests. The cache key is a SHA-256 hash, never the secret itself, and the two methods never share an entry. Identity-provider tokens are refreshed a minute before they expire.
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

Store `PrivateKey` and `ClientSecret` encrypted (for example in an encrypted Site Property or database value). Don't hard-code them or write them to logs.

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
| `ServiceAccountEmail` | The service account the extension acts as |
| `TokenEndpoint` | Your identity provider's OAuth 2.0 token endpoint (https) |
| `ClientId` / `ClientSecret` | Your app registration at the identity provider |
| `Scope` | Optional `scope` for the token request |
| `Audience` | Optional `audience` for the token request (some providers require it) |
| `SubjectToken` | Optional JWT you obtained yourself. When set, `TokenEndpoint`, `ClientId` and `ClientSecret` are not needed |

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

### Object Operations

#### `Object_Upload`
Uploads binary content to a bucket. Overwrites the object if it already exists.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Destination bucket |
| `ObjectName` | In | Text | Full path and name in the bucket |
| `Content` | In | Binary Data | File content |
| `ContentType` | In | Text | MIME type, e.g. `application/pdf` |
| `Metadata` | In | List of `GCS_MetadataEntry` | Optional custom key-value metadata to store with the object |

#### `Object_Download`
Downloads an object's content and content type.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path and name |
| `Content` | Out | Binary Data | File content |
| `ContentType` | Out | Text | Stored MIME type |

#### `Object_List`
Lists objects in a bucket, optionally filtered by prefix, with pagination and folder-style listing.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket to list |
| `Prefix` | In | Text | Optional: only objects whose names start with this |
| `MaxResults` | In | Integer | Objects per call. `0` (default) returns everything in one call; negative values are rejected |
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
Changes an object's metadata without re-uploading its content. Only the fields you pass change, and at least one change is required. The write is guarded by a metageneration precondition, so if someone else changed the metadata in the meantime the call fails instead of overwriting their change.

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

If a delete fails partway through, the error says how many objects were already deleted.

#### `Object_Delete`
Permanently deletes an object.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |

#### `Object_Copy`
Copies an object within a bucket or to another bucket, without downloading it. Overwrites the destination if it exists.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `SourceBucketName` | In | Text | Bucket that holds the object |
| `SourceObjectName` | In | Text | Full path and name of the object |
| `DestinationBucketName` | In | Text | Bucket to copy into (can be the source bucket) |
| `DestinationObjectName` | In | Text | Full path and name of the copy |

#### `Object_Move`
Moves an object within a bucket or to another bucket. Use the same bucket to rename. Overwrites the destination if it exists.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `SourceBucketName` | In | Text | Bucket that holds the object |
| `SourceObjectName` | In | Text | Full path and name of the object |
| `DestinationBucketName` | In | Text | Bucket to move into (can be the source bucket) |
| `DestinationObjectName` | In | Text | New full path and name |

A move is a copy followed by a delete, so it isn't atomic. If the delete fails, the error says that both objects now exist.

#### `Object_GetSignedUrl`
Creates a time-limited V4 signed URL that lets a browser or other client read, write or delete one object directly, without credentials.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `Operation` | In | Text | Required: `Download` (GET), `Upload` (PUT) or `Delete` (DELETE), case-insensitive |
| `BucketName` | In | Text | Bucket |
| `ObjectName` | In | Text | Full path and name |
| `ExpirationMinutes` | In | Integer | How long the URL is valid: 1 to 10080 minutes (7 days, the V4 maximum) |
| `ContentType` | In | Text | Optional, for `Upload`: the exact `Content-Type` the client will send. It becomes part of the signature, so an upload with a different type is rejected. Empty allows any |
| `URL` | Out | Text | The signed URL. For `Upload`, the client sends an HTTP PUT with the file as the body |

A signed URL covers one object path, so request one `Upload` URL per file.

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

#### `Bucket_Exists`
Checks whether a bucket exists and the service account can access it, without listing its contents.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket name |
| `Exists` | Out | Boolean | True if the bucket exists. Permission and authentication problems raise an error instead of returning False |

#### `Bucket_Delete`
Deletes an empty bucket.

| Parameter | Direction | Type | Description |
|---|---|---|---|
| `BucketName` | In | Text | Bucket to delete; it must be empty |

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
- `OutSystems.SAP.API.dll`

To publish:

1. Open `Source/NET/GoogleCloudStorage_ext.sln` in Integration Studio.
2. Run Verify, which compiles the .NET code.
3. Run 1-Click Publish to publish to your environment.

---

## Testing

The test suite in [tests/](tests/) covers every action and needs no Google account:

- integration tests against the [fake-gcs-server](https://github.com/fsouza/fake-gcs-server) emulator, through the `GCSCONNECTOR_EMULATOR_HOST` environment variable (never set on a real server);
- offline tests for signing, validation and caching;
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
- Store `PrivateKey` and `ClientSecret` encrypted, for example as an encrypted Site Property. Don't hard-code or log them.
- For large files, use `Object_GetSignedUrl` so the transfer goes directly between the browser and GCS instead of through the OutSystems server.
- Bucket names must be 3 to 63 characters of lowercase letters, numbers and hyphens.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, build steps and coding conventions.

---

## License

MIT. See [LICENSE](LICENSE).
