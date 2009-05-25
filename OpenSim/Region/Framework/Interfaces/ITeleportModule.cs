using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ITeleportModule
    {
        void RequestTeleportToLocation(ScenePresence avatar, ulong regionHandle, Vector3 position,
                                                      Vector3 lookAt, uint teleportFlags);
    }
}
