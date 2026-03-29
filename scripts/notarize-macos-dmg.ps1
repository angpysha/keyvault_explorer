[CmdletBinding()]
param(
    [string]$ProjectPath = "src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0-maccatalyst",
    [string]$RuntimeIdentifier = "maccatalyst-arm64",
    [string]$AppName = "Key Vault Explorer",
    [string]$SigningIdentity = "Developer ID Application: Andrii Petrovskyi (RFVKD37M39)",
    [string]$SigningIdentitySha1 = "",
    [string]$ProvisioningProfilePath = "/Users/andrii/Downloads/KeyVaultExplorer_App_DMG.provisionprofile",
    [string]$KeychainProfile = "",
    [string]$ArtifactsDirectory = "artifacts/macos",
    [switch]$SkipNotarization
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Command {
    param([string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$CommandName' was not found in PATH."
    }
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

function Get-SafeFileName {
    param([string]$Value)

    $safe = $Value -replace "[^A-Za-z0-9._-]", ""
    if ([string]::IsNullOrWhiteSpace($safe)) {
        return "app"
    }

    return $safe
}

function Get-AppBundlePath {
    param(
        [string]$PublishDirectory,
        [string]$ExpectedAppName
    )

    $candidateDirectories = @(
        $PublishDirectory,
        (Split-Path $PublishDirectory -Parent)
    ) | Select-Object -Unique

    foreach ($directory in $candidateDirectories) {
        if ([string]::IsNullOrWhiteSpace($directory) -or -not (Test-Path $directory)) {
            continue
        }

        $preferredPath = Join-Path $directory "$ExpectedAppName.app"
        if (Test-Path $preferredPath) {
            return (Resolve-Path $preferredPath).Path
        }

        $appBundle = Get-ChildItem -Path $directory -Filter "*.app" -Directory | Select-Object -First 1
        if ($null -ne $appBundle) {
            return $appBundle.FullName
        }
    }

    throw "Unable to find a .app bundle near '$PublishDirectory'."
}

function Get-CodeSigningIdentities {
    $identities = & security find-identity -v -p codesigning
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query available signing identities."
    }

    $results = @()
    foreach ($line in $identities) {
        if ($line -match '^\s*\d+\)\s+([0-9A-F]{40})\s+"(.+)"$') {
            $results += [pscustomobject]@{
                Sha1 = $matches[1]
                Name = $matches[2]
            }
        }
    }

    return $results
}

function Resolve-SigningIdentitySpecifier {
    param(
        [string]$Identity,
        [string]$IdentitySha1
    )

    $availableIdentities = @(Get-CodeSigningIdentities)
    if ($availableIdentities.Count -eq 0) {
        throw "No valid code signing identities were found in the current keychain."
    }

    if (-not [string]::IsNullOrWhiteSpace($IdentitySha1)) {
        $matchBySha = $availableIdentities | Where-Object { $_.Sha1 -eq $IdentitySha1 }
        if ($null -eq $matchBySha) {
            throw "Signing identity SHA-1 '$IdentitySha1' was not found in the current keychain."
        }

        return $IdentitySha1
    }

    $matchesByName = @($availableIdentities | Where-Object { $_.Name -eq $Identity })
    if ($matchesByName.Count -eq 0) {
        throw "Signing identity '$Identity' was not found in the current keychain."
    }

    if ($matchesByName.Count -gt 1) {
        Write-Warning "Multiple signing identities matched '$Identity'. Using SHA-1 '$($matchesByName[0].Sha1)'. Pass -SigningIdentitySha1 to override."
    }

    return $matchesByName[0].Sha1
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedProjectPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$resolvedProvisioningProfilePath = (Resolve-Path $ProvisioningProfilePath).Path
$artifactsRoot = Join-Path $repoRoot $ArtifactsDirectory
$publishDirectory = Join-Path $repoRoot "src/KeyVaultExplorer.App/bin/$Configuration/$Framework/$RuntimeIdentifier/publish"
$safeFileName = Get-SafeFileName -Value $AppName
$dmgFileName = "$safeFileName-macos.dmg"
$dmgPath = Join-Path $artifactsRoot $dmgFileName
$appStagingDirectory = Join-Path $artifactsRoot "app"
$dmgStagingDirectory = Join-Path $artifactsRoot "dmg"

if ([string]::IsNullOrWhiteSpace($KeychainProfile) -and -not $SkipNotarization) {
    throw "Provide -KeychainProfile with an xcrun notarytool keychain profile name, or use -SkipNotarization."
}

Write-Step "Validating required tooling"
Assert-Command -CommandName "dotnet"
Assert-Command -CommandName "codesign"
Assert-Command -CommandName "hdiutil"
Assert-Command -CommandName "security"
Assert-Command -CommandName "spctl"
Assert-Command -CommandName "xcrun"
$resolvedSigningIdentitySpecifier = Resolve-SigningIdentitySpecifier -Identity $SigningIdentity -IdentitySha1 $SigningIdentitySha1

Write-Step "Cleaning previous artifacts"
if (Test-Path $artifactsRoot) {
    Remove-Item -Path $artifactsRoot -Recurse -Force
}

New-Item -Path $appStagingDirectory -ItemType Directory -Force | Out-Null
New-Item -Path $dmgStagingDirectory -ItemType Directory -Force | Out-Null

Write-Step "Publishing Mac Catalyst app"
Invoke-External -FilePath "dotnet" -Arguments @(
    "publish",
    $resolvedProjectPath,
    "-c", $Configuration,
    "-f", $Framework,
    "-r", $RuntimeIdentifier
)

$publishedAppPath = Get-AppBundlePath -PublishDirectory $publishDirectory -ExpectedAppName $AppName
$stagedAppPath = Join-Path $appStagingDirectory (Split-Path $publishedAppPath -Leaf)

Write-Step "Copying app bundle to staging"
Copy-Item -Path $publishedAppPath -Destination $stagedAppPath -Recurse -Force

Write-Step "Embedding provisioning profile"
# Provisioning profiles belong to the app bundle, not to the DMG file itself.
$contentsDirectory = Join-Path $stagedAppPath "Contents"
$embeddedProvisioningProfilePath = if (Test-Path $contentsDirectory) {
    Join-Path $contentsDirectory "embedded.provisionprofile"
}
else {
    Join-Path $stagedAppPath "embedded.provisionprofile"
}
Copy-Item -Path $resolvedProvisioningProfilePath -Destination $embeddedProvisioningProfilePath -Force

Write-Step "Signing app bundle"
Invoke-External -FilePath "codesign" -Arguments @(
    "--force",
    "--deep",
    "--options", "runtime",
    "--timestamp",
    "--sign", $resolvedSigningIdentitySpecifier,
    $stagedAppPath
)

Write-Step "Verifying app signature"
Invoke-External -FilePath "codesign" -Arguments @(
    "--verify",
    "--deep",
    "--strict",
    "--verbose=2",
    $stagedAppPath
)

Write-Step "Preparing DMG staging directory"
Copy-Item -Path $stagedAppPath -Destination $dmgStagingDirectory -Recurse -Force
$applicationsLinkPath = Join-Path $dmgStagingDirectory "Applications"
if (Test-Path $applicationsLinkPath) {
    Remove-Item -Path $applicationsLinkPath -Force
}
New-Item -Path $applicationsLinkPath -ItemType SymbolicLink -Target "/Applications" | Out-Null

Write-Step "Creating DMG"
Invoke-External -FilePath "hdiutil" -Arguments @(
    "create",
    "-volname", $AppName,
    "-srcfolder", $dmgStagingDirectory,
    "-ov",
    "-format", "UDZO",
    $dmgPath
)

Write-Step "Signing DMG"
Invoke-External -FilePath "codesign" -Arguments @(
    "--force",
    "--timestamp",
    "--sign", $resolvedSigningIdentitySpecifier,
    $dmgPath
)

if (-not $SkipNotarization) {
    Write-Step "Submitting DMG for notarization"
    Invoke-External -FilePath "xcrun" -Arguments @(
        "notarytool",
        "submit",
        $dmgPath,
        "--keychain-profile", $KeychainProfile,
        "--wait"
    )

    Write-Step "Stapling notarization tickets"
    Invoke-External -FilePath "xcrun" -Arguments @("stapler", "staple", $stagedAppPath)
    Invoke-External -FilePath "xcrun" -Arguments @("stapler", "staple", $dmgPath)

    Write-Step "Validating notarized artifacts"
    Invoke-External -FilePath "spctl" -Arguments @(
        "-a",
        "-t", "exec",
        "-vv",
        $stagedAppPath
    )
    Invoke-External -FilePath "spctl" -Arguments @(
        "-a",
        "-t", "open",
        "--context", "context:primary-signature",
        "-vv",
        $dmgPath
    )
}
else {
    Write-Step "Skipping notarization because -SkipNotarization was provided"
}

Write-Step "Done"
Write-Host "Signing SHA-1: $resolvedSigningIdentitySpecifier" -ForegroundColor Green
Write-Host "App bundle: $stagedAppPath" -ForegroundColor Green
Write-Host "DMG file:   $dmgPath" -ForegroundColor Green
