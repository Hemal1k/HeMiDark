# Builds the download of a HeMi Dark release - the same on a PC and in the GitHub workflow (.github/workflows/release.yml):
#   build\dist\HeMiDark-Skins.zip  the .vlt files by colour + README.txt
# Needs: the .NET 10 SDK and the font Segoe Fluent Icons (Windows 11; the icons of the skin are drawn from it).
# The version comes from src\HeMiDark.csproj.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

$version = ([xml](Get-Content src\HeMiDark.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Add-Type -AssemblyName System.Drawing
if (-not ((New-Object System.Drawing.Text.InstalledFontCollection).Families | Where-Object Name -eq 'Segoe Fluent Icons')) {
    throw 'The font Segoe Fluent Icons is missing (https://aka.ms/SegoeFluentIcons).'
}

# the skins: build\skins\<colour>\HeMi-Dark-<colour>-<scale>.vlt
$ErrorActionPreference = 'Continue'
dotnet run --project src -c Release
$ok = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
if (-not $ok) { throw 'The skins could not be built.' }
$skins = Join-Path $root 'build\skins'

$dist = Join-Path $root 'build\dist'
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null
Write-Host "HeMi Dark $version" -ForegroundColor Cyan

$stage = Join-Path $root 'build\pack\HeMi Dark'
Remove-Item (Split-Path $stage) -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null
Copy-Item "$skins\*" $stage -Recurse
Copy-Item src\Assets\README.txt $stage
Compress-Archive $stage -DestinationPath "$dist\HeMiDark-Skins.zip"
Remove-Item (Split-Path $stage) -Recurse -Force

Get-ChildItem $dist | ForEach-Object { Write-Host ("  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
