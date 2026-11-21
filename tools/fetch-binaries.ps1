<#
.SYNOPSIS
    Télécharge yt-dlp pour le développement dans le dossier du moteur de DVA Stream (M2-03).

.DESCRIPTION
    Récupère le binaire officiel depuis les releases GitHub de yt-dlp, vérifie sa somme SHA-256 avec le fichier
    SHA2-256SUMS publié avec la même release, puis l'installe dans <DataDirectory>/engine/ (celui que l'application
    utilise). La signature GPG du fichier de sommes n'est pas vérifiée : ce contrôle protège contre un téléchargement
    corrompu, pas contre une release compromise.
    Le téléchargement de FFmpeg est ajouté en M4-02.

.PARAMETER OutDir
    Dossier d'installation. Par défaut : le dossier du moteur de DVA Stream pour l'OS courant.

.PARAMETER Version
    'latest' (défaut) ou une étiquette de release, par ex. 2026.10.26.

.EXAMPLE
    .\tools\fetch-binaries.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [string]$Version = 'latest'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch { }

$onWindows = ($PSVersionTable.PSEdition -eq 'Desktop') -or ($PSVersionTable.Platform -eq 'Win32NT')
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
} elseif ($IsMacOS) {
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
