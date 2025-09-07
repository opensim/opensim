using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using OpenSim.Tools.ControlPanel.Services;
using OpenSim.Tools.ControlPanel.Models;

namespace OpenSim.Tools.ControlPanel.UI
{
    /// <summary>
    /// Main console application for the OpenSim Control Panel.
    /// Provides a rich terminal interface for managing OpenSim instances.
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
            AnsiConsole.Clear();
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
            var banner = new FigletText("OpenSim Control Panel")
                .Centered()
                .Color(Color.Cyan1);
            
            AnsiConsole.Write(banner);
            
            AnsiConsole.Write(new Rule("[silver]Cross-Platform Simulation Management[/]")
                .Centered()
                .RuleStyle("grey"));
            
            AnsiConsole.WriteLine();
        }
        
        private async Task ShowMainMenu()
        {
            var instances = _simManager.GetSimInstances();
            
            // Display current status
            DisplaySimulationStatus(instances);
            
            // Show menu options
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "View Detailed Status",
                        "Start Simulation",
                        "Stop Simulation", 
                        "Restart Simulation",
                        "Create New Simulation",
                        "Configuration Manager",
                        "Performance Monitor",
                        "View Logs",
                        "Settings",
                        "Exit"
                    }));
            
            switch (choice)
            {
                case "View Detailed Status":
                    await ShowDetailedStatus();
                    break;
                case "Start Simulation":
                    await StartSimulation();
                    break;
                case "Stop Simulation":
                    await StopSimulation();
                    break;
                case "Restart Simulation":
                    await RestartSimulation();
                    break;
                case "Create New Simulation":
                    await CreateNewSimulation();
                    break;
                case "Configuration Manager":
                    await ShowConfigurationManager();
                    break;
                case "Performance Monitor":
                    await ShowPerformanceMonitor();
                    break;
                case "View Logs":
                    await ShowLogs();
                    break;
                case "Settings":
                    await ShowSettings();
                    break;
                case "Exit":
                    _shouldExit = true;
                    break;
            }
        }
        
        private void DisplaySimulationStatus(List<SimInstance> instances)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[yellow]Simulation Status[/]");
            
            table.AddColumn("[blue]Name[/]");
            table.AddColumn("[blue]Status[/]");
            table.AddColumn("[blue]Uptime[/]");
            table.AddColumn("[blue]Config[/]");
            
            if (!instances.Any())
            {
                table.AddRow("[grey]No simulations found[/]", "", "", "");
            }
            else
            {
                foreach (var instance in instances.OrderBy(i => i.Name))
                {
                    var statusColor = instance.Status switch
                    {
                        SimStatus.Running => "green",
                        SimStatus.Error => "red",
                        SimStatus.Starting or SimStatus.Stopping => "yellow",
                        _ => "grey"
                    };
                    
                    table.AddRow(
                        instance.Name,
                        $"[{statusColor}]{instance.StatusText}[/]",
                        instance.UptimeText,
                        Path.GetFileName(instance.ConfigPath)
                    );
                }
            }
            
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
        
        private async Task ShowDetailedStatus()
        {
            var instances = _simManager.GetSimInstances();
            
            if (!instances.Any())
            {
                AnsiConsole.MarkupLine("[red]No simulations found.[/]");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedSim = AnsiConsole.Prompt(
                new SelectionPrompt<SimInstance>()
                    .Title("[green]Select simulation for detailed view:[/]")
                    .UseConverter(sim => $"{sim.Name} ({sim.Status})")
                    .AddChoices(instances));
            
            AnsiConsole.Clear();
            DisplayHeader();
            
            var panel = new Panel(
                new Markup($"""
                [bold]Simulation Details[/]
                
                [blue]Name:[/] {selectedSim.Name}
                [blue]Status:[/] {selectedSim.StatusText}
                [blue]Process ID:[/] {(selectedSim.ProcessId > 0 ? selectedSim.ProcessId.ToString() : "N/A")}
                [blue]Start Time:[/] {selectedSim.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}
                [blue]Uptime:[/] {selectedSim.UptimeText}
                [blue]Config File:[/] {selectedSim.ConfigPath}
                """))
                .Header($"[yellow]{selectedSim.Name}[/]");
            
            AnsiConsole.Write(panel);
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task StartSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Stopped)
                .ToList();
            
            if (!instances.Any())
            {
                AnsiConsole.MarkupLine("[red]No stopped simulations found.[/]");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedSim = AnsiConsole.Prompt(
                new SelectionPrompt<SimInstance>()
                    .Title("[green]Select simulation to start:[/]")
                    .UseConverter(sim => sim.Name)
                    .AddChoices(instances));
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .Start($"Starting {selectedSim.Name}...", async ctx =>
                {
                    var success = await _simManager.StartSimAsync(selectedSim.Name, selectedSim.ConfigPath);
                    
                    if (success)
                    {
                        AnsiConsole.MarkupLine($"[green]Successfully started {selectedSim.Name}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to start {selectedSim.Name}[/]");
                    }
                });
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task StopSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Running)
                .ToList();
            
            if (!instances.Any())
            {
                AnsiConsole.MarkupLine("[red]No running simulations found.[/]");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedSim = AnsiConsole.Prompt(
                new SelectionPrompt<SimInstance>()
                    .Title("[green]Select simulation to stop:[/]")
                    .UseConverter(sim => sim.Name)
                    .AddChoices(instances));
            
            var confirm = AnsiConsole.Confirm($"Are you sure you want to stop {selectedSim.Name}?");
            if (!confirm) return;
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .Start($"Stopping {selectedSim.Name}...", async ctx =>
                {
                    var success = await _simManager.StopSimAsync(selectedSim.Name);
                    
                    if (success)
                    {
                        AnsiConsole.MarkupLine($"[green]Successfully stopped {selectedSim.Name}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to stop {selectedSim.Name}[/]");
                    }
                });
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task RestartSimulation()
        {
            var instances = _simManager.GetSimInstances()
                .Where(i => i.Status == SimStatus.Running)
                .ToList();
            
            if (!instances.Any())
            {
                AnsiConsole.MarkupLine("[red]No running simulations found.[/]");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedSim = AnsiConsole.Prompt(
                new SelectionPrompt<SimInstance>()
                    .Title("[green]Select simulation to restart:[/]")
                    .UseConverter(sim => sim.Name)
                    .AddChoices(instances));
            
            var confirm = AnsiConsole.Confirm($"Are you sure you want to restart {selectedSim.Name}?");
            if (!confirm) return;
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .Start($"Restarting {selectedSim.Name}...", async ctx =>
                {
                    var success = await _simManager.RestartSimAsync(selectedSim.Name);
                    
                    if (success)
                    {
                        AnsiConsole.MarkupLine($"[green]Successfully restarted {selectedSim.Name}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to restart {selectedSim.Name}[/]");
                    }
                });
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task CreateNewSimulation()
        {
            AnsiConsole.MarkupLine("[blue]Simulation Setup Wizard[/]");
            AnsiConsole.WriteLine();
            
            var config = new SimConfig();
            
            // Collect basic information
            config.Name = AnsiConsole.Ask<string>("Enter simulation name:");
            config.RegionName = AnsiConsole.Ask<string>("Enter region name:");
            config.HttpPort = AnsiConsole.Ask<int>("Enter HTTP port:", 9000);
            config.InternalPort = AnsiConsole.Ask<int>("Enter internal port:", config.HttpPort);
            config.ExternalHost = AnsiConsole.Ask<string>("Enter external host:", "127.0.0.1");
            
            // Advanced settings
            var advancedSettings = AnsiConsole.Confirm("Configure advanced settings?");
            if (advancedSettings)
            {
                config.RegionSizeX = AnsiConsole.Ask<int>("Region size X:", 256);
                config.RegionSizeY = AnsiConsole.Ask<int>("Region size Y:", 256);
                
                config.PhysicsEngine = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select physics engine:")
                        .AddChoices("BulletS", "ubOde", "BasicPhysics", "POS"));
                        
                config.DatabaseProvider = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select database provider:")
                        .AddChoices("SQLite", "MySQL", "PostgreSQL"));
            }
            
            // Validate configuration
            var validation = config.Validate();
            if (!validation.IsValid)
            {
                AnsiConsole.MarkupLine("[red]Configuration validation failed:[/]");
                foreach (var error in validation.Errors)
                {
                    AnsiConsole.MarkupLine($"[red]- {error}[/]");
                }
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            
            // TODO: Implement actual configuration file creation
            AnsiConsole.MarkupLine("[yellow]Configuration file creation will be implemented in the next phase.[/]");
            AnsiConsole.MarkupLine($"[green]Configuration for '{config.Name}' is valid and ready to be saved.[/]");
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task ShowConfigurationManager()
        {
            AnsiConsole.MarkupLine("[yellow]Configuration Manager - Coming Soon[/]");
            AnsiConsole.WriteLine("This feature will allow you to:");
            AnsiConsole.WriteLine("- Edit existing simulation configurations");
            AnsiConsole.WriteLine("- Validate configuration files");
            AnsiConsole.WriteLine("- Create configuration templates");
            AnsiConsole.WriteLine("- Backup and restore configurations");
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task ShowPerformanceMonitor()
        {
            AnsiConsole.MarkupLine("[yellow]Performance Monitor - Coming Soon[/]");
            AnsiConsole.WriteLine("This feature will show:");
            AnsiConsole.WriteLine("- Real-time performance metrics");
            AnsiConsole.WriteLine("- CPU and memory usage");
            AnsiConsole.WriteLine("- Physics performance data");
            AnsiConsole.WriteLine("- User activity statistics");
            AnsiConsole.WriteLine("- Alert system for issues");
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task ShowLogs()
        {
            AnsiConsole.MarkupLine("[yellow]Log Viewer - Coming Soon[/]");
            AnsiConsole.WriteLine("This feature will provide:");
            AnsiConsole.WriteLine("- Real-time log streaming");
            AnsiConsole.WriteLine("- Log filtering and search");
            AnsiConsole.WriteLine("- Error highlighting");
            AnsiConsole.WriteLine("- Log archiving and rotation");
            
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
        private async Task ShowSettings()
        {
            AnsiConsole.MarkupLine("[blue]Control Panel Settings[/]");
            AnsiConsole.WriteLine();
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Settings:")
                    .AddChoices(new[]
                    {
                        "OpenSim Installation Path",
                        "Auto-refresh Interval",
                        "Default Configuration Template",
                        "Backup Settings",
                        "Back to Main Menu"
                    }));
            
            switch (choice)
            {
                case "OpenSim Installation Path":
                    AnsiConsole.MarkupLine($"[green]Current path:[/] {_simManager.GetType().GetField("_openSimPath", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_simManager)}");
                    AnsiConsole.WriteLine("Path configuration will be implemented in the next phase.");
                    break;
                default:
                    AnsiConsole.MarkupLine("[yellow]This setting will be implemented in the next phase.[/]");
                    break;
            }
            
            if (choice != "Back to Main Menu")
            {
                AnsiConsole.WriteLine("Press any key to continue...");
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
        
        private void OnSimStatusChanged(object? sender, SimStatusChangedEventArgs e)
        {
            // Status changes are handled by the background refresh
            // In a more advanced implementation, we could show real-time notifications
        }
    }
}