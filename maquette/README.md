# Maquette — DVA Stream

Livrables de la phase Design. Chaque dossier contient une planche de présentation (`planche-*.html` / `.png`) et les fichiers sources.

| Dossier | Contenu | État |
|---|---|---|
| `logo/` | Monogramme (D + « play »), logo horizontal, icônes d'application (SVG, PNG 16–1024 px, ICO), planche | Validé |
| `palette/` | Tokens clair / sombre (`tokens.json`, `Tokens.colors.axaml`), accent aqua `#22D3C5` ; `alternatives/` = variante ambre écartée | Validé |
| `police/` | Plus Jakarta Sans (TTF, licence OFL), échelle typographique (`typography.json`), comparatif des 4 candidates | Validé |
| `ecrans/` | Écran principal (`ecran-principal-*`) et écrans secondaires : premier lancement, Paramètres, Historique, À propos (`ecrans-secondaires-*`), en clair et sombre ; `tokens-layout.json` (espacements et rayons figés, v1.0), `Tokens.layout.axaml` | Premier jet, à valider ; tokens de mise en page figés |

Les planches HTML s'ouvrent dans un navigateur (chemins relatifs). `Tokens.colors.axaml` et `Tokens.layout.axaml` sont des brouillons non compilés : il sert de base aux tâches M0-06 et M3-08 du `PLAN.md`.
