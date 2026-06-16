param(
    [string]$TestRoot = ""
)

$ErrorActionPreference = "Stop"

$pluginName = "YNWpsTranslator"
$version = "1.0.0"
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePlugin = Join-Path $packageRoot "plugin"
$sourceHelper = Join-Path $packageRoot "helper\YNWpsTranslatorHelper.exe"
$appDataRoot = if ($TestRoot) { Join-Path $TestRoot "AppData\Roaming" } else { $env:APPDATA }
$localAppDataRoot = if ($TestRoot) { Join-Path $TestRoot "AppData\Local" } else { $env:LOCALAPPDATA }
$jsAddonsRoot = Join-Path $appDataRoot "kingsoft\wps\jsaddons"
$pluginTarget = Join-Path $jsAddonsRoot ($pluginName + "_" + $version)
$programRoot = Join-Path $localAppDataRoot "Programs\YNWpsTranslator"
$helperTarget = Join-Path $programRoot "YNWpsTranslatorHelper.exe"
$publishPath = Join-Path $jsAddonsRoot "publish.xml"

if (-not (Test-Path $sourcePlugin)) {
    throw "Plugin files are missing."
}
if (-not (Test-Path $sourceHelper)) {
    throw "Helper executable is missing."
}

if (-not $TestRoot) {
    $wpsProcesses = Get-Process et,wps,wpp -ErrorAction SilentlyContinue
    if ($wpsProcesses) {
        throw "Please close WPS Office before installation, then run install.cmd again."
    }
}

New-Item -ItemType Directory -Force -Path $jsAddonsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $programRoot | Out-Null

if (Test-Path $pluginTarget) {
    $resolvedTarget = (Resolve-Path $pluginTarget).Path
    $resolvedRoot = (Resolve-Path $jsAddonsRoot).Path
    if (-not $resolvedTarget.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace an unexpected plugin path."
    }
    Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}

Copy-Item -LiteralPath $sourcePlugin -Destination $pluginTarget -Recurse -Force
Copy-Item -LiteralPath $sourceHelper -Destination $helperTarget -Force

$xml = New-Object System.Xml.XmlDocument
if (Test-Path $publishPath) {
    try {
        $xml.Load($publishPath)
    } catch {
        Copy-Item -LiteralPath $publishPath -Destination ($publishPath + ".invalid.bak") -Force
        $xml.LoadXml("<jsplugins />")
    }
} else {
    $xml.LoadXml("<jsplugins />")
}

if ($xml.DocumentElement.Name -ne "jsplugins") {
    throw "Unexpected WPS publish.xml root element."
}

$existing = @($xml.SelectNodes("//jsplugin[@name='$pluginName'] | //jspluginonline[@name='$pluginName']"))
foreach ($node in $existing) {
    [void]$node.ParentNode.RemoveChild($node)
}

$pluginNode = $xml.CreateElement("jsplugin")
$attributes = @{
    name = $pluginName
    type = "et"
    url = ($pluginName + "_" + $version)
    version = $version
    enable = "enable_dev"
    install = "null"
    customDomain = ""
}
foreach ($key in $attributes.Keys) {
    $pluginNode.SetAttribute($key, $attributes[$key])
}
[void]$xml.DocumentElement.AppendChild($pluginNode)

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding -ArgumentList $false
$writer = [System.Xml.XmlWriter]::Create($publishPath, $settings)
try {
    $xml.Save($writer)
} finally {
    $writer.Dispose()
}

if (-not $TestRoot) {
    $runPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    New-Item -Path $runPath -Force | Out-Null
    Set-ItemProperty -Path $runPath -Name "YNWpsTranslatorHelper" -Value ('"{0}"' -f $helperTarget)

    $startMenu = Join-Path $appDataRoot "Microsoft\Windows\Start Menu\Programs\YN WPS Translator"
    New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $startShortcut = $shell.CreateShortcut((Join-Path $startMenu "Start Translator Helper.lnk"))
    $startShortcut.TargetPath = $helperTarget
    $startShortcut.WorkingDirectory = $programRoot
    $startShortcut.Save()
    $uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenu "Uninstall.lnk"))
    $uninstallShortcut.TargetPath = "powershell.exe"
    $uninstallShortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $packageRoot "uninstall.ps1") + '"'
    $uninstallShortcut.WorkingDirectory = $packageRoot
    $uninstallShortcut.Save()

    Get-Process YNWpsTranslatorHelper -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Process -FilePath $helperTarget -WindowStyle Hidden
}

Write-Host ""
Write-Host "YN WPS Translator was installed successfully."
Write-Host "Open WPS Spreadsheet and use the new translation ribbon tab."
