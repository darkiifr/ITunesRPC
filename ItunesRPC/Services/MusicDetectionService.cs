using ItunesRPC.Models;
using System;
using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using System.Linq;

namespace ItunesRPC.Services
{
    public class MusicDetectionService
    {
        private readonly ItunesService _itunesService;
        private readonly AppleMusicService _appleMusicService;
        private readonly EnhancedAppleMusicService _enhancedAppleMusicService;
        private readonly DiscordRpcService _discordRpcService;
        private Timer? _healthCheckTimer;
        private const int HEALTH_CHECK_INTERVAL = 10000; // 10 secondes

        // Propriétés pour suivre l'état des services
        public bool IsItunesActive { get; private set; }
        public bool IsAppleMusicActive { get; private set; }
        public string CurrentActiveService { get; private set; } = "Aucun";
        
        // Événements pour notifier les changements
        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;
        public event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        public MusicDetectionService(ItunesService itunesService, AppleMusicService appleMusicService, DiscordRpcService discordRpcService)
        {
            _itunesService = itunesService ?? throw new ArgumentNullException(nameof(itunesService));
            _appleMusicService = appleMusicService ?? throw new ArgumentNullException(nameof(appleMusicService));
            _enhancedAppleMusicService = new EnhancedAppleMusicService();
            _discordRpcService = discordRpcService ?? throw new ArgumentNullException(nameof(discordRpcService));

            // S'abonner aux événements des services
            _itunesService.TrackChanged += OnItunesTrackChanged;
            _itunesService.PlayStateChanged += OnItunesPlayStateChanged;
            
            _appleMusicService.TrackChanged += OnAppleMusicTrackChanged;
            _appleMusicService.PlayStateChanged += OnAppleMusicPlayStateChanged;
            
            // S'abonner aux événements du service amélioré
            _enhancedAppleMusicService.TrackChanged += OnEnhancedAppleMusicTrackChanged;
            _enhancedAppleMusicService.PlayStateChanged += OnEnhancedAppleMusicPlayStateChanged;

            // Initialiser le timer de vérification de santé
            InitializeHealthCheck();
        }

        private void InitializeHealthCheck()
        {
            _healthCheckTimer = new Timer(HEALTH_CHECK_INTERVAL);
            _healthCheckTimer.Elapsed += PerformHealthCheck;
            _healthCheckTimer.AutoReset = true;
        }

        private void PerformHealthCheck(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // Utiliser le détecteur d'applications pour une détection plus précise
                var detectedApps = MusicAppDetector.DetectRunningMusicApps();
                var priorityApp = MusicAppDetector.GetPriorityMusicApp();
                
                bool itunesRunning = detectedApps.Any(app => 
                    app.AppName.Equals("iTunes", StringComparison.OrdinalIgnoreCase));
                bool appleMusicRunning = detectedApps.Any(app => 
                    app.AppName.Equals("Apple Music", StringComparison.OrdinalIgnoreCase) ||
                    app.AppName.Equals("Music", StringComparison.OrdinalIgnoreCase));

                // Mettre à jour les états
                bool itunesStateChanged = IsItunesActive != itunesRunning;
                bool appleMusicStateChanged = IsAppleMusicActive != appleMusicRunning;

                IsItunesActive = itunesRunning;
                IsAppleMusicActive = appleMusicRunning;

                // Déterminer le service actif prioritaire
                string previousActiveService = CurrentActiveService;
                if (appleMusicRunning)
                {
                    CurrentActiveService = "Apple Music";
                }
                else if (itunesRunning)
                {
                    CurrentActiveService = "iTunes";
                }
                else
                {
                    CurrentActiveService = "Aucun";
                }

                // Notifier les changements d'état
                if (itunesStateChanged || appleMusicStateChanged || previousActiveService != CurrentActiveService)
                {
                    ServiceStatusChanged?.Invoke(this, new ServiceStatusEventArgs
                    {
                        ItunesActive = IsItunesActive,
                        AppleMusicActive = IsAppleMusicActive,
                        ActiveService = CurrentActiveService
                    });
                }

                // Si aucun service n'est actif, effacer la présence Discord
                if (!itunesRunning && !appleMusicRunning)
                {
                    _discordRpcService.ClearPresence();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification de santé: {ex.Message}");
            }
        }

        private bool IsProcessRunning(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification du processus {processName}: {ex.Message}");
                return false;
            }
        }
        
        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("Initialisation du service de détection de musique...");
                
                // Initialiser les services de musique
                // ItunesService n'a pas besoin d'initialisation async
                await _appleMusicService.InitializeAsync();
                // Le service amélioré n'a pas besoin d'initialisation async
                
                Console.WriteLine("Service de détection de musique initialisé avec succès.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation du service de détection: {ex.Message}");
                throw;
            }
        }

        public void Start()
        {
            try
            {
                Console.WriteLine("Démarrage du service de détection de musique...");
                
                // Démarrer les services individuels
                _itunesService.Start();
                _appleMusicService.Start();
                _enhancedAppleMusicService.Start();
                
                // Démarrer la vérification de santé
                _healthCheckTimer?.Start();
                
                Console.WriteLine("Service de détection de musique démarré avec succès.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du démarrage du service de détection: {ex.Message}");
                throw;
            }
        }
        
        public void Stop()
        {
            try
            {
                Console.WriteLine("Arrêt du service de détection de musique...");
                
                // Arrêter le timer de vérification de santé
                _healthCheckTimer?.Stop();
                
                // Arrêter les services individuels
                _itunesService.Stop();
                _appleMusicService.Stop();
                _enhancedAppleMusicService.Stop();
                
                // Effacer la présence Discord
                _discordRpcService.ClearPresence();
                
                // Réinitialiser les états
                IsItunesActive = false;
                IsAppleMusicActive = false;
                CurrentActiveService = "Aucun";
                
                Console.WriteLine("Service de détection de musique arrêté.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'arrêt du service de détection: {ex.Message}");
            }
        }
        
        public void Restart()
        {
            // Arrêter puis redémarrer les services
            Stop();
            Start();
        }
        
        private void OnEnhancedAppleMusicTrackChanged(object? sender, TrackInfoEventArgs e)
        {
            try
            {
                // Le service amélioré a la priorité la plus élevée pour Apple Music
                if (IsAppleMusicActive)
                {
                    if (e.TrackInfo != null)
                    {
                        Console.WriteLine($"Nouvelle piste Apple Music (service amélioré): {e.TrackInfo.Name} - {e.TrackInfo.Artist}");
                    }
                    else
                    {
                        Console.WriteLine("Apple Music (service amélioré): Aucune piste en cours");
                    }
                    
                    _discordRpcService.UpdatePresence(e.TrackInfo, "Apple Music");
                    TrackChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement de piste Apple Music amélioré: {ex.Message}");
            }
        }

        private void OnEnhancedAppleMusicPlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            try
            {
                if (IsAppleMusicActive)
                {
                    Console.WriteLine($"État de lecture Apple Music amélioré changé: {(e.IsPlaying ? "En lecture" : "En pause")}");
                    
                    if (!e.IsPlaying)
                    {
                        _discordRpcService.ClearPresence();
                    }
                    else if (_discordRpcService.CurrentTrack != null)
                    {
                        _discordRpcService.UpdatePresence(_discordRpcService.CurrentTrack, "Apple Music");
                    }
                    
                    PlayStateChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement d'état Apple Music amélioré: {ex.Message}");
            }
        }

        private void OnAppleMusicTrackChanged(object? sender, TrackInfoEventArgs e)
        {
            try
            {
                // Apple Music a la priorité sur iTunes
                if (IsAppleMusicActive)
                {
                    if (e.TrackInfo != null)
                    {
                        Console.WriteLine($"Nouvelle piste Apple Music: {e.TrackInfo.Name} - {e.TrackInfo.Artist}");
                    }
                    else
                    {
                        Console.WriteLine("Apple Music: Aucune piste en cours");
                    }
                    
                    _discordRpcService.UpdatePresence(e.TrackInfo, "Apple Music");
                    TrackChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement de piste Apple Music: {ex.Message}");
            }
        }

        private void OnAppleMusicPlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            try
            {
                if (IsAppleMusicActive)
                {
                    Console.WriteLine($"État de lecture Apple Music changé: {(e.IsPlaying ? "En lecture" : "En pause")}");
                    
                    if (!e.IsPlaying)
                    {
                        _discordRpcService.ClearPresence();
                    }
                    else if (_discordRpcService.CurrentTrack != null)
                    {
                        _discordRpcService.UpdatePresence(_discordRpcService.CurrentTrack, "Apple Music");
                    }
                    
                    PlayStateChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement d'état Apple Music: {ex.Message}");
            }
        }

        private void OnItunesTrackChanged(object? sender, TrackInfoEventArgs e)
        {
            try
            {
                // iTunes n'est utilisé que si Apple Music n'est pas actif
                if (IsItunesActive && !IsAppleMusicActive)
                {
                    if (e.TrackInfo != null)
                    {
                        Console.WriteLine($"Nouvelle piste iTunes: {e.TrackInfo.Name} - {e.TrackInfo.Artist}");
                    }
                    else
                    {
                        Console.WriteLine("iTunes: Aucune piste en cours");
                    }
                    
                    _discordRpcService.UpdatePresence(e.TrackInfo, "iTunes");
                    TrackChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement de piste iTunes: {ex.Message}");
            }
        }

        private void OnItunesPlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            try
            {
                // iTunes n'est utilisé que si Apple Music n'est pas actif
                if (IsItunesActive && !IsAppleMusicActive)
                {
                    Console.WriteLine($"État de lecture iTunes changé: {(e.IsPlaying ? "En lecture" : "En pause")}");
                    
                    if (!e.IsPlaying)
                    {
                        _discordRpcService.ClearPresence();
                    }
                    else if (_discordRpcService.CurrentTrack != null)
                    {
                        _discordRpcService.UpdatePresence(_discordRpcService.CurrentTrack, "iTunes");
                    }
                    
                    PlayStateChanged?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du traitement du changement d'état iTunes: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                _healthCheckTimer?.Dispose();
                _enhancedAppleMusicService?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la libération des ressources: {ex.Message}");
            }
        }
    }

    // Classe d'événement pour le statut des services
    public class ServiceStatusEventArgs : EventArgs
    {
        public bool ItunesActive { get; set; }
        public bool AppleMusicActive { get; set; }
        public string ActiveService { get; set; } = string.Empty;
    }
}