using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenGrid.Framework.Communications;
namespace OpenGrid.Framework.Communications.OGS1
{
    public class OGSInterSimComms  : IInterRegionCommunications
    {
        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            return false;
        }
        public bool ExpectAvatarCrossing(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
        {
            return false;
        }
    }
}
