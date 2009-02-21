using System;
using System.Collections.Generic;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.GridServer
{
    public interface IGridMessagingModule
    {
        List<MessageServerInfo> MessageServers { get; }
    }
}
