using ItunesRPC.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ItunesRPC
{
    public partial class ThemeSelector : Window
    {
        private ObservableCollection<ThemeItem> _themes;
        private string _selectedBackgroundPath;
        private ThemeManager.AppTheme _selectedTheme;
        
        public ThemeSelector()
        {
            InitializeComponent();
            LoadThemes();
            
            // Charger les paramètres actuels
            _selectedTheme = ThemeManager.CurrentTheme;
            UseCustomBackgroundCheckBox.IsChecked = Properties.Settings.Default.UseCustomBackground;
            _selectedBackgroundPath = Properties.Settings.Default.CustomBackgroundPath;
            
            // Activer/désactiver le bouton de sélection d'image
            SelectBackgroundButton.IsEnabled = UseCustomBackgroundCheckBox.IsChecked == true;
        }
        
        private void LoadThemes()
        {
            _themes = new ObservableCollection<ThemeItem>
            {
                new ThemeItem
                {
                    Name = "Sombre",
                    ThemeType = ThemeManager.AppTheme.Dark,
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
                    ThemeType = ThemeManager.AppTheme.Light,
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
                    ThemeType = ThemeManager.AppTheme.Blue,
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
                    ThemeType = ThemeManager.AppTheme.Purple,
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
                    ThemeType = ThemeManager.AppTheme.Green,
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
                    ThemeType = ThemeManager.AppTheme.Orange,
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
                    ThemeType = ThemeManager.AppTheme.Red,
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
            
            // Ajouter les gestionnaires d'événements pour la sélection
            foreach (var item in ThemesItemsControl.Items)
            {
                var container = ThemesItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    container.MouseLeftButtonDown += ThemeItem_Click;
                }
            }
        }
        
        private void ThemeItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var themeItem = element.DataContext as ThemeItem;
                if (themeItem != null)
                {
                    // Désélectionner tous les thèmes
                    foreach (var item in _themes)
                    {
                        item.IsSelected = false;
                    }
                    
                    // Sélectionner le thème cliqué
                    themeItem.IsSelected = true;
                    _selectedTheme = themeItem.ThemeType;
                }
            }
        }
        
        private void UseCustomBackgroundCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            SelectBackgroundButton.IsEnabled = UseCustomBackgroundCheckBox.IsChecked == true;
        }
        
        private void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Sélectionner une image de fond"
            };
            
            if (dialog.ShowDialog() == true)
            {
                _selectedBackgroundPath = dialog.FileName;
                MessageBox.Show("Image sélectionnée : " + Path.GetFileName(_selectedBackgroundPath), "Image sélectionnée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Appliquer le thème sélectionné
            ThemeManager.ChangeTheme(_selectedTheme);
            
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
                                Opacity = 0.2 // Opacité réduite pour ne pas gêner la lisibilité
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
    }
    
    public class ThemeItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public string Name { get; set; }
        public ThemeManager.AppTheme ThemeType { get; set; }
        public Brush PrimaryColor { get; set; }
        public Brush SecondaryColor { get; set; }
        public Brush AccentColor { get; set; }
        public Brush BackgroundColor { get; set; }
        public Brush CardBackgroundColor { get; set; }
        public Brush TextColor { get; set; }
        
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
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}