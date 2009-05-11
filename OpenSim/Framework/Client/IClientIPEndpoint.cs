using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace OpenSim.Framework.Client
{
    public interface IClientIPEndpoint
    {
        IPAddress EndPoint { get; }
    }
}
