using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface ISocialEntity
    {
        UUID GlobalID { get; }
        string Name { get; }
        bool IsUser { get; }
    }
}