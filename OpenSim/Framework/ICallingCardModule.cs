using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Framework
{
    public interface ICallingCardModule
    {
        void CreateCallingCard(UUID userID, UUID creatorID, UUID folderID);
    }
}
