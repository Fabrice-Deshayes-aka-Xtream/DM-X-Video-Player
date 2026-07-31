using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace DMXVideoPlayer.Services
{
    /// <summary>
    /// Service de gestion des mises à jour automatiques via Velopack
    /// </summary>
    public class UpdateService
    {
        private readonly UpdateManager? _updateManager;
        private VelopackAsset? _pendingUpdate;
        private const string GitHubRepoUrl = "https://github.com/Fabrice-Deshayes-aka-Xtream/DM-X-Video-Player";

        public UpdateService()
        {
            try
            {
                var source = new GithubSource(GitHubRepoUrl, string.Empty, false);
                _updateManager = new UpdateManager(source);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de l'initialisation de UpdateManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Vérifie si une mise à jour est disponible
        /// </summary>
        /// <returns>Informations sur la mise à jour disponible, ou null si aucune mise à jour</returns>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (_updateManager == null)
            {
                Debug.WriteLine("UpdateManager non initialisé - pas de vérification de mise à jour");
                return null;
            }

            try
            {
                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo != null)
                {
                    Debug.WriteLine($"Mise à jour disponible: v{updateInfo.TargetFullRelease.Version}");
                    return updateInfo;
                }
                else
                {
                    Debug.WriteLine("Aucune mise à jour disponible");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de la vérification de mise à jour: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Télécharge et applique une mise à jour
        /// </summary>
        /// <param name="updateInfo">Informations sur la mise à jour à télécharger</param>
        /// <param name="progress">Callback pour suivre la progression (0-100)</param>
        /// <returns>True si la mise à jour a été téléchargée avec succès</returns>
        public async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, Action<int>? progress = null)
        {
            if (_updateManager == null)
            {
                Debug.WriteLine("UpdateManager non initialisé - impossible de télécharger la mise à jour");
                return false;
            }

            try
            {
                Debug.WriteLine($"Téléchargement de la mise à jour v{updateInfo.TargetFullRelease.Version}...");

                // Télécharger la mise à jour avec suivi de progression
                await _updateManager.DownloadUpdatesAsync(updateInfo, p => progress?.Invoke(p));

                // Stocker l'asset pour l'appliquer plus tard
                _pendingUpdate = updateInfo.TargetFullRelease;

                Debug.WriteLine("Mise à jour téléchargée avec succès - sera appliquée au prochain redémarrage");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors du téléchargement de la mise à jour: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applique la mise à jour et redémarre l'application
        /// </summary>
        public void ApplyUpdateAndRestart()
        {
            if (_updateManager == null || _pendingUpdate == null)
            {
                Debug.WriteLine("UpdateManager non initialisé ou aucune mise à jour en attente");
                return;
            }

            try
            {
                Debug.WriteLine("Application de la mise à jour et redémarrage...");
                _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de l'application de la mise à jour: {ex.Message}");
            }
        }

        /// <summary>
        /// Applique la mise à jour et quitte l'application (sans redémarrage)
        /// </summary>
        public void ApplyUpdateAndExit()
        {
            if (_updateManager == null || _pendingUpdate == null)
            {
                Debug.WriteLine("UpdateManager non initialisé ou aucune mise à jour en attente");
                return;
            }

            try
            {
                Debug.WriteLine("Application de la mise à jour et fermeture...");
                _updateManager.ApplyUpdatesAndExit(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de l'application de la mise à jour: {ex.Message}");
            }
        }

        /// <summary>
        /// Version actuelle de l'application
        /// </summary>
        public string CurrentVersion => _updateManager?.CurrentVersion?.ToString() ?? "1.0.0";
    }
}
