using DiscordRPC;
using DiscordRPC.Logging;
using ItunesRPC.Models;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace ItunesRPC.Services
{
    public class DiscordRpcService
    {
        private DiscordRpcClient? _client;
        private readonly string _applicationId = "1369005012486852649"; // ID d'application Discord configuré
        private Timer? _reconnectTimer;
        private bool _isReconnecting = false;
        private const int RECONNECT_INTERVAL = 30000; // 30 secondes
        
        // Propriété pour stocker la piste en cours
        public TrackInfo? CurrentTrack { get; private set; }
        
        // Événement pour notifier les changements de statut de connexion
        public event EventHandler<DiscordConnectionStatusEventArgs>? ConnectionStatusChanged;

        public DiscordRpcService()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                // Nettoyer les ressources existantes si nécessaire
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
                
                _client = new DiscordRpcClient(_applicationId)
                {
                    Logger = new ConsoleLogger() { Level = LogLevel.Warning }
                };

                _client.OnReady += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC prêt pour l'utilisateur {e.User.Username}");
                    StopReconnectTimer(); // Arrêter le timer de reconnexion si la connexion est établie
                };

                _client.OnPresenceUpdate += (sender, e) =>
                {
                    Console.WriteLine("Présence mise à jour");
                };
                
                _client.OnConnectionFailed += (sender, e) =>
                {
                    Console.WriteLine($"Échec de connexion à Discord: {e.FailedPipe}");
                    StartReconnectTimer(); // Démarrer le timer de reconnexion en cas d'échec
                };
                
                _client.OnError += (sender, e) =>
                {
                    Console.WriteLine($"Erreur Discord RPC: {e.Message}");
                };

                _client.Initialize();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation de Discord RPC: {ex.Message}");
                StartReconnectTimer(); // Démarrer le timer de reconnexion en cas d'erreur
            }
        }

        public void UpdatePresence(TrackInfo trackInfo, string source = "iTunes")
        {
            try
            {
                if (_client == null || !_client.IsInitialized)
                {
                    Initialize();
                }

                if (_client != null && _client.IsInitialized)
                {
                    // Stocker la piste en cours
                    CurrentTrack = trackInfo;
                    
                    var presence = new RichPresence()
                    {
                        Details = trackInfo.Name,
                        State = $"par {trackInfo.Artist}",
                        Assets = new Assets()
                        {
                            LargeImageKey = source.ToLower().Contains("apple") ? "apple_music_logo" : "itunes_logo", // Clé d'image définie dans le portail développeur Discord
                            LargeImageText = trackInfo.Album,
                            SmallImageKey = "play_icon",
                            SmallImageText = $"Via {source}"
                        },
                        Timestamps = new Timestamps()
                        {
                            Start = trackInfo.StartTime,
                            End = trackInfo.EndTime
                        }
                    };
                    
                    // Notifier que la connexion est établie
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(true));

                    _client.SetPresence(presence);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la mise à jour de la présence Discord: {ex.Message}");
                // Tenter de réinitialiser la connexion
                Task.Run(() => ReconnectAsync());
            }
        }

        public void ClearPresence()
        {
            try
            {
                if (_client != null && _client.IsInitialized)
                {
                    _client.ClearPresence();
                    
                    // Réinitialiser la piste en cours
                    if (CurrentTrack != null)
                    {
                        CurrentTrack.IsPlaying = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'effacement de la présence: {ex.Message}");
            }
        }
        
        public void Reconnect()
        {
            try
            {
                // Fermer la connexion existante
                _client?.Dispose();
                _client = null;
                
                // Réinitialiser
                Initialize();
                
                // Notifier que la tentative de reconnexion a été lancée
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Tentative de reconnexion..."));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la reconnexion: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            try
            {
                StopReconnectTimer();
                
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'arrêt de Discord RPC: {ex.Message}");
            }
        }
        
        private void StartReconnectTimer()
        {
            if (_reconnectTimer == null)
            {
                _reconnectTimer = new Timer(RECONNECT_INTERVAL);
                _reconnectTimer.Elapsed += async (sender, e) => await ReconnectAsync();
            }
            
            if (!_reconnectTimer.Enabled)
            {
                _reconnectTimer.Start();
            }
        }
        
        private void StopReconnectTimer()
        {
            if (_reconnectTimer != null && _reconnectTimer.Enabled)
            {
                _reconnectTimer.Stop();
            }
        }
        
        private async Task ReconnectAsync()
        {
            if (_isReconnecting) return;
            
            try
            {
                _isReconnecting = true;
                Console.WriteLine("Tentative de reconnexion à Discord...");
                
                // Attendre un peu avant de tenter la reconnexion
                await Task.Delay(1000);
                
                Initialize();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la tentative de reconnexion: {ex.Message}");
            }
            finally
            {
                _isReconnecting = false;
            }
        }
    }
}