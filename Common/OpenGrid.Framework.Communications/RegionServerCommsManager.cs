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
        public UserServer.UserCommsManagerBase UserServer;
        public GridServer.GridCommsManagerBase GridServer;
        public InterSimsCommsBase InterSims;

        public RegionServerCommsManager()
        {
            
        }
    }
}
