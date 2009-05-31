using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Object
{
    public interface IObjectSound
    {
        void Play(UUID soundAsset, double volume);
    }
}
