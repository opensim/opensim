using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;


namespace OpenSim.Grid.ScriptServer
{
    class RemotingServer
    {

        public void CreateServer(int port, string instanceName)
        {
            // Create an instance of a channel
            TcpChannel channel = new TcpChannel(port);
            ChannelServices.RegisterChannel(channel, true);

            // Register as an available service with the name HelloWorld
            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemotingObject),
                instanceName,
                WellKnownObjectMode.Singleton);

        }
    }
}
