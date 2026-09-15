[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [string]$ProjectPath,

    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot "..\.."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "I6-baseline.json"
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable was not found: $UnityPath"
}

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$arguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $project,
    "-executeMethod", "FaceMotion.Editor.Tests.Performance.OneOffPerformanceBenchmarks.Run",
    "-performanceResults", $OutputPath,
    "-logFile", (Join-Path $outputDirectory "I6-benchmark.log")
)

$argumentLine = ($arguments | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join " "
$process = Start-Process -FilePath $UnityPath -ArgumentList $argumentLine -WorkingDirectory $project -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "I.6 benchmark exited with code $($process.ExitCode). See $(Join-Path $outputDirectory "I6-benchmark.log")."
}

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "I.6 benchmark completed without a result file: $OutputPath"
}

Get-Item -LiteralPath $OutputPath
