/*
Copyright (c) OpenSim project, http://osgrid.org/
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
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Timers;
using OpenSim.GridServers;
using OpenSim.world;

namespace OpenSim
{
	/// <summary>
	/// Handles new client connections
	/// Constructor takes a single Packet and authenticates everything
	/// </summary>
	public class OpenSimClient {
		
		public LLUUID AgentID;
		public LLUUID SessionID;
		public uint CircuitCode;
		public world.Avatar ClientAvatar;
		private UseCircuitCodePacket cirpack;
		private Thread ClientThread;
		public EndPoint userEP;
		private BlockingQueue<QueItem> PacketQueue;
		private Dictionary<uint, uint> PendingAcks = new Dictionary<uint, uint>();
		private Dictionary<uint, Packet> NeedAck = new Dictionary<uint, Packet>();
		private System.Timers.Timer AckTimer;
	 	private uint Sequence = 0;
	 	private object SequenceLock = new object();
		private const int MAX_APPENDED_ACKS = 10;
		private const int RESEND_TIMEOUT = 4000;
		private const int MAX_SEQUENCE = 0xFFFFFF;
	
		public void ack_pack(Packet Pack) {
                    //libsecondlife.Packets.PacketAckPacket ack_it = new PacketAckPacket();
                    //ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
                    //ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
                    //ack_it.Packets[0].ID = Pack.Header.ID;
                    //ack_it.Header.Reliable = false;

                    //OutPacket(ack_it);
			
		    if (Pack.Header.Reliable) {
			    lock (PendingAcks) {
		 		uint sequence = (uint)Pack.Header.Sequence;
	 			if (!PendingAcks.ContainsKey(sequence)) { PendingAcks[sequence] = sequence; }
	  		    }
		    }
		}
		
		public void ProcessInPacket(Packet Pack) {
		    ack_pack(Pack);
		    switch(Pack.Type) {
		    	case PacketType.CompleteAgentMovement:
		    		ClientAvatar.CompleteMovement(OpenSim_Main.local_world);
		    		ClientAvatar.SendInitialPosition();
		    		break;
		    	case PacketType.RegionHandshakeReply:
		    		OpenSim_Main.local_world.SendLayerData(this);
		    		break;
		    	case PacketType.AgentWearablesRequest:
		    		ClientAvatar.SendInitialAppearance();
		    		foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
		    			if(client.AgentID != this.AgentID)
		    			{
		    				ObjectUpdatePacket objupdate = client.ClientAvatar.CreateUpdatePacket();
		    				this.OutPacket(objupdate);
		    				client.ClientAvatar.SendAppearanceToOtherAgent(this);
		    			}
    				}
		    		OpenSim_Main.local_world.GetInitialPrims(this);
		    		break;
		    	case PacketType.ObjectAdd:
		    		OpenSim_Main.local_world.AddNewPrim((ObjectAddPacket)Pack, this);
		    		break;
		    	case PacketType.ObjectLink:
		    		ServerConsole.MainConsole.Instance.WriteLine(Pack.ToString());
		    		break;
		    	case PacketType.ObjectScale:
		    		ServerConsole.MainConsole.Instance.WriteLine(Pack.ToString());
		    		break;
		    	case PacketType.ObjectShape:
		    		ObjectShapePacket shape = (ObjectShapePacket)Pack;
		    		for(int i =0; i <shape.ObjectData.Length; i++)
		    		{
		    			foreach (Entity ent in OpenSim_Main.local_world.Entities.Values)
		    			{
		    				if(ent.localid == shape.ObjectData[i].ObjectLocalID)
		    				{
		    					((OpenSim.world.Primitive)ent).UpdateShape(shape.ObjectData[i]);
		    				}
		    			}
		    		}
		    		break;
		    	case PacketType.MultipleObjectUpdate :
		    		MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)Pack;

		    		for( int i = 0; i < multipleupdate.ObjectData.Length; i++ )
		    		{
		    			if( multipleupdate.ObjectData[ i ].Type == 9 ) //change position
		    			{
		    				libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[ i ].Data, 0 );
		    				foreach (Entity ent in OpenSim_Main.local_world.Entities.Values)
		    				{
		    					if(ent.localid == multipleupdate.ObjectData[ i ].ObjectLocalID)
		    					{
		    						((OpenSim.world.Primitive)ent).UpdatePosition( pos);
		    						
		    					}
		    				}
		    				
		    				//should update stored position of the prim
		    			}
		    			else if(multipleupdate.ObjectData[i].Type == 10 )//rotation
		    			{
		    				libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 0, true);
		    				foreach (Entity ent in OpenSim_Main.local_world.Entities.Values)
		    				{
		    					if(ent.localid == multipleupdate.ObjectData[ i ].ObjectLocalID)
		    					{
		    						ent.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.W);
		    						((OpenSim.world.Primitive)ent).UpdateFlag = true;
		    					}
		    				}
		    			}
		    			else if(multipleupdate.ObjectData[i].Type == 13 )//scale
		    			{
		    				
		    				libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[ i ].Data, 12 );
		    				foreach (Entity ent in OpenSim_Main.local_world.Entities.Values)
		    				{
		    					if(ent.localid == multipleupdate.ObjectData[ i ].ObjectLocalID)
		    					{ 
		    						((OpenSim.world.Primitive)ent).Scale = scale;
		    					}
		    				}
		    			}
		    		}
		    		break;
		    	case PacketType.TransferRequest:
		    		//Console.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got transfer request");
		    		TransferRequestPacket transfer = (TransferRequestPacket)Pack;
		    		OpenSim_Main.sim.assetCache.AddAssetRequest(this, transfer);
		    		break;
		    	case PacketType.AgentUpdate:
		    		ClientAvatar.HandleUpdate((AgentUpdatePacket)Pack);
		    		break;
		    	case PacketType.LogoutRequest:
		    		ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got a logout request");
		    		//tell all clients to kill our object
		    		KillObjectPacket kill = new KillObjectPacket();
		    		kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
		    		kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
		    		kill.ObjectData[0].ID = this.ClientAvatar.localid;
		    		foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
		    			client.OutPacket(kill);
		    		}
		    		OpenSim_Main.gridServers.GridServer.LogoutSession(this.SessionID, this.AgentID, this.CircuitCode);
		    		lock(OpenSim_Main.local_world.Entities) {
		    			OpenSim_Main.local_world.Entities.Remove(this.AgentID);
		    		}
		    		//need to do other cleaning up here too
		    		OpenSim_Main.sim.ClientThreads.Remove(this.userEP);
		    		this.ClientThread.Abort();
		    		break;
		    	case PacketType.ChatFromViewer:
		    		ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;
		    		if(Helpers.FieldToString(inchatpack.ChatData.Message)=="") break;

		    		System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		    		libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
		    		reply.ChatData.Audible = 1;
		    		reply.ChatData.Message = inchatpack.ChatData.Message;
		    		reply.ChatData.ChatType = 1;
		    		reply.ChatData.SourceType = 1;
		    		reply.ChatData.Position = this.ClientAvatar.position;
		    		reply.ChatData.FromName = _enc.GetBytes(this.ClientAvatar.firstname + " " + this.ClientAvatar.lastname + "\0");
		    		reply.ChatData.OwnerID = this.AgentID;
		    		reply.ChatData.SourceID = this.AgentID;
		    		foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
		    			client.OutPacket(reply);
		    		}
		    		break;
		    }
		}

	 	private void ResendUnacked()
	 	{
	 			int now = Environment.TickCount;

	 			lock (NeedAck)
	 			{
	 				foreach (Packet packet in NeedAck.Values)
	 				{
	 					if ((now - packet.TickCount > RESEND_TIMEOUT) && (!packet.Header.Resent))
	 					{
	 						ServerConsole.MainConsole.Instance.WriteLine("Resending " + packet.Type.ToString() + " packet, " +
	 						 (now - packet.TickCount) + "ms have passed");

	 						packet.Header.Resent = true;
	 						OutPacket(packet);
	 					}
	 				}
	 			}
	 	}

	 	private void SendAcks()
	 	{
	 		lock (PendingAcks)
	 		{
	 			if (PendingAcks.Count > 0)
	 			{
	 				if (PendingAcks.Count > 250)
	 				{
	 					// FIXME: Handle the odd case where we have too many pending ACKs queued up
	 					ServerConsole.MainConsole.Instance.WriteLine("Too many ACKs queued up!");
	 					return;
	 				}
					
					ServerConsole.MainConsole.Instance.WriteLine("Sending PacketAck");
					

	 				int i = 0;
	 				PacketAckPacket acks = new PacketAckPacket();
	 				acks.Packets = new PacketAckPacket.PacketsBlock[PendingAcks.Count];

	 				foreach (uint ack in PendingAcks.Values)
	 				{
	 					acks.Packets[i] = new PacketAckPacket.PacketsBlock();
	 					acks.Packets[i].ID = ack;
	 					i++;
	 				}

	 				acks.Header.Reliable = false;
	 				OutPacket(acks);

	 				PendingAcks.Clear();
	 			}
	 		}
	 	}

	 	private void AckTimer_Elapsed(object sender, ElapsedEventArgs ea)
	 	{
			SendAcks();
			ResendUnacked();
		}

		public void ProcessOutPacket(Packet Pack) {

	 			// Keep track of when this packet was sent out
	 			Pack.TickCount = Environment.TickCount;

	 			if (!Pack.Header.Resent)
	 			{
	 				// Set the sequence number
	 				lock (SequenceLock)
	 				{
	 					if (Sequence >= MAX_SEQUENCE)
	 						Sequence = 1;
	 					else
	 						Sequence++;
	 					Pack.Header.Sequence = Sequence;
	 				}

					if (Pack.Header.Reliable)  //DIRTY HACK
	 				{
	 					lock (NeedAck)
	 					{
	 						if (!NeedAck.ContainsKey(Pack.Header.Sequence))
	 						{
	 							NeedAck.Add(Pack.Header.Sequence, Pack);
	 						}
	 						else
	 						{
	 							//  Client.Log("Attempted to add a duplicate sequence number (" +
	 							//     packet.Header.Sequence + ") to the NeedAck dictionary for packet type " +
	 							//      packet.Type.ToString(), Helpers.LogLevel.Warning);
	 						}
	 					}

	 					// Don't append ACKs to resent packets, in case that's what was causing the
	 					// delivery to fail
	 					if (!Pack.Header.Resent)
	 					{
	 						// Append any ACKs that need to be sent out to this packet
	 						lock (PendingAcks)
	 						{
	 							if (PendingAcks.Count > 0 && PendingAcks.Count < MAX_APPENDED_ACKS &&
	 							    Pack.Type != PacketType.PacketAck &&
	 							    Pack.Type != PacketType.LogoutRequest)
	 							{
	 								Pack.Header.AckList = new uint[PendingAcks.Count];
	 								int i = 0;
	 								
	 								foreach (uint ack in PendingAcks.Values)
	 								{
	 									Pack.Header.AckList[i] = ack;
	 									i++;
	 								}

	 								PendingAcks.Clear();
	 								Pack.Header.AppendedAcks = true;
	 							}
	 						}
	 					}
	 				}
	 			}

			//ServerConsole.MainConsole.Instance.WriteLine("OUT: \n" + Pack.ToString());

		    byte[] ZeroOutBuffer = new byte[4096];
		    byte[] sendbuffer; 
		    sendbuffer = Pack.ToBytes();

		    try {
			if (Pack.Header.Zerocoded) {
                        	int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                        	OpenSim_Main.Server.SendTo(ZeroOutBuffer, packetsize, SocketFlags.None,userEP);
                	} else {
				OpenSim_Main.Server.SendTo(sendbuffer, sendbuffer.Length, SocketFlags.None,userEP);
			}
		    } catch (Exception) {
			ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " + userEP.ToString() + " - killing thread");
			ClientThread.Abort();
		    }
	
		}

		public void InPacket(Packet NewPack) {
	 		// Handle appended ACKs
	 		if (NewPack.Header.AppendedAcks)
	 		{
	 			lock (NeedAck)
	 			{
	 				foreach (uint ack in NewPack.Header.AckList)
	 				{
	 					NeedAck.Remove(ack);
	 				}
	 			}
	 		}

	 		// Handle PacketAck packets
	 		if (NewPack.Type == PacketType.PacketAck)
	 		{
	 			PacketAckPacket ackPacket = (PacketAckPacket)NewPack;

	 			lock (NeedAck)
	 			{
	 				foreach (PacketAckPacket.PacketsBlock block in ackPacket.Packets)
	 				{
	 					NeedAck.Remove(block.ID);
	 				}
	 			}
	 		} else if( ( NewPack.Type == PacketType.StartPingCheck ) ) {
	 			//reply to pingcheck
	 			libsecondlife.Packets.StartPingCheckPacket startPing = (libsecondlife.Packets.StartPingCheckPacket)NewPack;
	 			libsecondlife.Packets.CompletePingCheckPacket endPing = new CompletePingCheckPacket();
	 			endPing.PingID.PingID = startPing.PingID.PingID;
	 			OutPacket(endPing);
	 		}
	 		else
	 		{
				QueItem item = new QueItem();
	                        item.Packet = NewPack;
                	        item.Incoming = true;
				this.PacketQueue.Enqueue(item);
	 		}
	 		
		}

		public void OutPacket(Packet NewPack) {
			QueItem item = new QueItem();
                        item.Packet = NewPack;
                        item.Incoming = false;
                        this.PacketQueue.Enqueue(item);
		}

		public OpenSimClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack) {
	        ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs - Started up new client thread to handle incoming request");
			cirpack = initialcirpack;
			userEP = remoteEP;
			PacketQueue = new BlockingQueue<QueItem>();
			AckTimer = new System.Timers.Timer(500);
	 		AckTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
			AckTimer.Start();

			ClientThread = new Thread(new ThreadStart(AuthUser));
        	        ClientThread.IsBackground = true;
			ClientThread.Start();
		}
		
		private void ClientLoop() {
			ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:ClientLoop() - Entered loop");
			while(true) {
				QueItem nextPacket = PacketQueue.Dequeue();
				if(nextPacket.Incoming)
				{
					//is a incoming packet
					ProcessInPacket(nextPacket.Packet);
				}
				else
				{
					//is a out going packet
					ProcessOutPacket(nextPacket.Packet);
				}	
			}
		}

		private void InitNewClient() {
			ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:InitNewClient() - Adding viewer agent to world");
			OpenSim_Main.local_world.AddViewerAgent(this);
			world.Entity tempent=OpenSim_Main.local_world.Entities[this.AgentID];
			this.ClientAvatar=(world.Avatar)tempent;
		}
		
		private void AuthUser() 
		{
			AuthenticateResponse sessionInfo = OpenSim_Main.gridServers.GridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
			if(!sessionInfo.Authorised)
			{
				//session/circuit not authorised
				ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:AuthUser() - New user request denied to " + userEP.ToString());
				ClientThread.Abort();
			}
			else
			{
				ServerConsole.MainConsole.Instance.WriteLine("OpenSimClient.cs:AuthUser() - Got authenticated connection from " + userEP.ToString());
				//session is authorised
				this.AgentID=cirpack.CircuitCode.ID;
				this.SessionID=cirpack.CircuitCode.SessionID;
				this.CircuitCode=cirpack.CircuitCode.Code;
				InitNewClient();
				this.ClientAvatar.firstname = sessionInfo.LoginInfo.First;
				this.ClientAvatar.lastname = sessionInfo.LoginInfo.Last;
				ClientLoop();	
			}
	 	}
	}

}
