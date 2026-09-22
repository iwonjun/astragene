param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'addons/gdUnit4'
if(Test-Path "$destination/plugin.cfg"){return}
$cache=Join-Path $root '.tools/gdunit-setup'
New-Item -ItemType Directory -Force $cache | Out-Null
Invoke-WebRequest 'https://github.com/godot-gdunit-labs/gdUnit4/archive/refs/tags/v6.2.1.zip' -OutFile "$cache/source.zip"
Expand-Archive "$cache/source.zip" "$cache/source" -Force
New-Item -ItemType Directory -Force (Join-Path $root 'addons') | Out-Null
Copy-Item -LiteralPath "$cache/source/gdUnit4-6.2.1/addons/gdUnit4" -Destination $destination -Recurse
