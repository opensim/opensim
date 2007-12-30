using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace OpenSim.Region.ScriptEngine.RemoteServer
{
    class RemoteServer
    {
        // Handles connections to servers
        // Create and returns server object

        public OpenSim.Grid.ScriptServer.RemotingObject Connect(string hostname, int port)
        {
            // Create a channel for communicating w/ the remote object
            // Notice no port is specified on the client
            TcpChannel chan = new TcpChannel();
            ChannelServices.RegisterChannel(chan, true);

            // Create an instance of the remote object
            OpenSim.Grid.ScriptServer.RemotingObject obj = (OpenSim.Grid.ScriptServer.RemotingObject)Activator.GetObject(
                typeof(OpenSim.Grid.ScriptServer.RemotingObject),
                "tcp://" + hostname + ":" + port + "/DotNetEngine");

            // Use the object
            if (obj.Equals(null))
            {
                System.Console.WriteLine("Error: unable to locate server");
            }
            else
            {
                return obj;
            }
            return null;

        }
    }
}
