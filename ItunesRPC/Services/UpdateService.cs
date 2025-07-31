using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using ItunesRPC.Properties;

namespace ItunesRPC.Services
{
    public class UpdateService : IDisposable
    {
        private string _owner = "darkiiuseai";
        private string _repo = "ITunesRPC";
        private readonly Version _currentVersion;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _updateSemaphore;
        private bool _disposed = false;
        
        // URL de base pour les releases GitHub
        private string _githubReleaseUrl = "https://github.com/darkiiuseai/ITunesRPC/releases";
        
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
                Console.WriteLine($"Erreur lors du chargement des paramètres de mise à jour: {ex.Message}");
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
                Console.WriteLine($"[UpdateService] {DateTime.Now:HH:mm:ss} - {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la notification de statut: {ex.Message}");
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
                Console.WriteLine($"Erreur lors de la vérification des mises à jour: {ex.Message}");
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
            
            // Chercher d'abord un asset spécifique à la plateforme
            var platformAsset = release.Assets.FirstOrDefault(a => 
                a.Name.ToLower().Contains(platformName) && 
                (a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe")));
            
            if (platformAsset != null)
                return platformAsset;
            
            // Si pas d'asset spécifique, chercher par extension selon la plateforme
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Pour Windows, priorité aux .exe puis .zip
                return release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe")) 
                    ?? release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip"));
            }
            else
            {
                // Pour Linux/Mac, priorité aux .zip
                return release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip"));
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
                    
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Aucun fichier d'installation trouvé pour {platformName} dans cette version.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                Console.WriteLine($"Erreur lors de la mise à jour: {ex}");
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
                        Console.WriteLine($"Impossible de supprimer le dossier temporaire: {ex.Message}");
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
                    // Sur Windows, chercher un fichier exécutable
                    var exeFiles = Directory.GetFiles(extractFolder, "*.exe", SearchOption.AllDirectories);
                    
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
                    await HandleExeUpdate(exePath).ConfigureAwait(false);
                }
                else
                {
                    // Sur Linux/Mac, proposer d'ouvrir le dossier d'extraction
                    NotifyUpdateStatus("Archive extraite avec succès");
                    
                    var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        return MessageBox.Show(
                            $"L'archive a été extraite dans:\n{extractFolder}\n\nVoulez-vous ouvrir ce dossier pour installer manuellement la mise à jour?",
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
                    Console.WriteLine($"Erreur lors de la libération des ressources: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}