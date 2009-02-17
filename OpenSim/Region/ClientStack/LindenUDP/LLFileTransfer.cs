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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// A work in progress, to contain the SL specific file transfer code that is currently in various region modules
    /// This file currently contains multiple classes that need to be split out into their own files.
    /// </summary>
    public class LLFileTransfer : IClientFileTransfer
    {
        protected IClientAPI m_clientAPI;

        /// Dictionary of handlers for uploading files from client
        /// TODO: Need to add cleanup code to remove handlers that have completed their upload
        protected Dictionary<ulong, XferUploadHandler> m_uploadHandlers;
        protected object m_uploadHandlersLock = new object();


        /// <summary>
        /// Dictionary of files ready to be sent to clients
        /// </summary>
        protected static Dictionary<string, byte[]> m_files;

        /// <summary>
        /// Dictionary of Download Transfers in progess
        /// </summary>
        protected Dictionary<ulong, XferDownloadHandler> m_downloadHandlers = new Dictionary<ulong, XferDownloadHandler>();


        public LLFileTransfer(IClientAPI clientAPI)
        {
            m_uploadHandlers = new Dictionary<ulong, XferUploadHandler>();
            m_clientAPI = clientAPI;

            m_clientAPI.OnXferReceive += XferReceive;
            m_clientAPI.OnAbortXfer += AbortXferUploadHandler;
        }

        public void Close()
        {
            if (m_clientAPI != null)
            {
                m_clientAPI.OnXferReceive -= XferReceive;
                m_clientAPI.OnAbortXfer -= AbortXferUploadHandler;
                m_clientAPI = null;
            }
        }

        #region Upload Handling

        public bool RequestUpload(string clientFileName, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            if ((String.IsNullOrEmpty(clientFileName)) || (uploadCompleteCallback == null))
            {
                return false;
            }

            XferUploadHandler uploader = new XferUploadHandler(m_clientAPI, clientFileName);

            return StartUpload(uploader, uploadCompleteCallback, abortCallback);
        }

        public bool RequestUpload(UUID fileID, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            if ((fileID == UUID.Zero) || (uploadCompleteCallback == null))
            {
                return false;
            }

            XferUploadHandler uploader = new XferUploadHandler(m_clientAPI, fileID);

            return StartUpload(uploader, uploadCompleteCallback, abortCallback);
        }

        private bool StartUpload(XferUploadHandler uploader, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            uploader.UploadDone += uploadCompleteCallback;
            uploader.UploadDone += RemoveXferUploadHandler;

            if (abortCallback != null)
            {
                uploader.UploadAborted += abortCallback;
            }

            lock (m_uploadHandlersLock)
            {
                if (!m_uploadHandlers.ContainsKey(uploader.XferID))
                {
                    m_uploadHandlers.Add(uploader.XferID, uploader);
                    uploader.RequestStartXfer(m_clientAPI);
                    return true;
                }
                else
                {
                    // something went wrong with the xferID allocation
                    uploader.UploadDone -= uploadCompleteCallback;
                    uploader.UploadDone -= RemoveXferUploadHandler;
                    if (abortCallback != null)
                    {
                        uploader.UploadAborted -= abortCallback;
                    }
                    return false;
                }
            }
        }

        protected void AbortXferUploadHandler(IClientAPI remoteClient, ulong xferID)
        {
            lock (m_uploadHandlersLock)
            {
                if (m_uploadHandlers.ContainsKey(xferID))
                {
                    m_uploadHandlers[xferID].AbortUpload(remoteClient);
                    m_uploadHandlers.Remove(xferID);
                }
            }
        }

        protected void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            lock (m_uploadHandlersLock)
            {
                if (m_uploadHandlers.ContainsKey(xferID))
                {
                    m_uploadHandlers[xferID].XferReceive(remoteClient, xferID, packetID, data);
                }
            }
        }

        protected void RemoveXferUploadHandler(string filename, UUID fileID, ulong transferID, byte[] fileData, IClientAPI remoteClient)
        {

        }
        #endregion

    }

    public class XferUploadHandler
    {
        private AssetBase m_asset;

        public event UploadComplete UploadDone;
        public event UploadAborted UploadAborted;

        private sbyte type = 0;

        public ulong mXferID;
        private UploadComplete handlerUploadDone;
        private UploadAborted handlerAbort;

        private bool m_complete = false;

        public bool UploadComplete
        {
            get { return m_complete; }
        }

        public XferUploadHandler(IClientAPI pRemoteClient, string pClientFilename)
        {
            Initialise(UUID.Zero, pClientFilename);
        }

        public XferUploadHandler(IClientAPI pRemoteClient, UUID fileID)
        {
            Initialise(fileID, String.Empty);
        }

        private void Initialise(UUID fileID, string fileName)
        {
            m_asset = new AssetBase();
            m_asset.FullID = fileID;
            m_asset.Type = type;
            m_asset.Data = new byte[0];
            m_asset.Name = fileName;
            m_asset.Description = "empty";
            m_asset.Local = true;
            m_asset.Temporary = true;
            mXferID = Util.GetNextXferID();
        }

        public ulong XferID
        {
            get { return mXferID; }
        }

        public void RequestStartXfer(IClientAPI pRemoteClient)
        {
            if (!String.IsNullOrEmpty(m_asset.Name))
            {
                pRemoteClient.SendXferRequest(mXferID, m_asset.Type, m_asset.FullID, 0, Utils.StringToBytes(m_asset.Name));
            }
            else
            {
                pRemoteClient.SendXferRequest(mXferID, m_asset.Type, m_asset.FullID, 0, new byte[0]);
            }
        }

        /// <summary>
        /// Process transfer data received from the client.
        /// </summary>
        /// <param name="xferID"></param>
        /// <param name="packetID"></param>
        /// <param name="data"></param>
        public void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            if (mXferID == xferID)
            {
                if (m_asset.Data.Length > 1)
                {
                    byte[] destinationArray = new byte[m_asset.Data.Length + data.Length];
                    Array.Copy(m_asset.Data, 0, destinationArray, 0, m_asset.Data.Length);
                    Array.Copy(data, 0, destinationArray, m_asset.Data.Length, data.Length);
                    m_asset.Data = destinationArray;
                }
                else
                {
                    byte[] buffer2 = new byte[data.Length - 4];
                    Array.Copy(data, 4, buffer2, 0, data.Length - 4);
                    m_asset.Data = buffer2;
                }

                remoteClient.SendConfirmXfer(xferID, packetID);

                if ((packetID & 0x80000000) != 0)
                {
                    SendCompleteMessage(remoteClient);

                }
            }
        }

        protected void SendCompleteMessage(IClientAPI remoteClient)
        {
            m_complete = true;
            handlerUploadDone = UploadDone;
            if (handlerUploadDone != null)
            {
                handlerUploadDone(m_asset.Name, m_asset.FullID, mXferID, m_asset.Data, remoteClient);
            }
        }

        public void AbortUpload(IClientAPI remoteClient)
        {
            handlerAbort = UploadAborted;
            if (handlerAbort != null)
            {
                handlerAbort(m_asset.Name, m_asset.FullID, mXferID, remoteClient);
            }
        }
    }

    public class XferDownloadHandler
    {
        public IClientAPI Client;
        private bool complete;
        public byte[] Data = new byte[0];
        public int DataPointer = 0;
        public string FileName = String.Empty;
        public uint Packet = 0;
        public uint Serial = 1;
        public ulong XferID = 0;

        public XferDownloadHandler(string fileName, byte[] data, ulong xferID, IClientAPI client)
        {
            FileName = fileName;
            Data = data;
            XferID = xferID;
            Client = client;
        }

        public XferDownloadHandler()
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
                    uint endPacket = Packet |= (uint)0x80000000;
                    Client.SendXferPacket(XferID, endPacket, transferData);
                    Packet++;
                    DataPointer += (Data.Length - DataPointer);

                    complete = true;
                }
            }

            return complete;
        }
    }

}
