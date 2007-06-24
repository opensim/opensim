using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Communications;
namespace OpenGrid.Framework.Communications.OGS1
{
    public class GridCommsManager : CommunicationsManager
    {
       

        public GridCommsManager()
        {
            GridServer = new OGS1GridServices();
            InterRegion = new OGSInterSimComms();
            UserServer = new OGSUserServices();
        }
    }
}
