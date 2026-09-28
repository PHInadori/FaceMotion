[CmdletBinding()]
param(
    [string]$RepositoryRoot = "",
    [string]$Destination = "",
    [switch]$WithModularAvatar
)

$ErrorActionPreference = "Stop"
$RetryMaxAttempts = 4
if ([string]::IsNullOrEmpty($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot "../.." }
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
if ([string]::IsNullOrEmpty($Destination)) {
    $name = if ($WithModularAvatar) { "ma" } else { "no-ma" }
    $Destination = Join-Path $RepositoryRoot "ci-host/$name"
}
$Destination = [IO.Path]::GetFullPath($Destination)
$ciRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot "ci-host"))
if (-not $Destination.StartsWith($ciRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination must be below the repository ci-host directory: $Destination"
}

function Get-VpmPackageVersion {
    param([object]$Catalog, [string]$Name, [string]$Version)

    $package = $Catalog.packages.PSObject.Properties[$Name].Value
    if ($null -eq $package) { throw "VPM catalog does not contain $Name." }
    $release = $package.versions.PSObject.Properties[$Version].Value
    if ($null -eq $release) { throw "VPM catalog does not contain $Name $Version." }
    return $release
}

function Invoke-WithRetry {
    param(
        [scriptblock]$Operation,
        [string]$Uri,
        [int]$MaxAttempts = $RetryMaxAttempts
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $Operation
        } catch {
            if ($attempt -eq $MaxAttempts) { throw }
            $delaySeconds = [int][Math]::Pow(2, $attempt)
            Write-Warning "Request failed for $Uri (attempt $attempt/$MaxAttempts): $($_.Exception.Message). Retrying in $delaySeconds seconds."
            Start-Sleep -Seconds $delaySeconds
        }
    }
}

function Install-VpmPackage {
    param([object]$Release, [string]$Target)

    $archive = Join-Path ([IO.Path]::GetTempPath()) ("facemotion-" + [guid]::NewGuid().ToString("N") + ".zip")
    try {
        Invoke-WithRetry -Uri $Release.url -Operation {
            if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
            Invoke-WebRequest -Uri $Release.url -OutFile $archive -ErrorAction Stop
        }
        $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $Release.zipSHA256.ToLowerInvariant()) { throw "Checksum mismatch for $($Release.name) $($Release.version)." }
        Expand-Archive -LiteralPath $archive -DestinationPath $Target -Force
        if (-not (Test-Path -LiteralPath (Join-Path $Target "package.json"))) { throw "VPM archive for $($Release.name) has no package.json at its root." }
    } finally {
        if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    }
}

if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $Destination "Assets") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Destination "Packages") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Destination "ProjectSettings") -Force | Out-Null

@"
m_EditorVersion: 2022.3.22f1
m_EditorVersionWithRevision: 2022.3.22f1 (887be4894c44)
"@ | Set-Content -LiteralPath (Join-Path $Destination "ProjectSettings/ProjectVersion.txt") -Encoding ASCII

$dependencies = [ordered]@{
    "com.unity.test-framework" = "1.1.33"
    "com.unity.modules.androidjni" = "1.0.0"
    "com.unity.modules.physics2d" = "1.0.0"
    "com.unity.modules.video" = "1.0.0"
    "com.facemotion.editor" = "file:../../../Packages/com.facemotion.editor"
}

$vrchatCatalogUri = "https://packages.vrchat.com/official"
$vrchatCatalog = Invoke-WithRetry -Uri $vrchatCatalogUri -Operation { Invoke-RestMethod -Uri $vrchatCatalogUri -ErrorAction Stop }
$vrchatBase = Get-VpmPackageVersion -Catalog $vrchatCatalog -Name "com.vrchat.base" -Version "3.10.5"
$vrchatAvatars = Get-VpmPackageVersion -Catalog $vrchatCatalog -Name "com.vrchat.avatars" -Version "3.10.5"
Install-VpmPackage -Release $vrchatBase -Target (Join-Path $Destination "Packages/com.vrchat.base")
Install-VpmPackage -Release $vrchatAvatars -Target (Join-Path $Destination "Packages/com.vrchat.avatars")
$dependencies["com.vrchat.base"] = "file:com.vrchat.base"
$dependencies["com.vrchat.avatars"] = "file:com.vrchat.avatars"

if ($WithModularAvatar) {
    $modularAvatarCatalogUri = "https://vpm.nadena.dev/vpm.json"
    $catalog = Invoke-WithRetry -Uri $modularAvatarCatalogUri -Operation { Invoke-RestMethod -Uri $modularAvatarCatalogUri -ErrorAction Stop }
    $ndmf = Get-VpmPackageVersion -Catalog $catalog -Name "nadena.dev.ndmf" -Version "1.14.8"
    $modularAvatar = Get-VpmPackageVersion -Catalog $catalog -Name "nadena.dev.modular-avatar" -Version "1.18.7"
    Install-VpmPackage -Release $ndmf -Target (Join-Path $Destination "Packages/nadena.dev.ndmf")
    Install-VpmPackage -Release $modularAvatar -Target (Join-Path $Destination "Packages/nadena.dev.modular-avatar")
    $dependencies["nadena.dev.ndmf"] = "file:nadena.dev.ndmf"
    $dependencies["nadena.dev.modular-avatar"] = "file:nadena.dev.modular-avatar"
}

$manifest = [ordered]@{
    dependencies = $dependencies
    testables = @("com.facemotion.editor")
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Destination "Packages/manifest.json") -Encoding UTF8
Write-Output "CI host created: $Destination (MA: $WithModularAvatar)"
