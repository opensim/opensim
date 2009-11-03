using OpenSim.Region.CoreModules.Framework.Monitoring.Monitors;

namespace OpenSim.Region.CoreModules.Framework.Monitoring.Alerts
{
    class DeadlockAlert : IAlert
    {
        private LastFrameTimeMonitor m_monitor;

        public DeadlockAlert(LastFrameTimeMonitor m_monitor)
        {
            this.m_monitor = m_monitor;
        }

        #region Implementation of IAlert

        public string GetName()
        {
            return "Potential Deadlock Alert";
        }

        public void Test()
        {
            if (m_monitor.GetValue() > 60 * 1000)
            {
                if(OnTriggerAlert != null)
                {
                    OnTriggerAlert(typeof (DeadlockAlert),
                                   (int) (m_monitor.GetValue()/1000) + " second(s) since last frame processed.", true);
                }
            }
        }

        public event Alert OnTriggerAlert;

        #endregion
    }
}
