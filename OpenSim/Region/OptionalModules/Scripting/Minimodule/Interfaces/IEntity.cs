using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IEntity
    {
        string Name { get; set; }
        UUID GlobalID { get; }
        Vector3 WorldPosition { get; set; }
    }
}
