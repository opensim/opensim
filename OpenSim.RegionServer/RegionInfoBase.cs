using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using libsecondlife;

namespace OpenSim
{
    public class RegionInfoBase
    {
        public LLUUID SimUUID;
        public string RegionName;
        public uint RegionLocX;
        public uint RegionLocY;
        public ulong RegionHandle;
        public ushort RegionWaterHeight = 20;
        public bool RegionTerraform = true;

        public int IPListenPort;
        public string IPListenAddr;

        public RegionInfoBase()
        {

        }
    }

}
