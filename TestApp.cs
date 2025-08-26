using System;
using System.Windows;
using System.Runtime.InteropServices;

namespace TestApp
{
    public class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [STAThread]
        public static void Main()
        {
            try
            {
                AllocConsole();
                Console.WriteLine("Test de démarrage de l'application...");
                
                // Test simple d'initialisation WPF
                var app = new Application();
                Console.WriteLine("Application WPF créée avec succès.");
                
                // Test d'initialisation des services un par un
                Console.WriteLine("Test d'initialisation des services...");
                
                // Test Discord RPC
                try
                {
                    Console.WriteLine("Test Discord RPC...");
                    var discordClient = new DiscordRPC.DiscordRpcClient("1369005012486852649");
                    Console.WriteLine("Client Discord RPC créé avec succès.");
                    discordClient.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur Discord RPC: {ex.Message}");
                }
                
                Console.WriteLine("Test terminé. Appuyez sur une touche pour continuer...");
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