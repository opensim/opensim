/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
