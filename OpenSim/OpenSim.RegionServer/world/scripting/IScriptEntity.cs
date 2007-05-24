using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.RegionServer.world.scripting
{
    public interface IScriptReadonlyEntity
    {
        LLVector3 Pos { get; }
        string Name { get; }
    }
    
    public interface IScriptEntity
    {
        LLVector3 Pos { get; set; }
        string Name { get; }
    }
}
