using ItunesRPC.Models;
using System;
using System.Threading.Tasks;

namespace ItunesRPC.Services
{
    public class MusicDetectionService
    {
        private readonly ItunesService _itunesService;
        private readonly AppleMusicService _appleMusicService;
        private readonly DiscordRpcService _discordService;
        
        private bool _isItunesActive = false;
        private bool _isAppleMusicActive = false;
        
        // Événements pour notifier les changements
        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;
        
        public MusicDetectionService(DiscordRpcService discordService)
        {
            _itunesService = new ItunesService();
            _appleMusicService = new AppleMusicService();
            _discordService = discordService;
            
            // S'abonner aux événements des services de musique
            _itunesService.TrackChanged += ItunesService_TrackChanged;
            _itunesService.PlayStateChanged += ItunesService_PlayStateChanged;
            
            _appleMusicService.TrackChanged += AppleMusicService_TrackChanged;
            _appleMusicService.PlayStateChanged += AppleMusicService_PlayStateChanged;
        }
        
        public void Start()
        {
            // Démarrer les deux services
            _itunesService.Start();
            _appleMusicService.Start();
        }
        
        public void Stop()
        {
            // Arrêter les deux services
            _itunesService.Stop();
            _appleMusicService.Stop();
        }
        
        public void Restart()
        {
            // Arrêter puis redémarrer les services
            Stop();
            Start();
            
            // Réinitialiser les états
            _isItunesActive = false;
            _isAppleMusicActive = false;
        }
        
        private void ItunesService_TrackChanged(object? sender, TrackInfoEventArgs e)
        {
            // Si iTunes est actif, mettre à jour Discord et notifier les abonnés
            if (!_isAppleMusicActive)
            {
                _isItunesActive = true;
                _discordService.UpdatePresence(e.TrackInfo, "iTunes");
                TrackChanged?.Invoke(this, new TrackInfoEventArgs(e.TrackInfo, "iTunes"));
            }
        }
        
        private void ItunesService_PlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            // Mettre à jour l'état de lecture d'iTunes
            if (e.IsPlaying)
            {
                _isItunesActive = true;
                // Si Apple Music était actif, le désactiver
                if (_isAppleMusicActive)
                {
                    _isAppleMusicActive = false;
                }
            }
            else
            {
                _isItunesActive = false;
            }
            
            // Notifier les abonnés du changement d'état
            PlayStateChanged?.Invoke(this, new PlayStateEventArgs(e.IsPlaying, _isItunesActive ? "iTunes" : null));
        }
        
        private void AppleMusicService_TrackChanged(object? sender, TrackInfoEventArgs e)
        {
            // Si Apple Music est actif et iTunes n'est pas actif, mettre à jour Discord et notifier les abonnés
            if (!_isItunesActive)
            {
                _isAppleMusicActive = true;
                _discordService.UpdatePresence(e.TrackInfo, "Apple Music");
                TrackChanged?.Invoke(this, new TrackInfoEventArgs(e.TrackInfo, "Apple Music"));
            }
        }
        
        private void AppleMusicService_PlayStateChanged(object? sender, PlayStateEventArgs e)
        {
            // Mettre à jour l'état de lecture d'Apple Music
            if (e.IsPlaying)
            {
                _isAppleMusicActive = true;
                // Si iTunes était actif, le désactiver
                if (_isItunesActive)
                {
                    _isItunesActive = false;
                }
            }
            else
            {
                _isAppleMusicActive = false;
            }
            
            // Notifier les abonnés du changement d'état
            PlayStateChanged?.Invoke(this, new PlayStateEventArgs(e.IsPlaying, _isAppleMusicActive ? "Apple Music" : null));
        }
    }
    
    // Classes d'événements étendues pour inclure la source de musique
    public class TrackInfoEventArgs : EventArgs
    {
        public TrackInfo TrackInfo { get; }
        public string? Source { get; }

        public TrackInfoEventArgs(TrackInfo trackInfo, string? source = null)
        {
            TrackInfo = trackInfo;
            Source = source;
        }
    }

    public class PlayStateEventArgs : EventArgs
    {
        public bool IsPlaying { get; }
        public string? Source { get; }

        public PlayStateEventArgs(bool isPlaying, string? source = null)
        {
            IsPlaying = isPlaying;
            Source = source;
        }
    }
}