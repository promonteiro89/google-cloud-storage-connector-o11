# Google Cloud Storage Connector for OutSystems 11

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
- **Client reuse:** `StorageClient` and `UrlSigner` instances are cached per service account (keyed by a SHA-256 hash of the credentials) and reused across requests. Both are thread-safe, so this removes credential-parsing and connection-setup overhead from every call and prevents socket exhaustion under load.
- **V4 Signed URLs:** Signed URLs are generated locally with the service account private key (GOOG4-RSA-SHA256), enabling secure direct-to-browser transfers that bypass the OutSystems server for large files.
- **Stateless credentials:** Credentials are passed as action inputs rather than stored in the extension, so the same module can serve multiple projects/service accounts.

---

## Prerequisites

- OutSystems 11 with **Integration Studio** (matching your environment version)
- **.NET Framework 4.8** targeting pack
- An active Google Cloud Project with billing enabled
- A Service Account with the appropriate IAM roles:
  - `Storage Object Admin` — object read/write/delete
  - `Storage Admin` — required for bucket management (create/delete/list)

> **Signed URLs** are signed locally using the service account's private key, so the `Service Account Token Creator` role is **not** required.

---

## Authentication

Every action authenticates with a Google Cloud Service Account, supplied as three individual inputs (taken from the service account JSON key). Store them securely in **Site Properties** and pass them at runtime.

| Input | Source in GCP JSON | Description |
|-------|--------------------|-------------|
| `ProjectId` | `project_id` | Your Google Cloud Project ID |
| `ClientEmail` | `client_email` | Service Account identification email |
| `PrivateKey` | `private_key` | Full RSA private key (including the `BEGIN`/`END` headers). JSON-escaped `\n` newlines are handled automatically. |

---

## Action Reference

### Object Operations

#### `Object_Upload`
Uploads binary content to a bucket. Overwrites the object if it already exists.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `BucketName` | Text | Destination bucket |
| `ObjectName` | Text | Full path/filename in the bucket |
| `Content` | Binary Data | File content to upload |
| `ContentType` | Text | MIME type (e.g. `application/pdf`, `image/png`) |

#### `Object_Download`
Downloads an object's content and content type.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename |
| `Content` | Out | Binary Data | Retrieved file content |
| `ContentType` | Out | Text | Stored MIME type |

#### `Object_List`
Lists objects in a bucket, optionally filtered by prefix, with support for pagination and folder-style navigation.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
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
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename to check |
| `Exists` | Out | Boolean | True if the object exists |

#### `Object_GetMetadata`
Retrieves an object's full metadata (size, content type, hashes, generation, storage class, timestamps) without downloading its content. Returns `Exists = False` if the object is not found, leaving `Metadata` empty.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
| `BucketName` | In | Text | Source bucket |
| `ObjectName` | In | Text | Full path/filename to inspect |
| `Exists` | Out | Boolean | True if the object was found |
| `Metadata` | Out | `GCS_ObjectMetadata` | Full metadata (only populated when `Exists` is True) |

#### `Object_Delete`
Permanently removes an object from a bucket.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `BucketName` | Text | Source bucket |
| `ObjectName` | Text | Full path/filename to delete |

#### `Object_Copy`
Copies an object to another location, within the same bucket or across buckets, without downloading its content. Overwrites the destination if it exists.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `SourceBucketName` | Text | Bucket that currently contains the object |
| `SourceObjectName` | Text | Full path/filename of the source object |
| `DestinationBucketName` | Text | Bucket to copy into (can equal the source) |
| `DestinationObjectName` | Text | Full path/filename for the destination |

#### `Object_Move`
Moves an object (copy + delete of the source), within the same bucket or across buckets. Use the same source and destination bucket to rename. Overwrites the destination if it exists.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `SourceBucketName` | Text | Bucket that currently contains the object |
| `SourceObjectName` | Text | Full path/filename of the source object |
| `DestinationBucketName` | Text | Bucket to move into (can equal the source) |
| `DestinationObjectName` | Text | Full path/filename for the destination |

> **Note:** Move is copy-then-delete and is not atomic — the source is removed only after a successful copy.

#### `Object_GetSignedUrl`
Generates a time-limited V4 signed URL for secure, direct-to-browser file access. The `Operation` controls what the URL permits.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
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
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
| `BucketList` | Out | List of `GCS_Bucket` | Project bucket metadata collection |

#### `Bucket_Create`
Creates a new globally unique storage bucket.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `BucketName` | Text | Globally unique name |
| `Location` | Text | Geographic region (e.g. `US`, `EU`, `asia-east1`) |

#### `Bucket_Exists`
Checks whether a bucket exists and is accessible to the service account, without listing its contents.

| Parameter | Direction | Type | Description |
|-----------|-----------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | In | Text | GCP credentials |
| `BucketName` | In | Text | The globally unique name of the storage bucket |
| `Exists` | Out | Boolean | True if the bucket exists and the service account can access it |

#### `Bucket_Delete`
Deletes an empty storage bucket.

| Input | Type | Description |
|-------|------|-------------|
| `ProjectId` / `ClientEmail` / `PrivateKey` | Text | GCP credentials |
| `BucketName` | Text | Name of the bucket to delete |

---

## Data Structures

### `GCS_Object`
Object metadata (list entry).
- `Name`: Text (full path)
- `Size`: Long Integer
- `ContentType`: Text
- `Updated`: Date Time (UTC)

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

## Best Practices

- **Security:** Store `PrivateKey` as an encrypted Site Property; avoid logging it.
- **Efficiency:** For large files, prefer `Object_GetSignedUrl` so uploads/downloads go directly between the browser and GCS instead of through the OutSystems server.
- **Naming:** Follow GCS bucket naming constraints (3–63 characters, lowercase letters, numbers, and hyphens).

---

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, build steps, and coding conventions.

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
