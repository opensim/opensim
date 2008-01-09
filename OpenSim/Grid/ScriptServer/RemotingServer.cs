using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using OpenSim.Region.ScriptEngine.Common;


namespace OpenSim.Grid.ScriptServer
{
    class RemotingServer
    {
        TcpChannel channel;
        public RemotingServer(int port, string instanceName)
        {
            // Create an instance of a channel
            channel = new TcpChannel(port);
            ChannelServices.RegisterChannel(channel, true);

            // Register as an available service with the name HelloWorld
            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemotingObject),
                instanceName,
                WellKnownObjectMode.Singleton);

        }
    }
}
