[CmdletBinding()]
param(
    [string]$ProjectPath = "src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0-maccatalyst",
    [string]$RuntimeIdentifier = "maccatalyst-arm64",
    [string]$CodesignKey = "",
    [string]$CodesignProvision = "",
    [string]$CodesignEntitlements = "src/KeyVaultExplorer.App/Platforms/MacCatalyst/Entitlements.plist",
    [bool]$UseHardenedRuntime = $true,
    [string]$ArchiveName = "KeyVaultExplorer",
    [string]$ArtifactsDirectory = "artifacts/macos"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host ">> $FilePath $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedProjectPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$artifactsRoot = Join-Path $repoRoot $ArtifactsDirectory
$archivePath = Join-Path $artifactsRoot "$ArchiveName.xcarchive"
$xcodeArchivesRoot = Join-Path $HOME "Library/Developer/Xcode/Archives"
$resolvedEntitlementsPath = ""

Write-Step "Preparing archive output directory"
New-Item -Path $artifactsRoot -ItemType Directory -Force | Out-Null
if (Test-Path $archivePath) {
    Remove-Item -Path $archivePath -Recurse -Force
}

$buildStartTimeUtc = [DateTime]::UtcNow

Write-Step "Building .xcarchive with dotnet publish"
# ArchiveOnBuild=true makes the Mac Catalyst build produce an Xcode archive.
$dotnetArguments = @(
    "publish",
    $resolvedProjectPath,
    "-f", $Framework,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "-p:ArchiveOnBuild=true",
    "-p:UseHardenedRuntime=$UseHardenedRuntime",
    "-p:CreatePackage=false"
)

if (-not [string]::IsNullOrWhiteSpace($CodesignKey)) {
    $dotnetArguments += "-p:CodesignKey=$CodesignKey"
}

if (-not [string]::IsNullOrWhiteSpace($CodesignProvision)) {
    if (-not (Test-Path $CodesignProvision)) {
        throw "The specified provisioning profile was not found: '$CodesignProvision'"
    }

    $resolvedProvisionPath = (Resolve-Path $CodesignProvision).Path
    $dotnetArguments += "-p:CodesignProvision=$resolvedProvisionPath"
}
else {
    Write-Host "No provisioning profile provided, building archive without -p:CodesignProvision." -ForegroundColor Yellow
}

if (-not [string]::IsNullOrWhiteSpace($CodesignEntitlements)) {
    $entitlementsCandidate = Join-Path $repoRoot $CodesignEntitlements
    if (-not (Test-Path $entitlementsCandidate)) {
        throw "The specified entitlements file was not found: '$entitlementsCandidate'"
    }

    $resolvedEntitlementsPath = (Resolve-Path $entitlementsCandidate).Path
    $dotnetArguments += "-p:CodesignEntitlements=$resolvedEntitlementsPath"
}

Invoke-External -FilePath "dotnet" -Arguments $dotnetArguments

if (-not (Test-Path $xcodeArchivesRoot)) {
    throw "Xcode archives root was not found: '$xcodeArchivesRoot'"
}

$generatedArchive = Get-ChildItem -Path $xcodeArchivesRoot -Filter "*.xcarchive" -Directory -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTimeUtc -ge $buildStartTimeUtc.AddMinutes(-1) } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $generatedArchive) {
    throw "Archive was not generated. No recent .xcarchive found in '$xcodeArchivesRoot'."
}

Write-Step "Copying archive to artifacts directory"
Copy-Item -Path $generatedArchive.FullName -Destination $archivePath -Recurse -Force

Write-Step "Archive generation completed"
Write-Host "XCArchive: $archivePath" -ForegroundColor Green
