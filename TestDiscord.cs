using System;
using System.Threading.Tasks;
using ItunesRPC.Services;
using ItunesRPC.Models;

namespace ItunesRPC
{
    class TestDiscord
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Test de diagnostic Discord RPC ===");
            
            // Test 1: Vérifier si Discord est en cours d'exécution
            Console.WriteLine("\n1. Vérification de Discord...");
            var discordService = new DiscordRpcService();
            
            Console.WriteLine($"Discord en cours d'exécution: {discordService.IsDiscordRunning()}");
            
            // Attendre un peu pour l'initialisation
            Console.WriteLine("\n2. Attente de l'initialisation (10 secondes)...");
            await Task.Delay(10000);
            
            // Test 2: Afficher les informations de diagnostic
            Console.WriteLine("\n3. Informations de diagnostic:");
            Console.WriteLine(discordService.GetDiagnosticInfo());
            
            // Test 3: Essayer de mettre à jour la présence avec une piste de test
            Console.WriteLine("\n4. Test de mise à jour de présence...");
            var testTrack = new TrackInfo
            {
                Name = "Test Song",
                Artist = "Test Artist",
                Album = "Test Album",
                IsPlaying = true,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(3)
            };
            
            discordService.UpdatePresence(testTrack, "Test");
            
            Console.WriteLine("\n5. Attente pour voir si la présence s'affiche (30 secondes)...");
            await Task.Delay(30000);
            
            // Test 4: Informations finales
            Console.WriteLine("\n6. Informations finales:");
            Console.WriteLine(discordService.GetDiagnosticInfo());
            
            Console.WriteLine("\n=== Fin du test ===");
            Console.WriteLine("Appuyez sur une touche pour quitter...");
            Console.ReadKey();
            
            discordService.Shutdown();
        }
    }
}