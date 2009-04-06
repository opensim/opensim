using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IParcel
    {
        string Name { get; set; }
        string Description { get; set; }
        ISocialEntity Owner { get; set; }
        bool[,] Bitmap { get; }
    }
}
