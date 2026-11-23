# TDD — DVA Stream

**DVA Labs** · Technical Design Document · Version 0.4 (brouillon) · Référence : PRD v0.2

## 1. Objet et périmètre

Ce document décrit **comment** construire le MVP 1.0 défini dans le PRD : décisions techniques, structure de la solution, contrats entre composants, flux principaux, gestion des erreurs, tests, packaging et plan de développement. Il ne répète pas le « quoi » du PRD ; il le renvoie par identifiants (FR-xx).

Les points marqués **[À valider]** sont des hypothèses à confirmer, idéalement par un spike technique (section 16) avant de coder le composant concerné.

**Version 0.4** : Avalonia 12 (D-018), solution `.slnx` (D-019), `IPlatformServices.Directories` (D-020). **Version 0.3** : ajout de la pause (FR-39, §8.3 bis), de la police Plus Jakarta Sans et des tokens figés (D-012, D-013, D-016) ; sources de design dans `maquette/`. **Version 0.2** : les décisions D-004 à D-009 du JOURNAL (FFmpeg embarqué, repli YoutubeExplode reporté en v1.1, CliWrap accepté, nom de fichier = Titre, dépôt GitHub privé) sont intégrées ; la section 19 les récapitule.

## 2. Décisions techniques (ADR synthétiques)

| ID | Décision | Raison | Alternative écartée |
|---|---|---|---|
| ADR-01 | **.NET 10 (LTS)**, C# 14, `Nullable` activé, `TreatWarningsAsErrors` | Support long terme, performances, un seul runtime sur 3 OS | .NET 8 (fin de support proche) |
| ADR-02 | **Avalonia 12** (12.1.x, D-018) + thème Fluent, MVVM via `CommunityToolkit.Mvvm` | Rendu identique sur les 3 OS, source generators, peu de code répétitif | MAUI (pas de Linux), WPF (Windows seul) |
| ADR-03 | **yt-dlp** comme moteur principal, exécuté en **processus externe** | Maintenu très activement, corrige les ruptures en quelques jours, mise à jour sans recompiler l'app | Bibliothèque seule (casse à chaque changement de YouTube) |
| ADR-04 | **YoutubeExplode** en implémentation de repli derrière la même abstraction | Reporté en v1.1 (D-005). Pas de binaire externe, utile si yt-dlp est indisponible | Le livrer dès le MVP (écarté pour réduire le périmètre) |
| ADR-05 | **FFmpeg embarqué** dans l'installeur, appelé en processus externe (décision D-004) | Fonctionne hors ligne dès l'installation ; lancement par processus = pas de liaison, donc obligations de licence allégées | Téléchargement au premier lancement (installeur plus léger mais 1er lancement dépendant du réseau) |
| ADR-06 | **Injection de dépendances** par `Microsoft.Extensions.DependencyInjection`, composition root unique dans `App` | Testabilité, remplacement des moteurs | Service locator |
| ADR-07 | **Serilog** (fichiers tournants) pour la journalisation | Diagnostic des ruptures de moteur sur le terrain | `ILogger` seul sans sink fichier |
| ADR-08 | **CliWrap** pour piloter les processus externes (décision D-006) | API fluide, annulation propre, lecture asynchrone de stdout/stderr | `System.Diagnostics.Process` brut |
| ADR-09 | **Velopack** pour l'installation et les mises à jour de l'app, CI GitHub Actions sur 3 OS | Installeurs et mises à jour delta multiplateformes | Installeurs natifs séparés (MSI, DMG, deb) |
| ADR-10 | **xUnit + NSubstitute** pour les tests | Standard de l'écosystème .NET | NUnit, MSTest |

## 2 bis. Principes directeurs

- **Core ne dépend de rien** (ni UI, ni YouTube, ni système de fichiers concret).
- **Tout ce qui est long est asynchrone** et accepte un `CancellationToken`.
- **Aucune exception ne remonte jusqu'à l'utilisateur brute** : elle est convertie en `AppError` typée.
- **Les processus externes ne reçoivent jamais de chaîne concaténée** : arguments passés en liste (anti-injection).
- **Écriture atomique** : fichiers temporaires puis renommage final.

## 3. Structure de la solution

```
dva-stream/
├── docs/                      PROJECT.md, PRD.md, TDD.md, PLAN.md, JOURNAL.md
├── maquette/                  design : logo, palette, police, écrans, tokens (sources de Tokens.axaml)
├── src/
│   ├── DvaStream.Core/        modèles, interfaces, logique métier
│   ├── DvaStream.Infrastructure/   yt-dlp, YoutubeExplode, FFmpeg, fichiers, paramètres
│   └── DvaStream.App/         Avalonia : vues, view models, thèmes, ressources
├── tests/
│   ├── DvaStream.Core.Tests/
│   └── DvaStream.Infrastructure.Tests/
├── tools/                     scripts (récupération des binaires, packaging)
├── .github/workflows/         ci.yml, release.yml
├── Directory.Build.props      options communes (nullable, analyseurs, versions)
├── Directory.Packages.props   gestion centralisée des versions NuGet
├── global.json                version du SDK .NET
└── DvaStream.slnx             solution (format .slnx, D-019)
```

### Règles de dépendances

| Projet | Peut référencer | Ne doit jamais référencer |
|---|---|---|
| `Core` | rien (BCL uniquement) | Infrastructure, App, Avalonia |
| `Infrastructure` | `Core` | App, Avalonia |
| `App` | `Core`, `Infrastructure` (uniquement pour l'enregistrement DI) | — |

Ces règles sont vérifiées automatiquement par un test d'architecture (section 14).

## 4. Modèle de domaine (`DvaStream.Core`)

```csharp
// 11 caractères A-Z a-z 0-9 _ - ; créable uniquement par VideoId.TryCreate(string?, out VideoId?) (D-022)
public sealed record VideoId { public string Value { get; } /* … */ }

public sealed record MediaInfo(
    VideoId Id,
    string Title,
    string Channel,
    TimeSpan Duration,
    Uri? ThumbnailUrl,
    IReadOnlyList<MediaStream> Streams);

public enum StreamKind { Video, Audio, Muxed }

public sealed record MediaStream(
    string FormatId,
    StreamKind Kind,
    int? Height,          // 720, 1080, 1440, 2160
    string Container,     // mp4, webm, m4a
    string? VideoCodec,
    string? AudioCodec,
    long? Bytes,
    int? Fps);

public enum OutputKind { Video, Audio }
public enum AudioFormat { M4a, Mp3 }

public sealed record DownloadRequest(
    VideoId Id,
    OutputKind Kind,
    int? TargetHeight,       // null si audio
    AudioFormat? Audio,      // null si vidéo
    string DestinationFolder);

public enum DownloadPhase { Queued, Preparing, DownloadingVideo, DownloadingAudio, Merging, Converting, Finalizing, Completed, Failed, Cancelled }

public sealed record DownloadProgress(
    DownloadPhase Phase,
    double Percent,          // 0..100 de la phase courante
    double OverallPercent,   // 0..100 global
    long BytesDone,
    long? BytesTotal,
    double? BytesPerSecond,
    TimeSpan? Remaining);
```

**Préparation des playlists (FR-04, v1.1)** : `MediaInfo` reste un élément unique ; un type `MediaCollection(Id, Title, IReadOnlyList<VideoId>)` sera ajouté en v1.1 sans modifier l'existant.

## 5. Contrats (interfaces)

```csharp
public interface IUrlParser
{
    UrlParseResult Parse(string? input);   // Valid(VideoId) | Playlist(PlaylistId, VideoId?) | Invalid(UrlInvalidReason)
}

public interface IMediaSource
{
    string Name { get; }
    Task<MediaInfo> GetInfoAsync(VideoId id, CancellationToken ct);
    Task DownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct);
}

public interface IDownloadService
{
    DownloadJob Enqueue(DownloadRequest request);
    Task RunAsync(DownloadJob job, CancellationToken ct);
    IReadOnlyList<DownloadJob> Jobs { get; }
}

public interface IMuxer
{
    Task MergeAsync(string videoPath, string audioPath, string outputPath, CancellationToken ct);
    Task ConvertAudioAsync(string inputPath, string outputPath, AudioFormat format, CancellationToken ct);
}

public interface IEngineUpdater
{
    Task<EngineStatus> CheckAsync(CancellationToken ct);
    Task UpdateAsync(IProgress<double> progress, CancellationToken ct);
}

public interface ISettingsStore
{
    AppSettings Current { get; }
    Task SaveAsync(AppSettings settings, CancellationToken ct);
    event EventHandler<AppSettings> Changed;
}

public interface IFileNamer
{
    string BuildSafeName(string title, string extension, string folder);   // nettoyage + suffixe anti-conflit
}

public interface IPlatformServices
{
    Task<string?> ReadClipboardAsync();
    void RevealInFolder(string path);
    void OpenFile(string path);
    void NotifyDone(string title, string message);
    AppDirectories Directories { get; }   // ConfigDirectory, DataDirectory, LogsDirectory, EngineDirectory, TempDirectory (D-020)
}
```

`DownloadPlan` est un objet interne construit par `IDownloadService` à partir de la `DownloadRequest` et du `MediaInfo` (flux choisis, chemin temporaire, chemin final).

## 6. Intégration yt-dlp (`Infrastructure`)

### 6.1 Principe

`YtDlpMediaSource` lance le binaire yt-dlp comme processus. Les arguments sont passés en liste, jamais interpolés.

### 6.2 Récupération des métadonnées (FR-10 à FR-13)

- Commande : `yt-dlp --dump-single-json --no-playlist --no-warnings <url>` ; la sortie JSON est désérialisée vers un DTO interne puis mappée vers `MediaInfo`.
- Les champs utilisés : `id`, `title`, `channel` (ou `uploader`), `duration`, `thumbnail`, `formats[]` (`format_id`, `height`, `ext`, `vcodec`, `acodec`, `filesize` ou `filesize_approx`, `fps`).
- La liste des résolutions proposées en UI est dérivée des `formats` réellement présents (FR-12).
- Les erreurs (vidéo privée, supprimée, restreinte) sont détectées via le code de sortie et le texte de stderr (section 10).

### 6.3 Téléchargement (FR-20 à FR-23, FR-30)

- **Vidéo MP4 à hauteur cible H** : sélecteur de format `bv*[height<=H]+ba/b[height<=H]`, fusion en MP4. Pour limiter les incompatibilités (VP9/AV1 + Opus dans MP4), préférer H.264 + AAC lorsque disponible (`-S` avec `vcodec:h264,acodec:m4a`), et basculer sur un ré-encodage audio AAC si seul Opus existe **[À valider par spike]**. La 4K est souvent disponible uniquement en VP9/AV1 : le MP4 produit doit rester lisible sur les lecteurs courants, sinon proposer le conteneur MKV en repli et le dire à l'utilisateur.
- **Audio M4A** : meilleur flux `m4a` natif, sans ré-encodage (`-f ba[ext=m4a]/ba`).
- **Audio MP3** : extraction + conversion FFmpeg (`-x --audio-format mp3 --audio-quality 0`).
- **Progression** : `--newline --progress-template` avec un gabarit machine (`DVA|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s|%(progress.status)s`) ; chaque ligne est parsée par `ProgressLineParser` (testé unitairement).
- **Phases** : le changement de fichier (vidéo puis audio) et les lignes de post-traitement permettent de déduire `DownloadingVideo`, `DownloadingAudio`, `Merging`, `Converting`.
- **FFmpeg** : fourni via `--ffmpeg-location <dossier>` ; `IMuxer` n'est appelé directement que pour les cas que yt-dlp ne couvre pas (reconversion audio).
- **Sortie** : `-o` vers un dossier temporaire dédié au job ; le fichier final est déplacé par `IFileNamer` après succès.
- **Options de robustesse** : `--no-playlist`, `--no-mtime`, `--retries`, `--fragment-retries`, `--continue` (reprise, FR-33).

### 6.4 Cycle de vie du binaire (FR-51)

- Emplacement : `DataDirectory/engine/yt-dlp[.exe]`, **hors du dossier d'installation** (écriture possible sans droits administrateur).
- Au démarrage : `IEngineUpdater.CheckAsync` lit la version locale (`--version`) et la dernière version publiée sur le dépôt officiel de yt-dlp (en arrière-plan, sans bloquer l'UI).
- Mise à jour : téléchargement du binaire correspondant à l'OS et à l'architecture, **vérification de la somme SHA-256** avec le fichier de sommes publié, test `--version`, puis remplacement atomique ; l'ancienne version est conservée pour **retour arrière** si le test échoue.
- Premier lancement : si aucun binaire n'est présent, un binaire de secours embarqué (la version livrée avec la release) est copié.
- Rupture détectée (erreur d'extraction répétée) : l'UI propose « Mettre à jour le moteur » (FR-53).

## 7. Implémentation de repli YoutubeExplode (reportée en v1.1)

`YoutubeExplodeMediaSource` (`IMediaSource` + `IMuxer`) et son décorateur `FallbackMediaSource` sont **reportés en v1.1** (D-005). Le MVP livre avec yt-dlp seul ; l'abstraction `IMediaSource` reste en place pour brancher le repli plus tard sans toucher au reste du code.

## 8. Orchestration des téléchargements

### 8.1 Machine à états d'un `DownloadJob`

| État | Transitions autorisées |
|---|---|
| `Queued` | → `Preparing`, `Cancelled` |
| `Preparing` | → `DownloadingVideo`, `DownloadingAudio`, `Failed`, `Cancelled` |
| `DownloadingVideo` | → `DownloadingAudio`, `Merging`, `Completed`, `Failed`, `Cancelled` |
| `DownloadingAudio` | → `Merging`, `Converting`, `Finalizing`, `Failed`, `Cancelled` |
| `Merging` / `Converting` | → `Finalizing`, `Failed`, `Cancelled` |
| `Finalizing` | → `Completed`, `Failed` |
| `Completed` / `Failed` / `Cancelled` | état terminal |

Le MVP exécute **un téléchargement à la fois** ; la file multi-téléchargements arrive en v1.1, c'est pourquoi `IDownloadService` expose déjà `Enqueue` et `Jobs`.

### 8.2 Progression globale (FR-30, FR-31)

Poids indicatifs pour `OverallPercent` en vidéo fusionnée : vidéo 0 à 70 %, audio 70 à 90 %, fusion 90 à 99 %, finalisation 100 %. Ces poids sont proportionnels aux tailles connues quand elles le sont. Les mises à jour sont **limitées à 10 par seconde** pour ne pas saturer l'UI, puis marshalées sur le thread UI.

### 8.3 Annulation (FR-32)

1. L'UI annule le `CancellationTokenSource` du job.
2. CliWrap termine le processus yt-dlp **et ses enfants** (FFmpeg) : arrêt de l'arbre de processus.
3. Le dossier temporaire du job est supprimé ; l'état passe à `Cancelled`.

### 8.3 bis Pause et reprise manuelle (FR-39)

1. « Pause » arrête l'arbre de processus (yt-dlp et FFmpeg) comme à l'annulation, mais **sans** supprimer le dossier temporaire du job ; l'état passe à `Paused`.
2. « Reprendre » relance yt-dlp avec `--continue` sur le même dossier temporaire (même mécanique que la reprise, section 8.4) ; l'état repasse à `Downloading`.
3. « Annuler » reste possible en `Paused` : le dossier temporaire est alors supprimé (section 8.3).
4. Ce n'est pas une suspension de processus (`SIGSTOP`) : l'approche par arrêt et reprise est identique sur les 3 OS. La vitesse et le temps restant sont recalculés à la reprise.

### 8.4 Reprise (FR-33)

- Les fichiers `.part` de yt-dlp restent dans le dossier temporaire du job, nommé de façon déterministe (identifiant vidéo + options).
- Au démarrage, les dossiers temporaires sans job actif sont soit **repris** (si l'utilisateur relance le même téléchargement), soit **purgés** après un délai (7 jours) pour éviter les fichiers orphelins (section 7.2 du PRD).
- Une coupure réseau pendant un job passe en `Failed` avec un message proposant « Reprendre ».

### 8.5 Fichier final (FR-34 à FR-37)

- `IFileNamer` : construit le nom « Titre.ext » (titre seul, sans nom de chaîne : D-007), puis retire les caractères interdits (`<>:"/\|?*` et caractères de contrôle), normalise en Unicode NFC, gère les noms réservés Windows (`CON`, `NUL`…), tronque à une longueur sûre, retire espaces et points finaux.
- Conflit : suffixe ` (1)`, ` (2)`… jamais d'écrasement.
- Déplacement du temporaire vers le dossier final par **renommage atomique** (copie puis suppression si volumes différents).
- Fin : `IPlatformServices.NotifyDone` puis actions Ouvrir / Afficher.

## 9. Couche présentation (`DvaStream.App`)

### 9.1 Organisation

```
DvaStream.App/
├── Program.cs, App.axaml(.cs)     point d'entrée, thèmes, composition root
├── Composition/                    ServiceCollectionExtensions (DI)
├── ViewModels/                     MainViewModel, UrlInputViewModel, PreviewViewModel,
│                                   FormatSelectorViewModel, DownloadViewModel, SettingsViewModel
├── Views/                          vues AXAML correspondantes
├── Controls/                       composants réutilisables (carte, segmenté, progression)
├── Styles/                         Tokens.axaml (couleurs, typo, espacements), Light.axaml, Dark.axaml
├── Assets/Fonts/                   police embarquée
├── Localization/                   Strings.resx, Strings.fr.resx
└── Platform/                       implémentations IPlatformServices par OS
```

### 9.2 View models et états

- `MainViewModel` : orchestre l'écran principal et l'état global `Idle → Analyzing → Ready → Downloading → Done / Error`.
- `UrlInputViewModel` : saisie, validation via `IUrlParser`, détection du presse-papiers (au focus de la fenêtre, jamais appliquée seule, désactivable : FR-02).
- `PreviewViewModel` : expose `MediaInfo` (miniature chargée en asynchrone avec cache mémoire).
- `FormatSelectorViewModel` : Vidéo/Audio, résolutions disponibles, format audio ; mémorise les préférences.
- `DownloadViewModel` : progression, vitesse, temps restant, annulation, actions de fin.
- Commandes : `[RelayCommand]` asynchrones avec annulation ; état `IsBusy` pour désactiver les contrôles.

### 9.3 Thèmes et design tokens (PRD §8)

- Base `FluentTheme`, variantes **Light** et **Dark** via `ThemeVariant`, suivi du thème système par défaut.
- **Tokens centralisés** (`Tokens.axaml`) : palette sémantique (`Bg`, `Surface`, `SurfaceAlt`, `Border`, `TextPrimary`, `TextMuted`, `Primary`, `Accent`, `AccentText`, `Danger`, `Success`, `Warning`…), échelle d'espacement (4, 8, 12, 16, 20, 24, 32, 40, 48, 64), rayons (4, 8, 12, 16, pill), tailles de police (32, 20, 16, 14, 12) ; **aucune couleur codée en dur** dans les vues. Valeurs **figées** : palette D-012, police D-013, mise en page D-016 ; sources dans `maquette/palette/` (`tokens.json`, `Tokens.colors.axaml`) et `maquette/ecrans/` (`tokens-layout.json`, `Tokens.layout.axaml`). Le texte sur fond clair utilise `AccentText`, jamais `Accent`.
- Police stylisée embarquée en ressource (rendu identique sur les 3 OS) : **Plus Jakarta Sans** (D-013) ; activer `FontFeatures="+tnum"` sur les textes de progression.
- Chaque écran définit ses états : vide, chargement, succès, erreur, désactivé.

### 9.4 Internationalisation

Textes dans des fichiers `.resx` (français par défaut, anglais), accès par une extension de balisage AXAML ; aucun texte utilisateur en dur dans le code.

## 10. Gestion des erreurs (FR-40 à FR-42)

### 10.1 Taxonomie

```csharp
public enum ErrorKind
{
    InvalidUrl, UnsupportedPlaylist,
    VideoPrivate, VideoUnavailable, VideoRestricted,
    Network, Timeout, RateLimited,
    DiskFull, FolderNotWritable,
    EngineMissing, EngineBroken, FfmpegMissing, FfmpegFailed,
    Cancelled, Unknown
}

public sealed record AppError(ErrorKind Kind, string TechnicalDetails, Exception? Inner = null);
```

### 10.2 Traduction

- `YtDlpErrorMapper` associe codes de sortie et motifs de stderr à un `ErrorKind` (table de motifs testée unitairement avec des sorties réelles enregistrées).
- Chaque `ErrorKind` correspond à une **clé de ressource** : message en langage courant + action suggérée (réessayer, choisir un autre dossier, mettre à jour le moteur…).
- Un gestionnaire global (`AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `Dispatcher.UIThread.UnhandledException`) journalise et affiche un message sûr : l'app ne plante pas (FR-41).
- **Copier les détails techniques** (FR-42) : version app/moteur/FFmpeg, OS, `ErrorKind`, détails, dernières lignes du journal, **sans donnée personnelle** autre que l'URL.

## 11. Paramètres et stockage (FR-60 à FR-62)

| Élément | Emplacement |
|---|---|
| Paramètres | `ConfigDirectory/settings.json` (Windows `%AppData%/DvaStream`, macOS `~/Library/Application Support/DvaStream`, Linux `~/.config/DvaStream`) |
| Binaires du moteur | `DataDirectory/engine/` |
| Temporaires de téléchargement | `DataDirectory/temp/<jobId>/` |
| Journaux | `DataDirectory/logs/dvastream-.log` (rotation quotidienne, 14 jours) |

```csharp
public sealed record AppSettings(
    int SchemaVersion,
    string DefaultFolder,
    ThemeChoice Theme,              // System | Light | Dark
    int PreferredHeight,            // 1080 par défaut
    AudioFormat PreferredAudio,     // M4a par défaut
    bool ClipboardDetection,        // true par défaut
    string Language);               // fr | en
```

- JSON versionné avec migration si `SchemaVersion` change ; fichier corrompu → valeurs par défaut + sauvegarde `.bak`.
- Écriture atomique (temporaire puis renommage), accès sérialisé par un verrou asynchrone.

## 12. Sécurité

- Binaires externes : sources officielles uniquement, HTTPS, SHA-256 vérifié avant installation (section 6.4).
- Processus : arguments en liste, **aucun shell** (`UseShellExecute = false`), pas d'interpolation d'URL ou de titre dans une ligne de commande.
- Validation stricte des URL **avant** tout appel au moteur (`IUrlParser`) ; seuls les domaines autorisés passent.
- Journaux locaux uniquement ; aucun envoi réseau hors téléchargements demandés et vérification du moteur.
- Aucun secret dans le dépôt ; signature de code (Windows, macOS) intégrée à la CI de release **[À planifier]**.

## 13. Journalisation

- Serilog avec enrichisseurs (version app, OS) ; niveaux : `Information` par défaut, `Debug` activable dans les paramètres avancés.
- Chaque job porte un `JobId` dans le contexte de log.
- Les lignes brutes de yt-dlp ne sont journalisées qu'en `Debug`.

## 14. Stratégie de tests

| Niveau | Cible | Exemples |
|---|---|---|
| Unitaires | `Core`, mappers, parseurs | `UrlParser` (tous formats valides/invalides), `ProgressLineParser`, `FileNamer` (caractères interdits, noms réservés, conflits), machine à états, `YtDlpErrorMapper` |
| Intégration | `Infrastructure` | `YtDlpMediaSource` contre un faux exécutable qui rejoue des sorties enregistrées ; `EngineUpdater` avec serveur HTTP local ; `SettingsStore` sur dossier temporaire |
| ViewModels | `App` | transitions d'état de `MainViewModel` avec dépendances simulées (NSubstitute) |
| Architecture | solution | test qui échoue si `Core` référence `Infrastructure` ou Avalonia |
| Manuel / E2E | 3 OS | liste de contrôle des critères d'acceptation (PRD §13) sur machines réelles |

Objectif : **70 % de couverture** sur `Core` (PRD §7.6). Les tests réseau réels contre YouTube **ne font pas partie de la CI** (fragiles) ; ils sont lancés à la demande dans un job séparé.

## 15. CI/CD et packaging

- **`ci.yml`** : sur chaque PR, matrice `windows-latest`, `ubuntu-latest`, `macos-latest` : restore, build, tests, couverture, publication de résultats.
- **`release.yml`** : sur tag `v*`, publication auto-contenue par plateforme (`win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`), récupération des binaires yt-dlp et FFmpeg avec vérification de sommes, packaging Velopack, signature, création de la release GitHub.
- Versionnement : **SemVer** ; version injectée au build et affichée dans « À propos ».
- Les binaires tiers sont récupérés par script (`tools/fetch-binaries`) et **ne sont pas commités** dans le dépôt.
- **Licences** : page « Licences » générée à partir des dépendances ; build FFmpeg LGPL privilégié, lancé en processus séparé ; texte de licence et accès aux sources fournis avec l'application.

## 16. Spikes techniques (à faire avant le composant concerné)

| Spike | Question | Critère de réussite |
|---|---|---|
| S1 | Format et fusion MP4 en 1080p/4K avec yt-dlp + FFmpeg | MP4 lisible partout (Windows, macOS, VLC) avec image et son |
| S2 | Fiabilité du parsing de progression avec `--progress-template` | Pourcentage, vitesse et ETA cohérents sur vidéo courte et longue |
| S3 | Arrêt propre de l'arbre de processus sur les 3 OS | Aucun processus orphelin, temporaires supprimés |
| S4 | Avalonia : détection du presse-papiers, notification système, « Afficher dans le dossier » sur les 3 OS | Comportement acceptable sans dépendance native fragile |
| S5 | Mise à jour atomique du binaire yt-dlp (Windows : fichier verrouillé) | Mise à jour réussie pendant que l'app tourne, retour arrière testé |

**Résultats (2026-11-22)** : S1 et S2 réalisés (M4-01), décisions et gabarit de progression dans `JOURNAL.md` D-038. S1 : MP4 H.264 + AAC jusqu'à 1080p, VP9/AV1 recopiés au-delà, jamais de ré-encodage vidéo ; 2160p à contrôler en M4-05. S2 : gabarit fiable, progression par flux à agréger. Les autres spikes restent à faire.

## 17. Plan de développement (jalons)

| Jalon | Contenu | FR couverts | Résultat visible |
|---|---|---|---|
| **M0 : Fondations** | Solution, 3 projets, `Directory.*.props`, DI, Serilog, CI minimale sur 3 OS, thème clair/sombre vide | — | App qui démarre sur les 3 OS |
| **M1 : Analyse d'URL** | `IUrlParser`, champ de saisie, validation, détection presse-papiers, messages d'erreur | FR-01 à FR-03 | URL validée ou refusée avec message clair |
| **M2 : Prévisualisation** | `YtDlpMediaSource.GetInfoAsync`, gestion du moteur (binaire présent), carte de prévisualisation | FR-10 à FR-13, FR-50 | Miniature, titre, chaîne, durée |
| **M3 : Formats** | Sélecteur Vidéo/Audio, résolutions réelles, préférences | FR-20 à FR-22, FR-60/61 | Choix de qualité mémorisé |
| **M4 : Téléchargement** | `IDownloadService`, progression, fusion, `IFileNamer`, dossier, fin de job | FR-23, FR-30, FR-31, FR-34 à FR-37 | Premier fichier MP4/M4A téléchargé |
| **M5 : Contrôle et robustesse** | Annulation, reprise, erreurs typées, gestionnaire global, nettoyage des orphelins | FR-32, FR-33, FR-39, FR-40 à FR-42 | Aucun plantage, messages clairs |
| **M6 : Moteur à jour** | `IEngineUpdater`, vérification d'intégrité, retour arrière, UI de mise à jour | FR-51 à FR-53 | Moteur mis à jour sans nouvelle release |
| **M7 : Finitions** | Écran À propos, licences, MP3, historique de session, i18n FR/EN, accessibilité clavier | FR-04, FR-38, FR-62, FR-22 | Application complète fonctionnellement |
| **M8 : Distribution** | Velopack, signatures, release GitHub, tests manuels 3 OS, critères d'acceptation | PRD §13 | **MVP 1.0** |

Chaque jalon se termine par : tests verts, CI verte sur 3 OS, critères d'acceptation du jalon cochés, document mis à jour si une décision a changé.

Le découpage de chaque jalon en sous-tâches se trouve dans PLAN.md ; l'avancement et les décisions sont suivis dans JOURNAL.md.

## 18. Risques techniques

| Risque | Réponse |
|---|---|
| Changement de format des sorties de yt-dlp | Sortie JSON et gabarit de progression versionnés ; tests sur sorties enregistrées ; mise à jour rapide du moteur |
| MP4 non lisible avec codecs VP9/AV1/Opus | Spike S1 ; préférence H.264/AAC ; repli MKV ou ré-encodage audio, avec message clair |
| Fichier verrouillé lors de la mise à jour (Windows) | Spike S5 ; écriture dans un nom temporaire puis échange au prochain lancement si nécessaire |
| Comportements différents des 3 OS (chemins, notifications, presse-papiers) | Abstraction `IPlatformServices`, CI 3 OS, tests manuels par jalon |
| Alertes antivirus sur les binaires embarqués | Sources officielles, sommes vérifiées, signature de l'app, documentation |
| Dérive d'architecture | Test d'architecture et règles de dépendances (section 3) |

## 19. Décisions et points restants

Les six questions de la version 0.1 sont tranchées (JOURNAL : D-004 à D-009).

| Question | Décision |
|---|---|
| FFmpeg embarqué ou téléchargé | **Embarqué** dans l'installeur (ADR-05, D-004) |
| Repli YoutubeExplode dans le MVP | **Reporté en v1.1** (section 7, D-005) |
| CliWrap ou `Process` brut | **CliWrap accepté** (ADR-08, D-006) |
| Nom du fichier de sortie | **Titre** seul (section 8.5, D-007) |
| Police et palette | **Décidées** : Plus Jakarta Sans, accent aqua (D-012, D-013, voir `maquette/`) ; tokens de couleur et de mise en page figés (D-012, D-016), repris tels quels en M0-06 |
| Hébergement du dépôt | **GitHub privé** pendant la conception, public après avis juridique (D-009) |

Points restants :

1. Validation des maquettes d'écrans (premier jet) et de l'état « en pause », avant la tâche M3-08. Logo, palette, police et tokens de mise en page : faits (D-011 à D-013, D-016).
2. Résultats des spikes S1 à S5, à consigner ici et dans JOURNAL.md.
3. Signature de code : budget et calendrier (tâche M8-03).
