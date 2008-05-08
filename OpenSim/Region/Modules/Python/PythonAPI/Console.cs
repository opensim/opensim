using System;
using System.Reflection;
using log4net;

namespace OpenSim.Region.Modules.Python.PythonAPI
{
    class Console
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void WriteLine(string txt)
        {
            m_log.Info(txt);
        }

        public void WriteLine(string txt, params Object[] e)
        {
            m_log.Info(String.Format(txt, e));
        }
    }
}
