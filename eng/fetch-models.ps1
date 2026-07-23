$ErrorActionPreference = "Stop"
Write-Host "[fetch-models] Starting..."

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LockFile = Join-Path $PSScriptRoot "models.lock.json"
$ModelsDir = Join-Path $RepoRoot "models"

if (-not (Test-Path $LockFile)) {
    Write-Error "models.lock.json not found at $LockFile"
    exit 1
}

$lock = Get-Content $LockFile -Raw | ConvertFrom-Json

# Download detection model
$detDir = Join-Path $ModelsDir "det"
New-Item -ItemType Directory -Path $detDir -Force | Out-Null

Write-Host "[fetch-models] Downloading $($lock.detModel.name)..."
foreach ($file in $lock.detModel.files.PSObject.Properties) {
    $name = $file.Name
    $info = $file.Value
    $outPath = Join-Path $detDir "inference.$name"
    if (-not (Test-Path $outPath)) {
        Write-Host "  Downloading inference.$name from $($info.url)..."
        Invoke-WebRequest -Uri $info.url -OutFile $outPath -UseBasicParsing
        Write-Host "  Saved to $outPath"
    } else {
        Write-Host "  inference.$name already exists, skipping"
    }
}

# Download recognition model
$recDir = Join-Path $ModelsDir "rec"
New-Item -ItemType Directory -Path $recDir -Force | Out-Null

Write-Host "[fetch-models] Downloading $($lock.recModel.name)..."
foreach ($file in $lock.recModel.files.PSObject.Properties) {
    $name = $file.Name
    $info = $file.Value
    $outPath = Join-Path $recDir "inference.$name"
    if (-not (Test-Path $outPath)) {
        Write-Host "  Downloading inference.$name from $($info.url)..."
        Invoke-WebRequest -Uri $info.url -OutFile $outPath -UseBasicParsing
        Write-Host "  Saved to $outPath"
    } else {
        Write-Host "  inference.$name already exists, skipping"
    }
}

Write-Host "[fetch-models] Done."
Write-Host "[fetch-models] Model files in:"
Write-Host "  Det: $detDir"
Write-Host "  Rec: $recDir"
