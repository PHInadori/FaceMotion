[CmdletBinding()]
param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot "../.."),
    [string]$Destination = (Join-Path $PSScriptRoot "../../.ci/no-ma-smoke")
)

$ErrorActionPreference = "Stop"
$SourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$Destination = [System.IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Recurse -Force }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

foreach ($name in @("Assets", "Packages", "ProjectSettings")) {
    $source = Join-Path $SourceRoot $name
    if (-not (Test-Path -LiteralPath $source)) { throw "Required Unity project directory is missing: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $name) -Recurse -Force
}

$packages = Join-Path $Destination "Packages"
foreach ($optionalPackage in @("nadena.dev.modular-avatar", "nadena.dev.ndmf")) {
    $path = Join-Path $packages $optionalPackage
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

$lockPath = Join-Path $packages "packages-lock.json"
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
foreach ($optionalPackage in @("nadena.dev.modular-avatar", "nadena.dev.ndmf")) {
    $lock.dependencies.PSObject.Properties.Remove($optionalPackage)
}
$lock | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $lockPath -Encoding UTF8

$vpmPath = Join-Path $packages "vpm-manifest.json"
if (Test-Path -LiteralPath $vpmPath) {
    $vpm = Get-Content -LiteralPath $vpmPath -Raw | ConvertFrom-Json
    foreach ($optionalPackage in @("nadena.dev.modular-avatar", "nadena.dev.ndmf")) {
        $vpm.dependencies.PSObject.Properties.Remove($optionalPackage)
        $vpm.locked.PSObject.Properties.Remove($optionalPackage)
    }
    $vpm | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $vpmPath -Encoding UTF8
}

foreach ($optionalPackage in @("nadena.dev.modular-avatar", "nadena.dev.ndmf")) {
    if (Test-Path -LiteralPath (Join-Path $packages $optionalPackage)) { throw "Optional package remains in smoke project: $optionalPackage" }
}
Write-Output "No-MA smoke project created: $Destination"
