using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using OpenSim.Region.ScriptEngine.Common;

namespace OpenSim.Region.ScriptEngine.RemoteServer
{
    class RemoteServer
    {
        // Handles connections to servers
        // Create and returns server object

        public RemoteServer()
        {
            TcpChannel chan = new TcpChannel();
            ChannelServices.RegisterChannel(chan, true);
        }

        public ScriptServerInterfaces.ServerRemotingObject Connect(string hostname, int port)
        {
            // Create a channel for communicating w/ the remote object
            // Notice no port is specified on the client
                        
            try
            {

                // Create an instance of the remote object
                ScriptServerInterfaces.ServerRemotingObject obj = (ScriptServerInterfaces.ServerRemotingObject)Activator.GetObject(
                    typeof(ScriptServerInterfaces.ServerRemotingObject),
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
