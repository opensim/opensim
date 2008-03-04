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
* 
*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

namespace OpenSim.ApplicationPlugins.LoadBalancer {
	public class AsynchronousClient {
	    private static ManualResetEvent connectDone = new ManualResetEvent(false);
	    private static ManualResetEvent sendDone = new ManualResetEvent(false);
	    private static ManualResetEvent receiveDone = new ManualResetEvent(false);
	    private static String response = String.Empty;

	    public static Socket StartClient(string hostname, int port) {
	        try {
	            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostname);
	            IPAddress ipAddress = ipHostInfo.AddressList[0];
	            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

				Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				client.BeginConnect( remoteEP, new AsyncCallback(ConnectCallback), client);
	            connectDone.WaitOne();
				/*
	            Send(client,"This is a test<EOF>");
	            sendDone.WaitOne();
	            Receive(client);
	            receiveDone.WaitOne();
	            client.Shutdown(SocketShutdown.Both);
	            client.Close();
				 */
				return client;
			} catch (Exception e) {
	            Console.WriteLine(e.ToString());
				throw new Exception("socket error !!");
			}
	    }

	    private static void ConnectCallback(IAsyncResult ar) {
	        try {
	            Socket client = (Socket) ar.AsyncState;
				client.EndConnect(ar);
	            Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());
	            connectDone.Set();
	        } catch (Exception e) {
	            Console.WriteLine(e.ToString());
	        }
	    }

/*
	    public static void Receive(Socket client) {
	        try {
	            StateObject state = new StateObject();
	            state.workSocket = client;
	            client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
	        } catch (Exception e) {
	            Console.WriteLine(e.ToString());
	        }
	    }

	    private static void ReceiveCallback( IAsyncResult ar ) {
	        try {
	            StateObject state = (StateObject) ar.AsyncState;
	            Socket client = state.workSocket;

				int bytesRead = client.EndReceive(ar);
	            if (bytesRead > 0) {
					state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));
	                client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);
	            } else {
	                if (state.sb.Length > 1) {
	                    response = state.sb.ToString();
	                }
	                receiveDone.Set();
	            }
	        } catch (Exception e) {
	            Console.WriteLine(e.ToString());
	        }
	    }
*/
	    public static void Send(Socket client, byte[] byteData) {
	        client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
	    }

	    private static void SendCallback(IAsyncResult ar) {
	        try {
	            Socket client = (Socket) ar.AsyncState;
	            int bytesSent = client.EndSend(ar);
	            //Console.WriteLine("Sent {0} bytes to server.", bytesSent);
	            sendDone.Set();
	        } catch (Exception e) {
	            Console.WriteLine(e.ToString());
	        }
	    }
	}

public class InternalPacketHeader
{
	private byte[] buffer = new byte[32];
	public int 	type;
	public int	throttlePacketType;
	public int	numbytes;
	public Guid	agent_id;
	public int	region_port;

	public void FromBytes(byte[] bytes)
    {
		int i = 0; // offset
		try
        {
			this.type = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
			this.throttlePacketType = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
			this.numbytes = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
			this.agent_id = new Guid(
                bytes[i++] | (bytes[i++] << 8) | (bytes[i++] << 16) | bytes[i++] << 24,
                (short)(bytes[i++] | (bytes[i++] << 8)),
                (short)(bytes[i++] | (bytes[i++] << 8)),
                bytes[i++], bytes[i++], bytes[i++], bytes[i++],
                bytes[i++], bytes[i++], bytes[i++], bytes[i++]);
			this.region_port = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
        }
        catch (Exception)
        {
            throw new Exception("bad format!!!");
        }
    }

	public byte[] ToBytes()
    {
		int i = 0;
		this.buffer[i++] = (byte)(this.type % 256);
        this.buffer[i++] = (byte)((this.type >> 8) % 256);
        this.buffer[i++] = (byte)((this.type >> 16) % 256);
        this.buffer[i++] = (byte)((this.type >> 24) % 256);

		this.buffer[i++] = (byte)(this.throttlePacketType % 256);
        this.buffer[i++] = (byte)((this.throttlePacketType >> 8) % 256);
        this.buffer[i++] = (byte)((this.throttlePacketType >> 16) % 256);
        this.buffer[i++] = (byte)((this.throttlePacketType >> 24) % 256);

		this.buffer[i++] = (byte)(this.numbytes % 256);
        this.buffer[i++] = (byte)((this.numbytes >> 8) % 256);
        this.buffer[i++] = (byte)((this.numbytes >> 16) % 256);
        this.buffer[i++] = (byte)((this.numbytes >> 24) % 256);

		// no endian care
		Buffer.BlockCopy(agent_id.ToByteArray(), 0, this.buffer, i, 16); i += 16;

		this.buffer[i++] = (byte)(this.region_port % 256);
        this.buffer[i++] = (byte)((this.region_port >> 8) % 256);
        this.buffer[i++] = (byte)((this.region_port >> 16) % 256);
        this.buffer[i++] = (byte)((this.region_port >> 24) % 256);

		return this.buffer;
	}
}
	public class TcpClient {

		public static int internalPacketHeaderSize = 4*4 + 16*1;

		private string mHostname;
		private int mPort;
		private Socket mConnection;
		public TcpClient(string hostname, int port) {
			this.mHostname = hostname;
			this.mPort = port;
			this.mConnection = null;
		}
		public void connect() {
			this.mConnection = AsynchronousClient.StartClient(mHostname, mPort);
		}
/*
		public void recevie() {
			if (mConnection == null) {
				throw new Exception("client not initialized");
			}
			try
			{
				AsynchronousClient.Receive(this.mConnection);
			}
			catch (Exception e)
			{
		            Console.WriteLine(e.ToString());
				mConnection = null;
			}
		}
*/
		public void send(InternalPacketHeader header, byte[] packet) {

           lock (this)
            {

    			if (mConnection == null) {
//	    			throw new Exception("client not initialized");
		    		connect();
    			}

    			AsynchronousClient.Send(this.mConnection, header.ToBytes());

/*
for (int i = 0; i < 10; i++)
{
    Console.Write(packet[i] + " ");
}
Console.WriteLine("");
*/
    			AsynchronousClient.Send(this.mConnection, packet);
            }
		}
	}
}
