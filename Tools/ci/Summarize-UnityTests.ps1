[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactDirectory,
    [string]$ResultsPath = "",
    [string]$UnityLogPath = "",
    [switch]$NoMa
)

$ErrorActionPreference = "Stop"
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    throw "Unity test artifacts are unavailable at '$ArtifactDirectory'. The Unity test runner failed before producing results; inspect the preceding runner step."
}
$result = $null
$xml = $null
if ($ResultsPath) {
    $result = Get-Item -LiteralPath $ResultsPath
    [xml]$xml = Get-Content -LiteralPath $result.FullName -Raw
} else {
    foreach ($candidate in Get-ChildItem -LiteralPath $ArtifactDirectory -Recurse -File -Filter "*.xml") {
        try { [xml]$candidateXml = Get-Content -LiteralPath $candidate.FullName -Raw } catch { continue }
        if ($null -ne $candidateXml.'test-run') {
            $result = $candidate
            $xml = $candidateXml
            break
        }
    }
}
if ($null -eq $result) { throw "No NUnit test-result XML found below $ArtifactDirectory" }
$run = $xml.'test-run'
if ($null -eq $run) { throw "Test result does not contain test-run: $($result.FullName)" }
$total = [int]$run.total
$passed = [int]$run.passed
$failed = [int]$run.failed
$skipped = [int]$run.skipped
if ($total -le 0) { throw "Test run reported zero tests." }

$logs = if ($UnityLogPath) { @(Get-Item -LiteralPath $UnityLogPath) } else { @(Get-ChildItem -LiteralPath $ArtifactDirectory -Recurse -File | Where-Object { $_.Name -match '(?i)(editor|unity).*\.log$|\.log$' }) }
if ($logs.Count -eq 0) { throw "No Unity log found below $ArtifactDirectory; cannot enforce compile/warning policy." }
$logLines = $logs | ForEach-Object { Get-Content -LiteralPath $_.FullName }
$compileErrors = @($logLines | Where-Object { $_ -match 'error CS|Scripts have compiler errors' })
$thirdPartyWarningPattern = 'nadena|modular.?avatar|ndmf|liltoon|lilToon|VRC\.SDK|VRChat'
$faceMotionWarnings = @($logLines | Where-Object {
    $_ -match 'warning CS' -and
    $_ -match 'FaceMotion|com\.facemotion|Assets[\\/]' -and
    $_ -notmatch $thirdPartyWarningPattern
})
if ($NoMa) {
    $maCompiled = @($logLines | Where-Object { $_ -match 'FaceMotion\.Editor\.Tests\.ModularAvatar\.dll|FaceMotion\.Editor\.ModularAvatar\.dll' })
    if ($maCompiled.Count -gt 0) { throw "No-MA smoke unexpectedly compiled Modular Avatar assemblies." }
    $missingMa = @($logLines | Where-Object { $_ -match 'nadena\.dev\.modular-avatar|ModularAvatar.*(could not|not found|missing)' })
    if ($missingMa.Count -gt 0) { throw "No-MA smoke reported a Modular Avatar resolution issue: $($missingMa[0])" }
}

$summary = @(
    "## Unity EditMode Results",
    "",
    "| Total | Passed | Failed | Skipped |",
    "| ---: | ---: | ---: | ---: |",
    "| $total | $passed | $failed | $skipped |",
    "",
    "- Result XML: ``$($result.Name)``",
    "- Compile errors: $($compileErrors.Count)",
    "- FaceMotion/Assets CS warnings: $($faceMotionWarnings.Count)"
) -join [Environment]::NewLine
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary }
Write-Output $summary
if ($compileErrors.Count -gt 0) { throw "Unity log contains compile errors." }
if ($faceMotionWarnings.Count -gt 0) { throw "Unity log contains FaceMotion/Assets CS warnings." }
if ($failed -ne 0) { throw "Test run reported $failed failures." }
