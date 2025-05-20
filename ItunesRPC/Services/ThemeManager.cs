using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;

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
            
            // Appliquer le thème
            ApplyTheme(theme);
            
            // Sauvegarder le thème dans les paramètres
            if (Properties.Settings.Default.Properties["CurrentTheme"] != null)
            {
                Properties.Settings.Default["CurrentTheme"] = (int)theme;
                Properties.Settings.Default.Save();
            }
            
            // Notifier les abonnés du changement de thème
            ThemeChanged?.Invoke(null, theme);
        }
        
        // Méthode pour appliquer le thème
        private static void ApplyTheme(AppTheme theme)
        {
            if (!ThemeResourceNames.TryGetValue(theme, out string? resourceName) || resourceName == null)
                return;
            
            // Obtenir les dictionnaires de ressources de l'application
            var appResources = Application.Current.Resources;
            
            // Obtenir le dictionnaire de thème sélectionné
            if (appResources[resourceName] is ResourceDictionary themeDictionary)
            {
                // Mettre à jour les brosses dynamiques avec les couleurs du thème
                UpdateBrush(appResources, themeDictionary, "PrimaryColor", "PrimaryBrush");
                UpdateBrush(appResources, themeDictionary, "SecondaryColor", "SecondaryBrush");
                UpdateBrush(appResources, themeDictionary, "AccentColor", "AccentBrush");
                UpdateBrush(appResources, themeDictionary, "TextPrimaryColor", "TextPrimaryBrush");
                UpdateBrush(appResources, themeDictionary, "TextSecondaryColor", "TextSecondaryBrush");
                UpdateBrush(appResources, themeDictionary, "BorderColor", "BorderBrush");
                UpdateBrush(appResources, themeDictionary, "BackgroundColor", "BackgroundBrush");
                UpdateBrush(appResources, themeDictionary, "HoverColor", "HoverBrush");
                UpdateBrush(appResources, themeDictionary, "AppBackgroundColor", "AppBackgroundBrush");
                UpdateBrush(appResources, themeDictionary, "CardBackgroundColor", "CardBackgroundBrush");
            }
        }
        
        // Méthode pour mettre à jour une brosse avec une couleur
        private static void UpdateBrush(ResourceDictionary appResources, ResourceDictionary themeDictionary, string colorKey, string brushKey)
        {
            if (themeDictionary[colorKey] is Color color && appResources[brushKey] is SolidColorBrush brush)
            {
                brush.Color = color;
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
    }
}