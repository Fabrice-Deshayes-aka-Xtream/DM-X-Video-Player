# Script de build et packaging pour DM-X Video Player avec Velopack
# Ce script compile l'application en mode Release et crée le package d'installation

param(
	[string]$Version = "1.0.0",
	[switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

Write-Host "===================================" -ForegroundColor Cyan
Write-Host "DM-X Video Player - Build & Package" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$ProjectFile = "DM-X Video Player.csproj"
$OutputDir = "bin\Release\net10.0-windows10.0.26100.0\win-x64"
$PublishDir = "bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"
$ReleasesDir = "Releases"
$AppName = "DM-X Video Player.exe"
$PackId = "DM-X-Video-Player"

# Mettre à jour la version dans le csproj
Write-Host "Mise à jour de la version dans $ProjectFile..." -ForegroundColor Yellow
$csprojContent = Get-Content $ProjectFile -Raw
$csprojContent = $csprojContent -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
Set-Content $ProjectFile $csprojContent -NoNewline

# Build
if (-not $SkipBuild) {
	Write-Host ""
	Write-Host "Build de l'application..." -ForegroundColor Yellow
	dotnet build $ProjectFile -c Release -p:Platform=x64
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Erreur lors du build!" -ForegroundColor Red
		exit 1
	}

	Write-Host ""
	Write-Host "Publication de l'application avec le profil FolderProfile..." -ForegroundColor Yellow
	dotnet publish $ProjectFile -p:PublishProfile=FolderProfile
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Erreur lors de la publication!" -ForegroundColor Red
		exit 1
	}
}

# Créer le dossier de releases
if (-not (Test-Path $ReleasesDir)) {
	New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
}
else {
	Get-ChildItem -Path $ReleasesDir -Force | Remove-Item -Recurse -Force
}

# Packaging avec Velopack
Write-Host ""
Write-Host "Packaging avec Velopack..." -ForegroundColor Yellow
Write-Host "  Source: $PublishDir" -ForegroundColor Gray
Write-Host "  Destination: $ReleasesDir" -ForegroundColor Gray

vpk pack --packId $PackId `
		 --packVersion $Version `
		 --packDir $PublishDir `
		 --mainExe $AppName `
		 --outputDir $ReleasesDir `
		 --icon "Assets\DMX-Video-Player.ico"

if ($LASTEXITCODE -ne 0) {
	Write-Host "Erreur lors du packaging!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "Build et packaging terminés avec succès!" -ForegroundColor Green
Write-Host "Package disponible dans: $ReleasesDir" -ForegroundColor Green

# Afficher les fichiers générés
Write-Host ""
Write-Host "Fichiers générés:" -ForegroundColor Cyan
Get-ChildItem $ReleasesDir | ForEach-Object {
	Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "Pour publier sur GitHub:" -ForegroundColor Cyan
Write-Host "  1. Créez une nouvelle release sur GitHub avec le tag $Version" -ForegroundColor White
Write-Host "  2. Uploadez tous les fichiers du dossier $ReleasesDir" -ForegroundColor White
Write-Host ""
