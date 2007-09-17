using System;
using System.Collections.Generic;
using System.Text;

using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;

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

        public void Initialise(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;

            m_scene.RegisterModuleInterface<IXfer>(this);
        }

        public void PostInitialise()
        {

        }

        public void CloseDown()
        {

        }

        public string GetName()
        {
            return "XferModule";
        }

        public bool IsSharedModule()
        {
            return false;
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
            if (this.Transfers.ContainsKey(xferID))
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
            public string FileName = "";
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
                    byte[] transferData = new byte[1000 +4];
                    Array.Copy(Helpers.IntToBytes(Data.Length), 0, transferData, 0, 4);
                    Array.Copy(Data, 0, transferData, 4, 1000);
                    Client.SendXferPacket(XferID, 0 , transferData);
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
                        uint endPacket = Packet |= (uint)0x80000000;
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
