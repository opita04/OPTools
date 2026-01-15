# Creates a shortcut to the OPTools settings folder
param(
    [string]$OutputPath = ""
)

if ([string]::IsNullOrEmpty($OutputPath)) {
    Write-Error "OutputPath is required"
    exit 1
}

$settingsPath = Join-Path $env:LOCALAPPDATA "OPTools"
$shortcutPath = Join-Path $OutputPath "Open Settings Folder.lnk"

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $settingsPath
$shortcut.Description = "Open OPTools Settings Folder"
$shortcut.Save()

Write-Host "Created shortcut: $shortcutPath"
