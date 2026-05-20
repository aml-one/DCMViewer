param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "DCMViewer\DCMViewer.csproj"

$outputBase = if ($Configuration -ieq "Release") { ".\bin\SingleMergeRelease\" } else { ".\bin\SingleMerge\" }
$intermediateBase = if ($Configuration -ieq "Release") { ".\obj\SingleMergeRelease\" } else { ".\obj\SingleMerge\" }

Write-Host "Building single merged DLL ($Configuration)..." -ForegroundColor Cyan

dotnet build $projectPath `
    -c $Configuration `
    -p:MergeToSingleDll=true `
    -p:OutputPath=$outputBase `
    -p:IntermediateOutputPath=$intermediateBase

if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$singleDllPath = Join-Path $repoRoot "publish\single\AmL.DCMViewer.dll"
if (Test-Path $singleDllPath) {
    Write-Host "Single DLL created: $singleDllPath" -ForegroundColor Green
} else {
    throw "Expected output not found: $singleDllPath"
}
