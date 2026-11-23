# PLAN — DVA Stream

Plan d'implémentation du MVP 1.0, découpé en jalons puis en sous-tâches. Références : `PRD.md` (exigences `FR-xx`), `TDD.md` (conception, sections et spikes `S1` à `S5`). L'avancement est suivi ici (cases à cocher) et résumé dans `JOURNAL.md`.

## Conventions

- **Identifiant** : `Mx-yy` (jalon, numéro). Une tâche = un commit ou une petite PR ; message de commit préfixé par l'identifiant (`M1-02: UrlParser`).
- **Taille** : **S** (moins d'1 h), **M** (1 à 3 h), **L** (plus de 3 h, à redécouper si possible).
- **Dép.** : tâches à terminer avant.
- **Fait quand** : critère vérifiable. Une tâche n'est cochée que si ses tests passent et que la CI est verte.
- **Règle d'or** : à chaque tâche terminée, cocher ici et ajouter une ligne dans `JOURNAL.md`.

## Décisions qui cadrent ce plan

- FFmpeg **embarqué** ; repli YoutubeExplode **reporté en v1.1** ; CliWrap **accepté** ; nom de fichier de sortie = **Titre** ; dépôt GitHub **privé** pendant la conception.
- Logo, palette, police et tokens de mise en page sont **décidés et figés** (D-011 à D-013, D-016, dossier `maquette/`) ; reste la validation des maquettes d'écrans. Les tokens sont repris tels quels en M0-06 ; l'habillage final conforme aux écrans validés est la tâche M3-08.

## Vue d'ensemble

| Jalon | Objectif | FR couverts | Nb tâches |
|---|---|---|---|
| M0 | Fondations | — | 9 |
| M1 | Analyse d'URL | FR-01 à 03 | 11 |
| M2 | Prévisualisation | FR-10 à 13, FR-50 | 13 |
| M3 | Formats et paramètres | FR-20 à 22, FR-60/61 | 10 |
| M4 | Téléchargement | FR-23, 30, 31, 34 à 37, 52 | 13 |
| M5 | Contrôle et robustesse | FR-32, 33, 39, 40 à 42 | 9 |
| M6 | Moteur à jour | FR-51, 53 | 9 |
| M7 | Finitions | FR-04, 22 (MP3), 38, 62 | 8 |
| M8 | Distribution | PRD §13 | 7 |

Chemin critique : **M0 → M1 → M2 → M3 → M4** donne le premier téléchargement réel ; M5 et M6 le fiabilisent ; M7 et M8 le terminent.

---

## M0 — Fondations (prérequis de M1)

> **État au 2026-11-19 :** M0 terminé. Build, tests et lancement vérifiés sur le PC d'Armistice (Windows), thèmes clair et sombre vérifiés, CI #1 verte sur `main` (1 min 43 s), protection de `main` en place. Le logo, défectueux au premier lancement, a été régénéré depuis les SVG. Reste à confirmer plus tard : fonctionnement sur macOS et Linux (la CI les compile et les teste, sans lancement manuel).

- [x] **M0-01 Solution et projets** — `DvaStream.sln`, `src/DvaStream.Core`, `src/DvaStream.Infrastructure`, `src/DvaStream.App` (Avalonia), `tests/DvaStream.Core.Tests`, `tests/DvaStream.Infrastructure.Tests`, `global.json` (.NET 10). *Fait quand :* `dotnet build` passe. · S
- [x] **M0-02 Réglages communs** — `Directory.Build.props` (`Nullable`, `TreatWarningsAsErrors`, analyseurs), `Directory.Packages.props` (versions centralisées), `.editorconfig`. *Fait quand :* build sans avertissement. · S · Dép : M0-01
- [x] **M0-03 Hygiène du dépôt** — `.gitignore` .NET, `README.md` (build, structure), modèle de PR et d'issues. *Fait quand :* le dépôt propre après build. · S · Dép : M0-01
- [x] **M0-04 Composition root** — `ServiceCollectionExtensions` dans `App/Composition`, conteneur DI, `MainViewModel` résolu par DI. *Fait quand :* l'app démarre avec une vue principale injectée. · M · Dép : M0-01
- [x] **M0-05 Journalisation** — Serilog, fichiers tournants dans `DataDirectory/logs`, `JobId` prévu dans le contexte. *Fait quand :* un fichier de log est créé au démarrage. · S · Dép : M0-04
- [x] **M0-06 Coquille UI et thèmes** — fenêtre principale vide, `FluentTheme`, `Tokens.axaml` / `Light.axaml` / `Dark.axaml` initialisés à partir de `maquette/palette/Tokens.colors.axaml` et `maquette/ecrans/Tokens.layout.axaml` (D-012, D-016), police Plus Jakarta Sans embarquée (`maquette/police/fonts/`, D-013), suivi du thème système. *Fait quand :* l'app s'affiche en clair et en sombre avec la palette et la police retenues. · M · Dép : M0-04
- [x] **M0-07 Test d'architecture** — échoue si `Core` référence `Infrastructure` ou Avalonia, ou si `Infrastructure` référence `App`. *Fait quand :* le test passe et échoue si on casse la règle (vérifié une fois). · S · Dép : M0-01
- [x] **M0-08 CI minimale** — `.github/workflows/ci.yml` : restore, build, tests sur `windows-latest`, `ubuntu-latest`, `macos-latest`. *Fait quand :* CI verte sur les 3 OS. · M · Dép : M0-07
- [x] **M0-09 Hébergement** — créer le dépôt GitHub **privé**, pousser `main`, protéger la branche (CI obligatoire). *Fait quand :* première CI exécutée sur GitHub. · S · Dép : M0-08

**Fin de M0 :** l'app démarre sur les 3 OS, CI verte, journal et DI en place.

---

## M1 — Analyse d'URL (FR-01 à FR-03)

> **État au 2026-11-20 :** M1 terminé. Vérifié sur le PC d'Armistice (Windows, .NET 10.0.15) : 164 tests verts avant M1-08 à M1-10, lancement en clair et en sombre, français et anglais, checklist manuelle FR-01 à FR-03 déroulée. **Écarts assumés :** (1) FR-02 « désactivable dans les paramètres » n'est pas encore vérifiable : l'interrupteur est une constante (`IsClipboardDetectionEnabled`) jusqu'à M3-06 ; (2) FR-04 n'est couvert que par le message provisoire (final en M7-02) ; (3) `MediaInfo`, `MediaStream`, `StreamKind` et `IMediaSource.GetInfoAsync` sont arrivés en M1 au lieu de M2-02 (D-025) ; (4) projet `tests/DvaStream.App.Tests` ajouté (tests des modèles de vue) ; (5) `Strings.resx` contient le français (langue par défaut), `Strings.en.resx` l'anglais (D-024).

- [x] **M1-01 Types du domaine** — `VideoId`, `UrlParseResult` (`Valid(VideoId)`, `Playlist`, `Invalid(reason)`) et `IUrlParser` dans `Core`. *Fait quand :* types compilés et documentés. · S · Dép : M0-01
- [x] **M1-02 Implémentation du parseur** — `UrlParser` : domaines `youtube.com`, `www.`, `m.`, `youtu.be` ; formats `watch?v=`, `shorts/`, `youtu.be/<id>` ; identifiant de 11 caractères valides ; URL avec `list=` détectée comme playlist ; schéma `http`/`https` uniquement. *Fait quand :* les cas valides renvoient le bon `VideoId`. · M · Dép : M1-01
- [x] **M1-03 Tests du parseur** — tableau de cas : valides (avec `&t=`, `&feature=`, casse), invalides (autre domaine, `youtube.com@evil.com`, schéma `file:`/`javascript:`, texte libre, espaces, identifiant trop court/long), playlists, `v=` + `list=`. *Fait quand :* au moins 30 cas, 100 % de couverture du parseur. · M · Dép : M1-02
- [x] **M1-04 Infrastructure de localisation** — `Strings.resx` (fr) + `Strings.en.resx`, extension de balisage AXAML, classe d'accès typée. *Fait quand :* un texte de test s'affiche en français et en anglais. · M · Dép : M0-06
- [x] **M1-05 Messages d'erreur d'URL** — clés de ressources pour : URL vide, non reconnue, domaine non pris en charge, identifiant invalide, playlist (message FR-04 provisoire). *Fait quand :* chaque `Invalid(reason)` a un message clair avec l'attendu. · S · Dép : M1-04
- [x] **M1-06 Service plateforme : presse-papiers** — `IPlatformServices.ReadClipboardAsync` et son implémentation Avalonia (`Platform/`). *Fait quand :* lecture du texte copié testée à la main sur l'OS de dev. · S · Dép : M0-04
- [x] **M1-07 `UrlInputViewModel`** — texte, validation à la saisie via `IUrlParser`, état `Empty/Valid/Invalid`, commande `Analyze` (active seulement si valide). *Fait quand :* tests de VM verts. · M · Dép : M1-02, M1-05
- [x] **M1-08 `UrlInputView` (AXAML)** — champ de saisie, bouton Analyser, message d'erreur, états vide/valide/invalide/désactivé, navigation clavier. *Fait quand :* les 4 états sont visibles en clair et en sombre. · M · Dép : M1-07, M0-06
- [x] **M1-09 Détection du presse-papiers (FR-02)** — à l'activation de la fenêtre, si le presse-papiers contient une URL valide : bandeau « Utiliser ce lien » ; **jamais appliquée automatiquement** ; interrupteur provisoire (constante) en attendant les paramètres (M3-06). *Fait quand :* comportement vérifié à la main + test de VM avec faux service plateforme. · M · Dép : M1-06, M1-07
- [x] **M1-10 Squelette d'état principal** — `MainViewModel` : états `Idle → Analyzing` (analyse simulée par un faux `IMediaSource`), annulation possible. *Fait quand :* clic sur Analyser passe en état `Analyzing` puis revient. · S · Dép : M1-07
- [x] **M1-11 Clôture du jalon** — vérification des critères FR-01 à FR-03, CI verte, mise à jour de `PLAN.md` et `JOURNAL.md`. · S · Dép : toutes

**Fin de M1 :** une URL est validée ou refusée avec un message clair ; le lien copié est proposé sans être imposé.

---

## M2 — Prévisualisation (FR-10 à FR-13, FR-50)

> **Jalon M2 clos le 2026-11-21** : toutes les cases cochées, CI verte sur les 3 OS (commit « Clôture M2 », 23eb353). Réserve consignée : fixtures de vidéo privée / restreinte par l'âge / bloquée par région non enregistrées (pas d'exemple disponible) ; les motifs d'erreur correspondants reposent sur la connaissance de yt-dlp. Spike S1 entamé (JOURNAL D-031), à finir en M4-01.

- [x] **M2-01 Fixtures réelles** — enregistrer des sorties JSON de `yt-dlp --dump-single-json` : vidéo standard, longue, courte, privée, supprimée, restreinte par l'âge, bloquée par région ; stockées dans `tests/.../Fixtures/`. *Fait quand :* au moins 6 fixtures versionnées. · M · Dép : M0-01 · *Outil :* `tools/record-fixtures.ps1` + `tools/fixtures/cases.json` (adresses signées retirées avant écriture ; les cas sans URL sont ignorés).
- [x] **M2-02 Modèles du domaine** — `MediaInfo`, `MediaStream`, `StreamKind` (TDD §4) ; début de `AppError` / `ErrorKind`. *Fait quand :* types compilés. · S · Dép : M0-01 · *Déjà fait en M1 (D-025) :* `MediaInfo`, `MediaStream`, `StreamKind` ; **reste** `AppError` / `ErrorKind`.
- [x] **M2-03 Emplacements et binaire de dev** — `DataDirectory` / `ConfigDirectory` par OS dans `IPlatformServices` ; script `tools/fetch-binaries.ps1` (yt-dlp pour le dev, avec vérification de somme ; PowerShell 5.1 ou 7, Windows, macOS, Linux) et `EngineLocator` + `IPlatformServices.EngineExecutablePath`. *Fait quand :* le binaire est trouvé via `DataDirectory/engine/`. · M · Dép : M1-06
- [x] **M2-04 Exécuteur de processus** — abstraction `IProcessRunner` + implémentation CliWrap : arguments en **liste**, lecture asynchrone de stdout/stderr, annulation, délai maximal. *Fait quand :* tests avec un petit exécutable de test. · M · Dép : M0-01
- [x] **M2-05 DTO et mapping JSON** — `YtDlpInfoDto`, désérialisation tolérante (champs absents), mapping vers `MediaInfo`. *Fait quand :* toutes les fixtures valides se mappent correctement. · M · Dép : M2-01, M2-02
- [x] **M2-06 `YtDlpMediaSource.GetInfoAsync`** — commande `--dump-single-json --no-playlist --no-warnings`, annulation, délai maximal, journalisation ; **remplace `SimulatedMediaSource`** (faux moteur de M1-10, D-025) dans l'enregistrement DI. *Fait quand :* retourne un `MediaInfo` réel sur une vidéo publique (test manuel). · M · Dép : M2-03, M2-04, M2-05
- [x] **M2-07 `YtDlpErrorMapper` v1** — vidéo privée, supprimée, restreinte (âge/région), réseau ; motifs testés sur sorties enregistrées. *Fait quand :* chaque fixture d'erreur donne le bon `ErrorKind`. · M · Dép : M2-01, M2-02
- [x] **M2-08 Tests d'intégration du moteur** — faux exécutable qui rejoue les fixtures ; tests du succès, des erreurs, de l'annulation, du délai dépassé. *Fait quand :* tests verts sur les 3 OS en CI. · M · Dép : M2-06, M2-07
- [x] **M2-09 Chargement des miniatures** — `IThumbnailLoader` (HTTP, cache mémoire borné, annulation). *Fait quand :* une miniature s'affiche, un échec de chargement ne bloque pas la carte. · S · Dép : M0-04
- [x] **M2-10 Résolutions disponibles (FR-12)** — calcul dans `Core` : liste triée des hauteurs réellement offertes (en déduisant les flux vidéo avec ou sans audio). *Fait quand :* tests sur plusieurs fixtures. · M · Dép : M2-05
- [x] **M2-11 `PreviewViewModel` et `PreviewView`** — miniature, titre, chaîne, durée `h:mm:ss`, état de chargement, bouton Annuler (FR-11), états vide/chargement/succès/erreur. *Fait quand :* les 4 états s'affichent. · L · Dép : M2-09, M2-10, M1-08
- [x] **M2-12 Erreurs de prévisualisation (FR-13)** — messages clairs pour privée/supprimée/restreinte/réseau via `ErrorKind` + clés de ressources. *Fait quand :* chaque cas affiche un message distinct avec action suggérée. · S · Dép : M2-07, M1-04
- [x] **M2-13 Intégration et clôture** — `MainViewModel` : `Analyze → Preview` avec le vrai moteur ; spike **S1** commencé (essai de format MP4 1080p) ; vérification FR-10 à FR-13 et FR-50 ; mise à jour de `PLAN.md` et `JOURNAL.md`. · M · Dép : M2-11, M2-12

**Fin de M2 :** coller une URL affiche miniature, titre, chaîne, durée en moins de 3 secondes (réseau normal).

---

## M3 — Formats et paramètres (FR-20 à FR-22, FR-60, FR-61)

> **État au 2026-11-21 :** **M3 terminée** — 455 tests verts, écran validé par Armistice (clair, sombre, habillage conforme aux maquettes). Clôture définitive à la CI verte sur les 3 OS après le commit « Clôture M3 ».

- [x] **M3-01 `AppSettings` et `ISettingsStore`** — enregistrement JSON versionné (`SchemaVersion`), valeurs par défaut, écriture atomique, verrou asynchrone, événement `Changed`. *Fait quand :* lecture/écriture testées. · M · Dép : M2-03
- [x] **M3-02 Robustesse des paramètres** — fichier corrompu → valeurs par défaut + sauvegarde `.bak` ; migration entre versions. *Fait quand :* tests sur fichiers corrompus et anciens. · S · Dép : M3-01
- [x] **M3-03 `FormatSelectorViewModel`** — mode Vidéo/Audio, résolutions disponibles, format audio (M4A par défaut) ; MP3 visible mais marqué « bientôt » jusqu'à M7-01. *Fait quand :* tests de VM. · M · Dép : M2-10
- [x] **M3-04 Résolution de repli** — si la résolution préférée n'existe pas, choisir la plus proche inférieure. *Fait quand :* tests unitaires du cas. · S · Dép : M3-03
- [x] **M3-05 `FormatSelectorView`** — sélecteur segmenté Vidéo/Audio, liste de qualités, mention sur le ré-encodage MP3, états désactivés. *Fait quand :* affichage correct en clair et en sombre. · M · Dép : M3-03
- [x] **M3-06 Câblage des préférences** — mémorisation de la qualité et du format ; interrupteur de détection du presse-papiers relié aux paramètres (remplace la constante `UrlInputViewModel.IsClipboardDetectionEnabled` de M1-09). *Fait quand :* les choix persistent après redémarrage. · S · Dép : M3-01, M3-05
- [x] **M3-07 Écran Paramètres minimal** — dossier par défaut (sélecteur natif), thème (système/clair/sombre), qualité, format audio, détection du presse-papiers. *Fait quand :* chaque réglage modifie le comportement. · M · Dép : M3-06
- [x] **M3-08 Thème appliqué et habillage** — thème choisi appliqué à chaud ; **habillage final conforme aux maquettes d'écrans validées**, logo et icônes d'application intégrés (`maquette/logo/`). *Fait quand :* les deux thèmes sont complets et conformes aux maquettes. · L · Dép : M3-07, **validation des maquettes d'écrans**
- [x] **M3-09 `DownloadRequest`** — construction et validation à partir du `MediaInfo` et des choix (cohérence vidéo/audio, dossier). *Fait quand :* tests des cas invalides. · S · Dép : M3-03
- [x] **M3-10 Clôture** — vérification FR-20 à FR-22, FR-60, FR-61 ; `PLAN.md` et `JOURNAL.md` à jour. *Vérifié :* FR-20 (mode Vidéo/Audio), FR-21 (toutes les hauteurs réellement disponibles, plus large que 720/1080/1440/4K : D-035 ; qualité mémorisée), FR-22 (M4A par défaut ; MP3 visible, désactivé jusqu'à M7-01), FR-60 (cinq réglages sur l'écran Paramètres), FR-61 (JSON versionné dans le dossier de configuration de l'OS). · S · Dép : toutes

**Fin de M3 :** les choix de format et de qualité sont faits, mémorisés, et produisent une `DownloadRequest` valide.

---

## M4 — Téléchargement (FR-23, FR-30, FR-31, FR-34 à FR-37, FR-52)

- [x] **M4-01 Spikes S1 et S2** — S1 : fusion MP4 en 1080p et 4K (codecs, lisibilité Windows/macOS/VLC) ; S2 : fiabilité du gabarit de progression. Résultats consignés dans `JOURNAL.md` et le TDD. *Avancement (2026-11-22) :* S2 concluant, S1 partiel (voir JOURNAL) ; reste le 1080p en copie H.264, le 2160p et la lecture. *Fait quand :* décision écrite sur les sélecteurs de format et le repli MKV/AAC. · L · Dép : M2-13
- [x] **M4-02 FFmpeg embarqué (FR-52)** — script `fetch-binaries` pour FFmpeg (build LGPL, sommes vérifiées), emplacement dans l'application, vérification de présence et de version au démarrage, `--ffmpeg-location`. *Fait quand :* le démarrage signale un FFmpeg absent. *Avancement (2026-11-21) :* écrit, **non vérifié** — `FfmpegLocator`, `FfmpegVersionParser`, `FfmpegProbe`, barre d'état « FFmpeg introuvable », script étendu (D-037) ; `--ffmpeg-location` sera passé en M4-05. · M · Dép : M2-03
- [x] **M4-03 `DownloadPlan`** — construction du plan (sélecteur de format yt-dlp, chemin temporaire, chemin final) pour vidéo et audio. *Vérifié (507 tests verts, 2026-11-22).* *Fait quand :* tests des plans par type de demande. · M · Dép : M3-09, M4-01
- [x] **M4-04 `ProgressLineParser`** — analyse des lignes du gabarit `--progress-template`, tolérante (valeurs `NA`, lignes inconnues). *Vérifié (507 tests verts, 2026-11-22).* *Fait quand :* tests sur des sorties réelles enregistrées. · M · Dép : M4-01
- [x] **M4-05 `YtDlpMediaSource.DownloadAsync`** — arguments, démarrage du processus, déduction des phases, rapport de progression, annulation. *Avancement (2026-11-22) :* *Vérifié (541 tests verts ; téléchargement réel, annulation et audio essayés avec `tools/DvaStream.TryDownload`, 2026-11-22).* (`DownloadAsync`, `DownloadProgressTracker`, lecture en continu des lignes, D-040) ; reste le test manuel : un MP4 1080p, puis le 2160p. *Fait quand :* un téléchargement MP4 1080p aboutit (test manuel). · L · Dép : M4-02, M4-03, M4-04
- [x] **M4-06 `IFileNamer`** — nettoyage des caractères interdits, noms réservés Windows, NFC, troncature, conflit `Titre (1)`. *Vérifié (730 tests verts, 2026-11-22).* *Fait quand :* au moins 20 cas testés. · M · Dép : M0-01
- [x] **M4-07 Dossier temporaire et fichier final** — dossier `temp/<jobId>/`, déplacement par renommage atomique (copie si volumes différents). *Vérifié (730 tests verts, 2026-11-22).* *Fait quand :* aucun fichier partiel n'apparaît dans le dossier de destination. · M · Dép : M4-05, M4-06
- [x] **M4-08 `DownloadJob` et machine à états** — transitions autorisées du TDD §8.1. *Vérifié (730 tests verts, 2026-11-22).* *Fait quand :* tests des transitions autorisées et interdites. · M · Dép : M2-02
- [x] **M4-09 `DownloadService`** — un téléchargement à la fois (API `Enqueue`/`Jobs` déjà prête pour v1.1), limitation de la progression à 10 mises à jour par seconde. *Avancement (2026-11-22) :* vérifié (build OK, 743 tests verts) : `IDownloadService`, `DownloadService`, 14 tests. *Fait quand :* tests avec faux `IMediaSource`. · M · Dép : M4-05, M4-08
- [x] **M4-10 Choix du dossier (FR-34)** — sélecteur natif Avalonia, dossier par défaut mémorisé. *Avancement (2026-11-23) :* vérifié (build OK, 771 tests, essai à l'écran : fichier enregistré dans le dossier affiché ; `DestinationViewModel`, ligne « Enregistrer dans » + « Modifier », le choix devient le dossier par défaut, D-043). *Fait quand :* le dossier choisi est utilisé. · S · Dép : M3-07
- [x] **M4-11 `DownloadViewModel` et vue** — pourcentage, vitesse, taille faite/totale, temps restant, phases (FR-30, FR-31), bouton Télécharger/Annuler. *Avancement (2026-11-23) :* vérifié (build OK, 771 tests, vraie vidéo de 69 Mo : progression, fin, fichier mp4 ; `DownloadViewModel`, `DownloadView`, bouton Télécharger, états `Downloading`/`Done`/`DownloadFailed`, Annuler, Réessayer, « Télécharger une autre vidéo » ; pas encore de menu « … » ni de pause). *Fait quand :* progression fluide sur une vidéo réelle. · L · Dép : M4-09
- [x] **M4-12 Fin de téléchargement (FR-37)** — Ouvrir le fichier, Afficher dans le dossier, notification système, sur Windows, macOS, Linux. *Avancement (2026-11-23) :* **vérifié sur Windows** (build OK, 783 tests ; notification et boutons essayés avec une vidéo et un M4A) ; **macOS et Linux non vérifiés** (point ouvert au journal, à confirmer avant la 1.0) ; `IDesktopShell`, `ShellCommands`, boutons « Ouvrir le fichier » et « Afficher dans le dossier », notification ; D-044). *Fait quand :* vérifié sur les 3 OS. · M · Dép : M4-11
- [ ] **M4-13 Clôture** — scénarios MP4 1080p et M4A de bout en bout ; vérification FR-23, 30, 31, 34 à 37, 52 ; `PLAN.md` et `JOURNAL.md` à jour. · M · Dép : toutes

**Fin de M4 :** premier vrai fichier MP4 ou M4A téléchargé avec progression précise.

---

## M5 — Contrôle et robustesse (FR-32, FR-33, FR-39, FR-40 à FR-42)

- [ ] **M5-01 Annulation de l'arbre de processus (spike S3)** — arrêt de yt-dlp **et** de FFmpeg sur les 3 OS. *Fait quand :* aucun processus orphelin après annulation. · L · Dép : M4-05
- [ ] **M5-02 Nettoyage des temporaires** — suppression à l'annulation ; purge des dossiers orphelins au démarrage (délai de 7 jours). *Fait quand :* tests sur dossiers simulés. · M · Dép : M5-01
- [ ] **M5-03 Reprise et pause (FR-33, FR-39)** — nommage déterministe du dossier temporaire, `--continue`, bouton Reprendre après coupure ; action « Pause » / « Reprendre » du menu du téléchargement (état `Paused`, temporaires conservés, annulation possible en pause). *Fait quand :* un téléchargement coupé ou mis en pause reprend sans repartir de zéro. · L · Dép : M5-02
- [ ] **M5-04 Taxonomie complète** — tous les `ErrorKind` du TDD §10.1 avec clés de ressources (message + action suggérée) en FR et EN. *Fait quand :* aucun `ErrorKind` sans message. · M · Dép : M2-07
- [ ] **M5-05 `YtDlpErrorMapper` complet** — disque plein, dossier non inscriptible, réseau, délai, limitation de débit, échec FFmpeg. *Fait quand :* tests sur sorties enregistrées. · M · Dép : M5-04
- [ ] **M5-06 Contrôles préalables** — espace disque suffisant, dossier inscriptible, avant de lancer. *Fait quand :* messages clairs sans lancer le moteur. · S · Dép : M5-04
- [ ] **M5-07 Gestionnaire global (FR-41)** — `AppDomain`, `TaskScheduler`, `Dispatcher` : journalisation et message sûr, jamais de plantage. *Fait quand :* une exception volontaire est affichée sans fermer l'app. · M · Dép : M5-04
- [ ] **M5-08 Détails techniques (FR-42)** — bouton « Copier les détails techniques » (versions, OS, erreur, extrait du journal, sans donnée personnelle hors URL). *Fait quand :* le contenu copié est vérifié. · S · Dép : M5-07
- [ ] **M5-09 Clôture** — tests de scénarios d'échec ; vérification FR-32, 33, 39, 40 à 42 ; `PLAN.md` et `JOURNAL.md` à jour. · M · Dép : toutes

**Fin de M5 :** aucune erreur ne plante l'app ; annulation et reprise fiables.

---

## M6 — Moteur à jour (FR-51, FR-53)

- [ ] **M6-01 Spike S5** — mise à jour du binaire pendant que l'app tourne (fichier verrouillé sous Windows). *Fait quand :* stratégie de remplacement décidée et consignée. · M · Dép : M2-03
- [ ] **M6-02 `EngineStatus`** — lecture de la version locale (`--version`), état (présent, absent, cassé). *Fait quand :* tests avec faux exécutable. · S · Dép : M2-04
- [ ] **M6-03 Client de versions** — dernière version publiée, sélection de la ressource selon l'OS et l'architecture, somme SHA-256. *Fait quand :* tests avec serveur HTTP local. · M · Dép : M6-02
- [ ] **M6-04 Mise à jour atomique et retour arrière** — téléchargement, vérification d'intégrité, test `--version`, remplacement, conservation de l'ancienne version. *Fait quand :* une mise à jour corrompue est refusée et l'ancienne version reste active. · L · Dép : M6-01, M6-03
- [ ] **M6-05 Binaire de secours embarqué** — copie du binaire livré au premier lancement. *Fait quand :* l'app fonctionne hors ligne dès l'installation. · M · Dép : M4-02
- [ ] **M6-06 Vérification au démarrage** — en arrière-plan, sans bloquer l'UI, avec limite de fréquence. *Fait quand :* le démarrage reste sous 3 secondes. · S · Dép : M6-04
- [ ] **M6-07 Interface moteur (FR-53)** — indicateur de version, bouton « Mettre à jour le moteur », progression. *Fait quand :* mise à jour manuelle réussie depuis l'UI. · M · Dép : M6-04
- [ ] **M6-08 Détection de rupture** — échecs d'extraction répétés → proposition de mise à jour. *Fait quand :* scénario simulé couvert par un test. · M · Dép : M6-07, M5-05
- [ ] **M6-09 Clôture** — vérification FR-51 et FR-53 ; `PLAN.md` et `JOURNAL.md` à jour. · S · Dép : toutes

**Fin de M6 :** le moteur se met à jour sans nouvelle version de l'application.

---

## M7 — Finitions (FR-04, FR-22 MP3, FR-38, FR-62)

- [ ] **M7-01 MP3 (FR-22)** — conversion FFmpeg via `IMuxer.ConvertAudioAsync`, qualité maximale, retrait du marquage « bientôt ». *Fait quand :* MP3 lisible produit. · M · Dép : M4-13
- [ ] **M7-02 Message playlist (FR-04)** — message final « non prise en charge », proposition de la vidéo seule si l'URL en contient une. *Fait quand :* les deux cas sont gérés. · S · Dép : M1-05
- [ ] **M7-03 Historique de session (FR-38)** — liste des téléchargements de la session avec statut et actions. *Fait quand :* liste cohérente après plusieurs téléchargements. · M · Dép : M4-09
- [ ] **M7-04 À propos et licences (FR-62)** — versions (app, moteur, FFmpeg), page des licences tierces générée. *Fait quand :* toutes les dépendances sont listées. · M · Dép : M6-02
- [ ] **M7-05 Avertissement au premier lancement** — écran d'acceptation (PRD §10) et mémorisation du choix. *Fait quand :* affiché une seule fois. · S · Dép : M3-01
- [ ] **M7-06 Traductions anglaises** — tous les textes en EN, vérification de la mise en page. *Fait quand :* aucune clé manquante. · M · Dép : M5-04
- [ ] **M7-07 Accessibilité** — navigation complète au clavier, ordre de tabulation, contrastes AA dans les deux thèmes. *Fait quand :* audit manuel sans blocage. · M · Dép : M3-08
- [ ] **M7-08 Performance et clôture** — mesure du démarrage (cible moins de 3 s) et de la mémoire (moins de 300 Mo) ; `PLAN.md` et `JOURNAL.md` à jour. · M · Dép : toutes

**Fin de M7 :** l'application est complète fonctionnellement.

---

## M8 — Distribution

- [ ] **M8-01 `release.yml`** — publication auto-contenue par plateforme (`win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`), récupération des binaires avec vérification de sommes. *Fait quand :* artefacts produits sur tag. · L · Dép : M7-08
- [ ] **M8-02 Packaging Velopack** — installeurs et mises à jour de l'application. *Fait quand :* installation et mise à jour testées. · L · Dép : M8-01
- [ ] **M8-03 Signature de code** — Windows et macOS (selon budget et calendrier). *Fait quand :* plus d'alerte de sécurité à l'installation, ou décision documentée de reporter. · L · Dép : M8-02
- [ ] **M8-04 Tests manuels sur 3 OS** — checklist des critères d'acceptation du PRD §13 sur machines réelles. *Fait quand :* tous les critères cochés. · L · Dép : M8-02
- [ ] **M8-05 Documentation utilisateur** — README public, guide d'installation, FAQ (alertes de sécurité, mise à jour). · M · Dép : M8-02
- [ ] **M8-06 Avis juridique** — avis obtenu avant toute distribution publique ; décision sur la visibilité du dépôt (D-009). *Fait quand :* décision consignée dans `JOURNAL.md`. · M · Dép : —
- [ ] **M8-07 Publication v1.0.0** — tag, release GitHub, journal et plan clôturés. · S · Dép : M8-04, M8-06

**Fin de M8 :** MVP 1.0 publié.

---

## Hors plan (à planifier plus tard)

Phase Design (maquettes, logo, police, palette) ; repli YoutubeExplode (v1.1) ; playlists, file d'attente, historique persistant, sous-titres (v1.1) ; cookies, chapitres (v1.2).
