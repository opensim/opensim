using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IWorld
    {
        IObject[] Objects { get; }
        IHeightmap Terrain { get; }
    }
}
