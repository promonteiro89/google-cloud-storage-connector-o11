# Test Suite

Tests every action the extension exposes to OutSystems developers — **no Google account or credentials required**.

## How it works

- **Integration tests** run the real extension DLL against [fake-gcs-server](https://github.com/fsouza/fake-gcs-server), a local in-memory Google Cloud Storage emulator, using the extension-specific `GCSCONNECTOR_EMULATOR_HOST` environment variable (which the extension honors — it is never set on a real OutSystems server, where the extension always talks to production GCS).
- **Offline tests** (signed URLs, input validation, client caching) need no server at all: V4 URL signing is local RSA cryptography, performed with a throwaway key generated on first run.

## Running

```powershell
# First run: downloads fake-gcs-server (~11 MB) from its official GitHub releases
.\run-tests.ps1 -Download

# Subsequent runs
.\run-tests.ps1

# Offline tests only (no emulator, no downloads)
.\run-tests.ps1 -SkipEmulator
```

Prerequisites:
- The extension built at `Source\NET\Bin\OutSystems.NssGoogleCloudStorage_ext.dll` (see the repo README for the OutSystems platform assemblies)
- Windows PowerShell 5.1 (runs on .NET Framework 4.8 — the extension's exact runtime)
- `openssl` on PATH or Git for Windows installed (one-time throwaway key generation)

## Coverage (49 checks)

| Area | What's verified |
|---|---|
| `Bucket_Create` / `Bucket_Exists` / `Bucket_List` / `Bucket_Delete` | Full lifecycle, duplicate-name error, non-empty-delete error, timestamps |
| `Object_Upload` / `Object_Download` | Byte-exact round-trips: text, binary, empty (0 bytes), 5 MB, unicode names, ContentType preservation |
| `Object_GetMetadata` | All fields (name, bucket, size, content type, hashes, generation, timestamps), missing → `Exists=False` |
| `Object_Exists` | Present/missing objects |
| `Object_List` | Flat listing, prefix filter, delimiter folders (`PrefixList`), prefix+delimiter, pagination via `MaxResults`/`NextPageToken` |
| `Object_Copy` / `Object_Move` / `Object_Delete` | Cross-bucket copy/move semantics, source retention/removal |
| `Object_GetSignedUrl` | V4 URL structure, operation case-insensitivity, ContentType-in-signature, expiration bounds (0, >7d, exactly 7d) |
| Error handling | Missing object/bucket errors, garbage private key → friendly message, negative MaxResults |
| Caching | `StorageClient`/`UrlSigner` instance reuse, per-credential isolation |

The harness ([TestHarness.cs](TestHarness.cs)) is compiled at run time against the built extension DLL and calls the same public `Mss*` methods the OutSystems platform calls.
