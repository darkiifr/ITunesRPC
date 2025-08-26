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
        private const int MAX_RECONNECT_ATTEMPTS = 10; // Nombre maximum de tentatives de reconnexion
        private int _reconnectAttempts = 0;
        private DateTime _lastPresenceUpdate = DateTime.MinValue;
        private const int MIN_UPDATE_INTERVAL = 1000; // Intervalle minimum entre les mises à jour (1 seconde)
        
        // Propriété pour stocker la piste en cours
        public TrackInfo? CurrentTrack { get; private set; }
        
        // Événement pour notifier les changements de statut de connexion
        public event EventHandler<DiscordConnectionStatusEventArgs>? ConnectionStatusChanged;

        public DiscordRpcService()
        {
            try
            {
                // Initialisation du constructeur DiscordRpcService
                // Initialiser de manière asynchrone sans bloquer le constructeur
                Task.Run(async () => 
                {
                    try
                    {
                        await InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        // Erreur lors de l'initialisation asynchrone de Discord RPC
                        ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur d'initialisation: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                // Erreur dans le constructeur DiscordRpcService
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
                    Logger = new ConsoleLogger() { Level = LogLevel.None },
                    SkipIdenticalPresence = true
                };

                _client.OnReady += (sender, e) =>
                {
                    // Discord RPC prêt pour l'utilisateur
                    StopReconnectTimer(); // Arrêter le timer de reconnexion si la connexion est établie
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(true, "Connecté"));
                };

                _client.OnPresenceUpdate += (sender, e) =>
                {
                    // Présence mise à jour
                };
                
                _client.OnConnectionFailed += (sender, e) =>
                {
                    // Échec de connexion à Discord
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Échec de connexion: {e.FailedPipe}"));
                    StartReconnectTimer(); // Démarrer le timer de reconnexion en cas d'échec
                };
                
                _client.OnError += (sender, e) =>
                {
                    // Erreur Discord RPC
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur: {e.Message}"));
                };

                // Initialiser le client Discord RPC
                _client.Initialize();
                
                // Vérifier si l'initialisation a réussi après un court délai
                _ = Task.Run(async () => {
                    await Task.Delay(5000); // Attendre 5 secondes
                    if (_client != null && !_client.IsInitialized)
                    {
                        // Impossible d'initialiser Discord RPC dans le délai imparti
                        ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Délai d'initialisation dépassé"));
                        StartReconnectTimer();
                    }
                });
            }
            catch (Exception ex)
            {
                // Erreur lors de l'initialisation de Discord RPC
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
                
                // Vérifier l'intervalle minimum entre les mises à jour pour éviter le spam
                var now = DateTime.Now;
                if ((now - _lastPresenceUpdate).TotalMilliseconds < MIN_UPDATE_INTERVAL)
                {
                    return; // Ignorer la mise à jour si elle est trop fréquente
                }
                _lastPresenceUpdate = now;
                
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
                    // Valider les données avant de créer la présence
                    var trackName = ValidateString(trackInfo.Name, "Titre inconnu");
                    var artistName = ValidateString(trackInfo.Artist, "Artiste inconnu");
                    var albumName = ValidateString(trackInfo.Album, "Album inconnu");
                    
                    var presence = new RichPresence()
                    {
                        Details = trackName,
                        State = $"par {artistName}",
                        Assets = new Assets()
                        {
                            LargeImageKey = source.ToLower().Contains("apple") ? "apple_music_logo" : "itunes_logo",
                            LargeImageText = albumName,
                            SmallImageKey = trackInfo.IsPlaying ? "play_icon" : "pause_icon",
                            SmallImageText = $"Via {source} - {(trackInfo.IsPlaying ? "En lecture" : "En pause")}"
                        }
                    };
                    
                    // Ajouter les timestamps seulement si la piste est en cours de lecture
                    if (trackInfo.IsPlaying && trackInfo.StartTime != DateTime.MinValue && trackInfo.EndTime != DateTime.MinValue)
                    {
                        presence.Timestamps = new Timestamps()
                        {
                            Start = trackInfo.StartTime,
                            End = trackInfo.EndTime
                        };
                    }
                    
                    // Réinitialiser le compteur de tentatives de reconnexion en cas de succès
                    _reconnectAttempts = 0;
                    
                    // Notifier que la connexion est établie
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(true));

                    _client.SetPresence(presence);
                }
            }
            catch (Exception ex)
            {
                // Erreur lors de la mise à jour de la présence Discord
                // Tenter de réinitialiser la connexion de manière asynchrone seulement si on n'a pas dépassé le nombre max de tentatives
                if (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
                {
                    _ = Task.Run(async () => await ReconnectAsync());
                }
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
                // Erreur lors de l'effacement de la présence
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
                // Erreur lors de la reconnexion
            }
        }
        
        private async Task ReconnectAsync()
        {
            try
            {
                _reconnectAttempts++;
                // Tentative de reconnexion
                
                // Fermer la connexion existante
                _client?.Dispose();
                _client = null;
                
                // Attendre un délai progressif basé sur le nombre de tentatives
                var delay = Math.Min(500 * _reconnectAttempts, 5000); // Maximum 5 secondes
                await Task.Delay(delay);
                
                // Réinitialiser
                await InitializeAsync();
                
                // Si la reconnexion réussit, réinitialiser le compteur
                if (_client != null && _client.IsInitialized)
                {
                    _reconnectAttempts = 0;
                    // Reconnexion réussie
                }
            }
            catch (Exception ex)
            {
                // Erreur lors de la reconnexion asynchrone
                
                if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
                {
                    // Nombre maximum de tentatives de reconnexion atteint. Arrêt des tentatives
                    StopReconnectTimer();
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, "Échec de reconnexion après plusieurs tentatives"));
                }
                else
                {
                    ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Erreur de reconnexion (tentative {_reconnectAttempts}): {ex.Message}"));
                }
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
                // Erreur lors de l'arrêt de Discord RPC
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
                // Tentative de reconnexion à Discord
                // Utiliser la méthode asynchrone pour la reconnexion
                await ReconnectAsync();
            }
            catch (Exception ex)
            {
                // Échec de la tentative de reconnexion
                // Notifier l'échec de la reconnexion
                ConnectionStatusChanged?.Invoke(this, new DiscordConnectionStatusEventArgs(false, $"Échec de reconnexion: {ex.Message}"));
            }
        }
        
        /// <summary>
        /// Valide et nettoie une chaîne de caractères pour l'affichage Discord
        /// </summary>
        /// <param name="input">La chaîne à valider</param>
        /// <param name="fallback">La valeur de remplacement si la chaîne est invalide</param>
        /// <returns>Une chaîne valide pour Discord</returns>
        private string ValidateString(string? input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input))
                return fallback;
            
            // Limiter la longueur pour Discord (max 128 caractères pour Details et State)
            var trimmed = input.Trim();
            if (trimmed.Length > 128)
                trimmed = trimmed.Substring(0, 125) + "...";
            
            // Remplacer les caractères de contrôle qui peuvent causer des problèmes
            trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"[\x00-\x1F\x7F]", "");
            
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }
        
        /// <summary>
        /// Vérifie si Discord est en cours d'exécution sur le système
        /// </summary>
        /// <returns>True si Discord est détecté, false sinon</returns>
        public bool IsDiscordRunning()
        {
            try
            {
                var discordProcesses = System.Diagnostics.Process.GetProcessesByName("Discord");
                return discordProcesses.Length > 0;
            }
            catch (Exception ex)
            {
                // Erreur lors de la vérification de Discord
                return false;
            }
        }
        
        /// <summary>
        /// Obtient des informations de diagnostic sur l'état du service
        /// </summary>
        /// <returns>Informations de diagnostic</returns>
        public string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"Client initialisé: {_client?.IsInitialized ?? false}");
            info.AppendLine($"Discord en cours d'exécution: {IsDiscordRunning()}");
            info.AppendLine($"Tentatives de reconnexion: {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}");
            info.AppendLine($"Timer de reconnexion actif: {_reconnectTimer != null}");
            info.AppendLine($"Dernière mise à jour: {_lastPresenceUpdate}");
            info.AppendLine($"Piste actuelle: {CurrentTrack?.Name ?? "Aucune"}");
            return info.ToString();
        }
    }
}