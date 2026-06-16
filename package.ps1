$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stage = Join-Path $root "stage"
$sourceStage = Join-Path $root "source-stage"
$outputs = Join-Path $root "outputs"

& (Join-Path $root "helper\build.ps1")

if (Test-Path $stage) {
    $resolvedStage = (Resolve-Path $stage).Path
    $resolvedRoot = (Resolve-Path $root).Path
    if (-not $resolvedStage.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear an unexpected staging path."
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "plugin") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "helper") | Out-Null
New-Item -ItemType Directory -Force -Path $outputs | Out-Null

Copy-Item -Path (Join-Path $root "plugin\*") -Destination (Join-Path $stage "plugin") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "helper\bin\YNWpsTranslatorHelper.exe") -Destination (Join-Path $stage "helper") -Force
Copy-Item -Path (Join-Path $root "distribution\*") -Destination $stage -Recurse -Force

$zipPath = Join-Path $outputs "YN-WPS-Indonesian-Translator-1.0.0.zip"
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Created $zipPath"

if (Test-Path $sourceStage) {
    $resolvedSourceStage = (Resolve-Path $sourceStage).Path
    $resolvedRoot = (Resolve-Path $root).Path
    if (-not $resolvedSourceStage.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear an unexpected source staging path."
    }
    Remove-Item -LiteralPath $resolvedSourceStage -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $sourceStage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sourceStage "plugin") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sourceStage "helper\src") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sourceStage "distribution") | Out-Null
Copy-Item -Path (Join-Path $root "plugin\*") -Destination (Join-Path $sourceStage "plugin") -Recurse -Force
Copy-Item -Path (Join-Path $root "helper\src\*") -Destination (Join-Path $sourceStage "helper\src") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "helper\build.ps1") -Destination (Join-Path $sourceStage "helper") -Force
Copy-Item -Path (Join-Path $root "distribution\*") -Destination (Join-Path $sourceStage "distribution") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "package.ps1") -Destination $sourceStage -Force
Copy-Item -LiteralPath (Join-Path $root "test-install.ps1") -Destination $sourceStage -Force
Copy-Item -LiteralPath (Join-Path $root "DEVELOPMENT.md") -Destination $sourceStage -Force

$sourceZipPath = Join-Path $outputs "YN-WPS-Indonesian-Translator-Source-1.0.0.zip"
if (Test-Path $sourceZipPath) {
    Remove-Item -LiteralPath $sourceZipPath -Force
}
Compress-Archive -Path (Join-Path $sourceStage "*") -DestinationPath $sourceZipPath -CompressionLevel Optimal
Write-Host "Created $sourceZipPath"
