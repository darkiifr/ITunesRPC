using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ItunesRPC.Services
{
    public class LoggingService
    {
        private static LoggingService _instance;
        private static readonly object _lock = new object();
        private DebugConsoleWindow _consoleWindow;
        private readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private readonly object _bufferLock = new object();
        private bool _isEnabled = true;

        public static LoggingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new LoggingService();
                    }
                }
                return _instance;
            }
        }

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }

        public class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public string Source { get; set; }
            public Exception Exception { get; set; }
        }

        private LoggingService()
        {
            // Constructeur privé pour le singleton
        }

        public void SetConsoleWindow(DebugConsoleWindow consoleWindow)
        {
            _consoleWindow = consoleWindow;
            
            // Envoyer les logs en buffer vers la console
            lock (_bufferLock)
            {
                foreach (var log in _logBuffer)
                {
                    SendToConsole(log);
                }
                _logBuffer.Clear();
            }
        }

        public void EnableLogging(bool enabled)
        {
            _isEnabled = enabled;
        }

        public void LogInfo(string message, string source = "App")
        {
            Log(message, LogLevel.Info, source);
        }

        public void LogWarning(string message, string source = "App")
        {
            Log(message, LogLevel.Warning, source);
        }

        public void LogError(string message, string source = "App", Exception exception = null)
        {
            Log(message, LogLevel.Error, source, exception);
        }

        public void LogDebug(string message, string source = "App")
        {
            Log(message, LogLevel.Debug, source);
        }

        private void Log(string message, LogLevel level, string source, Exception exception = null)
        {
            if (!_isEnabled)
                return;

            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Source = source,
                    Exception = exception
                };

                // Si la console n'est pas encore disponible, mettre en buffer
                if (_consoleWindow == null)
                {
                    lock (_bufferLock)
                    {
                        _logBuffer.Add(logEntry);
                        
                        // Limiter la taille du buffer
                        if (_logBuffer.Count > 100)
                        {
                            _logBuffer.RemoveAt(0);
                        }
                    }
                }
                else
                {
                    SendToConsole(logEntry);
                }

                // Optionnel: écrire aussi dans un fichier de log
                _ = Task.Run(() => WriteToFile(logEntry));
            }
            catch
            {
                // En cas d'erreur de logging, ne pas planter l'application
            }
        }

        private void SendToConsole(LogEntry logEntry)
        {
            try
            {
                if (_consoleWindow != null)
                {
                    var consoleLevel = logEntry.Level switch
                    {
                        LogLevel.Info => DebugConsoleWindow.LogLevel.Info,
                        LogLevel.Warning => DebugConsoleWindow.LogLevel.Warning,
                        LogLevel.Error => DebugConsoleWindow.LogLevel.Error,
                        LogLevel.Debug => DebugConsoleWindow.LogLevel.Info,
                        _ => DebugConsoleWindow.LogLevel.Info
                    };

                    string message = logEntry.Message;
                    if (logEntry.Exception != null)
                    {
                        message += $" - Exception: {logEntry.Exception.Message}";
                    }

                    _consoleWindow.AddLog(message, consoleLevel, logEntry.Source);
                }
            }
            catch
            {
                // Ignorer les erreurs d'envoi vers la console
            }
        }

        private async Task WriteToFile(LogEntry logEntry)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iTunes RPC", "Logs");
                Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
                
                string logLine = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{logEntry.Level}] [{logEntry.Source}] {logEntry.Message}";
                
                if (logEntry.Exception != null)
                {
                    logLine += $"\n    Exception: {logEntry.Exception}";
                }
                
                logLine += Environment.NewLine;

                await File.AppendAllTextAsync(logFile, logLine);

                // Nettoyer les anciens fichiers de log (garder seulement les 7 derniers jours)
                CleanOldLogFiles(logDir);
            }
            catch
            {
                // Ignorer les erreurs d'écriture de fichier
            }
        }

        private void CleanOldLogFiles(string logDir)
        {
            try
            {
                var files = Directory.GetFiles(logDir, "log_*.txt");
                var cutoffDate = DateTime.Now.AddDays(-7);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs de nettoyage
            }
        }

        // Méthodes de convenance pour les services existants
        public void LogServiceStart(string serviceName)
        {
            LogInfo($"Initialisation du service {serviceName}...", serviceName);
        }

        public void LogServiceStop(string serviceName)
        {
            LogInfo($"Arrêt du service {serviceName}...", serviceName);
        }

        public void LogServiceError(string serviceName, string error, Exception exception = null)
        {
            LogError($"Erreur dans le service {serviceName}: {error}", serviceName, exception);
        }

        public void LogConnectionAttempt(string service, string target)
        {
            LogInfo($"Tentative de connexion à {target}", service);
        }

        public void LogConnectionSuccess(string service, string target)
        {
            LogInfo($"Connexion réussie à {target}", service);
        }

        public void LogConnectionFailure(string service, string target, string reason)
        {
            LogWarning($"Échec de connexion à {target}: {reason}", service);
        }

        public void LogTrackChange(string trackName, string artist, string source)
        {
            LogInfo($"Nouvelle piste: {trackName} - {artist}", source);
        }

        public void LogPlaybackStateChange(string state, string source)
        {
            LogInfo($"État de lecture: {state}", source);
        }
    }
}