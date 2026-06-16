param(
    [switch]$RemoveUserData,
    [string]$TestRoot = ""
)

$ErrorActionPreference = "Stop"
$pluginName = "YNWpsTranslator"
$version = "1.0.0"
$appDataRoot = if ($TestRoot) { Join-Path $TestRoot "AppData\Roaming" } else { $env:APPDATA }
$localAppDataRoot = if ($TestRoot) { Join-Path $TestRoot "AppData\Local" } else { $env:LOCALAPPDATA }
$jsAddonsRoot = Join-Path $appDataRoot "kingsoft\wps\jsaddons"
$pluginTarget = Join-Path $jsAddonsRoot ($pluginName + "_" + $version)
$publishPath = Join-Path $jsAddonsRoot "publish.xml"
$programRoot = Join-Path $localAppDataRoot "Programs\YNWpsTranslator"
$dataRoot = Join-Path $localAppDataRoot "YNWpsTranslator"
$startMenu = Join-Path $appDataRoot "Microsoft\Windows\Start Menu\Programs\YN WPS Translator"

function Assert-UnderRoot([string]$Path, [string]$Root) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove an unexpected path: $fullPath"
    }
}

if (-not $TestRoot) {
    $wpsProcesses = Get-Process et,wps,wpp -ErrorAction SilentlyContinue
    if ($wpsProcesses) {
        throw "Please close WPS Office before uninstallation, then run uninstall.cmd again."
    }
}

if (-not $TestRoot) {
    Get-Process YNWpsTranslatorHelper -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "YNWpsTranslatorHelper" -ErrorAction SilentlyContinue
}

if (Test-Path $publishPath) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($publishPath)
    $nodes = @($xml.SelectNodes("//jsplugin[@name='$pluginName'] | //jspluginonline[@name='$pluginName']"))
    foreach ($node in $nodes) {
        [void]$node.ParentNode.RemoveChild($node)
    }
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding -ArgumentList $false
    $writer = [System.Xml.XmlWriter]::Create($publishPath, $settings)
    try {
        $xml.Save($writer)
    } finally {
        $writer.Dispose()
    }
}

foreach ($path in @($pluginTarget, $programRoot, $startMenu)) {
    if (Test-Path $path) {
        if ($path -eq $pluginTarget) {
            Assert-UnderRoot $path $jsAddonsRoot
        } elseif ($path -eq $programRoot) {
            Assert-UnderRoot $path $localAppDataRoot
        } else {
            Assert-UnderRoot $path $appDataRoot
        }
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
if ($RemoveUserData -and (Test-Path $dataRoot)) {
    Assert-UnderRoot $dataRoot $localAppDataRoot
    Remove-Item -LiteralPath $dataRoot -Recurse -Force
}

Write-Host ""
Write-Host "YN WPS Translator was uninstalled."
if (-not $RemoveUserData) {
    Write-Host "Encrypted API settings and glossary were retained."
}
