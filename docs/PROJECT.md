# PROJECT.md — DVA Stream (by DVA Labs)

## 1. Identité
* **Entreprise  :** DVA Labs 
* **Produit :** DVA Stream


## 2. Présentation du Projet
**DVA Stream** est une application de bureau multiplateforme (Windows, macOS, Linux) permettant de télécharger des vidéos et d'extraire des pistes audio depuis YouTube à partir d'une simple URL.

## 3. Problèmes Résolus
1. **Sécurité et confort utilisateur :** Élimine le recours aux sites web de téléchargement remplis de publicités agressives, de traqueurs et de logiciels malveillants.
2. **Contrôle des formats et qualités :** Permet à l'utilisateur de choisir précisément la résolution vidéo (720p, 1080p, 4K) ou l'extraction audio haute fidélité sans compression abusive.
3. **Expérience native et fluide :** Fonctionne localement, une barre de progression précise et un accès direct aux fichiers enregistrés.

## 4. Stack Technique & Architecture
* **Langage & Runtime :** .NET (dernière version LTS)
* **Framework UI :** Avalonia UI
* **Moteur d'extraction & téléchargement :** `YoutubeExplode` (avec possibilité d'intégration `yt-dlp` / `FFmpeg` pour le muxing haute définition)
* **Direction Artistique & UI :** Interface épurée, moderne et corporate, privilégiant une typographie stylisée et lisible, des espaces bien structurés et un mode sombre/clair soigné.

## 5. Feuille de Route (MVP - Version 1.0)
- [ ] **Analyse d'URL :** Champ de saisie avec détection intelligente du presse-papiers.
- [ ] **Prévisualisation :** Récupération et affichage de la miniature, du titre, de la chaîne et de la durée.
- [ ] **Sélecteur de format :** Choix entre Vidéo (MP4) avec sélection de résolution, et Audio seul (M4A / MP3).
- [ ] **Gestionnaire de téléchargement :** Barre de progression en temps réel (pourcentage, vitesse, taille restante) et choix du dossier de destination.
- [ ] **Gestion des erreurs :** Messages clairs en cas de lien invalide, de vidéo privée ou de coupure réseau.
