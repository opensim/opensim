using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.WorldX
{
    public interface IWorldAudio
    {
        void PlaySound(UUID audio, Vector3 position, double volume);
        void PlaySound(UUID audio, Vector3 position);
    }
}
