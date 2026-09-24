# Dot-source in PowerShell: . ./tools/use_local_tools.ps1
# Uses the project-local standard (non-.NET) Godot 4.6.3 in .tools/godot-std, else `godot` on PATH.
$projectRoot = Split-Path $PSScriptRoot -Parent
$local = Join-Path $projectRoot '.tools/godot-std/Godot_v4.6.3-stable_win64_console.exe'
if (Test-Path -LiteralPath $local) { Set-Alias -Name godot -Value $local -Scope Global; Write-Host 'Using project-local Godot 4.6.3 (standard).' }
elseif (Get-Command godot -ErrorAction SilentlyContinue) { Write-Host 'Using godot from PATH.' }
else { throw 'Godot 4.6.3 standard build not found. See README SETUP.' }
