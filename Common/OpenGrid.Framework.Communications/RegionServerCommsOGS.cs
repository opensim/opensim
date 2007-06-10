using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsOGS : RegionServerCommsManager
    {
        public RegionServerCommsOGS()
        {
            UserServer = new UserServer.UserCommsManagerOGS(); //Remote User Server
            GridServer = new GridServer.GridCommsManagerOGS(); //Remote Grid Server
            InterSims = new InterSimsCommsOGS();
        }

    }
}
