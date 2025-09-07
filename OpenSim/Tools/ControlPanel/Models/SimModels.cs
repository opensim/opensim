using System;
using System.Collections.Generic;

namespace OpenSim.Tools.ControlPanel.Models
{
    /// <summary>
    /// Represents the status of a simulation instance.
    /// </summary>
    public enum SimStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }
    
    /// <summary>
    /// Represents an OpenSim simulation instance.
    /// </summary>
    public class SimInstance
    {
        public string Name { get; set; } = "";
        public SimStatus Status { get; set; }
        public int ProcessId { get; set; }
        public DateTime? StartTime { get; set; }
        public string ConfigPath { get; set; } = "";
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Gets the display text for the status.
        /// </summary>
        public string StatusText => Status switch
        {
            SimStatus.Stopped => "Stopped",
            SimStatus.Starting => "Starting...",
            SimStatus.Running => $"Running (PID: {ProcessId})",
            SimStatus.Stopping => "Stopping...",
            SimStatus.Error => $"Error: {ErrorMessage}",
            _ => "Unknown"
        };
        
        /// <summary>
        /// Gets the uptime if the simulation is running.
        /// </summary>
        public TimeSpan? Uptime => StartTime.HasValue && Status == SimStatus.Running 
            ? DateTime.Now - StartTime.Value 
            : null;
            
        /// <summary>
        /// Gets a user-friendly uptime display.
        /// </summary>
        public string UptimeText => Uptime?.ToString(@"dd\.hh\:mm\:ss") ?? "N/A";
    }
    
    /// <summary>
    /// Configuration settings for a simulation.
    /// </summary>
    public class SimConfig
    {
        public string Name { get; set; } = "";
        public string RegionName { get; set; } = "";
        public int HttpPort { get; set; } = 9000;
        public int InternalPort { get; set; } = 9000;
        public string ExternalHost { get; set; } = "127.0.0.1";
        public int RegionSizeX { get; set; } = 256;
        public int RegionSizeY { get; set; } = 256;
        public string PhysicsEngine { get; set; } = "BulletS";
        public string DatabaseProvider { get; set; } = "SQLite";
        public string DatabaseConnectionString { get; set; } = "";
        
        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrWhiteSpace(Name))
                result.AddError("Simulation name is required");
                
            if (string.IsNullOrWhiteSpace(RegionName))
                result.AddError("Region name is required");
                
            if (HttpPort < 1024 || HttpPort > 65535)
                result.AddError("HTTP port must be between 1024 and 65535");
                
            if (InternalPort < 1024 || InternalPort > 65535)
                result.AddError("Internal port must be between 1024 and 65535");
                
            if (RegionSizeX < 64 || RegionSizeX > 4096)
                result.AddError("Region X size must be between 64 and 4096");
                
            if (RegionSizeY < 64 || RegionSizeY > 4096)
                result.AddError("Region Y size must be between 64 and 4096");
                
            return result;
        }
    }
    
    /// <summary>
    /// Result of a configuration validation.
    /// </summary>
    public class ValidationResult
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        
        public bool IsValid => _errors.Count == 0;
        public List<string> Errors => _errors;
        public List<string> Warnings => _warnings;
        
        public void AddError(string error) => _errors.Add(error);
        public void AddWarning(string warning) => _warnings.Add(warning);
        
        public string GetSummary()
        {
            if (IsValid && _warnings.Count == 0)
                return "Configuration is valid";
                
            var summary = "";
            if (_errors.Count > 0)
                summary += $"{_errors.Count} error(s)";
                
            if (_warnings.Count > 0)
            {
                if (!string.IsNullOrEmpty(summary))
                    summary += ", ";
                summary += $"{_warnings.Count} warning(s)";
            }
            
            return summary;
        }
    }
    
    /// <summary>
    /// Performance metrics for a simulation.
    /// </summary>
    public class SimPerformanceMetrics
    {
        public string SimName { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public int ActiveObjects { get; set; }
        public int ConnectedUsers { get; set; }
        public double FrameTime { get; set; }
        public double PhysicsTime { get; set; }
        public double ScriptTime { get; set; }
        
        /// <summary>
        /// Gets the overall health status based on metrics.
        /// </summary>
        public PerformanceStatus Status
        {
            get
            {
                if (FrameTime > 100 || CpuUsagePercent > 90)
                    return PerformanceStatus.Critical;
                if (FrameTime > 50 || CpuUsagePercent > 70)
                    return PerformanceStatus.Warning;
                return PerformanceStatus.Good;
            }
        }
    }
    
    /// <summary>
    /// Performance status indicators.
    /// </summary>
    public enum PerformanceStatus
    {
        Good,
        Warning,
        Critical,
        Unknown
    }
}