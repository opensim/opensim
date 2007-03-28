using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.CAPS
{
    public interface IXmlRPCHandler
    {
        string HandleRPC(string requestBody);
    }
}
