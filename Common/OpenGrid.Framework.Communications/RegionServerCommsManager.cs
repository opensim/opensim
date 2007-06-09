using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using libsecondlife;

namespace OpenGrid.Framework.Communications
{
 
    public class RegionServerCommsManager
    {
        public UserServer.UserCommsManagerBase userServer;
        public GridServer.GridCommsManagerBase gridServer;

        public RegionServerCommsManager()
        {
            
        }
    }
}
