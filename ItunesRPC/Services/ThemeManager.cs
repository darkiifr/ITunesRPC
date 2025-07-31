using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;

namespace ItunesRPC.Services
{
    public static class ThemeManager
    {
        // Énumération des thèmes disponibles
        public enum AppTheme
        {
            Dark,
            Light,
            Blue,
            Purple,
            Green,
            Orange,
            Red
        }
        
        // Dictionnaire pour mapper les thèmes à leurs noms de ressource
        private static readonly Dictionary<AppTheme, string> ThemeResourceNames = new Dictionary<AppTheme, string>
        {
            { AppTheme.Dark, "DarkTheme" },
            { AppTheme.Light, "LightTheme" },
            { AppTheme.Blue, "BlueTheme" },
            { AppTheme.Purple, "PurpleTheme" },
            { AppTheme.Green, "GreenTheme" },
            { AppTheme.Orange, "OrangeTheme" },
            { AppTheme.Red, "RedTheme" }
        };
        
        // Thème actuel
        private static AppTheme _currentTheme = AppTheme.Dark;
        
        // Événement pour notifier les changements de thème
        public static event EventHandler<AppTheme>? ThemeChanged;
        
        // Propriété pour obtenir le thème actuel
        public static AppTheme CurrentTheme => _currentTheme;
        
        // Méthode pour changer de thème
        public static void ChangeTheme(AppTheme theme)
        {
            // Stocker le nouveau thème
            _currentTheme = theme;
            
            // Appliquer le thème à toutes les fenêtres
            ApplyThemeToAllWindows(theme);
            
            // Sauvegarder le thème dans les paramètres
            SaveThemeSettings(theme);
            
            // Notifier les abonnés du changement de thème
            ThemeChanged?.Invoke(null, theme);
        }
        
        // Méthode pour appliquer le thème à toutes les fenêtres
        private static void ApplyThemeToAllWindows(AppTheme theme)
        {
            if (!ThemeResourceNames.TryGetValue(theme, out string? resourceName) || resourceName == null)
                return;
            
            try
            {
                // Appliquer le thème aux ressources de l'application
                ApplyThemeToApplication(theme, resourceName);
                
                // Appliquer le thème à toutes les fenêtres ouvertes
                foreach (Window window in Application.Current.Windows)
                {
                    ApplyThemeToWindow(window, theme, resourceName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de l'application du thème: {ex.Message}");
            }
        }
        
        // Méthode pour appliquer le thème aux ressources de l'application
        private static void ApplyThemeToApplication(AppTheme theme, string resourceName)
        {
            try
            {
                var appResources = Application.Current.Resources;
                
                if (appResources[resourceName] is ResourceDictionary themeDictionary)
                {
                    UpdateColorAndBrush(appResources, themeDictionary, "PrimaryColor", "PrimaryBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "SecondaryColor", "SecondaryBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "AccentColor", "AccentBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "TextPrimaryColor", "TextPrimaryBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "TextSecondaryColor", "TextSecondaryBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "BorderColor", "BorderBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "BackgroundColor", "BackgroundBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "HoverColor", "HoverBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "AppBackgroundColor", "AppBackgroundBrush");
                    UpdateColorAndBrush(appResources, themeDictionary, "CardBackgroundColor", "CardBackgroundBrush");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de l'application du thème à l'application: {ex.Message}");
            }
        }
        
        // Méthode pour appliquer le thème à une fenêtre spécifique
        private static void ApplyThemeToWindow(Window window, AppTheme theme, string resourceName)
        {
            try
            {
                var appResources = Application.Current.Resources;
                
                if (appResources[resourceName] is ResourceDictionary themeDictionary)
                {
                    // Mettre à jour les ressources de la fenêtre
                    UpdateWindowResources(window, themeDictionary);
                    
                    // Forcer la mise à jour de l'interface utilisateur
                    window.InvalidateVisual();
                    window.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de l'application du thème à la fenêtre {window.GetType().Name}: {ex.Message}");
            }
        }
        
        // Méthode pour mettre à jour les ressources d'une fenêtre
        private static void UpdateWindowResources(Window window, ResourceDictionary themeDictionary)
        {
            var colorKeys = new[]
            {
                "PrimaryColor", "SecondaryColor", "AccentColor", "TextPrimaryColor", "TextSecondaryColor",
                "BorderColor", "BackgroundColor", "HoverColor", "AppBackgroundColor", "CardBackgroundColor"
            };
            
            foreach (var colorKey in colorKeys)
            {
                if (themeDictionary[colorKey] is Color color)
                {
                    var brushKey = colorKey.Replace("Color", "Brush");
                    
                    // Mettre à jour dans les ressources de la fenêtre
                    window.Resources[colorKey] = color;
                    
                    var newBrush = new SolidColorBrush(color);
                    newBrush.Freeze();
                    window.Resources[brushKey] = newBrush;
                }
            }
        }
        
        // Méthode pour mettre à jour une couleur et créer une nouvelle brosse
        private static void UpdateColorAndBrush(ResourceDictionary appResources, ResourceDictionary themeDictionary, string colorKey, string brushKey)
        {
            try
            {
                if (themeDictionary[colorKey] is Color color)
                {
                    // Mettre à jour la couleur dans les ressources principales
                    appResources[colorKey] = color;
                    
                    // Créer une nouvelle brosse au lieu de modifier l'existante
                    var newBrush = new SolidColorBrush(color);
                    newBrush.Freeze(); // Optimisation pour les performances
                    appResources[brushKey] = newBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la mise à jour de {colorKey}/{brushKey}: {ex.Message}");
            }
        }
        
        // Méthode pour sauvegarder les paramètres de thème
        private static void SaveThemeSettings(AppTheme theme)
        {
            try
            {
                if (Properties.Settings.Default.Properties["CurrentTheme"] != null)
                {
                    Properties.Settings.Default["CurrentTheme"] = (int)theme;
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la sauvegarde des paramètres de thème: {ex.Message}");
            }
        }
        
        // Méthode pour charger le thème depuis les paramètres
        public static void LoadSavedTheme()
        {
            try
            {
                int savedTheme = 0; // Valeur par défaut (Dark)
                
                // Vérifier si la propriété existe dans Settings
                if (Properties.Settings.Default.Properties["CurrentTheme"] != null)
                {
                    savedTheme = (int)Properties.Settings.Default["CurrentTheme"];
                }
                if (Enum.IsDefined(typeof(AppTheme), savedTheme))
                {
                    ChangeTheme((AppTheme)savedTheme);
                }
                else
                {
                    // Thème par défaut si le thème sauvegardé n'est pas valide
                    ChangeTheme(AppTheme.Dark);
                }
            }
            catch
            {
                // En cas d'erreur, utiliser le thème par défaut
                ChangeTheme(AppTheme.Dark);
            }
        }
        
        // Méthode pour appliquer le thème à une nouvelle fenêtre
        public static void ApplyCurrentThemeToWindow(Window window)
        {
            if (ThemeResourceNames.TryGetValue(_currentTheme, out string? resourceName) && resourceName != null)
            {
                ApplyThemeToWindow(window, _currentTheme, resourceName);
            }
        }
        
        // Méthode pour obtenir les couleurs du thème actuel
        public static Dictionary<string, Color> GetCurrentThemeColors()
        {
            var colors = new Dictionary<string, Color>();
            
            if (ThemeResourceNames.TryGetValue(_currentTheme, out string? resourceName) && resourceName != null)
            {
                try
                {
                    var appResources = Application.Current.Resources;
                    if (appResources[resourceName] is ResourceDictionary themeDictionary)
                    {
                        var colorKeys = new[]
                        {
                            "PrimaryColor", "SecondaryColor", "AccentColor", "TextPrimaryColor", "TextSecondaryColor",
                            "BorderColor", "BackgroundColor", "HoverColor", "AppBackgroundColor", "CardBackgroundColor"
                        };
                        
                        foreach (var colorKey in colorKeys)
                        {
                            if (themeDictionary[colorKey] is Color color)
                            {
                                colors[colorKey] = color;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erreur lors de la récupération des couleurs du thème: {ex.Message}");
                }
            }
            
            return colors;
        }
    }
}