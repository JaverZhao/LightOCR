$ErrorActionPreference = "Stop"
Write-Host "[fetch-dependencies] Starting..."

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LockFile = Join-Path $PSScriptRoot "dependencies.lock.json"
$RuntimeDir = Join-Path $RepoRoot "runtime"

if (-not (Test-Path $LockFile)) {
    Write-Error "dependencies.lock.json not found at $LockFile"
    exit 1
}

$lock = Get-Content $LockFile -Raw | ConvertFrom-Json

# Download Paddle Inference
$paddleDir = Join-Path $RuntimeDir "paddle"
New-Item -ItemType Directory -Path $paddleDir -Force | Out-Null

$paddleZip = Join-Path $RuntimeDir "paddle_inference.zip"
if (-not (Test-Path $paddleZip)) {
    Write-Host "[fetch-dependencies] Downloading Paddle Inference $($lock.paddleInference.version)..."
    Write-Host "  URL: $($lock.paddleInference.url)"
    Invoke-WebRequest -Uri $lock.paddleInference.url -OutFile $paddleZip -UseBasicParsing
    Write-Host "  Saved to $paddleZip"
}
else {
    Write-Host "[fetch-dependencies] Paddle Inference zip already exists"
}

Write-Host "[fetch-dependencies] Extracting Paddle Inference..."
Expand-Archive -Path $paddleZip -DestinationPath $paddleDir -Force
Write-Host "  Extracted to $paddleDir"

# Download OpenCV (optional at this stage)
$opencvDir = Join-Path $RuntimeDir "opencv"
if (-not (Test-Path (Join-Path $opencvDir "build"))) {
    Write-Host "[fetch-dependencies] OpenCV not yet downloaded"
    Write-Host "  URL: $($lock.opencv.url)"
    Write-Host "  Download manually and extract to $opencvDir, or use vcpkg/chocolatey"
    Write-Host "  Tip: 'choco install opencv --params=\"'/InstallDir:$opencvDir'\"'"
}
else {
    Write-Host "[fetch-dependencies] OpenCV found at $opencvDir"
}

Write-Host "[fetch-dependencies] Done."
Write-Host ""
Write-Host "Summary:"
Get-ChildItem -Path $RuntimeDir -Recurse -Depth 2 | Where-Object { -not $_.PSIsContainer } | ForEach-Object { "  $($_.FullName.Replace($RepoRoot,'')) ($(($_.Length/1MB).ToString('F2')) MB)" }
