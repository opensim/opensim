using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Framework
{
    public delegate void ExpectUserDelegate(AgentCircuitData agent);
    public delegate void UpdateNeighbours(List<RegionInfo> neighbours);

    public interface IRegionCommsHost
    {
        event ExpectUserDelegate OnExpectUser;
        event GenericCall2 OnExpectChildAgent;
        event GenericCall2 OnAvatarCrossingIntoRegion;
        event UpdateNeighbours OnNeighboursUpdate;
    }
}
