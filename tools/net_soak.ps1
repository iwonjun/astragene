# Phase 11 network validation: two real Godot processes play over ENet (optionally through the lossy UDP proxy),
# then both recorded replays are re-simulated and every tick hash is compared.
#   ./tools/net_soak.ps1 -Minutes 15
#   ./tools/net_soak.ps1 -Minutes 5 -DelayMs 200 -LossPercent 2
param(
    [double]$Minutes = 15,
    [int]$DelayMs = 0,
    [double]$LossPercent = 0,
    [int]$Port = 27415,
    [string]$Name = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'use_local_tools.ps1') | Out-Null
$godot = (Get-Alias godot).Definition
if ($Name -eq '') { $Name = if ($DelayMs -gt 0 -or $LossPercent -gt 0) { "lossy" } else { "clean" } }
$ticks = [int]($Minutes * 60 * 20)
$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$hostReplay = "user://soak_${Name}_host.agr"; $clientReplay = "user://soak_${Name}_client.agr"
$joinPort = $Port
$proxy = $null
Push-Location $root
try {
    if ($DelayMs -gt 0 -or $LossPercent -gt 0) {
        $joinPort = $Port + 1
        $proxy = Start-Process $godot -ArgumentList @('--headless','--script','res://tools/udp_lossy_proxy.gd','--',"--listen=$joinPort","--target=127.0.0.1:$Port","--delay-ms=$DelayMs","--loss=$LossPercent") -PassThru -NoNewWindow -RedirectStandardOutput "$logDir/soak_${Name}_proxy.log" -RedirectStandardError "$logDir/soak_${Name}_proxy.err.log"
        Start-Sleep -Seconds 2
    }
    $common = @('--soak-bot',"--soak-ticks=$ticks",'--auto-ready')
    $hostProc = Start-Process $godot -ArgumentList (@('--headless','res://game/scenes/lobby.tscn','--',"--host=$Port",'--auto-start','--faction=0',"--replay-out=$hostReplay") + $common) -PassThru -NoNewWindow -RedirectStandardOutput "$logDir/soak_${Name}_host.log" -RedirectStandardError "$logDir/soak_${Name}_host.err.log"
    $null = $hostProc.Handle # cache the handle so ExitCode is available after exit
    Start-Sleep -Seconds 3
    $clientProc = Start-Process $godot -ArgumentList (@('--headless','res://game/scenes/lobby.tscn','--',"--join=127.0.0.1:$joinPort",'--faction=1',"--replay-out=$clientReplay") + $common) -PassThru -NoNewWindow -RedirectStandardOutput "$logDir/soak_${Name}_client.log" -RedirectStandardError "$logDir/soak_${Name}_client.err.log"
    $null = $clientProc.Handle
    $started = Get-Date
    $limit = [int]($Minutes * 60 * 2 + 120)
    if (!$hostProc.WaitForExit($limit * 1000) -or !$clientProc.WaitForExit(60000)) { throw "Soak timed out after $limit s." }
    $wall = ((Get-Date) - $started).TotalSeconds
    $hostResult = Select-String -Path "$logDir/soak_${Name}_host.log" -Pattern '^SOAK RESULT' | Select-Object -Last 1
    $clientResult = Select-String -Path "$logDir/soak_${Name}_client.log" -Pattern '^SOAK RESULT' | Select-Object -Last 1
    if (!$hostResult -or !$clientResult) { throw 'A process ended without a SOAK RESULT line. See logs/soak_*.log.' }
    Write-Host $hostResult.Line; Write-Host $clientResult.Line
    $h = [regex]::Match($hostResult.Line, 'finalHash=(\w+)').Groups[1].Value
    $c = [regex]::Match($clientResult.Line, 'finalHash=(\w+)').Groups[1].Value
    $desync = ($hostResult.Line + $clientResult.Line) -match 'desync=1'
    $verify = @()
    foreach ($replay in @($hostReplay, $clientReplay)) {
        $out = & $godot --headless res://game/scenes/replay.tscn -- "--replay=$replay" --verify 2>&1
        $verify += ($out | Select-String -Pattern 'REPLAY (OK|MISMATCH)').Line
    }
    $verify | ForEach-Object { Write-Host $_ }
    $ok = !$desync -and $h -eq $c -and ($verify | Where-Object { $_ -like 'REPLAY OK*' }).Count -eq 2 -and $hostProc.ExitCode -eq 0 -and $clientProc.ExitCode -eq 0
    $summary = [ordered]@{ name = $Name; minutes = $Minutes; ticks = $ticks; delayMs = $DelayMs; lossPercent = $LossPercent; wallSeconds = [math]::Round($wall, 1);
        host = $hostResult.Line; client = $clientResult.Line; replays = $verify; finalHashesEqual = ($h -eq $c); passed = $ok }
    $summary | ConvertTo-Json | Set-Content -Encoding utf8 "$logDir/soak_${Name}_summary.json"
    if (!$ok) { throw "Soak failed. See logs/soak_${Name}_summary.json" }
    Write-Host "SOAK PASSED ($Name): $ticks ticks in $([math]::Round($wall,1)) s, final hash $h"
}
finally {
    if ($proxy -and !$proxy.HasExited) { Stop-Process -Id $proxy.Id -Force }
    Pop-Location
}
