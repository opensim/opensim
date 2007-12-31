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
            try
            {
                ChannelServices.RegisterChannel(chan, true);
            }
            catch (System.Runtime.Remoting.RemotingException)
            {
                System.Console.WriteLine("Error: tcp already registered, RemoteServer.cs in OpenSim.Region.ScriptEngine.RemoteServer line 24");
            }
            try
            {

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
            }
            catch (System.Net.Sockets.SocketException)
            {
                System.Console.WriteLine("Error: unable to connect to server");
            }
            catch (System.Runtime.Remoting.RemotingException)
            {
                System.Console.WriteLine("Error: unable to connect to server");
            }
            return null;

        }
    }
}
