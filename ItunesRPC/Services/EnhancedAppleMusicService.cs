using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ItunesRPC.Models;
using Windows.Media.Control;

namespace ItunesRPC.Services
{
    public class EnhancedAppleMusicService : IDisposable
    {
        private readonly Timer _timer;
        private readonly WindowsMediaSessionService _mediaSessionService;
        private bool _isInitialized = false;
        private TrackInfo? _lastTrackInfo;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

        // Import des API Windows pour une meilleure détection
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;

        public EnhancedAppleMusicService()
        {
            _mediaSessionService = new WindowsMediaSessionService();
            _timer = new Timer(2000); // Vérifier toutes les 2 secondes
            _timer.Elapsed += Timer_Elapsed;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                Console.WriteLine("Initialisation du service Apple Music amélioré...");
                
                // Initialiser le service de session média Windows
                _isInitialized = await _mediaSessionService.InitializeAsync();
                
                if (_isInitialized)
                {
                    // Initialiser le gestionnaire de sessions pour une détection directe
                    try
                    {
                        _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                        Console.WriteLine("Gestionnaire de sessions média initialisé avec succès");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Impossible d'initialiser le gestionnaire de sessions: {ex.Message}");
                    }
                }

                Console.WriteLine($"Service Apple Music amélioré initialisé: {_isInitialized}");
                return _isInitialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation du service Apple Music amélioré: {ex.Message}");
                _isInitialized = false;
                return false;
            }
        }

        public void Start()
        {
            if (_isInitialized)
            {
                _timer.Start();
                Console.WriteLine("Service Apple Music amélioré démarré");
            }
        }

        public void Stop()
        {
            _timer.Stop();
            Console.WriteLine("Service Apple Music amélioré arrêté");
        }

        private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (!IsAppleMusicRunning())
                {
                    if (_lastTrackInfo != null)
                    {
                        _lastTrackInfo = null;
                        TrackChanged?.Invoke(this, new TrackInfoEventArgs(null, "Apple Music"));
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(false, "Apple Music"));
                    }
                    return;
                }

                var trackInfo = await GetCurrentTrackInfoAsync();
                
                if (trackInfo != null && !AreTracksEqual(trackInfo, _lastTrackInfo))
                {
                    var previousIsPlaying = _lastTrackInfo?.IsPlaying ?? false;
                    _lastTrackInfo = trackInfo;
                    TrackChanged?.Invoke(this, new TrackInfoEventArgs(trackInfo, "Apple Music"));
                    
                    // Notifier le changement d'état de lecture si nécessaire
                    if (previousIsPlaying != trackInfo.IsPlaying)
                    {
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(trackInfo.IsPlaying, "Apple Music"));
                    }
                    
                    Console.WriteLine($"Nouvelle piste détectée: {trackInfo.Name} - {trackInfo.Artist}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur dans le timer Apple Music amélioré: {ex.Message}");
            }
        }

        public async Task<TrackInfo?> GetCurrentTrackInfoAsync()
        {
            try
            {
                // Méthode 1: Utiliser l'API Windows Media Session directement
                var trackInfo = await GetTrackFromMediaSession();
                if (trackInfo != null)
                {
                    Console.WriteLine("Piste détectée via API Windows Media Session directe");
                    return trackInfo;
                }

                // Méthode 2: Utiliser le service existant
                trackInfo = await _mediaSessionService.GetCurrentTrackInfoAsync("Apple Music");
                if (trackInfo != null)
                {
                    Console.WriteLine("Piste détectée via service Windows Media Session");
                    return trackInfo;
                }

                // Méthode 3: Détection par titre de fenêtre améliorée
                trackInfo = GetTrackFromWindowTitle();
                if (trackInfo != null)
                {
                    Console.WriteLine("Piste détectée via titre de fenêtre");
                    return trackInfo;
                }

                Console.WriteLine("Aucune piste détectée avec les méthodes disponibles");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des informations de piste: {ex.Message}");
                return null;
            }
        }

        private async Task<TrackInfo?> GetTrackFromMediaSession()
        {
            try
            {
                if (_sessionManager == null)
                    return null;

                var sessions = _sessionManager.GetSessions();
                
                foreach (var session in sessions)
                {
                    var sourceId = session.SourceAppUserModelId?.ToLower() ?? "";
                    
                    // Rechercher spécifiquement Apple Music / Music app
                    if (sourceId.Contains("zunemusic") || sourceId.Contains("music") || 
                        sourceId.Contains("apple"))
                    {
                        try
                        {
                            var mediaProperties = await session.TryGetMediaPropertiesAsync();
                            var playbackInfo = session.GetPlaybackInfo();
                            var timelineProperties = session.GetTimelineProperties();

                            if (mediaProperties != null && !string.IsNullOrEmpty(mediaProperties.Title))
                            {
                                var trackInfo = new TrackInfo
                                {
                                    Name = mediaProperties.Title,
                                    Artist = mediaProperties.Artist ?? "Artiste inconnu",
                                    Album = mediaProperties.AlbumTitle ?? "",
                                    IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                                    StartTime = DateTime.Now - (timelineProperties?.Position ?? TimeSpan.Zero),
                                    EndTime = DateTime.Now - (timelineProperties?.Position ?? TimeSpan.Zero) + 
                                             (timelineProperties?.EndTime - timelineProperties?.StartTime ?? TimeSpan.FromMinutes(3))
                                };

                                // Essayer de récupérer l'artwork
                                if (mediaProperties.Thumbnail != null)
                                {
                                    try
                                    {
                                        trackInfo.ArtworkPath = await SaveArtworkFromThumbnail(mediaProperties.Thumbnail);
                                    }
                                    catch
                                    {
                                        trackInfo.ArtworkPath = GetDefaultArtworkPath();
                                    }
                                }
                                else
                                {
                                    trackInfo.ArtworkPath = GetDefaultArtworkPath();
                                }

                                return trackInfo;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erreur lors du traitement de la session {sourceId}: {ex.Message}");
                            continue;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération via session média: {ex.Message}");
                return null;
            }
        }

        private TrackInfo? GetTrackFromWindowTitle()
        {
            try
            {
                var processes = GetAppleMusicProcesses();
                
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                        {
                            var titleLength = GetWindowTextLength(process.MainWindowHandle);
                            if (titleLength > 0)
                            {
                                var title = new StringBuilder(titleLength + 1);
                                GetWindowText(process.MainWindowHandle, title, title.Capacity);
                                
                                var windowTitle = title.ToString();
                                Console.WriteLine($"Titre de fenêtre Apple Music: {windowTitle}");
                                
                                var trackInfo = ParseAppleMusicWindowTitle(windowTitle);
                                if (trackInfo != null)
                                {
                                    return trackInfo;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors du traitement du processus {process.ProcessName}: {ex.Message}");
                        continue;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la détection par titre de fenêtre: {ex.Message}");
                return null;
            }
        }

        private TrackInfo? ParseAppleMusicWindowTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            // Ignorer les titres génériques
            if (title.Equals("Apple Music", StringComparison.OrdinalIgnoreCase) ||
                title.Equals("Music", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Microsoft Store"))
            {
                return null;
            }

            // Format typique: "Artist - Song" ou "Song - Artist"
            var separators = new[] { " - ", " – ", " — " };
            
            foreach (var separator in separators)
            {
                var parts = title.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return new TrackInfo
                    {
                        Artist = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Album = "",
                        IsPlaying = true,
                        StartTime = DateTime.Now,
                        EndTime = DateTime.Now.AddMinutes(3),
                        ArtworkPath = GetDefaultArtworkPath()
                    };
                }
            }

            // Si aucun séparateur trouvé, utiliser le titre complet comme nom de piste
            if (title.Length > 5) // Éviter les titres trop courts
            {
                return new TrackInfo
                {
                    Name = title.Trim(),
                    Artist = "Apple Music",
                    Album = "",
                    IsPlaying = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(3),
                    ArtworkPath = GetDefaultArtworkPath()
                };
            }

            return null;
        }

        public bool IsAppleMusicRunning()
        {
            try
            {
                var processes = GetAppleMusicProcesses();
                return processes.Any();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification d'Apple Music: {ex.Message}");
                return false;
            }
        }

        private List<Process> GetAppleMusicProcesses()
        {
            var processes = new List<Process>();
            
            try
            {
                // Rechercher différents noms de processus pour Apple Music
                var processNames = new[] { "Music", "AppleMusic", "Microsoft.ZuneMusic" };
                
                foreach (var processName in processNames)
                {
                    try
                    {
                        var foundProcesses = Process.GetProcessesByName(processName);
                        processes.AddRange(foundProcesses);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la recherche du processus {processName}: {ex.Message}");
                    }
                }

                // Rechercher aussi par titre de fenêtre
                var allProcesses = Process.GetProcesses();
                foreach (var process in allProcesses)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainWindowTitle) &&
                            (process.MainWindowTitle.Contains("Apple Music") || 
                             process.MainWindowTitle.Contains("Music")))
                        {
                            if (!processes.Any(p => p.Id == process.Id))
                            {
                                processes.Add(process);
                            }
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès aux processus
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la recherche des processus Apple Music: {ex.Message}");
            }

            return processes;
        }

        private async Task<string> SaveArtworkFromThumbnail(Windows.Storage.Streams.IRandomAccessStreamReference thumbnailRef)
        {
            try
            {
                using var stream = await thumbnailRef.OpenReadAsync();
                using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
                
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);

                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ItunesRPC");
                System.IO.Directory.CreateDirectory(tempPath);
                
                var artworkPath = System.IO.Path.Combine(tempPath, $"artwork_{DateTime.Now.Ticks}.jpg");
                await System.IO.File.WriteAllBytesAsync(artworkPath, bytes);
                
                return artworkPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde de l'artwork: {ex.Message}");
                return GetDefaultArtworkPath();
            }
        }

        private string GetDefaultArtworkPath()
        {
            var defaultPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default_album.png");
            return System.IO.File.Exists(defaultPath) ? defaultPath : "";
        }

        private bool AreTracksEqual(TrackInfo? track1, TrackInfo? track2)
        {
            if (track1 == null && track2 == null) return true;
            if (track1 == null || track2 == null) return false;
            
            return track1.Name == track2.Name && 
                   track1.Artist == track2.Artist && 
                   track1.IsPlaying == track2.IsPlaying;
        }

        public void Dispose()
        {
            try
            {
                Stop();
                _timer?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la libération des ressources du service Apple Music amélioré: {ex.Message}");
            }
        }
    }
}