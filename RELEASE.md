# Guide de Release - DM-X Video Player

Ce document explique comment créer et publier une nouvelle version de DM-X Video Player avec le système de mise à jour automatique Velopack.

## Prérequis

- **Velopack CLI (vpk)** : Déjà installé ✅
- **GitHub Token** (pour la publication automatique) : Optionnel
- **Accès au dépôt GitHub** : https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player

## Étape 1 : Préparation de la version

1. **Mettre à jour le numéro de version**
   - Le script `build-and-pack.ps1` met à jour automatiquement la version dans le `.csproj`
   - Format : `MAJOR.MINOR.PATCH` (ex: 1.0.0, 1.1.0, 2.0.0)

2. **Tester l'application**
   - Compilez et testez en mode Debug
   - Vérifiez que toutes les fonctionnalités fonctionnent

## Étape 2 : Build et packaging

### Méthode automatique (recommandée)

Utilisez le script PowerShell fourni :

```powershell
# Build + Package
.\build-and-pack.ps1 -Version 1.0.0

# Build + Package + Publication automatique sur GitHub
.\build-and-pack.ps1 -Version 1.0.0 -Publish
```

### Méthode manuelle

```powershell
# 1. Build en mode Release
dotnet build "DM-X Video Player.csproj" -c Release

# 2. Publish
dotnet publish "DM-X Video Player.csproj" -c Release

# 3. Package avec Velopack
vpk pack --packId DM-X-Video-Player `
		 --packVersion 1.0.0 `
		 --packDir "bin\Release\net10.0-windows10.0.26100.0\win-x64\publish" `
		 --mainExe "DM-X Video Player.exe" `
		 --outputDir "Releases" `
		 --icon "Assets\DMX-Video-Player.ico"
```

## Étape 3 : Fichiers générés

Le dossier `Releases` contient :

- **RELEASES** : Fichier index des versions (requis par Velopack)
- **DM-X-Video-Player-{version}-full.nupkg** : Package complet
- **DM-X-Video-Player-{version}-delta.nupkg** : Update incrémentielle (si applicable)
- **DM-X-Video-Player-Setup.exe** : Installateur standalone

## Étape 4 : Publication sur GitHub

### Méthode 1 : Via GitHub Web Interface

1. Allez sur https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player/releases
2. Cliquez sur **"Draft a new release"**
3. Remplissez :
   - **Tag** : `1.0.0` (ou format 'v1.0.0')
   - **Release title** : `DM-X Video Player v1.0.0`
   - **Description** : Notes de version (changelog)
4. **Uploadez tous les fichiers** du dossier `Releases`
5. Cliquez sur **"Publish release"**

### Méthode 2 : Via vpk CLI (automatique)

```powershell
# Définir le token GitHub (une seule fois par session)
$env:GITHUB_TOKEN = "ghp_votre_token_ici"

# Publier
vpk upload github `
	--repoUrl "https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player" `
	--tag "v1.0.0" `
	--releaseName "DM-X Video Player v1.0.0" `
	--token $env:GITHUB_TOKEN `
	--publish `
	--releasesDir "Releases"
```

### Créer un GitHub Token

1. Allez sur https://github.com/settings/tokens
2. Cliquez **"Generate new token"** → **"Generate new token (classic)"**
3. Permissions requises :
   - ✅ `repo` (Full control of private repositories)
4. Copiez le token généré

## Étape 5 : Distribution

### Première installation

Les utilisateurs peuvent installer l'application via :

1. **Installateur direct** : `DM-X-Video-Player-Setup.exe`
2. **Téléchargement manuel** : Depuis la page Releases GitHub

### Mises à jour automatiques

Une fois installée via Velopack :
- L'application vérifie automatiquement les mises à jour au démarrage
- L'utilisateur est notifié quand une nouvelle version est disponible
- Un simple clic télécharge et applique la mise à jour

## Cycle de version recommandé

### Versions majeures (X.0.0)
- Changements architecturaux importants
- Nouvelles fonctionnalités majeures
- Modifications de compatibilité

### Versions mineures (1.X.0)
- Nouvelles fonctionnalités
- Améliorations significatives
- Ajout de support pour nouveaux formats/protocoles

### Versions patch (1.0.X)
- Corrections de bugs
- Petites améliorations de performance
- Mises à jour de dépendances

## Dépannage

### L'application ne détecte pas les mises à jour

Vérifiez que :
1. La release GitHub contient **TOUS** les fichiers du dossier `Releases`
2. Le tag GitHub commence par `v` (ex: `v1.0.0`)
3. Le fichier `RELEASES` est présent dans les assets de la release
4. L'URL du dépôt dans `UpdateService.cs` est correcte

### Erreur lors du packaging

```powershell
# Nettoyez et recommencez
dotnet clean
Remove-Item -Recurse -Force bin, obj, Releases
.\build-and-pack.ps1 -Version 1.0.0
```

### L'installateur ne fonctionne pas

Vérifiez que :
- Le fichier `.exe` principal est bien nommé `DM-X Video Player.exe`
- Tous les fichiers natifs (libvlc) sont inclus dans le dossier publish
- L'icône existe à `Assets\DMX-Video-Player.ico`

## Checklist de release

- [ ] Code testé et fonctionnel
- [ ] Build réussit sans warnings
- [ ] Package créé avec `build-and-pack.ps1`
- [ ] Fichiers générés dans `Releases/`
- [ ] Release créée sur GitHub avec tag `vX.Y.Z`
- [ ] Tous les fichiers uploadés sur GitHub
- [ ] Release publiée (non draft)
- [ ] Installateur testé sur une machine propre
- [ ] Mise à jour automatique testée depuis version précédente

## Ressources

- Documentation Velopack : https://docs.velopack.io/
- GitHub Releases : https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player/releases
- Dépôt du projet : https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player
