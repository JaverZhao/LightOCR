$ErrorActionPreference = "Stop"

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$PaddleInferenceDir = ""
)

$RepoRoot = Split-Path -Parent $PSScriptRoot
$NativeDir = Join-Path $RepoRoot "src" "LightOCR.Native"
$BuildDir = Join-Path $NativeDir "build"
$OutDir = Join-Path $BuildDir $Configuration

Write-Host "[build-native] Configuration: $Configuration"
Write-Host "[build-native] Source: $NativeDir"

if (-not (Test-Path $BuildDir)) {
    New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
}

$cmakeArgs = @(
    "-S", $NativeDir
    "-B", $BuildDir
    "-A", "x64"
    "-DCMAKE_BUILD_TYPE=$Configuration"
)

if ($PaddleInferenceDir) {
    $cmakeArgs += "-DPADDLE_INFERENCE_DIR=$PaddleInferenceDir"
}

Write-Host "[build-native] Running CMake..."
& cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed"
    exit 1
}

Write-Host "[build-native] Building..."
& cmake --build $BuildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host "[build-native] Output: $OutDir"
Write-Host "[build-native] Done."
