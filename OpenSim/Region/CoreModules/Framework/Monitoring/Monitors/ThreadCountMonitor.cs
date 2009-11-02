
namespace OpenSim.Region.CoreModules.Framework.Monitoring.Monitors
{
    class ThreadCountMonitor : IMonitor
    {
        #region Implementation of IMonitor

        public double GetValue()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
        }

        public string GetName()
        {
            return "Total Threads";
        }

        public string GetFriendlyValue()
        {
            return (int)GetValue() + " Thread(s) (Global)";
        }

        #endregion
    }
}
