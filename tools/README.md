# tools/

Scripts de développement (PowerShell 5.1 ou 7).

| Script | Rôle |
|---|---|
| `fetch-binaries.ps1` | Télécharge yt-dlp dans le dossier du moteur de l'application (`%LOCALAPPDATA%\DvaStream\engine` sous Windows), somme SHA-256 vérifiée, puis FFmpeg et ffprobe (build LGPL BtbN 8.1, Windows et Linux ; sous macOS à copier à la main). `-SkipFfmpeg` pour yt-dlp seul. |
| `record-fixtures.ps1` | Enregistre de vraies sorties `yt-dlp --dump-single-json` dans `tests/DvaStream.Infrastructure.Tests/Fixtures/ytdlp/` (M2-01). Les cas viennent de `fixtures/cases.json` ; ceux sans URL sont ignorés. Les adresses signées sont retirées avant écriture. |
| `spike-m4.ps1` | Spikes S1 et S2 (M4-01) : fusion MP4 1080p/4K, M4A natif, lignes de progression. Écrit dans `%TEMP%\dva-spike` et produit un rapport à copier. |
| `DvaStream.TryDownload` | Test manuel de `DownloadAsync` (M4-05) : `dotnet run --project tools/DvaStream.TryDownload -- <adresse> [--height 1080] [--audio] [--mp3]`. Utilise le vrai code, affiche la progression, range le fichier dans `Téléchargements\dva-try` ; Ctrl+C teste l'annulation. Hors de la solution et de la CI. |

Ordre habituel : `.\tools\fetch-binaries.ps1`, puis `.\tools\record-fixtures.ps1`.

Si Windows refuse d'exécuter les scripts (« running scripts is disabled »), autoriser seulement la session en cours, sans toucher à la configuration du système :

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```
