using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;
namespace OpenSim.Framework.Communications.OGS1
{
    public delegate bool InformRegionChild(ulong regionHandle, AgentCircuitData agentData);
    public delegate bool ExpectArrival(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position);

    public sealed class InterRegionSingleton
    {
        static readonly InterRegionSingleton instance = new InterRegionSingleton();

        public event InformRegionChild OnChildAgent;
        public event ExpectArrival OnArrival;

        static InterRegionSingleton()
        {
        }

        InterRegionSingleton()
        {
        }

        public static InterRegionSingleton Instance
        {
            get
            {
                return instance;
            }
        }

        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            if (OnChildAgent != null)
            {
                return OnChildAgent(regionHandle, agentData);
            }
            return false;
        }

        public bool ExpectAvatarCrossing(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
        {
            if (OnArrival != null)
            {
                return OnArrival(regionHandle, agentID, position);
            }
            return false;
        }
    }

    public class OGS1InterRegionRemoting : MarshalByRefObject
    {

        public OGS1InterRegionRemoting()
        {
        }

        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            return InterRegionSingleton.Instance.InformRegionOfChildAgent(regionHandle, agentData);
        }

        public bool ExpectAvatarCrossing(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
        {
            return InterRegionSingleton.Instance.ExpectAvatarCrossing(regionHandle, agentID, position);
        }
    }
}
