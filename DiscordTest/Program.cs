using DiscordRPC;
using DiscordRPC.Logging;
using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DiscordTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Test de diagnostic Discord RPC ===");
            
            // Test 1: Vérifier si Discord est en cours d'exécution
            Console.WriteLine("\n1. Vérification de Discord...");
            bool discordRunning = IsDiscordRunning();
            Console.WriteLine($"Discord en cours d'exécution: {discordRunning}");
            
            if (!discordRunning)
            {
                Console.WriteLine("PROBLÈME: Discord n'est pas en cours d'exécution!");
                Console.WriteLine("Veuillez démarrer Discord et réessayer.");
                Console.WriteLine("Appuyez sur une touche pour quitter...");
                Console.ReadKey();
                return;
            }
            
            // Test 2: Initialiser le client Discord RPC
            Console.WriteLine("\n2. Initialisation du client Discord RPC...");
            var client = new DiscordRpcClient("1369005012486852649")
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Info }
            };
            
            bool connected = false;
            string connectionError = "";
            
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine($"Discord RPC prêt pour l'utilisateur {e.User.Username}");
                connected = true;
            };
            
            client.OnConnectionFailed += (sender, e) =>
            {
                Console.WriteLine($"Échec de connexion à Discord: {e.FailedPipe}");
                connectionError = $"Échec de connexion: {e.FailedPipe}";
            };
            
            client.OnError += (sender, e) =>
            {
                Console.WriteLine($"Erreur Discord RPC: {e.Message}");
                connectionError = $"Erreur: {e.Message}";
            };
            
            try
            {
                client.Initialize();
                Console.WriteLine("Client initialisé, attente de la connexion...");
                
                // Attendre jusqu'à 10 secondes pour la connexion
                for (int i = 0; i < 100; i++)
                {
                    await Task.Delay(100);
                    if (connected || !string.IsNullOrEmpty(connectionError))
                        break;
                }
                
                if (connected)
                {
                    Console.WriteLine("\n3. Connexion réussie! Test de mise à jour de présence...");
                    
                    var presence = new RichPresence()
                    {
                        Details = "Test Song",
                        State = "par Test Artist",
                        Assets = new Assets()
                        {
                            LargeImageKey = "itunes_logo",
                            LargeImageText = "Test Album",
                            SmallImageKey = "play_icon",
                            SmallImageText = "Via Test - En lecture"
                        },
                        Timestamps = new Timestamps()
                        {
                            Start = DateTime.UtcNow,
                            End = DateTime.UtcNow.AddMinutes(3)
                        }
                    };
                    
                    client.SetPresence(presence);
                    Console.WriteLine("Présence mise à jour! Vérifiez votre profil Discord.");
                    
                    Console.WriteLine("\n4. Attente de 30 secondes pour voir la présence...");
                    await Task.Delay(30000);
                    
                    Console.WriteLine("\n5. Test terminé avec succès!");
                }
                else
                {
                    Console.WriteLine($"\nPROBLÈME: Impossible de se connecter à Discord.");
                    if (!string.IsNullOrEmpty(connectionError))
                    {
                        Console.WriteLine($"Erreur: {connectionError}");
                    }
                    Console.WriteLine("\nCauses possibles:");
                    Console.WriteLine("- Discord n'est pas démarré");
                    Console.WriteLine("- Discord est en mode développeur");
                    Console.WriteLine("- Problème de permissions");
                    Console.WriteLine("- Application Discord RPC non autorisée");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErreur lors de l'initialisation: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                client?.Dispose();
            }
            
            Console.WriteLine("\nAppuyez sur une touche pour quitter...");
            Console.ReadKey();
        }
        
        private static bool IsDiscordRunning()
        {
            try
            {
                var discordProcesses = Process.GetProcessesByName("Discord");
                return discordProcesses.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification de Discord: {ex.Message}");
                return false;
            }
        }
    }
}
