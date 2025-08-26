using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ItunesRPC.Models;
using ItunesRPC.Services;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ItunesRPC.Services
{
    public class WindowsMediaSessionService : IDisposable
    {
        private bool _isInitialized = false;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

        // Import des API Windows pour récupérer les titres de fenêtres
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Initialiser le gestionnaire de sessions média Windows
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                
                if (_sessionManager != null)
                {
                    LoggingService.Instance.LogInfo("WindowsMediaSessionService initialisé avec succès avec l'API Windows Media Control", "WindowsMediaSessionService");
                    _isInitialized = true;
                    return true;
                }
                else
                {
                    LoggingService.Instance.LogError("Impossible d'obtenir le gestionnaire de sessions média", "WindowsMediaSessionService");
                    _isInitialized = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'initialisation de WindowsMediaSessionService: {ex.Message}", "WindowsMediaSessionService", ex);
                _isInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Obtient les informations de la piste actuellement en cours de lecture
        /// </summary>
        /// <returns>Informations de la piste ou null si aucune piste n'est trouvée</returns>
        public async Task<TrackInfo?> GetCurrentTrackAsync()
        {
            try
            {
                var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var currentSession = sessionManager?.GetCurrentSession();

                if (currentSession == null)
                {
                    // Essayer de trouver une session active parmi toutes les sessions
                    var sessions = sessionManager?.GetSessions();
                    if (sessions != null)
                    {
                        foreach (var session in sessions)
                        {
                            try
                            {
                                var sessionPlaybackInfo = session.GetPlaybackInfo();
                                var sourceApp = session.SourceAppUserModelId ?? "";
                                
                                // Vérifier si c'est une application de musique et si elle joue
                                if (IsMusicApp(sourceApp) && 
                                    sessionPlaybackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                                {
                                    currentSession = session;
                                    break;
                                }
                            }
                            catch
                            {
                                // Ignorer les erreurs pour cette session et continuer
                                continue;
                            }
                        }
                        
                        // Si aucune session en cours de lecture, prendre la première session de musique
                        if (currentSession == null)
                        {
                            foreach (var session in sessions)
                            {
                                try
                                {
                                    var sourceApp = session.SourceAppUserModelId ?? "";
                                    if (IsMusicApp(sourceApp))
                                    {
                                        currentSession = session;
                                        break;
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                    }
                }

                if (currentSession == null)
                 {
                     return GetTrackFromWindowTitleAsync();
                 }

                 var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
                 if (mediaProperties == null)
                 {
                     return GetTrackFromWindowTitleAsync();
                 }

                var playbackInfo = currentSession.GetPlaybackInfo();
                var isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var trackInfo = new TrackInfo
                 {
                     Name = !string.IsNullOrWhiteSpace(mediaProperties.Title) ? mediaProperties.Title : "Titre inconnu",
                     Artist = !string.IsNullOrWhiteSpace(mediaProperties.Artist) ? mediaProperties.Artist : "Artiste inconnu",
                     Album = !string.IsNullOrWhiteSpace(mediaProperties.AlbumTitle) ? mediaProperties.AlbumTitle : "Album inconnu",
                     IsPlaying = isPlaying
                 };

                // Essayer d'obtenir l'artwork
                try
                {
                    if (mediaProperties.Thumbnail != null)
                    {
                        var artworkPath = await SaveArtworkFromStream(mediaProperties.Thumbnail);
                        if (!string.IsNullOrEmpty(artworkPath))
                        {
                            trackInfo.ArtworkPath = artworkPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Erreur lors de la sauvegarde de l'artwork: {ex.Message}", "WindowsMediaSessionService", ex);
                }

                return trackInfo;
            }
            catch (Exception ex)
             {
                 LoggingService.Instance.LogError($"Erreur lors de l'obtention des informations de la piste: {ex.Message}", "WindowsMediaSessionService", ex);
                 return GetTrackFromWindowTitleAsync();
             }
        }

        public async Task<TrackInfo?> GetCurrentTrackInfoAsync(string targetAppName = "")
        {
            try
            {
                if (!_isInitialized || _sessionManager == null)
                {
                    if (!await InitializeAsync())
                        return null;
                }

                // Obtenir toutes les sessions actives
                var sessions = _sessionManager!.GetSessions();
                
                foreach (var session in sessions)
                {
                    try
                    {
                        var sourceAppUserModelId = session.SourceAppUserModelId;
                        LoggingService.Instance.LogInfo($"Session trouvée: {sourceAppUserModelId}", "WindowsMediaSessionService");
                        
                        // Vérifier si c'est une application de musique supportée
                        if (IsMusicApp(sourceAppUserModelId, targetAppName))
                        {
                            var mediaProperties = await session.TryGetMediaPropertiesAsync();
                            if (mediaProperties != null)
                            {
                                var playbackInfo = session.GetPlaybackInfo();
                                var timelineProperties = session.GetTimelineProperties();
                                
                                var trackInfo = new TrackInfo
                                {
                                    Name = mediaProperties.Title ?? "Titre inconnu",
                                    Artist = mediaProperties.Artist ?? "Artiste inconnu",
                                    Album = mediaProperties.AlbumTitle ?? "",
                                    IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                                    StartTime = DateTime.Now - (timelineProperties?.Position ?? TimeSpan.Zero),
                                    EndTime = DateTime.Now - (timelineProperties?.Position ?? TimeSpan.Zero) + (timelineProperties?.EndTime - timelineProperties?.StartTime ?? TimeSpan.FromMinutes(3))
                                };

                                // Essayer de récupérer l'artwork
                                if (mediaProperties.Thumbnail != null)
                                {
                                    try
                                    {
                                        var artworkPath = await SaveArtworkFromStream(mediaProperties.Thumbnail);
                                        trackInfo.ArtworkPath = artworkPath;
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggingService.Instance.LogError($"Erreur lors de la sauvegarde de l'artwork: {ex.Message}", "WindowsMediaSessionService", ex);
                                        trackInfo.ArtworkPath = GetDefaultArtworkPath();
                                    }
                                }
                                else
                                {
                                    trackInfo.ArtworkPath = GetDefaultArtworkPath();
                                }

                                // Piste détectée via Windows Media API
                                return trackInfo;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Erreur lors du traitement de la session
                        continue;
                    }
                }

                // Fallback vers l'ancienne méthode si aucune session n'est trouvée
                // Aucune session média trouvée, utilisation du fallback
                return GetTrackInfoFromProcesses(targetAppName);
            }
            catch (Exception ex)
            {
                // Erreur lors de la récupération des informations de piste
                return GetTrackInfoFromProcesses(targetAppName);
            }
        }

        private bool IsMusicApp(string sourceAppUserModelId, string targetAppName = "")
        {
            if (string.IsNullOrEmpty(sourceAppUserModelId))
                return false;

            var musicAppIdentifiers = new[]
            {
                "Microsoft.ZuneMusic", // Apple Music / Groove Music
                "iTunes",
                "Spotify",
                "vlc",
                "AIMP",
                "foobar2000",
                "MusicBee",
                "Winamp"
            };

            var lowerSourceId = sourceAppUserModelId.ToLower();
            
            // Si un nom d'application cible est spécifié, vérifier la correspondance
            if (!string.IsNullOrEmpty(targetAppName))
            {
                var lowerTargetName = targetAppName.ToLower();
                if (lowerTargetName.Contains("apple") || lowerTargetName.Contains("music"))
                {
                    return lowerSourceId.Contains("zunemusic") || lowerSourceId.Contains("music");
                }
                if (lowerTargetName.Contains("itunes"))
                {
                    return lowerSourceId.Contains("itunes");
                }
                if (lowerTargetName.Contains("spotify"))
                {
                    return lowerSourceId.Contains("spotify");
                }
            }

            // Vérification générale
            return musicAppIdentifiers.Any(identifier => lowerSourceId.Contains(identifier.ToLower()));
        }

        private async Task<string> SaveArtworkFromStream(IRandomAccessStreamReference thumbnailRef)
        {
            try
            {
                using var stream = await thumbnailRef.OpenReadAsync();
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);

                var tempPath = Path.Combine(Path.GetTempPath(), "ItunesRPC");
                Directory.CreateDirectory(tempPath);
                
                var artworkPath = Path.Combine(tempPath, $"artwork_{DateTime.Now.Ticks}.jpg");
                await File.WriteAllBytesAsync(artworkPath, bytes);
                
                return artworkPath;
            }
            catch (Exception ex)
            {
                // Erreur lors de la sauvegarde de l'artwork
                return GetDefaultArtworkPath();
            }
        }

        private string GetDefaultArtworkPath()
        {
            var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default_album.png");
            return File.Exists(defaultPath) ? defaultPath : "";
        }

        private TrackInfo? GetTrackInfoFromProcesses(string targetAppName)
        {
            try
            {
                var musicApps = MusicAppDetector.DetectRunningMusicApps();
                
                foreach (var app in musicApps.OrderBy(a => a.Priority))
                {
                    if (!string.IsNullOrEmpty(targetAppName) && 
                        !app.AppName.Equals(targetAppName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var trackInfo = ExtractTrackInfoFromWindowTitle(app);
                    if (trackInfo != null)
                        return trackInfo;
                }

                return null;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de l'extraction des informations de piste: {ex.Message}", "WindowsMediaSessionService", ex);
                return null;
            }
        }

        private TrackInfo? ExtractTrackInfoFromWindowTitle(DetectedMusicApp app)
        {
            try
            {
                if (string.IsNullOrEmpty(app.WindowTitle))
                    return null;

                var title = app.WindowTitle;
                LoggingService.Instance.LogInfo($"Titre de fenêtre détecté: {title}", "WindowsMediaSessionService");

                // Patterns pour différentes applications
                switch (app.ProcessName.ToLower())
                {
                    case "music":
                    case "applemusic":
                        return ParseAppleMusicTitle(title);
                    
                    case "itunes":
                        return ParseITunesTitle(title);
                    
                    case "spotify":
                        return ParseSpotifyTitle(title);
                    
                    default:
                        return ParseGenericTitle(title);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors du parsing du titre: {ex.Message}", "WindowsMediaSessionService", ex);
                return null;
            }
        }

        private TrackInfo? ParseAppleMusicTitle(string title)
        {
            // Apple Music format: "Artist - Song" ou "Song - Artist" ou juste "Apple Music"
            if (title.Equals("Apple Music", StringComparison.OrdinalIgnoreCase) ||
                title.Equals("Music", StringComparison.OrdinalIgnoreCase))
            {
                return new TrackInfo
                {
                    Name = "Musique en cours",
                    Artist = "Apple Music",
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            // Essayer de parser "Artist - Song"
            var parts = title.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return new TrackInfo
                {
                    Artist = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            return null;
        }

        private TrackInfo? ParseITunesTitle(string title)
        {
            // iTunes format: "Artist - Song" ou "iTunes"
            if (title.Equals("iTunes", StringComparison.OrdinalIgnoreCase))
            {
                return new TrackInfo
                {
                    Name = "Musique en cours",
                    Artist = "iTunes",
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            var parts = title.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return new TrackInfo
                {
                    Artist = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            return null;
        }

        private TrackInfo? ParseSpotifyTitle(string title)
        {
            // Spotify format: "Spotify" ou "Artist - Song"
            if (title.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                return new TrackInfo
                {
                    Name = "Musique en cours",
                    Artist = "Spotify",
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            var parts = title.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return new TrackInfo
                {
                    Artist = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            return null;
        }

        private TrackInfo? ParseGenericTitle(string title)
        {
            // Format générique
            var parts = title.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return new TrackInfo
                {
                    Artist = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3)
                };
            }

            return new TrackInfo
            {
                Name = title,
                Artist = "Application musicale",
                Album = "",
                IsPlaying = true,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(3)
            };
        }

        public async Task<string?> SaveArtworkAsync(object thumbnail, string trackName, string artistName)
        {
            try
            {
                await Task.Delay(10);
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde de l'artwork: {ex.Message}", "WindowsMediaSessionService", ex);
                return null;
            }
        }

        private async Task<string?> SaveArtworkAsync(IRandomAccessStreamReference thumbnailRef)
        {
            try
            {
                return await SaveArtworkFromStream(thumbnailRef);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la sauvegarde de l'artwork: {ex.Message}", "WindowsMediaSessionService", ex);
                return GetDefaultArtworkPath();
            }
        }

        private TrackInfo? GetTrackFromWindowTitleAsync()
         {
             try
             {
                 return GetTrackInfoFromProcesses("");
             }
             catch (Exception ex)
             {
                 LoggingService.Instance.LogError($"Erreur lors de l'extraction des informations de piste: {ex.Message}", "WindowsMediaSessionService", ex);
                 return null;
             }
         }

        public async Task<bool> IsMediaPlayingAsync(string appName = "")
        {
            try
            {
                if (!_isInitialized)
                    return false;

                await Task.Delay(10);
                
                // Vérifier si une application musicale est en cours d'exécution
                var musicApps = MusicAppDetector.DetectRunningMusicApps();
                if (!string.IsNullOrEmpty(appName))
                {
                    return musicApps.Any(app => app.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase));
                }
                
                return musicApps.Any();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la vérification de l'état de lecture: {ex.Message}", "WindowsMediaSessionService", ex);
                return false;
            }
        }

        public async Task<List<string>> GetActiveMediaAppsAsync()
        {
            try
            {
                if (!_isInitialized)
                    return new List<string>();

                await Task.Delay(10);
                
                var musicApps = MusicAppDetector.DetectRunningMusicApps();
                return musicApps.Select(app => app.AppName).ToList();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la récupération des applications actives: {ex.Message}", "WindowsMediaSessionService", ex);
                return new List<string>();
            }
        }

        public void Dispose()
        {
            try
            {
                _sessionManager = null;
                _isInitialized = false;
                LoggingService.Instance.LogInfo("WindowsMediaSessionService libéré", "WindowsMediaSessionService");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError($"Erreur lors de la libération de WindowsMediaSessionService: {ex.Message}", "WindowsMediaSessionService", ex);
            }
        }
    }
}