using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenGrid.Framework.Communications;
namespace OpenGrid.Framework.Communications.OGS1
{
    public class GridCommsManager : CommunicationsManager
    {
        private OGS1GridServices gridInterComms = new OGS1GridServices();
        public GridCommsManager(NetworkServersInfo serversInfo) :base(serversInfo)
        {
            GridServer = gridInterComms;
            InterRegion = gridInterComms;
            UserServer = new OGSUserServices();
        }
    }
}
