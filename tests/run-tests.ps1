<#
.SYNOPSIS
    Full test suite for the Google Cloud Storage Connector (O11 extension).

.DESCRIPTION
    Tests every action the extension exposes to OutSystems developers, with NO Google
    account or credentials required:

      - Integration tests run against fake-gcs-server, a local Google Cloud Storage
        emulator, via the extension-specific GCSCONNECTOR_EMULATOR_HOST environment variable.
      - Signed-URL, caching, and validation tests run fully offline (V4 signing is
        local RSA; a throwaway key is generated on first run).

    Prerequisites:
      - The extension is built: Source\NET\Bin\OutSystems.NssGoogleCloudStorage_ext.dll
        (including the OutSystems platform assemblies — see repo README).
      - fake-gcs-server.exe: pass -Download once to fetch it from the official GitHub
        releases into tests\.tools\, or point -EmulatorExe / $env:FAKE_GCS_EXE at it.
      - openssl (bundled with Git for Windows) for the one-time throwaway key.

.EXAMPLE
    .\run-tests.ps1 -Download      # first run: fetches the emulator, runs everything
    .\run-tests.ps1                # subsequent runs
    .\run-tests.ps1 -SkipEmulator  # offline + federation contract tests only (no emulator needed)
    .\run-tests.ps1 -Category Live # live Workload Identity Federation (GitHub Actions only)
#>
[CmdletBinding()]
param(
    [string]$EmulatorExe,
    [switch]$Download,
    [switch]$SkipEmulator,
    [int]$EmulatorPort = 4443,
    # Default = offline + emulator integration + federation contract. Live = real Google Cloud via
    # GitHub OIDC (needs id-token: write and the GCP_* repository variables); never mixed with Default.
    [ValidateSet("Default", "Live")]
    [string]$Category = "Default"
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$testsDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $testsDir
$bin = Join-Path $repoRoot 'Source\NET\Bin'
$tools = Join-Path $testsDir '.tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null

$extDll = Join-Path $bin 'OutSystems.NssGoogleCloudStorage_ext.dll'
$osrtDll = Join-Path $bin 'OutSystems.HubEdition.RuntimePlatform.dll'
if (-not (Test-Path $extDll)) { throw "Extension not built: $extDll. Build the project first (see README)." }
if (-not (Test-Path $osrtDll)) { throw "OutSystems platform assemblies missing from Bin (see README)." }

# --- Throwaway RSA key (local signing only; never touches any real service) ----------
$pemPath = Join-Path $tools 'test-key.pem'
if (-not (Test-Path $pemPath)) {
    $openssl = Get-Command openssl -ErrorAction SilentlyContinue
    if (-not $openssl) {
        $gitOpenssl = Join-Path $env:ProgramFiles 'Git\usr\bin\openssl.exe'
        if (Test-Path $gitOpenssl) { $openssl = $gitOpenssl } else { throw "openssl not found (install Git for Windows or add openssl to PATH)." }
    }
    # openssl writes progress to stderr; don't let PS 5.1 treat that as a failure
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out $pemPath 2>$null
    $ErrorActionPreference = $eap
    if (-not (Test-Path $pemPath)) { throw "openssl failed to generate the test key." }
    Write-Host "Generated throwaway test key: $pemPath"
}
$pem = [IO.File]::ReadAllText($pemPath)

# --- fake-gcs-server ------------------------------------------------------------------
$emulatorProc = $null
$emulatorOn = $false
if (-not $SkipEmulator -and $Category -ne "Live") {
    if (-not $EmulatorExe) { $EmulatorExe = $env:FAKE_GCS_EXE }
    if (-not $EmulatorExe) { $EmulatorExe = Join-Path $tools 'fake-gcs-server.exe' }

    if (-not (Test-Path $EmulatorExe)) {
        if ($Download) {
            $version = '1.54.0'
            $url = "https://github.com/fsouza/fake-gcs-server/releases/download/v$version/fake-gcs-server_${version}_Windows_amd64.tar.gz"
            $tgz = Join-Path $tools 'fake-gcs-server.tar.gz'
            Write-Host "Downloading fake-gcs-server v$version from official GitHub releases..."
            Invoke-WebRequest -Uri $url -OutFile $tgz
            tar -xzf $tgz -C $tools fake-gcs-server.exe
            Remove-Item $tgz
            Write-Host "Saved to $EmulatorExe"
        } else {
            throw "fake-gcs-server.exe not found. Re-run with -Download to fetch it, or set -EmulatorExe / `$env:FAKE_GCS_EXE. Use -SkipEmulator for offline tests only."
        }
    }

    Write-Host "Starting fake-gcs-server on 127.0.0.1:$EmulatorPort..."
    $emulatorProc = Start-Process -FilePath $EmulatorExe -ArgumentList "-scheme http -host 127.0.0.1 -port $EmulatorPort -backend memory" -PassThru -WindowStyle Hidden

    # Wait for the port to accept connections
    $ready = $false
    foreach ($i in 1..50) {
        try {
            $tcp = New-Object Net.Sockets.TcpClient
            $tcp.Connect('127.0.0.1', $EmulatorPort); $tcp.Close(); $ready = $true; break
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $ready) { throw "fake-gcs-server did not start listening on port $EmulatorPort." }
    $emulatorOn = $true
    # Must be set in-process BEFORE the extension creates its first StorageClient
    [Environment]::SetEnvironmentVariable('GCSCONNECTOR_EMULATOR_HOST', "127.0.0.1:$EmulatorPort")
} else {
    [Environment]::SetEnvironmentVariable('GCSCONNECTOR_EMULATOR_HOST', $null)
}

# --- Test harness (compiled against the real extension DLL, .NET Framework 4.8) ------
$sources = @("TestHarness.cs", "FederationTests.cs", "LiveFederationTests.cs") | ForEach-Object { Join-Path $testsDir $_ }
$refs = @($extDll, $osrtDll) + (@("Google.Apis.dll", "Google.Apis.Core.dll", "Google.Apis.Auth.dll", "Newtonsoft.Json.dll") | ForEach-Object { Join-Path $bin $_ }) +
        @("System.dll", "System.Core.dll", "System.Net.Http.dll")

# The harness now derives types from Google assemblies (the federation fakes), which are loaded
# as soon as Add-Type enumerates the compiled types. Resolve them from Bin before compiling.
Add-Type -TypeDefinition @"
public static class GcsBinResolver
{
    public static void Install(string bin)
    {
        System.AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs e)
        {
            string p = System.IO.Path.Combine(bin, new System.Reflection.AssemblyName(e.Name).Name + ".dll");
            return System.IO.File.Exists(p) ? System.Reflection.Assembly.LoadFrom(p) : null;
        };
    }
}
"@
[GcsBinResolver]::Install($bin)

try {
    Add-Type -Path $sources -ReferencedAssemblies $refs
    $result = [GcsExtensionTestSuite]::Run($bin, $pem, $emulatorOn, $Category)
    Write-Host $result.Log
    if ($result.Failed -gt 0) {
        Write-Host ("RESULT: {0} passed, {1} FAILED, {2} skipped" -f $result.Passed, $result.Failed, $result.Skipped) -ForegroundColor Red
        exit 1
    }
    Write-Host ("RESULT: {0} passed, 0 failed, {1} skipped" -f $result.Passed, $result.Skipped) -ForegroundColor Green
    exit 0
}
finally {
    if ($emulatorProc -and -not $emulatorProc.HasExited) { Stop-Process -Id $emulatorProc.Id -Force }
    [Environment]::SetEnvironmentVariable('GCSCONNECTOR_EMULATOR_HOST', $null)
}
