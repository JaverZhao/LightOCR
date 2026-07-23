$ErrorActionPreference = "Stop"

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "[build-app] Configuration: $Configuration"

& dotnet publish "$RepoRoot\src\LightOCR.App\LightOCR.App.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output "$RepoRoot\publish\$Configuration"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host "[build-app] Done."
