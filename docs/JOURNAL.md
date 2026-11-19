# JOURNAL — DVA Stream

Journal de bord vivant du projet. Il répond à deux questions : **où en est-on ?** et **qu'est-ce qui a été fait, par qui, quand ?**

## Comment tenir ce journal (règles pour tous les contributeurs)

1. **Chaque fois que tu termines quelque chose** (une tâche du `PLAN.md`, une décision, un incident, un changement de document), ajoute une entrée dans la section « Journal chronologique », **avant** de passer à la suite, et dans le même commit que le travail concerné.
2. Les entrées les plus récentes sont **en haut** de la section chronologique.
3. Une entrée = une date, un auteur, une ligne par fait, avec l'identifiant de tâche (`M1-03`) quand il existe.
4. Quand une tâche est terminée, **coche-la aussi dans `PLAN.md`** et mets à jour le tableau « État d'avancement » ci-dessous.
5. Les décisions structurantes vont dans « Registre des décisions » (numérotées `D-xxx`, jamais supprimées : si une décision change, on en ajoute une nouvelle qui remplace l'ancienne).
6. Les blocages et questions ouvertes vont dans « Blocages et points en attente » et en sortent quand ils sont résolus (en laissant une trace dans le journal).
7. On écrit au passé, en phrases courtes et factuelles.

### Modèle d'entrée

```
### AAAA-MM-JJ
- **[Auteur] · Jalon/Tâche** — ce qui a été fait (résultat, pas intention).
```

---

## État d'avancement

**Dernière mise à jour :** 2026-11-19 · **Phase actuelle :** Design en cours (logo, palette et police faits ; écran principal en premier jet à valider)

| Jalon | Contenu | Statut |
|---|---|---|
| Cadrage | PROJECT (v0.2), PRD (v0.1), TDD (v0.2), PLAN, JOURNAL | **Terminé** |
| Design | Maquettes, logo, police, palette, tokens | **En cours** : logo, palette, police faits (`maquette/`) ; écran principal et écrans secondaires (premier lancement, Paramètres, Historique, À propos, Licences) en premier jet à valider |
| M0 — Fondations | Solution, DI, logs, CI, thème vide | À faire |
| M1 — Analyse d'URL | Parseur, saisie, presse-papiers | À faire |
| M2 — Prévisualisation | Métadonnées yt-dlp, carte de prévisualisation | À faire |
| M3 — Formats | Sélecteur, paramètres persistants, thème | À faire |
| M4 — Téléchargement | Progression, fusion, fichier final | À faire |
| M5 — Contrôle et robustesse | Annulation, reprise, erreurs typées | À faire |
| M6 — Moteur à jour | Mise à jour indépendante de yt-dlp | À faire |
| M7 — Finitions | MP3, historique, À propos, EN, accessibilité | À faire |
| M8 — Distribution | Velopack, signature, release 1.0 | À faire |

**Prochaine action :** valider (ou corriger) les premiers jets des écrans, dessiner l'état « en pause », trancher la présence d'un réglage de langue, démarrer M0 (tokens d'espacement et de rayons figés, D-016).

---

## Registre des décisions

| ID | Date | Décision | Contexte |
|---|---|---|---|
| D-001 | 2026-11-18 | Moteur principal : **yt-dlp** (processus externe) derrière `IMediaSource` | Fragilité de YoutubeExplode face aux changements de YouTube |
| D-002 | 2026-11-18 | **.NET 10 (LTS)**, Avalonia 11 + MVVM (`CommunityToolkit.Mvvm`), DI Microsoft | TDD ADR-01, ADR-02, ADR-06 |
| D-003 | 2026-11-18 | Solution en 3 projets : `Core`, `Infrastructure`, `App` | Core sans dépendance UI |
| D-004 | 2026-11-18 | **FFmpeg embarqué** dans l'installeur (appelé en processus externe) | Fonctionne hors ligne dès l'installation ; obligations de licence allégées |
| D-005 | 2026-11-18 | **Repli YoutubeExplode reporté en v1.1** ; l'abstraction `IMediaSource` reste en place | Réduction du périmètre du MVP |
| D-006 | 2026-11-18 | **CliWrap** accepté pour piloter les processus externes | TDD ADR-08 |
| D-007 | 2026-11-18 | Nom du fichier de sortie : **`Titre`** (sans nom de chaîne) | Réponse aux questions ouvertes PRD §15 / TDD §19 |
| D-008 | 2026-11-18 | Police et palette : **décidées pendant la phase Design**, après la rédaction des documents | Tokens de thème provisoires jusque-là |
| D-009 | 2026-11-18 | **Hébergement : dépôt GitHub privé** pendant la conception, passage en public (ou release publiques) après avis juridique | Exposition juridique de l'éditeur (PRD §10) ; la matrice CI à 3 OS consomme des minutes GitHub Actions, à surveiller en dépôt privé |
| D-010 | 2026-11-18 | Priorités PRD : M4A par défaut, MP3 en option ; annulation, reprise, journalisation, mise à jour du moteur incluses dans le MVP | Recommandations validées |
| D-011 | 2026-11-18 | **Logo** : monogramme « D » dont le vide est un triangle « play » ; icône sombre (principale) et claire ; logo horizontal | Phase Design (`maquette/logo/`). Variante « D + V pointé vers le bas » explorée puis écartée au profit du « play » |
| D-012 | 2026-11-18 | **Palette** : encre `#0E1330`, indigo `#3B47E0` (action principale), indigo clair `#5B6CFF`, **accent aqua `#22D3C5`** ; tokens clair et sombre, contrastes de texte ≥ 4,5:1 (WCAG AA) | Précise D-008 pour la palette. Variante ambre écartée (`maquette/palette/alternatives/`). Aqua jamais en texte sur fond clair : utiliser `AccentText` |
| D-013 | 2026-11-18 | **Police** : Plus Jakarta Sans (OFL), graisses 400/500/600/700 embarquées en TTF ; chiffres tabulaires (`tnum`) pour la progression | Précise D-008 pour la police. Échelle : Display 32, Title 20, Subtitle 16, Body 14, Caption 12 (`maquette/police/`) |
| D-014 | 2026-11-18 | Le mot « DVA Stream » du logo est dessiné en Plus Jakarta Sans (Bold + Medium), converti en tracés | Cohérence marque / interface |
| D-015 | 2026-11-18 | **Ton de l'interface : vouvoiement** (« Collez un lien », « votre vidéo ») dans tous les textes de l'application | Image plus professionnelle ; remplace le tutoiement du premier jet. Maquettes écran principal, écrans secondaires et spécimen de police mis à jour |
| D-016 | 2026-11-19 | **Tokens de mise en page figés (v1.0)** : espacements 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40 · 48 · 64 ; rayons 4 · 8 · 12 · 16 · pill ; hauteurs de contrôles 32 · 36 · 40 · 44 · 48 · 56 · 60 | Maquettes alignées sur ces valeurs (`maquette/ecrans/tokens-layout.json`, `Tokens.layout.axaml`) ; remplace les valeurs provisoires du premier jet |
| D-017 | 2026-11-19 | **Action « Pause » intégrée au PRD** (FR-39, P1) : arrêt avec conservation des temporaires, puis « Reprendre » ; annulation possible en pause | Tranche le point ouvert du menu de téléchargement. TDD §8.3 bis, PLAN M5-03 et critères d'acceptation mis à jour |

---

## Blocages et points en attente

| Point | Depuis | Responsable | Statut |
|---|---|---|---|
| Identité Git du dépôt (nom et email de l'auteur des commits) | 2026-11-18 | À définir | Ouvert |
| Page « Licences » retirée de la maquette alors que PRD §10, FR-62 et TDD §10 l'exigent. À trancher : où fournir les mentions de licence (fichier à côté de l'application, lien dans À propos…) et mettre PRD / PLAN M7-04 à jour | 2026-11-19 | Armistice | Ouvert |

---

## Journal chronologique

### 2026-11-19
- **[Claude] · Dépôt** — Reprise du travail fait hors de cette conversation (archive `dva-stream-v0_9-design`) : `maquette/` (logo, palette, police, écrans, tokens de mise en page), documents mis à jour (D-011 à D-017, FR-39 Pause) ; le tout committé dans le dépôt, `PROJECT.md` aligné sur l'état réel (écrans en premier jet, tokens figés). Constats laissés ouverts : `PLAN.md` M0-06 et `TDD.md` §9.3 parlent encore de tokens « provisoires » alors que palette et tokens de mise en page sont figés ; `TDD.md` §3 ne liste pas `maquette/`.
- **[Armistice + Claude] · Cadrage** — « Pause » ajoutée au PRD (FR-39, D-017) : exigence P1, critère d'acceptation, section TDD §8.3 bis (arrêt de l'arbre de processus, dossier temporaire conservé, reprise par `--continue`), tâche M5-03 étendue. Point ouvert « Pause » levé. À faire côté maquette : état « en pause » du téléchargement (menu avec « Reprendre »).
- **[Armistice + Claude] · Design** — Tokens d'espacement et de rayons figés (D-016) : audit des maquettes (valeurs éparses 2, 3, 5, 6, 7, 9, 10, 14, 18, 22, 28…), échelle retenue, valeurs mappées dans les 4 fichiers HTML de l'écran principal et des écrans secondaires, PNG régénérés. `tokens-layout.json` mis à jour (usages, règle du rayon interne, exceptions) ; `Tokens.layout.axaml` créé (brouillon non compilé pour M0-06 / M3-08). Changement visible : légers décalages de 2 à 4 px, à valider à l'œil.
- **[Armistice + Claude] · Design** — Écran E (Licences des composants tiers) supprimé de la maquette, ainsi que la ligne qui y menait dans À propos. Point ouvert : PRD §10 / FR-62 et TDD §10 prévoient une page « Licences » dans l'application ; à arbitrer (par ex. fichier de licences fourni avec l'installeur) avant M7-04.
- **[Armistice + Claude] · Design** — Écrans D (À propos) et E (Licences) refaits : mentions « yt-dlp · » et « FFmpeg · inclus avec l'application » retirées (versions seules sous les titres, statut « À jour » en vert), lien « Copier les informations de diagnostic » retiré, pied de page « Logiciel développé par DVA Labs » placé en bas de l'écran, « par DVA Labs » retiré sous la version. Licences : liste triée par ordre alphabétique, descriptions retirées, chevron par ligne, phrase d'introduction ; tags « À confirmer » conservés (audit PRD §10). Historique : on garde la session seule (FR-38, persistant = P2) comme prévu au PRD.
- **[Armistice + Claude] · Design** — Écran B (Paramètres) : retrait des deux textes d'aide sous « Qualité vidéo préférée » et « Format audio préféré » (jugés superflus) ; réglage du presse-papiers renommé « Détection du presse-papiers » avec une aide d'une ligne (« Propose un lien YouTube copié au retour dans l'application. ») ; PNG régénérés.

### 2026-11-18
- **[Armistice + Claude] · Design** — Passage au vouvoiement (D-015) dans toutes les maquettes : écran principal (clair/sombre), écrans secondaires (premier lancement, paramètres, historique vide) et spécimen de police ; PNG régénérés.
- **[Armistice + Claude] · Design** — Écran 6 (erreur) repensé : la carte d'erreur et le bouton « Essayer un autre lien » disparaissent ; l'erreur passe sous le champ (bordure rouge, une ligne de message, lien discret « Copier les détails techniques »). Quand l'erreur impose de changer de lien (vidéo privée, lien invalide), un bouton ✕ dans le champ remplace « Analyser », qui est masqué ; le ✕ vide le champ et remet le focus. Erreur réseau (6c) : « Réessayer » à la place d'« Analyser », le lien n'étant pas en cause. Quatre variantes 6 / 6b / 6c / 6d (vidéo privée, lien invalide, réseau, vidéo indisponible) en clair et sombre, PNG régénérés. Non validé.
- **[Armistice + Claude] · Design** — Premier jet des écrans secondaires (clair et sombre, `maquette/ecrans/ecrans-secondaires-*`) : A premier lancement (avertissement et acceptation, PRD §10), B Paramètres (FR-60), C Historique de la session (FR-38) et son état vide, D À propos (FR-62, mise à jour manuelle du moteur FR-53), E Licences des composants tiers. Non validé. Points relevés : texte de l'avertissement provisoire à faire relire (D-009) ; licences de FFmpeg et CliWrap marquées « À confirmer » (audit PRD §10) ; pas de réglage de langue (FR/EN) car absent de FR-60 ; versions affichées (moteur, FFmpeg) et noms de vidéos = exemples.
- **[Armistice + Claude] · Design** — Écrans 4, 4b et 5 : les icônes (menu à trois points ; Ouvrir / Dossier / Fermer) passent sur la ligne du titre, la barre de progression occupe désormais toute la largeur de la carte, alignée avec le bord droit des détails.
- **[Armistice + Claude] · Design** — Écrans 4 et 5 retouchés une seconde fois : écran 4, pourcentage remplacé par un menu à trois points (Pause, Annuler) et bouton « Annuler » du bas supprimé, état « menu ouvert » ajouté (4b) ; écran 5, mention « Terminé » remplacée par trois boutons-icônes Ouvrir / Afficher dans le dossier / Fermer, boutons larges du bas retirés (doublon), seul « Télécharger une autre vidéo » reste. Le menu propose « Pause », absente du PRD (FR-32 annulation, FR-33 reprise) : point ouvert ci-dessous.
- **[Armistice + Claude] · Design** — Écrans 4 et 5 retouchés sur demande : étapes Image / Son / Assemblage / Finalisation supprimées (jugées peu professionnelles) ; progression dans une carte unique à la manière des téléchargements de Chrome (titre, barre juste dessous, puis chaîne à gauche et détails à droite). À l'écran 5, la même carte passe à une barre pleine aqua avec « Terminé » et la taille du fichier.
- **[Armistice + Claude] · Design** — Premier jet de l'écran principal : 6 états (vide, analyse, prévisualisation, téléchargement, terminé, erreur) en clair et en sombre, `maquette/ecrans/` ; valeurs de mise en page proposées dans `tokens-layout.json` (fenêtre 880×640, colonne de contenu 640, rayons 8/12, hauteurs de contrôle 40–56). Non validé : pas de décision enregistrée. Choix à confirmer : « Annuler » à la place de « Analyser » pendant l'analyse, suggestion du presse-papiers sous le champ.
- **[Armistice + Claude] · Design** — Mot « DVA Stream » du logo redessiné en Plus Jakarta Sans (D-014) ; 4 fichiers `logo-horizontal*.svg` et planche mis à jour.
- **[Armistice + Claude] · Design** — Police retenue : Plus Jakarta Sans (D-013), 4 graisses converties en TTF, échelle typographique, `typography.json`. Constat : les chiffres sont proportionnels par défaut, `tnum` à activer dans Avalonia ; le symbole « → » est absent de la police.
- **[Armistice + Claude] · Design** — Palette validée avec l'accent aqua (D-012) : `tokens.json`, `Tokens.colors.axaml` (brouillon non compilé), planche avec aperçu clair / sombre. Variante ambre produite puis écartée, archivée dans `alternatives/`.
- **[Armistice + Claude] · Design** — Logo conçu puis validé (D-011) : monogramme, logo horizontal (4 versions), icônes d'application (SVG, PNG, ICO), planche de présentation. Création du dossier `maquette/` avec son `README.md`.
- **[Armistice + Claude] · Cadrage** — `PROJECT.md` mis à jour (v0.2) : stack et décisions actuelles, feuille de route MVP complétée, liste des documents du projet.
- **[Armistice + Claude] · Cadrage** — `docs/PRD.md` remplacé par l'export du document en ligne ; contenu identique à la copie précédente (seul le format des tableaux Markdown change).
- **[Armistice + Claude] · Cadrage** — TDD passé en v0.2 : décisions D-004 à D-009 intégrées (ADR-04, ADR-05, ADR-08, sections 7, 8.5, 19), copie ajoutée dans `docs/TDD.md`.
- **[Armistice + Claude] · Cadrage** — Création de `PLAN.md` (découpage de M0 à M8 en sous-tâches) et de `JOURNAL.md`.
- **[Armistice + Claude] · Cadrage** — Réponses aux questions ouvertes du TDD enregistrées (D-004 à D-009).
- **[Armistice + Claude] · Cadrage** — TDD v0.1 rédigé (décisions, structure, contrats, flux, tests, CI, jalons M0 à M8, spikes, risques).
- **[Armistice + Claude] · Dépôt** — Dépôt Git initialisé (branche `main`), premier commit avec `docs/PROJECT.md` et `docs/PRD.md`.
- **[Armistice + Claude] · Cadrage** — PRD v0.1 rédigé puis validé.
- **[Armistice + Claude] · Cadrage** — Revue de `PROJECT.md` et recommandations adoptées (yt-dlp principal, FFmpeg indispensable, .NET 10, architecture en 3 projets, annulation/reprise, journalisation, mise à jour du moteur, paramètres persistants, packaging CI).
