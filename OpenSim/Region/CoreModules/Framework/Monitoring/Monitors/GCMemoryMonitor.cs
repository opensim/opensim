using System;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class GCMemoryMonitor : IMonitor
    {
        #region Implementation of IMonitor

        public double GetValue()
        {
            return GC.GetTotalMemory(false);
        }

        public string GetName()
        {
            return "GC Reported Memory";
        }

        public string GetFriendlyValue()
        {
            return (int)(GetValue() / (1024*1024)) + "MB (Global)";
        }

        #endregion
    }
}
