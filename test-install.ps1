$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stage = Join-Path $root "stage"
$testRoot = Join-Path $root "install-test"

if (Test-Path $testRoot) {
    $resolvedTest = (Resolve-Path $testRoot).Path
    $resolvedRoot = (Resolve-Path $root).Path.TrimEnd('\') + '\'
    if (-not $resolvedTest.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear an unexpected test path."
    }
    Remove-Item -LiteralPath $resolvedTest -Recurse -Force
}

$publishPath = Join-Path $testRoot "AppData\Roaming\kingsoft\wps\jsaddons\publish.xml"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $publishPath) | Out-Null
[IO.File]::WriteAllText(
    $publishPath,
    '<jsplugins><jsplugin name="KeepMe" type="et" url="KeepMe_1.0.0" version="1.0.0" enable="enable_dev" install="null" customDomain=""/></jsplugins>',
    (New-Object Text.UTF8Encoding -ArgumentList $false))

& (Join-Path $stage "install.ps1") -TestRoot $testRoot

$installedXml = New-Object Xml.XmlDocument
$installedXml.Load($publishPath)
if (-not $installedXml.SelectSingleNode("//jsplugin[@name='KeepMe']")) {
    throw "Installer removed an unrelated WPS plugin entry."
}
if (-not $installedXml.SelectSingleNode("//jsplugin[@name='YNWpsTranslator']")) {
    throw "Installer did not register the WPS plugin."
}

$pluginPath = Join-Path $testRoot "AppData\Roaming\kingsoft\wps\jsaddons\YNWpsTranslator_1.0.0"
$helperPath = Join-Path $testRoot "AppData\Local\Programs\YNWpsTranslator\YNWpsTranslatorHelper.exe"
if (-not (Test-Path (Join-Path $pluginPath "ribbon.xml"))) {
    throw "Installed plugin files are incomplete."
}
if (-not (Test-Path $helperPath)) {
    throw "Installed helper executable is missing."
}

& (Join-Path $stage "uninstall.ps1") -TestRoot $testRoot

$uninstalledXml = New-Object Xml.XmlDocument
$uninstalledXml.Load($publishPath)
if (-not $uninstalledXml.SelectSingleNode("//jsplugin[@name='KeepMe']")) {
    throw "Uninstaller removed an unrelated WPS plugin entry."
}
if ($uninstalledXml.SelectSingleNode("//jsplugin[@name='YNWpsTranslator']")) {
    throw "Uninstaller did not remove the WPS plugin entry."
}
if (Test-Path $pluginPath) {
    throw "Uninstaller did not remove the WPS plugin files."
}
if (Test-Path $helperPath) {
    throw "Uninstaller did not remove the helper executable."
}

Write-Host "ISOLATED_INSTALL_UNINSTALL_OK"
