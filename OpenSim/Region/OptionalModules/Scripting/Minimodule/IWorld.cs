using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IWorld
    {
        IObject[] Objects { get; }
        IHeightmap Terrain { get; }
    }
}
