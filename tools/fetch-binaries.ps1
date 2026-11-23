<#
.SYNOPSIS
    Télécharge yt-dlp pour le développement dans le dossier du moteur de DVA Stream (M2-03).

.DESCRIPTION
    Récupère le binaire officiel depuis les releases GitHub de yt-dlp, vérifie sa somme SHA-256 avec le fichier
    SHA2-256SUMS publié avec la même release, puis l'installe dans <DataDirectory>/engine/ (celui que l'application
    utilise). La signature GPG du fichier de sommes n'est pas vérifiée : ce contrôle protège contre un téléchargement
    corrompu, pas contre une release compromise.
    Télécharge aussi FFmpeg et ffprobe (build LGPL de BtbN, série 8.1) dans le même dossier, somme SHA-256 vérifiée
    avec le fichier checksums.sha256 de la release. Sous macOS aucun build LGPL de ce type n'existe : installer
    FFmpeg à la main (brew install ffmpeg) et copier ffmpeg et ffprobe dans le dossier du moteur.

.PARAMETER OutDir
    Dossier d'installation. Par défaut : le dossier du moteur de DVA Stream pour l'OS courant.

.PARAMETER Version
    'latest' (défaut) ou une étiquette de release, par ex. 2026.10.26.

.PARAMETER SkipFfmpeg
    Ne télécharge que yt-dlp.

.EXAMPLE
    .\tools\fetch-binaries.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [string]$Version = 'latest',
    [switch]$SkipFfmpeg
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch { }

$onWindows = ($PSVersionTable.PSEdition -eq 'Desktop') -or ($PSVersionTable.Platform -eq 'Win32NT')
# $IsMacOS n'existe pas dans Windows PowerShell 5.1 (et le mode strict interdit les variables non définies).
$onMac = $false
if (-not $onWindows) { $onMac = [bool](Get-Variable -Name IsMacOS -ValueOnly -ErrorAction SilentlyContinue) }
# Windows PowerShell 5.1 n'a pas toujours RuntimeInformation.OSArchitecture (ancien .NET Framework) :
# sous Windows on lit les variables d'environnement (PROCESSOR_ARCHITEW6432 donne l'architecture réelle
# quand PowerShell tourne en 32 bits sur un Windows 64 bits).
if ($onWindows) {
    $rawArch = $env:PROCESSOR_ARCHITEW6432
    if (-not $rawArch) { $rawArch = $env:PROCESSOR_ARCHITECTURE }
    switch ($rawArch) {
        'AMD64' { $arch = 'X64' }
        'ARM64' { $arch = 'Arm64' }
        default { $arch = "$rawArch" }
    }
} else {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
}

# --- Quel fichier, pour quel OS --------------------------------------------------------------------------
if ($onWindows) {
    $assets = @{ 'X64' = 'yt-dlp.exe'; 'Arm64' = 'yt-dlp_arm64.exe' }
    $target = 'yt-dlp.exe'
    if (-not $OutDir) { $OutDir = Join-Path $env:LOCALAPPDATA 'DvaStream\engine' }
} elseif ($onMac) {
    $assets = @{ 'X64' = 'yt-dlp_macos'; 'Arm64' = 'yt-dlp_macos' }
    $target = 'yt-dlp'
    if (-not $OutDir) { $OutDir = Join-Path $HOME 'Library/Application Support/DvaStream/engine' }
} else {
    $assets = @{ 'X64' = 'yt-dlp_linux'; 'Arm64' = 'yt-dlp_linux_aarch64' }
    $target = 'yt-dlp'
    if (-not $OutDir) {
        $dataHome = $env:XDG_DATA_HOME
        if (-not $dataHome) { $dataHome = Join-Path $HOME '.local/share' }
        $OutDir = Join-Path $dataHome 'DvaStream/engine'
    }
}

if (-not $assets.ContainsKey($arch)) { throw "Architecture non prise en charge par ce script : $arch" }
$asset = $assets[$arch]

if ($Version -eq 'latest') {
    $baseUrl = 'https://github.com/yt-dlp/yt-dlp/releases/latest/download'
} else {
    $baseUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/$Version"
}

# --- Téléchargement et vérification ----------------------------------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('dva-ytdlp-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $binary = Join-Path $work $asset
    $sums = Join-Path $work 'SHA2-256SUMS'

    Write-Host "Téléchargement de $asset ($Version)..."
    Invoke-WebRequest -Uri "$baseUrl/$asset" -OutFile $binary -UseBasicParsing
    Invoke-WebRequest -Uri "$baseUrl/SHA2-256SUMS" -OutFile $sums -UseBasicParsing

    $expected = $null
    foreach ($line in Get-Content -LiteralPath $sums) {
        if ($line -match '^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$' -and $Matches[2] -eq $asset) {
            $expected = $Matches[1].ToLowerInvariant()
            break
        }
    }
    if (-not $expected) { throw "Somme SHA-256 de $asset introuvable dans SHA2-256SUMS." }

    $actual = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Somme SHA-256 incorrecte pour $asset (attendue $expected, obtenue $actual). Fichier refusé."
    }
    Write-Host "SHA-256 vérifiée : $actual"

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $destination = Join-Path $OutDir $target
    try {
        Move-Item -LiteralPath $binary -Destination $destination -Force
    } catch {
        throw "Impossible d'écrire $destination (fichier en cours d'utilisation ?) : $($_.Exception.Message)"
    }
    if (-not $onWindows) { & chmod +x $destination }

    Write-Host "Installé : $destination"
    Write-Host ("Version : " + (& $destination --version))
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

# --- FFmpeg (M4-02) --------------------------------------------------------------------------------------
if ($SkipFfmpeg) { return }

if ($onMac) {
    Write-Warning "FFmpeg : pas de build LGPL téléchargeable pour macOS. Installez-le (brew install ffmpeg) puis copiez ffmpeg et ffprobe dans $OutDir."
    return
}

$ffBase = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest'
if ($onWindows) {
    $ffAssets = @{ 'X64' = 'ffmpeg-n8.1-latest-win64-lgpl-8.1.zip'; 'Arm64' = 'ffmpeg-n8.1-latest-winarm64-lgpl-8.1.zip' }
    $ffNames = @('ffmpeg.exe', 'ffprobe.exe')
} else {
    $ffAssets = @{ 'X64' = 'ffmpeg-n8.1-latest-linux64-lgpl-8.1.tar.xz'; 'Arm64' = 'ffmpeg-n8.1-latest-linuxarm64-lgpl-8.1.tar.xz' }
    $ffNames = @('ffmpeg', 'ffprobe')
}
if (-not $ffAssets.ContainsKey($arch)) { throw "FFmpeg : architecture non prise en charge par ce script : $arch" }
$ffAsset = $ffAssets[$arch]

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('dva-ffmpeg-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $archive = Join-Path $work $ffAsset
    $sums = Join-Path $work 'checksums.sha256'

    Write-Host "Téléchargement de $ffAsset (plusieurs dizaines de Mo)..."
    Invoke-WebRequest -Uri "$ffBase/$ffAsset" -OutFile $archive -UseBasicParsing
    Invoke-WebRequest -Uri "$ffBase/checksums.sha256" -OutFile $sums -UseBasicParsing

    $expected = $null
    foreach ($line in Get-Content -LiteralPath $sums) {
        if ($line -match '^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$' -and $Matches[2] -eq $ffAsset) {
            $expected = $Matches[1].ToLowerInvariant()
            break
        }
    }
    if (-not $expected) { throw "Somme SHA-256 de $ffAsset introuvable dans checksums.sha256." }

    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Somme SHA-256 incorrecte pour $ffAsset (attendue $expected, obtenue $actual). Fichier refusé."
    }
    Write-Host "SHA-256 vérifiée : $actual"

    $extract = Join-Path $work 'x'
    New-Item -ItemType Directory -Path $extract | Out-Null
    if ($onWindows) {
        Expand-Archive -LiteralPath $archive -DestinationPath $extract -Force
    } else {
        & tar -xJf $archive -C $extract
        if ($LASTEXITCODE -ne 0) { throw "Extraction de $ffAsset impossible (tar -xJf a échoué)." }
    }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    foreach ($name in $ffNames) {
        $found = Get-ChildItem -LiteralPath $extract -Recurse -Filter $name -File | Select-Object -First 1
        if (-not $found) { throw "$name introuvable dans l'archive." }
        $destination = Join-Path $OutDir $name
        try {
            Move-Item -LiteralPath $found.FullName -Destination $destination -Force
        } catch {
            throw "Impossible d'écrire $destination (fichier en cours d'utilisation ?) : $($_.Exception.Message)"
        }
        if (-not $onWindows) { & chmod +x $destination }
        Write-Host "Installé : $destination"
    }
    $ffmpegPath = Join-Path $OutDir $ffNames[0]
    Write-Host ((& $ffmpegPath -version | Select-Object -First 1))
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
