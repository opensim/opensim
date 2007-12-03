using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IAvatarFactory : IRegionModule
    {
        bool TryGetInitialAvatarAppearance(LLUUID avatarId, out AvatarWearable[] wearables, out byte[] visualParams);
    }
}