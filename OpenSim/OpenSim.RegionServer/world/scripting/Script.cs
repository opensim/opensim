using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.RegionServer.world.scripting
{
    public class Script
    {
        private LLUUID m_scriptId;        
        public virtual LLUUID ScriptId
        {
            get
            {
                return m_scriptId;
            }
        }

        public Script( LLUUID scriptId )
        {
            m_scriptId = scriptId;
        }
        
        public ScriptEventHandler OnFrame;
    }
}
