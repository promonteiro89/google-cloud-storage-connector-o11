# Test Suite

Tests every action of the extension. The default run needs no Google account or credentials.

## How it works

- Integration tests run the built extension DLL against [fake-gcs-server](https://github.com/fsouza/fake-gcs-server), a local in-memory Google Cloud Storage emulator. The extension talks to it only when the `GCSCONNECTOR_EMULATOR_HOST` environment variable is set, which never happens on an OutSystems server.
- Offline tests (signed URLs, input validation, client caching) need no server: V4 signing is local RSA, done with a throwaway key generated on the first run.
- Upload integrity tests drive `Object_Upload` through a fake of Google's resumable-upload endpoint that checks the `x-goog-hash` CRC32C the way the real server does.
- Federation contract tests run Workload Identity Federation end to end against an in-process fake of every party: the identity provider's token endpoint, Google STS, IAM Credentials (`generateAccessToken` and `signBlob`) and Storage. The fake records every request, so the tests check the exact protocol. No network is used. The fake is plugged in through the extension's internal `HttpClientFactoryOverride` property, which is null unless a test sets it.
- Live tests (`-Category Live`) run in GitHub Actions against real Google Cloud: the workflow's GitHub OIDC token is exchanged through Workload Identity Federation for a sandbox service account, and real Storage and `signBlob` are called.

## Running

```powershell
# First run: downloads fake-gcs-server (~11 MB) from its GitHub releases
.\run-tests.ps1 -Download

# Later runs
.\run-tests.ps1

# Offline and contract tests only (no emulator, no downloads)
.\run-tests.ps1 -SkipEmulator

# Live tests (in GitHub Actions; elsewhere they are reported as SKIPPED)
.\run-tests.ps1 -Category Live
```

`-Category Default` (the default) runs everything except the live tests. `-Category Live` runs only the live tests and doesn't start the emulator.

Requirements:
- the extension built at `Source\NET\Bin\OutSystems.NssGoogleCloudStorage_ext.dll` (see the repo README for the OutSystems platform assemblies);
- Windows PowerShell 5.1, which runs on .NET Framework 4.8 like the extension;
- `openssl` on the PATH, or Git for Windows, to generate the throwaway key once.

## Coverage

| Area | What's checked |
|---|---|
| `Bucket_Create` / `Bucket_Exists` / `Bucket_List` / `Bucket_Delete` | Full lifecycle, duplicate-name error, non-empty-delete error, timestamps |
| `Object_Upload` / `Object_Download` | Byte-exact round trips of text, binary, empty (0 bytes), 5 MB and unicode-named objects; ContentType kept |
| `Object_GetMetadata` | All fields (name, bucket, size, content type, hashes, generation, timestamps); missing object gives `Exists = False` |
| `Object_Exists` | Present and missing objects |
| `Object_List` | Flat listing, prefix filter, delimiter folders (`PrefixList`), prefix with delimiter, pagination through `MaxResults` / `NextPageToken` |
| `Object_Copy` / `Object_Move` / `Object_Delete` | Cross-bucket copy and move, source kept or removed |
| `Object_UpdateMetadata` | Header changes without re-upload, custom keys added, overwritten and removed, content unchanged; errors for nothing to change and for a missing object |
| `Object_DeleteByPrefix` | Deletes a folder and its subfolders with the right count, keeps a sibling folder with the same stem and unrelated objects, zero-match count; rejects an empty or blank prefix |
| Custom metadata | Upload with key-value metadata, read back through `CustomMetadata` |
| `Object_GetSignedUrl` | V4 URL structure, case-insensitive operation, ContentType in the signature, expiration limits (0, over 7 days, exactly 7 days) |
| Errors | Missing object and bucket, unparseable private key, negative `MaxResults` |
| Caching | `StorageClient` and `UrlSigner` reuse, separate signers per credential, key and federation never sharing a client |
| Upload integrity | Uploads send `x-goog-hash: crc32c=...` equal to an independent CRC32C of the exact bytes (reference value checked against RFC 3720), the bytes are stored unchanged, and data corrupted in transit is rejected with a "nothing was stored, retry" error and nothing is stored |
| `Authentication` record | Empty record names the missing key fields; a key in the record signs as that service account, locally; `Bucket_List` requires `ProjectId` |
| Method switch | Empty, explicit and case-insensitive `ServiceAccountKey`; invalid method; missing-field messages for both methods; the three provider formats; https-only token endpoint |
| Federation protocol (fakes) | Client-credentials request, RFC 8693 STS exchange, impersonation, Storage called with the impersonated token, supplied `SubjectToken`, `client_secret_basic` fallback; errors for a rejected client, a non-JWT token, an STS rejection and a missing Token Creator role |
| Federation live (CI) | Real Google Cloud through GitHub OIDC: Storage accepts the federated identity, and the signed URL's signature verifies against the service account's published certificates |

## The live job

The `live-federation` CI job runs the federation chain against real Google Cloud. No Google credential exists for it:

1. The job has `permissions: id-token: write`, so GitHub issues an OIDC token for the run. The test requests it from `ACTIONS_ID_TOKEN_REQUEST_URL`, with the workload identity provider as the audience.
2. The token goes into the `Authentication` record as `SubjectToken`, with `AuthenticationMethod = WorkloadIdentityFederation`. The extension exchanges it at Google STS and impersonates the sandbox service account.
3. Google accepts it because the sandbox provider trusts GitHub's issuer and its attribute condition allows this repository.

`GCP_WIF_PROVIDER`, `GCP_SERVICE_ACCOUNT` and `GCP_PROJECT_ID` are repository variables, not secrets. The sandbox service account has no keys and no project roles, so a non-existent bucket returns 404, which shows the chain works, and there is no data to expose. The job's only secret is the read-only deploy key that fetches the OutSystems assemblies needed to compile.

The live checks:
- `Bucket_Exists` on a random, non-existent bucket returns `False`. A 404 means the chain worked; a broken chain gives 401 or 403.
- `Object_GetSignedUrl` puts the service account in `X-Goog-Credential`, and the signature verifies against the service account's public certificates from `https://www.googleapis.com/service_accounts/v1/metadata/x509/{sa}`. The verifier rebuilds the V4 canonical request and string-to-sign from the URL and checks the RSA-SHA256 signature. The default run tests the verifier itself: a genuine URL verifies and a tampered one doesn't.

When `ACTIONS_ID_TOKEN_REQUEST_URL`, `ACTIONS_ID_TOKEN_REQUEST_TOKEN`, `GCP_WIF_PROVIDER` or `GCP_SERVICE_ACCOUNT` is missing, the live checks are reported as SKIPPED, never as passed. In CI, the "Check live configuration" step fails before that can happen, so a green `live-federation` job means the live checks ran.

## Limits

The harness loads the Google assemblies through a resolver that ignores versions, so it doesn't test the OutSystems runtime's own assembly binding. Only a call from a published app does.

The harness files ([TestHarness.cs](TestHarness.cs), [FederationTests.cs](FederationTests.cs), [LiveFederationTests.cs](LiveFederationTests.cs)) are compiled when the tests run, against the built extension DLL, and call the same `Mss*` methods the OutSystems platform calls.
