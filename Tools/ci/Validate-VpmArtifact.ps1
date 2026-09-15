[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot "../.."),
    [string]$ArtifactDirectory = "dist"
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    Write-Output "PASS  $Message"
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArtifactDirectory = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $ArtifactDirectory))
$infoPath = Join-Path $ArtifactDirectory "release-info.json"
Assert-True (Test-Path -LiteralPath $infoPath) "release-info.json exists"
try { $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json } catch { throw "Invalid release-info.json: $($_.Exception.Message)" }

$zipPath = Join-Path $ArtifactDirectory $info.archive
Assert-True (Test-Path -LiteralPath $zipPath) "Release ZIP exists: $($info.archive)"
Assert-True ($info.name -eq "com.facemotion.editor") "Release metadata package name matches"
Assert-True ($info.version -match '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)') "Release metadata version is SemVer"
Assert-True ($info.archive -eq "$($info.name)-$($info.version).zip") "Archive name matches metadata version"
Assert-True ($info.zipSHA256 -match '^[a-f0-9]{64}$') "Release metadata contains SHA-256"
Assert-True ($info.url -match "/v$([regex]::Escape($info.version))/$([regex]::Escape($info.archive))$") "Release URL matches archive metadata"
$actualSha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-True ($actualSha -eq $info.zipSHA256) "ZIP SHA-256 matches release-info.json"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try { $entries = @($zip.Entries | ForEach-Object FullName) } finally { $zip.Dispose() }
Assert-True ($entries -contains "package.json") "ZIP has package.json at root"
Assert-True (($entries | Where-Object { $_ -like "Packages/*" }).Count -eq 0) "ZIP has no nested Packages directory"
foreach ($required in @("Editor/", "Documentation~/", "README.md", "CHANGELOG.md", "LICENSE.md", "THIRD-PARTY-NOTICES.md")) {
    $present = if ($required.EndsWith("/")) { ($entries | Where-Object { $_ -like "$required*" }).Count -gt 0 } else { $entries -contains $required }
    Assert-True $present "ZIP includes required content: $required"
}
$forbidden = $entries | Where-Object { $_ -match '(^|/)(Library|Temp|Logs|obj|UserSettings|TestResults|\.vs|\.idea|\.vscode|\.github|Yumeka|lilToon|liltoon|__FaceMotionTests_[^/]*)(/|$)|\.(csproj|sln|user|suo|opendb)$' }
Assert-True ($forbidden.Count -eq 0) "ZIP has no forbidden or generated content"

$extractPath = Join-Path ([System.IO.Path]::GetTempPath()) ("facemotion-vpm-" + [guid]::NewGuid().ToString("N"))
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extractPath)
    $zipManifest = Get-Content -LiteralPath (Join-Path $extractPath "package.json") -Raw | ConvertFrom-Json
    Assert-True ($zipManifest.name -eq $info.name -and $zipManifest.version -eq $info.version) "Extracted package manifest matches release metadata"
} finally {
    if (Test-Path -LiteralPath $extractPath) { Remove-Item -LiteralPath $extractPath -Recurse -Force }
}
Write-Output "VPM artifact validation completed successfully."
