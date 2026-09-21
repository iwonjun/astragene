# Dot-source in PowerShell: . ./tools/use_local_tools.ps1
$projectRoot = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $projectRoot '.tools/dotnet'
$localGodot = Join-Path $projectRoot '.tools/godot/Godot_v4.6.3-stable_mono_win64/Godot_v4.6.3-stable_mono_win64_console.exe'
if (!(Test-Path -LiteralPath "$localSdk/dotnet.exe") -or !(Test-Path -LiteralPath $localGodot)) {
    throw 'Local tools are missing. See README SETUP.'
}
# Godot's SDK discovery can corrupt non-ASCII SDK paths on Windows.
# Keep the SDK in the project and expose it through an ASCII junction.
$sdkAlias = Join-Path $env:LOCALAPPDATA 'Codex/Toolchains/rts-dotnet-8'
if (!(Test-Path -LiteralPath $sdkAlias)) {
    New-Item -ItemType Directory -Force (Split-Path $sdkAlias -Parent) | Out-Null
    New-Item -ItemType Junction -Path $sdkAlias -Target $localSdk | Out-Null
}
$aliasInfo = Get-Item -LiteralPath $sdkAlias
if ($aliasInfo.LinkType -ne 'Junction' -or $aliasInfo.Target -ne $localSdk) {
    throw "SDK alias already exists with a different target: $sdkAlias"
}
$env:DOTNET_ROOT = $sdkAlias
$env:PATH = "$sdkAlias;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Set-Alias -Name godot -Value $localGodot -Scope Global
Write-Host 'Using project-local .NET 8 SDK and Godot 4.6.3 .NET.'
