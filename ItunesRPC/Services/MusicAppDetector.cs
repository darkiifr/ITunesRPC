using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ItunesRPC.Services
{
    public static class MusicAppDetector
    {
        // Dictionnaire des applications de musique supportées avec leurs identifiants de processus
        private static readonly Dictionary<string, List<string>> SupportedMusicApps = new()
        {
            {
                "Apple Music", new List<string>
                {
                    "Music",                    // Application Music de Windows
                    "AppleMusic",              // Apple Music du Microsoft Store
                    "Microsoft.ZuneMusic",     // Nom alternatif sur certains systèmes
                    "ZuneMusic"                // Autre variante possible
                }
            },
            {
                "iTunes", new List<string>
                {
                    "iTunes",                  // iTunes classique
                    "iTunesHelper"             // Helper iTunes
                }
            },
            {
                "Spotify", new List<string>
                {
                    "Spotify",                 // Spotify
                    "SpotifyWebHelper"         // Helper Spotify
                }
            },
            {
                "VLC", new List<string>
                {
                    "vlc"                      // VLC Media Player
                }
            },
            {
                "Windows Media Player", new List<string>
                {
                    "wmplayer",                // Windows Media Player
                    "MediaPlayer"              // Autre nom possible
                }
            }
        };

        /// <summary>
        /// Détecte toutes les applications de musique actuellement en cours d'exécution
        /// </summary>
        /// <returns>Liste des applications de musique détectées</returns>
        public static List<DetectedMusicApp> DetectRunningMusicApps()
        {
            var detectedApps = new List<DetectedMusicApp>();

            foreach (var app in SupportedMusicApps)
            {
                var appName = app.Key;
                var processNames = app.Value;

                foreach (var processName in processNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        if (processes.Length > 0)
                        {
                            var process = processes.First();
                            detectedApps.Add(new DetectedMusicApp
                            {
                                AppName = appName,
                                ProcessName = processName,
                                ProcessId = process.Id,
                                WindowTitle = GetProcessWindowTitle(process),
                                Priority = GetAppPriority(appName)
                            });
                            break; // Une seule instance par type d'application
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la détection de {processName}: {ex.Message}");
                    }
                }
            }

            // Trier par priorité (plus élevée en premier)
            return detectedApps.OrderByDescending(app => app.Priority).ToList();
        }

        /// <summary>
        /// Vérifie si une application de musique spécifique est en cours d'exécution
        /// </summary>
        /// <param name="appName">Nom de l'application à vérifier</param>
        /// <returns>True si l'application est en cours d'exécution</returns>
        public static bool IsAppRunning(string appName)
        {
            if (!SupportedMusicApps.ContainsKey(appName))
                return false;

            var processNames = SupportedMusicApps[appName];
            return processNames.Any(processName =>
            {
                try
                {
                    return Process.GetProcessesByName(processName).Length > 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Obtient l'application de musique avec la plus haute priorité actuellement en cours d'exécution
        /// </summary>
        /// <returns>Application de musique prioritaire ou null si aucune n'est détectée</returns>
        public static DetectedMusicApp? GetPriorityMusicApp()
        {
            var runningApps = DetectRunningMusicApps();
            return runningApps.FirstOrDefault();
        }

        /// <summary>
        /// Obtient la priorité d'une application (plus élevée = plus prioritaire)
        /// </summary>
        /// <param name="appName">Nom de l'application</param>
        /// <returns>Niveau de priorité</returns>
        private static int GetAppPriority(string appName)
        {
            return appName switch
            {
                "Apple Music" => 100,      // Priorité la plus élevée
                "iTunes" => 90,            // Deuxième priorité
                "Spotify" => 80,           // Troisième priorité
                "VLC" => 70,               // Quatrième priorité
                "Windows Media Player" => 60, // Priorité la plus basse
                _ => 50                    // Applications inconnues
            };
        }

        /// <summary>
        /// Obtient le titre de la fenêtre d'un processus
        /// </summary>
        /// <param name="process">Processus à examiner</param>
        /// <returns>Titre de la fenêtre ou chaîne vide</returns>
        private static string GetProcessWindowTitle(Process process)
        {
            try
            {
                return process.MainWindowTitle ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Obtient les identifiants de processus pour Windows Media Session
        /// </summary>
        /// <param name="appName">Nom de l'application</param>
        /// <returns>Liste des identifiants de processus possibles</returns>
        public static List<string> GetMediaSessionIdentifiers(string appName)
        {
            var identifiers = new List<string>();

            switch (appName)
            {
                case "Apple Music":
                    identifiers.AddRange(new[]
                    {
                        "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic",
                        "Microsoft.ZuneMusic",
                        "Music",
                        "AppleMusic",
                        "ZuneMusic",
                        "com.apple.music"
                    });
                    break;

                case "iTunes":
                    identifiers.AddRange(new[]
                    {
                        "iTunes",
                        "iTunes.exe",
                        "com.apple.itunes"
                    });
                    break;

                case "Spotify":
                    identifiers.AddRange(new[]
                    {
                        "Spotify.exe",
                        "SpotifyWebHelper.exe",
                        "Spotify",
                        "com.spotify.client"
                    });
                    break;

                case "VLC":
                    identifiers.AddRange(new[]
                    {
                        "vlc.exe",
                        "vlc",
                        "VideoLAN.VLCMediaPlayer"
                    });
                    break;

                case "Windows Media Player":
                    identifiers.AddRange(new[]
                    {
                        "wmplayer.exe",
                        "wmplayer",
                        "Microsoft.WindowsMediaPlayer"
                    });
                    break;

                default:
                    if (SupportedMusicApps.ContainsKey(appName))
                    {
                        identifiers.AddRange(SupportedMusicApps[appName]);
                    }
                    break;
            }

            return identifiers;
        }
    }

    /// <summary>
    /// Représente une application de musique détectée
    /// </summary>
    public class DetectedMusicApp
    {
        public string AppName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public int Priority { get; set; }

        public override string ToString()
        {
            return $"{AppName} (PID: {ProcessId}, Process: {ProcessName})";
        }
    }
}