param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $Version = "0.1.0",
    [switch] $SelfContained,
    [string] $InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "MultiWebView\MultiWebView.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\MultiWebView-$Version-$Runtime"
$intermediateDir = Join-Path $repoRoot "artifacts\obj\MultiWebView-$Version-$Runtime"
$installerOutDir = Join-Path $repoRoot "artifacts\installer"
$installerScript = Join-Path $repoRoot "installer\MultiWebView.iss"

New-Item -ItemType Directory -Force -Path $publishDir, $intermediateDir, $installerOutDir | Out-Null

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:BaseIntermediateOutputPath="$intermediateDir\" `
    -p:PublishDir="$publishDir\"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )

    $InnoSetupCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or -not (Test-Path $InnoSetupCompiler)) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6 or pass -InnoSetupCompiler <path-to-ISCC.exe>."
}

& $InnoSetupCompiler `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$installerOutDir" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

Write-Host "Published files: $publishDir"
Write-Host "Installer output: $installerOutDir"
