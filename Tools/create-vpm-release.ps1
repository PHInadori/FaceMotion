<#
.SYNOPSIS
  Builds the VPM release artifact for com.facemotion.editor.

.DESCRIPTION
  Stages the package folder and writes com.facemotion.editor-<version>.zip with the
  package root at the archive root (package.json at the top level, never nested under
  Packages/com.facemotion.editor). Emits release-info.json containing the version, the
  archive file name, the recorded zipSHA256, and the planned release URL.

  No network access and no publishing is performed. Used locally and by
  .github/workflows/vpm-release.yml.
#>
[CmdletBinding()]
param(
    [string]$PackagePath = (Join-Path $PSScriptRoot "../Packages/com.facemotion.editor"),
    [string]$OutputDir = (Join-Path $PSScriptRoot "../dist"),
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

function Resolve-PathOrFail {
    param([string]$Path, [string]$Label)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved)) { throw "$Label not found: $resolved" }
    return $resolved
}

$PackagePath = Resolve-PathOrFail $PackagePath "Package path"
$manifestPath = Join-Path $PackagePath "package.json"
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "package.json not found: $manifestPath" }

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not $Version) { $Version = $manifest.version }
if ([string]::IsNullOrWhiteSpace($Version)) { throw "package.json has no version." }
if ($manifest.name -ne "com.facemotion.editor") { throw "Unexpected package name: $($manifest.name)." }

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$archiveName = "com.facemotion.editor-$Version.zip"
$archivePath = Join-Path $OutputDir $archiveName
$staging = Join-Path $OutputDir ".staging"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

$excludeNames = @(
    ".git", ".github", "TestResults", "Temp", "Logs", "Library", "obj",
    ".vs", ".vscode", "*.user", "*.suo", "*.csproj", "*.sln", "*.opendb"
)
Get-ChildItem -LiteralPath $PackagePath -Force | Where-Object {
    $skip = $false
    foreach ($pattern in $excludeNames) {
        if ($_.Name -like $pattern) { $skip = $true; break }
    }
    -not $skip
} | Copy-Item -Destination $staging -Recurse -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
$archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $stagingPrefix = $staging.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    Get-ChildItem -LiteralPath $staging -File -Recurse | ForEach-Object {
        $entryName = $_.FullName.Substring($stagingPrefix.Length).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archive.Dispose()
}
Remove-Item -LiteralPath $staging -Recurse -Force

$sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()

$releaseUrl = "https://github.com/PHInadori/FaceMotion/releases/download/v$Version/$archiveName"
$info = [ordered]@{
    name        = $manifest.name
    version     = $Version
    displayName = $manifest.displayName
    archive     = $archiveName
    zipSHA256   = $sha256
    url         = $releaseUrl
    generated   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$infoPath = Join-Path $OutputDir "release-info.json"
$info | ConvertTo-Json | Set-Content -LiteralPath $infoPath -Encoding UTF8

Write-Output "archive:   $archivePath"
Write-Output "size:      $((Get-Item -LiteralPath $archivePath).Length) bytes"
Write-Output "zipSHA256: $sha256"
Write-Output "metadata:  $infoPath"
Write-Output ""
Write-Output "Listing snippet (zipSHA256 into source.json / index.json):"
Write-Output "  zipSHA256: $sha256"
