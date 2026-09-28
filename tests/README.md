# Test Suite

Tests every action the extension exposes to OutSystems developers — **no Google account or credentials required** for the default run.

## How it works

- **Integration tests** run the real extension DLL against [fake-gcs-server](https://github.com/fsouza/fake-gcs-server), a local in-memory Google Cloud Storage emulator, using the extension-specific `GCSCONNECTOR_EMULATOR_HOST` environment variable (which the extension honors — it is never set on a real OutSystems server, where the extension always talks to production GCS).
- **Offline tests** (signed URLs, input validation, client caching) need no server at all: V4 URL signing is local RSA cryptography, performed with a throwaway key generated on first run.
- **Federation contract tests** exercise Workload Identity Federation end to end against an in-process fake of every party in the chain — the identity provider's token endpoint, Google STS, IAM Credentials (`generateAccessToken` + `signBlob`) and Storage. Every request is recorded, so the tests assert the exact protocol the extension speaks. No network is used. The fakes are plugged in through an internal `HttpClientFactoryOverride` test seam that is never set in production.
- **Live tests** (`-Category Live`, GitHub Actions only) run keyless against **real** Google Cloud: the workflow's GitHub OIDC token is exchanged through Workload Identity Federation for a sandbox service account, and real Storage and `signBlob` are called. No Google key is involved.

## Running

```powershell
# First run: downloads fake-gcs-server (~11 MB) from its official GitHub releases
.\run-tests.ps1 -Download

# Subsequent runs
.\run-tests.ps1

# Offline + federation contract tests only (no emulator, no downloads)
.\run-tests.ps1 -SkipEmulator

# Live Workload Identity Federation (runs in GitHub Actions; elsewhere the live checks report SKIPPED)
.\run-tests.ps1 -Category Live
```

`-Category Default` (the default) runs everything except the live tests; `-Category Live` runs only the live tests. This is the harness's equivalent of `--filter "Category!=Live"` / `--filter "Category=Live"`.

Prerequisites:
- The extension built at `Source\NET\Bin\OutSystems.NssGoogleCloudStorage_ext.dll` (see the repo README for the OutSystems platform assemblies)
- Windows PowerShell 5.1 (runs on .NET Framework 4.8 — the extension's exact runtime)
- `openssl` on PATH or Git for Windows installed (one-time throwaway key generation)

## Coverage

| Area | What's verified |
|---|---|
| `Bucket_Create` / `Bucket_Exists` / `Bucket_List` / `Bucket_Delete` | Full lifecycle, duplicate-name error, non-empty-delete error, timestamps |
| `Object_Upload` / `Object_Download` | Byte-exact round-trips: text, binary, empty (0 bytes), 5 MB, unicode names, ContentType preservation |
| `Object_GetMetadata` | All fields (name, bucket, size, content type, hashes, generation, timestamps), missing → `Exists=False` |
| `Object_Exists` | Present/missing objects |
| `Object_List` | Flat listing, prefix filter, delimiter folders (`PrefixList`), prefix+delimiter, pagination via `MaxResults`/`NextPageToken` |
| `Object_Copy` / `Object_Move` / `Object_Delete` | Cross-bucket copy/move semantics, source retention/removal |
| `Object_UpdateMetadata` | Field updates without re-upload, custom key add/overwrite/removal, content untouched, nothing-to-update and missing-object errors |
| `Object_DeleteByPrefix` | Recursive "folder" delete with count, unrelated objects untouched, zero-match count, empty-prefix rejection |
| Custom metadata | Upload with key-value metadata, retrieval via `Object_GetMetadata` `CustomMetadata` |
| `Object_GetSignedUrl` | V4 URL structure, operation case-insensitivity, ContentType-in-signature, expiration bounds (0, >7d, exactly 7d) |
| Error handling | Missing object/bucket errors, garbage private key → friendly message, negative MaxResults |
| Caching | `StorageClient`/`UrlSigner` instance reuse, per-credential isolation, key and federation never sharing a client |
| Credential inputs | The `Authentication` input (a `GCS_Authentication` record) as the only credential input: an empty record names the missing key fields, a key in the record signs as that service account (locally, no network), `ProjectId` required by `Bucket_List` |
| Auth method switch | Empty/explicit/case-insensitive `ServiceAccountKey`, invalid method, missing-field messages for both methods, provider formats, https-only token endpoint |
| Federation protocol (contract) | Client-credentials request, RFC 8693 STS exchange, service account impersonation, Storage called with the short-lived token, supplied `SubjectToken`, `client_secret_basic` fallback, and friendly errors for a rejected client, a non-JWT token, an STS rejection, or a missing Token Creator role |
| Federation live (CI only) | Real Google Cloud via GitHub OIDC: the federated identity is accepted by Storage, and the signed URL's signature verifies against the service account's published certificates (the verifier itself is proven offline in every default run) |

### How the live job works, with no Google secrets

The `live-federation` CI job proves the whole chain against **real** Google Cloud without any Google credential existing anywhere:

1. The job has `permissions: id-token: write`, so GitHub Actions can mint an OIDC token for this workflow run. The test requests it from `ACTIONS_ID_TOKEN_REQUEST_URL`, with the audience set to the workload identity provider.
2. The token goes into the `Authentication` record as `SubjectToken` with `AuthenticationMethod = WorkloadIdentityFederation`. The extension exchanges it at Google STS and impersonates the sandbox service account.
3. Google accepts it only because the sandbox provider trusts GitHub's issuer and its attribute condition allows this repository's ID.

The `GCP_WIF_PROVIDER`, `GCP_SERVICE_ACCOUNT` and `GCP_PROJECT_ID` values are plain repository **variables**, not secrets. The sandbox service account has no keys and no project roles, so a non-existent bucket answers 404 (the chain works) rather than exposing data. The only secret the job uses is the read-only deploy key that fetches the proprietary OutSystems assemblies needed to compile.

The live checks:
- `Bucket_Exists` on a random, non-existent bucket returns `False`. A 404 proves the chain; a broken chain gives 401/403.
- `Object_GetSignedUrl` names the service account in `X-Goog-Credential`, and its signature verifies against the service account's public certificates from `https://www.googleapis.com/service_accounts/v1/metadata/x509/{sa}`. The verifier recomputes the V4 canonical request and string-to-sign and checks the RSA-SHA256 signature; it is proven offline in every default run.

Outside GitHub Actions (when `ACTIONS_ID_TOKEN_REQUEST_URL`, `ACTIONS_ID_TOKEN_REQUEST_TOKEN`, `GCP_WIF_PROVIDER` or `GCP_SERVICE_ACCOUNT` is missing), the live checks are reported as **SKIPPED**, never passed. In CI, the job's *Check live configuration* step fails first if any of those values is empty, so a green `live-federation` job always means the live checks ran and passed.

What the tests **cannot** prove: they load the Google assemblies through a version-agnostic resolver, so they don't exercise the OutSystems runtime's own assembly binding. Only a call from a published OutSystems app does.

The harness ([TestHarness.cs](TestHarness.cs), [FederationTests.cs](FederationTests.cs), [LiveFederationTests.cs](LiveFederationTests.cs)) is compiled at run time against the built extension DLL and calls the same public `Mss*` methods the OutSystems platform calls.

## Parity with the ODC connector's tests

Every test in the [ODC connector's suite](https://github.com/promonteiro89/google-cloud-storage-connector-odc/tree/main/tests/GoogleCloudStorage.Tests) has an O11 counterpart that asserts the same behaviour. ODC's xUnit tests map to named checks in this harness; where ODC asserts several things in one test, O11 reports each as its own check.

| ODC test | O11 check(s) |
|---|---|
| **OfflineTests** | |
| `SignedUrl_Download_returns_a_v4_url` | SignedUrl Download returns V4 URL |
| `SignedUrl_operation_is_case_insensitive` | SignedUrl operation is case-insensitive |
| `SignedUrl_without_ContentType_does_not_sign_content_type_header` | No ContentType -> content-type not in signed headers |
| `SignedUrl_with_ContentType_binds_it_into_the_signature` | ContentType becomes part of the signature |
| `SignedUrl_invalid_operation_is_rejected` | Invalid Operation rejected |
| `SignedUrl_expiration_zero_is_rejected` | ExpirationMinutes <= 0 rejected |
| `SignedUrl_expiration_over_seven_days_is_rejected` | ExpirationMinutes > 7 days rejected |
| `SignedUrl_exactly_seven_days_is_accepted` | Exactly 7 days accepted |
| `SignedUrl_accepts_json_escaped_private_key` | JSON-escaped (\n) private key parses |
| `Garbage_private_key_gives_a_friendly_parse_error` | Garbage key -> friendly parse error |
| `Object_List_rejects_negative_MaxResults` | Negative MaxResults rejected |
| `UpdateMetadata_with_nothing_to_change_is_rejected` | UpdateMetadata with nothing to change is rejected |
| `DeleteByPrefix_rejects_an_empty_prefix` | DeleteByPrefix rejects an empty prefix |
| `DeleteByPrefix_rejects_a_whitespace_prefix` | DeleteByPrefix rejects a whitespace prefix |
| `StorageClient_is_cached_for_the_same_credentials` | StorageClient cached (same instance reused) |
| `UrlSigner_is_cached_for_the_same_credentials` | UrlSigner cached (same instance reused) |
| `Distinct_credentials_get_distinct_signers` | Distinct credentials get distinct signers |
| **IntegrationTests** (O11 runs them as one sequential scenario) | |
| `Bucket_lifecycle_create_exists_list_delete` | Bucket_Exists on missing bucket -> False · Bucket_Create + Bucket_Exists -> True · Duplicate Bucket_Create raises error · Bucket_List returns both buckets · Bucket_List Created timestamps populated · Emptied and deleted bucket (x2) |
| `Upload_download_round_trips_all_shapes` | Object_Upload of 8 objects · Object_Download round-trips content · round-trips ContentType · binary is byte-exact · empty object -> 0 bytes · 5MB is byte-exact · Unicode object names round-trip |
| `Object_Exists_reports_presence` | Object_Exists -> True after upload · Object_Exists on missing object -> False |
| `GetMetadata_populates_fields_and_reports_missing` | Object_GetMetadata -> Exists True · GetMetadata Name/Bucket/Size/ContentType · Generation/timestamps populated · hashes populated · missing object -> Exists False |
| `Custom_metadata_round_trips_through_upload_and_getmetadata` | Upload with custom metadata round-trips |
| `UpdateMetadata_changes_headers_and_adds_overwrites_removes_keys_without_touching_content` | UpdateMetadata sets fields without re-upload · adds, overwrites and removes custom keys · leaves content untouched |
| `DeleteByPrefix_deletes_the_folder_and_returns_the_count` | DeleteByPrefix deletes the whole 'folder' (incl. subfolders) and counts · keeps a sibling folder sharing the stem · leaves unrelated objects alone |
| `List_flat_prefix_and_delimiter_folders` | Object_List returns all objects · sizes are correct · full mode -> empty NextPageToken · Prefix filters correctly · Delimiter: only root objects · Delimiter: folders as PrefixList · Prefix+Delimiter: direct children only |
| `List_pagination_walks_every_object_via_NextPageToken` | Pagination: MaxResults=3 walks all objects via NextPageToken |
| `Copy_keeps_source_move_removes_it_delete_removes_object` | Object_Copy across buckets · Object_Move across buckets · Object_Delete removes the object |
| `Errors_surface_for_missing_object_missing_bucket_and_nonempty_delete` | Download of missing object raises error · Upload to missing bucket raises error · Bucket_Delete on non-empty bucket raises error |
| **FederationTests** | |
| `Invalid_method_is_rejected_with_the_valid_values` | Invalid method is rejected with the valid values |
| `Explicit_ServiceAccountKey_method_behaves_like_the_default` | Explicit ServiceAccountKey (case-insensitive) behaves like the default |
| `ServiceAccountKey_without_key_material_names_the_missing_fields` | ServiceAccountKey without key material names the missing field |
| `Federation_without_required_fields_names_every_missing_field` | Federation without required fields names every missing field |
| `Federation_with_a_supplied_token_does_not_need_client_credentials` | Federation with a supplied token needs no client credentials |
| `Malformed_provider_is_rejected_with_the_expected_format` | Malformed provider is rejected with the expected format |
| `Provider_is_accepted_in_every_common_form` (3 cases) | Provider accepted as `//iam…` · `https://iam…` · `projects/…` |
| `Plain_http_token_endpoint_is_rejected_unless_loopback` | Plain http token endpoint is rejected unless loopback |
| `Key_and_federation_never_share_a_cached_client_and_federation_is_cached_per_identity` | Key and federation never share a cached client; federation cached per identity |
| `Client_credentials_flow_speaks_the_exact_protocol_end_to_end` | Client credentials flow: bucket reported as existing · 1) IdP · 2) STS · 3) IAM Credentials · 4) Storage |
| `Supplied_token_skips_the_identity_provider_entirely` | Supplied token skips the identity provider entirely |
| `Falls_back_to_http_basic_client_auth_when_the_provider_requires_it` | Falls back to HTTP Basic client auth when the provider requires it |
| `Rejected_client_credentials_give_an_actionable_error` | Rejected client credentials give an actionable error |
| `A_non_jwt_token_from_the_provider_is_explained` | A non-JWT token from the provider is explained and never sent to Google |
| `A_token_rejected_by_google_sts_is_explained` | A token rejected by Google STS is explained |
| `Signed_urls_are_signed_by_the_service_account_through_signBlob` | Signed URLs are signed by the service account through signBlob |
| `Missing_token_creator_role_on_signBlob_is_explained` | Missing Token Creator role on signBlob is explained |
| **LiveFederationTests** (`-Category Live`, CI only) | |
| `Google_accepts_the_federated_identity_for_storage_calls` | LIVE: Google accepts the federated identity for Storage calls |
| `Signed_url_signature_verifies_against_the_service_accounts_published_certificate` | LIVE: X-Goog-Credential names the service account · LIVE: signature verifies against the service account's published certificates |
| `Verifier_accepts_a_genuine_v4_signed_url_and_rejects_a_tampered_one` | Signed-URL verifier accepts a genuine V4 URL · rejects a tampered V4 URL (runs in every default run) |

O11 also has checks ODC doesn't: the emulator's shared client, distinct credentials getting distinct clients, empty `CustomMetadata` when none is set, `UpdateMetadata` on a missing object, a zero-match `DeleteByPrefix`, and the `GCS_Authentication` record checks (empty record, key in the record, `ProjectId` required by `Bucket_List`).
