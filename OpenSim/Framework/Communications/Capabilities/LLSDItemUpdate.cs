using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Capabilities
{
    [LLSDMap]
    public class LLSDItemUpdate
    {
        public LLUUID item_id;

        public LLSDItemUpdate()
        {
        }
    }
}
