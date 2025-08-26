using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using ItunesRPC.Properties;

namespace ItunesRPC.Services
{
    public class UpdateService : IDisposable
    {
        private string _owner = "darkiifr";
        private string _repo = "ITunesRPC";
        private readonly Version _currentVersion;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _updateSemaphore;
        private bool _disposed = false;
        
        // URL de base pour les releases GitHub
        private string _githubReleaseUrl = "https://github.com/darkiifr/ITunesRPC/releases";
        
        // Configuration des timeouts et retry
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 2000;
        private const int ApiTimeoutSeconds = 30;
        private const int DownloadTimeoutMinutes = 10;
        private const int ProgressUpdateIntervalMs = 500;
        private const int BufferSize = 81920; // 80KB buffer

        public UpdateService()
        {
            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            
            // Initialiser HttpClient avec configuration optimisée
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(DownloadTimeoutMinutes);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ITunesRPC/{_currentVersion}");
            
            // Semaphore pour éviter les vérifications simultanées
            _updateSemaphore = new SemaphoreSlim(1, 1);
            
            // Charger les paramètres de mise à jour depuis les settings
            LoadUpdateSettings();
        }
        
        private void LoadUpdateSettings()
        {
            try
            {
                // Charger les paramètres de l'URL GitHub si disponibles
                if (!string.IsNullOrEmpty(Settings.Default.GitHubOwner))
                    _owner = Settings.Default.GitHubOwner;
                    
                if (!string.IsNullOrEmpty(Settings.Default.GitHubRepo))
                    _repo = Settings.Default.GitHubRepo;
                    
                if (!string.IsNullOrEmpty(Settings.Default.GitHubReleaseUrl))
                    _githubReleaseUrl = Settings.Default.GitHubReleaseUrl;
            }
            catch (Exception ex)
            {
                // Erreur lors du chargement des paramètres de mise à jour
            }
        }
        
        public void SaveUpdateSettings(string owner, string repo, string releaseUrl)
        {
            try
            {
                Settings.Default.GitHubOwner = owner;
                Settings.Default.GitHubRepo = repo;
                Settings.Default.GitHubReleaseUrl = releaseUrl;
                Settings.Default.Save();
                
                // Mettre à jour les variables locales
                _owner = owner;
                _repo = repo;
                _githubReleaseUrl = releaseUrl;
                
                MessageBox.Show(
                    "Les paramètres de mise à jour ont été enregistrés avec succès.",
                    "Paramètres enregistrés",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors de l'enregistrement des paramètres de mise à jour: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Méthode pour exécuter une opération avec retry et backoff exponentiel
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < MaxRetryAttempts && IsRetryableException(ex))
                {
                    lastException = ex;
                    var delay = TimeSpan.FromMilliseconds(RetryDelayMs * Math.Pow(2, attempt - 1));
                    NotifyUpdateStatus($"Tentative {attempt}/{MaxRetryAttempts} échouée, nouvelle tentative dans {delay.TotalSeconds:F1}s...");
                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
            
            // Si toutes les tentatives ont échoué, relancer la dernière exception
            throw lastException ?? new InvalidOperationException("Toutes les tentatives ont échoué");
        }
        
        // Détermine si une exception justifie une nouvelle tentative
        private static bool IsRetryableException(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   (ex is Octokit.ApiException apiEx && (apiEx.HttpResponse?.StatusCode == HttpStatusCode.TooManyRequests ||
                                                        apiEx.HttpResponse?.StatusCode == HttpStatusCode.InternalServerError ||
                                                        apiEx.HttpResponse?.StatusCode == HttpStatusCode.BadGateway ||
                                                        apiEx.HttpResponse?.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                                        apiEx.HttpResponse?.StatusCode == HttpStatusCode.GatewayTimeout));
        }

        // Événement pour notifier les changements de statut de mise à jour
        public event EventHandler<string>? UpdateStatusChanged;
        
        // Méthode pour notifier les changements de statut
        private void NotifyUpdateStatus(string status)
        {
            try
            {
                UpdateStatusChanged?.Invoke(this, status);
            }
            catch (Exception ex)
            {
                // Erreur lors de la notification de statut
            }
        }
        
        // Variables pour le calcul de vitesse
        private DateTime _downloadStartTime;
        private long _lastDownloadedBytes;
        
        // Calcule la vitesse de téléchargement en MB/s
        private double CalculateDownloadSpeed(long totalDownloadedBytes, DateTime currentTime)
        {
            if (_downloadStartTime == default)
            {
                _downloadStartTime = currentTime;
                _lastDownloadedBytes = totalDownloadedBytes;
                return 0;
            }
            
            var elapsedSeconds = (currentTime - _downloadStartTime).TotalSeconds;
            if (elapsedSeconds <= 0) return 0;
            
            var bytesPerSecond = totalDownloadedBytes / elapsedSeconds;
            return bytesPerSecond / (1024.0 * 1024.0); // Convertir en MB/s
        }

        private async Task<string> CalculateFileChecksumAsync(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = await Task.Run(() => sha256.ComputeHash(stream)).ConfigureAwait(false);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                // Erreur lors du calcul du checksum
                return string.Empty;
            }
        }

        private async Task<string?> GetExpectedChecksumAsync(Release release, string assetName)
        {
            try
            {
                // Chercher un fichier de checksum dans les assets (SHA256SUMS, checksums.txt, etc.)
                var checksumAssets = release.Assets.Where(a => 
                    a.Name.ToLower().Contains("checksum") || 
                    a.Name.ToLower().Contains("sha256") ||
                    a.Name.ToLower().Contains("hash") ||
                    a.Name.EndsWith(".sha256") ||
                    a.Name.EndsWith(".md5") ||
                    a.Name.EndsWith(".txt") && (a.Name.ToLower().Contains("sum") || a.Name.ToLower().Contains("hash"))
                ).ToList();

                foreach (var checksumAsset in checksumAssets)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        var response = await _httpClient.GetStringAsync(checksumAsset.BrowserDownloadUrl, cts.Token).ConfigureAwait(false);
                        
                        // Analyser le contenu du fichier de checksum
                        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var hash = parts[0];
                                var fileName = parts[1].TrimStart('*'); // Enlever le * des fichiers binaires
                                
                                if (fileName.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return hash.ToLowerInvariant();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Erreur lors de la lecture du fichier de checksum
                    }
                }
                
                // Chercher dans la description de la release
                if (!string.IsNullOrEmpty(release.Body))
                {
                    var lines = release.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.ToLower().Contains(assetName.ToLower()) && 
                            (line.ToLower().Contains("sha256") || line.ToLower().Contains("checksum")))
                        {
                            // Extraire le hash de la ligne (chercher une chaîne de 64 caractères hexadécimaux)
                            var words = line.Split(new[] { ' ', '\t', ':', '|', '-' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var word in words)
                            {
                                if (word.Length == 64 && word.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                                {
                                    return word.ToLowerInvariant();
                                }
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Erreur lors de la récupération du checksum attendu
                return null;
            }
        }
        
        public async Task CheckForUpdatesAsync(bool showNoUpdateMessage = false)
        {
            // Éviter les vérifications simultanées
            if (!await _updateSemaphore.WaitAsync(100).ConfigureAwait(false))
            {
                NotifyUpdateStatus("Vérification de mise à jour déjà en cours...");
                return;
            }

            try
            {
                // Vérifier si l'objet a été disposé
                if (_disposed)
                {
                    NotifyUpdateStatus("Service de mise à jour non disponible");
                    return;
                }

                // Notifier le début de la vérification
                NotifyUpdateStatus("Vérification des mises à jour en cours...");
                
                // Vérifier que les paramètres GitHub sont valides
                if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
                {
                    NotifyUpdateStatus("Paramètres de mise à jour invalides");
                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show(
                            "Les paramètres GitHub ne sont pas configurés correctement. Veuillez les configurer dans les paramètres.",
                            "Erreur de mise à jour",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    return;
                }
                
                // Vérifier la connectivité réseau avant de continuer
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    NotifyUpdateStatus("Aucune connexion réseau disponible");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                "Aucune connexion réseau disponible. Vérifiez votre connexion Internet.",
                                "Erreur de connexion",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                    return;
                }
                
                NotifyUpdateStatus("Connexion à GitHub...");
                
                // Créer un client GitHub avec un timeout raisonnable
                var github = new GitHubClient(new ProductHeaderValue("ITunesRPC-UpdateCheck"));
                
                // Obtenir la dernière release avec gestion des erreurs réseau
                try {
                    NotifyUpdateStatus("Recherche de nouvelles versions...");
                    
                    // Appel avec retry logic
                    var latestRelease = await ExecuteWithRetryAsync(async () =>
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ApiTimeoutSeconds));
                        var releases = await github.Repository.Release.GetAll(_owner, _repo).ConfigureAwait(false);
                        return releases.FirstOrDefault();
                    }).ConfigureAwait(false);
                    
                    if (latestRelease == null)
                    {
                        NotifyUpdateStatus("Aucune mise à jour disponible");
                        if (showNoUpdateMessage)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                MessageBox.Show(
                                    "Aucune mise à jour disponible sur le dépôt GitHub.",
                                    "Mise à jour",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                        return;
                    }
                    
                    // Extraire la version de la release
                    string tagName = latestRelease.TagName.TrimStart('v');
                    if (!Version.TryParse(tagName, out Version? latestVersion) || latestVersion == null)
                    {
                        NotifyUpdateStatus("Erreur: Format de version invalide");
                        if (showNoUpdateMessage)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                MessageBox.Show(
                                    $"Impossible de déterminer la version de la dernière mise à jour.\nFormat de version reçu: {latestRelease.TagName}",
                                    "Erreur de mise à jour",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        }
                        return;
                    }
                    
                    // Comparer les versions
                    if (latestVersion > _currentVersion)
                    {
                        // Une mise à jour est disponible
                        NotifyUpdateStatus($"Mise à jour disponible: v{latestVersion}");
                        
                        var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            return MessageBox.Show(
                                $"Une nouvelle version est disponible: {latestVersion}\n\nVersion actuelle: {_currentVersion}\n\nVoulez-vous la télécharger et l'installer maintenant?",
                                "Mise à jour disponible",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);
                        });
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            // Télécharger et installer automatiquement
                            await DownloadAndInstallUpdateAsync(latestRelease).ConfigureAwait(false);
                        }
                        else
                        {
                            NotifyUpdateStatus("Mise à jour reportée");
                        }
                    }
                    else if (latestVersion == _currentVersion)
                    {
                        // Même version - aucune mise à jour nécessaire
                        NotifyUpdateStatus($"Vous utilisez la dernière version ({_currentVersion})");
                        if (showNoUpdateMessage)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                MessageBox.Show(
                                    $"Vous utilisez déjà la dernière version ({_currentVersion}).",
                                    "Mise à jour",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                    }
                    else
                    {
                        // Version actuelle plus récente que la dernière release (version de développement)
                        NotifyUpdateStatus($"Version de développement détectée ({_currentVersion})");
                        if (showNoUpdateMessage)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                MessageBox.Show(
                                    $"Vous utilisez une version de développement ({_currentVersion}) plus récente que la dernière version stable ({latestVersion}).",
                                    "Mise à jour",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    NotifyUpdateStatus("Timeout lors de la vérification des mises à jour");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                "La vérification des mises à jour a pris trop de temps. Vérifiez votre connexion Internet.",
                                "Timeout",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                }
                catch (Octokit.RateLimitExceededException)
                {
                    NotifyUpdateStatus("Limite de requêtes GitHub dépassée. Réessayez plus tard.");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                "Limite de requêtes GitHub dépassée. Veuillez réessayer plus tard.",
                                "Erreur de mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                }
                catch (Octokit.NotFoundException)
                {
                    NotifyUpdateStatus("Dépôt GitHub non trouvé. Vérifiez les paramètres.");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                $"Le dépôt {_owner}/{_repo} n'a pas été trouvé. Vérifiez les paramètres de mise à jour.",
                                "Erreur de mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                }
                catch (Octokit.ApiException apiEx)
                {
                    NotifyUpdateStatus($"Erreur API GitHub: {apiEx.Message}");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                $"Erreur de l'API GitHub: {apiEx.Message}\nCode d'erreur: {apiEx.HttpResponse?.StatusCode}",
                                "Erreur de mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    NotifyUpdateStatus($"Erreur de connexion: {httpEx.Message}");
                    if (showNoUpdateMessage)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                $"Erreur de connexion réseau: {httpEx.Message}",
                                "Erreur de connexion",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus($"Erreur: {ex.Message}");
                if (showNoUpdateMessage)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"Erreur inattendue lors de la vérification des mises à jour: {ex.Message}",
                            "Erreur de mise à jour",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
                // Erreur lors de la vérification des mises à jour
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        private string GetPlatformAssetName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "mac";
            }
            
            // Par défaut, retourner windows
            return "windows";
        }

        private ReleaseAsset? SelectBestAsset(Release release)
        {
            string platformName = GetPlatformAssetName();
            
            // Filtrer les assets compatibles avec la plateforme actuelle
            var compatibleAssets = FilterCompatibleAssets(release.Assets, platformName);
            
            if (!compatibleAssets.Any())
            {
                return null;
            }
            
            // Chercher d'abord un asset spécifique à la plateforme
            var platformAsset = compatibleAssets.FirstOrDefault(a => 
                a.Name.ToLower().Contains(platformName));
            
            if (platformAsset != null)
                return platformAsset;
            
            // Si pas d'asset spécifique, chercher par extension selon la plateforme
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Pour Windows, priorité aux .exe puis .zip (mais éviter les .exe Linux/Mac)
                return compatibleAssets.FirstOrDefault(a => a.Name.EndsWith(".exe")) 
                    ?? compatibleAssets.FirstOrDefault(a => a.Name.EndsWith(".zip"));
            }
            else
            {
                // Pour Linux/Mac, priorité aux .zip (éviter les .exe Windows)
                return compatibleAssets.FirstOrDefault(a => a.Name.EndsWith(".zip"));
            }
        }
        
        private IEnumerable<ReleaseAsset> FilterCompatibleAssets(IEnumerable<ReleaseAsset> assets, string currentPlatform)
        {
            var filteredAssets = new List<ReleaseAsset>();
            
            foreach (var asset in assets)
            {
                string assetName = asset.Name.ToLower();
                
                // Vérifier si l'asset est compatible avec la plateforme actuelle
                if (IsAssetCompatibleWithPlatform(assetName, currentPlatform))
                {
                    filteredAssets.Add(asset);
                }
            }
            
            return filteredAssets;
        }
        
        private bool IsAssetCompatibleWithPlatform(string assetName, string currentPlatform)
        {
            // Extensions supportées
            if (!assetName.EndsWith(".zip") && !assetName.EndsWith(".exe") && 
                !assetName.EndsWith(".dmg") && !assetName.EndsWith(".deb") && 
                !assetName.EndsWith(".rpm") && !assetName.EndsWith(".appimage"))
            {
                return false;
            }
            
            // Règles spécifiques par plateforme
            switch (currentPlatform)
            {
                case "windows":
                    // Windows accepte .exe et .zip, mais évite les fichiers spécifiquement Linux/Mac
                    if (assetName.Contains("linux") || assetName.Contains("mac") || assetName.Contains("osx") ||
                        assetName.EndsWith(".dmg") || assetName.EndsWith(".deb") || 
                        assetName.EndsWith(".rpm") || assetName.EndsWith(".appimage"))
                    {
                        return false;
                    }
                    return assetName.EndsWith(".exe") || assetName.EndsWith(".zip");
                    
                case "linux":
                    // Linux accepte .zip et formats Linux, mais évite .exe Windows et .dmg Mac
                    if (assetName.Contains("windows") || assetName.Contains("win") || 
                        assetName.Contains("mac") || assetName.Contains("osx") ||
                        assetName.EndsWith(".exe") || assetName.EndsWith(".dmg"))
                    {
                        return false;
                    }
                    return assetName.EndsWith(".zip") || assetName.EndsWith(".deb") || 
                           assetName.EndsWith(".rpm") || assetName.EndsWith(".appimage");
                    
                case "mac":
                    // Mac accepte .zip et .dmg, mais évite .exe Windows et formats Linux
                    if (assetName.Contains("windows") || assetName.Contains("win") || 
                        assetName.Contains("linux") || assetName.EndsWith(".exe") ||
                        assetName.EndsWith(".deb") || assetName.EndsWith(".rpm") || 
                        assetName.EndsWith(".appimage"))
                    {
                        return false;
                    }
                    return assetName.EndsWith(".zip") || assetName.EndsWith(".dmg");
                    
                default:
                    // Par défaut, accepter seulement les .zip universels
                    return assetName.EndsWith(".zip") && 
                           !assetName.Contains("windows") && !assetName.Contains("linux") && 
                           !assetName.Contains("mac") && !assetName.Contains("osx");
            }
        }

        public async Task DownloadAndInstallUpdateAsync(Release release)
        {
            if (_disposed)
            {
                NotifyUpdateStatus("Service de mise à jour non disponible");
                return;
            }

            string? tempFolder = null;
            
            try
            {
                NotifyUpdateStatus("Préparation du téléchargement...");
                
                // Sélectionner le meilleur asset selon la plateforme
                var asset = SelectBestAsset(release);
                          
                if (asset == null)
                {
                    NotifyUpdateStatus("Aucun fichier d'installation trouvé pour cette plateforme");
                    string platformName = GetPlatformAssetName();
                    
                    // Créer un message détaillé avec les assets disponibles
                    var availableAssets = string.Join(", ", release.Assets.Select(a => a.Name));
                    var detailedMessage = $"Aucun fichier d'installation compatible trouvé pour {platformName}.\n\n" +
                                         $"Fichiers disponibles dans cette version:\n{availableAssets}\n\n" +
                                         $"Le système évite automatiquement les fichiers incompatibles (ex: .exe Linux sur Windows).";
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(detailedMessage, "Aucun fichier compatible", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                // Créer un dossier temporaire unique pour le téléchargement
                tempFolder = Path.Combine(Path.GetTempPath(), $"ItunesRPC_Update_{DateTime.Now:yyyyMMdd_HHmmss}");
                
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                    Directory.CreateDirectory(tempFolder);
                }
                catch (Exception ex)
                {
                    NotifyUpdateStatus("Erreur lors de la création du dossier temporaire");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Impossible de créer le dossier temporaire: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                string downloadPath = Path.Combine(tempFolder, asset.Name);
                NotifyUpdateStatus($"Téléchargement de {asset.Name}...");

                // Télécharger le fichier avec gestion de progression
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(DownloadTimeoutMinutes));
                    var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;
                    var lastProgressUpdate = DateTime.Now;

                    using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new FileStream(downloadPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: BufferSize))
                    {
                        var buffer = new byte[BufferSize];
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token).ConfigureAwait(false);
                            downloadedBytes += bytesRead;
                            
                            // Mettre à jour le progrès selon l'intervalle défini
                            if (DateTime.Now - lastProgressUpdate > TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs))
                            {
                                if (totalBytes > 0)
                                {
                                    var percentage = (int)((downloadedBytes * 100) / totalBytes);
                                    var downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                                    var totalMB = totalBytes / (1024.0 * 1024.0);
                                    var speed = CalculateDownloadSpeed(downloadedBytes, DateTime.Now);
                                    NotifyUpdateStatus($"Téléchargement... {percentage}% ({downloadedMB:F1}/{totalMB:F1} MB) - {speed:F1} MB/s");
                                }
                                else
                                {
                                    var downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                                    NotifyUpdateStatus($"Téléchargement... {downloadedMB:F1} MB");
                                }
                                lastProgressUpdate = DateTime.Now;
                            }
                        }
                    }
                    
                    // Vérifier que le fichier a été téléchargé complètement
                    var fileInfo = new FileInfo(downloadPath);
                    if (totalBytes > 0 && fileInfo.Length != totalBytes)
                    {
                        throw new InvalidOperationException($"Téléchargement incomplet. Attendu: {totalBytes} bytes, Reçu: {fileInfo.Length} bytes");
                    }
                }
                catch (HttpRequestException ex)
                {
                    NotifyUpdateStatus("Erreur de téléchargement");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Erreur lors du téléchargement: {ex.Message}", "Erreur de connexion", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }
                catch (TaskCanceledException)
                {
                    NotifyUpdateStatus("Téléchargement annulé (timeout)");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Le téléchargement a pris trop de temps. Vérifiez votre connexion internet.", "Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                NotifyUpdateStatus("Téléchargement terminé");

                // Vérification du checksum si disponible
                NotifyUpdateStatus("Vérification de l'intégrité du fichier...");
                var expectedChecksum = await GetExpectedChecksumAsync(release, asset.Name).ConfigureAwait(false);
                
                if (!string.IsNullOrEmpty(expectedChecksum))
                {
                    var actualChecksum = await CalculateFileChecksumAsync(downloadPath).ConfigureAwait(false);
                    
                    if (string.IsNullOrEmpty(actualChecksum))
                    {
                        NotifyUpdateStatus("Erreur lors du calcul du checksum");
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Impossible de vérifier l'intégrité du fichier téléchargé.", "Erreur de vérification", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        // Continuer malgré l'erreur de checksum
                    }
                    else if (!actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        NotifyUpdateStatus("Checksum invalide - fichier corrompu");
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                $"Le fichier téléchargé est corrompu ou a été modifié.\n\nChecksum attendu: {expectedChecksum}\nChecksum calculé: {actualChecksum}\n\nLe téléchargement sera annulé pour votre sécurité.",
                                "Fichier corrompu",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                        return;
                    }
                    else
                    {
                        NotifyUpdateStatus("Vérification d'intégrité réussie");
                    }
                }
                else
                {
                    NotifyUpdateStatus("Aucun checksum disponible - vérification ignorée");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var result = MessageBox.Show(
                            "Aucun checksum n'est disponible pour vérifier l'intégrité du fichier téléchargé.\n\nVoulez-vous continuer l'installation malgré tout?",
                            "Vérification d'intégrité",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.No)
                        {
                            NotifyUpdateStatus("Installation annulée par l'utilisateur");
                            return;
                        }
                    });
                }

                // Traitement selon le type de fichier
                if (asset.Name.EndsWith(".zip"))
                {
                    await HandleZipUpdate(downloadPath, tempFolder).ConfigureAwait(false);
                }
                else if (asset.Name.EndsWith(".exe"))
                {
                    await HandleExeUpdate(downloadPath).ConfigureAwait(false);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                NotifyUpdateStatus("Erreur d'autorisation");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Accès refusé: {ex.Message}\n\nEssayez de relancer l'application en tant qu'administrateur.", "Erreur d'autorisation", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (IOException ex)
            {
                NotifyUpdateStatus("Erreur d'accès au fichier");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur d'accès au fichier: {ex.Message}", "Erreur de fichier", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus($"Erreur: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur lors de la mise à jour: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                // Erreur lors de la mise à jour
            }
            finally
            {
                // Nettoyer le dossier temporaire en cas d'erreur
                if (!string.IsNullOrEmpty(tempFolder) && Directory.Exists(tempFolder))
                {
                    try
                    {
                        Directory.Delete(tempFolder, true);
                    }
                    catch (Exception ex)
                    {
                        // Impossible de supprimer le dossier temporaire
                    }
                }
            }
        }

        private async Task HandleZipUpdate(string zipPath, string tempFolder)
        {
            try
            {
                NotifyUpdateStatus("Vérification de l'archive...");
                
                // Vérifier que le fichier ZIP existe et n'est pas vide
                var zipInfo = new FileInfo(zipPath);
                if (!zipInfo.Exists || zipInfo.Length == 0)
                {
                    throw new FileNotFoundException("Le fichier ZIP téléchargé est introuvable ou vide.");
                }
                
                NotifyUpdateStatus("Extraction de l'archive...");
                
                string extractFolder = Path.Combine(tempFolder, "extracted");
                Directory.CreateDirectory(extractFolder);
                
                // Extraire l'archive ZIP avec validation
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, extractFolder);
                }
                catch (InvalidDataException)
                {
                    throw new InvalidDataException("L'archive téléchargée est corrompue ou n'est pas un fichier ZIP valide.");
                }
                
                // Vérifier que l'extraction a produit des fichiers
                var extractedFiles = Directory.GetFiles(extractFolder, "*", SearchOption.AllDirectories);
                if (extractedFiles.Length == 0)
                {
                    throw new InvalidOperationException("L'archive extraite ne contient aucun fichier.");
                }
                
                NotifyUpdateStatus($"Archive extraite: {extractedFiles.Length} fichier(s) trouvé(s)");
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Sur Windows, chercher un fichier exécutable et les fichiers PDB
                    var exeFiles = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories);
                    var pdbFiles = Directory.GetFiles(extractFolder, "*.pdb", SearchOption.AllDirectories);
                    
                    if (exeFiles.Length == 0)
                    {
                        NotifyUpdateStatus("Aucun exécutable trouvé dans l'archive");
                        
                        // Proposer d'ouvrir le dossier pour installation manuelle
                        var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            return MessageBox.Show(
                                $"Aucun fichier exécutable trouvé dans l'archive.\n\nVoulez-vous ouvrir le dossier d'extraction pour une installation manuelle?\n\nChemin: {extractFolder}",
                                "Installation manuelle requise",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                        });
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            try
                            {
                                Process.Start("explorer.exe", extractFolder);
                            }
                            catch (Exception ex)
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    MessageBox.Show($"Impossible d'ouvrir le dossier: {ex.Message}\n\nChemin: {extractFolder}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                                });
                            }
                        }
                        return;
                    }
                    
                    // Prendre le premier exécutable trouvé (ou celui qui correspond au nom de l'app)
                    string exePath = exeFiles.FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("itunes")) ?? exeFiles[0];
                    NotifyUpdateStatus($"Exécutable trouvé: {Path.GetFileName(exePath)}");
                    
                    // Remplacer automatiquement l'exécutable et les fichiers PDB
                    await ReplaceExecutableAndPdbAsync(exePath, pdbFiles, extractFolder).ConfigureAwait(false);
                }
                else
                {
                    // Sur Linux/Mac, chercher l'exécutable principal
                    var executableFiles = Directory.GetFiles(extractFolder, "*", SearchOption.AllDirectories)
                        .Where(f => !Path.HasExtension(f) || 
                               Path.GetExtension(f).ToLower() == ".app" ||
                               Path.GetFileName(f).ToLower().Contains("itunes"))
                        .ToArray();
                    
                    if (executableFiles.Length > 0)
                    {
                        // Tenter le remplacement automatique sur Linux/Mac
                        string mainExecutable = executableFiles.FirstOrDefault(f => 
                            Path.GetFileName(f).ToLower().Contains("itunes")) ?? executableFiles[0];
                        
                        NotifyUpdateStatus($"Exécutable trouvé: {Path.GetFileName(mainExecutable)}");
                        await ReplaceExecutableUnixAsync(mainExecutable, extractFolder).ConfigureAwait(false);
                    }
                    else
                    {
                        // Fallback: proposer d'ouvrir le dossier d'extraction
                        NotifyUpdateStatus("Archive extraite avec succès");
                        
                        var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            return MessageBox.Show(
                                $"L'archive a été extraite dans:\n{extractFolder}\n\nAucun exécutable reconnu trouvé. Voulez-vous ouvrir ce dossier pour installer manuellement la mise à jour?",
                                "Extraction terminée",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);
                        });
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            try
                            {
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                {
                                    Process.Start("xdg-open", extractFolder);
                                }
                                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                {
                                    Process.Start("open", extractFolder);
                                }
                            }
                            catch (Exception ex)
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    MessageBox.Show($"Impossible d'ouvrir le dossier: {ex.Message}\n\nChemin: {extractFolder}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                                });
                            }
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                NotifyUpdateStatus("Archive corrompue");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"L'archive téléchargée est corrompue: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                NotifyUpdateStatus("Erreur d'autorisation lors de l'extraction");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Accès refusé lors de l'extraction: {ex.Message}\n\nEssayez de relancer l'application en tant qu'administrateur.", "Erreur d'autorisation", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus("Erreur lors de l'extraction");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur lors de l'extraction: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task ReplaceExecutableAndPdbAsync(string newExePath, string[] pdbFiles, string extractFolder)
        {
            try
            {
                NotifyUpdateStatus("Préparation du remplacement automatique...");
                
                // Obtenir le chemin de l'exécutable actuel
                string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string currentDirectory = Path.GetDirectoryName(currentExePath) ?? Environment.CurrentDirectory;
                string currentExeName = Path.GetFileName(currentExePath);
                
                // Demander confirmation à l'utilisateur
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show(
                        $"La mise à jour va remplacer automatiquement l'exécutable et les fichiers associés.\n\nFichiers à remplacer:\n- {currentExeName}\n- {pdbFiles.Length} fichier(s) PDB\n\nL'application va se fermer pour permettre la mise à jour.\n\nContinuer?",
                        "Remplacement automatique",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                });
                
                if (result != MessageBoxResult.Yes)
                {
                    NotifyUpdateStatus("Mise à jour annulée par l'utilisateur");
                    return;
                }
                
                // Créer un script batch pour effectuer le remplacement après fermeture de l'application
                string batchPath = Path.Combine(Path.GetTempPath(), "ItunesRPC_Update.bat");
                string backupFolder = Path.Combine(currentDirectory, "backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                
                var batchContent = new System.Text.StringBuilder();
                batchContent.AppendLine("@echo off");
                batchContent.AppendLine("echo Mise à jour d'ItunesRPC en cours...");
                batchContent.AppendLine("timeout /t 2 /nobreak > nul");
                batchContent.AppendLine();
                
                // Créer le dossier de sauvegarde
                batchContent.AppendLine($"mkdir \"{backupFolder}\"");
                
                // Sauvegarder l'ancien exécutable
                batchContent.AppendLine($"copy \"{currentExePath}\" \"{backupFolder}\"");
                
                // Sauvegarder les anciens fichiers PDB s'ils existent
                string currentPdbPath = Path.ChangeExtension(currentExePath, ".pdb");
                if (File.Exists(currentPdbPath))
                {
                    batchContent.AppendLine($"copy \"{currentPdbPath}\" \"{backupFolder}\"");
                }
                
                // Remplacer l'exécutable
                batchContent.AppendLine($"copy \"{newExePath}\" \"{currentExePath}\"");
                
                // Remplacer les fichiers PDB
                foreach (string pdbFile in pdbFiles)
                {
                    string pdbFileName = Path.GetFileName(pdbFile);
                    string targetPdbPath = Path.Combine(currentDirectory, pdbFileName);
                    batchContent.AppendLine($"copy \"{pdbFile}\" \"{targetPdbPath}\"");
                }
                
                batchContent.AppendLine();
                batchContent.AppendLine("echo Mise à jour terminée!");
                batchContent.AppendLine($"echo Sauvegarde créée dans: {backupFolder}");
                
                // Relancer l'application
                batchContent.AppendLine($"start \"\" \"{currentExePath}\"");
                
                // Nettoyer les fichiers temporaires
                batchContent.AppendLine($"timeout /t 2 /nobreak > nul");
                batchContent.AppendLine($"rmdir /s /q \"{extractFolder}\"");
                batchContent.AppendLine($"del \"{batchPath}\"");
                
                // Écrire le script batch
                await File.WriteAllTextAsync(batchPath, batchContent.ToString()).ConfigureAwait(false);
                
                NotifyUpdateStatus("Lancement du processus de mise à jour...");
                
                // Lancer le script batch
                var startInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                };
                
                Process.Start(startInfo);
                
                // Attendre un peu pour s'assurer que le processus démarre
                await Task.Delay(1000).ConfigureAwait(false);
                
                // Fermer l'application actuelle
                NotifyUpdateStatus("Fermeture de l'application pour mise à jour...");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus($"Erreur lors du remplacement: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur lors du remplacement automatique: {ex.Message}\n\nVeuillez effectuer la mise à jour manuellement.", "Erreur de mise à jour", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
         
         private async Task ReplaceExecutableUnixAsync(string newExecutablePath, string extractFolder)
         {
             try
             {
                 NotifyUpdateStatus("Préparation du remplacement automatique (Unix)...");
                 
                 // Obtenir le chemin de l'exécutable actuel
                 string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                 string currentDirectory = Path.GetDirectoryName(currentExePath) ?? Environment.CurrentDirectory;
                 string currentExeName = Path.GetFileName(currentExePath);
                 
                 // Demander confirmation à l'utilisateur
                 var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                 {
                     return MessageBox.Show(
                         $"La mise à jour va remplacer automatiquement l'exécutable.\n\nFichier à remplacer:\n- {currentExeName}\n\nL'application va se fermer pour permettre la mise à jour.\n\nContinuer?",
                         "Remplacement automatique",
                         MessageBoxButton.YesNo,
                         MessageBoxImage.Question);
                 });
                 
                 if (result != MessageBoxResult.Yes)
                 {
                     NotifyUpdateStatus("Mise à jour annulée par l'utilisateur");
                     return;
                 }
                 
                 // Créer un script shell pour effectuer le remplacement après fermeture de l'application
                 string scriptPath = Path.Combine(Path.GetTempPath(), "ItunesRPC_Update.sh");
                 string backupFolder = Path.Combine(currentDirectory, "backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                 
                 var scriptContent = new System.Text.StringBuilder();
                 scriptContent.AppendLine("#!/bin/bash");
                 scriptContent.AppendLine("echo \"Mise à jour d'ItunesRPC en cours...\"");
                 scriptContent.AppendLine("sleep 2");
                 scriptContent.AppendLine();
                 
                 // Créer le dossier de sauvegarde
                 scriptContent.AppendLine($"mkdir -p \"{backupFolder}\"");
                 
                 // Sauvegarder l'ancien exécutable
                 scriptContent.AppendLine($"cp \"{currentExePath}\" \"{backupFolder}/\"");
                 
                 // Remplacer l'exécutable
                 scriptContent.AppendLine($"cp \"{newExecutablePath}\" \"{currentExePath}\"");
                 
                 // Rendre l'exécutable exécutable
                 scriptContent.AppendLine($"chmod +x \"{currentExePath}\"");
                 
                 scriptContent.AppendLine();
                 scriptContent.AppendLine("echo \"Mise à jour terminée!\"");
                 scriptContent.AppendLine($"echo \"Sauvegarde créée dans: {backupFolder}\"");
                 
                 // Relancer l'application
                 scriptContent.AppendLine($"\"{currentExePath}\" &");
                 
                 // Nettoyer les fichiers temporaires
                 scriptContent.AppendLine("sleep 2");
                 scriptContent.AppendLine($"rm -rf \"{extractFolder}\"");
                 scriptContent.AppendLine($"rm \"{scriptPath}\"");
                 
                 // Écrire le script shell
                 await File.WriteAllTextAsync(scriptPath, scriptContent.ToString()).ConfigureAwait(false);
                 
                 // Rendre le script exécutable
                 try
                 {
                     var chmodProcess = new ProcessStartInfo
                     {
                         FileName = "chmod",
                         Arguments = $"+x \"{scriptPath}\"",
                         UseShellExecute = false,
                         CreateNoWindow = true
                     };
                     Process.Start(chmodProcess)?.WaitForExit();
                 }
                 catch (Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"Erreur lors du chmod: {ex.Message}");
                 }
                 
                 NotifyUpdateStatus("Lancement du processus de mise à jour...");
                 
                 // Lancer le script shell
                 var startInfo = new ProcessStartInfo
                 {
                     FileName = "/bin/bash",
                     Arguments = $"\"{scriptPath}\"",
                     UseShellExecute = false,
                     CreateNoWindow = true
                 };
                 
                 Process.Start(startInfo);
                 
                 // Attendre un peu pour s'assurer que le processus démarre
                 await Task.Delay(1000).ConfigureAwait(false);
                 
                 // Fermer l'application actuelle
                 NotifyUpdateStatus("Fermeture de l'application pour mise à jour...");
                 await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                 {
                     System.Windows.Application.Current.Shutdown();
                 });
             }
             catch (Exception ex)
             {
                 NotifyUpdateStatus($"Erreur lors du remplacement: {ex.Message}");
                 await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                 {
                     MessageBox.Show($"Erreur lors du remplacement automatique: {ex.Message}\n\nVeuillez effectuer la mise à jour manuellement.", "Erreur de mise à jour", MessageBoxButton.OK, MessageBoxImage.Error);
                 });
             }
         }
         
         private async Task HandleExeUpdate(string exePath)
        {
            try
            {
                NotifyUpdateStatus("Lancement de l'installation...");
                
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show(
                        "Le fichier de mise à jour est prêt à être installé.\n\nL'application va se fermer pour permettre la mise à jour.\n\nContinuer?",
                        "Installation de la mise à jour",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                });
                
                if (result == MessageBoxResult.Yes)
                {
                    // Lancer l'installateur
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas" // Demander les privilèges administrateur si nécessaire
                    };
                    
                    try
                    {
                        Process.Start(startInfo);
                        
                        // Attendre un peu pour s'assurer que le processus démarre
                        await Task.Delay(1000).ConfigureAwait(false);
                        
                        // Fermer l'application actuelle
                        NotifyUpdateStatus("Fermeture de l'application...");
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            System.Windows.Application.Current.Shutdown();
                        });
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        NotifyUpdateStatus("Installation annulée par l'utilisateur");
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("L'installation a été annulée. Vous pouvez relancer la mise à jour plus tard.", "Installation annulée", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                }
                else
                {
                    NotifyUpdateStatus("Installation annulée par l'utilisateur");
                }
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus("Erreur lors du lancement de l'installation");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Erreur lors du lancement de l'installation: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _httpClient?.Dispose();
                    _updateSemaphore?.Dispose();
                }
                catch (Exception ex)
                {
                    // Erreur lors de la libération des ressources
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}