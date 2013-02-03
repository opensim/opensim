using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace OpenSim.Region.ClientStack.TCPJSONStream
{
    public class DisconnectedEventArgs:EventArgs
    {
        public SocketError Error { get; private set; }
        public DisconnectedEventArgs(SocketError err)
        {
            Error = err;
        }
    }
}
