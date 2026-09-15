Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-I7TempProject {
    param([Parameter(Mandatory = $true)][string]$ProjectPath, [Parameter(Mandatory = $true)][string]$WorkRoot)
    $fullProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    $fullRoot = [IO.Path]::GetFullPath($WorkRoot).TrimEnd('\', '/')
    if (-not $fullProject.StartsWith($fullRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($fullProject) -notlike "i7-*") {
        throw "Refusing to operate on non-I.7 temporary project: $fullProject"
    }
}

function Remove-I7TempProject {
    param([Parameter(Mandatory = $true)][string]$ProjectPath, [Parameter(Mandatory = $true)][string]$WorkRoot)
    Assert-I7TempProject -ProjectPath $ProjectPath -WorkRoot $WorkRoot
    if (Test-Path -LiteralPath $ProjectPath) { Remove-Item -LiteralPath $ProjectPath -Recurse -Force }
}

function Set-I7PackageLockEntry {
    param([object]$Lock, [object]$SourceLock, [string]$Name, [bool]$Present)
    if ($Present) {
        $sourceEntry = $SourceLock.dependencies.$Name
        if ($null -eq $sourceEntry) { throw "Source lock has no $Name entry." }
        $Lock.dependencies | Add-Member -NotePropertyName $Name -NotePropertyValue $sourceEntry -Force
    } else {
        $Lock.dependencies.PSObject.Properties.Remove($Name)
    }
}

function New-I7TempProject {
    param(
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][string]$WorkRoot,
        [Parameter(Mandatory = $true)][string]$Name,
        [bool]$IncludeFaceMotion = $true,
        [bool]$IncludeModularAvatar = $false
    )
    $SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
    $WorkRoot = [IO.Path]::GetFullPath($WorkRoot)
    $destination = Join-Path $WorkRoot $Name
    Assert-I7TempProject -ProjectPath $destination -WorkRoot $WorkRoot
    if (-not (Test-Path -LiteralPath $SourceRoot)) { throw "Source project not found: $SourceRoot" }
    if (-not (Test-Path -LiteralPath $WorkRoot)) { New-Item -ItemType Directory -Path $WorkRoot -Force | Out-Null }
    Remove-I7TempProject -ProjectPath $destination -WorkRoot $WorkRoot
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    Copy-Item -LiteralPath (Join-Path $SourceRoot "ProjectSettings") -Destination (Join-Path $destination "ProjectSettings") -Recurse -Force
    New-Item -ItemType Directory -Path (Join-Path $destination "Assets/Editor") -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceRoot "Tools/install-validation/templates/StandaloneStateProbe.cs") -Destination (Join-Path $destination "Assets/Editor/StandaloneStateProbe.cs") -Force
    New-Item -ItemType Directory -Path (Join-Path $destination "Packages") -Force | Out-Null

    $packageNames = @("com.vrchat.avatars", "com.vrchat.base")
    if ($IncludeFaceMotion) { $packageNames += "com.facemotion.editor" }
    if ($IncludeModularAvatar) { $packageNames += @("nadena.dev.modular-avatar", "nadena.dev.ndmf") }
    foreach ($packageName in $packageNames) {
        $source = Join-Path $SourceRoot ("Packages/" + $packageName)
        if (-not (Test-Path -LiteralPath $source)) { throw "Required embedded package missing: $source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $destination "Packages/$packageName") -Recurse -Force
    }

    $manifest = Get-Content -LiteralPath (Join-Path $SourceRoot "Packages/manifest.json") -Raw | ConvertFrom-Json
    if (-not $IncludeFaceMotion) { $manifest.testables = @() }
    $manifest | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $destination "Packages/manifest.json") -Encoding UTF8

    $sourceLock = Get-Content -LiteralPath (Join-Path $SourceRoot "Packages/packages-lock.json") -Raw | ConvertFrom-Json
    $lock = $sourceLock | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    foreach ($packageName in @("com.facemotion.editor", "nadena.dev.modular-avatar", "nadena.dev.ndmf", "jp.lilxyzw.liltoon")) {
        Set-I7PackageLockEntry -Lock $lock -SourceLock $sourceLock -Name $packageName -Present (($packageNames -contains $packageName))
    }
    $lock | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $destination "Packages/packages-lock.json") -Encoding UTF8
    return $destination
}

function Set-I7FaceMotionInstall {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$WorkRoot,
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][ValidateSet("Embedded", "Local", "Zip", "Absent")][string]$Mode,
        [string]$ZipPath = ""
    )
    Assert-I7TempProject -ProjectPath $ProjectPath -WorkRoot $WorkRoot
    $SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
    $packages = Join-Path $ProjectPath "Packages"
    $target = Join-Path $packages "com.facemotion.editor"
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    $manifestPath = Join-Path $packages "manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifest.dependencies.PSObject.Properties.Remove("com.facemotion.editor")
    if ($Mode -eq "Local") {
        $manifest.dependencies | Add-Member -NotePropertyName "com.facemotion.editor" -NotePropertyValue ("file:" + (Join-Path $SourceRoot "Packages/com.facemotion.editor").Replace('\', '/')) -Force
    }
    $manifest.testables = if ($Mode -eq "Absent") { @() } else { @("com.facemotion.editor") }
    $manifest | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    if ($Mode -eq "Embedded") {
        Copy-Item -LiteralPath (Join-Path $SourceRoot "Packages/com.facemotion.editor") -Destination $target -Recurse -Force
    } elseif ($Mode -eq "Zip") {
        if ([string]::IsNullOrEmpty($ZipPath) -or -not (Test-Path -LiteralPath $ZipPath)) { throw "VPM ZIP not found: $ZipPath" }
        $expanded = Join-Path ([IO.Path]::GetTempPath()) ("facemotion-i7-" + [guid]::NewGuid().ToString("N"))
        try {
            Expand-Archive -LiteralPath $ZipPath -DestinationPath $expanded -Force
            if (-not (Test-Path -LiteralPath (Join-Path $expanded "package.json"))) { throw "ZIP does not contain package.json at its root." }
            Copy-Item -LiteralPath $expanded -Destination $target -Recurse -Force
        } finally {
            if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
        }
    }

    $sourceLock = Get-Content -LiteralPath (Join-Path $SourceRoot "Packages/packages-lock.json") -Raw | ConvertFrom-Json
    $lockPath = Join-Path $packages "packages-lock.json"
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    Set-I7PackageLockEntry -Lock $lock -SourceLock $sourceLock -Name "com.facemotion.editor" -Present ($Mode -ne "Absent")
    $lock | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $lockPath -Encoding UTF8
}

function Invoke-I7Unity {
    param(
        [Parameter(Mandatory = $true)][string]$UnityPath,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$WorkRoot,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$ReportPath,
        [Parameter(Mandatory = $true)][string]$LogPath
    )
    Assert-I7TempProject -ProjectPath $ProjectPath -WorkRoot $WorkRoot
    if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
    $env:VRC_TEST_PROTOCOL = "0"
    $global:LASTEXITCODE = 0
    & $UnityPath -batchmode -nographics -quit -projectPath $ProjectPath -executeMethod $Method -i7Report $ReportPath -logFile $LogPath
    if ($LASTEXITCODE -ne 0) { throw "Unity executeMethod failed ($LASTEXITCODE): $Method. Log: $LogPath" }
}

Export-ModuleMember -Function Assert-I7TempProject, Remove-I7TempProject, New-I7TempProject, Set-I7FaceMotionInstall, Invoke-I7Unity
