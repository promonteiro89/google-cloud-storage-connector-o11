# Changelog

All notable changes to the Google Cloud Storage Connector for OutSystems 11 are documented here. Versions follow [Semantic Versioning](https://semver.org/) and are independent of the ODC connector's versions.

## [1.6.0] - Unreleased

Keyless authentication with Workload Identity Federation, alongside the existing service account key, with one `GCS_Authentication` record per action, with exactly the same attributes as the ODC connector's `Authentication` structure. **This is a breaking release**: see Migration below.

### Breaking

- The flat `ProjectId`, `ClientEmail` and `PrivateKey` inputs were **removed from all 15 actions** and replaced by a single mandatory `Authentication` input (a `GCS_Authentication` record). Every existing call must be updated; see Migration.

### Added

- **Workload Identity Federation** (Google's recommended method for workloads outside Google Cloud). Set `AuthenticationMethod` to `WorkloadIdentityFederation` and no Google key exists anywhere: the extension obtains a JWT from **any OIDC identity provider** (Entra ID, Okta, Auth0, Keycloak, …), exchanges it with Google's Security Token Service, and impersonates a service account for a short-lived access token.
  - **Server-side, no user interaction:** tokens come from the standard OAuth 2.0 **client-credentials** grant (`TokenEndpoint`, `ClientId`, `ClientSecret`, optional `Scope` / `Audience`). It uses `client_secret_post`, with an automatic fallback to `client_secret_basic`.
  - **Bring your own token:** alternatively, pass a JWT in `SubjectToken`.
  - **Signed URLs** are signed by the service account through the IAM Credentials `signBlob` API (needs `Service Account Token Creator`).
  - Tokens are cached and refreshed automatically a minute before they expire.
- New `GCS_Authentication` structure, with the same attributes as the ODC connector's `Authentication`: `ProjectId`, `AuthenticationMethod`, `ClientEmail`, `PrivateKey`, `WorkloadIdentityProvider`, `ServiceAccountEmail`, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scope`, `Audience`, `SubjectToken`. An empty `AuthenticationMethod` means `ServiceAccountKey`.
- Actionable errors for the new failure modes: missing fields for the chosen method, a malformed provider, the identity provider rejecting the client, a non-JWT token, Google rejecting the token exchange (issuer/audience/attribute condition), and a missing Token Creator role for signing.
- Tests: validation and federation protocol contract tests (a fake identity provider, STS, IAM Credentials and Storage; every hop asserted), plus a **live** CI job that runs keyless against real Google Cloud using GitHub's OIDC token.

### Changed

- `ProjectId` is validated by `Bucket_List` and `Bucket_Create`, the only actions that use it.
- The unauthenticated / access-denied messages now name the identity actually in use and give hints for whichever method is configured.
- Clients and signers are cached per credential with method-specific keys, so a key and a federated identity can never share a cached client.

### Migration from 1.5.x and earlier

Behaviour with a service account key is unchanged; only the way credentials are passed changes.

1. Refresh the extension dependency in each consumer module.
2. In one place, build the record: for example a server action or function `GetGcsAuthentication` that returns a `GCS_Authentication` record with `ProjectId`, `ClientEmail` and `PrivateKey` taken from the Site Properties you use today, and `AuthenticationMethod` left empty.
3. On every call to the extension, set the new `Authentication` input to that record (the old `ProjectId`, `ClientEmail` and `PrivateKey` arguments are gone).
4. Republish the consumer modules.

To go keyless later, change only that one record: set `AuthenticationMethod` to `WorkloadIdentityFederation` and fill the federation fields.

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
