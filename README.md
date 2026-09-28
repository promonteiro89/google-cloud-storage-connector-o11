# Google Cloud Storage Connector for OutSystems 11

[![CI](https://github.com/promonteiro89/google-cloud-storage-connector-o11/actions/workflows/ci.yml/badge.svg)](https://github.com/promonteiro89/google-cloud-storage-connector-o11/actions/workflows/ci.yml)
[![Platform](https://img.shields.io/badge/Platform-OutSystems_11-red.svg)](https://www.outsystems.com/)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GCS SDK](https://img.shields.io/badge/SDK-Google_Cloud_Storage-green.svg)](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Storage.V1/latest)

A .NET Framework 4.8 Integration Studio extension for OutSystems 11 (O11) that provides a seamless integration with Google Cloud Storage (GCS). It wraps the official `Google.Cloud.Storage.V1` SDK behind a set of server actions for buckets, objects, and signed URLs.

> Looking for the OutSystems Developer Cloud (ODC) edition? See [google-cloud-storage-connector-odc](https://github.com/promonteiro89/google-cloud-storage-connector-odc).

## Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Authentication](#authentication)
- [Action Reference](#action-reference)
  - [Object Operations](#object-operations)
  - [Bucket Operations](#bucket-operations)
- [Data Structures](#data-structures)
- [Project Structure](#project-structure)
- [Build and Deployment](#build-and-deployment)
- [Best Practices](#best-practices)
- [Contributing](#contributing)
- [License](#license)

---

## Architecture

```
Source/NET/
├── GoogleCloudStorage_ext.csproj   # Project definition (.NET Framework 4.8)
├── Interface.cs                    # Action signatures (IssGoogleCloudStorage_ext)
├── GoogleCloudStorage_ext.cs       # Implementation (StorageClient adapter)
├── AssemblyInfo.cs
├── Structures.cs / Records.cs / RecordLists.cs / Entities.cs   # Generated OutSystems data types
└── Bin/                            # Compiled output + referenced assemblies
```

The connector is architected as an **adapter** that bridges the OutSystems 11 runtime with the official Google Cloud Storage .NET SDK, keeping application logic decoupled from the low-level SDK.

### Key Architectural Decisions
- **Keyless authentication:** Workload Identity Federation (Google's recommended method for workloads outside Google Cloud) works with any OIDC identity provider, server-to-server. Service account keys remain supported as a legacy fallback. Both behave exactly like the ODC connector. See [Authentication](#authentication).
- **One credential record, same as ODC:** every action takes one `GCS_Authentication` record with exactly the same attributes as the ODC connector's `Authentication` structure, so apps and documentation carry over between the two platforms.
- **Client reuse:** `StorageClient` and `UrlSigner` instances are cached per credential (keyed by a SHA-256 hash, never the raw secret; the two methods never share an entry) and reused across requests. They are thread-safe, so this removes credential-parsing, token-exchange and connection-setup overhead from every call and prevents socket exhaustion under load. Identity-provider tokens are refreshed a minute before they expire.
- **V4 Signed URLs:** Signed URLs (GOOG4-RSA-SHA256) are signed locally with a service account key, or by the service account through the IAM `signBlob` API with Workload Identity Federation. Either way, large transfers go directly between the browser and GCS, bypassing the OutSystems server.
- **Stateless credentials:** Credentials are passed as action inputs rather than stored in the extension, so the same module can serve multiple projects and service accounts.

---

## Prerequisites

- OutSystems 11 with **Integration Studio** (matching your environment version)
- **.NET Framework 4.8** targeting pack
- An active Google Cloud Project with billing enabled
- A Service Account with the appropriate IAM roles:
  - `Storage Object Admin` — object read/write/delete
  - `Storage Admin` — required for bucket management (create/delete/list)
- With **Workload Identity Federation**: grant the federated identity `Workload Identity User` on the service account, plus `Service Account Token Creator` for **Signed URLs** (signing goes through the IAM `signBlob` API). With a **service account key**, signed URLs are signed locally and need no extra role.

---

## Authentication

Two methods are supported, selected with `AuthenticationMethod`:

| Method | Google's guidance | Google credential stored | Signed URLs |
|---|---|---|---|
| **`WorkloadIdentityFederation`** | **Recommended** for workloads outside Google Cloud | **None**, only short-lived tokens | Signed by the service account through the IAM `signBlob` API |
| **`ServiceAccountKey`** *(default when empty)* | Last resort | A long-lived private key | Signed locally with the key |

### Passing credentials to an action

Every action takes a single mandatory `Authentication` input: a `GCS_Authentication` record with exactly the same attributes as the ODC connector's `Authentication` structure. Leave `AuthenticationMethod` empty to use a service account key; set it to `WorkloadIdentityFederation` for keyless access. Because the method is just a value, you can drive it from a Site Property and migrate one environment at a time.

Tip: build the record once, in a small server action (or function) of your own that fills it from your Site Properties, and pass that to every call.

`ProjectId` is required by `Bucket_List` and `Bucket_Create` (the project-scoped actions); the other actions don't use it.

> **Upgrading from 1.5.x or earlier?** v1.6.0 replaced the flat `ProjectId`, `ClientEmail` and `PrivateKey` inputs with the `Authentication` input (a `GCS_Authentication` record). See the [CHANGELOG](CHANGELOG.md) for the migration steps.

Store secret values (`PrivateKey`, `ClientSecret`) encrypted — for example in an encrypted Site Property or database value — and never hard-code them or write them to logs.

### Workload Identity Federation (recommended, keyless)

The extension never holds a Google key. On each token refresh (roughly hourly) it:

1. obtains a JWT from **your identity provider** using the standard OAuth 2.0 **client-credentials** grant. This is server-to-server with no user interaction, so it works in server actions and timers. Alternatively, you pass a JWT you already have in `SubjectToken`;
2. exchanges it with **Google's Security Token Service** for a federated token;
3. **impersonates your service account** for a short-lived access token, which Storage calls use.

Any OIDC identity provider that issues **signed JWTs (RS256/ES256)** works: Microsoft Entra ID, Okta, Auth0, Keycloak, Ping, ADFS, and others. The OutSystems server must be able to reach your provider's token endpoint, `sts.googleapis.com` and `iamcredentials.googleapis.com`.

| `Authentication` field | Description |
|---|---|
| `AuthenticationMethod` | `WorkloadIdentityFederation` |
| `ProjectId` | Your Google Cloud project ID (used by `Bucket_List` / `Bucket_Create`) |
| `WorkloadIdentityProvider` | `//iam.googleapis.com/projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/POOL/providers/PROVIDER` (the `https://iam.googleapis.com/...` and `projects/...` forms are also accepted) |
| `ServiceAccountEmail` | The service account the extension acts as |
| `TokenEndpoint` | Your identity provider's OAuth 2.0 token endpoint (**https**) |
| `ClientId` / `ClientSecret` | Your app registration at the identity provider (store the secret encrypted) |
| `Scope` | Optional `scope` for the token request |
| `Audience` | Optional `audience` for the token request (some providers require it) |
| `SubjectToken` | Optional: a JWT you obtained yourself. When set, `TokenEndpoint`/`ClientId`/`ClientSecret` are not needed |

Client authentication uses `client_secret_post`. If the provider rejects the client (`invalid_client`), the extension retries once with HTTP Basic (`client_secret_basic`) and remembers which method worked.

**Provider examples** (confirm your tokens' `iss` and `aud` claims by decoding one, e.g. at [jwt.ms](https://jwt.ms)):

| Provider | `TokenEndpoint` | `Scope` / `Audience` | Notes |
|---|---|---|---|
| Microsoft Entra ID | `https://login.microsoftonline.com/TENANT_ID/oauth2/v2.0/token` | `Scope` = `api://YOUR_APP_ID_URI/.default` | Request a token for **your own app registration**, not Microsoft Graph. The issuer is `https://sts.windows.net/TENANT_ID/` for v1 tokens (the default) or `https://login.microsoftonline.com/TENANT_ID/v2.0` for v2. |
| Okta | `https://YOUR_ORG.okta.com/oauth2/AUTH_SERVER_ID/v1/token` | `Scope` = your custom scope | Needs a **custom authorization server** (the org server doesn't issue client-credentials tokens with custom scopes). |
| Auth0 | `https://YOUR_TENANT.auth0.com/oauth/token` | `Audience` = your API identifier | Machine-to-machine application. |
| Keycloak | `https://HOST/realms/REALM/protocol/openid-connect/token` | — | Enable **Service accounts** on the client. |

**Google Cloud setup (one-time).** The federation pieces cost nothing: IAM, STS and IAM Credentials are free.

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

Then set `WorkloadIdentityProvider` to `//iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/outsystems-apps/providers/my-idp` and `ServiceAccountEmail` to `$SA`. If your identity provider isn't reachable from the internet, upload its public keys (JWKS) to the provider instead of relying on its discovery URL. The extension itself must still be able to reach the provider's token endpoint.

### Service Account Key (legacy)

Set these in the `GCS_Authentication` record, with `AuthenticationMethod` empty or `ServiceAccountKey`:

| Field | Source in the service account JSON key | Description |
|---|---|---|
| `ProjectId` | `project_id` | Your Google Cloud project ID |
| `ClientEmail` | `client_email` | Service account email |
| `PrivateKey` | `private_key` | Full RSA private key (including the `BEGIN`/`END` headers). JSON-escaped `\n` newlines are handled automatically. |

---

## Action Reference

### Object Operations

#### `Object_Upload`
Uploads binary content to a bucket. Overwrites the object if it already exists.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | Text | Destination bucket |
| `ObjectName` | Text | Full path/filename in the bucket |
| `Content` | Binary Data | File content to upload |
| `ContentType` | Text | MIME type (e.g. `application/pdf`, `image/png`) |
| `Metadata` | List of `GCS_MetadataEntry` | Optional custom key-value metadata to store with the object (e.g. user id, tenant, document type) |

#### `Object_Download`
Downloads an object's content and content type.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename |
| `Content` | Out | Binary Data | Retrieved file content |
| `ContentType` | Out | Text | Stored MIME type |

#### `Object_List`
Lists objects in a bucket, optionally filtered by prefix, with support for pagination and folder-style navigation.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `Prefix` | In | Text | Optional prefix filter for hierarchical navigation |
| `MaxResults` | In | Integer | Maximum objects to return in this call; `0` (default) returns everything |
| `PageToken` | In | Text | Continuation token from a previous call's `NextPageToken`; empty starts from the first page |
| `Delimiter` | In | Text | Typically `/` — groups nested objects into `PrefixList` for folder-style browsing; empty lists recursively |
| `ObjectList` | Out | List of `GCS_Object` | Object metadata collection |
| `NextPageToken` | Out | Text | Non-empty when more results exist (paged mode only) — pass it as `PageToken` in the next call |
| `PrefixList` | Out | List of `GCS_Prefix` | The "folders" found directly under `Prefix` when `Delimiter` is set |

#### `Object_Exists`
Checks whether an object exists via a lightweight metadata probe.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename to check |
| `Exists` | Out | Boolean | True if the object exists |

#### `Object_GetMetadata`
Retrieves an object's full metadata (size, content type, hashes, generation, storage class, timestamps) without downloading its content. Returns `Exists = False` if the object is not found, leaving `Metadata` empty.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename to inspect |
| `Exists` | Out | Boolean | True if the object was found |
| `Metadata` | Out | `GCS_ObjectMetadata` | Full metadata (only populated when `Exists` is True) |
| `CustomMetadata` | Out | List of `GCS_MetadataEntry` | The object's custom key-value metadata; empty when none |

#### `Object_UpdateMetadata`
Updates an object's metadata without re-uploading its content. Only the provided fields change; the write is guarded by a metageneration precondition so concurrent updates fail cleanly instead of overwriting each other.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename to update |
| `ContentType` / `ContentEncoding` / `ContentDisposition` / `CacheControl` | In | Text | New values; empty = unchanged |
| `Metadata` | In | List of `GCS_MetadataEntry` | Custom metadata changes. Empty list = unchanged; an entry with an empty `Value` removes that key, others are set/overwritten |

#### `Object_DeleteByPrefix`
Deletes all objects whose names start with the given prefix (a "folder" and everything under it).

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | Source bucket |
| `Prefix` | In | Text | Mandatory and non-empty (safety guard against wiping a whole bucket), e.g. `uploads/2025/` |
| `DeletedCount` | Out | Long Integer | Number of objects deleted |

#### `Object_Delete`
Permanently removes an object from a bucket.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | Text | Source bucket |
| `ObjectName` | Text | Full path/filename to delete |

#### `Object_Copy`
Copies an object to another location, within the same bucket or across buckets, without downloading its content. Overwrites the destination if it exists.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `SourceBucketName` | Text | Bucket that currently contains the object |
| `SourceObjectName` | Text | Full path/filename of the source object |
| `DestinationBucketName` | Text | Bucket to copy into (can equal the source) |
| `DestinationObjectName` | Text | Full path/filename for the destination |

#### `Object_Move`
Moves an object (copy + delete of the source), within the same bucket or across buckets. Use the same source and destination bucket to rename. Overwrites the destination if it exists.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `SourceBucketName` | Text | Bucket that currently contains the object |
| `SourceObjectName` | Text | Full path/filename of the source object |
| `DestinationBucketName` | Text | Bucket to move into (can equal the source) |
| `DestinationObjectName` | Text | Full path/filename for the destination |

> **Note:** Move is copy-then-delete and is not atomic — the source is removed only after a successful copy.

#### `Object_GetSignedUrl`
Generates a time-limited V4 signed URL for secure, direct-to-browser file access. The `Operation` controls what the URL permits.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `Operation` | In | Text | `Download` (GET), `Upload` (PUT), or `Delete` (DELETE). Case-insensitive. Defaults to `Download`. |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename |
| `ExpirationMinutes` | In | Integer | Link validity duration (V4 maximum: 7 days / 10 080 minutes) |
| `ContentType` | In | Text | Optional, for Upload URLs: the exact `Content-Type` the client will send in the PUT. It becomes part of the signature, so mismatching uploads are rejected. Empty allows any. |
| `URL` | Out | Text | Temporary secure URL. For `Upload`, the client sends an HTTP PUT with the file as the body. |

> **Multi-upload:** signed URLs are bound to a specific object path, so request one `Upload` URL per file (pass each file's `ObjectName`).

---

### Bucket Operations

#### `Bucket_List`
Lists all buckets in the specified project.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketList` | Out | List of `GCS_Bucket` | Project bucket metadata collection |

#### `Bucket_Create`
Creates a new globally unique storage bucket.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | Text | Globally unique name |
| `Location` | Text | Geographic region (e.g. `US`, `EU`, `asia-east1`) |

#### `Bucket_Exists`
Checks whether a bucket exists and is accessible to the service account, without listing its contents.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `Authentication` | In | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | In | Text | The globally unique name of the storage bucket |
| `Exists` | Out | Boolean | True if the bucket exists and the service account can access it |

#### `Bucket_Delete`
Deletes an empty storage bucket.

| Input | Type | Description |
|-------|------|-------------|
| `Authentication` | `GCS_Authentication` | Credentials for either method, see [Authentication](#authentication) |
| `BucketName` | Text | Name of the bucket to delete |

---

## Data Structures

### `GCS_Authentication`
Google Cloud credentials for either method (see [Authentication](#authentication)). Same attributes as the ODC connector's `Authentication` structure. Passed through the mandatory `Authentication` input of every action.
- `ProjectId`: Text (mandatory in the record; used by `Bucket_List` / `Bucket_Create`)
- `AuthenticationMethod`: Text: `WorkloadIdentityFederation` or `ServiceAccountKey` (empty = `ServiceAccountKey`)
- `ClientEmail`, `PrivateKey`: Text (ServiceAccountKey only)
- `WorkloadIdentityProvider`, `ServiceAccountEmail`, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope`, `Audience`, `SubjectToken`: Text (WorkloadIdentityFederation only)

### `GCS_Object`
Object metadata (list entry).
- `Name`: Text (full path)
- `Size`: Long Integer
- `ContentType`: Text
- `Updated`: Date Time (UTC)

### `GCS_MetadataEntry`
A single custom metadata key-value pair stored with an object.
- `Key`: Text (e.g. `department`)
- `Value`: Text — in `Object_UpdateMetadata`, an empty Value removes the key

### `GCS_Prefix`
A folder-style entry returned by `Object_List` when `Delimiter` is set — a common prefix shared by the objects grouped under it.
- `Prefix`: Text (e.g. `images/2026/`)

### `GCS_Bucket`
Storage container metadata.
- `Name`: Text
- `Location`: Text
- `StorageClass`: Text
- `Created`: Date Time (UTC)

### `GCS_ObjectMetadata`
Complete metadata of an object (returned by `Object_GetMetadata`).
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

## Project Structure

```
Source/NET/
├── GoogleCloudStorage_ext.csproj   # References Google.Cloud.Storage.V1, Google.Apis.*, etc.
├── Interface.cs                    # OutSystems action signatures
├── GoogleCloudStorage_ext.cs       # StorageClient implementation & credential handling
├── Structures.cs                   # GCS_Object / GCS_Bucket / GCS_ObjectMetadata
├── Records.cs / RecordLists.cs     # Generated record & list wrappers
├── Entities.cs
├── AssemblyInfo.cs
└── Bin/                            # Referenced assemblies + build output
```

There is no `packages.config` / `PackageReference` — Integration Studio requires referenced assemblies to exist as plain DLLs in `Bin\`, referenced via `<HintPath>`.

---

## Build and Deployment

**Getting the dependencies**

Third-party NuGet DLLs (`Google.*`, `Newtonsoft.Json`, `System.*`, `Microsoft.*`) are committed under `Source/NET/Bin/`, so the project builds out of the box.

The **OutSystems platform assemblies** are *not* included (they are proprietary and ship with Integration Studio itself). Before opening the project, copy these into `Source/NET/Bin/` from your own Integration Studio installation, or from the `Bin\` folder of any existing extension in your environment:

- `OutSystems.RuntimeCommon.dll`
- `OutSystems.HubEdition.RuntimePlatform.dll`
- `OutSystems.HubEdition.DatabaseAbstractionLayer.dll`
- `OutSystems.REST.API.dll`
- `OutSystems.SOAP.API.dll`
- `OutSystems.SAP.API.dll`

**Publish**

1. Open `Source/NET/GoogleCloudStorage_ext.sln` in Integration Studio.
2. Verify the extension (**Verify** — compiles the .NET code).
3. Use **1-Click Publish** to compile and publish to your OutSystems environment.

---

## Testing

The repo ships a full test suite ([tests/](tests/)) covering every action — **no Google account needed**: integration tests run against the [fake-gcs-server](https://github.com/fsouza/fake-gcs-server) emulator (the extension honors the extension-specific `GCSCONNECTOR_EMULATOR_HOST` environment variable, which is never set on a real server), signing/validation/caching tests run fully offline, and the Workload Identity Federation protocol is contract-tested against an in-process fake identity provider, STS, IAM Credentials and Storage.

A separate **live** CI job runs keyless against real Google Cloud, using GitHub's own OIDC token as the identity provider.

```powershell
cd tests
.\run-tests.ps1 -Download   # first run fetches the emulator
```

See [tests/README.md](tests/README.md) for details.

---

## Best Practices

- **Prefer keyless:** Use Workload Identity Federation where you can, so no long-lived Google key exists to leak or rotate.
- **Security:** Store `PrivateKey` and `ClientSecret` encrypted (for example as an encrypted Site Property); never hard-code or log them.
- **Efficiency:** For large files, prefer `Object_GetSignedUrl` so uploads/downloads go directly between the browser and GCS instead of through the OutSystems server.
- **Naming:** Follow GCS bucket naming constraints (3–63 characters, lowercase letters, numbers, and hyphens).

---

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, build steps, and coding conventions.

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
