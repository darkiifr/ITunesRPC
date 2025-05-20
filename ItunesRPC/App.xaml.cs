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

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Vérifier les mises à jour au démarrage
            _updateService = new UpdateService();
            _ = _updateService.CheckForUpdatesAsync();

            // Initialiser les services
            _discordService = new DiscordRpcService();
            _musicDetectionService = new MusicDetectionService(_discordService);

            // Créer l'icône de notification
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            _notifyIcon.ToolTipText = "iTunes RPC - En cours d'exécution";

            // Créer et afficher la fenêtre principale
            _mainWindow = new MainWindow(_musicDetectionService, _discordService, _updateService);
            _mainWindow.Closing += MainWindow_Closing!;
            _mainWindow.Show();

            // Démarrer le service de détection de musique
            _musicDetectionService.Start();

            // Configurer le démarrage automatique si nécessaire
            ConfigureAutoStart();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExiting)
            {
                // Vérifier si l'option de minimisation dans la zone de notification est activée
                if (Settings.Default.MinimizeToTray)
                {
                    e.Cancel = true;
                    _mainWindow!.Hide();
                    _notifyIcon!.ShowBalloonTip("iTunes RPC", "L'application continue de fonctionner en arrière-plan.", BalloonIcon.Info);
                }
                // Sinon, laisser la fenêtre se fermer normalement
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _isExiting = true;
            _musicDetectionService?.Stop();
            _discordService?.Shutdown();
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