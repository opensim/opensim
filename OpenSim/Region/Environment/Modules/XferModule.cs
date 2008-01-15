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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class XferModule : IRegionModule, IXfer
    {
        public Dictionary<string, byte[]> NewFiles = new Dictionary<string, byte[]>();
        public Dictionary<ulong, XferDownLoad> Transfers = new Dictionary<ulong, XferDownLoad>();

        private Scene m_scene;

        public XferModule()
        {
        }

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
                        transaction.StartSend();
                    }
                }
            }
        }

        public void AckPacket(IClientAPI remoteClient, ulong xferID, uint packet)
        {
            if (Transfers.ContainsKey(xferID))
            {
                Transfers[xferID].AckPacket(packet);
            }
        }

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
            return true;
        }


        public class XferDownLoad
        {
            public byte[] Data = new byte[0];
            public string FileName = String.Empty;
            public ulong XferID = 0;
            public int DataPointer = 0;
            public uint Packet = 0;
            public IClientAPI Client;
            public uint Serial = 1;
            private bool complete = false;

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

            public void StartSend()
            {
                if (Data.Length < 1000)
                {
                    // for now (testing ) we only support files under 1000 bytes
                    byte[] transferData = new byte[Data.Length + 4];
                    Array.Copy(Helpers.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, Data.Length);
                    Client.SendXferPacket(XferID, 0 + 0x80000000, transferData);
                    complete = true;
                }
                else
                {
                    byte[] transferData = new byte[1000 + 4];
                    Array.Copy(Helpers.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, 1000);
                    Client.SendXferPacket(XferID, 0, transferData);
                    Packet++;
                    DataPointer = 1000;
                }
            }

            public void AckPacket(uint packet)
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
            }
        }
    }
}