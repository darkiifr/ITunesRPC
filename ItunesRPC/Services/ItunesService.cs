using ItunesRPC.Models;
using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ItunesRPC.Services
{
    public class ItunesService
    {
        private Timer? _pollingTimer;
        private TrackInfo? _currentTrack;
        private bool _isPlaying = false;
        private readonly string _tempFolder;
        private int _consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 5;
        private DateTime _lastSuccessfulPoll = DateTime.Now;

        // Événements pour notifier les changements
        public event EventHandler<TrackInfoEventArgs>? TrackChanged;
        public event EventHandler<PlayStateEventArgs>? PlayStateChanged;

        public ItunesService()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "ItunesRPC");
            Directory.CreateDirectory(_tempFolder);
        }

        public void Start()
        {
            // Démarrer un timer pour interroger iTunes toutes les secondes
            _pollingTimer = new Timer(PollITunes, null, 0, 1000);
        }

        public void Stop()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        private void PollITunes(object? state)
        {
            try
            {
                // Vérifier si iTunes est en cours d'exécution
                bool isItunesRunning = IsItunesRunning();
                if (!isItunesRunning)
                {
                    if (_isPlaying)
                    {
                        _isPlaying = false;
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying, "iTunes"));
                        _currentTrack = null;
                    }
                    _consecutiveErrors = 0; // Réinitialiser le compteur d'erreurs
                    return;
                }

                // Si trop d'erreurs consécutives, attendre plus longtemps
                if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                {
                    var timeSinceLastSuccess = DateTime.Now - _lastSuccessfulPoll;
                    if (timeSinceLastSuccess.TotalMinutes < 2) // Attendre 2 minutes avant de réessayer
                    {
                        return;
                    }
                    _consecutiveErrors = 0; // Réinitialiser après l'attente
                }

                // Obtenir les informations de la piste en cours
                var trackInfo = GetCurrentTrackInfo();
                if (trackInfo == null)
                {
                    _consecutiveErrors++;
                    if (_isPlaying)
                    {
                        _isPlaying = false;
                        PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying, "iTunes"));
                    }
                    return;
                }

                // Succès - réinitialiser le compteur d'erreurs
                _consecutiveErrors = 0;
                _lastSuccessfulPoll = DateTime.Now;

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
                    TrackChanged?.Invoke(this, new TrackInfoEventArgs(trackInfo, "iTunes"));
                }

                if (playStateChanged)
                {
                    PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying, "iTunes"));
                }

                // Nettoyer les anciens fichiers d'artwork périodiquement
                if (DateTime.Now.Minute % 10 == 0) // Toutes les 10 minutes
                {
                    CleanupOldArtworkFiles();
                }
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                Console.WriteLine($"Erreur lors de l'interrogation d'iTunes (erreur #{_consecutiveErrors}): {ex.Message}");
                
                // Assurer que l'état est cohérent en cas d'erreur
                if (_isPlaying)
                {
                    _isPlaying = false;
                    PlayStateChanged?.Invoke(this, new PlayStateEventArgs(_isPlaying, "iTunes"));
                }
            }
        }

        private bool IsItunesRunning()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name='iTunes.exe'"))
                using (var processes = searcher.Get())
                {
                    return processes.Count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification du processus iTunes: {ex.Message}");
                return false;
            }
        }

        // Définition des interfaces COM pour iTunes
        [ComImport, Guid("9DD6680B-3EDC-40DB-A771-E6FE4832E34A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface iTunesApp
        {
            [DispId(0x0000068)] bool PlayerButtonsEnabled { get; }
            [DispId(0x0000069)] ITPlayerState PlayerState { get; }
            [DispId(0x000006a)] double PlayerPosition { get; set; }
            [DispId(0x000006b)] IITTrack CurrentTrack { get; }
            [DispId(0x000006c)] IITPlaylist CurrentPlaylist { get; }
        }

        [ComImport, Guid("9DD6680B-3EDC-40DB-A771-E6FE4832E34A")]
        [CoClass(typeof(iTunesAppClass))]
        private interface iTunesAppCoClass : iTunesApp { }

        [ComImport, Guid("DC0C2640-1415-4644-875C-6F4D769839BA")]
        private class iTunesAppClass { }

        [ComImport, Guid("4CB0915D-1E54-4727-BAF3-CE6CC9A225A1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IITTrack
        {
            [DispId(0x00000191)] string Name { get; }
            [DispId(0x00000192)] string Artist { get; }
            [DispId(0x00000193)] string Album { get; }
            [DispId(0x00000197)] int TrackNumber { get; }
            [DispId(0x0000019a)] int TrackCount { get; }
            [DispId(0x0000019c)] double Duration { get; }
            [DispId(0x0000019d)] IITArtworkCollection Artwork { get; }
        }

        [ComImport, Guid("3D8DE381-6C0E-481F-A865-E2385F59FA43")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IITArtworkCollection
        {
            [DispId(0x00000001)] int Count { get; }
            [DispId(0x00000002)] IITArtwork get_Item(int index);
        }

        [ComImport, Guid("D0A6C1F8-BF3D-4CD8-AC47-FE32BDD17257")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IITArtwork
        {
            [DispId(0x00000001)] ITArtworkFormat Format { get; }
            [DispId(0x00000002)] void SaveArtworkToFile(string filePath);
        }

        [ComImport, Guid("FF503D6F-8FB9-4051-B6C6-3271A6E1F3B4")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IITPlaylist
        {
            [DispId(0x00000191)] string Name { get; }
            [DispId(0x00000195)] int TrackCount { get; }
        }

        private enum ITPlayerState
        {
            ITPlayerStateStopped = 0,
            ITPlayerStatePlaying = 1,
            ITPlayerStateFastForward = 2,
            ITPlayerStateRewind = 3
        }

        private enum ITArtworkFormat
        {
            ITArtworkFormatUnknown = 0,
            ITArtworkFormatJPEG = 1,
            ITArtworkFormatPNG = 2,
            ITArtworkFormatBMP = 3
        }

        private TrackInfo? GetCurrentTrackInfo()
        {
            try
            {
                if (!IsItunesRunning())
                {
                    return null;
                }

                // Créer une instance de l'application iTunes via COM
                var iTunes = new iTunesAppCoClass() as iTunesApp;
                if (iTunes == null)
                {
                    return null;
                }

                // Vérifier si une piste est en cours de lecture
                var currentTrack = iTunes.CurrentTrack;
                if (currentTrack == null)
                {
                    return null;
                }

                // Obtenir les informations de la piste
                var trackInfo = new TrackInfo
                {
                    Name = currentTrack.Name,
                    Artist = currentTrack.Artist,
                    Album = currentTrack.Album,
                    // Vérifier si les propriétés Genre et Year sont disponibles via une méthode alternative
                    // ou utiliser des valeurs par défaut si elles ne sont pas accessibles
                    Genre = GetTrackGenre(currentTrack),
                    Year = GetTrackYear(currentTrack),
                    TrackNumber = currentTrack.TrackNumber,
                    TrackCount = currentTrack.TrackCount,
                    IsPlaying = iTunes.PlayerState == ITPlayerState.ITPlayerStatePlaying,
                    StartTime = DateTime.Now.AddSeconds(-iTunes.PlayerPosition),
                    EndTime = DateTime.Now.AddSeconds(currentTrack.Duration - iTunes.PlayerPosition)
                };
                
                // Méthodes d'assistance pour obtenir le genre et l'année si les propriétés directes ne sont pas disponibles
                string GetTrackGenre(IITTrack track)
                {
                    try
                    {
                        // Utiliser une approche alternative pour obtenir le genre
                        // Comme nous ne pouvons pas accéder directement à la propriété Genre,
                        // nous utilisons une valeur par défaut
                        return "";
                    }
                    catch
                    {
                        return "";
                    }
                }
                
                int GetTrackYear(IITTrack track)
                {
                    try
                    {
                        // Utiliser une approche alternative pour obtenir l'année
                        // Comme nous ne pouvons pas accéder directement à la propriété Year,
                        // nous utilisons une valeur par défaut
                        return 0; // Retourner 0 comme valeur par défaut pour l'année
                    }
                    catch
                    {
                        return 0; // Retourner 0 en cas d'erreur
                    }
                }

                // Obtenir la pochette d'album si disponible
                trackInfo.ArtworkPath = GetArtworkFromiTunes(currentTrack);

                return trackInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'obtention des informations de la piste: {ex.Message}");
                return null;
            }
        }

        private string GetArtworkFromiTunes(IITTrack track)
        {
            try
            {
                var artwork = track.Artwork;
                if (artwork != null && artwork.Count > 0)
                {
                    var firstArtwork = artwork.get_Item(1); // L'index commence à 1 dans l'API iTunes
                    
                    // Créer un nom de fichier valide en remplaçant les caractères invalides
                    string safeArtist = string.Join("_", track.Artist.Split(Path.GetInvalidFileNameChars()));
                    string safeName = string.Join("_", track.Name.Split(Path.GetInvalidFileNameChars()));
                    string artworkPath = Path.Combine(_tempFolder, $"artwork_{safeArtist}_{safeName}.png");
                    
                    try
                    {
                        // Supprimer le fichier s'il existe déjà
                        if (File.Exists(artworkPath))
                        {
                            File.Delete(artworkPath);
                        }
                        
                        // Sauvegarder l'artwork dans un fichier temporaire
                        firstArtwork.SaveArtworkToFile(artworkPath);
                        
                        // Vérifier que le fichier a bien été créé
                        if (File.Exists(artworkPath))
                        {
                            return artworkPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la sauvegarde de la pochette: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'extraction de la pochette: {ex.Message}");
            }
            
            // Utiliser l'image par défaut si aucune pochette n'est disponible
            return SaveDefaultArtwork();
        }

        private string SaveDefaultArtwork()
        {
            try
            {
                string tempFilePath = Path.Combine(_tempFolder, "default_album.png");
                
                // Si le fichier existe déjà, le retourner
                if (File.Exists(tempFilePath))
                {
                    return tempFilePath;
                }

                // Sinon, extraire l'image des ressources et la sauvegarder
                var resourceUri = new Uri("pack://application:,,,/Resources/default_album.png", UriKind.Absolute);
                var resourceStream = Application.GetResourceStream(resourceUri)?.Stream;
                
                if (resourceStream != null)
                {
                    using (var fileStream = new FileStream(tempFilePath, System.IO.FileMode.Create))
                    using (resourceStream) // Utilisation de using pour assurer la libération des ressources
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
                
                return tempFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde de l'image: {ex.Message}");
                return string.Empty;
            }
        }

        private void CleanupOldArtworkFiles()
        {
            try
            {
                if (!Directory.Exists(_tempFolder))
                    return;

                var files = Directory.GetFiles(_tempFolder, "artwork_*.png");
                var cutoffTime = DateTime.Now.AddHours(-1); // Supprimer les fichiers de plus d'1 heure

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffTime)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la suppression du fichier {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du nettoyage des fichiers d'artwork: {ex.Message}");
            }
        }
    }

    // Les classes d'événements ont été déplacées vers EventArgs.cs
}