using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenGrid.Framework.Communications;
namespace OpenGrid.Framework.Communications.OGS1
{
    public class GridCommsManager : CommunicationsManager
    {
        public GridCommsManager(NetworkServersInfo serversInfo) :base(serversInfo)
        {
            GridServer = new OGS1GridServices();
            InterRegion = new OGSInterSimComms();
            UserServer = new OGSUserServices();
        }
    }
}
