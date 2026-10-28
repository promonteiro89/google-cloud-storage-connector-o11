# Changelog

All notable changes to the Google Cloud Storage Connector for OutSystems 11 are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.7.0] - 2026-10-28

Security and reliability release. No action, input or output changes: a drop-in replacement for 1.6.1. Some invalid calls that used to fail late, or succeed with a wrong result, are now rejected up front (see Security and Fixed).

### Security

- Workload Identity Federation with a caller-supplied `SubjectToken`: each token now gets its own cached credentials. Before, all callers with the same provider and service account shared one cached credential ("latest token wins"), so a caller with an expired or wrong token could reuse another caller's access token for up to an hour, and a refresh could exchange another caller's token.
- A supplied `SubjectToken` whose `exp` has passed is rejected before anything is sent to Google, and cached credentials built from a supplied token expire at the token's `exp`.
- `TokenEndpoint` must be `https://` (plain `http://` to `localhost` was accepted before) and must not contain a `user:password@` part. The identity provider client no longer follows redirects, so the client secret is never sent to a redirect target; a redirect is reported with a clear error.
- Identity provider error responses are no longer echoed into error messages: only the OAuth `error` / `error_description` fields are shown, and a non-JSON body is summarized by its size and content type.
- `Object_GetSignedUrl` refuses an empty `ObjectName` (it would have signed a URL for the whole bucket) and a `ContentType` with line breaks.

### Fixed

- `Object_Exists` and `Object_GetMetadata` returned an error instead of `Exists = False` for a missing object in a bucket whose name contains "bucket" (e.g. `acme-bucket`): Google's "No such object: acme-bucket/..." message was mistaken for a missing bucket. Other actions reported the wrong cause for the same reason.
- `Object_Move` onto the same object (same bucket and name) deleted the object: the copy succeeded and the following delete removed the only copy. It is now refused.
- `Object_Move` is guarded by the source's generation: a source replaced by another writer during the move is reported instead of being deleted, a source already gone after the copy is no longer an error, and a missing destination bucket is named as such.
- `Object_GetSignedUrl` signs the `ContentType` only for `Upload` URLs, so `Download` and `Delete` URLs no longer fail with `SignatureDoesNotMatch` when a browser sends no such header.
- `Object_Download` of objects stored with `Content-Encoding: gzip` could fail the integrity check on valid data (Google serves them decompressed); the check is now skipped only in that case.
- Identity provider failures (wrong `ClientSecret`, a non-JWT token) were hidden behind the SDK's generic "error occurred while attempting to obtain the subject token" message. Their actionable message is now the error's top-level message.
- Network failures and timeouts now give a clear message instead of a raw .NET exception, and a timeout of the identity provider is no longer reported as "could not reach".
- `Object_GetSignedUrl` with Workload Identity Federation suggests the "Service Account Token Creator" role only when Google answers 403; other failures show the status and Google's message.
- Text outputs (`GCS_ObjectMetadata`, `GCS_Object`, `GCS_Bucket`, `Object_Download.ContentType`) are empty instead of null when Google omits a field.
- `Bucket_Create` with an empty `Location` uses Google's default location instead of sending an empty one; `Object_Upload` accepts empty content and an empty `ContentType`.
- `Object_UpdateMetadata` reports a concurrent metadata change (412) with a "retry the update" message.
- `Object_DeleteByPrefix` reports the cause of a mid-run failure with the same message as the single-object actions.

### Changed

- Credential caches expire after 60 minutes without use and hold at most about 500 entries each, so rotated keys and per-user tokens no longer accumulate. A service account key is parsed once and shared by Storage calls and signed URLs. Cache keys length-prefix every credential part before hashing.
- Identity provider tokens are cached until shortly before the earlier of `expires_in` and the token's own `exp`.

## [1.6.1] - 2026-10-09

Maintenance release: end-to-end upload integrity. No changes to actions, inputs or outputs; a drop-in replacement for 1.6.0.

### Changed

- `Google.Cloud.Storage.V1` 4.15.0 → 5.0.0, the minimum .NET version in Google's announcement that Cloud Storage client libraries validate upload checksums end to end by default. `Object_Upload` now sends the CRC32C checksum of the exact bytes with the upload, and Google rejects a mismatch before storing the object. Before, the check ran after the upload: a corrupted object was stored and then deleted, which could also lose the previous version when overwriting.
- Other Google libraries: `Google.Apis` / `Google.Apis.Auth` / `Google.Apis.Core` 1.76.0 → 1.77.0, `Google.Apis.Storage.v1` 1.76.0.4250 → 1.77.0.4290, `Google.Api.Gax` / `Google.Api.Gax.Rest` 4.15.0 → 4.15.1.
- Other bundled libraries, to their latest releases: `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Extensions.DependencyInjection.Abstractions` and `System.Diagnostics.DiagnosticSource` 10.0.6 → 10.0.12, and `Newtonsoft.Json` 13.0.3 → 13.0.4 (assembly version unchanged at 13.0.0.0). Every bundled package is now at its latest stable version.

### Added

- A clear error when Google rejects an upload for a checksum mismatch: it says nothing was stored, any existing object is unchanged, and the upload can be retried.
- Upload integrity tests: the checksum sent matches an independent CRC32C of the content, and corrupted data is rejected with that error and not stored. Both checks fail against 4.15.0.

### Documentation

- Signed URL uploads go directly from the client to Google, so the automatic checksum applies only to `Object_Upload`; the README points to Google's data validation guide for direct uploads.

## [1.6.0] - 2026-09-28

Adds keyless authentication with Workload Identity Federation next to service account keys, with one `GCS_Authentication` record per action. This release is breaking; see Migration.

### Breaking

- The `ProjectId`, `ClientEmail` and `PrivateKey` inputs are removed from all 15 actions and replaced by one mandatory `Authentication` input (a `GCS_Authentication` record). Every existing call must be updated.

### Added

- Workload Identity Federation, Google's recommended method for workloads outside Google Cloud. With `AuthenticationMethod` set to `WorkloadIdentityFederation`, no Google key is used: the extension gets a JWT from an OIDC identity provider (Entra ID, Okta, Auth0, Keycloak and others), exchanges it with Google's Security Token Service, and impersonates a service account for a short-lived access token.
  - The JWT comes from the OAuth 2.0 client-credentials grant (`TokenEndpoint`, `ClientId`, `ClientSecret`, optional `Scope` / `Audience`), so no user interaction is needed. It uses `client_secret_post` and falls back to `client_secret_basic`.
  - Or pass a JWT you already have in `SubjectToken`.
  - Signed URLs are signed by the service account through the IAM Credentials `signBlob` API, which needs `Service Account Token Creator`.
  - Tokens are cached and refreshed a minute before they expire.
- `GCS_Authentication` structure: `ProjectId`, `AuthenticationMethod`, `ClientEmail`, `PrivateKey`, `WorkloadIdentityProvider`, `ServiceAccountEmail`, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope`, `Audience`, `SubjectToken`. An empty `AuthenticationMethod` means `ServiceAccountKey`.
- Error messages for the new failure cases: missing fields for the chosen method, a malformed provider, the identity provider rejecting the client, a non-JWT token, Google rejecting the token exchange (issuer, audience or attribute condition), and a missing Token Creator role when signing.
- Tests: contract tests of the federation protocol against a fake identity provider, STS, IAM Credentials and Storage, and a CI job that runs the chain against real Google Cloud with GitHub's OIDC token.

### Changed

- `Bucket_List` and `Bucket_Create`, the only actions that use `ProjectId`, now check that it is set.
- Access-denied and unauthenticated errors name the identity in use and give hints for the configured method.
- Clients and signers are cached per credential with a key per method, so a key and a federated identity never share a client.

### Migration from 1.5.x and earlier

Behaviour with a service account key doesn't change; only the way credentials are passed does.

1. Refresh the extension dependency in each consumer module.
2. Build the record in one place, for example a server action `GetGcsAuthentication` that returns a `GCS_Authentication` record with `ProjectId`, `ClientEmail` and `PrivateKey` from the Site Properties you use today and `AuthenticationMethod` empty.
3. On every call to the extension, set the `Authentication` input to that record. The old `ProjectId`, `ClientEmail` and `PrivateKey` arguments no longer exist.
4. Republish the consumer modules.

To switch to Workload Identity Federation later, change only that record: set `AuthenticationMethod` to `WorkloadIdentityFederation` and fill in the federation fields.

## [1.5.2] - 2026-09-01

Maintenance release. No changes to actions, inputs or outputs.

### Changed

- Google.Apis / Google.Apis.Auth / Google.Apis.Core 1.74.0 → 1.76.0; Google.Apis.Storage.v1 1.74.0.4161 → 1.76.0.4250; Google.Api.Gax / Google.Api.Gax.Rest 4.14.0 → 4.15.0. Google.Cloud.Storage.V1 stays at 4.15.0.

### Fixed

- The NuGet update watch now checks every bundled `Google.*` package, not only `Google.Cloud.Storage.V1`.
- CI now builds the extension and runs the full test suite on every pull request.

## [1.5.0] - 2026-07-18

### Added

- Custom object metadata: optional `Metadata` input on `Object_Upload`, `CustomMetadata` output on `Object_GetMetadata`, and the `GCS_MetadataEntry` structure.
- `Object_UpdateMetadata`: change content headers and custom metadata without re-uploading, guarded by a metageneration precondition.
- `Object_DeleteByPrefix`: delete a "folder" server-side and return the count; the prefix is mandatory and non-empty.
- Monthly NuGet update watch and a CI badge.

## [1.4.0] - 2026-07-12

### Added

- `Object_List` pagination (`MaxResults`, `PageToken`, `NextPageToken`) and folder navigation (`Delimiter`, `PrefixList`), with the `GCS_Prefix` structure.
- Optional `ContentType` input on `Object_GetSignedUrl`, bound into the V4 signature.
- `Bucket_Exists`.
- Full test suite against the fake-gcs-server emulator, and CI.

### Changed

- Actionable error messages across all actions; `ExpirationMinutes` and `MaxResults` validation; clearer `Object_Move` partial-failure reporting; defensive timestamp parsing.

## [1.3.1] - 2026-07-12

### Changed

- `StorageClient` and `UrlSigner` are cached and reused per service account.
- Google.Cloud.Storage.V1 4.15.0, Google.Apis* 1.74.0, Google.Api.Gax* 4.14.0.

### Fixed

- `Object_List` sizes above 2 GB; a missing Newtonsoft.Json dependency; a `System.ValueTuple` version-conflict build warning.
