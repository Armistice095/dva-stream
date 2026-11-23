<#
.SYNOPSIS
    Spikes S1 et S2 du jalon M4 (M4-01) : fusion MP4 et lignes de progression de yt-dlp.

.DESCRIPTION
    S1 : télécharge 20 secondes d'une vidéo en 1080p (H.264 + AAC) et, si -Url4k est donné, en la plus haute
         qualité jusqu'à 2160p, fusionne en MP4 avec FFmpeg, puis décrit chaque fichier avec ffprobe (codecs,
         conteneur). Télécharge aussi l'audio M4A natif.
    S2 : télécharge en 360p une vidéo entière avec un gabarit de progression et enregistre les lignes brutes.
    Les fichiers sont écrits dans %TEMP%\dva-spike (jamais dans le dépôt). Un rapport texte est affiché et
    enregistré ; à copier dans la conversation. Ouvrir ensuite les fichiers .mp4 avec le lecteur Windows et VLC
    pour vérifier image et son.

.PARAMETER Url
    Vidéo YouTube courte ou moyenne (quelques minutes).

.PARAMETER Url4k
    Vidéo YouTube disposant du 2160p (facultatif). Seules 20 secondes sont téléchargées.

.EXAMPLE
    .\tools\spike-m4.ps1 -Url https://www.youtube.com/watch?v=p1FpH_adikQ
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$Url4k
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$engine = Join-Path $env:LOCALAPPDATA 'DvaStream\engine'
$ytdlp = Join-Path $engine 'yt-dlp.exe'
$ffprobe = Join-Path $engine 'ffprobe.exe'
foreach ($f in @($ytdlp, $ffprobe, (Join-Path $engine 'ffmpeg.exe'))) {
    if (-not (Test-Path -LiteralPath $f)) { throw "Introuvable : $f (lancer d'abord .\tools\fetch-binaries.ps1)" }
}

$out = Join-Path $env:TEMP 'dva-spike'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'

$report = New-Object System.Collections.Generic.List[string]
function Say([string]$text) { Write-Host $text; $script:report.Add($text) }

# Propriété facultative : le mode strict refuse une propriété absente (ex. l'audio Opus n'a pas de profil).
function Prop($o, [string]$name) { $p = $o.PSObject.Properties[$name]; if ($p) { $p.Value } else { '' } }

function Describe-File([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { Say "  fichier absent : $path"; return }
    $size = (Get-Item -LiteralPath $path).Length
    Say ("  fichier : {0} ({1:N1} Mo)" -f (Split-Path -Leaf $path), ($size / 1MB))
    $json = & $ffprobe -v error -show_entries 'stream=codec_type,codec_name,profile,pix_fmt,width,height:format=format_name,duration' -of json $path 2>&1 | Out-String
    try {
        $info = $json | ConvertFrom-Json
        Say ("  conteneur : {0}, durée {1} s" -f $info.format.format_name, $info.format.duration)
        foreach ($s in $info.streams) {
            if ($s.codec_type -eq 'video') {
                Say ("  image : {0} {1} {2}x{3} {4}" -f $s.codec_name, (Prop $s 'profile'), $s.width, $s.height, $s.pix_fmt)
            } else {
                Say ("  {0} : {1} {2}" -f $s.codec_type, $s.codec_name, (Prop $s 'profile'))
            }
        }
    } catch { Say "  ffprobe illisible : $json" }
}

function Run-YtDlp([string]$label, [string[]]$arguments, [string]$rawFile) {
    Say ''
    Say "=== $label ==="
    Say ('  yt-dlp ' + ($arguments -join ' '))
    $lines = & $ytdlp @arguments 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    Say "  code de sortie : $code"
    if ($rawFile) { $lines | Set-Content -LiteralPath $rawFile -Encoding UTF8 }
    return ,$lines
}

$common = @('--no-playlist', '--no-warnings', '--newline', '--ffmpeg-location', $engine)
$tpl = @(
    '--progress-template', 'download:DVAPROG|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s|%(progress.fragment_index)s|%(progress.fragment_count)s',
    '--progress-template', 'postprocess:DVAPOST|%(progress.status)s|%(progress.postprocessor)s'
)

Say ("Spike M4-01 — {0:yyyy-MM-dd HH:mm}" -f (Get-Date))
Say ("yt-dlp : " + (& $ytdlp --version))
Say ("ffmpeg : " + ((& (Join-Path $engine 'ffmpeg.exe') -version | Select-Object -First 1)))

# --- S1 : MP4 1080p H.264 + AAC (20 s) --------------------------------------------------------------------
$null = Run-YtDlp 'S1-a : 1080p H.264 + AAC, MP4, 20 s' ($common + @(
    '-f', 'bv*[vcodec^=avc1][height<=1080]+ba[acodec^=mp4a]/b[ext=mp4][height<=1080]',
    '--merge-output-format', 'mp4', '--download-sections', '*0-20',
    '-o', (Join-Path $out 's1-1080.%(ext)s'), $Url)) $null
Describe-File (Join-Path $out 's1-1080.mp4')

# --- S3 : encodeurs disponibles dans ce FFmpeg (le build LGPL n'a pas x264) --------------------------------
Say ''
Say '=== S3 : encodeurs de ce FFmpeg ==='
$enc = & (Join-Path $engine 'ffmpeg.exe') -hide_banner -encoders 2>&1 | ForEach-Object { "$_" }
foreach ($name in @('libx264', 'libmp3lame', 'aac', 'mpeg4', 'libvpx-vp9', 'libopus')) {
    $hit = $enc | Where-Object { $_ -match "^\s*[VAS][\.A-Z]{5}\s+$([regex]::Escape($name))\s" } | Select-Object -First 1
    if ($hit) { Say ("  présent : " + $hit.Trim()) } else { Say "  absent  : $name" }
}
$conf = & (Join-Path $engine 'ffmpeg.exe') -hide_banner -buildconf 2>&1 | ForEach-Object { "$_" }
Say ("  GPL activé : " + [bool]($conf -match '--enable-gpl'))

# --- S1 : audio M4A natif ----------------------------------------------------------------------------------
$null = Run-YtDlp 'S1-b : audio M4A natif (sans ré-encodage)' ($common + @(
    '-f', 'ba[ext=m4a]/ba[acodec^=mp4a]', '--download-sections', '*0-20',
    '-o', (Join-Path $out 's1-audio.%(ext)s'), $Url)) $null
Get-ChildItem -LiteralPath $out -Filter 's1-audio.*' | ForEach-Object { Describe-File $_.FullName }

# --- S1 : haute qualité (VP9/AV1) --------------------------------------------------------------------------
if ($Url4k) {
    $null = Run-YtDlp 'S1-c : meilleure image jusqu a 2160p + AAC, MP4, 20 s' ($common + @(
        '-f', 'bv*[height<=2160]+ba[acodec^=mp4a]', '--merge-output-format', 'mp4',
        '--download-sections', '*0-20', '-o', (Join-Path $out 's1-4k.%(ext)s'), $Url4k)) $null
    Get-ChildItem -LiteralPath $out -Filter 's1-4k.*' | ForEach-Object { Describe-File $_.FullName }
}

# --- S2 : lignes de progression ----------------------------------------------------------------------------
$raw = Join-Path $out 's2-progress.txt'
$lines = Run-YtDlp 'S2 : progression, 360p complet' ($common + $tpl + @(
    '-f', 'bv*[height<=360]+ba', '--merge-output-format', 'mp4',
    '-o', (Join-Path $out 's2-360.%(ext)s'), $Url)) $raw
Describe-File (Join-Path $out 's2-360.mp4')

$prog = @($lines | Where-Object { $_ -like 'DVAPROG|*' })
$post = @($lines | Where-Object { $_ -like 'DVAPOST|*' })
$other = @($lines | Where-Object { $_ -notlike 'DVAPROG|*' -and $_ -notlike 'DVAPOST|*' })
Say ("  lignes DVAPROG : {0}, DVAPOST : {1}, autres : {2}" -f $prog.Count, $post.Count, $other.Count)
Say '  gabarit : status|downloaded|total|total_estimate|speed|eta|fragment_index|fragment_count'
foreach ($l in ($prog | Select-Object -First 3)) { Say "  [début] $l" }
foreach ($l in ($prog | Select-Object -Last 3)) { Say "  [fin]   $l" }
foreach ($l in ($post | Select-Object -First 6)) { Say "  [post]  $l" }
foreach ($l in ($other | Select-Object -First 12)) { Say "  [autre] $l" }

$reportFile = Join-Path $out 'spike-report.txt'
$report | Set-Content -LiteralPath $reportFile -Encoding UTF8
Write-Host ''
Write-Host "Rapport : $reportFile"
Write-Host "Fichiers : $out (ouvrir les .mp4 avec le lecteur Windows et VLC)"
