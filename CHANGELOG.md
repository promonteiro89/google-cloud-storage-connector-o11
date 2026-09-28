# Changelog

All notable changes to the Google Cloud Storage Connector for OutSystems 11 are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.6.0] - Unreleased

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
