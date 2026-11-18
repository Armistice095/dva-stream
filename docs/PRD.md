# PRD — DVA Stream

**DVA Labs** · Product Requirements Document · Version 0.1 (brouillon) · Cible : MVP 1.0

## 1. Résumé exécutif

**DVA Stream** est une application de bureau multiplateforme (Windows, macOS, Linux) qui permet de télécharger des vidéos et d'extraire des pistes audio depuis YouTube à partir d'une simple URL, de façon locale, sûre et sans publicités.

La proposition de valeur tient en trois points : **sécurité** (aucun site tiers, aucun traqueur), **contrôle** (résolution et format choisis précisément) et **expérience native** (progression précise, accès direct aux fichiers).

## 2. Contexte et problème

Les utilisateurs qui veulent récupérer une vidéo ou un son depuis YouTube se tournent aujourd'hui vers des sites web de téléchargement. Ces sites posent trois problèmes :

1. **Sécurité et confort** : publicités agressives, traqueurs, redirections et logiciels malveillants.
2. **Manque de contrôle** : qualité imposée, compression excessive, résolutions limitées, pas de choix du format.
3. **Expérience dégradée** : pas de progression fiable, pas de gestion des erreurs claire, fichiers à retrouver dans le dossier Téléchargements.

Les alternatives en ligne de commande (yt-dlp) sont puissantes mais inaccessibles au grand public. DVA Stream comble cet écart avec une interface graphique soignée.

## 3. Objectifs et non-objectifs

### 3.1 Objectifs du MVP

- **O1** : permettre un téléchargement complet (URL → fichier) en moins de 4 interactions.
- **O2** : offrir un choix réel de qualité (720p, 1080p, 4K quand disponible) et un audio haute fidélité.
- **O3** : fournir une progression en temps réel fiable (pourcentage, vitesse, temps restant).
- **O4** : être résilient : erreurs expliquées clairement, annulation et reprise, moteur mis à jour sans nouvelle release de l'app.
- **O5** : livrer une interface moderne, cohérente en mode clair et sombre, identique sur les 3 OS.

### 3.2 Non-objectifs (hors MVP)

- Téléchargement de playlists et de chaînes entières (prévu v1.1, mais le modèle de données le prévoit dès maintenant).
- Autres plateformes que YouTube.
- Lecteur intégré, édition ou conversion de fichiers locaux.
- Comptes utilisateurs, synchronisation cloud, télémétrie obligatoire.
- Contournement de contenus protégés (DRM), de contenus privés ou de restrictions d'accès.
- Distribution via Microsoft Store / Mac App Store (incompatible avec le produit).

## 4. Utilisateurs cibles

| Persona | Besoin | Priorité |
|---|---|---|
| **Le particulier prudent** | Sauvegarder une vidéo ou un son pour usage personnel sans passer par des sites douteux | Principal |
| **Le créateur de contenu** | Récupérer ses propres vidéos ou des contenus sous licence libre en haute qualité | Principal |
| **L'étudiant / enseignant** | Conserver des contenus pédagogiques pour les consulter hors ligne | Secondaire |
| **L'utilisateur avancé** | Choisir finement format et qualité, sans ligne de commande | Secondaire |

## 5. Parcours utilisateur principal

1. L'utilisateur copie une URL YouTube. À l'ouverture de l'app (ou au retour au premier plan), l'app détecte l'URL dans le presse-papiers et propose de l'utiliser.
2. L'app analyse l'URL et affiche la **prévisualisation** : miniature, titre, chaîne, durée.
3. L'utilisateur choisit **Vidéo (MP4)** + résolution, ou **Audio** (M4A par défaut, MP3 en option).
4. L'utilisateur confirme le dossier de destination (mémorisé par défaut) et lance le téléchargement.
5. La **progression** s'affiche en temps réel, avec possibilité d'annuler.
6. À la fin, un bouton **Ouvrir le dossier** / **Afficher le fichier** est proposé.

## 6. Exigences fonctionnelles (MVP)

Priorités : **P0** = indispensable au MVP, **P1** = important, **P2** = souhaitable.

### 6.1 Analyse d'URL

| ID | Exigence | Prio |
|---|---|---|
| FR-01 | Un champ de saisie accepte une URL YouTube ; la validation est stricte (domaines `youtube.com`, `youtu.be`, `m.youtube.com`, formats `watch`, `shorts`, `youtu.be/<id>`). | P0 |
| FR-02 | L'app détecte une URL YouTube valide dans le presse-papiers et propose de l'utiliser, sans jamais l'appliquer automatiquement ; cette détection est désactivable dans les paramètres. | P0 |
| FR-03 | Une URL invalide affiche un message clair indiquant ce qui est attendu. | P0 |
| FR-04 | Une URL de playlist est reconnue et signalée comme « non prise en charge dans cette version » (la vidéo seule peut être proposée si l'URL en contient une). | P1 |

### 6.2 Prévisualisation

| ID | Exigence | Prio |
|---|---|---|
| FR-10 | Après analyse, l'app affiche la miniature, le titre, la chaîne et la durée formatée (`h:mm:ss`). | P0 |
| FR-11 | L'analyse affiche un état de chargement et peut être annulée. | P0 |
| FR-12 | L'app liste uniquement les résolutions réellement disponibles pour la vidéo. | P0 |
| FR-13 | Les vidéos privées, supprimées, restreintes par l'âge ou la région sont détectées à cette étape avec un message explicite. | P0 |

### 6.3 Sélecteur de format

| ID | Exigence | Prio |
|---|---|---|
| FR-20 | Choix entre **Vidéo (MP4)** et **Audio seul**. | P0 |
| FR-21 | En mode vidéo : sélection de résolution parmi 720p, 1080p, 1440p et 4K lorsqu'elles existent ; une qualité préférée est mémorisée. | P0 |
| FR-22 | En mode audio : **M4A par défaut** (flux AAC natif, sans ré-encodage) ; **MP3 en option**, avec une mention indiquant qu'il implique un ré-encodage. | P0 |
| FR-23 | Pour les résolutions où vidéo et audio sont séparés, l'app télécharge les deux flux et les fusionne (muxing FFmpeg) de façon transparente pour l'utilisateur. | P0 |
| FR-24 | Les métadonnées (titre, artiste/chaîne) et la miniature sont intégrées au fichier audio lorsque le format le permet. | P2 |

### 6.4 Gestionnaire de téléchargement

| ID | Exigence | Prio |
|---|---|---|
| FR-30 | Barre de progression en temps réel : pourcentage, vitesse, taille téléchargée / totale, temps restant estimé. | P0 |
| FR-31 | La progression couvre les phases distinctes : téléchargement vidéo, téléchargement audio, fusion/conversion, finalisation. | P0 |
| FR-32 | **Annulation** à tout moment, avec nettoyage des fichiers temporaires. | P0 |
| FR-33 | **Reprise** d'un téléchargement interrompu (coupure réseau, fermeture de l'app) lorsque c'est techniquement possible. | P1 |
| FR-34 | Choix du dossier de destination via le sélecteur natif de l'OS ; dossier par défaut mémorisé. | P0 |
| FR-35 | **Nettoyage des noms de fichiers** : caractères interdits selon l'OS retirés ou remplacés, longueur limitée. | P0 |
| FR-36 | **Gestion des conflits de noms** : suffixe automatique (`Titre (1).mp4`) ; jamais d'écrasement silencieux. | P0 |
| FR-37 | À la fin : actions **Ouvrir le fichier** et **Afficher dans le dossier**, ainsi qu'une notification système. | P0 |
| FR-38 | Historique des téléchargements de la session (liste simple avec statut). Un historique persistant est P2. | P1 |

### 6.5 Gestion des erreurs

| ID | Exigence | Prio |
|---|---|---|
| FR-40 | Messages clairs, en langage courant, avec une action suggérée, pour : lien invalide, vidéo privée / supprimée / restreinte, coupure réseau, espace disque insuffisant, dossier non accessible, échec du moteur, échec de FFmpeg. | P0 |
| FR-41 | Aucune erreur ne doit faire planter l'app ; toute exception est capturée, journalisée et transformée en message utilisateur. | P0 |
| FR-42 | Un bouton **Copier les détails techniques** permet de joindre un diagnostic à un signalement. | P1 |

### 6.6 Moteur et mises à jour

| ID | Exigence | Prio |
|---|---|---|
| FR-50 | Le moteur d'extraction principal est **yt-dlp**, derrière l'abstraction `IMediaSource`. **YoutubeExplode** est une implémentation alternative / de repli. | P0 |
| FR-51 | Le binaire yt-dlp est **mis à jour indépendamment de l'application** (vérification au démarrage, téléchargement en arrière-plan, vérification d'intégrité, retour à la version précédente en cas d'échec). | P0 |
| FR-52 | FFmpeg est fourni avec l'application (ou téléchargé au premier lancement) ; sa présence et sa version sont vérifiées au démarrage. | P0 |
| FR-53 | En cas de rupture du moteur, l'app l'indique clairement et propose une mise à jour manuelle du moteur. | P1 |

### 6.7 Paramètres

| ID | Exigence | Prio |
|---|---|---|
| FR-60 | Paramètres persistants : dossier par défaut, thème (clair / sombre / système), qualité vidéo préférée, format audio préféré, détection du presse-papiers. | P0 |
| FR-61 | Les paramètres sont stockés dans le dossier de configuration propre à chaque OS, au format JSON versionné. | P0 |
| FR-62 | Un écran **À propos** affiche la version de l'app, du moteur et de FFmpeg, ainsi que les licences tierces. | P1 |

## 7. Exigences non fonctionnelles

### 7.1 Performance

- Démarrage à froid de l'app : **moins de 3 secondes** sur une machine standard.
- Analyse d'URL : **moins de 3 secondes** dans des conditions réseau normales.
- L'interface reste fluide (aucun blocage du thread UI) pendant tout téléchargement ; toutes les opérations longues sont asynchrones avec `CancellationToken`.
- Consommation mémoire raisonnable : moins de 300 Mo en téléchargement actif.

### 7.2 Fiabilité

- Aucune corruption de fichier final : écriture dans un fichier temporaire puis renommage atomique à la fin.
- Reprise propre après arrêt brutal (nettoyage des fichiers temporaires orphelins au démarrage).

### 7.3 Sécurité et confidentialité

- Fonctionnement 100 % local : **aucun compte, aucune publicité, aucun traqueur**.
- Aucune télémétrie par défaut ; si des rapports d'erreur sont ajoutés un jour, ils seront strictement optionnels (opt-in).
- Les binaires externes (yt-dlp, FFmpeg) sont téléchargés uniquement depuis des sources officielles, en HTTPS, avec vérification d'intégrité (somme de contrôle).
- Les processus externes sont lancés sans interpolation de chaîne non assainie (pas d'injection de commande via une URL ou un titre).
- Les journaux ne contiennent aucune donnée personnelle au-delà des URL traitées, et restent sur la machine.

### 7.4 Compatibilité

| Plateforme | Versions minimales visées | Architectures |
|---|---|---|
| Windows | 10 (22H2) et 11 | x64, arm64 |
| macOS | 12 et supérieur | x64, arm64 (Apple Silicon) |
| Linux | Ubuntu 22.04+, Fedora, Debian 12+ | x64 |

### 7.5 Accessibilité et internationalisation

- Navigation complète au clavier, ordre de tabulation logique, contrastes conformes WCAG AA dans les deux thèmes.
- Textes externalisés dans des ressources (`.resx`) dès le départ ; **français et anglais** pour le MVP.

### 7.6 Maintenabilité

- Couverture de tests unitaires de la couche Core d'au moins 70 %.
- Journalisation structurée avec Serilog (fichiers tournants, niveaux configurables).
- Intégration continue sur les 3 OS (GitHub Actions) : build, tests, packaging.

## 8. Direction artistique et UX

- **Style** : épuré, moderne, corporate ; beaucoup d'espace, hiérarchie visuelle nette, une seule action principale par écran.
- **Thème** : base Avalonia Fluent, variantes **clair et sombre** soignées, couleurs et typographie centralisées dans des ressources partagées (design tokens) ; suivi du thème système par défaut.
- **Typographie** : une police stylisée et lisible, embarquée dans l'application pour garantir un rendu identique sur les 3 OS.
- **Composants clés** : champ URL avec bouton Analyser, carte de prévisualisation, sélecteur segmenté Vidéo / Audio, liste déroulante de qualité, bouton principal Télécharger, carte de progression, barre d'état.
- **États** à concevoir pour chaque écran : vide, chargement, succès, erreur, désactivé.
- **Principe** : l'utilisateur ne voit jamais un terme technique (muxing, flux adaptatif) sans explication simple.

## 9. Architecture (synthèse)

- **Runtime** : .NET 10 (LTS). **UI** : Avalonia, MVVM avec `CommunityToolkit.Mvvm`, injection de dépendances via `Microsoft.Extensions.DependencyInjection`.
- **Solution en 3 projets** :
  - `DvaStream.Core` : modèles, interfaces, logique métier, sans dépendance UI.
  - `DvaStream.Infrastructure` : implémentations (yt-dlp, YoutubeExplode, FFmpeg, système de fichiers, paramètres).
  - `DvaStream.App` : vues Avalonia et view models.
- **Interfaces clés** : `IMediaSource` (métadonnées, flux), `IDownloadService` (`IProgress<T>` + `CancellationToken`), `IMuxer`, `ISettingsStore`, `IEngineUpdater`.
- **Modèle de données** : `MediaInfo` (id, titre, chaîne, durée, miniature, flux disponibles), `DownloadJob` (statut, progression, options, chemin de sortie), `DownloadRequest`. `MediaInfo` prévoit déjà un type « collection » pour les futures playlists.
- Le détail technique fera l'objet d'un **document de conception technique (TDD)** séparé.

## 10. Conformité, licences et distribution

- **Conditions d'utilisation de YouTube** : le téléchargement va à l'encontre de celles-ci, et le droit d'auteur peut s'appliquer selon les pays. DVA Labs, en tant qu'éditeur, porte une exposition spécifique ; un avis juridique est recommandé avant la distribution publique.
- **Positionnement produit** : usage personnel, contenus dont l'utilisateur détient les droits ou sous licence libre. Aucun message marketing sur le contournement des publicités de YouTube.
- **Avertissement dans l'app** : écran d'acceptation au premier lancement rappelant la responsabilité de l'utilisateur.
- **Licences tierces** : vérifier la licence du build FFmpeg embarqué (LGPL ou GPL) et les obligations associées ; YoutubeExplode est sous LGPL-3.0 (liaison dynamique, mention et fourniture des sources). Page « Licences » dans l'app.
- **Distribution** : directe uniquement (site officiel, GitHub Releases). Stores exclus. Installeurs et mises à jour automatiques via **Velopack** ou installeurs natifs par OS ; signature de code recommandée (Windows, macOS) pour éviter les alertes de sécurité.

## 11. Risques et mitigations

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| Rupture du moteur après un changement côté YouTube | Élevée | Élevé | yt-dlp en moteur principal, mise à jour indépendante du binaire, abstraction `IMediaSource` |
| Risque juridique / retrait par un hébergeur | Moyenne | Élevé | Positionnement prudent, avertissement, avis juridique, distribution directe |
| Alertes antivirus / SmartScreen sur un exécutable non signé | Élevée | Moyen | Signature de code, réputation progressive, documentation d'installation |
| Licences FFmpeg / LGPL mal respectées | Moyenne | Moyen | Audit des licences, build FFmpeg LGPL, liaison par processus externe |
| Différences de comportement entre OS (chemins, notifications, presse-papiers) | Moyenne | Moyen | CI sur 3 OS, abstractions par plateforme, tests manuels réguliers |
| Restrictions YouTube (limitation de débit, vérification anti-bot) | Moyenne | Moyen | Messages d'erreur explicites, options de cookies à étudier en v1.1 |

## 12. Indicateurs de succès

- **Taux de réussite des téléchargements** supérieur à 95 % sur des vidéos publiques standard.
- **Délai de rétablissement** après une rupture du moteur : moins de 72 heures.
- **Temps URL → téléchargement lancé** : moins de 15 secondes pour un utilisateur novice.
- **Stabilité** : moins de 1 % de sessions avec une erreur non gérée.
- Mesure via des tests internes et des retours utilisateurs, puisqu'aucune télémétrie n'est collectée par défaut.

## 13. Critères d'acceptation du MVP 1.0

- [ ] Une URL valide donne une prévisualisation correcte en moins de 3 secondes.
- [ ] Les erreurs de la liste FR-40 produisent chacune un message clair, sans plantage.
- [ ] Un téléchargement MP4 en 1080p aboutit à un fichier lisible avec image et son (fusion réussie).
- [ ] Un téléchargement M4A et un téléchargement MP3 aboutissent à des fichiers lisibles.
- [ ] L'annulation supprime tous les fichiers temporaires.
- [ ] La progression affiche pourcentage, vitesse et temps restant, et reste fluide.
- [ ] Le moteur yt-dlp se met à jour sans réinstaller l'application.
- [ ] Les thèmes clair et sombre sont complets, sans élément illisible.
- [ ] L'application s'installe et fonctionne sur Windows, macOS et Linux via les artefacts de la CI.

## 14. Feuille de route

| Version | Contenu |
|---|---|
| **MVP 1.0** | Périmètre décrit dans ce document |
| **1.1** | Playlists, file d'attente multi-téléchargements, historique persistant, sous-titres |
| **1.2** | Support des cookies / vidéos réservées aux membres, métadonnées enrichies, découpage par chapitres |
| **2.0** | Autres plateformes, personnalisation avancée, éventuellement une version premium |

## 15. Questions ouvertes

1. **Choix définitif du moteur** : yt-dlp principal confirmé ? YoutubeExplode conservé comme repli ou abandonné pour réduire la maintenance ?
2. **FFmpeg** : embarqué dans l'installeur (taille, licence) ou téléchargé au premier lancement ?
3. **Nom commercial et identité visuelle** : logo, palette de couleurs, typographie à valider.
4. **Langues du MVP** : français et anglais uniquement ?
5. **Signature de code** : budget et calendrier pour les certificats Windows et Apple.
6. **Modèle économique** : gratuit et open source, gratuit fermé, ou freemium ?
7. **Format du titre de fichier** : modèle par défaut (`Titre`, `Chaîne - Titre`) et personnalisable ou non.

## 16. Glossaire

- **Muxing** : fusion d'un flux vidéo et d'un flux audio dans un même fichier sans ré-encodage.
- **Flux adaptatif** : flux vidéo et audio distribués séparément par YouTube pour les hautes résolutions.
- **M4A / AAC** : format audio compressé, natif sur YouTube, sans perte supplémentaire s'il n'est pas ré-encodé.
- **MVVM** : modèle d'architecture séparant vues, logique de présentation et données.
- **PRD / TDD** : document d'exigences produit / document de conception technique.
