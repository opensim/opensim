using System;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using Nwc.XmlRpc;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Timers;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.world;
using OpenSim.Assets;

namespace OpenSim
{
    public partial class SimClient
    {

        public void EnableNeighbours()
        {
            if ((this.m_gridServer.GetName() == "Remote") && (!this.m_child))
            {
                Hashtable SimParams;
                ArrayList SendParams;
                XmlRpcRequest GridReq;
                XmlRpcResponse GridResp;
                List<Packet> enablePackets = new List<Packet>();

                RemoteGridBase gridServer = (RemoteGridBase)this.m_gridServer;

                    foreach (Hashtable neighbour in gridServer.neighbours)
                    {
                        string neighbourIPStr = (string)neighbour["sim_ip"];
                        System.Net.IPAddress neighbourIP = System.Net.IPAddress.Parse(neighbourIPStr);
                        ushort neighbourPort = (ushort)Convert.ToInt32(neighbour["sim_port"]);
                        string reqUrl = "http://" + neighbourIPStr + ":" + neighbourPort.ToString();
                        
                        Console.WriteLine(reqUrl);
                        
                        SimParams = new Hashtable();
                        SimParams["session_id"] = this.SessionID.ToString();
                        SimParams["secure_session_id"] = this.SecureSessionID.ToString();
                        SimParams["firstname"] = this.ClientAvatar.firstname;
                        SimParams["lastname"] = this.ClientAvatar.lastname;
                        SimParams["agent_id"] = this.AgentID.ToString();
                        SimParams["circuit_code"] = (Int32)this.CircuitCode;
                        SimParams["child_agent"] = "1";
                        SendParams = new ArrayList();
                        SendParams.Add(SimParams);

                        GridReq = new XmlRpcRequest("expect_user", SendParams);
                        GridResp = GridReq.Send(reqUrl, 3000);
                        EnableSimulatorPacket enablesimpacket = new EnableSimulatorPacket();
                        enablesimpacket.SimulatorInfo = new EnableSimulatorPacket.SimulatorInfoBlock();
                        enablesimpacket.SimulatorInfo.Handle = Helpers.UIntsToLong((uint)(Convert.ToInt32(neighbour["region_locx"]) * 256), (uint)(Convert.ToInt32(neighbour["region_locy"]) * 256));
                        
                        
                        byte[] byteIP = neighbourIP.GetAddressBytes();
                        enablesimpacket.SimulatorInfo.IP = (uint)byteIP[3] << 24;
                        enablesimpacket.SimulatorInfo.IP += (uint)byteIP[2] << 16;
                        enablesimpacket.SimulatorInfo.IP += (uint)byteIP[1] << 8;
                        enablesimpacket.SimulatorInfo.IP += (uint)byteIP[0];
                        enablesimpacket.SimulatorInfo.Port = neighbourPort;
                        enablePackets.Add(enablesimpacket);
                    }
                    Thread.Sleep(3000);
                    foreach (Packet enable in enablePackets)
                    {
                        this.OutPacket(enable);
                    }
                    enablePackets.Clear();
                
            }
        }

        public void CrossSimBorder(LLVector3 avatarpos)
        {		// VERY VERY BASIC

            LLVector3 newpos = avatarpos;
            uint neighbourx = this.m_regionData.RegionLocX;
            uint neighboury = this.m_regionData.RegionLocY;

            if (avatarpos.X < 0)
            {
                neighbourx -= 1;
                newpos.X = 254;
            }
            if (avatarpos.X > 255)
            {
                neighbourx += 1;
                newpos.X = 1;
            }
            if (avatarpos.Y < 0)
            {
                neighboury -= 1;
                newpos.Y = 254;
            }
            if (avatarpos.Y > 255)
            {
                neighboury += 1;
                newpos.Y = 1;
            }
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "SimClient.cs:CrossSimBorder() - Crossing border to neighbouring sim at [" + neighbourx.ToString() + "," + neighboury.ToString() + "]");

            Hashtable SimParams;
            ArrayList SendParams;
            XmlRpcRequest GridReq;
            XmlRpcResponse GridResp;
            foreach (Hashtable borderingSim in ((RemoteGridBase)m_gridServer).neighbours)
            {
                if (((string)borderingSim["region_locx"]).Equals(neighbourx.ToString()) && ((string)borderingSim["region_locy"]).Equals(neighboury.ToString()))
                {
                    SimParams = new Hashtable();
                    SimParams["firstname"] = this.ClientAvatar.firstname;
                    SimParams["lastname"] = this.ClientAvatar.lastname;
                    SimParams["circuit_code"] = this.CircuitCode.ToString();
                    SimParams["pos_x"] = newpos.X.ToString();
                    SimParams["pos_y"] = newpos.Y.ToString();
                    SimParams["pos_z"] = newpos.Z.ToString();
                    SendParams = new ArrayList();
                    SendParams.Add(SimParams);

                    GridReq = new XmlRpcRequest("agent_crossing", SendParams);
                    GridResp = GridReq.Send("http://" + borderingSim["sim_ip"] + ":" + borderingSim["sim_port"], 3000);

                    CrossedRegionPacket NewSimPack = new CrossedRegionPacket();
                    NewSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
                    NewSimPack.AgentData.AgentID = this.AgentID;
                    NewSimPack.AgentData.SessionID = this.SessionID;
                    NewSimPack.Info = new CrossedRegionPacket.InfoBlock();
                    NewSimPack.Info.Position = newpos;
                    NewSimPack.Info.LookAt = new LLVector3(0.99f, 0.042f, 0);	// copied from Avatar.cs - SHOULD BE DYNAMIC!!!!!!!!!!
                    NewSimPack.RegionData = new libsecondlife.Packets.CrossedRegionPacket.RegionDataBlock();
                    NewSimPack.RegionData.RegionHandle = Helpers.UIntsToLong((uint)(Convert.ToInt32(borderingSim["region_locx"]) * 256), (uint)(Convert.ToInt32(borderingSim["region_locy"]) * 256));
                    System.Net.IPAddress neighbourIP = System.Net.IPAddress.Parse((string)borderingSim["sim_ip"]);
                    byte[] byteIP = neighbourIP.GetAddressBytes();
                    NewSimPack.RegionData.SimIP = (uint)byteIP[3] << 24;
                    NewSimPack.RegionData.SimIP += (uint)byteIP[2] << 16;
                    NewSimPack.RegionData.SimIP += (uint)byteIP[1] << 8;
                    NewSimPack.RegionData.SimIP += (uint)byteIP[0];
                    NewSimPack.RegionData.SimPort = (ushort)Convert.ToInt32(borderingSim["sim_port"]);
                    NewSimPack.RegionData.SeedCapability = new byte[0];
                    lock (PacketQueue)
                    {
                        ProcessOutPacket(NewSimPack);
                        DowngradeClient();
                    }
                }
            }
        }
   }
}
