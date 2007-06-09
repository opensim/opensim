using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsOGS : RegionServerCommsManager
    {
        public RegionServerCommsOGS()
        {
            userServer = new UserServer.UserCommsManagerOGS(); //Remote User Server
            gridServer = new GridServer.GridCommsManagerOGS(); //Remote Grid Server
        }
    }
}
