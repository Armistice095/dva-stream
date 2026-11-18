# PROJECT.md — DVA Stream (by DVA Labs)

> Fiche d'identité du projet. Version 0.2 — mise à jour pour refléter les décisions du PRD v0.1 et du TDD v0.2. Détails dans les documents listés en section 6 ; l'avancement est dans `JOURNAL.md`.

## 1. Identité
* **Entreprise :** DVA Labs
* **Produit :** DVA Stream
* **Statut :** phase de cadrage terminée (documents v0.1/v0.2) ; prochaine étape : phase Design (maquettes, logo, police, palette), puis jalon M0

## 2. Présentation du Projet
**DVA Stream** est une application de bureau multiplateforme (Windows, macOS, Linux) permettant de télécharger des vidéos et d'extraire des pistes audio depuis YouTube à partir d'une simple URL, de façon locale, sûre et sans publicité.

## 3. Problèmes Résolus
1. **Sécurité et confort utilisateur :** Élimine le recours aux sites web de téléchargement remplis de publicités agressives, de traqueurs et de logiciels malveillants.
2. **Contrôle des formats et qualités :** Permet à l'utilisateur de choisir précisément la résolution vidéo (720p, 1080p, 4K) ou l'extraction audio haute fidélité sans compression abusive.
3. **Expérience native et fluide :** Fonctionne localement, avec une barre de progression précise et un accès direct aux fichiers enregistrés.

## 4. Stack Technique & Architecture
* **Langage & Runtime :** .NET 10 (LTS)
* **Framework UI :** Avalonia UI 11 (thème Fluent), MVVM avec `CommunityToolkit.Mvvm`, injection de dépendances `Microsoft.Extensions.DependencyInjection`
* **Moteur d'extraction & téléchargement :** **`yt-dlp`** (moteur principal, processus externe, mis à jour indépendamment de l'application) et **`FFmpeg`** (embarqué, pour la fusion vidéo/audio et le MP3). `YoutubeExplode` est reporté en v1.1 comme moteur de repli, derrière l'abstraction `IMediaSource`.
* **Outils :** `CliWrap` (processus externes), Serilog (journalisation), xUnit + NSubstitute (tests), Velopack (installation et mises à jour), GitHub Actions (CI sur 3 OS)
* **Solution en 3 projets :** `DvaStream.Core` (domaine, sans dépendance UI), `DvaStream.Infrastructure` (moteurs, fichiers, paramètres), `DvaStream.App` (Avalonia)
* **Direction Artistique & UI :** Interface épurée, moderne et corporate, privilégiant une typographie stylisée et lisible, des espaces bien structurés et un mode sombre/clair soigné. Police, palette et logo : à définir pendant la phase Design.
* **Distribution :** directe uniquement (site, GitHub Releases) ; stores exclus. Dépôt GitHub privé pendant la conception ; avis juridique avant toute distribution publique.

## 5. Feuille de Route (MVP - Version 1.0)
- [ ] **Analyse d'URL :** Champ de saisie avec détection intelligente du presse-papiers (jamais appliquée automatiquement, désactivable).
- [ ] **Prévisualisation :** Récupération et affichage de la miniature, du titre, de la chaîne et de la durée.
- [ ] **Sélecteur de format :** Choix entre Vidéo (MP4) avec sélection de résolution, et Audio seul (M4A par défaut, MP3 en option).
- [ ] **Gestionnaire de téléchargement :** Barre de progression en temps réel (pourcentage, vitesse, taille restante), choix du dossier de destination, nettoyage des noms de fichiers (nom = titre) et gestion des conflits.
- [ ] **Annulation et reprise :** Annuler à tout moment avec nettoyage des fichiers temporaires ; reprendre un téléchargement interrompu.
- [ ] **Gestion des erreurs :** Messages clairs en cas de lien invalide, de vidéo privée ou de coupure réseau ; aucune erreur ne plante l'application.
- [ ] **Moteur à jour :** Mise à jour de yt-dlp indépendante de l'application, avec vérification d'intégrité et retour arrière.
- [ ] **Paramètres et journalisation :** Dossier par défaut, thème, qualité préférée ; journaux locaux pour diagnostiquer les ruptures du moteur.
- [ ] **Packaging :** Installeurs et mises à jour pour Windows, macOS et Linux via la CI.

Hors MVP (v1.1 et après) : playlists, file d'attente multi-téléchargements, historique persistant, sous-titres, repli YoutubeExplode, cookies et chapitres.

## 6. Documents du projet
| Document | Rôle |
|---|---|
| `docs/PROJECT.md` | Cette fiche : identité, vision, stack, feuille de route |
| `docs/PRD.md` | Exigences produit (FR-xx), non-objectifs, risques, critères d'acceptation |
| `docs/TDD.md` | Conception technique : décisions (ADR), structure, contrats, flux, tests, CI, spikes |
| `docs/PLAN.md` | Plan d'implémentation M0 à M8 découpé en sous-tâches cochables |
| `docs/JOURNAL.md` | Journal vivant : état d'avancement, décisions, blocages, historique du travail |

Règle : toute personne qui travaille sur le projet consigne ce qu'elle fait dans `JOURNAL.md` au fur et à mesure et coche les tâches dans `PLAN.md`.
