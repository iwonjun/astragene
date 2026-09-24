# Builds Windows + Web. The Windows zip contains the web build in web/, so the host PC serves it to
# friends' browsers at http://HOST:27502 (net/web_host.gd).
#   ./tools/build_release.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
. (Join-Path $PSScriptRoot 'use_local_tools.ps1') | Out-Null
$templates = Join-Path $env:APPDATA 'Godot/export_templates/4.6.3.stable'
if (!(Test-Path (Join-Path $templates 'version.txt'))) { Write-Host "Export templates missing: $templates (README 배포 빌드)"; exit 2 }
foreach ($target in @(@('Web', 'builds/web/index.html'), @('Windows Desktop', 'builds/windows/AgeOfDominion.exe'))) {
    New-Item -ItemType Directory -Force (Split-Path $target[1] -Parent) | Out-Null
    Write-Host "Exporting $($target[0]) -> $($target[1])"
    $ErrorActionPreference = 'Continue'
    $log = godot --headless --export-release $target[0] $target[1] 2>&1 | ForEach-Object { "$_" }
    $exit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $log | Select-String 'ERROR' | ForEach-Object { Write-Host $_ }
    if ($exit -ne 0 -or !(Test-Path $target[1])) { throw "Export failed: $($target[0])" }
}
New-Item -ItemType File -Force builds/.gdignore | Out-Null
Get-ChildItem builds/web -Filter *.import | Remove-Item -Force
Remove-Item -Recurse -Force builds/windows/web -ErrorAction SilentlyContinue
Copy-Item -Recurse builds/web builds/windows/web
Compress-Archive -Force -Path builds/windows/* -DestinationPath builds/AgeOfDominion-windows.zip
Write-Host 'Done: builds/AgeOfDominion-windows.zip (with web/), builds/web/'
