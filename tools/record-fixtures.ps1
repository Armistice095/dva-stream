<#
.SYNOPSIS
    Enregistre des sorties réelles de yt-dlp (--dump-single-json) comme fixtures de tests (M2-01).

.DESCRIPTION
    Lit tools/fixtures/cases.json, lance yt-dlp pour chaque cas qui a une URL et enregistre le résultat dans
    tests/DvaStream.Infrastructure.Tests/Fixtures/ytdlp/ :
      - <nom>.json        : sortie JSON en cas de succès, NETTOYÉE (voir ci-dessous) ;
      - <nom>.error.json  : { exitCode, stderr, ytDlpVersion, arguments } en cas d'échec.
    Le nettoyage retire les adresses signées (url, manifest_url, fragments, http_headers, vignettes, sous-titres...) :
    elles contiennent votre adresse IP et des jetons d'accès, et n'ont rien à faire dans un dépôt.
    Les cas sans URL sont ignorés : renseignez-les dans cases.json, puis relancez le script.

.PARAMETER YtDlp
    Chemin de yt-dlp. Par défaut : le dossier du moteur de DVA Stream (voir fetch-binaries.ps1), puis le PATH.

.EXAMPLE
    .\tools\record-fixtures.ps1
#>
[CmdletBinding()]
param(
    [string]$YtDlp,
    [string]$Cases,
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Cases)  { $Cases  = Join-Path $PSScriptRoot 'fixtures\cases.json' }
if (-not $OutDir) { $OutDir = Join-Path $repo 'tests\DvaStream.Infrastructure.Tests\Fixtures\ytdlp' }

# --- Localiser yt-dlp --------------------------------------------------------------------------------
function Find-YtDlp {
    $onWindows = ($PSVersionTable.PSEdition -eq 'Desktop') -or ($PSVersionTable.Platform -eq 'Win32NT')
    if ($onWindows) {
        $candidate = Join-Path $env:LOCALAPPDATA 'DvaStream\engine\yt-dlp.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $cmd = Get-Command 'yt-dlp' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

if (-not $YtDlp) { $YtDlp = Find-YtDlp }
if (-not $YtDlp -or -not (Test-Path -LiteralPath $YtDlp)) {
    throw "yt-dlp introuvable. Lancez d'abord .\tools\fetch-binaries.ps1 (ou passez -YtDlp <chemin>)."
}

# --- Outils ------------------------------------------------------------------------------------------
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments, [int]$TimeoutSeconds = 120)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.Arguments = (($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $process = [System.Diagnostics.Process]::Start($psi)
    $outTask = $process.StandardOutput.ReadToEndAsync()
    $errTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill() } catch { }
        throw "Délai dépassé ($TimeoutSeconds s) : $Exe"
    }
    $process.WaitForExit()
    [pscustomobject]@{ ExitCode = $process.ExitCode; Out = $outTask.Result; Err = $errTask.Result }
}

# Propriétés retirées à tous les niveaux : adresses signées (IP, jetons) et données volumineuses sans intérêt.
$script:DropKeys = @(
    'url', 'manifest_url', 'fragment_base_url', 'fragments', 'http_headers',
    'thumbnails', 'automatic_captions', 'subtitles',
    'requested_formats', 'requested_downloads', 'requested_subtitles'
)

function Remove-Noise {
    param($Node)
    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($name in @($Node.PSObject.Properties.Name)) {
            if ($script:DropKeys -contains $name) {
                $Node.PSObject.Properties.Remove($name)
            } else {
                Remove-Noise $Node.PSObject.Properties[$name].Value
            }
        }
    } elseif (($Node -is [System.Collections.IEnumerable]) -and ($Node -isnot [string])) {
        foreach ($item in $Node) { Remove-Noise $item }
    }
}

function Save-Utf8 {
    param([string]$Path, [string]$Text)
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

# --- Exécution ---------------------------------------------------------------------------------------
$version = (Invoke-Native -Exe $YtDlp -Arguments @('--version')).Out.Trim()
Write-Host "yt-dlp $version ($YtDlp)"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Attention : PowerShell ne distingue pas la casse, donc $cases écraserait le paramètre $Cases (typé string).
$caseList = Get-Content -LiteralPath $Cases -Raw -Encoding UTF8 | ConvertFrom-Json
$urlPattern = '^https://(www\.)?(youtube\.com/watch\?v=[A-Za-z0-9_-]{11}|youtu\.be/[A-Za-z0-9_-]{11})$'
$report = @()

foreach ($case in $caseList) {
    $name = $case.name
    if ([string]::IsNullOrWhiteSpace($case.url)) {
        $report += [pscustomobject]@{ Cas = $name; Résultat = 'IGNORÉ (pas d''URL)'; Attendu = $case.expect }
        continue
    }
    if ($case.url -notmatch $urlPattern) {
        $report += [pscustomobject]@{ Cas = $name; Résultat = 'IGNORÉ (URL non reconnue)'; Attendu = $case.expect }
        continue
    }

    $extra = @()
    if ($case.PSObject.Properties.Name -contains 'extraArgs') { $extra = @($case.extraArgs) }
    $arguments = @('--dump-single-json', '--no-playlist', '--no-warnings') + $extra + @($case.url)

    $successFile = Join-Path $OutDir "$name.json"
    $errorFile = Join-Path $OutDir "$name.error.json"
    Remove-Item -LiteralPath $successFile, $errorFile -ErrorAction SilentlyContinue

    try {
        $run = Invoke-Native -Exe $YtDlp -Arguments $arguments
    } catch {
        $report += [pscustomobject]@{ Cas = $name; Résultat = "ERREUR : $($_.Exception.Message)"; Attendu = $case.expect }
        continue
    }

    if ($run.ExitCode -eq 0) {
        try {
            $json = $run.Out | ConvertFrom-Json
            Remove-Noise $json
            Save-Utf8 -Path $successFile -Text ($json | ConvertTo-Json -Depth 50)
            $outcome = 'succès'
        } catch {
            $report += [pscustomobject]@{ Cas = $name; Résultat = "NON ENREGISTRÉ (nettoyage impossible : $($_.Exception.Message))"; Attendu = $case.expect }
            continue
        }
    } else {
        $record = [ordered]@{
            exitCode     = $run.ExitCode
            stderr       = $run.Err.Trim()
            ytDlpVersion = $version
            arguments    = $arguments
        }
        Save-Utf8 -Path $errorFile -Text ($record | ConvertTo-Json -Depth 5)
        $outcome = 'échec'
    }

    $expected = if ($case.expect -eq 'success') { 'succès' } else { 'échec' }
    $verdict = if ($outcome -eq $expected) { "OK ($outcome)" } else { "INATTENDU : $outcome (attendu : $expected)" }
    $report += [pscustomobject]@{ Cas = $name; Résultat = $verdict; Attendu = $case.expect }
}

Save-Utf8 -Path (Join-Path $OutDir '_meta.json') -Text ([ordered]@{
    ytDlpVersion = $version
    recordedAt   = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json)

Write-Host ''
$report | Format-Table -AutoSize
Write-Host "Fixtures enregistrées dans : $OutDir"
Write-Host 'Relisez les fichiers avant de les committer (aucune URL signée ne doit rester).'
