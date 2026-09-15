[CmdletBinding()]
param(
    [string]$SourceRoot = "",
    [string]$WorkRoot = "",
    [string]$UnityPath = "",
    [ValidateSet("CreateNoMa", "CreateMa", "SetEmbedded", "SetLocal", "SetZip", "SetAbsent", "RunStandalone", "RunBaseSetup", "RunBaseVerify", "RunBaseMigrate", "RunBaseRecovery", "RunBaseVerifyReinstalled", "RunBaseRecoveryReinstalled", "RunMaSetup", "RunMaVerify", "RunMaMigrate", "RunMaRecovery")]
    [string]$Action,
    [string]$ProjectName,
    [string]$ZipPath = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrEmpty($SourceRoot)) { $SourceRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot "../..")) }
if ([string]::IsNullOrEmpty($WorkRoot)) { $WorkRoot = Join-Path $SourceRoot ".ci" }
if ([string]::IsNullOrEmpty($UnityPath)) { $UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe" }
if ([string]::IsNullOrEmpty($ProjectName)) { throw "-ProjectName must be an i7-* temporary project name." }

Import-Module (Join-Path $scriptRoot "I7-Helpers.psm1") -Force
$projectPath = Join-Path $WorkRoot $ProjectName
$reports = Join-Path $WorkRoot "reports"
New-Item -ItemType Directory -Path $reports -Force | Out-Null

switch ($Action) {
    "CreateNoMa" { New-I7TempProject -SourceRoot $SourceRoot -WorkRoot $WorkRoot -Name $ProjectName -IncludeFaceMotion $true -IncludeModularAvatar $false | Out-Null }
    "CreateMa" { New-I7TempProject -SourceRoot $SourceRoot -WorkRoot $WorkRoot -Name $ProjectName -IncludeFaceMotion $true -IncludeModularAvatar $true | Out-Null }
    "SetEmbedded" { Set-I7FaceMotionInstall -ProjectPath $projectPath -WorkRoot $WorkRoot -SourceRoot $SourceRoot -Mode Embedded }
    "SetLocal" { Set-I7FaceMotionInstall -ProjectPath $projectPath -WorkRoot $WorkRoot -SourceRoot $SourceRoot -Mode Local }
    "SetZip" { Set-I7FaceMotionInstall -ProjectPath $projectPath -WorkRoot $WorkRoot -SourceRoot $SourceRoot -Mode Zip -ZipPath $ZipPath }
    "SetAbsent" { Set-I7FaceMotionInstall -ProjectPath $projectPath -WorkRoot $WorkRoot -SourceRoot $SourceRoot -Mode Absent }
    "RunStandalone" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.I7Validation.StandaloneStateProbe.Run" -ReportPath (Join-Path $reports ($ProjectName + "-standalone.json")) -LogPath (Join-Path $reports ($ProjectName + "-standalone.log")) }
    "RunBaseSetup" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7SetupFixture" -ReportPath (Join-Path $reports ($ProjectName + "-base-setup.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-setup.log")) }
    "RunBaseVerify" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7VerifyState" -ReportPath (Join-Path $reports ($ProjectName + "-base-verify.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-verify.log")) }
    "RunBaseMigrate" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7MigrateLegacy" -ReportPath (Join-Path $reports ($ProjectName + "-base-migrate.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-migrate.log")) }
    "RunBaseRecovery" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7Recovery" -ReportPath (Join-Path $reports ($ProjectName + "-base-recovery.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-recovery.log")) }
    "RunBaseVerifyReinstalled" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7VerifyReinstalledState" -ReportPath (Join-Path $reports ($ProjectName + "-base-reinstall-verify.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-reinstall-verify.log")) }
    "RunBaseRecoveryReinstalled" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.InstallValidationProbe.I7RecoveryAfterReinstall" -ReportPath (Join-Path $reports ($ProjectName + "-base-reinstall-recovery.json")) -LogPath (Join-Path $reports ($ProjectName + "-base-reinstall-recovery.log")) }
    "RunMaSetup" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.ModularAvatarInstallValidationProbe.I7MaSetupFixture" -ReportPath (Join-Path $reports ($ProjectName + "-ma-setup.json")) -LogPath (Join-Path $reports ($ProjectName + "-ma-setup.log")) }
    "RunMaVerify" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.ModularAvatarInstallValidationProbe.I7MaVerifyState" -ReportPath (Join-Path $reports ($ProjectName + "-ma-verify.json")) -LogPath (Join-Path $reports ($ProjectName + "-ma-verify.log")) }
    "RunMaMigrate" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.ModularAvatarInstallValidationProbe.I7MaMigrateLegacy" -ReportPath (Join-Path $reports ($ProjectName + "-ma-migrate.json")) -LogPath (Join-Path $reports ($ProjectName + "-ma-migrate.log")) }
    "RunMaRecovery" { Invoke-I7Unity -UnityPath $UnityPath -ProjectPath $projectPath -WorkRoot $WorkRoot -Method "FaceMotion.Editor.Tests.ModularAvatarInstallValidationProbe.I7MaRecovery" -ReportPath (Join-Path $reports ($ProjectName + "-ma-recovery.json")) -LogPath (Join-Path $reports ($ProjectName + "-ma-recovery.log")) }
}

Write-Output "I.7 action completed: $Action ($projectPath)"
