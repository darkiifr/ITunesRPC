using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using ItunesRPC.Properties;

namespace ItunesRPC.Services
{
    public class UpdateService
    {
        private string _owner = "darkiiuseai";
        private string _repo = "ITunesRPC";
        private readonly Version _currentVersion;
        
        // URL de base pour les releases GitHub
        private string _githubReleaseUrl = "https://github.com/darkiiuseai/ITunesRPC/releases";

        public UpdateService()
        {
            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            
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

        // Événement pour notifier les changements de statut de mise à jour
        public event EventHandler<string> UpdateStatusChanged;
        
        // Méthode pour notifier les changements de statut
        private void NotifyUpdateStatus(string status)
        {
            UpdateStatusChanged?.Invoke(this, status);
        }
        
        public async Task CheckForUpdatesAsync(bool showNoUpdateMessage = false)
        {
            try
            {
                // Notifier le début de la vérification
                NotifyUpdateStatus("Vérification des mises à jour en cours...");
                
                // Afficher un message de progression uniquement si demandé explicitement
                if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        "Vérification des mises à jour en cours...",
                        "Mise à jour",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                
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
                
                NotifyUpdateStatus("Connexion à GitHub...");
                
                // Créer un client GitHub avec un timeout raisonnable
                var github = new GitHubClient(new ProductHeaderValue("ITunesRPC-UpdateCheck"));
                
                // Obtenir la dernière release avec gestion des erreurs réseau
                try {
                    NotifyUpdateStatus("Recherche de nouvelles versions...");
                    var releases = await github.Repository.Release.GetAll(_owner, _repo);
                    var latestRelease = releases.FirstOrDefault();
                    
                    if (latestRelease == null)
                    {
                        NotifyUpdateStatus("Aucune mise à jour disponible");
                        if (showNoUpdateMessage)
                        {
                            MessageBox.Show(
                                "Aucune mise à jour disponible.",
                                "Mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
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
                            MessageBox.Show(
                                "Impossible de déterminer la version de la dernière mise à jour.",
                                "Erreur de mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        return;
                    }
                    
                    // Comparer les versions
                    if (latestVersion > _currentVersion)
                    {
                        // Une mise à jour est disponible
                        NotifyUpdateStatus($"Mise à jour disponible: v{latestVersion}");
                        var result = MessageBox.Show(
                            $"Une nouvelle version est disponible: {latestVersion}\n\nVersion actuelle: {_currentVersion}\n\nVoulez-vous la télécharger maintenant?",
                            "Mise à jour disponible",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            NotifyUpdateStatus("Ouverture du navigateur pour téléchargement...");
                            // Ouvrir le navigateur vers la page de release
                            try {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = latestRelease.HtmlUrl,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex) {
                                NotifyUpdateStatus("Erreur lors de l'ouverture du navigateur");
                                MessageBox.Show(
                                    $"Impossible d'ouvrir le navigateur: {ex.Message}\n\nVeuillez visiter manuellement: {latestRelease.HtmlUrl}",
                                    "Erreur",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        // Aucune mise à jour disponible
                        NotifyUpdateStatus($"Vous utilisez la dernière version ({_currentVersion})");
                        if (showNoUpdateMessage)
                        {
                            MessageBox.Show(
                                $"Vous utilisez déjà la dernière version ({_currentVersion}).",
                                "Mise à jour",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
                catch (Octokit.RateLimitExceededException)
                {
                    NotifyUpdateStatus("Limite de requêtes GitHub dépassée. Réessayez plus tard.");
                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show(
                            "Limite de requêtes GitHub dépassée. Veuillez réessayer plus tard.",
                            "Erreur de mise à jour",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                catch (Octokit.NotFoundException)
                {
                    NotifyUpdateStatus("Dépôt GitHub non trouvé. Vérifiez les paramètres.");
                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show(
                            $"Le dépôt {_owner}/{_repo} n'a pas été trouvé. Vérifiez les paramètres de mise à jour.",
                            "Erreur de mise à jour",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyUpdateStatus($"Erreur: {ex.Message}");
                if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        $"Erreur lors de la vérification des mises à jour: {ex.Message}",
                        "Erreur de mise à jour",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                Console.WriteLine($"Erreur lors de la vérification des mises à jour: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallUpdateAsync(Release release)
        {
            try
            {
                // Trouver l'asset à télécharger (généralement un .exe ou .zip)
                var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe") || a.Name.EndsWith(".zip"));
                if (asset == null)
                {
                    MessageBox.Show("Aucun fichier d'installation trouvé dans cette version.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Créer un dossier temporaire pour le téléchargement
                string tempFolder = Path.Combine(Path.GetTempPath(), "ItunesRPC_Update");
                Directory.CreateDirectory(tempFolder);
                string downloadPath = Path.Combine(tempFolder, asset.Name);

                // Télécharger le fichier
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5); // Augmenter le timeout pour les téléchargements volumineux
                    var response = await client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(downloadPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192]; // Buffer de 8KB
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                }

                // Exécuter le fichier téléchargé
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true,
                    Verb = "runas" // Demander les privilèges administrateur si nécessaire
                });

                // Fermer l'application actuelle pour permettre la mise à jour
                System.Windows.Application.Current.Shutdown();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Erreur réseau lors du téléchargement de la mise à jour: {ex.Message}");
                MessageBox.Show($"Erreur réseau lors du téléchargement de la mise à jour: {ex.Message}", "Erreur de connexion", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Erreur d'accès au fichier lors de la mise à jour: {ex.Message}");
                MessageBox.Show($"Erreur d'accès au fichier lors de la mise à jour: {ex.Message}", "Erreur de fichier", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du téléchargement de la mise à jour: {ex.Message}");
                MessageBox.Show($"Erreur lors du téléchargement de la mise à jour: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}