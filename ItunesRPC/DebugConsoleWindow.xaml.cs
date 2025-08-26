using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace ItunesRPC
{
    public partial class DebugConsoleWindow : Window
    {
        private List<LogEntry> _allLogs = new List<LogEntry>();
        private LogLevel _currentFilter = LogLevel.All;
        private int _logCount = 0;

        public enum LogLevel
        {
            All,
            Info,
            Warning,
            Error
        }

        public class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public string Source { get; set; }

            public override string ToString()
            {
                string levelStr = Level switch
                {
                    LogLevel.Info => "INFO",
                    LogLevel.Warning => "WARN",
                    LogLevel.Error => "ERR ",
                    _ => "LOG "
                };

                return $"[{Timestamp:HH:mm:ss}] {levelStr}: {Message}";
            }
        }

        public DebugConsoleWindow()
        {
            InitializeComponent();
            InitializeConsole();
        }

        private void InitializeConsole()
        {
            // Appliquer le thème actuel
            ApplyCurrentTheme();
            
            // Ajouter un log d'initialisation
            AddLog("Console de débogage initialisée", LogLevel.Info, "System");
            AddLog("Prêt à recevoir les logs d'application", LogLevel.Info, "System");
            
            UpdateLogCount();
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                // Appliquer les ressources de thème depuis l'application principale
                if (Application.Current?.Resources != null)
                {
                    // Copier les ressources de base nécessaires
                    var resourceKeys = new string[]
                    {
                        "PrimaryBrush", "SecondaryBrush", "AccentBrush", "TextPrimaryBrush", "TextSecondaryBrush",
                        "BorderBrush", "HoverBrush", "PressedBrush", "DisabledBrush", "ErrorBrush", "SuccessBrush",
                        "WarningBrush", "InfoBrush", "ModernButton", "SmallButton", "ToolbarIconButton",
                        "ModernPanel", "HeaderPanel", "ToolbarPanel", "StatusBarPanel", "ModernCheckBox",
                        "ToolbarSeparator", "TitleTextBlock", "SubtitleTextBlock"
                    };

                    foreach (var key in resourceKeys)
                    {
                        if (Application.Current.Resources.Contains(key))
                        {
                            var resource = Application.Current.Resources[key];
                            if (resource != null)
                            {
                                if (Resources.Contains(key))
                                {
                                    Resources[key] = resource;
                                }
                                else
                                {
                                    Resources.Add(key, resource);
                                }
                            }
                        }
                    }

                    // Forcer la mise à jour de l'interface
                    InvalidateVisual();
                }
            }
            catch (Exception ex)
            {
                // En cas d'erreur, définir des ressources de base
                SetFallbackResources();
                AddLog($"Erreur lors de l'application du thème: {ex.Message}", LogLevel.Warning, "Theme");
            }
        }

        private void SetFallbackResources()
        {
            try
            {
                // Définir des couleurs de base si les ressources de thème ne sont pas disponibles
                var fallbackResources = new Dictionary<string, object>
                {
                    { "PrimaryBrush", new SolidColorBrush(Color.FromRgb(45, 45, 48)) },
                    { "SecondaryBrush", new SolidColorBrush(Color.FromRgb(37, 37, 38)) },
                    { "AccentBrush", new SolidColorBrush(Color.FromRgb(0, 122, 204)) },
                    { "TextPrimaryBrush", new SolidColorBrush(Color.FromRgb(241, 241, 241)) },
                    { "TextSecondaryBrush", new SolidColorBrush(Color.FromRgb(153, 153, 153)) },
                    { "BorderBrush", new SolidColorBrush(Color.FromRgb(63, 63, 70)) }
                };

                foreach (var kvp in fallbackResources)
                {
                    if (Resources.Contains(kvp.Key))
                    {
                        Resources[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        Resources.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                // Si même les ressources de base échouent, on ne peut rien faire de plus
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la définition des ressources de base: {ex.Message}");
            }
        }

        public void AddLog(string message, LogLevel level = LogLevel.Info, string source = "App")
        {
            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Source = source
                };

                _allLogs.Add(logEntry);
                _logCount++;

                // Limiter le nombre de logs pour éviter les problèmes de mémoire
                if (_allLogs.Count > 1000)
                {
                    _allLogs.RemoveRange(0, 100); // Supprimer les 100 plus anciens
                }

                // Mettre à jour l'affichage si le niveau correspond au filtre
                if (ShouldShowLog(level))
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLogToTextBox(logEntry);
                        UpdateLogCount();
                        
                        // Défilement automatique si activé
                        if (AutoScrollCheckBox.IsChecked == true)
                        {
                            LogScrollViewer.ScrollToEnd();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // En cas d'erreur, essayer d'afficher au moins l'erreur
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogTextBox.AppendText($"\n[ERROR] Erreur lors de l'ajout du log: {ex.Message}");
                    });
                }
                catch
                {
                    // Si même ça échoue, on ne peut rien faire
                }
            }
        }

        private bool ShouldShowLog(LogLevel level)
        {
            return _currentFilter == LogLevel.All || level == _currentFilter;
        }

        private void AppendLogToTextBox(LogEntry logEntry)
        {
            try
            {
                string logText = logEntry.ToString();
                
                // Ajouter une couleur selon le niveau
                LogTextBox.AppendText(logText + "\n");
            }
            catch (Exception ex)
            {
                LogTextBox.AppendText($"\n[ERROR] Erreur d'affichage: {ex.Message}\n");
            }
        }

        private void UpdateLogCount()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LogCountText.Text = _logCount.ToString();
                    StatusText.Text = $"Logs affichés: {GetFilteredLogsCount()} / {_logCount}";
                });
            }
            catch
            {
                // Ignorer les erreurs de mise à jour de l'interface
            }
        }

        private int GetFilteredLogsCount()
        {
            if (_currentFilter == LogLevel.All)
                return _allLogs.Count;
            
            return _allLogs.Count(log => log.Level == _currentFilter);
        }

        private void RefreshDisplay()
        {
            try
            {
                LogTextBox.Clear();
                
                var filteredLogs = _currentFilter == LogLevel.All 
                    ? _allLogs 
                    : _allLogs.Where(log => log.Level == _currentFilter);

                foreach (var log in filteredLogs)
                {
                    AppendLogToTextBox(log);
                }

                UpdateLogCount();
                
                if (AutoScrollCheckBox.IsChecked == true)
                {
                    LogScrollViewer.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                LogTextBox.Text = $"Erreur lors du rafraîchissement: {ex.Message}";
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Êtes-vous sûr de vouloir effacer tous les logs?", 
                    "Confirmation", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _allLogs.Clear();
                    _logCount = 0;
                    LogTextBox.Clear();
                    UpdateLogCount();
                    
                    AddLog("Logs effacés par l'utilisateur", LogLevel.Info, "System");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'effacement: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Fichiers texte (*.txt)|*.txt|Fichiers log (*.log)|*.log|Tous les fichiers (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"iTunes_RPC_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var logsToSave = _currentFilter == LogLevel.All 
                        ? _allLogs 
                        : _allLogs.Where(log => log.Level == _currentFilter);

                    var content = string.Join("\n", logsToSave.Select(log => log.ToString()));
                    
                    File.WriteAllText(saveDialog.FileName, content);
                    
                    AddLog($"Logs sauvegardés dans: {saveDialog.FileName}", LogLevel.Info, "System");
                    MessageBox.Show("Logs sauvegardés avec succès!", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sauvegarde: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogLevel_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _currentFilter = (LogLevel)LogLevelComboBox.SelectedIndex;
                RefreshDisplay();
            }
            catch (Exception ex)
            {
                AddLog($"Erreur lors du changement de filtre: {ex.Message}", LogLevel.Error, "System");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Nettoyer les ressources si nécessaire
            base.OnClosed(e);
        }

        // Méthodes publiques pour l'intégration avec l'application principale
        public void LogInfo(string message, string source = "App")
        {
            AddLog(message, LogLevel.Info, source);
        }

        public void LogWarning(string message, string source = "App")
        {
            AddLog(message, LogLevel.Warning, source);
        }

        public void LogError(string message, string source = "App")
        {
            AddLog(message, LogLevel.Error, source);
        }
    }
}