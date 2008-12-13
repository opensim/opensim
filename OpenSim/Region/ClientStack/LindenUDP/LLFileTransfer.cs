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
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLFileTransfer : IClientFileTransfer
    {
        protected IClientAPI m_clientAPI;

        /// Dictionary of handlers for uploading files from client
        /// TODO: Need to add cleanup code to remove handlers that have completed their upload
        protected Dictionary<ulong, XferHandler> m_handlers;
        protected object m_handlerLock = new object();

        public LLFileTransfer(IClientAPI clientAPI)
        {
            m_handlers = new Dictionary<ulong, XferHandler>();
            m_clientAPI = clientAPI;

            m_clientAPI.OnXferReceive += XferReceive;
            m_clientAPI.OnAbortXfer += AbortXferHandler;
        }

        public void Close()
        {
            if (m_clientAPI != null)
            {
                m_clientAPI.OnXferReceive -= XferReceive;
                m_clientAPI.OnAbortXfer -= AbortXferHandler;
                m_clientAPI = null;
            }
        }

        public bool RequestUpload(string clientFileName, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            if ((String.IsNullOrEmpty(clientFileName)) || (uploadCompleteCallback == null))
            {
                 return false;
            }

            XferHandler uploader = new XferHandler(m_clientAPI, clientFileName);

            return StartUpload(uploader, uploadCompleteCallback, abortCallback);
        }

        public bool RequestUpload(UUID fileID, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            if ((fileID == UUID.Zero) || (uploadCompleteCallback == null))
            {
                return false;
            }

            XferHandler uploader = new XferHandler(m_clientAPI, fileID);

            return StartUpload(uploader, uploadCompleteCallback, abortCallback);
        }

        private bool StartUpload(XferHandler uploader, UploadComplete uploadCompleteCallback, UploadAborted abortCallback)
        {
            uploader.UploadDone += uploadCompleteCallback;

            if (abortCallback != null)
            {
                uploader.UploadAborted += abortCallback;
            }

            lock (m_handlerLock)
            {
                if (!m_handlers.ContainsKey(uploader.XferID))
                {
                    m_handlers.Add(uploader.XferID, uploader);
                    uploader.RequestStartXfer(m_clientAPI);
                    return true;
                }
                else
                {
                    // something went wrong with the xferID allocation
                    uploader.UploadDone -= uploadCompleteCallback;
                    if (abortCallback != null)
                    {
                        uploader.UploadAborted -= abortCallback;
                    }
                    return false;
                }
            }
        }

        protected void AbortXferHandler(IClientAPI remoteClient, ulong xferID)
        {
            lock (m_handlerLock)
            {
                if (m_handlers.ContainsKey(xferID))
                {
                    m_handlers[xferID].AbortUpload(remoteClient);
                    m_handlers.Remove(xferID);
                }
            }
        }

        protected void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            lock (m_handlerLock)
            {
                if (m_handlers.ContainsKey(xferID))
                {
                    m_handlers[xferID].XferReceive(remoteClient, xferID, packetID, data);
                }
            }
        }
    }

    public class XferHandler
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

        public XferHandler(IClientAPI pRemoteClient, string pClientFilename)
        {

            m_asset = new AssetBase();
            m_asset.FullID = UUID.Zero;
            m_asset.Type = type;
            m_asset.Data = new byte[0];
            m_asset.Name = pClientFilename;
            m_asset.Description = "empty";
            m_asset.Local = true;
            m_asset.Temporary = true;
            mXferID = Util.GetNextXferID();
        }

        public XferHandler(IClientAPI pRemoteClient, UUID fileID)
        {
            m_asset = new AssetBase();
            m_asset.FullID = fileID;
            m_asset.Type = type;
            m_asset.Data = new byte[0];
            m_asset.Name = null;
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
            if (m_asset.Name != null)
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
                handlerUploadDone(m_asset.Name, m_asset.FullID, m_asset.Data, remoteClient);
            }
        }

        public void AbortUpload(IClientAPI remoteClient)
        {
            handlerAbort = UploadAborted;
            if (handlerAbort != null)
            {
                handlerAbort(m_asset.Name, mXferID, remoteClient);
            }
        }
    }
}
