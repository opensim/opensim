using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IAvatarFactory : IRegionModule
    {
        bool TryGetAvatarAppearance(LLUUID avatarId, out AvatarAppearance appearance);
    }
}