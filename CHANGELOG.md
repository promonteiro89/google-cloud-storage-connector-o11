# Changelog

All notable changes to the Google Cloud Storage Connector for OutSystems 11 are documented here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.7.5] - 2026-11-10

Hardening release from five independent reviews of 1.7.4 and from the review of the ODC connector, whose findings were checked against this code. No action, input or output changes: a drop-in replacement for 1.7.4. Some invalid inputs that used to reach Google, or fail deep inside .NET, are now rejected up front (see "Input checks"), and `Object_Download` makes one extra request.

### Fixed

- `Object_Delete` and `Object_Move` could report success for an object that was not deleted. 1.7.4 treats a 412 on a repeated request as "the first request already deleted it". That is only true when the first request may have been applied: a lost answer, or a 500, 502, 503 or 504. After a 408 or 429 Google refused the request, so the 412 means another writer replaced the object. In that case `Object_Delete` now reads the new generation and deletes it, `Object_Move` reports that the source was modified and both objects exist, and `Object_UpdateMetadata` says "nothing was changed" instead of "may already have been applied".
- `Bucket_Delete` and `Bucket_Create` failed when the Storage library repeated a request that Google had already applied: the repeat was answered "does not exist" or "name already taken". The extension now repeats these requests itself. A repeated delete that finds the bucket gone succeeds. A repeated create that is told the name is taken succeeds when the bucket is readable and was created after the call started (a creation time up to one minute earlier is accepted, since Google's clock and the server's can differ); otherwise it is a conflict, as before.
- Network failures (a reset connection, DNS, TLS) were repeated only by `Object_Upload`, `Object_Download` and the guarded deletes. The Storage library repeats Google's error answers but not these, so one reset connection failed `Object_Exists`, `Object_GetMetadata`, `Object_List`, `Object_Copy`, `Bucket_Exists`, `Bucket_List`, the reads inside `Object_Delete`, `Object_Move` and `Object_UpdateMetadata`, and a whole `Object_DeleteByPrefix`. They are now repeated up to 3 times on every action. A listing of several pages repeats only the page that failed.
- `Object_DeleteByPrefix` did not count an object whose repeated delete found it already gone, although this call had deleted it. It is now counted.
- `Object_GetSignedUrl` with Workload Identity Federation failed on one network failure, 408 or 429 of the IAM `signBlob` request. Those are now repeated, up to 3 attempts. Google's library already repeated a 500, 502, 503 or 504 there.
- A download whose data stopped arriving held the calling request until the platform's own timeout, because the 100-second limit covers a request only until the response headers arrive. `Object_Download` now fails with a "stalled" error when no data arrives for 60 seconds. This is not a limit on the whole download: a slow download that keeps receiving data completes.
- A slow identity provider failed after about 12 seconds with "A task was canceled" instead of the documented 30-second limit. Google's sign-in code allows its first attempt 12 seconds and treated the cancelled request as final. The identity provider request now runs on its own 30-second deadline, and a token that arrives after Google's first attempt gave up is used by its second.
- An assembly that could not be loaded (a `FileNotFoundException` or `FileLoadException` for a Google or `System.*` DLL) was repeated three times and reported as "The connection to Google Cloud was interrupted". Those are no longer treated as network errors, so the real cause reaches the error log.
- A malformed bucket name (uppercase, a space, a line break) was repeated as given in a raw library error, on every action, so a line break or a bidirectional-override character in it could forge or reorder lines in the error log; a name ending in a line feed passed the library's own check and reached a signed URL. Bucket names are now checked before any request.
- If the identity provider repeated the `ClientSecret` in exactly the form it was sent (URL-escaped, with spaces as `+`), it appeared in the error message: for a secret with a space and one of `! * ( )`, that form differs from the two masked since 1.7.4. It is now masked too, and so are the HTTP Basic credentials (the client id and secret in Base64) and a secret repeated in the `Content-Type` of a non-JSON answer.
- If Google's token service repeated the `SubjectToken`, or the identity provider's token, in its error, the token appeared in the error message. A JWT in Google's error text is now replaced by `[token]`.
- Google's error text was lost when the body was not JSON (a plain-text answer, a proxy page): the message said "No error message was specified", and a missing bucket could be reported as a missing object. The start of such a body is now used.
- A successful answer that was not Google's (a proxy or sign-in page with status 200) was reported as "rejected by the Google Cloud Storage client library before it was sent". It now says that something answered in Google's place and to check outbound HTTPS access.
- A 429 was shown as "Google Cloud Storage error (429 429)". It is now "(429 TooManyRequests)".
- `Object_Move`'s "modified during the move" error put the source's generation after the destination's name. It now names it as the source's.
- The connections to Google now check the server certificate themselves, as the identity provider connection does since 1.7.4, so a server-wide "accept any certificate" setting made by another component doesn't apply to them.
- The message cleaner now also removes the direction marks U+200E, U+200F and U+061C.

### Input checks

- `BucketName`: only the characters Google allows (lowercase letters, digits, `-`, `_`, `.`), up to 222 characters. Before, such a name failed later with a raw error.
- `ContentType` on `Object_Upload` and `Object_GetSignedUrl`: printable ASCII only. A non-ASCII or control character used to fail inside .NET, be repeated three times and be reported as "Could not reach Google Cloud".
- `SubjectToken`: must look like a JWT (three base64url parts separated by dots, at most 32,768 characters). Each distinct token takes an entry in the credential caches, so arbitrary text is now rejected before anything is cached; it was rejected by Google before, after being cached.
- `ServiceAccountEmail`: letters, digits and `@ . - _ +` only, since it becomes part of the IAM Credentials URL.

### Changed

- `Object_Download` reads the object's size first, which costs one extra request. An object over the 2 GB limit is refused before any content is transferred, and the memory for the content is allocated once at its final size instead of growing by doubling and then being copied. The size is only a hint (an object replaced in between still downloads correctly, and an object stored gzip-compressed is never refused by its stored size), and the content type still comes from the download itself. The read buffer of a download is 1 MB instead of the library's 10 MB.

### Documentation

- With Workload Identity Federation, a signed URL should not be valid for more than 12 hours: Google signs it with a key it manages and rotates. The action still accepts up to 7 days with both methods.
- Signed URLs: the bucket's CORS configuration is needed for browser use, and an `Upload` URL doesn't limit the upload's size.
- A Site Property is not encrypted; the advice to store secrets in an "encrypted Site Property" is corrected.
- A download of an object stored with `Content-Encoding: gzip` is not checked against its checksum.
- `Object_List` with `MaxResults` 0 has no limit on time or memory; the actions have no overall time limit, with the cases that can run long.
- `Bucket_Create` uses Google's default settings (no uniform bucket-level access, no public access prevention).
- The publish steps open the `.xif`, not the `.sln`.
- Action descriptions in Service Studio: `Object_Delete` no longer says "permanently", `Object_GetSignedUrl` describes all three operations, its `Operation` says that it defaults to `Download` when not set (it said "Required"), and `Bucket_Exists` says that an inaccessible bucket raises an error.
- New [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/` for the bundled assemblies.

### CI

- The deploy key for the OutSystems assemblies is no longer in the environment of every step: the jobs only see whether it exists. Checkouts no longer leave their credentials on the runner.
- The test runner checks the SHA-256 of the fake-gcs-server download before extracting it.

### Tests

- New offline tests ([tests/HardeningTests.cs](tests/HardeningTests.cs)) for each change above, including: a 408 or 429 followed by a 412 on `Object_Delete`, `Object_Move` and `Object_UpdateMetadata`; all 13 actions rejecting a bucket name with a line break without sending a request; a stalled download body and a slow one; an identity provider that answers after 13 seconds; one reset connection on each action; each of 408, 429, 500, 502, 503 and 504 on download and upload; 12 parallel first calls with one credential; and a key sign-in through the test seam.
- Each fix was removed in a copy of the code to confirm that at least one test then fails.
- Two independent reviews of this release's diff found eight further points, fixed above: the one-minute clock allowance in `Bucket_Create`, a stall timer that could act on a read that had already finished, a gzip object refused by its stored size, the HTTP Basic credentials, the `Content-Type` text and an echoed token in error messages, and two README statements. Each has a test.
- Five emulator checks that could not fail now check the error message.

## [1.7.4] - 2026-11-09

Hardening release from four independent reviews of 1.7.3. No action, input or output changes: a drop-in replacement for 1.7.3.

### Fixed

- `Object_Delete` could delete a newer object. When Google applied the delete but its answer was lost, the Storage library repeated the request; if another writer had replaced the object in that second, the repeat got a 412 and the action read and deleted the new object too. The extension now repeats the delete itself, with the library's retry turned off, and a 412 on a repeated request counts as deleted. A 412 on the first request still means the object was replaced before the delete, which reads the new generation and deletes it, as in 1.7.3.
- For the same reason, `Object_UpdateMetadata` said "nothing was changed" when a repeated update got a 412, although the first request may have been applied. It now says the update "may already have been applied" and asks to read the metadata before retrying. `Object_Move` no longer reports a failed source delete in that case: the source was deleted.
- Error messages repeated caller input (object names, prefixes, `Operation`, `AuthenticationMethod`) and Google's error text as they were, so a line break or a bidirectional-override character in a name could forge or reorder lines in the OutSystems error log. Both are now cleaned the way the identity provider's text is since 1.7.3.
- If the identity provider repeated the configured `ClientSecret` in its `error` or `error_description` (plain or URL-encoded), the secret appeared in the error message. It is now replaced by `[redacted]`.
- The identity provider connection is shared by every app on the server and kept cookies, so cookies the provider set for one app were sent with another app's token requests. Cookies are now off for it.
- The 30-second limit on the identity provider applied separately to the response headers and the body, so one token request could take about 60 seconds, and about 120 with the HTTP Basic retry. One 30-second deadline now covers the whole exchange.
- The request that sends `ClientSecret` now checks the provider's certificate itself, so a server-wide "accept any certificate" setting made by another component doesn't apply to it.
- With an emulator or a proxy that answers a plain "Not Found", `Object_Delete`, `Object_Download`, `Object_UpdateMetadata`, `Object_Move` and `Object_Copy` reported a missing bucket as a missing object. An unclear 404 is now settled by listing at most one object in the bucket, as `Object_Exists` and `Object_GetMetadata` already did.

### Tests

- New offline tests for each change: a log-forging name and `Operation`, a Google 404 with a bidirectional override and a line break, a secret in the identity provider's error (plain and URL-encoded), the production identity provider handler's settings, one deadline across both client authentication attempts, a repeated delete answered with 412 (no re-read, no second generation deleted), three 503s on a delete (exactly 3 requests), the two `Object_UpdateMetadata` 412 messages, a repeated `Object_Move` delete, and an unclear 404 settled by the listing either way.

## [1.7.3] - 2026-11-03

Reliability and hardening release from two independent reviews of 1.7.2 (this repository's and the ODC connector's). No action, input or output changes: a drop-in replacement for 1.7.2.

### Fixed

- `Object_Delete`, `Object_Download` and `Object_Upload` failed on a single transient Google error (a 503, a 429, a dropped connection), while the other actions are retried. Now:
  - `Object_Delete` reads the object's generation first and deletes that generation, which lets the Storage library retry it; this costs one extra request and uses `storage.objects.get`, which `Storage Object Admin` includes. If the object is replaced between the read and the delete, the new generation is read and the delete repeated, up to 3 times, so, as before, the action removes whatever object has the name. A delete whose retry finds the object already gone succeeds. Deleting a missing object is still an error. A role that can delete but not read objects still works: the object is then deleted without the read, and without retries, as in 1.7.2.
  - `Object_Download` and `Object_Upload` repeat the whole call, up to 3 attempts, waiting about 1 and then 2 seconds, on 408, 429, 500, 502, 503 and 504 answers, network failures, broken or corrupted transfers, and (upload) a CRC32C mismatch. The Storage library has no retry setting for downloads, and doesn't retry the request that opens an upload. A request timeout is not repeated, since it already waited 100 seconds; it now gets the "timed out" message instead of a raw "One or more errors occurred."
- `Object_Exists` and `Object_GetMetadata` told a missing bucket from a missing object only by Google's English error text. A 404 with other wording (an emulator, a proxy, a future change) made them return False for a missing bucket, where an error is documented. Unclear 404s are now checked by listing at most one object in the bucket.
- A `ContentType` with a line break made `Object_Upload` fail with a raw .NET `FormatException`. It is now rejected up front with "ContentType must not contain line breaks.", as `Object_GetSignedUrl` already did.
- `Authentication.ProjectId`, `ClientEmail` and `ServiceAccountEmail` are trimmed. A trailing space or line break, for example from a Site Property, broke sign-in or the bucket actions with an unclear error.
- `Object_GetSignedUrl` checks `Operation` before the `Authentication` fields, so a typo such as "downloads" is reported as such even when the credentials are also misconfigured.
- A download larger than about 2 GB failed with "connection interrupted ... retry", which can never succeed. It now fails without retrying, says the object is too large for `Object_Download`, and points to `Object_GetSignedUrl`. In practice the server may run out of memory well below 2 GB, since the content is held in memory; use signed URLs for large objects.
- A `TokenEndpoint` that isn't an `http(s)://` URL, such as `host/path?secret=...` or `user:password@host`, was echoed in full in the error message. Such values are no longer echoed.
- The identity provider's `error` and `error_description` fields are cut to 100 and 300 characters and stripped of line breaks, other control characters and bidirectional overrides before they reach an error message, so they can't flood or forge lines in the error log.
- The identity provider's response is read up to 1 MB, and within 30 seconds, instead of buffering whatever the endpoint sends. Its charset and a leading byte-order mark are handled as before.
- Each credential cache holds at most 500 entries at all times. Before, the limit was applied only by the once-a-minute cleanup, so many new identities within a minute could grow it past the limit.

### Documentation

- `Object_Delete` uses `storage.objects.get` as well as `storage.objects.delete` (see above).
- `Bucket_Exists` needs the `storage.buckets.get` permission, which `Storage Object Admin` doesn't include.
- `Object_DeleteByPrefix` lists every matching object first, then deletes them one at a time, so a very large prefix can outlast the action's timeout; run it again to continue. Its count can be one lower than the objects removed when a retried delete finds its object already gone.

### CI

- The build job's token is read-only (`permissions: contents: read`), and every GitHub Action is pinned to a commit SHA instead of a movable tag.

### Tests

- New offline tests for each change above, and two emulator tests: a gzip-encoded object downloads as its original bytes, and `Object_Exists` in a missing bucket raises the bucket error.

## [1.7.2] - 2026-11-03

Reliability release from an independent review of 1.7.1. No action, input or output changes: a drop-in replacement for 1.7.1. Some invalid calls that used to reach Google are now rejected up front.

### Changed

- Every action rejects an empty bucket or object name before anything is sent, with "<Input> is required". `Object_Upload` and `Object_Download` used to send a request with an empty object name, and the other actions failed with an unreadable "Google Cloud Storage error (0 0): Parameter validation failed" (`Object_Move` only after reading the source).
- `Object_UpdateMetadata` sends a PATCH with only the changed fields, instead of writing the whole object back. A full write could reset per-object access control lists on buckets that use them. The generation and metageneration preconditions are unchanged.
- The bundled Google libraries are updated: `Google.Api.Gax` and `Google.Api.Gax.Rest` 4.15.1 to 4.15.2, `Google.Apis`, `Google.Apis.Auth` and `Google.Apis.Core` 1.77.0 to 1.78.0, and `Google.Apis.Storage.v1` 1.77.0.4290 to 1.77.0.4299. `Google.Cloud.Storage.V1` stays at 5.0.0.
- The emulator used by the integration tests is selected through an internal test seam set by the test harness. The `GCSCONNECTOR_EMULATOR_HOST` environment variable is gone, so no server setting can redirect the extension's storage traffic.

### Fixed

- `Object_DeleteByPrefix` and `Object_Copy` were never retried after a transient Google error: the Storage library only retries a delete with a generation precondition, and a copy with a destination precondition. `Object_DeleteByPrefix` now deletes each object only if it is still the generation it listed, which also makes the delete retryable; an object replaced after the listing is left in place and not counted. `Object_Copy` and `Object_Move`'s copy are retried explicitly (`Object_Move`'s delete already was).
- After a copy failed with 404, a failed check of the source (for example a 403 or a timeout) was taken to mean the source was missing, so the error could name the wrong side. Only a 404 on the check means that now; any other failure names both the source object and the destination bucket.
- Identity provider timeout and connection errors showed the full `TokenEndpoint`, including its query string. They now show the scheme, host and path only, as validation errors already did.
- A downloaded object whose data didn't match its checksum, or a connection dropped mid-transfer, surfaced as a raw .NET `IOException`. Both now have actionable messages.
- Some error messages ended with two periods or ran two sentences together.

### Documentation

- Bucket names may also contain underscores and dots.
- `MaxResults` counts objects and `PrefixList` entries together, and Google returns at most 1,000 per call.
- `Object_Delete` and `Object_DeleteByPrefix` are permanent only on buckets without Object Versioning or soft delete.
- gzip-encoded objects download decompressed.
- `OutSystems.SAP.API.dll` was listed as a required assembly, but the project doesn't reference it.

## [1.7.1] - 2026-10-28

Patch release with two fixes found while porting 1.7.0 to the ODC connector. No changes to actions, inputs or outputs: a drop-in replacement for 1.7.0.

### Fixed

- `Object_UpdateMetadata` could write stale metadata onto new content. Setting the metageneration precondition explicitly turned off the SDK's automatic generation precondition, and replacing an object's content resets its metageneration to 1. So if another writer replaced the content between the read and the update, the update still succeeded and applied the old `ContentType` and custom metadata to the new content. The update is now guarded by both the generation and the metageneration it read, and that case fails with the existing "changed by another request, retry" error.
- `Object_Copy` into a bucket that doesn't exist reported the source as missing ("Bucket '<source>' does not exist" or "Object '<source>' was not found"). It now checks whether the source still exists and names the missing destination bucket, the same way `Object_Move` has done since 1.7.0. Both actions share this logic.

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
