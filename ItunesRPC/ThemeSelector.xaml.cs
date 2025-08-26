using ItunesRPC.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ItunesRPC
{
    public partial class ThemeSelector : Window
    {
        private ObservableCollection<ThemeItem> _themes = new ObservableCollection<ThemeItem>();
        private string _selectedBackgroundPath = string.Empty;
        private ThemeManager.AppTheme _selectedTheme;
        
        public ThemeSelector()
        {
            InitializeComponent();
            
            // Enregistrer cette fenêtre pour la propagation automatique des thèmes
            ThemeManager.RegisterWindow(this);
            
            LoadThemes();
            
            // Charger les paramètres actuels
            _selectedTheme = ThemeManager.CurrentTheme;
            
            // Vérifier si les propriétés existent dans Settings
            if (Properties.Settings.Default.Properties["UseCustomBackground"] != null)
            {
                UseCustomBackgroundCheckBox.IsChecked = (bool)Properties.Settings.Default["UseCustomBackground"];
            }
            
            if (Properties.Settings.Default.Properties["CustomBackgroundPath"] != null)
            {
                _selectedBackgroundPath = (string)Properties.Settings.Default["CustomBackgroundPath"];
            }
            
            // Activer/désactiver le bouton de sélection d'image si le contrôle existe
            if (this.FindName("SelectBackgroundButton") is System.Windows.Controls.Button selectButton)
            {
                selectButton.IsEnabled = UseCustomBackgroundCheckBox.IsChecked == true;
            }
        }
        
        private void LoadThemes()
        {
            _themes = new ObservableCollection<ThemeItem>
            {
                new ThemeItem
                {
                    Name = "Sombre",
                    Type = ThemeManager.AppTheme.Dark,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2D")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Dark
                },
                new ThemeItem
                {
                    Name = "Clair",
                    Type = ThemeManager.AppTheme.Light,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D7")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                    TextColor = new SolidColorBrush(Colors.Black),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Light
                },
                new ThemeItem
                {
                    Name = "Bleu",
                    Type = ThemeManager.AppTheme.Blue,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A4973")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00AEFF")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1E32")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Blue
                },
                new ThemeItem
                {
                    Name = "Violet",
                    Type = ThemeManager.AppTheme.Purple,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A266A")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D3A7E")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A1540")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A266A")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Purple
                },
                new ThemeItem
                {
                    Name = "Vert",
                    Type = ThemeManager.AppTheme.Green,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5F34")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A7349")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C853")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F3220")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5F34")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Green
                },
                new ThemeItem
                {
                    Name = "Orange",
                    Type = ThemeManager.AppTheme.Orange,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F3A1E")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#73492A")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32200F")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F3A1E")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Orange
                },
                new ThemeItem
                {
                    Name = "Rouge",
                    Type = ThemeManager.AppTheme.Red,
                    PrimaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F1E1E")),
                    SecondaryColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#732A2A")),
                    AccentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935")),
                    BackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#320F0F")),
                    CardBackgroundColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F1E1E")),
                    TextColor = new SolidColorBrush(Colors.White),
                    IsSelected = ThemeManager.CurrentTheme == ThemeManager.AppTheme.Red
                }
            };
            
            ThemesItemsControl.ItemsSource = _themes;
            
            // Attacher les gestionnaires d'événements après que les éléments soient générés
            ThemesItemsControl.Loaded += (s, e) => AttachEventHandlers();
        }
        
        private void AttachEventHandlers()
        {
            // Attendre que les conteneurs soient générés
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0; i < _themes.Count; i++)
                {
                    var container = ThemesItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null)
                    {
                        // Trouver le Border principal dans le template
                        var border = FindChild<Border>(container, "ThemeCard");
                        if (border != null)
                        {
                            border.MouseLeftButtonDown += ThemeCard_Click;
                        }
                        else
                        {
                            // Fallback: attacher à l'élément conteneur
                            container.MouseLeftButtonDown += ThemeCard_Click;
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        // Méthode utilitaire pour trouver un enfant par nom
        private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T && (child as FrameworkElement)?.Name == childName)
                {
                    return (T)child;
                }
                
                var childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }
        
        private void ThemeCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is ThemeItem selectedTheme)
                {
                    // Désélectionner tous les thèmes
                    foreach (var theme in _themes)
                    {
                        theme.IsSelected = false;
                    }

                    // Sélectionner le thème cliqué
                    selectedTheme.IsSelected = true;

                    // Appliquer le thème immédiatement à toutes les fenêtres
                    ThemeManager.ChangeTheme(selectedTheme.Type);

                    // Sauvegarder la sélection
                    // Properties.Settings.Default.SelectedTheme = selectedTheme.Type.ToString();
                    Properties.Settings.Default.Save();

                    // Feedback visuel
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        var statusText = mainWindow.FindName("StatusText") as TextBlock;
                        if (statusText != null)
                        {
                            statusText.Text = $"Thème '{selectedTheme.Name}' appliqué avec succès";
                            statusText.Foreground = new SolidColorBrush(Colors.Green);
                            
                            // Effacer le message après 3 secondes
                            var timer = new System.Windows.Threading.DispatcherTimer();
                            timer.Interval = TimeSpan.FromSeconds(3);
                            timer.Tick += (s, args) =>
                            {
                                statusText.Text = "";
                                timer.Stop();
                            };
                            timer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'application du thème : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UseCustomBackground_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Mettre à jour l'état des contrôles liés au fond personnalisé
            if (CustomBackgroundPathTextBox != null)
            {
                CustomBackgroundPathTextBox.IsEnabled = UseCustomBackgroundCheckBox.IsChecked == true;
            }
        }
        
        private void BrowseBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Sélectionner une image de fond"
            };
            
            if (dialog.ShowDialog() == true)
            {
                _selectedBackgroundPath = dialog.FileName;
                CustomBackgroundPathTextBox.Text = _selectedBackgroundPath;
                MessageBox.Show("Image sélectionnée : " + Path.GetFileName(_selectedBackgroundPath), "Image sélectionnée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void BackgroundOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Gérer le changement d'opacité du fond
            // Cette méthode sera implémentée pour appliquer l'opacité en temps réel
        }
        
        private void EnableBlurEffect_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Gérer l'activation/désactivation de l'effet de flou
            // Cette méthode sera implémentée pour appliquer l'effet en temps réel
        }
        
        private void EnableAnimations_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Gérer l'activation/désactivation des animations
            // Cette méthode sera implémentée pour appliquer les animations en temps réel
        }
        
        private void EnableRoundedCorners_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Gérer l'activation/désactivation des coins arrondis
            // Cette méthode sera implémentée pour appliquer les coins arrondis en temps réel
        }
        
        private void EnableShadows_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Gérer l'activation/désactivation des ombres
            // Cette méthode sera implémentée pour appliquer les ombres en temps réel
        }
        
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // Réinitialiser les paramètres à leurs valeurs par défaut
            UseCustomBackgroundCheckBox.IsChecked = false;
            CustomBackgroundPathTextBox.Text = string.Empty;
            _selectedBackgroundPath = string.Empty;
            BackgroundOpacitySlider.Value = 0.15;
            EnableBlurEffectCheckBox.IsChecked = true;
            EnableAnimationsCheckBox.IsChecked = true;
            EnableRoundedCornersCheckBox.IsChecked = true;
            EnableShadowsCheckBox.IsChecked = true;
            
            // Réinitialiser le thème sélectionné au thème par défaut (Sombre)
            foreach (var item in _themes)
            {
                item.IsSelected = item.Type == ThemeManager.AppTheme.Dark;
            }
        }
        
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Trouver le thème sélectionné
                var selectedTheme = _themes.FirstOrDefault(t => t.IsSelected);
                if (selectedTheme != null)
                {
                    // Appliquer le thème sélectionné à toutes les fenêtres
                    ThemeManager.ChangeTheme(selectedTheme.Type);
                    
                    // Sauvegarder la sélection
                    // Properties.Settings.Default.SelectedTheme = selectedTheme.Type.ToString();
                }
                
                // Enregistrer les paramètres de fond personnalisé
                Properties.Settings.Default.UseCustomBackground = UseCustomBackgroundCheckBox.IsChecked == true;
                if (UseCustomBackgroundCheckBox.IsChecked == true && !string.IsNullOrEmpty(_selectedBackgroundPath))
                {
                    Properties.Settings.Default.CustomBackgroundPath = _selectedBackgroundPath;
                    
                    // Appliquer le fond personnalisé
                    try
                    {
                        var app = Application.Current;
                        if (app != null && app.MainWindow != null)
                        {
                            var mainGrid = app.MainWindow.FindName("MainGrid") as Grid;
                            if (mainGrid != null)
                            {
                                var image = new BitmapImage(new Uri(_selectedBackgroundPath));
                                var brush = new ImageBrush(image)
                                {
                                    Stretch = Stretch.UniformToFill,
                                    Opacity = BackgroundOpacitySlider.Value
                                };
                                mainGrid.Background = brush;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erreur lors de l'application du fond personnalisé : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Réinitialiser le fond à la couleur du thème
                    var app = Application.Current;
                    if (app != null && app.MainWindow != null)
                    {
                        var mainGrid = app.MainWindow.FindName("MainGrid") as Grid;
                        if (mainGrid != null)
                        {
                            mainGrid.Background = (SolidColorBrush)app.Resources["AppBackgroundBrush"];
                        }
                    }
                }
                
                Properties.Settings.Default.Save();
                
                // Fermer la fenêtre
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'application des paramètres : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    public class ThemeItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public string Name { get; set; } = string.Empty;
        public ThemeManager.AppTheme Type { get; set; }
        public Brush PrimaryColor { get; set; } = new SolidColorBrush(Colors.Black);
        public Brush SecondaryColor { get; set; } = new SolidColorBrush(Colors.Black);
        public Brush AccentColor { get; set; } = new SolidColorBrush(Colors.Black);
        public Brush BackgroundColor { get; set; } = new SolidColorBrush(Colors.Black);
        public Brush CardBackgroundColor { get; set; } = new SolidColorBrush(Colors.Black);
        public Brush TextColor { get; set; } = new SolidColorBrush(Colors.Black);
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}