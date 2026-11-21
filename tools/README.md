# tools/

Scripts de développement (PowerShell 5.1 ou 7).

| Script | Rôle |
|---|---|
| `fetch-binaries.ps1` | Télécharge yt-dlp dans le dossier du moteur de l'application (`%LOCALAPPDATA%\DvaStream\engine` sous Windows), somme SHA-256 vérifiée. FFmpeg s'y ajoute en M4-02. |
| `record-fixtures.ps1` | Enregistre de vraies sorties `yt-dlp --dump-single-json` dans `tests/DvaStream.Infrastructure.Tests/Fixtures/ytdlp/` (M2-01). Les cas viennent de `fixtures/cases.json` ; ceux sans URL sont ignorés. Les adresses signées sont retirées avant écriture. |

Ordre habituel : `.\tools\fetch-binaries.ps1`, puis `.\tools\record-fixtures.ps1`.

Si Windows refuse d'exécuter les scripts (« running scripts is disabled »), autoriser seulement la session en cours, sans toucher à la configuration du système :

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```
