# Build the Thunderstore/Nexus zip for mods\base-defense-ward into dist\ (git-ignored).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$mod = Join-Path $root "mods\base-defense-ward"
$manifest = Get-Content "$mod\manifest.json" | ConvertFrom-Json
$b = dotnet build "$mod\BaseDefenseWard.csproj" -c Release 2>&1 | Select-String "error CS|Build succeeded"; $b
if ($b -match "error CS") { throw "build failed" }
$stage = Join-Path $root "dist\stage"; Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null
Copy-Item "$mod\bin\Release\netstandard2.1\BaseDefenseWard.dll" "$stage\plugins\"
Copy-Item "$mod\manifest.json","$mod\README.md","$mod\icon.png","$mod\CHANGELOG.md" $stage
$zip = Join-Path $root "dist\$($manifest.name)-$($manifest.version_number).zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Write-Host "Packaged $zip"
