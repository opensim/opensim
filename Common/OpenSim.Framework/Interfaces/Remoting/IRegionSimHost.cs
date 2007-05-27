using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public interface IRegionSimHost
    {
        bool ExpectUser(string name);
        bool AgentCrossing(string name);
    }
}
