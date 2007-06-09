using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsLocal : RegionServerCommsManager
    {
        public RegionServerCommsLocal()
        {
            userServer = new UserServer.UserCommsManagerLocal(); //Local User Server
            gridServer = new GridServer.GridCommsManagerLocal(); //Locl Grid Server
        }
    }
}
