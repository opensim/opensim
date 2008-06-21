using libsecondlife;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class RegionBanListItem
    {
        public LLUUID regionUUID = LLUUID.Zero;
        public LLUUID bannedUUID = LLUUID.Zero;
        public string bannedIP = string.Empty;
        public string bannedIPHostMask = string.Empty;

        public RegionBanListItem()
        {

        }
    }
}
