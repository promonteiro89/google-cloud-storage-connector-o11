# Test Suite

Tests every action of the extension. The default run needs no Google account or credentials.

## How it works

- Integration tests run the built extension DLL against [fake-gcs-server](https://github.com/fsouza/fake-gcs-server), a local in-memory Google Cloud Storage emulator. The harness points the extension at it through the internal `EmulatorBaseUri` test seam, which is null unless a test sets it; no environment variable or server setting can redirect the extension.
- Offline tests (signed URLs, input validation, client caching) need no server: V4 signing is local RSA, done with a throwaway key generated on the first run.
- Upload integrity tests drive `Object_Upload` through a fake of Google's resumable-upload endpoint that checks the `x-goog-hash` CRC32C the way the real server does.
- Federation contract tests run Workload Identity Federation end to end against an in-process fake of every party: the identity provider's token endpoint, Google STS, IAM Credentials (`generateAccessToken` and `signBlob`) and Storage. The fake records every request, so the tests check the exact protocol. No network is used. The fake is plugged in through the extension's internal `HttpClientFactoryOverride` property, which is null unless a test sets it.
- Live tests (`-Category Live`) run in GitHub Actions against real Google Cloud: the workflow's GitHub OIDC token is exchanged through Workload Identity Federation for a sandbox service account, and real Storage and `signBlob` are called.

## Running

```powershell
# First run: downloads fake-gcs-server (~11 MB) from its GitHub releases and checks its SHA-256
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
| `Object_Upload` / `Object_Download` | Byte-exact round trips of text, binary, empty (0 bytes), 5 MB and unicode-named objects; ContentType kept; an object stored with `Content-Encoding: gzip` downloads as its original bytes with no checksum error |
| `Object_GetMetadata` | All fields (name, bucket, size, content type, hashes, generation, timestamps); missing object gives `Exists = False` |
| `Object_Exists` | Present and missing objects |
| `Object_List` | Flat listing, prefix filter, delimiter folders (`PrefixList`), prefix with delimiter, pagination through `MaxResults` / `NextPageToken` |
| `Object_Copy` / `Object_Move` / `Object_Delete` | Cross-bucket copy and move, source kept or removed; rename within a bucket; a move onto itself is refused and leaves the object and its content untouched; a missing source and a missing destination bucket fail without creating or deleting anything, and the error names the missing side (copy and move) |
| `Object_UpdateMetadata` | Header changes without re-upload, custom keys added, overwritten and removed, content unchanged; errors for nothing to change and for a missing object; the update carries both `ifGenerationMatch` and `ifMetagenerationMatch` from the read, and fails with the retry error when the content was replaced in between (offline fake, since the emulator ignores generation preconditions on updates) |
| `Object_DeleteByPrefix` | Deletes a folder and its subfolders with the right count, keeps a sibling folder with the same stem and unrelated objects, zero-match count; rejects an empty or blank prefix |
| Custom metadata | Upload with key-value metadata, read back through `CustomMetadata` |
| `Object_GetSignedUrl` | V4 URL structure, case-insensitive operation, required `BucketName` / `ObjectName`, `ContentType` signed for `Upload` and ignored for `Download` / `Delete`, rejected line breaks in `ContentType`, expiration limits (0, over 7 days, exactly 7 days) |
| Errors | Missing object and bucket, unparseable private key, negative `MaxResults` |
| Caching | `StorageClient` and `UrlSigner` reuse, separate signers per credential, key and federation never sharing a client |
| Upload integrity | Uploads send `x-goog-hash: crc32c=...` equal to an independent CRC32C of the exact bytes (reference value checked against RFC 3720), the bytes are stored unchanged, and data corrupted in transit is rejected with a "nothing was stored, retry" error and nothing is stored |
| `Authentication` record | Empty record names the missing key fields; a key in the record signs as that service account, locally; `Bucket_List` requires `ProjectId` |
| Method switch | Empty, explicit and case-insensitive `ServiceAccountKey`; invalid method; missing-field messages for both methods; the three provider formats; https-only token endpoint (plain http allowed only for loopback hosts, and only while the test override is set — rejected when unset); token endpoint URLs with user information (`user:password@`) rejected and not echoed |
| Federation protocol (fakes) | Client-credentials request, RFC 8693 STS exchange, impersonation, Storage called with the impersonated token, supplied `SubjectToken`, `client_secret_basic` fallback; errors for a rejected client, a non-JWT token, an STS rejection and a missing Token Creator role; the rejected-client and non-JWT errors surface in the top-level message (the SDK's `SubjectTokenException` wrapper is unwrapped); a redirecting token endpoint is rejected instead of followed; a non-JSON error body is summarized, never echoed; a non-string `access_token` is reported as missing; a signBlob failure other than 403 reports the status code and Google's message instead of the Token Creator hint |
| Safety (fakes) | Every action rejects an empty bucket or object name with "is required" and sends no request at all; a client-side (status 0) error gets a readable message; `Object_DeleteByPrefix` sends each listed generation as `ifGenerationMatch`, skips a replaced (412) or vanished (404) object without counting it and retries a 503; `Object_Move` sends `ifSourceGenerationMatch` and `ifGenerationMatch`, reports a changed source on the copy (no delete sent) and on the delete, and retries a 503 on either step; `Object_Copy` retries a 503 and, after a 404, names the missing source, the missing destination bucket, or both when the check itself fails; `Object_UpdateMetadata` sends a PATCH with only the changed fields and a JSON `null` for a removed key (the emulator stores that null as an empty value, so the integration test accepts either); identity provider errors never show the token endpoint's query string and have no doubled periods; a downloaded checksum mismatch is reported with a retry hint; the credential caches' 500-entry cap, kept at every insert (least recently used evicted first), 60-minute idle expiry and absolute expiry. Removing any of these safeguards makes at least one check fail |
| 1.7.3 (fakes, [RetryTests.cs](RetryTests.cs)) | `Object_Delete` sends the generation it read as `ifGenerationMatch`, retries a 503, treats a 404 on the retry as deleted, re-reads after a 412 and gives up after 3; `Object_Download` and `Object_Upload` (including the request that opens the session) retry a 503 or a network failure up to 3 attempts, waiting 1.0-1.5 s then 2.0-2.5 s, and don't retry a 403 or 404; a download over the size cap fails once with a 'larger than' error; an unclear 404 is settled by a one-object listing; `Object_GetSignedUrl` rejects a bad `Operation` before any request; `ProjectId`, `ClientEmail` and `ServiceAccountEmail` are trimmed; a non-http(s) `TokenEndpoint` is never echoed; the identity provider's error fields are cut and stripped of line breaks, and a response over 1 MB is refused, with or without a declared length |
| 1.7.4 (fakes) | A repeated `Object_Delete` answered with 412 counts as deleted without a re-read, a first 412 still re-reads, three 503s make exactly 3 requests; `Object_UpdateMetadata` says 'may already have been applied' after a repeated 412 and 'nothing was changed' after a first 412; a repeated `Object_Move` delete answered with 412 completes; an unclear 404 on a delete is settled by the listing (bucket or object); a line break in a name or `Operation` and a bidirectional override in Google's text never reach the message; `ClientSecret` is redacted from the identity provider's error (plain and URL-encoded); the production identity provider handler has cookies and redirects off and checks certificates itself; one deadline covers both client authentication attempts |
| 1.7.5 (fakes, [HardeningTests.cs](HardeningTests.cs)) | A 408 or 429 followed by a 412 is not read as "already applied" by `Object_Delete`, `Object_Move` and `Object_UpdateMetadata`, while a 503 followed by a 412 still is; a malformed bucket name (line break, U+202E, uppercase, 223 characters, a trailing line feed) is rejected by all 13 actions that take one, with a clean message and no request; `ContentType` accepts all 95 printable ASCII characters and nothing else; an assembly-load error is not retried or called a network error; every transport handler of the production Google HTTP factory checks the certificate; a key sign-in goes through the test seam with a JWT signed by the key; `Bucket_Delete` and `Bucket_Create` repeats (gone or created by the first send, refused sends, a bucket that existed before or belongs to someone else); plain-text error bodies, a page in place of Google's answer, and the 429 label; `Object_Download` reads the size first (refused over the limit with no content request, wrong or gzip sizes still download in full, 3 MB in one request); a stalled download body fails after the stall limit and closes the connection, and a slow one completes; one reset connection is repeated on every action, and a listing repeats only the failed page; each of 408, 429, 500, 502, 503 and 504 is repeated on download and upload; `Object_DeleteByPrefix` counts an object its own repeated delete removed; `signBlob` is repeated; a `SubjectToken` that isn't a JWT or is too long is rejected before anything is cached; `ServiceAccountEmail` with URL characters is rejected; the `ClientSecret` is masked in the form it was sent; a refused impersonation names the role; 12 parallel first calls make one token exchange; an identity provider that answers after 13 seconds still signs in (this check takes 13 seconds); a stall-timer callback acts only on a read that is pending and has waited the whole limit; a gzip object is never refused by its stored size; the HTTP Basic credentials, a secret in a non-JSON answer's `Content-Type` and a token repeated by Google's token service are masked; the 30-second allowance of the `SubjectToken` expiry check; the production Storage client has the stall guard directly under Google's handler. Each fix was removed in a copy of the code to confirm that a check then fails |
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

The harness files ([TestHarness.cs](TestHarness.cs), [FederationTests.cs](FederationTests.cs), [SafetyTests.cs](SafetyTests.cs), [RetryTests.cs](RetryTests.cs), [HardeningTests.cs](HardeningTests.cs), [LiveFederationTests.cs](LiveFederationTests.cs)) are compiled when the tests run, against the built extension DLL, and call the same `Mss*` methods the OutSystems platform calls.
