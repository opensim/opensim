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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.Xfer
{
    public class XferModule : IRegionModule, IXfer
    {
        private Scene m_scene;
        private Dictionary<string, XferRequest> Requests = new Dictionary<string, XferRequest>();
        private List<XferRequest> RequestTime = new List<XferRequest>();
        public Dictionary<string, byte[]> NewFiles = new Dictionary<string, byte[]>();
        public Dictionary<ulong, XferDownLoad> Transfers = new Dictionary<ulong, XferDownLoad>();
        

        public struct XferRequest
        {
            public IClientAPI remoteClient;
            public ulong xferID;
            public string fileName;
            public DateTime timeStamp;
        }
       
        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;

            m_scene.RegisterModuleInterface<IXfer>(this);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "XferModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region IXfer Members

        public bool AddNewFile(string fileName, byte[] data)
        {
            lock (NewFiles)
            {
                if (NewFiles.ContainsKey(fileName))
                {
                    NewFiles[fileName] = data;
                }
                else
                {
                    NewFiles.Add(fileName, data);
                }
            }

            if (Requests.ContainsKey(fileName))
            {
                RequestXfer(Requests[fileName].remoteClient, Requests[fileName].xferID, fileName);
                Requests.Remove(fileName);
            }

            return true;
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            client.OnRequestXfer += RequestXfer;
            client.OnConfirmXfer += AckPacket;
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
                    if (!Transfers.ContainsKey(xferID))
                    {
                        byte[] fileData = NewFiles[fileName];
                        XferDownLoad transaction = new XferDownLoad(fileName, fileData, xferID, remoteClient);

                        Transfers.Add(xferID, transaction);
                        NewFiles.Remove(fileName);

                        if (transaction.StartSend())
                        {
                            Transfers.Remove(xferID);
                        }
                    }
                }
                else
                {
                    if (RequestTime.Count > 0)
                    {
                        TimeSpan ts = new TimeSpan(DateTime.UtcNow.Ticks - RequestTime[0].timeStamp.Ticks);
                        if (ts.TotalSeconds > 30)
                        {
                            Requests.Remove(RequestTime[0].fileName);
                            RequestTime.RemoveAt(0);
                        }
                    }

                    if (!Requests.ContainsKey(fileName))
                    {
                        XferRequest nRequest = new XferRequest();
                        nRequest.remoteClient = remoteClient;
                        nRequest.xferID = xferID;
                        nRequest.fileName = fileName;
                        nRequest.timeStamp = DateTime.UtcNow;
                        Requests.Add(fileName, nRequest);
                        RequestTime.Add(nRequest);
                    }
                    
                }
            }
        }

        public void AckPacket(IClientAPI remoteClient, ulong xferID, uint packet)
        {
            if (Transfers.ContainsKey(xferID))
            {
                if (Transfers[xferID].AckPacket(packet))
                {
                    {
                        Transfers.Remove(xferID);
                    }
                }
            }
        }

        #region Nested type: XferDownLoad

        public class XferDownLoad
        {
            public IClientAPI Client;
            private bool complete;
            public byte[] Data = new byte[0];
            public int DataPointer = 0;
            public string FileName = String.Empty;
            public uint Packet = 0;
            public uint Serial = 1;
            public ulong XferID = 0;

            public XferDownLoad(string fileName, byte[] data, ulong xferID, IClientAPI client)
            {
                FileName = fileName;
                Data = data;
                XferID = xferID;
                Client = client;
            }

            public XferDownLoad()
            {
            }

            /// <summary>
            /// Start a transfer
            /// </summary>
            /// <returns>True if the transfer is complete, false if not</returns>
            public bool StartSend()
            {
                if (Data.Length < 1000)
                {
                    // for now (testing) we only support files under 1000 bytes
                    byte[] transferData = new byte[Data.Length + 4];
                    Array.Copy(Utils.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, Data.Length);
                    Client.SendXferPacket(XferID, 0 + 0x80000000, transferData);
                    complete = true;
                }
                else
                {
                    byte[] transferData = new byte[1000 + 4];
                    Array.Copy(Utils.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, 1000);
                    Client.SendXferPacket(XferID, 0, transferData);
                    Packet++;
                    DataPointer = 1000;
                }

                return complete;
            }

            /// <summary>
            /// Respond to an ack packet from the client
            /// </summary>
            /// <param name="packet"></param>
            /// <returns>True if the transfer is complete, false otherwise</returns>
            public bool AckPacket(uint packet)
            {
                if (!complete)
                {
                    if ((Data.Length - DataPointer) > 1000)
                    {
                        byte[] transferData = new byte[1000];
                        Array.Copy(Data, DataPointer, transferData, 0, 1000);
                        Client.SendXferPacket(XferID, Packet, transferData);
                        Packet++;
                        DataPointer += 1000;
                    }
                    else
                    {
                        byte[] transferData = new byte[Data.Length - DataPointer];
                        Array.Copy(Data, DataPointer, transferData, 0, Data.Length - DataPointer);
                        uint endPacket = Packet |= (uint) 0x80000000;
                        Client.SendXferPacket(XferID, endPacket, transferData);
                        Packet++;
                        DataPointer += (Data.Length - DataPointer);

                        complete = true;
                    }
                }

                return complete;
            }
        }

        #endregion
    }
}
