using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class Host : IHost
    {
        private readonly IObject m_obj;

        public Host(IObject m_obj)
        {
            this.m_obj = m_obj;
        }

        public IObject Object
        {
            get { return m_obj; }
        }
    }
}
