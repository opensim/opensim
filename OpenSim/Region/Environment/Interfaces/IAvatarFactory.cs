using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IAvatarFactory : IRegionModule
    {
        bool TryGetIntialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables, out byte[] visualParams);
    }
}