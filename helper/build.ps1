$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$bin = Join-Path $root "bin"
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) {
    throw ".NET Framework C# compiler was not found."
}

New-Item -ItemType Directory -Force -Path $bin | Out-Null
$sources = Get-ChildItem $src -Filter "*.cs" | Select-Object -ExpandProperty FullName

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /out:"$bin\YNWpsTranslatorHelper.exe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Security.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Helper compilation failed."
}

Write-Host "Built $bin\YNWpsTranslatorHelper.exe"
