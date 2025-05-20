using ItunesRPC.Models;
using System;
using System.IO;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace ItunesRPC.Services
{
    public class AppleMusicService
    {
        private Timer? _pollingTimer;
        private TrackInfo? _currentTrack;
        private bool _isPlaying = false;
        private readonly string _tempFolder;

        // Événements pour notifier les changements
        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;

        public AppleMusicService()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "ItunesRPC", "AppleMusic");
            Directory.CreateDirectory(_tempFolder);
        }

        public void Start()
        {
            // Démarrer un timer pour interroger Apple Music toutes les secondes
            _pollingTimer = new Timer(PollAppleMusic, null, 0, 1000);
        }

        public void Stop()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        private void PollAppleMusic(object? state)
        {
            try
            {
                // Vérifier si Apple Music est en cours d'exécution
                bool isAppleMusicRunning = IsAppleMusicRunning();
                if (!isAppleMusicRunning)
                {
                    if (_isPlaying)
                    {
                        _isPlaying = false;
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying));
                        _currentTrack = null; // Réinitialiser la piste actuelle quand Apple Music est fermé
                    }
                    return;
                }

                // Obtenir les informations de la piste en cours via WMI
                var trackInfo = GetCurrentTrackInfo();
                if (trackInfo == null)
                {
                    if (_isPlaying)
                    {
                        _isPlaying = false;
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying));
                    }
                    return;
                }

                // Vérifier si la piste a changé
                bool trackChanged = _currentTrack == null ||
                                   _currentTrack.Name != trackInfo.Name ||
                                   _currentTrack.Artist != trackInfo.Artist ||
                                   _currentTrack.Album != trackInfo.Album;

                // Vérifier si l'état de lecture a changé
                bool playStateChanged = _isPlaying != trackInfo.IsPlaying;

                // Mettre à jour les informations actuelles
                _currentTrack = trackInfo;
                _isPlaying = trackInfo.IsPlaying;

                // Déclencher les événements si nécessaire
                if (trackChanged && _isPlaying)
                {
                    TrackChanged?.Invoke(this, new TrackInfoEventArgs(trackInfo));
                }

                if (playStateChanged)
                {
                    PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'interrogation d'Apple Music: {ex.Message}");
                // Assurer que l'état est cohérent en cas d'erreur
                if (_isPlaying)
                {
                    _isPlaying = false;
                    PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying));
                }
            }
        }

        private bool IsAppleMusicRunning()
        {
            try
            {
                // Rechercher le processus Apple Music
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'AppleMusic.exe' OR Name = 'Music.UI.exe'"))
                {
                    return searcher.Get().Count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification d'Apple Music: {ex.Message}");
                return false;
            }
        }

        private TrackInfo? GetCurrentTrackInfo()
        {
            try
            {
                // Utiliser WMI pour obtenir les informations de la piste en cours
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'AppleMusic.exe' OR Name = 'Music.UI.exe'"))
                {
                    foreach (var process in searcher.Get())
                    {
                        // Obtenir les informations via l'API Windows Media Session
                        // Note: Cette implémentation est simplifiée et nécessiterait l'utilisation de l'API Windows Media Session
                        // pour obtenir les informations réelles de la piste en cours dans Apple Music
                        
                        // Pour l'instant, nous simulons les données
                        var trackInfo = GetAppleMusicTrackInfoViaWindowsMediaSession();
                        if (trackInfo != null)
                        {
                            return trackInfo;
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'obtention des informations de piste Apple Music: {ex.Message}");
                return null;
            }
        }

        private TrackInfo? GetAppleMusicTrackInfoViaWindowsMediaSession()
        {
            // Cette méthode devrait utiliser l'API Windows Media Session pour obtenir les informations réelles
            // Pour l'instant, nous utilisons une approche simplifiée en vérifiant les informations de lecture système
            try
            {
                // Utiliser la classe SystemMediaTransportControls pour obtenir les informations de lecture
                // Note: Ceci est une implémentation simplifiée
                
                // Vérifier si Apple Music est en train de jouer de la musique via WMI
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'AppleMusic.exe' OR Name = 'Music.UI.exe'"))
                {
                    if (searcher.Get().Count > 0)
                    {
                        // Utiliser SMTC pour obtenir les informations de lecture
                        // Pour l'instant, nous simulons les données
                        return SimulateAppleMusicTrackInfo();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'obtention des informations via Windows Media Session: {ex.Message}");
                return null;
            }
        }

        private TrackInfo SimulateAppleMusicTrackInfo()
        {
            // Cette méthode est temporaire et devrait être remplacée par une implémentation réelle
            // qui obtient les informations de piste d'Apple Music
            
            // Dans une implémentation réelle, nous utiliserions l'API Windows Media Session
            // ou une autre méthode pour obtenir les informations de la piste en cours
            
            // Pour l'instant, nous retournons null pour indiquer qu'aucune piste n'est en cours de lecture
            return null; // Retour null intentionnel, géré par le type nullable TrackInfo?
        }

        // Méthode pour extraire et sauvegarder la pochette d'album
        private string? ExtractAndSaveArtwork(byte[]? artworkData)
        {
            if (artworkData == null || artworkData.Length == 0)
                return null; // Retour null explicitement autorisé par le type de retour nullable (string?)

            try
            {
                string fileName = Path.Combine(_tempFolder, $"artwork_{DateTime.Now.Ticks}.jpg");
                File.WriteAllBytes(fileName, artworkData);
                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'extraction de la pochette: {ex.Message}");
                return null;
            }
        }
    }

    // Les classes d'événements ont été déplacées vers EventArgs.cs
}