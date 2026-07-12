# Google Cloud Storage Connector for OutSystems 11

An OutSystems 11 (.NET) extension that exposes Google Cloud Storage operations to OutSystems applications: bucket management, object upload/download, listing, copy/move, existence checks, metadata retrieval, and signed URL generation.

Authentication is via a Google Cloud service account (`client_email` + `private_key` from the service account JSON key), passed as inputs to each action.

## Actions

| Action | Description |
|---|---|
| `Bucket_List` | Lists all buckets in a project |
| `Bucket_Create` | Creates a bucket in a given location |
| `Bucket_Delete` | Deletes an (empty) bucket |
| `Object_Upload` | Uploads binary content to a bucket |
| `Object_Download` | Downloads an object's content |
| `Object_List` | Lists objects in a bucket, optionally filtered by prefix |
| `Object_Delete` | Deletes an object |
| `Object_Exists` | Checks whether an object exists |
| `Object_GetMetadata` | Retrieves an object's metadata without downloading it |
| `Object_Copy` | Copies an object within or across buckets |
| `Object_Move` | Moves (copy + delete) an object within or across buckets |
| `Object_GetSignedUrl` | Generates a time-limited signed URL for Download/Upload/Delete |

## Project structure

```
Source/NET/                  Integration Studio project (the extension itself)
  GoogleCloudStorage_ext.cs  Action implementations
  Interface.cs               Action signatures (IssGoogleCloudStorage_ext)
  Structures.cs / Records.cs / RecordLists.cs / Entities.cs   Generated OutSystems data types
  GoogleCloudStorage_ext.csproj
  Bin/                       Compiled output + referenced assemblies (see below)
```

There is no `packages.config` / `PackageReference` — Integration Studio requires referenced assemblies to exist as plain DLLs in `Bin\`, referenced via `<HintPath>`.

## Building

**Prerequisites**
- OutSystems Integration Studio (matching your O11 environment version)
- .NET Framework 4.8 targeting pack

**Getting the dependencies**

Third-party NuGet DLLs (`Google.*`, `Newtonsoft.Json`, `System.*`, `Microsoft.*`) are committed in `Source/NET/Bin/` so the project builds out of the box.

The **OutSystems platform assemblies** are *not* included (they're proprietary and ship with Integration Studio itself). Before opening the project, copy these into `Source/NET/Bin/` from your own Integration Studio installation, or from a `Bin\` folder of any existing extension in your environment:

- `OutSystems.RuntimeCommon.dll`
- `OutSystems.HubEdition.RuntimePlatform.dll`
- `OutSystems.HubEdition.DatabaseAbstractionLayer.dll`
- `OutSystems.REST.API.dll`
- `OutSystems.SOAP.API.dll`
- `OutSystems.SAP.API.dll`

Then open `Source/NET/GoogleCloudStorage_ext.sln` in Integration Studio and use **1-Click Publish** to compile and publish to your OutSystems environment.

## Dependencies (current versions)

| Package | Version |
|---|---|
| Google.Cloud.Storage.V1 | 4.15.0 |
| Google.Api.Gax / Google.Api.Gax.Rest | 4.14.0 |
| Google.Apis / Google.Apis.Auth / Google.Apis.Core | 1.74.0 |
| Google.Apis.Storage.v1 | 1.74.0.4161 |
| Newtonsoft.Json | 13.0.3 |
| System.ValueTuple | 4.0.5.0 |

## License

TBD.
