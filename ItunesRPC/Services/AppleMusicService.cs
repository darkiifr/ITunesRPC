using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Timers;
using System.Threading.Tasks;
using System.Linq;
using ItunesRPC.Models;

namespace ItunesRPC.Services
{
    public class AppleMusicService
    {
        private Timer _timer;
        private TrackInfo? _lastTrackInfo;
        private int _consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 5;
        private DateTime _lastSuccessfulPoll = DateTime.Now;
        private readonly WindowsMediaSessionService _mediaSessionService;
        private bool _isInitialized = false;

        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;

        public AppleMusicService()
        {
            _timer = new Timer(2000); // 2 secondes
            _timer.Elapsed += Timer_Elapsed;
            _mediaSessionService = new WindowsMediaSessionService();
        }

        public async Task InitializeAsync()
        {
            try
            {
                _isInitialized = await _mediaSessionService.InitializeAsync();
                if (!_isInitialized)
                {
                    Console.WriteLine("Impossible d'initialiser Windows Media Session. Utilisation du mode de base.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation d'Apple Music Service: {ex.Message}");
                _isInitialized = false;
            }
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                Console.WriteLine("=== AppleMusicService: Vérification de la musique ===");

                TrackInfo? trackInfo = null;

                // Vérifier d'abord si Apple Music ou iTunes est en cours d'exécution
                var detectedApps = MusicAppDetector.DetectRunningMusicApps();
                var musicApps = detectedApps.Where(app => 
                    app.AppName.Equals("Apple Music", StringComparison.OrdinalIgnoreCase) ||
                    app.AppName.Equals("iTunes", StringComparison.OrdinalIgnoreCase)).ToList();

                Console.WriteLine($"Applications musicales détectées: {string.Join(", ", detectedApps.Select(a => a.AppName))}");
                Console.WriteLine($"Applications Apple/iTunes: {string.Join(", ", musicApps.Select(a => a.AppName))}");

                if (!musicApps.Any())
                {
                    if (_lastTrackInfo != null)
                    {
                        _lastTrackInfo = null;
                        TrackChanged?.Invoke(this, new TrackInfoEventArgs(null, "Apple Music"));
                    }
                    Console.WriteLine("Aucune application musicale détectée");
                    return;
                }

                // Essayer d'abord avec Windows Media Session API
                if (_isInitialized)
                {
                    try
                    {
                        foreach (var app in musicApps.OrderBy(a => a.Priority))
                        {
                            Console.WriteLine($"Tentative de récupération depuis {app.AppName}...");
                            trackInfo = await _mediaSessionService.GetCurrentTrackInfoAsync(app.AppName);
                            if (trackInfo != null)
                            {
                                Console.WriteLine($"Piste trouvée via {app.AppName}: {trackInfo.Name} - {trackInfo.Artist}");
                                break;
                            }
                        }
                        
                        // Si pas trouvé avec le nom détecté, essayer avec "Apple Music" directement
                        if (trackInfo == null)
                        {
                            Console.WriteLine("Tentative avec 'Apple Music' générique...");
                            trackInfo = await _mediaSessionService.GetCurrentTrackInfoAsync("Apple Music");
                        }
                        
                        // Essayer avec "iTunes" si Apple Music n'a pas fonctionné
                        if (trackInfo == null)
                        {
                            Console.WriteLine("Tentative avec 'iTunes' générique...");
                            trackInfo = await _mediaSessionService.GetCurrentTrackInfoAsync("iTunes");
                        }
                        
                        // Essayer avec "Music" générique
                        if (trackInfo == null)
                        {
                            Console.WriteLine("Tentative avec 'Music' générique...");
                            trackInfo = await _mediaSessionService.GetCurrentTrackInfoAsync("Music");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur Windows Media Session: {ex.Message}");
                    }
                }

                // Mode de fallback si Windows Media Session ne fonctionne pas
                if (trackInfo == null)
                {
                    Console.WriteLine("Utilisation du mode fallback...");
                    trackInfo = CreateFallbackTrackInfo(musicApps.First());
                }

                // Vérifier les changements et notifier
                if (trackInfo != null && (_lastTrackInfo == null || !AreTracksEqual(_lastTrackInfo, trackInfo)))
                {
                    var previousIsPlaying = _lastTrackInfo?.IsPlaying ?? false;
                    _lastTrackInfo = trackInfo;
                    
                    Console.WriteLine($"Nouvelle piste détectée: {trackInfo.Name} - {trackInfo.Artist}");
                    TrackChanged?.Invoke(this, new TrackInfoEventArgs(trackInfo, "Apple Music"));
                    
                    // Notifier le changement d'état de lecture si nécessaire
                    if (previousIsPlaying != trackInfo.IsPlaying)
                    {
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(trackInfo.IsPlaying, "Apple Music"));
                    }
                }
                else if (trackInfo != null)
                {
                    Console.WriteLine($"Piste inchangée: {trackInfo.Name} - {trackInfo.Artist}");
                }

                _consecutiveErrors = 0;
                _lastSuccessfulPoll = DateTime.Now;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                Console.WriteLine($"Erreur dans AppleMusicService: {ex.Message}");

                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    var timeSinceLastSuccess = DateTime.Now - _lastSuccessfulPoll;
                    if (timeSinceLastSuccess.TotalMinutes > 5)
                    {
                        _timer.Interval = 10000; // Attendre 10 secondes avant de réessayer
                    }
                }
            }
        }

        private TrackInfo CreateFallbackTrackInfo(DetectedMusicApp app)
        {
            Console.WriteLine($"Création d'une piste fallback pour {app.AppName}");
            
            // Si on a un titre de fenêtre, essayer de l'analyser
            if (!string.IsNullOrEmpty(app.WindowTitle) && 
                !app.WindowTitle.Equals(app.AppName, StringComparison.OrdinalIgnoreCase))
            {
                var parts = app.WindowTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return new TrackInfo
                    {
                        Artist = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Album = "",
                        IsPlaying = true,
                        ArtworkPath = SaveDefaultArtwork(),
                        StartTime = DateTime.Now,
                        EndTime = DateTime.Now.AddMinutes(3)
                    };
                }
            }

            return new TrackInfo
            {
                Name = $"{app.AppName} détecté",
                Artist = "Application en cours d'exécution",
                Album = "",
                IsPlaying = true,
                ArtworkPath = SaveDefaultArtwork(),
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(3)
            };
        }

        public bool IsAppleMusicRunning()
        {
            try
            {
                // Vérifier pour l'application Music de Windows
                var processes = Process.GetProcessesByName("Music");
                if (processes.Length > 0)
                {
                    return true;
                }

                // Vérifier pour Microsoft.ZuneMusic (nom du processus sur certains systèmes)
                processes = Process.GetProcessesByName("Microsoft.ZuneMusic");
                if (processes.Length > 0)
                {
                    return true;
                }

                // Vérifier pour Apple Music (si installé via le Microsoft Store)
                processes = Process.GetProcessesByName("AppleMusic");
                if (processes.Length > 0)
                {
                    return true;
                }

                // Vérifier pour iTunes (au cas où)
                processes = Process.GetProcessesByName("iTunes");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool AreTracksEqual(TrackInfo track1, TrackInfo track2)
        {
            return track1.Name == track2.Name &&
                   track1.Artist == track2.Artist &&
                   track1.Album == track2.Album &&
                   track1.IsPlaying == track2.IsPlaying;
        }

        private string SaveDefaultArtwork()
        {
            try
            {
                string tempPath = Path.GetTempPath();
                string fileName = Path.Combine(tempPath, "apple_music_default.png");

                if (!File.Exists(fileName))
                {
                    // Créer une image par défaut avec le logo Apple Music
                    using (var bitmap = new Bitmap(300, 300))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // Fond dégradé
                        using (var brush = new LinearGradientBrush(
                            new Rectangle(0, 0, 300, 300),
                            Color.FromArgb(255, 45, 45, 45),
                            Color.FromArgb(255, 25, 25, 25),
                            LinearGradientMode.Vertical))
                        {
                            graphics.FillRectangle(brush, 0, 0, 300, 300);
                        }
                        
                        // Texte Apple Music
                        using (var brush = new SolidBrush(Color.White))
                        using (var font = new Font("Segoe UI", 18, FontStyle.Bold))
                        {
                            var text = "Apple Music";
                            var textSize = graphics.MeasureString(text, font);
                            var x = (bitmap.Width - textSize.Width) / 2;
                            var y = (bitmap.Height - textSize.Height) / 2;
                            graphics.DrawString(text, font, brush, x, y);
                        }
                        
                        // Icône musicale simple
                        using (var pen = new Pen(Color.FromArgb(255, 255, 59, 48), 3))
                        {
                            graphics.DrawEllipse(pen, 120, 80, 60, 60);
                            graphics.DrawEllipse(pen, 130, 90, 40, 40);
                        }
                        
                        bitmap.Save(fileName, ImageFormat.Png);
                    }
                }

                return fileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _mediaSessionService?.Dispose();
        }
    }
}