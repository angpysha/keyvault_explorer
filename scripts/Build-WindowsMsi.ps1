[CmdletBinding()]
param(
    [string]$ProjectPath = "src/KeyVaultExplorer.App/KeyVaultExplorer.App.csproj",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0-windows10.0.19041.0",
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true,
    [string]$WixSource = "installer/wix/KeyVaultExplorer.wxs",
    [string]$ArtifactsDirectory = "artifacts/windows",
    [string]$MsiFileName = "KeyVaultExplorer.msi",
    [string]$WixVersion = "6.0.2"
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

$isWindowsOs = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
if (-not $isWindowsOs) {
    throw "Build-WindowsMsi.ps1 must run on Windows (WiX produces Windows Installer packages)."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedProjectPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$resolvedWixSource = (Resolve-Path (Join-Path $repoRoot $WixSource)).Path
$artifactsRoot = Join-Path $repoRoot $ArtifactsDirectory
$msiOut = Join-Path $artifactsRoot $MsiFileName

function Invoke-Wix {
    param([string[]]$Arguments)
    Invoke-External -FilePath "dotnet" -Arguments (@("wix") + $Arguments)
}

Write-Step "Restoring dotnet tools (WiX)"
Push-Location $repoRoot
try {
    Invoke-External -FilePath "dotnet" -Arguments @("tool", "restore")
}
finally {
    Pop-Location
}

Write-Step "Ensuring WiX UI extension is cached"
Invoke-Wix -Arguments @("extension", "add", "WixToolset.UI.wixext/$WixVersion")

Write-Step "Publishing MAUI Windows app ($Configuration, $RuntimeIdentifier)"
$publishDir = Join-Path $repoRoot "artifacts/obj/windows-publish"
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}

$publishArgs = @(
    "publish",
    $resolvedProjectPath,
    "-c", $Configuration,
    "-f", $Framework,
    "-r", $RuntimeIdentifier,
    "-o", $publishDir,
    "-p:PublishSingleFile=false",
    "-p:SelfContained=$SelfContained"
)
Invoke-External -FilePath "dotnet" -Arguments $publishArgs

Write-Step "Deriving MSI product version from the project file"
$displayNode = Select-Xml -Path $resolvedProjectPath -XPath "//ApplicationDisplayVersion" -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Node -First 1
$appVerNode = Select-Xml -Path $resolvedProjectPath -XPath "//ApplicationVersion" -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Node -First 1
$displayVer = if ($null -ne $displayNode) { $displayNode.InnerText.Trim() } else { "1.0" }
$appVer = if ($null -ne $appVerNode) { $appVerNode.InnerText.Trim() } else { "0" }
$parts = $displayVer.Split(".", [System.StringSplitOptions]::RemoveEmptyEntries)
$major = if ($parts.Length -ge 1) { $parts[0] } else { "1" }
$minor = if ($parts.Length -ge 2) { $parts[1] } else { "0" }
$build = if ($parts.Length -ge 3) { $parts[2] } else { "0" }
$revision = $appVer.Trim()
$productVersion = "$major.$minor.$build.$revision"
Write-Host "ProductVersion for MSI: $productVersion" -ForegroundColor Green

New-Item -Path $artifactsRoot -ItemType Directory -Force | Out-Null

Write-Step "Building MSI with WiX"
$publishDirFull = (Resolve-Path $publishDir).Path
$wixBuildArgs = @(
    "build",
    $resolvedWixSource,
    "-ext", "WixToolset.UI.wixext",
    "-arch", "x64",
    "-bindpath", "publish=$publishDirFull",
    "-define", "ProductVersion=$productVersion",
    "-out", $msiOut
)
Invoke-Wix -Arguments $wixBuildArgs

Write-Step "MSI build completed"
Write-Host "MSI: $msiOut" -ForegroundColor Green
