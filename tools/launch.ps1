# Double-click launchers (게임 실행.cmd / 에디터 열기.cmd) call this script.
# It always uses the project-local Godot 4.6.3 .NET build, so the standard (non-C#) Godot is never involved.
param([ValidateSet('game', 'editor')][string]$Mode = 'game')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
. (Join-Path $PSScriptRoot 'use_local_tools.ps1') | Out-Null
$gui = Join-Path $root '.tools/godot/Godot_v4.6.3-stable_mono_win64/Godot_v4.6.3-stable_mono_win64.exe'
Write-Host 'C# 빌드 확인 중...'
dotnet build RtsGame.sln -v q -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { Read-Host '빌드 실패. Enter를 눌러 닫기'; exit 1 }
# A crash of another Godot build leaves a recovery-mode marker; the .NET editor does not need it.
foreach ($m in @('.godot/recovery_mode', '.godot/editor/recovery_mode')) { Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $root $m) }
if ($Mode -eq 'editor') { Start-Process $gui -ArgumentList @('--path', $root, '--editor') }
else { Start-Process $gui -ArgumentList @('--path', $root) }
