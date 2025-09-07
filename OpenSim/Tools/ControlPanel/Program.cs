using System;
using OpenSim.Tools.ControlPanel.UI;

namespace OpenSim.Tools.ControlPanel
{
    /// <summary>
    /// Main entry point for the OpenSim Control Panel application.
    /// Provides a console-based interface for managing OpenSim instances.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            try
            {
                Console.Title = "OpenSim Control Panel";
                
                // Initialize and run the console UI
                var app = new ControlPanelApp();
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }
    }
}