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

**Dernière mise à jour :** 2026-11-18 · **Phase actuelle :** Cadrage terminé, avant la phase Design

| Jalon | Contenu | Statut |
|---|---|---|
| Cadrage | PROJECT (v0.2), PRD (v0.1), TDD (v0.2), PLAN, JOURNAL | **Terminé** |
| Design | Maquettes, logo, police, palette, tokens | À faire (prochaine phase) |
| M0 — Fondations | Solution, DI, logs, CI, thème vide | À faire |
| M1 — Analyse d'URL | Parseur, saisie, presse-papiers | À faire |
| M2 — Prévisualisation | Métadonnées yt-dlp, carte de prévisualisation | À faire |
| M3 — Formats | Sélecteur, paramètres persistants, thème | À faire |
| M4 — Téléchargement | Progression, fusion, fichier final | À faire |
| M5 — Contrôle et robustesse | Annulation, reprise, erreurs typées | À faire |
| M6 — Moteur à jour | Mise à jour indépendante de yt-dlp | À faire |
| M7 — Finitions | MP3, historique, À propos, EN, accessibilité | À faire |
| M8 — Distribution | Velopack, signature, release 1.0 | À faire |

**Prochaine action :** phase Design (maquettes, logo, police, palette), puis démarrage de M0.

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

---

## Blocages et points en attente

| Point | Depuis | Responsable | Statut |
|---|---|---|---|
| Avis juridique avant toute distribution publique | 2026-11-18 | DVA Labs | Ouvert (avant M8) |
| Identité Git du dépôt (nom et email de l'auteur des commits) | 2026-11-18 | À définir | Ouvert |

---

## Journal chronologique

### 2026-11-18
- **[Armistice + Claude] · Cadrage** — `PROJECT.md` mis à jour (v0.2) : stack et décisions actuelles, feuille de route MVP complétée, liste des documents du projet.
- **[Armistice + Claude] · Cadrage** — `docs/PRD.md` remplacé par l'export du document en ligne ; contenu identique à la copie précédente (seul le format des tableaux Markdown change).
- **[Armistice + Claude] · Cadrage** — TDD passé en v0.2 : décisions D-004 à D-009 intégrées (ADR-04, ADR-05, ADR-08, sections 7, 8.5, 19), copie ajoutée dans `docs/TDD.md`.
- **[Armistice + Claude] · Cadrage** — Création de `PLAN.md` (découpage de M0 à M8 en sous-tâches) et de `JOURNAL.md`.
- **[Armistice + Claude] · Cadrage** — Réponses aux questions ouvertes du TDD enregistrées (D-004 à D-009).
- **[Armistice + Claude] · Cadrage** — TDD v0.1 rédigé (décisions, structure, contrats, flux, tests, CI, jalons M0 à M8, spikes, risques).
- **[Armistice + Claude] · Dépôt** — Dépôt Git initialisé (branche `main`), premier commit avec `docs/PROJECT.md` et `docs/PRD.md`.
- **[Armistice + Claude] · Cadrage** — PRD v0.1 rédigé puis validé.
- **[Armistice + Claude] · Cadrage** — Revue de `PROJECT.md` et recommandations adoptées (yt-dlp principal, FFmpeg indispensable, .NET 10, architecture en 3 projets, annulation/reprise, journalisation, mise à jour du moteur, paramètres persistants, packaging CI).
