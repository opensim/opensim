using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IAvatar
    {
        string Name { get; }
        UUID GlobalID { get; }
        Vector3 Position { get; }
    }
}
