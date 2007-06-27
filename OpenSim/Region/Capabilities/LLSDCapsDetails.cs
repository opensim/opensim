using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Capabilities
{
    [LLSDType("MAP")]
    public class LLSDCapsDetails
    {
        public string MapLayer = "";
        public string NewFileAgentInventory = "";
        //public string EventQueueGet = "";

        public LLSDCapsDetails()
        {

        }
    }
}
