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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Region.ClientStack
{
    public partial class ClientView
    {
        protected BlockingQueue<QueItem> PacketQueue;

        protected Queue<QueItem> IncomingPacketQueue;
        protected Queue<QueItem> OutgoingPacketQueue;
        protected Queue<QueItem> ResendOutgoingPacketQueue;
        protected Queue<QueItem> LandOutgoingPacketQueue;
        protected Queue<QueItem> WindOutgoingPacketQueue;
        protected Queue<QueItem> CloudOutgoingPacketQueue;
        protected Queue<QueItem> TaskOutgoingPacketQueue;
        protected Queue<QueItem> TextureOutgoingPacketQueue;
        protected Queue<QueItem> AssetOutgoingPacketQueue;

        protected Dictionary<uint, uint> PendingAcks = new Dictionary<uint, uint>();
        protected Dictionary<uint, Packet> NeedAck = new Dictionary<uint, Packet>();

        protected Timer AckTimer;
        protected uint Sequence = 0;
        protected object SequenceLock = new object();
        protected const int MAX_APPENDED_ACKS = 10;
        protected const int RESEND_TIMEOUT = 4000;
        protected const int MAX_SEQUENCE = 0xFFFFFF;

        private uint m_circuitCode;
        public EndPoint userEP;

        protected PacketServer m_networkServer;

        public uint CircuitCode
        {
            get { return m_circuitCode; }
            set { m_circuitCode = value; }
        }

        protected virtual void ProcessOutPacket(Packet Pack)
        {
            // Keep track of when this packet was sent out
            Pack.TickCount = System.Environment.TickCount;

            if (!Pack.Header.Resent)
            {
                // Set the sequence number
                lock (SequenceLock)
                {
                    if (Sequence >= MAX_SEQUENCE)
                    {
                        Sequence = 1;
                    }
                    else
                    {
                        Sequence++;
                    }

                    Pack.Header.Sequence = Sequence;
                }

                if (Pack.Header.Reliable) //DIRTY HACK
                {
                    lock (NeedAck)
                    {
                        if (!NeedAck.ContainsKey(Pack.Header.Sequence))
                        {
                            try
                            {
                                NeedAck.Add(Pack.Header.Sequence, Pack);
                            }
                            catch (Exception e) // HACKY
                            {
                                e.ToString();
                                // Ignore
                                // Seems to throw a exception here occasionally
                                // of 'duplicate key' despite being locked.
                                // !?!?!?
                            }
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

            byte[] ZeroOutBuffer = new byte[4096];
            byte[] sendbuffer;
            sendbuffer = Pack.ToBytes();

            try
            {
                if (Pack.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                    m_networkServer.SendPacketTo(ZeroOutBuffer, packetsize, SocketFlags.None, m_circuitCode); //userEP);
                }
                else
                {
                    m_networkServer.SendPacketTo(sendbuffer, sendbuffer.Length, SocketFlags.None, m_circuitCode);
                    //userEP);
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("client",
                                      "ClientView.PacketQueue.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " +
                                      userEP.ToString() + " - killing thread");
                MainLog.Instance.Error(e.ToString());
                KillThread();
            }
        }

        public virtual void InPacket(Packet NewPack)
        {
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
                PacketAckPacket ackPacket = (PacketAckPacket) NewPack;

                lock (NeedAck)
                {
                    foreach (PacketAckPacket.PacketsBlock block in ackPacket.Packets)
                    {
                        NeedAck.Remove(block.ID);
                    }
                }
            }
            else if ((NewPack.Type == PacketType.StartPingCheck))
            {
                //reply to pingcheck
                StartPingCheckPacket startPing = (StartPingCheckPacket) NewPack;
                CompletePingCheckPacket endPing = new CompletePingCheckPacket();
                endPing.PingID.PingID = startPing.PingID.PingID;
                OutPacket(endPing, ThrottleOutPacketType.Task);
            }
            else
            {
                QueItem item = new QueItem();
                item.Packet = NewPack;
                item.Incoming = true;
                PacketQueue.Enqueue(item);
            }
        }

        public virtual void OutPacket(Packet NewPack, ThrottleOutPacketType throttlePacketType)
        {
            QueItem item = new QueItem();
            item.Packet = NewPack;
            item.Incoming = false;
            item.throttleType = throttlePacketType; // Packet throttle type

            // The idea..  is if the packet throttle queues are empty and the client is under throttle for the type.
            // Queue it up directly.
            switch (throttlePacketType)
            {
                case ThrottleOutPacketType.Resend:
                    if (ResendBytesSent <= ((int)(ResendthrottleOutbound / throttleTimeDivisor)) && ResendOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        ResendBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        ResendOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Texture:
                    if (TextureBytesSent <= ((int)(TexturethrottleOutbound / throttleTimeDivisor)) && TextureOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        TextureBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        TextureOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Task:
                    if (TaskBytesSent <= ((int)(TexturethrottleOutbound / throttleTimeDivisor)) && TaskOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        TaskBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        TaskOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Land:
                    if (LandBytesSent <= ((int)(LandthrottleOutbound / throttleTimeDivisor)) && LandOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        LandBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        LandOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Asset:
                    if (AssetBytesSent <= ((int)(AssetthrottleOutbound / throttleTimeDivisor)) && AssetOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        AssetBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        AssetOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Cloud:
                    if (CloudBytesSent <= ((int)(CloudthrottleOutbound / throttleTimeDivisor)) && CloudOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        CloudBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        CloudOutgoingPacketQueue.Enqueue(item);
                    }
                    break;
                case ThrottleOutPacketType.Wind:
                    if (WindBytesSent <= ((int)(WindthrottleOutbound / throttleTimeDivisor)) && WindOutgoingPacketQueue.Count == 0)
                    {
                        bytesSent += item.Packet.ToBytes().Length;
                        WindBytesSent += item.Packet.ToBytes().Length;
                        PacketQueue.Enqueue(item);
                    }
                    else
                    {
                        WindOutgoingPacketQueue.Enqueue(item);
                    }
                    break;

                default:
                    
                    // Acknowledgements and other such stuff should go directly to the blocking Queue
                    // Throttling them may and likely 'will' be problematic
                    PacketQueue.Enqueue(item); 
                    break;
            }
            //OutgoingPacketQueue.Enqueue(item);
        }

        # region Low Level Packet Methods

        protected void ack_pack(Packet Pack)
        {
            if (Pack.Header.Reliable)
            {
                PacketAckPacket ack_it = new PacketAckPacket();
                ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
                ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
                ack_it.Packets[0].ID = Pack.Header.Sequence;
                ack_it.Header.Reliable = false;

                OutPacket(ack_it, ThrottleOutPacketType.Unknown);
            }
            /*
            if (Pack.Header.Reliable)
            {
                lock (PendingAcks)
                {
                    uint sequence = (uint)Pack.Header.Sequence;
                    if (!PendingAcks.ContainsKey(sequence)) { PendingAcks[sequence] = sequence; }
                }
            }*/
        }

        protected void ResendUnacked()
        {
            int now = System.Environment.TickCount;

            lock (NeedAck)
            {
                foreach (Packet packet in NeedAck.Values)
                {
                    if ((now - packet.TickCount > RESEND_TIMEOUT) && (!packet.Header.Resent))
                    {
                        MainLog.Instance.Verbose("Resending " + packet.Type.ToString() + " packet, " +
                                                 (now - packet.TickCount) + "ms have passed");

                        packet.Header.Resent = true;
                        OutPacket(packet, ThrottleOutPacketType.Resend);
                    }
                }
            }
        }

        protected void SendAcks()
        {
            lock (PendingAcks)
            {
                if (PendingAcks.Count > 0)
                {
                    if (PendingAcks.Count > 250)
                    {
                        // FIXME: Handle the odd case where we have too many pending ACKs queued up
                        MainLog.Instance.Verbose("Too many ACKs queued up!");
                        return;
                    }

                    //OpenSim.Framework.Console.MainLog.Instance.WriteLine("Sending PacketAck");


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
                    OutPacket(acks, ThrottleOutPacketType.Unknown);

                    PendingAcks.Clear();
                }
            }
        }

        protected void AckTimer_Elapsed(object sender, ElapsedEventArgs ea)
        {
            SendAcks();
            ResendUnacked();
        }

        #endregion
    }
}