param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/Sim/Sim.csproj'
$probe = Join-Path $root 'src/Sim/ForbiddenGodotProbe.cs'
if (Test-Path -LiteralPath $probe) { throw 'Probe file already exists; refusing to overwrite.' }
try {
    'using Godot;' | Set-Content -LiteralPath $probe -Encoding utf8
    $log = & $Dotnet build $project --no-restore 2>&1
    $code = $LASTEXITCODE
    if ($code -eq 0 -or ($log -join "`n") -notmatch 'SIM002') {
        throw "Expected SIM002 failure. Actual output: $log"
    }
    Write-Host 'PASS: using Godot; is rejected with SIM002.'
} finally {
    Remove-Item -LiteralPath $probe -ErrorAction SilentlyContinue
}
& $Dotnet build $project --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Clean Sim build failed.' }
