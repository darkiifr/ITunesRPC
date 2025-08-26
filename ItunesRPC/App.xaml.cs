using Hardcodet.Wpf.TaskbarNotification;
using ItunesRPC.Services;
using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using ItunesRPC.Properties;

namespace ItunesRPC
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private MusicDetectionService? _musicDetectionService;
        private DiscordRpcService? _discordService;
        private UpdateService? _updateService;
        private bool _isExiting = false;



        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            
            try
            {
                // Initialiser le service de logging en premier
                LoggingService.Instance.LogInfo("Démarrage de l'application...", "App");
                
                // Initialiser le service de mise à jour avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Initialisation du service de mise à jour...", "App");
                    _updateService = new UpdateService();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'initialisation du service de mise à jour: {ex.Message}", "App", ex);
                    // Continuer sans le service de mise à jour
                }

                // Initialiser les services dans le bon ordre avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Initialisation du service Discord RPC...", "App");
                    _discordService = new DiscordRpcService();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'initialisation du service Discord: {ex.Message}", "App", ex);
                    // Continuer sans Discord RPC
                }

                ItunesService? itunesService = null;
                AppleMusicService? appleMusicService = null;
                
                try
                {
                    LoggingService.Instance.LogInfo("Initialisation du service iTunes...", "App");
                    itunesService = new ItunesService();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'initialisation du service iTunes: {ex.Message}", "App", ex);
                }

                try
                {
                    LoggingService.Instance.LogInfo("Initialisation du service Apple Music...", "App");
                    appleMusicService = new AppleMusicService();
                    await appleMusicService.InitializeAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'initialisation du service Apple Music: {ex.Message}", "App", ex);
                }

                // Vérifier que les services essentiels sont initialisés
                if (itunesService == null || appleMusicService == null)
                {
                    MessageBox.Show("Erreur critique: Impossible d'initialiser les services de musique.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                try
                {
                    LoggingService.Instance.LogInfo("Initialisation du service de détection de musique...", "App");
                    _musicDetectionService = new MusicDetectionService(itunesService, appleMusicService, _discordService!);
                    await _musicDetectionService.InitializeAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'initialisation du service de détection: {ex.Message}", "App", ex);
                    MessageBox.Show($"Erreur critique lors de l'initialisation: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // Créer l'icône de notification avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Création de l'icône de notification...", "App");
                    _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.ToolTipText = "iTunes RPC - En cours d'exécution";
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de la création de l'icône de notification: {ex.Message}", "App", ex);
                    // Continuer sans icône de notification
                }

                // Créer et afficher la fenêtre principale avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Création de la fenêtre principale...", "App");
                    _mainWindow = new MainWindow(_musicDetectionService, _discordService!, _updateService!);
                    if (_mainWindow != null)
                    {
                        _mainWindow.Closing += MainWindow_Closing;
                        _mainWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de la création de la fenêtre principale: {ex.Message}", "App", ex);
                    MessageBox.Show($"Erreur critique lors de la création de l'interface: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // Démarrer le service de détection de musique avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Démarrage du service de détection de musique...", "App");
                    _musicDetectionService?.Start();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors du démarrage du service de détection: {ex.Message}", "App", ex);
                    // Continuer même si le service ne démarre pas
                }

                // Configurer le démarrage automatique si nécessaire avec gestion d'erreur
                try
                {
                    LoggingService.Instance.LogInfo("Configuration du démarrage automatique...", "App");
                    ConfigureAutoStart();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de la configuration du démarrage automatique: {ex.Message}", "App", ex);
                    // Continuer même si la configuration échoue
                }
                
                LoggingService.Instance.LogInfo("Application démarrée avec succès", "App");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur critique lors du démarrage de l'application: {ex.Message}", "App", ex);
                MessageBox.Show($"Erreur critique lors du démarrage: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Erreur critique", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                // Vérifier si l'option de minimisation dans la zone de notification est activée
                if (Settings.Default.MinimizeToTray)
                {
                    e.Cancel = true;
                    _mainWindow?.Hide();
                    _notifyIcon?.ShowBalloonTip("iTunes RPC", "L'application continue de fonctionner en arrière-plan.", BalloonIcon.Info);
                }
                // Sinon, laisser la fenêtre se fermer normalement
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _isExiting = true;
            _musicDetectionService?.Stop();
            _discordService?.Shutdown();
            _updateService?.Dispose();
            _notifyIcon?.Dispose();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        public void ExitApplication()
        {
            _isExiting = true;
            if (_mainWindow != null)
            {
                _mainWindow.Close();
            }
            Shutdown();
        }
        
        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }
        
        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        public void ConfigureAutoStart()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (key != null)
                    {
                        string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        appPath = appPath.Replace(".dll", ".exe");

                        // Vérifier si l'application est configurée pour démarrer automatiquement
                        bool isAutoStartEnabled = Settings.Default.AutoStartEnabled;

                        if (isAutoStartEnabled)
                        {
                            key.SetValue("ItunesRPC", appPath);
                        }
                        else
                        {
                            if (key.GetValue("ItunesRPC") != null)
                            {
                                key.DeleteValue("ItunesRPC", false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la configuration du démarrage automatique : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}