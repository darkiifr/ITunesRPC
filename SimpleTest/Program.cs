using System;
using System.Threading.Tasks;

namespace SimpleTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Test de diagnostic iTunes RPC ===");
            Console.WriteLine();
            
            try
            {
                Console.WriteLine("1. Test de base - OK");
                
                // Test 2: Vérification des dépendances système
                Console.WriteLine("2. Test des dépendances système...");
                
                // Test de System.Management
                try
                {
                    var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'Discord.exe'");
                    Console.WriteLine("   - System.Management: OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   - System.Management: ERREUR - {ex.Message}");
                }
                
                // Test 3: Vérification de Discord RPC
                Console.WriteLine("3. Test Discord RPC...");
                try
                {
                    // Simuler l'initialisation Discord RPC
                    Console.WriteLine("   - Création du client Discord RPC...");
                    // var client = new DiscordRPC.DiscordRpcClient("1369005012486852649");
                    Console.WriteLine("   - Discord RPC: OK (simulation)");
                }
                catch (Exception ex)
                {
                }
                
                Console.WriteLine();
                Console.WriteLine("=== Fin des tests ===");
                Console.WriteLine("Appuyez sur une touche pour continuer...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur critique: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Appuyez sur une touche pour fermer...");
                Console.ReadKey();
            }
        }
    }
}
