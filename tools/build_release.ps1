# Release builds for Windows / Linux / macOS from export_presets.cfg.
#   ./tools/build_release.ps1            (all platforms)
#   ./tools/build_release.ps1 -Platform windows
# Requires Godot 4.6.3 .NET export templates (see README "배포 빌드").
param([ValidateSet('all', 'windows', 'linux', 'macos')][string]$Platform = 'all')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
. (Join-Path $PSScriptRoot 'use_local_tools.ps1') | Out-Null
$templates = Join-Path $env:APPDATA 'Godot/export_templates/4.6.3.stable.mono'
if (!(Test-Path (Join-Path $templates 'version.txt'))) {
    Write-Host "Export templates missing: $templates"
    Write-Host 'Install: Godot editor > Editor > Manage Export Templates > Download and Install (.NET 4.6.3),'
    Write-Host 'or extract Godot_v4.6.3-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.6.3-stable/'
    exit 2
}
$presets = [ordered]@{ windows = @('Windows Desktop', 'builds/windows/Astragene.exe'); linux = @('Linux', 'builds/linux/Astragene.x86_64'); macos = @('macOS', 'builds/macos/Astragene.zip') }
dotnet build RtsGame.sln -c Release -v q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
foreach ($key in $presets.Keys) {
    if ($Platform -ne 'all' -and $Platform -ne $key) { continue }
    $name, $path = $presets[$key]
    New-Item -ItemType Directory -Force (Split-Path $path -Parent) | Out-Null
    Write-Host "Exporting $name -> $path"
    # Godot writes warnings to stderr; keep them as log lines instead of terminating errors (PowerShell 5.1).
    $ErrorActionPreference = 'Continue'
    $log = godot --headless --export-release $name $path 2>&1 | ForEach-Object { "$_" }
    $exit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $log | Select-String -Pattern 'ERROR' | ForEach-Object { Write-Host $_ }
    if ($exit -ne 0 -or !(Test-Path $path)) { throw "Export failed: $name" }
    if ($key -ne 'macos') { Compress-Archive -Force -Path (Join-Path (Split-Path $path -Parent) '*') -DestinationPath "builds/Astragene-$key.zip" }
}
Write-Host 'Release builds are in builds/.'
