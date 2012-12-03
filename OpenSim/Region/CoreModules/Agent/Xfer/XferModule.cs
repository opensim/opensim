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
using Nini.Config;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
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
            public int Count;
        }
       
        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;

            m_scene.RegisterModuleInterface<IXfer>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.EventManager.OnNewClient -= NewClient;

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
                if (NewFiles.ContainsKey(fileName))
                {
                    NewFiles[fileName].Count++;
                    NewFiles[fileName].Data = data;
                }
                else
                {
                    FileData fd = new FileData();
                    fd.Count = 1;
                    fd.Data = data;
                    NewFiles.Add(fileName, fd);
                }
            }

            return true;
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            client.OnRequestXfer += RequestXfer;
            client.OnConfirmXfer += AckPacket;
            client.OnAbortXfer += AbortXfer;
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
                        byte[] fileData = NewFiles[fileName].Data;
                        XferDownLoad transaction = new XferDownLoad(fileName, fileData, xferID, remoteClient);
                        if (fileName.StartsWith("inventory_"))
                            transaction.isTaskInventory = true;

                        Transfers.Add(xferID, transaction);

                        if (transaction.StartSend())
                            RemoveXferData(xferID);

                        // The transaction for this file is either complete or on its way
                        RemoveOrDecrement(fileName);

                    }
                }
                else
                    m_log.WarnFormat("[Xfer]: {0} not found", fileName);
                
            }
        }

        public void AckPacket(IClientAPI remoteClient, ulong xferID, uint packet)
        {
            lock (NewFiles)  // This is actually to lock Transfers
            {
                if (Transfers.ContainsKey(xferID))
                {
                    XferDownLoad dl = Transfers[xferID];
                    if (Transfers[xferID].AckPacket(packet))
                    {
                        RemoveXferData(xferID);
                        RemoveOrDecrement(dl.FileName);
                    }
                }
            }
        }

        private void RemoveXferData(ulong xferID)
        {
            // NewFiles must be locked!
            if (Transfers.ContainsKey(xferID))
            {
                XferModule.XferDownLoad xferItem = Transfers[xferID];
                //string filename = xferItem.FileName;
                Transfers.Remove(xferID);
                xferItem.Data = new byte[0]; // Clear the data
                xferItem.DataPointer = 0;

            }
        }

        public void AbortXfer(IClientAPI remoteClient, ulong xferID)
        {
            lock (NewFiles)
            {
                if (Transfers.ContainsKey(xferID))
                    RemoveOrDecrement(Transfers[xferID].FileName);

                RemoveXferData(xferID);
            }
        }

        private void RemoveOrDecrement(string fileName)
        {
            // NewFiles must be locked

            if (NewFiles.ContainsKey(fileName))
            {
                if (NewFiles[fileName].Count == 1)
                    NewFiles.Remove(fileName);
                else
                    NewFiles[fileName].Count--;
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
            public bool isTaskInventory = false;

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
                    Client.SendXferPacket(XferID, 0 + 0x80000000, transferData, isTaskInventory);
                    complete = true;
                }
                else
                {
                    byte[] transferData = new byte[1000 + 4];
                    Array.Copy(Utils.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, 1000);
                    Client.SendXferPacket(XferID, 0, transferData, isTaskInventory);
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
                        Client.SendXferPacket(XferID, Packet, transferData, isTaskInventory);
                        Packet++;
                        DataPointer += 1000;
                    }
                    else
                    {
                        byte[] transferData = new byte[Data.Length - DataPointer];
                        Array.Copy(Data, DataPointer, transferData, 0, Data.Length - DataPointer);
                        uint endPacket = Packet |= (uint) 0x80000000;
                        Client.SendXferPacket(XferID, endPacket, transferData, isTaskInventory);
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
