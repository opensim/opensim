/*
Copyright (c) OpenSim project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.world;

namespace OpenSim
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public class OpenSim_Main 
    {
	private static OpenSim_Main sim;
	public static SimConfig cfg;
	public static World local_world;
	private static Thread MainListener;
	private static Thread PingRespponder;
	public static Socket Server;
	private static IPEndPoint ServerIncoming;
	private static byte[] RecvBuffer = new byte[4096];
	private byte[] ZeroBuffer = new byte[8192];
	private static IPEndPoint ipeSender;
	private static EndPoint epSender;
	private static AsyncCallback ReceivedData;
	public Dictionary<EndPoint, OpenSimClient> ClientThreads = new Dictionary<EndPoint, OpenSimClient>();
	
	[STAThread]
        public static void Main( string[] args ) 
        {
		Console.WriteLine("OpenSim " + VersionInfo.Version + "\n");
		Console.WriteLine("Starting...\n");
		sim = new OpenSim_Main();		
		sim.Startup();
		while(true) {
			Thread.Sleep(1000);
		}
	}

	private OpenSim_Main() {
	}
	
	private void Startup() {
		// We check our local database first, then the grid for config options
		Console.WriteLine("Main.cs:Startup() - Loading configuration");
		cfg = new SimConfig();
		cfg.InitConfig();
		Console.WriteLine("Main.cs:Startup() - Contacting gridserver");
		cfg.LoadFromGrid();

		Console.WriteLine("Main.cs:Startup() - We are " + cfg.RegionName + " at " + cfg.RegionLocX.ToString() + "," + cfg.RegionLocY.ToString());
		Console.WriteLine("Initialising world");
		local_world = cfg.LoadWorld();

		Console.WriteLine("Main.cs:Startup() - Starting up messaging system");
		MainListener = new Thread(new ThreadStart(MainServerListener));	
		MainListener.Start();

	}

	private void OnReceivedData(IAsyncResult result) {
		ipeSender = new IPEndPoint(IPAddress.Any, 0);
		epSender = (EndPoint)ipeSender;
		Packet packet = null;
		int numBytes = Server.EndReceiveFrom(result, ref epSender);
		int packetEnd = numBytes - 1;
		packet = Packet.BuildPacket(RecvBuffer, ref packetEnd, ZeroBuffer);
		Console.Error.WriteLine(packet.ToString());

		// This is either a new client or a packet to send to an old one
		if(ClientThreads.ContainsKey(epSender)) {
			ClientThreads[epSender].InPacket(packet);
		} else if( packet.Type == PacketType.UseCircuitCode ) { // new client
			OpenSimClient newuser = new OpenSimClient(epSender,(UseCircuitCodePacket)packet);
			ClientThreads.Add(epSender, newuser);
		} else { // invalid client
			Console.Error.WriteLine("Main.cs:OnReceivedData() - WARNING: Got a packet from an invalid client - " + epSender.ToString());
		}
		Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
	}

	private void MainServerListener() {
		Console.WriteLine("Main.cs:MainServerListener() - New thread started");
		Console.WriteLine("Main.cs:MainServerListener() - Opening UDP socket on " + cfg.IPListenAddr + ":" + cfg.IPListenPort);

		ServerIncoming = new IPEndPoint(IPAddress.Parse(cfg.IPListenAddr),cfg.IPListenPort);
		Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		Server.Bind(ServerIncoming);
		
		Console.WriteLine("Main.cs:MainServerListener() - UDP socket bound, getting ready to listen");

		ipeSender = new IPEndPoint(IPAddress.Any, 0);
		epSender = (EndPoint) ipeSender;
		ReceivedData = new AsyncCallback(this.OnReceivedData);
		Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
		
		Console.WriteLine("Main.cs:MainServerListener() - Listening...");
		while(true) {
			Thread.Sleep(1000);	
		}
	}
    }
}
