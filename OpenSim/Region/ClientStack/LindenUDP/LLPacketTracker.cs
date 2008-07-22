using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.LindenUDP
{

    public class LLPacketTracker
    {
        public delegate void PacketAcked(uint sequenceNumber);
        public event PacketAcked OnPacketAcked;

        protected List<uint> m_beenAcked = new List<uint>();

        protected TerrainPacketTracker[,] m_sentTerrainPackets = new TerrainPacketTracker[16, 16];
        protected Dictionary<LLUUID, PrimPacketTracker> m_sendPrimPackets = new Dictionary<LLUUID, PrimPacketTracker>();

        protected LLClientView m_parentClient;

        public LLPacketTracker(LLClientView parent)
        {
            m_parentClient = parent;
            OnPacketAcked += TerrainPacketAcked;
            //OnPacketAcked += PrimPacketAcked;
        }

        public void PacketAck(uint sequenceNumber)
        {
            lock (m_beenAcked)
            {
                m_beenAcked.Add(sequenceNumber);
            }
        }

        public void TrackTerrainPacket(uint sequenceNumber, int patchX, int patchY)
        {
            TrackTerrainPacket(sequenceNumber, patchX, patchY, false, null);
        }

        public void TrackTerrainPacket(uint sequenceNumber, int patchX, int patchY, bool keepResending, LayerDataPacket packet)
        {
            TerrainPacketTracker tracker = new TerrainPacketTracker();
            tracker.X = patchX;
            tracker.Y = patchY;
            tracker.SeqNumber = sequenceNumber;
            tracker.TimeSent = DateTime.Now;
            tracker.KeepResending = keepResending;
            tracker.Packet = packet;
            lock (m_sentTerrainPackets)
            {
                m_sentTerrainPackets[patchX, patchY] = tracker;
            }
        }

        public void TrackPrimPacket(uint sequenceNumber, LLUUID primID)
        {
            PrimPacketTracker tracker = new PrimPacketTracker();
            tracker.PrimID = primID;
            tracker.TimeSent = DateTime.Now;
            tracker.SeqNumber = sequenceNumber;
            lock (m_sendPrimPackets)
            {
                m_sendPrimPackets[primID] = tracker;
            }
        }

        public void TerrainPacketCheck()
        {
            DateTime now = DateTime.Now;
            List<TerrainPacketTracker> resendList = new List<TerrainPacketTracker>();
            lock (m_sentTerrainPackets)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        if (m_sentTerrainPackets[x, y] != null)
                        {
                            TerrainPacketTracker tracker = m_sentTerrainPackets[x, y];
                            if ((now - tracker.TimeSent) > TimeSpan.FromMinutes(1))
                            {
                                tracker.TimeSent = now;
                                m_sentTerrainPackets[x, y] = null;
                                resendList.Add(tracker);
                            }
                        }
                    }
                }
            }

            foreach (TerrainPacketTracker tracker in resendList)
            {
                if (!tracker.KeepResending)
                {
                    m_parentClient.TriggerTerrainUnackedEvent(tracker.X, tracker.Y);
                }
                else
                {
                    if (tracker.Packet != null)
                    {
                        tracker.Packet.Header.Resent = true;
                        m_parentClient.OutPacket(tracker.Packet, ThrottleOutPacketType.Resend);
                        tracker.TimeSent = DateTime.Now;
                        lock (m_sentTerrainPackets)
                        {
                            if (m_sentTerrainPackets[tracker.X, tracker.Y] == null)
                            {
                                m_sentTerrainPackets[tracker.X, tracker.Y] = tracker;
                            }
                        }
                    }
                }
            }
        }

        public void PrimPacketCheck()
        {
            DateTime now = DateTime.Now;
            List<PrimPacketTracker> resendList = new List<PrimPacketTracker>();
            List<PrimPacketTracker> ackedList = new List<PrimPacketTracker>();

            lock (m_sendPrimPackets)
            {
                foreach (PrimPacketTracker tracker in m_sendPrimPackets.Values)
                {
                    if (tracker.Acked)
                    {
                        ackedList.Add(tracker);
                    }
                    else if (((now - tracker.TimeSent) > TimeSpan.FromMinutes(1)) && (!tracker.Acked))
                    {
                        resendList.Add(tracker);
                    }
                }
            }

            foreach (PrimPacketTracker tracker in resendList)
            {
                lock (m_sendPrimPackets)
                {
                    m_sendPrimPackets.Remove(tracker.PrimID);
                }
                //call event
                Console.WriteLine("Prim packet not acked, " + tracker.PrimID.ToString());
            }
            
           
            RemovePrimTrackers(ackedList);
        }

        public void PrimTrackerCleanup()
        {
            List<PrimPacketTracker> ackedList = new List<PrimPacketTracker>();

            lock (m_sendPrimPackets)
            {
                foreach (PrimPacketTracker tracker in m_sendPrimPackets.Values)
                {
                    if (tracker.Acked)
                    {
                        ackedList.Add(tracker);
                    }
                }
            }
            Thread.Sleep(15); //give a little bit of time for other code to access list before we lock it again

            RemovePrimTrackers(ackedList);
        }

        protected void RemovePrimTrackers(List<PrimPacketTracker> ackedList)
        {
            lock (m_sendPrimPackets)
            {
                foreach (PrimPacketTracker tracker in ackedList)
                {
                    m_sendPrimPackets.Remove(tracker.PrimID);
                }
            }
        }

        protected void TerrainPacketAcked(uint sequence)
        {
            lock (m_sentTerrainPackets)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        if (m_sentTerrainPackets[x, y] != null)
                        {
                            if (m_sentTerrainPackets[x, y].SeqNumber == sequence)
                            {
                                m_sentTerrainPackets[x, y] = null;
                                return;
                            }
                        }
                    }
                }
            }
        }

        protected void PrimPacketAcked(uint sequence)
        {
            lock (m_sendPrimPackets)
            {
                foreach (PrimPacketTracker tracker in m_sendPrimPackets.Values)
                {
                    if (tracker.SeqNumber == sequence)
                    {
                        tracker.Acked = true;
                        break;
                    }
                }
            }
        }

        public void Process()
        {
            List<uint> ackedPackets = null;
            lock (m_beenAcked)
            {
                ackedPackets = new List<uint>(m_beenAcked);
                m_beenAcked.Clear();
            }

            if (ackedPackets != null)
            {
                foreach (uint packetId in ackedPackets)
                {
                    if (OnPacketAcked != null)
                    {
                        OnPacketAcked(packetId);
                    }
                }
            }

            // ackedPackets.Clear();
            ackedPackets = null;
        }

        public class TerrainPacketTracker
        {
            public uint SeqNumber = 0;
            public int X;
            public int Y;
            public DateTime TimeSent;
            public LayerDataPacket Packet;
            public bool KeepResending;
        }

        public class PrimPacketTracker
        {
            public uint SeqNumber = 0;
            public DateTime TimeSent;
            public LLUUID PrimID;
            public bool Acked = false;
        }
    }
}
