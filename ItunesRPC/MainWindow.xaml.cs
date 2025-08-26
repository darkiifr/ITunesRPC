using ItunesRPC.Models;
using ItunesRPC.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Input;

namespace ItunesRPC
{
    public partial class MainWindow : Window
    {
        private readonly MusicDetectionService _musicService;
        private readonly DiscordRpcService _discordService;
        private readonly UpdateService _updateService;
        private System.Timers.Timer _progressTimer;
        private DebugConsoleWindow _debugConsole;

        public MainWindow(MusicDetectionService musicService, DiscordRpcService discordService, UpdateService updateService)
        {
            InitializeComponent();
            
            _musicService = musicService ?? throw new ArgumentNullException(nameof(musicService));
            _discordService = discordService ?? throw new ArgumentNullException(nameof(discordService));
            _updateService = updateService; // Peut être null

            try
            {
                // S'abonner aux événements
                _musicService.TrackChanged += MusicService_TrackChanged;
                _musicService.PlayStateChanged += MusicService_PlayStateChanged;
                _musicService.ServiceStatusChanged += MusicService_ServiceStatusChanged;
                _discordService.ConnectionStatusChanged += DiscordService_ConnectionStatusChanged;

                // Configurer le timer pour mettre à jour la progression
                _progressTimer = new System.Timers.Timer(500);
                _progressTimer.Elapsed += (s, e) => Dispatcher.Invoke(UpdateTrackProgress);
                _progressTimer.Start();
                
                // S'abonner à l'événement de redimensionnement
                SizeChanged += MainWindow_SizeChanged;

                // Charger les paramètres
                LoadSettings();
                
                // Initialiser le service de logging
                LoggingService.Instance.LogInfo("Fenêtre principale initialisée", "MainWindow");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Erreur lors de l'initialisation de MainWindow: {ex.Message}");
                LoggingService.Instance.LogError($"Erreur lors de l'initialisation de MainWindow: {ex.Message}", "MainWindow", ex);
                throw;
            }
        }
        
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Ajuster l'interface en fonction de la taille de la fenêtre
            if (e.NewSize.Width < 950)
            {
                // Réduire la taille des éléments pour les petites fenêtres
                TrackNameText.FontSize = 14;
                ArtistText.FontSize = 14;
            }
            else
            {
                // Taille normale pour les fenêtres plus grandes
                TrackNameText.FontSize = 16;
                ArtistText.FontSize = 16;
            }
            
            // Ajuster la hauteur des éléments en fonction de la hauteur de la fenêtre
            if (e.NewSize.Height < 620)
            {
                // Réduire les marges pour les fenêtres basses
                TrackProgressBar.Margin = new Thickness(0, 5, 0, 0);
            }
            else
            {
                // Marges normales pour les fenêtres plus hautes
                TrackProgressBar.Margin = new Thickness(0, 10, 0, 0);
            }
        }

        private void LoadSettings()
        {
            try
            {
                // Charger les paramètres de l'interface avec vérifications de sécurité
                if (AutoStartCheckBox != null)
                    AutoStartCheckBox.IsChecked = Properties.Settings.Default.AutoStartEnabled;
                
                if (MinimizeToTrayCheckBox != null)
                    MinimizeToTrayCheckBox.IsChecked = Properties.Settings.Default.MinimizeToTray;
                
                if (ShowNotificationsCheckBox != null)
                    ShowNotificationsCheckBox.IsChecked = Properties.Settings.Default.ShowNotifications;
                
                if (CheckUpdateOnStartupCheckBox != null)
                    CheckUpdateOnStartupCheckBox.IsChecked = Properties.Settings.Default.CheckUpdateOnStartup;
                
                // Charger et appliquer le thème sauvegardé
                ThemeManager.LoadSavedTheme();
                
                // Enregistrer cette fenêtre pour la propagation automatique des thèmes
                ThemeManager.RegisterWindow(this);
                
                // Appliquer le fond personnalisé
                ApplyCustomBackground();
                
                // Initialiser les statuts
                InitializeStatuses();
                
                // S'abonner aux événements de mise à jour si le service est disponible
                if (_updateService != null)
                {
                    _updateService.UpdateStatusChanged += UpdateService_StatusChanged;
                    
                    // Vérifier les mises à jour au démarrage (silencieusement) si l'option est activée
                    if (Properties.Settings.Default.CheckUpdateOnStartup)
                    {
                        _ = _updateService.CheckForUpdatesAsync(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors du chargement des paramètres: {ex.Message}", "MainWindow", ex);
                // Utiliser des valeurs par défaut en cas d'erreur avec vérifications de sécurité
                if (AutoStartCheckBox != null)
                    AutoStartCheckBox.IsChecked = false;
                
                if (MinimizeToTrayCheckBox != null)
                    MinimizeToTrayCheckBox.IsChecked = true;
                
                if (ShowNotificationsCheckBox != null)
                    ShowNotificationsCheckBox.IsChecked = true;
                
                if (CheckUpdateOnStartupCheckBox != null)
                    CheckUpdateOnStartupCheckBox.IsChecked = true;
                
                // Appliquer le thème par défaut
                try
                {
                    ThemeManager.ChangeTheme(ThemeManager.AppTheme.Dark);
                    // S'assurer que le thème est appliqué à cette fenêtre
                    ThemeManager.ApplyCurrentThemeToWindow(this);
                }
                catch (Exception themeEx)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'application du thème par défaut: {themeEx.Message}", "MainWindow", themeEx);
                }
                
                // Utiliser le fond par défaut
                try
                {
                    if (MainGrid != null && Application.Current?.Resources != null)
                    {
                        var backgroundBrush = Application.Current.Resources["AppBackgroundBrush"] as SolidColorBrush;
                        if (backgroundBrush != null)
                        {
                            MainGrid.Background = backgroundBrush;
                        }
                    }
                }
                catch (Exception bgEx)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'application du fond par défaut: {bgEx.Message}", "MainWindow", bgEx);
                }
            }
        }
        
        private void InitializeStatuses()
        {
            try
            {
                // Initialiser les statuts des services avec vérifications de sécurité
                if (iTunesStatusText != null)
                    iTunesStatusText.Text = "En attente...";
                
                if (DiscordStatusText != null)
                    DiscordStatusText.Text = "En attente...";
                
                if (StatusText != null)
                    StatusText.Text = "En attente de musique...";
                
                if (AppStatusText != null)
                    AppStatusText.Text = "Application prête";
                
                // Charger l'image par défaut pour l'album
                LoadDefaultAlbumArt();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'initialisation des statuts: {ex.Message}", "MainWindow", ex);
            }
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Appliquer les ajustements de taille initiaux en utilisant la taille actuelle de la fenêtre
            double width = this.ActualWidth;
            double height = this.ActualHeight;
            
            // Simuler un événement de redimensionnement en ajustant manuellement les éléments
            if (width < 950)
            {
                TrackNameText.FontSize = 14;
                ArtistText.FontSize = 14;
            }
            else
            {
                TrackNameText.FontSize = 16;
                ArtistText.FontSize = 16;
            }
            
            if (height < 620)
            {
                TrackProgressBar.Margin = new Thickness(0, 5, 0, 0);
            }
            else
            {
                TrackProgressBar.Margin = new Thickness(0, 10, 0, 0);
            }
            
            // Mettre à jour le statut de l'application
            AppStatusText.Text = "Application démarrée - En attente de musique";
            
            // Vérifier si iTunes/Apple Music est en cours d'exécution
            try
            {
                Process[] processes = Process.GetProcessesByName("iTunes");
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName("Music"); // Apple Music sur macOS
                }
                
                if (processes.Length == 0)
                {
                    iTunesStatusText.Text = "Non détecté";
                    AppStatusText.Text = "iTunes/Apple Music n'est pas en cours d'exécution";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la vérification d'iTunes: {ex.Message}", "MainWindow", ex);
            }
        }
        
        private void UpdateService_StatusChanged(object? sender, string status)
        {
            try
            {
                // Mettre à jour le statut de l'application depuis n'importe quel thread
                Dispatcher.Invoke(() => {
                    if (AppStatusText != null && !string.IsNullOrEmpty(status))
                    {
                        AppStatusText.Text = status;
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la mise à jour du statut: {ex.Message}", "MainWindow", ex);
            }
        }

        private void MusicService_TrackChanged(object? sender, TrackInfoEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Vérifier si TrackInfo est null (service arrêté)
                if (e.TrackInfo == null)
                {
                    // Réinitialiser l'interface utilisateur
                    TrackNameText.Text = "Aucune piste";
                    ArtistText.Text = "Aucun artiste";
                    AlbumText.Text = "Aucun album";
                    PositionText.Text = "";
                    StatusText.Text = "En attente de musique...";
                    SourceText.Text = e.Source ?? "Aucun";
                    AppStatusText.Text = $"{e.Source} arrêté";
                    
                    // Réinitialiser la barre de progression
                    TrackProgressBar.Value = 0;
                    ElapsedTimeText.Text = "00:00";
                    RemainingTimeText.Text = "00:00";
                    
                    // Masquer l'indicateur de pause
                    PlayingIndicator.Visibility = Visibility.Collapsed;
                    
                    // Charger l'image par défaut
                    LoadDefaultAlbumArt();
                    
                    return;
                }

                // Mettre à jour l'interface utilisateur avec les informations de la piste
                TrackNameText.Text = e.TrackInfo.Name;
                ArtistText.Text = e.TrackInfo.Artist;
                AlbumText.Text = e.TrackInfo.Album;
                PositionText.Text = $"{e.TrackInfo.TrackNumber} sur {e.TrackInfo.TrackCount}";
                StatusText.Text = "En cours de lecture";
                
                // Mettre à jour la source de musique (iTunes ou Apple Music)
                SourceText.Text = e.Source ?? "Musique";
                
                // Mettre à jour le statut d'iTunes
                iTunesStatusText.Text = "Connecté";
                
                // Mettre à jour le statut de l'application
                AppStatusText.Text = $"Lecture de {e.TrackInfo.Name} par {e.TrackInfo.Artist}";
                
                // Réinitialiser la barre de progression
                TrackProgressBar.Value = 0;
                
                // Masquer l'indicateur de pause
                PlayingIndicator.Visibility = Visibility.Collapsed;

                // Mettre à jour l'image de l'album si disponible
                if (!string.IsNullOrEmpty(e.TrackInfo.ArtworkPath) && File.Exists(e.TrackInfo.ArtworkPath))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bitmap.UriSource = new Uri(e.TrackInfo.ArtworkPath);
                        bitmap.EndInit();
                        bitmap.Freeze(); // Optimisation pour éviter les fuites de mémoire
                        AlbumArt.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        // En cas d'erreur, utiliser l'image par défaut
                        LoadDefaultAlbumArt();
                        LoggingService.Instance.LogError($"Erreur lors du chargement de l'image: {ex.Message}", "MainWindow", ex);
                    }
                }
                else
                {
                    // Utiliser l'image par défaut si aucune pochette n'est disponible
                    LoadDefaultAlbumArt();
                }
                
                // Afficher une notification si l'option est activée
                if (Properties.Settings.Default.ShowNotifications)
                {
                    try
                    {
                        var notifyIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)Application.Current.FindResource("NotifyIcon");
                        if (notifyIcon != null)
                        {
                            notifyIcon.ShowBalloonTip(
                            "iTunes RPC - Nouvelle piste",
                            $"{e.TrackInfo.Name}\npar {e.TrackInfo.Artist}",
                            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError($"Erreur lors de l'affichage de la notification: {ex.Message}", "MainWindow", ex);
                    }
                }
            });
        }

        private void MusicService_PlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsPlaying)
                {
                    StatusText.Text = "En cours de lecture";
                    PlayingIndicator.Visibility = Visibility.Collapsed;
                    iTunesStatusText.Text = "Connecté";
                    
                    // Mettre à jour la source si disponible
                    if (!string.IsNullOrEmpty(e.Source))
                    {
                        SourceText.Text = e.Source;
                    }
                }
                else
                {
                    StatusText.Text = "Lecture en pause";
                    PlayingIndicator.Visibility = Visibility.Visible;
                    iTunesStatusText.Text = "Connecté (en pause)";
                }
            });
        }
        
        private void DiscordService_ConnectionStatusChanged(object? sender, DiscordConnectionStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                DiscordStatusText.Text = e.IsConnected ? "Connecté" : "Déconnecté";
                DiscordStatusBarText.Text = e.IsConnected ? "Connecté" : "Déconnecté";
                ReconnectButton.IsEnabled = !e.IsConnected;
            });
        }

        private void MusicService_ServiceStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Mettre à jour le statut iTunes/Apple Music
                if (e.AppleMusicActive)
                {
                    iTunesStatusText.Text = "Apple Music détecté";
                    AppStatusText.Text = "Apple Music actif";
                }
                else if (e.ItunesActive)
                {
                    iTunesStatusText.Text = "iTunes détecté";
                    AppStatusText.Text = "iTunes actif";
                }
                else
                {
                    iTunesStatusText.Text = "Non détecté";
                    AppStatusText.Text = "Aucune application de musique détectée";
                }

                // Mettre à jour le statut général
                StatusText.Text = e.ActiveService != "Aucun" 
                    ? $"Service actif: {e.ActiveService}" 
                    : "En attente de musique...";
            });
        }
        
        private void UpdateTrackProgress()
        {
            try
            {
                // Cette méthode est appelée par le timer pour mettre à jour la barre de progression
                var currentTrack = _discordService?.CurrentTrack;
                if (currentTrack != null && currentTrack.IsPlaying)
                {
                    // Vérifier que les éléments UI existent avant de les utiliser
                    if (TrackProgressBar != null)
                    {
                        TrackProgressBar.Value = currentTrack.ProgressPercentage;
                    }
                    
                    // Calculer le temps écoulé et la durée totale
                    TimeSpan elapsed = DateTime.Now - currentTrack.StartTime;
                    TimeSpan totalDuration = currentTrack.Duration;
                    
                    // S'assurer que le temps écoulé ne dépasse pas la durée totale
                    if (elapsed > totalDuration)
                        elapsed = totalDuration;
                    
                    // Mettre à jour les affichages de temps avec vérification de nullité
                    if (ElapsedTimeText != null)
                    {
                        ElapsedTimeText.Text = string.Format("{0:mm\\:ss}", elapsed);
                    }
                    
                    if (RemainingTimeText != null)
                    {
                        RemainingTimeText.Text = string.Format("{0:mm\\:ss}", totalDuration);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la mise à jour de la progression: {ex.Message}", "MainWindow", ex);
            }
        }
        
        private void ReconnectDiscord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _discordService?.Reconnect();
                
                // Mettre à jour le statut
                if (AppStatusText != null)
                {
                    AppStatusText.Text = "Tentative de reconnexion à Discord...";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la reconnexion Discord: {ex.Message}", "MainWindow", ex);
                if (AppStatusText != null)
                {
                    AppStatusText.Text = $"Erreur de reconnexion Discord: {ex.Message}";
                }
            }
        }
        
        private void Diagnostic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_discordService == null)
                {
                    MessageBox.Show("Service Discord non initialisé.", "Diagnostic Discord", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var diagnosticInfo = _discordService.GetDiagnosticInfo();
                
                // Créer une fenêtre de diagnostic
                Window diagnosticWindow = new Window
                {
                    Title = "Diagnostic Discord RPC",
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Style = (Style)FindResource("ModernWindow"),
                    ResizeMode = ResizeMode.CanResize
                };
                
                // Créer le contenu de la fenêtre
                Grid mainGrid = new Grid { Margin = new Thickness(15) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // Titre
                TextBlock titleBlock = new TextBlock
                {
                    Text = "Informations de diagnostic Discord RPC",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(titleBlock, 0);
                mainGrid.Children.Add(titleBlock);
                
                // Zone de texte avec les informations
                TextBox diagnosticTextBox = new TextBox
                {
                    Text = diagnosticInfo,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 12,
                    Padding = new Thickness(10),
                    Background = (Brush)FindResource("SecondaryBrush"),
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1)
                };
                Grid.SetRow(diagnosticTextBox, 1);
                mainGrid.Children.Add(diagnosticTextBox);
                
                // Boutons
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 15, 0, 0)
                };
                
                Button refreshButton = new Button
                {
                    Content = "Actualiser",
                    Style = (Style)FindResource("ModernButton"),
                    Margin = new Thickness(0, 0, 10, 0),
                    Padding = new Thickness(15, 8, 15, 8)
                };
                refreshButton.Click += (s, args) => {
                    diagnosticTextBox.Text = _discordService.GetDiagnosticInfo();
                };
                
                Button closeButton = new Button
                {
                    Content = "Fermer",
                    Style = (Style)FindResource("ModernButton"),
                    Padding = new Thickness(15, 8, 15, 8)
                };
                closeButton.Click += (s, args) => diagnosticWindow.Close();
                
                buttonPanel.Children.Add(refreshButton);
                buttonPanel.Children.Add(closeButton);
                Grid.SetRow(buttonPanel, 2);
                mainGrid.Children.Add(buttonPanel);
                
                diagnosticWindow.Content = mainGrid;
                diagnosticWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'affichage du diagnostic: {ex.Message}", "MainWindow", ex);
                MessageBox.Show($"Erreur lors de l'affichage du diagnostic: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RefreshConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Actualiser les connexions iTunes/Apple Music et Discord
                _musicService?.Restart();
                _discordService?.Reconnect();
                
                // Afficher un message de confirmation
                try
                {
                    var notifyIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon?)Application.Current?.FindResource("NotifyIcon");
                    if (notifyIcon != null)
                    {
                        notifyIcon.ShowBalloonTip(
                        "iTunes RPC",
                        "Connexions actualisées",
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de l'affichage de la notification: {ex.Message}", "MainWindow", ex);
                }
                
                // Mettre à jour le statut de l'application
                if (AppStatusText != null)
                {
                    AppStatusText.Text = "Connexions actualisées";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'actualisation des connexions: {ex.Message}", "MainWindow", ex);
                if (AppStatusText != null)
                {
                    AppStatusText.Text = $"Erreur lors de l'actualisation: {ex.Message}";
                }
            }
        }
        
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Créer une fenêtre de paramètres simple
            Window settingsWindow = new Window
            {
                Title = "Paramètres",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Style = (Style)FindResource("ModernWindow"),
                ResizeMode = ResizeMode.NoResize
            };
            
            // Enregistrer la fenêtre pour la propagation automatique des thèmes
            ThemeManager.RegisterWindow(settingsWindow);
            
            // Créer le contenu de la fenêtre
            StackPanel panel = new StackPanel { Margin = new Thickness(15) };
            
            // Titre
            TextBlock titleBlock = new TextBlock
            {
                Text = "Paramètres",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(titleBlock);
            
            // Ajouter les options existantes
            panel.Children.Add(new CheckBox
            {
                Content = "Démarrer avec Windows",
                IsChecked = AutoStartCheckBox.IsChecked,
                Style = (Style)FindResource("ModernCheckBox"),
                Margin = new Thickness(0, 5, 0, 5)
            });
            
            panel.Children.Add(new CheckBox
            {
                Content = "Minimiser dans la zone de notification",
                IsChecked = MinimizeToTrayCheckBox.IsChecked,
                Style = (Style)FindResource("ModernCheckBox"),
                Margin = new Thickness(0, 5, 0, 5)
            });
            
            panel.Children.Add(new CheckBox
            {
                Content = "Afficher les notifications lors des changements de piste",
                IsChecked = ShowNotificationsCheckBox.IsChecked,
                Style = (Style)FindResource("ModernCheckBox"),
                Margin = new Thickness(0, 5, 0, 15)
            });
            
            // Bouton de fermeture
            Button closeButton = new Button
            {
                Content = "Fermer",
                Style = (Style)FindResource("ModernButton"),
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            closeButton.Click += (s, args) => settingsWindow.Close();
            panel.Children.Add(closeButton);
            
            settingsWindow.Content = panel;
            settingsWindow.ShowDialog();
        }
        
        private void Theme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Créer un menu contextuel avec les options
                var contextMenu = new ContextMenu();
                
                // Option pour changer le thème
                var themeMenuItem = new MenuItem()
                {
                    Header = "Changer de thème",
                    Icon = new TextBlock { Text = "🎨", FontSize = 14 }
                };
                themeMenuItem.Click += (s, args) =>
                {
                    var themeSelector = new ThemeSelector
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    
                    themeSelector.ShowDialog();
                    
                    // Mettre à jour le statut
                    AppStatusText.Text = $"Thème {ThemeManager.CurrentTheme} appliqué";
                    
                    // Appliquer le fond personnalisé si activé
                    ApplyCustomBackground();
                };
                
                // Option pour ouvrir la console de débogage
                var debugMenuItem = new MenuItem()
                {
                    Header = "Console de débogage",
                    Icon = new TextBlock { Text = "🔧", FontSize = 14 }
                };
                debugMenuItem.Click += (s, args) => OpenDebugConsole();
                
                // Ajouter un séparateur
                var separator = new Separator();
                
                // Option pour les paramètres avancés
                var settingsMenuItem = new MenuItem()
                {
                    Header = "Paramètres avancés",
                    Icon = new TextBlock { Text = "⚙️", FontSize = 14 }
                };
                settingsMenuItem.Click += (s, args) => Settings_Click(s, args);
                
                contextMenu.Items.Add(themeMenuItem);
                contextMenu.Items.Add(debugMenuItem);
                contextMenu.Items.Add(separator);
                contextMenu.Items.Add(settingsMenuItem);
                
                // Afficher le menu contextuel
                if (sender is Button button)
                {
                    contextMenu.PlacementTarget = button;
                    contextMenu.Placement = PlacementMode.Bottom;
                    contextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                // En cas d'erreur, ouvrir directement le sélecteur de thème
                LoggingService.Instance.LogError($"Erreur lors de l'ouverture du menu de personnalisation: {ex.Message}", "MainWindow", ex);
                
                var themeSelector = new ThemeSelector
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                themeSelector.ShowDialog();
                
                // Mettre à jour le statut
                AppStatusText.Text = $"Thème {ThemeManager.CurrentTheme} appliqué";
                
                // Appliquer le fond personnalisé si activé
                ApplyCustomBackground();
            }
         }
         
        private void OpenDebugConsole()
        {
            try
            {
                // Si la console existe déjà, la mettre au premier plan
                if (_debugConsole != null && _debugConsole.IsLoaded)
                {
                    _debugConsole.Activate();
                    _debugConsole.WindowState = WindowState.Normal;
                    LoggingService.Instance.LogInfo("Console de débogage mise au premier plan", "MainWindow");
                    return;
                }
                
                // Créer une nouvelle instance de la console
                _debugConsole = new DebugConsoleWindow()
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                
                // Connecter la console au service de logging
                LoggingService.Instance.SetConsoleWindow(_debugConsole);
                
                // Gérer la fermeture de la console
                _debugConsole.Closed += (s, e) =>
                {
                    _debugConsole = null;
                    LoggingService.Instance.SetConsoleWindow(null);
                    LoggingService.Instance.LogInfo("Console de débogage fermée", "MainWindow");
                };
                
                // Afficher la console
                _debugConsole.Show();
                
                // Mettre à jour le statut
                AppStatusText.Text = "Console de débogage ouverte";
                
                LoggingService.Instance.LogInfo("Console de débogage ouverte", "MainWindow");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'ouverture de la console de débogage: {ex.Message}", "MainWindow", ex);
                MessageBox.Show($"Impossible d'ouvrir la console de débogage: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
         
        private void ApplyCustomBackground()
        {
            if (Properties.Settings.Default.UseCustomBackground && !string.IsNullOrEmpty(Properties.Settings.Default.CustomBackgroundPath))
            {
                try
                {
                    // Charger l'image personnalisée
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(Properties.Settings.Default.CustomBackgroundPath);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    
                    // Appliquer l'image au contrôle CustomBackgroundImage
                    if (CustomBackgroundImage != null)
                    {
                        CustomBackgroundImage.Source = image;
                        CustomBackgroundImage.Visibility = Visibility.Visible;
                        
                        // Appliquer l'opacité par défaut (sera configurable plus tard)
                        double opacity = 0.15; // Valeur par défaut
                        CustomBackgroundImage.Opacity = opacity;
                    }
                    
                    // Afficher l'overlay pour améliorer la lisibilité
                    if (BackgroundOverlay != null)
                    {
                        BackgroundOverlay.Visibility = Visibility.Visible;
                    }
                    
                    LoggingService.Instance.LogInfo($"Fond d'écran personnalisé appliqué: {Path.GetFileName(Properties.Settings.Default.CustomBackgroundPath)}", "MainWindow");
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors du chargement du fond personnalisé: {ex.Message}", "MainWindow", ex);
                    
                    // En cas d'erreur, masquer les éléments de fond personnalisé
                    if (CustomBackgroundImage != null)
                    {
                        CustomBackgroundImage.Visibility = Visibility.Collapsed;
                        CustomBackgroundImage.Source = null;
                    }
                    if (BackgroundOverlay != null)
                    {
                        BackgroundOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                // Masquer le fond personnalisé et utiliser le fond par défaut
                if (CustomBackgroundImage != null)
                {
                    CustomBackgroundImage.Visibility = Visibility.Collapsed;
                    CustomBackgroundImage.Source = null;
                }
                if (BackgroundOverlay != null)
                {
                    BackgroundOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        private void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is ThemeManager.AppTheme theme)
            {
                ThemeManager.ChangeTheme(theme);
                AppStatusText.Text = $"Thème {theme} appliqué";
            }
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // S'assurer que tous les services sont correctement arrêtés
            try
            {
                // Arrêter le timer de progression
                if (_progressTimer != null)
                {
                    _progressTimer.Stop();
                    _progressTimer.Dispose();
                }
                
                // Se désabonner des événements
                if (_musicService != null)
                {
                    _musicService.TrackChanged -= MusicService_TrackChanged;
                    _musicService.PlayStateChanged -= MusicService_PlayStateChanged;
                    _musicService.ServiceStatusChanged -= MusicService_ServiceStatusChanged;
                }
                
                if (_discordService != null)
                {
                    _discordService.ConnectionStatusChanged -= DiscordService_ConnectionStatusChanged;
                }
                
                if (_updateService != null)
                {
                    _updateService.UpdateStatusChanged -= UpdateService_StatusChanged;
                    _updateService.Dispose();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la fermeture de l'application: {ex.Message}", "MainWindow", ex);
            }
            
            base.OnClosing(e);
        }

        // Les méthodes ShowWindow_Click et ExitApplication_Click ont été déplacées dans App.xaml.cs

        private void LoadDefaultAlbumArt()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("pack://application:,,,/Resources/default_album.png", UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze(); // Optimisation pour éviter les fuites de mémoire
                AlbumArt.Source = bitmap;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors du chargement de l'image par défaut: {ex.Message}", "MainWindow", ex);
            }
        }
        
        private void AutoStartCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.AutoStartEnabled = AutoStartCheckBox.IsChecked ?? false;
                Properties.Settings.Default.Save();
                
                // Mettre à jour la configuration du démarrage automatique
                ((App)Application.Current).ConfigureAutoStart();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde des paramètres: {ex.Message}", "MainWindow", ex);
                MessageBox.Show($"Impossible de sauvegarder les paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void MinimizeToTrayCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde des paramètres: {ex.Message}", "MainWindow", ex);
                MessageBox.Show($"Impossible de sauvegarder les paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ShowNotificationsCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? true;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde des paramètres: {ex.Message}", "MainWindow", ex);
                MessageBox.Show($"Impossible de sauvegarder les paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CheckUpdateOnStartupCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.CheckUpdateOnStartup = CheckUpdateOnStartupCheckBox?.IsChecked == true;
                Properties.Settings.Default.Save();
                
                // Mettre à jour le statut
                if (AppStatusText != null)
                {
                    AppStatusText.Text = CheckUpdateOnStartupCheckBox?.IsChecked == true 
                        ? "Vérification des mises à jour au démarrage activée" 
                        : "Vérification des mises à jour au démarrage désactivée";
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde des paramètres: {ex.Message}", "MainWindow", ex);
                if (AppStatusText != null)
                {
                    AppStatusText.Text = $"Erreur lors de la sauvegarde: {ex.Message}";
                }
            }
        }

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Vérifier les mises à jour
                if (_updateService != null)
                {
                    _ = _updateService.CheckForUpdatesAsync(true);
                    if (AppStatusText != null)
                    {
                        AppStatusText.Text = "Vérification des mises à jour...";
                    }
                }
                else
                {
                    if (AppStatusText != null)
                    {
                        AppStatusText.Text = "Service de mise à jour non disponible";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la vérification des mises à jour: {ex.Message}", "MainWindow", ex);
                if (AppStatusText != null)
                {
                    AppStatusText.Text = $"Erreur lors de la vérification: {ex.Message}";
                }
            }
        }
        
        private void ConfigUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_updateService != null)
                {
                    // Ouvrir la fenêtre de configuration des mises à jour
                    var configWindow = new UpdateConfigWindow(_updateService);
                    configWindow.Owner = this;
                    configWindow.ShowDialog();
                    
                    if (AppStatusText != null)
                    {
                        AppStatusText.Text = "Configuration des mises à jour mise à jour";
                    }
                }
                else
                {
                    if (AppStatusText != null)
                    {
                        AppStatusText.Text = "Service de mise à jour non disponible";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'ouverture de la configuration: {ex.Message}", "MainWindow", ex);
                if (AppStatusText != null)
                {
                    AppStatusText.Text = $"Erreur lors de l'ouverture: {ex.Message}";
                }
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // Créer une fenêtre À propos plus élaborée
            Window aboutWindow = new Window
            {
                Title = "À propos de iTunes RPC",
                Width = 450,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Style = (Style)FindResource("ModernWindow"),
                ResizeMode = ResizeMode.NoResize
            };
            
            // Créer le contenu de la fenêtre
            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };
            
            StackPanel panel = new StackPanel { Margin = new Thickness(10) };
            
            // Logo et titre
            Image logoImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Resources/icon.ico")),
                Width = 64,
                Height = 64,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(logoImage);
            
            // Titre
            TextBlock titleBlock = new TextBlock
            {
                Text = "iTunes RPC",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(titleBlock);
            
            // Version
            TextBlock versionBlock = new TextBlock
            {
                Text = $"Version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(versionBlock);
            
            // Description
            TextBlock descriptionBlock = new TextBlock
            {
                Text = "Cette application permet d'afficher vos musiques iTunes/Apple Music sur votre profil Discord.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(descriptionBlock);
            
            // Fonctionnalités
            TextBlock featuresTitle = new TextBlock
            {
                Text = "Fonctionnalités :",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(featuresTitle);
            
            // Liste des fonctionnalités
            string[] features = new string[]
            {
                "Affichage en temps réel de la musique en cours de lecture",
                "Personnalisation de l'interface avec différents thèmes",
                "Fond d'écran personnalisable",
                "Démarrage automatique avec Windows",
                "Notifications lors des changements de piste",
                "Mises à jour automatiques"
            };
            
            foreach (var feature in features)
            {
                TextBlock featureBlock = new TextBlock
                {
                    Text = "• " + feature,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 5)
                };
                panel.Children.Add(featureBlock);
            }
            
            // Lien GitHub
            TextBlock githubTitle = new TextBlock
            {
                Text = "GitHub :",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 5)
            };
            panel.Children.Add(githubTitle);
            
            TextBlock githubLink = new TextBlock
            {
                Text = "https://github.com/darkiifr/ITunesRPC",
                Foreground = (SolidColorBrush)Application.Current.Resources["AccentBrush"],
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 15)
            };
            githubLink.MouseDown += (s, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/darkiifr/ITunesRPC",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Impossible d'ouvrir le lien: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            panel.Children.Add(githubLink);
            
            // Copyright
            TextBlock copyrightBlock = new TextBlock
            {
                Text = $"© {DateTime.Now.Year} - Tous droits réservés",
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontStyle = FontStyles.Italic
            };
            panel.Children.Add(copyrightBlock);
            
            // Bouton de fermeture
            Button closeButton = new Button
            {
                Content = "Fermer",
                Style = (Style)FindResource("ModernButton"),
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            closeButton.Click += (s, args) => aboutWindow.Close();
            panel.Children.Add(closeButton);
            
            scrollViewer.Content = panel;
            aboutWindow.Content = scrollViewer;
            aboutWindow.ShowDialog();
        }
    }
}