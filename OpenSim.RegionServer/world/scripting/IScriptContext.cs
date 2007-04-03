using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.RegionServer.world.scripting
{
    public interface IScriptContext
    {
        bool MoveTo(LLVector3 newPos);
        LLVector3 GetPos();
    }
}
