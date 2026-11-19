# DVA Stream

Application de bureau multiplateforme (Windows, macOS, Linux) pour télécharger des vidéos et extraire l'audio depuis YouTube, en local et sans publicité. Produit de **DVA Labs**.

> Projet en cours de construction (jalon M0). Les documents de référence sont dans [`docs/`](docs/).

## Documents

| Document | Rôle |
|---|---|
| [`docs/PROJECT.md`](docs/PROJECT.md) | Identité, stack, feuille de route |
| [`docs/PRD.md`](docs/PRD.md) | Exigences produit |
| [`docs/TDD.md`](docs/TDD.md) | Conception technique |
| [`docs/PLAN.md`](docs/PLAN.md) | Plan d'implémentation en tâches |
| [`docs/JOURNAL.md`](docs/JOURNAL.md) | État d'avancement, décisions, historique |
| [`maquette/`](maquette/) | Logo, palette, police, écrans |

**Règle :** tout contributeur consigne son travail dans `docs/JOURNAL.md` et coche les tâches dans `docs/PLAN.md`.

## Structure

```
src/
  DvaStream.Core            domaine et interfaces, sans dépendance externe
  DvaStream.Infrastructure  yt-dlp, FFmpeg, fichiers, journalisation
  DvaStream.App             interface Avalonia (MVVM)
tests/
  DvaStream.Core.Tests
  DvaStream.Infrastructure.Tests   (inclut les tests d'architecture)
```

Règles de dépendances : `Core` ne référence rien d'externe ; `Infrastructure` référence `Core` ; `App` référence `Core` et `Infrastructure`. Ces règles sont vérifiées par `ArchitectureTests`.

## Prérequis

- [SDK .NET 10](https://dotnet.microsoft.com/download) (version indiquée dans `global.json`)

## Commandes

```bash
dotnet restore DvaStream.slnx
dotnet build DvaStream.slnx
dotnet test DvaStream.slnx
dotnet run --project src/DvaStream.App
```

Les journaux sont écrits dans le dossier de données de l'application (`.../DvaStream/logs`).

## Licences

Police Plus Jakarta Sans sous licence OFL (voir `src/DvaStream.App/Assets/Fonts/OFL.txt`).
