using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Collections.Concurrent;
using System.Windows.Threading;

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
        
        // Liste des fenêtres enregistrées pour la propagation automatique des thèmes
        private static readonly List<WeakReference> _registeredWindows = new List<WeakReference>();
        
        // Propriété pour obtenir le thème actuel
        public static AppTheme CurrentTheme => _currentTheme;
        
        // Méthode pour enregistrer une fenêtre pour la propagation automatique des thèmes
        public static void RegisterWindow(Window window)
        {
            if (window == null) return;
            
            // Nettoyer les références mortes
            CleanupDeadReferences();
            
            // Vérifier si la fenêtre n'est pas déjà enregistrée
            bool alreadyRegistered = _registeredWindows.Any(wr => wr.IsAlive && ReferenceEquals(wr.Target, window));
            
            if (!alreadyRegistered)
            {
                _registeredWindows.Add(new WeakReference(window));
                
                // Appliquer immédiatement le thème actuel à la nouvelle fenêtre
                ApplyThemeToWindow(window, _currentTheme, GetThemeResourceName(_currentTheme));
                
                // S'abonner à l'événement de fermeture pour nettoyer automatiquement
                window.Closed += (sender, e) => UnregisterWindow(window);
            }
        }
        
        // Méthode pour désenregistrer une fenêtre
        public static void UnregisterWindow(Window window)
        {
            if (window == null) return;
            
            _registeredWindows.RemoveAll(wr => !wr.IsAlive || ReferenceEquals(wr.Target, window));
        }
        
        // Méthode pour nettoyer les références mortes
        private static void CleanupDeadReferences()
        {
            _registeredWindows.RemoveAll(wr => !wr.IsAlive);
        }
        
        // Méthode pour obtenir le nom de ressource d'un thème
        private static string GetThemeResourceName(AppTheme theme)
        {
            return ThemeResourceNames.TryGetValue(theme, out string? resourceName) ? resourceName ?? "DarkTheme" : "DarkTheme";
        }
        
        // Méthode pour changer de thème
        public static void ChangeTheme(AppTheme theme)
        {
            // Stocker le nouveau thème
            _currentTheme = theme;
            
            // Appliquer le thème à toutes les fenêtres (existantes et enregistrées)
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
                
                // Forcer la mise à jour de toutes les ressources de l'application
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Invalider toutes les ressources visuelles
                    foreach (Window window in Application.Current.Windows)
                    {
                        ApplyThemeToWindow(window, theme, resourceName);
                        
                        // Forcer la mise à jour récursive de tous les éléments visuels sans perdre les styles
                        InvalidateVisualTreeSafe(window);
                    }
                    
                    // Appliquer le thème aux fenêtres enregistrées (qui pourraient ne pas être dans Application.Current.Windows)
                    CleanupDeadReferences();
                    foreach (WeakReference windowRef in _registeredWindows.ToList())
                    {
                        if (windowRef.IsAlive && windowRef.Target is Window registeredWindow)
                        {
                            ApplyThemeToWindow(registeredWindow, theme, resourceName);
                            InvalidateVisualTreeSafe(registeredWindow);
                        }
                    }
                    
                    // Recharger seulement les ressources de thème sans vider toutes les ressources
                    ReloadThemeResources();
                });
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
                    // Mettre à jour toutes les couleurs et brosses du thème
                    var colorKeys = new[]
                    {
                        "PrimaryColor", "SecondaryColor", "AccentColor", "TextPrimaryColor", "TextSecondaryColor",
                        "BorderColor", "BackgroundColor", "HoverColor", "AppBackgroundColor", "CardBackgroundColor"
                    };
                    
                    foreach (var colorKey in colorKeys)
                    {
                        var brushKey = colorKey.Replace("Color", "Brush");
                        UpdateColorAndBrush(appResources, themeDictionary, colorKey, brushKey);
                    }
                    
                    // Forcer la mise à jour de tous les dictionnaires de ressources fusionnés
                    foreach (var mergedDict in appResources.MergedDictionaries)
                    {
                        UpdateMergedDictionaryColors(mergedDict, themeDictionary, colorKeys);
                    }
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
            
            // Mettre à jour également les dictionnaires fusionnés de la fenêtre
            foreach (var mergedDict in window.Resources.MergedDictionaries)
            {
                UpdateMergedDictionaryColors(mergedDict, themeDictionary, colorKeys);
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
        
        // Méthode pour invalider récursivement l'arbre visuel sans perdre les styles
        private static void InvalidateVisualTreeSafe(DependencyObject parent)
        {
            try
            {
                if (parent is FrameworkElement element)
                {
                    element.InvalidateVisual();
                    element.UpdateLayout();
                    
                    // Forcer la mise à jour des ressources sans réinitialiser le style
                    element.InvalidateProperty(FrameworkElement.StyleProperty);
                }
                
                // Parcourir récursivement tous les enfants
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    InvalidateVisualTreeSafe(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de l'invalidation de l'arbre visuel: {ex.Message}");
            }
        }
        
        // Méthode pour recharger seulement les ressources de thème
        private static void ReloadThemeResources()
        {
            try
            {
                var app = Application.Current;
                
                // Recharger seulement les ressources de thème
                var themeResourceUris = new[]
                {
                    "/Styles/ThemeStyles.xaml"
                };
                
                foreach (var uri in themeResourceUris)
                {
                    try
                    {
                        // Trouver et remplacer le dictionnaire de thème existant
                        var existingDict = app.Resources.MergedDictionaries
                            .FirstOrDefault(d => d.Source?.ToString().Contains("ThemeStyles.xaml") == true);
                        
                        if (existingDict != null)
                        {
                            var index = app.Resources.MergedDictionaries.IndexOf(existingDict);
                            app.Resources.MergedDictionaries.RemoveAt(index);
                            
                            var newResourceDict = new ResourceDictionary
                            {
                                Source = new Uri(uri, UriKind.Relative)
                            };
                            app.Resources.MergedDictionaries.Insert(index, newResourceDict);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erreur lors du rechargement de {uri}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors du rechargement des ressources de thème: {ex.Message}");
            }
        }
        
        // Méthode pour recharger les ressources de l'application
        private static void LoadApplicationResources()
        {
            try
            {
                var app = Application.Current;
                var resourceUris = new[]
                {
                    "/Styles/AnimationResources.xaml",
                    "/Styles/ModernStyles.xaml",
                    "/Styles/ToolbarStyles.xaml",
                    "/Styles/ThemeStyles.xaml",
                    "/Styles/StatusIndicatorStyles.xaml",
                    "/Styles/BackgroundStyles.xaml",
                    "/ThemeConfig.xaml"
                };
                
                foreach (var uri in resourceUris)
                {
                    try
                    {
                        var resourceDict = new ResourceDictionary
                        {
                            Source = new Uri(uri, UriKind.Relative)
                        };
                        app.Resources.MergedDictionaries.Add(resourceDict);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erreur lors du chargement de {uri}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors du rechargement des ressources: {ex.Message}");
            }
        }
        
        // Méthode pour mettre à jour les couleurs dans les dictionnaires fusionnés
        private static void UpdateMergedDictionaryColors(ResourceDictionary dictionary, ResourceDictionary themeDictionary, string[] colorKeys)
        {
            try
            {
                foreach (var colorKey in colorKeys)
                {
                    if (themeDictionary[colorKey] is Color color)
                    {
                        var brushKey = colorKey.Replace("Color", "Brush");
                        
                        // Mettre à jour la couleur
                        if (dictionary.Contains(colorKey))
                        {
                            dictionary[colorKey] = color;
                        }
                        
                        // Mettre à jour la brosse
                        if (dictionary.Contains(brushKey))
                        {
                            var newBrush = new SolidColorBrush(color);
                            newBrush.Freeze();
                            dictionary[brushKey] = newBrush;
                        }
                    }
                }
                
                // Mettre à jour récursivement les dictionnaires fusionnés
                foreach (var mergedDict in dictionary.MergedDictionaries)
                {
                    UpdateMergedDictionaryColors(mergedDict, themeDictionary, colorKeys);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la mise à jour des couleurs du dictionnaire fusionné: {ex.Message}");
            }
        }
    }
}