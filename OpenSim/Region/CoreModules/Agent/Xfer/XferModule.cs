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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Reflection;
using System.Threading;
using Nini.Config;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;

namespace OpenSim.Region.CoreModules.Agent.Xfer
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XferModule")]
    public class XferModule : INonSharedRegionModule, IXfer
    {
        private Scene m_scene;
        private Dictionary<string, FileData> NewFiles = new Dictionary<string, FileData>();
        private Dictionary<ulong, XferDownLoad> Transfers = new Dictionary<ulong, XferDownLoad>();
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private object  timeTickLock = new object();
        private double  lastTimeTick = 0.0;
        private double  lastFilesExpire = 0.0;
        private bool    inTimeTick = false;

        public struct XferRequest
        {
            public IClientAPI remoteClient;
            public ulong xferID;
            public string fileName;
            public DateTime timeStamp;
        }

        private class FileData
        {
            public byte[] Data;
            public int refsCount;
            public double timeStampMS;
        }

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            lastTimeTick = Util.GetTimeStampMS() + 30000.0;
            lastFilesExpire = lastTimeTick + 180000.0;
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IXfer>(this);
            m_scene.EventManager.OnNewClient += NewClient;
            m_scene.EventManager.OnRegionHeartbeatEnd += OnTimeTick;
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.EventManager.OnNewClient -= NewClient;
            m_scene.EventManager.OnRegionHeartbeatEnd -= OnTimeTick;

            m_scene.UnregisterModuleInterface<IXfer>(this);
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "XferModule"; }
        }

        #endregion

        public void OnTimeTick(Scene scene)
        {
            // we are on a heartbeat thread we there can be several
            if(Monitor.TryEnter(timeTickLock))
            {
                if(!inTimeTick)
                {
                    double now = Util.GetTimeStampMS();
                    if(now - lastTimeTick > 1750.0)
                    {

                        if(Transfers.Count == 0 && NewFiles.Count == 0)
                            lastTimeTick = now;
                        else
                        {
                            inTimeTick = true;

                            //don't overload busy heartbeat
                            WorkManager.RunInThreadPool(
                                delegate
                                {
                                    transfersTimeTick(now);
                                    expireFiles(now);

                                    lastTimeTick = now;
                                    inTimeTick = false;
                                },
                                null,
                                "XferTimeTick");
                        }
                    }
                }
                Monitor.Exit(timeTickLock);
            }
        }
        #region IXfer Members

        /// <summary>
        /// Let the Xfer module know about a file that the client is about to request.
        /// Caller is responsible for making sure that the file is here before
        /// the client starts the XferRequest.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool AddNewFile(string fileName, byte[] data)
        {
            lock (NewFiles)
            {
                double now = Util.GetTimeStampMS();
                if (NewFiles.ContainsKey(fileName))
                {
                    NewFiles[fileName].refsCount++;
                    NewFiles[fileName].Data = data;
                    NewFiles[fileName].timeStampMS = now;
                }
                else
                {
                    FileData fd = new FileData();
                    fd.refsCount = 1;
                    fd.Data = data;
                    fd.timeStampMS = now;
                    NewFiles.Add(fileName, fd);
                }
            }
            return true;
        }

        #endregion
        public void expireFiles(double now)
        {
            lock (NewFiles)
            {
                // hopefully we will not have many files so nasty code will do it
                if(now - lastFilesExpire > 120000.0)
                {
                    lastFilesExpire = now;
                    List<string> expires = new List<string>();
                    foreach(string fname in NewFiles.Keys)
                    {
                        if(NewFiles[fname].refsCount == 0 && now - NewFiles[fname].timeStampMS > 120000.0)
                            expires.Add(fname);
                    }
                    foreach(string fname in expires)
                        NewFiles.Remove(fname);
                }
            }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnRequestXfer += RequestXfer;
            client.OnConfirmXfer += AckPacket;
            client.OnAbortXfer += AbortXfer;
        }

        public void OnClientClosed(IClientAPI client)
        {
            client.OnRequestXfer -= RequestXfer;
            client.OnConfirmXfer -= AckPacket;
            client.OnAbortXfer -= AbortXfer;
        }

        private void RemoveOrDecrementFile(string fileName)
        {
            // NewFiles must be locked

            if (NewFiles.ContainsKey(fileName))
            {
                if (NewFiles[fileName].refsCount == 1)
                    NewFiles.Remove(fileName);
                else
                    NewFiles[fileName].refsCount--;
            }
        }

        public void transfersTimeTick(double now)
        {
            XferDownLoad[] xfrs;
            lock(Transfers)
            {
                if(Transfers.Count == 0)
                    return;

                xfrs = new XferDownLoad[Transfers.Count];
                Transfers.Values.CopyTo(xfrs,0);
            }
            foreach(XferDownLoad xfr in xfrs)
            {
                if(xfr.checkTime(now))
                {
                    ulong xfrID = xfr.XferID;
                    lock(Transfers)
                    {
                        if(Transfers.ContainsKey(xfrID))
                            Transfers.Remove(xfrID);
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="fileName"></param>
        public void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName)
        {
            lock (NewFiles)
            {
                if (NewFiles.ContainsKey(fileName))
                {
                    lock(Transfers)
                    {
                        if (!Transfers.ContainsKey(xferID))
                        {
                            byte[] fileData = NewFiles[fileName].Data;
                            int burstSize = remoteClient.GetAgentThrottleSilent((int)ThrottleOutPacketType.Asset) >> 11;
                            if(Transfers.Count > 1)
                                burstSize /= Transfers.Count;
                            XferDownLoad transaction =
                                new XferDownLoad(fileName, fileData, xferID, remoteClient, burstSize);

                            Transfers.Add(xferID, transaction);

                            transaction.StartSend();

                            // The transaction for this file is on its way
                            RemoveOrDecrementFile(fileName);
                        }
                    }
                }
                else
                    m_log.WarnFormat("[Xfer]: {0} not found", fileName);
            }
        }

        public void AckPacket(IClientAPI remoteClient, ulong xferID, uint packet)
        {
            lock (Transfers)
            {
                if (Transfers.ContainsKey(xferID))
                {
                    if (Transfers[xferID].AckPacket(packet))
                        Transfers.Remove(xferID);
                }
            }
        }

        public void AbortXfer(IClientAPI remoteClient, ulong xferID)
        {
            lock (Transfers)
            {
                if (Transfers.ContainsKey(xferID))
                {
                    Transfers[xferID].done();
                    Transfers.Remove(xferID);
                }
            }
        }

        #region Nested type: XferDownLoad

        public class XferDownLoad
        {
            public IClientAPI Client;
            public byte[] Data = new byte[0];
            public string FileName = String.Empty;
            public ulong XferID = 0;
            public bool isDeleted = false;

            private object myLock = new object();
            private double lastsendTimeMS;
            private int LastPacket;
            private int lastBytes;
            private int lastSentPacket;
            private int lastAckPacket;
            private int burstSize;
            private int retries = 0;

            public XferDownLoad(string fileName, byte[] data, ulong xferID, IClientAPI client, int burstsz)
            {
                FileName = fileName;
                Data = data;
                XferID = xferID;
                Client = client;
                burstSize = burstsz;
            }

            public XferDownLoad()
            {
            }

            public void done()
            {
                if(!isDeleted)
                {
                    Data = new byte[0];
                    isDeleted = true;
                }
            }

            /// <summary>
            /// Start a transfer
            /// </summary>
            /// <returns>True if the transfer is complete, false if not</returns>
            public void StartSend()
            {
                lock(myLock)
                {
                    if(Data.Length == 0) //??
                    {
                        LastPacket = 0;
                        lastBytes = 0;
                        burstSize = 0;
                    }
                    else
                    {
                        // payload of 1024bytes
                        LastPacket = Data.Length >> 10;
                        lastBytes = Data.Length & 0x3ff;
                        if(lastBytes == 0)
                        {
                            lastBytes = 1024;
                            LastPacket--;
                        }

                    }

                    lastAckPacket = -1;
                    lastSentPacket = -1;

                    double now = Util.GetTimeStampMS();

                    SendBurst(now);
                    return;
                }
            }

            private void SendBurst(double now)
            {
                int start = lastAckPacket + 1;
                int end = start + burstSize;
                if (end > LastPacket)
                    end = LastPacket;
                while(start <= end)
                    SendPacket(start++ , now);
            }

            private void SendPacket(int pkt, double now)
            {
                if(pkt > LastPacket)
                    return;

                int pktsize;
                uint pktid;
                if (pkt == LastPacket)
                {
                    pktsize = lastBytes;
                    pktid = (uint)pkt |  0x80000000u;
                }
                else
                {
                    pktsize = 1024;
                    pktid = (uint)pkt;
                }

                byte[] transferData;
                if(pkt == 0)
                {
                    transferData = new byte[pktsize + 4];
                    Array.Copy(Utils.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, pktsize);
                }
                else
                {
                    transferData = new byte[pktsize];
                    Array.Copy(Data, pkt << 10, transferData, 0, pktsize);
                }

                Client.SendXferPacket(XferID, pktid, transferData, false);

                lastSentPacket = pkt;
                lastsendTimeMS = now;
            }

            /// <summary>
            /// Respond to an ack packet from the client
            /// </summary>
            /// <param name="packet"></param>
            /// <returns>True if the transfer is complete, false otherwise</returns>
            public bool AckPacket(uint packet)
            {
                lock(myLock)
                {
                    if(isDeleted)
                        return true;

                    packet &=  0x7fffffff;
                    if(lastAckPacket < packet)
                        lastAckPacket = (int)packet;

                    if(lastAckPacket == LastPacket)
                    {
                        done();
                        return true;
                    }
                    double now = Util.GetTimeStampMS();
                    SendPacket(lastSentPacket + 1, now);
                    return false;
                }
            }

            public bool checkTime(double now)
            {
                if(Monitor.TryEnter(myLock))
                {
                    if(!isDeleted)
                    {
                        double timeMS = now - lastsendTimeMS;
                        if(timeMS > 60000.0)
                            done();
                        else if(timeMS > 3500.0 && retries++ < 3)
                        {
                            burstSize >>= 1;
                            SendBurst(now);
                        }
                    }

                    Monitor.Exit(myLock);
                    return isDeleted;
                }
                return false;
            }
        }

        #endregion
    }
}
