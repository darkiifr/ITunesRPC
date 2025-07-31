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
            try
            {
                Console.WriteLine("Initialisation du constructeur DiscordRpcService...");
                // Initialiser de manière asynchrone sans bloquer le constructeur
                Task.Run(async () => 
                {
                    try
                    {
                        await InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de l'initialisation asynchrone de Discord RPC: {ex.Message}");
                        ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur d'initialisation: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur dans le constructeur DiscordRpcService: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur de constructeur: {ex.Message}"));
            }
        }

        private void Initialize()
        {
            // Méthode de compatibilité qui appelle la version asynchrone
            Task.Run(async () => await InitializeAsync());
        }
        
        private async Task InitializeAsync()
        {
            try
            {
                // Nettoyer les ressources existantes si nécessaire
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
                
                // Attendre un court instant pour s'assurer que les ressources sont libérées
                await Task.Delay(100);
                
                _client = new DiscordRpcClient(_applicationId)
                {
                    Logger = new ConsoleLogger() { Level = LogLevel.Warning },
                    SkipIdenticalPresence = true
                };

                _client.OnReady += (sender, e) =>
                {
                    Console.WriteLine($"Discord RPC prêt pour l'utilisateur {e.User.Username}");
                    StopReconnectTimer(); // Arrêter le timer de reconnexion si la connexion est établie
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(true, "Connecté"));
                };

                _client.OnPresenceUpdate += (sender, e) =>
                {
                    Console.WriteLine("Présence mise à jour");
                };
                
                _client.OnConnectionFailed += (sender, e) =>
                {
                    Console.WriteLine($"Échec de connexion à Discord: {e.FailedPipe}");
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Échec de connexion: {e.FailedPipe}"));
                    StartReconnectTimer(); // Démarrer le timer de reconnexion en cas d'échec
                };
                
                _client.OnError += (sender, e) =>
                {
                    Console.WriteLine($"Erreur Discord RPC: {e.Message}");
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur: {e.Message}"));
                };

                // Initialiser le client Discord RPC
                _client.Initialize();
                
                // Vérifier si l'initialisation a réussi après un court délai
                _ = Task.Run(async () => {
                    await Task.Delay(5000); // Attendre 5 secondes
                    if (_client != null && !_client.IsInitialized)
                    {
                        Console.WriteLine("Impossible d'initialiser Discord RPC dans le délai imparti");
                        ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Délai d'initialisation dépassé"));
                        StartReconnectTimer();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'initialisation de Discord RPC: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur: {ex.Message}"));
                StartReconnectTimer(); // Démarrer le timer de reconnexion en cas d'erreur
            }
        }

        public void UpdatePresence(TrackInfo? trackInfo, string source = "iTunes")
        {
            try
            {
                // Stocker la piste en cours même si Discord n'est pas disponible
                CurrentTrack = trackInfo;
                
                if (_client == null || !_client.IsInitialized)
                {
                    // Tenter de réinitialiser le client de manière asynchrone
                    _ = Task.Run(async () => await InitializeAsync());
                    return;
                }

                // Si trackInfo est null, effacer la présence
                if (trackInfo == null)
                {
                    ClearPresence();
                    return;
                }

                if (_client != null && _client.IsInitialized)
                {
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
                // Tenter de réinitialiser la connexion de manière asynchrone
                _ = Task.Run(async () => await ReconnectAsync());
                // Notifier l'erreur
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur de mise à jour: {ex.Message}"));
            }
        }

        public void ClearPresence()
        {
            try
            {
                if (_client != null && _client.IsInitialized)
                {
                    _client.ClearPresence();
                }
                
                // Réinitialiser la piste en cours
                CurrentTrack = null;
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
                // Notifier que la tentative de reconnexion a été lancée
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Tentative de reconnexion..."));
                
                // Lancer la reconnexion de manière asynchrone pour ne pas bloquer l'interface utilisateur
                _ = Task.Run(async () => await ReconnectAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la reconnexion: {ex.Message}");
            }
        }
        
        private async Task ReconnectAsync()
        {
            try
            {
                // Fermer la connexion existante
                _client?.Dispose();
                _client = null;
                
                // Attendre un court instant pour s'assurer que les ressources sont libérées
                await Task.Delay(500);
                
                // Réinitialiser
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la reconnexion asynchrone: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur de reconnexion: {ex.Message}"));
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
            if (_reconnectTimer == null && !_isReconnecting)
            {
                _isReconnecting = true;
                _reconnectTimer = new Timer(RECONNECT_INTERVAL);
                _reconnectTimer.Elapsed += (s, e) => _ = Task.Run(async () => await TryReconnect()); // Exécuter la reconnexion dans un thread séparé
                _reconnectTimer.Start();
                
                // Notifier du changement de statut
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Déconnecté - Tentatives de reconnexion en cours"));
            }
        }
        
        private void StopReconnectTimer()
        {
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Stop();
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
                _isReconnecting = false;
                
                // Notifier du changement de statut
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(true, "Connecté"));
            }
        }
        
        private async Task TryReconnect()
        {
            try
            {
                Console.WriteLine("Tentative de reconnexion à Discord...");
                // Utiliser la méthode asynchrone pour la reconnexion
                await ReconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Échec de la tentative de reconnexion: {ex.Message}");
                // Notifier l'échec de la reconnexion
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Échec de reconnexion: {ex.Message}"));
            }
        }
    }
}