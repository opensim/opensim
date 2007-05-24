using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class AssetBase
    {
        public byte[] Data;
        public LLUUID FullID;
        public sbyte Type;
        public sbyte InvType;
        public string Name;
        public string Description;

        public AssetBase()
        {

        }
    }
}
