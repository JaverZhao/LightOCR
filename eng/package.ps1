$ErrorActionPreference = "Stop"
$Version = "1.0.0"
$RepoRoot = "J:\Javer_Workplace\dev\LightOCR"
$PublishDir = Join-Path $RepoRoot "publish"
$VersionedDir = Join-Path $PublishDir "LightOCR-$Version-win-x64"
Write-Host "[package] LightOCR v$Version packaging"

Write-Host "[package] Publishing .NET app..."
dotnet publish "$RepoRoot\src\LightOCR.App\LightOCR.App.csproj" `
    --configuration Release --runtime win-x64 --self-contained true `
    --output "$VersionedDir" -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[package] Copying native DLLs..."
Copy-Item "$RepoRoot\src\LightOCR.Native\build\Release\LightOCR.Native.dll" $VersionedDir -Force -ErrorAction SilentlyContinue
Copy-Item "$RepoRoot\runtime\onnxruntime\onnxruntime-win-x64-1.21.0\lib\onnxruntime.dll" $VersionedDir -Force -ErrorAction SilentlyContinue

Write-Host "[package] Copying models..."
$mt = Join-Path $VersionedDir "models\onnx"
New-Item -ItemType Directory -Path $mt -Force | Out-Null
if (Test-Path "$RepoRoot\models\onnx\det\inference.onnx") {
    Copy-Item "$RepoRoot\models\onnx\det\inference.onnx" (Join-Path $mt "det_inference.onnx") -Force }
if (Test-Path "$RepoRoot\models\onnx\rec\inference.onnx") {
    Copy-Item "$RepoRoot\models\onnx\rec\inference.onnx" (Join-Path $mt "rec_inference.onnx") -Force }
if (Test-Path "$RepoRoot\models\onnx\ppocrv6_dict.txt") {
    Copy-Item "$RepoRoot\models\onnx\ppocrv6_dict.txt" (Join-Path $mt "ppocrv6_dict.txt") -Force }

Set-Content -Path (Join-Path $VersionedDir "portable.flag") -Value "LightOCR Portable Mode"

Write-Host "[package] Cleaning up..."
Remove-Item (Join-Path $VersionedDir "*.pdb") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $VersionedDir "*.xml") -Force -ErrorAction SilentlyContinue

Write-Host "[package] Size report:"
$total = 0
Get-ChildItem $VersionedDir -Recurse -File | Group-Object Extension | Sort-Object Name | ForEach-Object {
    $s = ($_.Group | Measure-Object Length -Sum).Sum; $total += $s
    Write-Host ("  {0,-8} : {1,8:F2} MB" -f $_.Name, ($s/1MB))
}
Write-Host ("  {0,-8} : {1,8:F2} MB" -f "TOTAL", ($total/1MB))

Write-Host "[package] Creating ZIP..."
$zip = Join-Path $PublishDir "LightOCR-$Version-win-x64-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($VersionedDir, $zip)
Write-Host "[package] ZIP: $zip ($(('{0:N2}' -f ((Get-Item $zip).Length/1MB))) MB)"
Write-Host "[package] Done."
