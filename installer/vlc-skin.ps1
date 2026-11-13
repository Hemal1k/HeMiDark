# Sets or resets the skin of VLC for the current user (%APPDATA%\vlc\vlcrc), used by the HeMi Dark installer:
#   vlc-skin.ps1 -Skin <path.vlt>     VLC starts with this skin (intf=skins2,any + skins2-last)
#   vlc-skin.ps1 -Reset -Folder <dir> back to the usual look, if the skin in use is one from <dir>
#   vlc-skin.ps1 -Running             exit code 1 while VLC runs (it writes vlcrc when it closes)
param([string]$Skin, [switch]$Reset, [string]$Folder, [switch]$Running)
$ErrorActionPreference = 'Stop'

if ($Running) {
    if (Get-Process vlc -ErrorAction SilentlyContinue) { exit 1 }
    exit 0
}

# VLC writes vlcrc as UTF-8 with a BOM and LF line ends: kept the same
$file = Join-Path $env:APPDATA 'vlc\vlcrc'
$bom = $true
$lines = New-Object System.Collections.Generic.List[string]
if (Test-Path $file) {
    $bytes = [System.IO.File]::ReadAllBytes($file)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = (New-Object System.Text.UTF8Encoding $false).GetString($bytes, $(if ($bom) { 3 } else { 0 }), $bytes.Length - $(if ($bom) { 3 } else { 0 }))
    $lines.AddRange([string[]]($text -split '\r?\n'))
    if ($lines.Count -and $lines[$lines.Count - 1] -eq '') { $lines.RemoveAt($lines.Count - 1) }
}

# replaces the first line of the option (set or commented out), or adds it at the end
function Put([string]$name, [string]$line) {
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^#?$([regex]::Escape($name))=") { $lines[$i] = $line; return }
    }
    $lines.Add($line)
}

if ($Skin) {
    Put 'intf' 'intf=skins2,any'
    Put 'skins2-last' "skins2-last=$Skin"
}
elseif ($Reset) {
    $last = $lines | Where-Object { $_ -like 'skins2-last=*' } | Select-Object -First 1
    if (-not $last) { exit 0 }
    $path = $last.Substring('skins2-last='.Length)
    if (-not $Folder -or -not $path.StartsWith($Folder.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
    Put 'intf' '#intf='
    Put 'skins2-last' '#skins2-last='
}
else { exit 2 }

New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
[System.IO.File]::WriteAllText($file, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $bom))
