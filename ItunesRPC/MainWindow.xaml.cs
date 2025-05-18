using ItunesRPC.Models;
using ItunesRPC.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ItunesRPC
{
    public partial class MainWindow : Window
    {
        private readonly ItunesService _itunesService;
        private readonly DiscordRpcService _discordService;
        private readonly UpdateService _updateService;

        public MainWindow(ItunesService itunesService, DiscordRpcService discordService, UpdateService updateService)
        {
            InitializeComponent();
            
            _itunesService = itunesService;
            _discordService = discordService;
            _updateService = updateService;

            // S'abonner aux événements
            _itunesService.TrackChanged += ItunesService_TrackChanged;
            _itunesService.PlayStateChanged += ItunesService_PlayStateChanged;

            // Charger les paramètres
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                AutoStartCheckBox.IsChecked = Properties.Settings.Default.AutoStartEnabled;
                MinimizeToTrayCheckBox.IsChecked = Properties.Settings.Default.MinimizeToTray;
                
                // Vérifier les mises à jour au démarrage (silencieusement)
                _ = _updateService.CheckForUpdatesAsync(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement des paramètres: {ex.Message}");
                // Utiliser des valeurs par défaut en cas d'erreur
                AutoStartCheckBox.IsChecked = false;
                MinimizeToTrayCheckBox.IsChecked = true;
            }
        }

        private void ItunesService_TrackChanged(object? sender, TrackInfoEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Mettre à jour l'interface utilisateur avec les informations de la piste
                TrackNameText.Text = e.TrackInfo.Name;
                ArtistText.Text = e.TrackInfo.Artist;
                AlbumText.Text = e.TrackInfo.Album;
                PositionText.Text = $"{e.TrackInfo.TrackNumber} sur {e.TrackInfo.TrackCount}";
                StatusText.Text = "En cours de lecture";

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
                        Console.WriteLine($"Erreur lors du chargement de l'image: {ex.Message}");
                    }
                }
                else
                {
                    // Utiliser l'image par défaut si aucune pochette n'est disponible
                    LoadDefaultAlbumArt();
                }

                // Mettre à jour Discord Rich Presence
                _discordService.UpdatePresence(e.TrackInfo);
            });
        }

        private void ItunesService_PlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsPlaying)
                {
                    StatusText.Text = "En cours de lecture";
                }
                else
                {
                    StatusText.Text = "Lecture en pause";
                    _discordService.ClearPresence();
                }
            });
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowMainWindow();
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ExitApplication();
        }

        private void LoadDefaultAlbumArt()
        {
            try
            {
                var bitmap = new BitmapImage(new Uri("/Resources/default_album.png", UriKind.Relative));
                bitmap.Freeze(); // Optimisation pour éviter les fuites de mémoire
                AlbumArt.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de l'image par défaut: {ex.Message}");
            }
        }
        
        private void AutoStartCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.AutoStartEnabled = AutoStartCheckBox.IsChecked ?? false;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde des paramètres: {ex.Message}");
                MessageBox.Show($"Impossible de sauvegarder les paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            _ = _updateService.CheckForUpdatesAsync(true);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                $"iTunes RPC v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}\n\n" +
                "Affiche vos musiques iTunes/Apple Music sur Discord\n\n" +
                "GitHub: https://github.com/darkiiuseai/ITunesRPC",
                "À propos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}