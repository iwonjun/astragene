# Double-click launchers call this. Uses the project-local standard Godot 4.6.3 (no .NET needed).
param([ValidateSet('game', 'editor', 'server')][string]$Mode = 'game')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$gui = Join-Path $root '.tools/godot-std/Godot_v4.6.3-stable_win64.exe'
$console = Join-Path $root '.tools/godot-std/Godot_v4.6.3-stable_win64_console.exe'
if (!(Test-Path $gui)) { Read-Host 'Godot 4.6.3 (표준) 이 .tools/godot-std 에 없습니다. README SETUP을 보세요. Enter로 닫기'; exit 1 }
if (!(Test-Path (Join-Path $root '.godot'))) { & $console --headless --path $root --import | Out-Null }
switch ($Mode) {
    'editor' { Start-Process $gui -ArgumentList @('--path', $root, '--editor') }
    'server' { & $console --headless --path $root -- --server }
    default { Start-Process $gui -ArgumentList @('--path', $root) }
}
