using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public class NullScriptHost : IScriptHost
    {
        LLVector3 m_pos = new LLVector3( 128, 128, 30 );
        public string Name
        {
            get { return "Object"; }
        }

        public LLUUID UUID
        {
            get { return LLUUID.Zero; }
        }

        public LLVector3 AbsolutePosition
        {
            get { return m_pos; }
        }
    }
}
