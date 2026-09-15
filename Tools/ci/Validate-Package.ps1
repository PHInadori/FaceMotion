[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot "../.."),
    [string]$PackageRelativePath = "Packages/com.facemotion.editor"
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    Write-Output "PASS  $Message"
}

function Read-JsonFile {
    param([string]$Path)
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { throw "Invalid JSON: $Path`n$($_.Exception.Message)" }
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$PackagePath = Join-Path $RepositoryRoot $PackageRelativePath
Assert-True (Test-Path -LiteralPath $PackagePath) "Package directory exists"

$manifestPath = Join-Path $PackagePath "package.json"
$manifest = Read-JsonFile $manifestPath
$semVer = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'

Assert-True ($manifest.name -eq "com.facemotion.editor") "Package name is com.facemotion.editor"
Assert-True ($manifest.version -match $semVer) "Package version is valid SemVer ($($manifest.version))"
Assert-True ($manifest.unity -eq "2022.3") "Unity version is 2022.3"
Assert-True ($manifest.author.name -eq "PHInadori") "Author name is PHInadori"
Assert-True ($manifest.author.email -eq "phinadori@gmail.com") "Author email is phinadori@gmail.com"
Assert-True ($manifest.license -eq "MIT") "Package license is MIT"
Assert-True ($manifest.vpmDependencies."com.vrchat.avatars" -eq "3.10.5") "VRCSDK dependency is pinned to 3.10.5"
Assert-True (-not $manifest.vpmDependencies.PSObject.Properties.Name.Contains("nadena.dev.modular-avatar")) "Modular Avatar is not a required VPM dependency"
Assert-True (-not $manifest.PSObject.Properties.Name.Contains("zipSHA256")) "Package manifest does not embed artifact checksum"

$required = @("package.json", "Editor", "Documentation~", "README.md", "CHANGELOG.md", "LICENSE.md", "THIRD-PARTY-NOTICES.md")
foreach ($item in $required) { Assert-True (Test-Path -LiteralPath (Join-Path $PackagePath $item)) "Required package content exists: $item" }
Assert-True (-not (Test-Path -LiteralPath (Join-Path $PackagePath "Packages"))) "Package has no nested Packages directory"

$forbiddenPatterns = @("Library", "Temp", "Logs", "obj", "UserSettings", "TestResults", ".vs", ".idea", ".vscode", ".github", "*.csproj", "*.sln", "*.user", "*.suo", "*.opendb", "Yumeka", "lilToon", "liltoon", "__FaceMotionTests_*")
$forbidden = @()
Get-ChildItem -LiteralPath $PackagePath -Recurse -Force | ForEach-Object {
    foreach ($pattern in $forbiddenPatterns) {
        if ($_.Name -like $pattern) { $forbidden += $_.FullName; break }
    }
}
Assert-True ($forbidden.Count -eq 0) "No forbidden or generated package content"

$versionsPath = Join-Path $PackagePath "Editor/Core/Versioning/FaceMotionVersions.cs"
$versions = Get-Content -LiteralPath $versionsPath -Raw
foreach ($constant in @("ProjectSchemaVersion", "MappingProfileSchemaVersion", "IntegrationManifestVersion")) {
    Assert-True ($versions -match "public const int $constant = 1;") "Schema guard $constant is 1"
}
Assert-True ($versions -match "public const string ToolVersion = `"$([regex]::Escape($manifest.version))`";") "ToolVersion matches package version"

$changeLog = Get-Content -LiteralPath (Join-Path $PackagePath "CHANGELOG.md") -Raw
Assert-True ($changeLog -match "(?m)^##?\s*\[?$([regex]::Escape($manifest.version))\]?") "CHANGELOG contains package version"
$license = Get-Content -LiteralPath (Join-Path $PackagePath "LICENSE.md") -Raw
Assert-True ($license -match "MIT License" -and $license -match "PHInadori") "LICENSE contains MIT and PHInadori"

$listingPath = Join-Path $RepositoryRoot "Tools/vpm-listing-template"
foreach ($listingName in @("source.json", "index.json")) {
    $listing = Read-JsonFile (Join-Path $listingPath $listingName)
    Assert-True ($listing.id -eq "com.phinadori.vpm") "$listingName has planned listing ID"
    Assert-True ($listing.author.name -eq $manifest.author.name -and $listing.author.email -eq $manifest.author.email) "$listingName author matches package metadata"
    Assert-True ($listing.url -notmatch "EXAMPLE|TODO|CHANGEME|USER_DECISION_REQUIRED") "$listingName has no unresolved placeholder marker"
}

$markdownFiles = Get-ChildItem -LiteralPath $PackagePath -Recurse -File -Filter "*.md"
$brokenLinks = @()
foreach ($file in $markdownFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '\]\(([^)#]+)(?:#[^)]+)?\)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?:|mailto:|#)') { continue }
        $targetPath = Join-Path $file.DirectoryName ($target -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $targetPath)) { $brokenLinks += "$($file.FullName): $target" }
    }
}
Assert-True ($brokenLinks.Count -eq 0) "Markdown local links resolve"
Write-Output "Package validation completed successfully."
