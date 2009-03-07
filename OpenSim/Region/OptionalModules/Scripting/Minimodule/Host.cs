using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class Host : IHost
    {
        private readonly IObject m_obj;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Host(IObject m_obj)
        {
            this.m_obj = m_obj;
        }

        public IObject Object
        {
            get { return m_obj; }
        }

        public ILog Console
        {
            get { return m_log; }
        }
    }
}
