using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using OpenSim.Tools.ControlPanel.Models;

namespace OpenSim.Tools.ControlPanel.Services
{
    /// <summary>
    /// Service for managing OpenSim instances - starting, stopping, monitoring status.
    /// </summary>
    public class OpenSimManager
    {
        private readonly Dictionary<string, Process> _runningInstances = new();
        private readonly string _openSimPath;
        
        public event EventHandler<SimStatusChangedEventArgs> StatusChanged;
        
        /// <summary>
        /// Gets the OpenSim installation path
        /// </summary>
        public string OpenSimPath => _openSimPath;
        
        public OpenSimManager(string openSimPath = "")
        {
            _openSimPath = string.IsNullOrEmpty(openSimPath) 
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "bin")
                : openSimPath;
        }
        
        /// <summary>
        /// Get the status of all managed OpenSim instances.
        /// </summary>
        public List<SimInstance> GetSimInstances()
        {
            var instances = new List<SimInstance>();
            
            // Add running instances
            foreach (var kvp in _runningInstances)
            {
                var process = kvp.Value;
                instances.Add(new SimInstance
                {
                    Name = kvp.Key,
                    Status = process.HasExited ? SimStatus.Stopped : SimStatus.Running,
                    ProcessId = process.HasExited ? 0 : process.Id,
                    StartTime = process.StartTime,
                    ConfigPath = GetConfigPath(kvp.Key)
                });
            }
            
            // Check for configured but not running instances
            var configuredSims = GetConfiguredSimulations();
            foreach (var configName in configuredSims)
            {
                if (!_runningInstances.ContainsKey(configName))
                {
                    instances.Add(new SimInstance
                    {
                        Name = configName,
                        Status = SimStatus.Stopped,
                        ConfigPath = GetConfigPath(configName)
                    });
                }
            }
            
            return instances;
        }
        
        /// <summary>
        /// Start an OpenSim instance with the specified configuration.
        /// </summary>
        public async Task<bool> StartSimAsync(string simName, string configPath = null)
        {
            try
            {
                if (_runningInstances.ContainsKey(simName))
                {
                    throw new InvalidOperationException($"Simulation '{simName}' is already running");
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "OpenSim.dll",
                    WorkingDirectory = _openSimPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                // Add configuration file if specified
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                {
                    startInfo.Arguments += $" -inifile \"{configPath}\"";
                }
                
                var process = new Process { StartInfo = startInfo };
                
                // Set up event handlers for process monitoring
                process.EnableRaisingEvents = true;
                process.Exited += (sender, e) => OnProcessExited(simName);
                
                if (process.Start())
                {
                    _runningInstances[simName] = process;
                    OnStatusChanged(simName, SimStatus.Starting);
                    
                    // Give the process a moment to initialize
                    await Task.Delay(2000);
                    
                    if (!process.HasExited)
                    {
                        OnStatusChanged(simName, SimStatus.Running);
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                OnStatusChanged(simName, SimStatus.Error, ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Stop a running OpenSim instance.
        /// </summary>
        public async Task<bool> StopSimAsync(string simName)
        {
            try
            {
                if (!_runningInstances.TryGetValue(simName, out var process))
                {
                    return false; // Not running
                }
                
                OnStatusChanged(simName, SimStatus.Stopping);
                
                // Try graceful shutdown first
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("shutdown");
                    
                    // Wait up to 10 seconds for graceful shutdown
                    if (!process.WaitForExit(10000))
                    {
                        // Force kill if graceful shutdown failed
                        process.Kill();
                    }
                }
                
                _runningInstances.Remove(simName);
                OnStatusChanged(simName, SimStatus.Stopped);
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged(simName, SimStatus.Error, ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Restart an OpenSim instance.
        /// </summary>
        public async Task<bool> RestartSimAsync(string simName)
        {
            var instance = GetSimInstances().Find(s => s.Name == simName);
            if (instance == null) return false;
            
            if (instance.Status == SimStatus.Running)
            {
                await StopSimAsync(simName);
                await Task.Delay(2000); // Wait for clean shutdown
            }
            
            return await StartSimAsync(simName, instance.ConfigPath);
        }
        
        private void OnProcessExited(string simName)
        {
            _runningInstances.Remove(simName);
            OnStatusChanged(simName, SimStatus.Stopped);
        }
        
        private void OnStatusChanged(string simName, SimStatus status, string errorMessage = null)
        {
            StatusChanged?.Invoke(this, new SimStatusChangedEventArgs
            {
                SimName = simName,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now
            });
        }
        
        private List<string> GetConfiguredSimulations()
        {
            var sims = new List<string>();
            
            // Look for .ini files in common locations
            var configPaths = new[]
            {
                Path.Combine(_openSimPath, "Regions"),
                Path.Combine(_openSimPath, "config-include")
            };
            
            foreach (var path in configPaths)
            {
                if (Directory.Exists(path))
                {
                    var iniFiles = Directory.GetFiles(path, "*.ini");
                    foreach (var file in iniFiles)
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!sims.Contains(name))
                        {
                            sims.Add(name);
                        }
                    }
                }
            }
            
            return sims;
        }
        
        private string GetConfigPath(string simName)
        {
            var configPaths = new[]
            {
                Path.Combine(_openSimPath, "Regions", $"{simName}.ini"),
                Path.Combine(_openSimPath, "config-include", $"{simName}.ini"),
                Path.Combine(_openSimPath, $"{simName}.ini")
            };
            
            foreach (var path in configPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return "";
        }
    }
    
    public class SimStatusChangedEventArgs : EventArgs
    {
        public string SimName { get; set; } = "";
        public SimStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}