# Contributing

Thanks for your interest in contributing to the Google Cloud Storage Connector for OutSystems 11. This guide covers how to set up the project, the conventions to follow, and how to submit changes.

## Getting Started

1. **Fork** the repository and clone your fork.
2. Copy the OutSystems platform assemblies into `Source/NET/Bin/` (see [Build and Deployment](README.md#build-and-deployment) in the README). These are not committed for licensing reasons.
3. Open `Source/NET/GoogleCloudStorage_ext.sln` in **Integration Studio**, or build the project directly with MSBuild:
   ```
   msbuild Source/NET/GoogleCloudStorage_ext.csproj /p:Configuration=Release
   ```
   > Use `Build`, not `Rebuild` — a clean step removes the referenced-assembly XML docs from `Bin\`, which this project keeps as tracked files.

## Development Workflow

1. Create a feature branch from `master`:
   ```
   git checkout -b feature/short-description
   ```
2. Make your change, keeping commits focused and descriptive.
3. Verify the extension compiles (Integration Studio **Verify**, or an MSBuild `Release` build with no warnings).
4. Run the test suite — no Google credentials needed: `tests\run-tests.ps1 -Download` (see [tests/README.md](tests/README.md)). All tests must pass.
5. Open a Pull Request against `master` describing the change and its motivation.

## Coding Conventions

- Target **.NET Framework 4.8**; keep language features compatible with it.
- Match the existing style: tabs for indentation, XML doc comments on every public action, and the OutSystems `ss`/`Mss` parameter/method naming already used in the generated code.
- Keep action **signatures** (the `Interface.cs` / `Structures.cs` contracts) in sync with what Integration Studio generates — do not hand-edit generated members unless the action definition changed in Integration Studio first.
- Prefer the SDK's non-obsolete APIs (e.g. the `*DateTimeOffset` properties).
- Reuse the cached `StorageClient` / `UrlSigner` helpers rather than constructing clients per call.

## Dependencies

Third-party NuGet DLLs live in `Source/NET/Bin/` and are committed. If you bump a dependency:

- Update both the DLL/XML in `Bin\` **and** the corresponding `<Reference>` version in `GoogleCloudStorage_ext.csproj`.
- Use the `net462` build for `Google.*` / `System.*` / `Microsoft.*` packages and `net45` for `Newtonsoft.Json` (these are the .NET Framework 4.8-compatible assets).
- Note in your PR the package versions changed and why.

## Reporting Issues

When filing an issue, include:
- The connector version / commit,
- The OutSystems 11 platform version,
- The action involved and the inputs used (redact credentials),
- The full error message or unexpected behavior.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
