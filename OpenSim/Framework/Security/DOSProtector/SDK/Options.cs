using System;

namespace OpenSim.Framework.Security.DOSProtector.SDK
{
    public abstract class BaseDosProtectorOptions : IDOSProtectorOptions
    {
    
        public string ReportingName { get; set; } = "";
    
        public ThrottleAction ThrottledAction { get; set; } = ThrottleAction.DoThrottledMethod;
    
        /// <summary>
        /// Time-To-Live for inspection entries. Inactive clients are removed after this duration.
        /// Defaults to 10 minutes to allow for temporary traffic bursts.
        /// </summary>
        public TimeSpan InspectionTTL { get; set; } = TimeSpan.FromMinutes(10);
    
        /// <summary>
        /// Log level for DOS protection events.
        /// Controls verbosity of logging to prevent log spam during attacks.
        /// Default: Warn (logs blocks and warnings)
        /// </summary>
        public DOSProtectorLogLevel LogLevel { get; set; } = DOSProtectorLogLevel.Warn;

        /// <summary>
        /// Redact client identifiers (IPs) in log messages for privacy/GDPR compliance.
        /// When enabled, logs show partial identifiers (e.g., "192.168.***.***").
        /// Default: false (full identifiers logged)
        /// </summary>
        public bool RedactClientIdentifiers { get; set; } = false;
    }
}