using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Communications
{
    public interface IAvatarService
    {
        /// Get's the User Appearance
        AvatarAppearance GetUserAppearance(LLUUID user);

        void UpdateUserAppearance(LLUUID user, AvatarAppearance appearance);

        void AddAttachment(LLUUID user, LLUUID attach);

        void RemoveAttachment(LLUUID user, LLUUID attach);

        List<LLUUID> GetAttachments(LLUUID user);
    }
}
