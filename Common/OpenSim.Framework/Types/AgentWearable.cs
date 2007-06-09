using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class AvatarWearable
    {
        public LLUUID AssetID = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");

        public AvatarWearable()
        {

        }

        public static AvatarWearable[] DefaultWearables
        {
            get
            {
                AvatarWearable[] defaultWearables =  new AvatarWearable[13]; //should be 13 of these
                for (int i = 0; i < 13; i++)
                {
                    defaultWearables[i] = new AvatarWearable();
                }
                defaultWearables[0].AssetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
                defaultWearables[0].ItemID = LLUUID.Random();
                return defaultWearables;
            }
        }
    }
}
