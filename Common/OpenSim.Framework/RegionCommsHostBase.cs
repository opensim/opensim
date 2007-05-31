using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Framework
{
    public class RegionCommsHostBase :IRegionCommsHost
    {
        public event ExpectUserDelegate OnExpectUser;
        public event GenericCall2 OnExpectChildAgent;
        public event GenericCall2 OnAvatarCrossingIntoRegion;
        public event UpdateNeighbours OnNeighboursUpdate;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public virtual bool TriggerExpectUser(ulong regionHandle, AgentCircuitData  agent)
        {
            if(OnExpectUser != null)
            {
                OnExpectUser(regionHandle, agent);
                return true;
            }

            return false;
        }
    }
}
