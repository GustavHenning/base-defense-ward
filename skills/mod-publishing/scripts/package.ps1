# Build a mod and produce its Thunderstore/Nexus zip in dist\ (git-ignored) at the repo root.
# Usage: package.ps1 -Mod mods\base-defense-ward     (default: the only folder under mods\ if there is one)
# The zip has manifest.json, README.md, CHANGELOG.md and icon.png at its root and the DLL under plugins\,
# which is the layout Thunderstore and r2modman expect. The same zip is what Nexus users unpack by hand.
param([string]$Mod = "")
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")
if (-not $Mod) {
    $mods = Get-ChildItem (Join-Path $root "mods") -Directory
    if ($mods.Count -ne 1) { throw "Pass -Mod mods\<name>; found: $($mods.Name -join ', ')" }
    $Mod = "mods\$($mods[0].Name)"
}
$mod = Resolve-Path (Join-Path $root $Mod)
$csproj = Get-ChildItem $mod -Filter *.csproj | Select-Object -First 1
if (-not $csproj) { throw "No csproj in $mod" }
$manifest = Get-Content "$mod\manifest.json" -Raw | ConvertFrom-Json

# ---- validate what the mod stores will validate ----
foreach ($f in "manifest.json", "README.md", "icon.png", "CHANGELOG.md") { if (-not (Test-Path "$mod\$f")) { throw "Missing $mod\$f" } }
if ($manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$') { throw "manifest name must be [A-Za-z0-9_], no spaces: '$($manifest.name)'" }
if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { throw "version_number must be Major.Minor.Patch: '$($manifest.version_number)'" }
if ($manifest.description.Length -gt 250) { throw "description is $($manifest.description.Length) chars; max 250" }
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile("$mod\icon.png")
try { if ($icon.Width -ne 256 -or $icon.Height -ne 256) { throw "icon.png must be 256x256, is $($icon.Width)x$($icon.Height)" } } finally { $icon.Dispose() }
$csprojVersion = ([xml](Get-Content $csproj.FullName)).Project.PropertyGroup.Version
if ($csprojVersion -and $csprojVersion -ne $manifest.version_number) { throw "csproj Version $csprojVersion != manifest version_number $($manifest.version_number)" }
$pluginVersion = Select-String -Path "$mod\src\*.cs" -Pattern 'VERSION\s*=\s*"([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Select-Object -First 1
if ($pluginVersion -and $pluginVersion -ne $manifest.version_number) { throw "Plugin VERSION $pluginVersion != manifest version_number $($manifest.version_number)" }

# ---- build ----
$b = dotnet build $csproj.FullName -c Release 2>&1 | Select-String "error CS|Build succeeded"; $b
if ($b -match "error CS") { throw "build failed" }
$asm = ([xml](Get-Content $csproj.FullName)).Project.PropertyGroup.AssemblyName
$dll = Get-ChildItem "$mod\bin\Release" -Recurse -Filter "$asm.dll" | Select-Object -First 1
if (-not $dll) { throw "Built DLL $asm.dll not found under $mod\bin\Release" }

# ---- stage + zip ----
$stage = Join-Path $root "dist\stage"; Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null
Copy-Item $dll.FullName "$stage\plugins\"
Copy-Item "$mod\manifest.json", "$mod\README.md", "$mod\icon.png", "$mod\CHANGELOG.md" $stage
$zip = Join-Path $root "dist\$($manifest.name)-$($manifest.version_number).zip"
Remove-Item $zip -ErrorAction SilentlyContinue
# Compress-Archive on Windows PowerShell writes backslash entry names; build the zip by hand with '/' so every
# unzip tool (r2modman, Thunderstore, macOS) sees plugins/<Mod>.dll.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in Get-ChildItem $stage -Recurse -File) {
        $entry = $f.FullName.Substring($stage.Length + 1) -replace '\\', '/'
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Host "Packaged $zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
$zip
