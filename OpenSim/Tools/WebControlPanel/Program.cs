using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Tools.ControlPanel.Services;
using OpenSim.Tools.WebControlPanel.Services;

namespace OpenSim.Tools.WebControlPanel
{
    class Program
    {
        private static WebControlPanelServer _server;
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("OpenSim Web Control Panel Phase 2");
            Console.WriteLine("================================================================================");
            Console.WriteLine("                    Web-Based Simulation Management Interface                   ");
            Console.WriteLine("================================================================================");
            Console.WriteLine();
            
            try
            {
                _server = new WebControlPanelServer();
                await _server.StartAsync();
                
                Console.WriteLine();
                Console.WriteLine("✅ Web Control Panel is now running!");
                Console.WriteLine($"🌐 Dashboard URL: http://localhost:8080/");
                Console.WriteLine($"📚 API Documentation: http://localhost:8080/api/docs");
                Console.WriteLine();
                Console.WriteLine("Press Ctrl+C to stop the server...");
                
                // Handle graceful shutdown
                Console.CancelKeyPress += async (sender, e) => {
                    e.Cancel = true;
                    Console.WriteLine("\nShutting down Web Control Panel...");
                    await _server.StopAsync();
                    Environment.Exit(0);
                };
                
                // Keep the server running
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error starting Web Control Panel: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}