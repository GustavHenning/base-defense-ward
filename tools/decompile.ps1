# Decompile assembly_valheim.dll to .decomp\ (git-ignored) so you can read the real game API before patching.
# Requires: dotnet tool install -g ilspycmd --version 9.1.0.7988  (newer versions failed to install as a tool at time of writing)
param([string]$GameDir = $(if ($env:VALHEIM_DIR) { $env:VALHEIM_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Valheim" }))
$out = Join-Path (Split-Path $PSScriptRoot -Parent) ".decomp"
New-Item -ItemType Directory -Force $out | Out-Null
ilspycmd -p -o $out "$GameDir\Valheim_Data\Managed\assembly_valheim.dll"
Write-Host "Decompiled to $out ($((Get-ChildItem $out -Filter *.cs).Count) files)"
