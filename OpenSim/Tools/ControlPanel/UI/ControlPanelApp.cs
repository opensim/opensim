using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenSim.Tools.ControlPanel.Services;
using OpenSim.Tools.ControlPanel.Models;

namespace OpenSim.Tools.ControlPanel.UI
{
    /// <summary>
    /// Main console application for the OpenSim Control Panel.
    /// Provides a terminal interface for managing OpenSim instances.
    /// </summary>
    public class ControlPanelApp
    {
        private readonly OpenSimManager _simManager;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _shouldExit = false;
        
        public ControlPanelApp()
        {
            _simManager = new OpenSimManager();
            _cancellationTokenSource = new CancellationTokenSource();
            _simManager.StatusChanged += OnSimStatusChanged;
        }
        
        public async Task RunAsync()
        {
            Console.Clear();
            DisplayHeader();
            
            // Start background refresh task
            var refreshTask = StartBackgroundRefresh(_cancellationTokenSource.Token);
            
            try
            {
                while (!_shouldExit)
                {
                    await ShowMainMenu();
                }
            }
            finally
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    await refreshTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }
        }
        
        private void DisplayHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
  ___                  ____  _            ____            _             _   ____                  _ 
 / _ \ _ __   ___ _ __ / ___|(_)_ __ ___  / ___|___  _ __ | |_ _ __ ___ | | |  _ \ __ _ _ __   ___| |
| | | | '_ \ / _ \ '_ \\___ \| | '_ ` _ \| |   / _ \| '_ \| __| '__/ _ \| | | |_) / _` | '_ \ / _ \ |
| |_| | |_) |  __/ | | |___) | | | | | | | |__| (_) | | | | |_| | | (_) | | |  __/ (_| | | | |  __/ |
 \___/| .__/ \___|_| |_|____/|_|_| |_| |_|\____\___/|_| |_|\__|_|  \___/|_| |_|   \__,_|_| |_|\___|_|
      |_|                                                                                            
");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("================================================================================");
            Console.WriteLine("                    Cross-Platform Simulation Management                        ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine();
        }
        
        private async Task ShowMainMenu()
        {
            var instances = _simManager.GetSimInstances();
            
            // Display current status
            DisplaySimulationStatus(instances);
            
            // Show menu options
            Console.WriteLine("What would you like to do?");
            Console.WriteLine();
            Console.WriteLine("1. View Detailed Status");
            Console.WriteLine("2. Start Simulation");
            Console.WriteLine("3. Stop Simulation");
            Console.WriteLine("4. Restart Simulation");
            Console.WriteLine("5. Create New Simulation");
            Console.WriteLine("6. Configuration Manager (Coming Soon)");
            Console.WriteLine("7. Performance Monitor (Coming Soon)");
            Console.WriteLine("8. View Logs (Coming Soon)");
            Console.WriteLine("9. Settings");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice (0-9): ");
            
            var input = Console.ReadLine();
            if (!int.TryParse(input, out var choice))
            {
                Console.WriteLine("Invalid input. Please enter a number between 0-9.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            switch (choice)
            {
                case 1:
                    await ShowDetailedStatus();
                    break;
                case 2:
                    await StartSimulation();
                    break;
                case 3:
                    await StopSimulation();
                    break;
                case 4:
                    await RestartSimulation();
                    break;
                case 5:
                    await CreateNewSimulation();
                    break;
                case 6:
                case 7:
                case 8:
                    ShowComingSoon();
                    break;
                case 9:
                    await ShowSettings();
                    break;
                case 0:
                    _shouldExit = true;
                    break;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }
        
        private void DisplaySimulationStatus(List<SimInstance> instances)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Simulation Status");
            Console.ResetColor();
            Console.WriteLine("================================================================================");
            
            if (!instances.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("No simulations found");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"{"Name",-20} {"Status",-15} {"Uptime",-12} {"Config",-30}");
                Console.WriteLine(new string('-', 80));
                
                foreach (var instance in instances.OrderBy(i => i.Name))
                {
                    var statusColor = instance.Status switch
                    {
                        SimStatus.Running => ConsoleColor.Green,
                        SimStatus.Error => ConsoleColor.Red,
                        SimStatus.Starting or SimStatus.Stopping => ConsoleColor.Yellow,
                        _ => ConsoleColor.Gray
                    };
                    
                    Console.Write($"{instance.Name,-20} ");
                    Console.ForegroundColor = statusColor;
                    Console.Write($"{instance.StatusText,-15}");
                    Console.ResetColor();
                    Console.WriteLine($" {instance.UptimeText,-12} {System.IO.Path.GetFileName(instance.ConfigPath),-30}");
                }
            }
            
            Console.WriteLine();
        }
        
        private async Task ShowDetailedStatus()
        {
            var instances = _simManager.GetSimInstances();
            
            if (!instances.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No simulations found.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Select simulation for detailed view:");
            for (int i = 0; i < instances.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {instances[i].Name} ({instances[i].Status})");
            }
            Console.Write("Enter selection (1-{0}): ", instances.Count);
            
            if (int.TryParse(Console.ReadLine(), out var selection) && 
                selection >= 1 && selection <= instances.Count)
            {
                var selectedSim = instances[selection - 1];
                
                Console.Clear();
                DisplayHeader();
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Simulation Details: {selectedSim.Name}");
                Console.ResetColor();
                Console.WriteLine("================================================================================");
                Console.WriteLine($"Name:         {selectedSim.Name}");
                Console.WriteLine($"Status:       {selectedSim.StatusText}");
                Console.WriteLine($"Process ID:   {(selectedSim.ProcessId > 0 ? selectedSim.ProcessId.ToString() : "N/A")}");
                Console.WriteLine($"Start Time:   {selectedSim.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
                Console.WriteLine($"Uptime:       {selectedSim.UptimeText}");
                Console.WriteLine($"Config File:  {selectedSim.ConfigPath}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task StartSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Stopped)
                .ToList();
            
            if (!instances.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No stopped simulations found.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Select simulation to start:");
            for (int i = 0; i < instances.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {instances[i].Name}");
            }
            Console.Write("Enter selection (1-{0}): ", instances.Count);
            
            if (int.TryParse(Console.ReadLine(), out var selection) && 
                selection >= 1 && selection <= instances.Count)
            {
                var selectedSim = instances[selection - 1];
                
                Console.WriteLine($"Starting {selectedSim.Name}...");
                var success = await _simManager.StartSimAsync(selectedSim.Name, selectedSim.ConfigPath);
                
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Successfully started {selectedSim.Name}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to start {selectedSim.Name}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task StopSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Running)
                .ToList();
            
            if (!instances.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No running simulations found.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Select simulation to stop:");
            for (int i = 0; i < instances.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {instances[i].Name}");
            }
            Console.Write("Enter selection (1-{0}): ", instances.Count);
            
            if (int.TryParse(Console.ReadLine(), out var selection) && 
                selection >= 1 && selection <= instances.Count)
            {
                var selectedSim = instances[selection - 1];
                
                Console.Write($"Are you sure you want to stop {selectedSim.Name}? (y/N): ");
                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "y") return;
                
                Console.WriteLine($"Stopping {selectedSim.Name}...");
                var success = await _simManager.StopSimAsync(selectedSim.Name);
                
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Successfully stopped {selectedSim.Name}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to stop {selectedSim.Name}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task RestartSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Running)
                .ToList();
            
            if (!instances.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No running simulations found.");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Select simulation to restart:");
            for (int i = 0; i < instances.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {instances[i].Name}");
            }
            Console.Write("Enter selection (1-{0}): ", instances.Count);
            
            if (int.TryParse(Console.ReadLine(), out var selection) && 
                selection >= 1 && selection <= instances.Count)
            {
                var selectedSim = instances[selection - 1];
                
                Console.Write($"Are you sure you want to restart {selectedSim.Name}? (y/N): ");
                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "y") return;
                
                Console.WriteLine($"Restarting {selectedSim.Name}...");
                var success = await _simManager.RestartSimAsync(selectedSim.Name);
                
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Successfully restarted {selectedSim.Name}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to restart {selectedSim.Name}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task CreateNewSimulation()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Simulation Setup Wizard");
            Console.ResetColor();
            Console.WriteLine();
            
            var config = new SimConfig();
            
            // Collect basic information
            Console.Write("Enter simulation name: ");
            config.Name = Console.ReadLine() ?? "";
            
            Console.Write("Enter region name: ");
            config.RegionName = Console.ReadLine() ?? "";
            
            Console.Write("Enter HTTP port (9000): ");
            if (int.TryParse(Console.ReadLine(), out var httpPort))
                config.HttpPort = httpPort;
            else
                config.HttpPort = 9000;
            
            config.InternalPort = config.HttpPort;
            
            Console.Write("Enter external host (127.0.0.1): ");
            var host = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(host))
                config.ExternalHost = host;
            
            // Advanced settings
            Console.Write("Configure advanced settings? (y/N): ");
            var advancedSettings = Console.ReadLine()?.ToLower() == "y";
            
            if (advancedSettings)
            {
                Console.Write("Region size X (256): ");
                if (int.TryParse(Console.ReadLine(), out var sizeX))
                    config.RegionSizeX = sizeX;
                
                Console.Write("Region size Y (256): ");
                if (int.TryParse(Console.ReadLine(), out var sizeY))
                    config.RegionSizeY = sizeY;
                
                Console.WriteLine("Select physics engine:");
                Console.WriteLine("1. BulletS (Recommended)");
                Console.WriteLine("2. ubOde");
                Console.WriteLine("3. BasicPhysics");
                Console.WriteLine("4. POS");
                Console.Write("Enter choice (1-4): ");
                
                var physicsChoice = Console.ReadLine();
                config.PhysicsEngine = physicsChoice switch
                {
                    "2" => "ubOde",
                    "3" => "BasicPhysics",
                    "4" => "POS",
                    _ => "BulletS"
                };
            }
            
            // Validate configuration
            var validation = config.Validate();
            if (!validation.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Configuration validation failed:");
                foreach (var error in validation.Errors)
                {
                    Console.WriteLine($"- {error}");
                }
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Configuration file creation will be implemented in the next phase.");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Configuration for '{config.Name}' is valid and ready to be saved.");
                Console.ResetColor();
            }
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private void ShowComingSoon()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("This feature is coming soon!");
            Console.ResetColor();
            Console.WriteLine("It will be implemented in the next phase of development.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task ShowSettings()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Control Panel Settings");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.WriteLine("1. OpenSim Installation Path");
            Console.WriteLine("2. Auto-refresh Interval");
            Console.WriteLine("3. Default Configuration Template");
            Console.WriteLine("4. Backup Settings");
            Console.WriteLine("5. Back to Main Menu");
            Console.Write("Enter choice (1-5): ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    // Use reflection to get the private field (for demo purposes)
                    var pathField = _simManager.GetType().GetField("_openSimPath", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var currentPath = pathField?.GetValue(_simManager) ?? "Unknown";
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Current path: {currentPath}");
                    Console.ResetColor();
                    Console.WriteLine("Path configuration will be implemented in the next phase.");
                    break;
                default:
                    if (choice != "5")
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("This setting will be implemented in the next phase.");
                        Console.ResetColor();
                    }
                    break;
            }
            
            if (choice != "5")
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
        
        private async Task StartBackgroundRefresh(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        private void OnSimStatusChanged(object sender, SimStatusChangedEventArgs e)
        {
            // Status changes are handled by the background refresh
            // In a more advanced implementation, we could show real-time notifications
        }
    }
}