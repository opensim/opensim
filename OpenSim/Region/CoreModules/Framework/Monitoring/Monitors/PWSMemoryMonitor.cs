using System;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class PWSMemoryMonitor : IMonitor
    {
        #region Implementation of IMonitor

        public double GetValue()
        {
            return System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
        }

        public string GetName()
        {
            return "Private Working Set Memory";
        }

        public string GetFriendlyValue()
        {
            return (int)(GetValue() / (1024 * 1024)) + "MB (Global)";
        }

        #endregion
    }
}
