using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IAvatarFactory : IRegionModule
    {
        bool TryGetIntialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables, out byte[] visualParams);
    }
}
