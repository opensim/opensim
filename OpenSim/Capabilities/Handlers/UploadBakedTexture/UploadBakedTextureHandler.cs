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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    public class UploadBakedTextureHandler
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Caps m_HostCapsObj;
        private IAssetService m_assetService;

        public UploadBakedTextureHandler(Caps caps, IAssetService assetService)
        {
            m_HostCapsObj = caps;
            m_assetService = assetService;
        }

        /// <summary>
        /// Handle a request from the client for a Uri to upload a baked texture.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns>The upload response if the request is successful, null otherwise.</returns>
        public string UploadBakedTexture(
            string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            try
            {
                string capsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath;
                string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

                BakedTextureUploader uploader =
                    new BakedTextureUploader(capsBase + uploaderPath, m_HostCapsObj.HttpListener, m_HostCapsObj.AgentID);
                uploader.OnUpLoad += BakedTextureUploaded;

                m_HostCapsObj.HttpListener.AddStreamHandler(
                    new BinaryStreamHandler(
                        "POST", capsBase + uploaderPath, uploader.uploaderCaps, "UploadBakedTexture", null));

                string protocol = "http://";

                if (m_HostCapsObj.SSLCaps)
                    protocol = "https://";

                string uploaderURL = protocol + m_HostCapsObj.HostName + ":" +
                        m_HostCapsObj.Port.ToString() + capsBase + uploaderPath;

                LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                uploadResponse.uploader = uploaderURL;
                uploadResponse.state = "upload";

                return LLSDHelpers.SerialiseLLSDReply(uploadResponse);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[UPLOAD BAKED TEXTURE HANDLER]: {0}{1}", e.Message, e.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// Called when a baked texture has been successfully uploaded by a client.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="data"></param>
        private void BakedTextureUploaded(UUID assetID, byte[] data)
        {
            m_log.DebugFormat("[UPLOAD BAKED TEXTURE HANDLER]: Received baked texture {0}", assetID.ToString());

            AssetBase asset;
            asset = new AssetBase(assetID, "Baked Texture", (sbyte)AssetType.Texture, m_HostCapsObj.AgentID.ToString());
            asset.Data = data;
            asset.Temporary = true;
            asset.Local = true;
            m_assetService.Store(asset);
        }
    }

    class BakedTextureUploader
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event Action<UUID, byte[]> OnUpLoad;

        private string uploaderPath = String.Empty;
        private UUID newAssetID;
        private IHttpServer httpListener;
        private UUID AgentId = UUID.Zero;

        public BakedTextureUploader(string path, IHttpServer httpServer, UUID uUID)
        {
            newAssetID = UUID.Random();
            uploaderPath = path;
            httpListener = httpServer;
            AgentId = uUID;
            // m_log.InfoFormat("[CAPS] baked texture upload starting for {0}",newAssetID);
        }

        /// <summary>
        /// Handle raw uploaded baked texture data.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string uploaderCaps(byte[] data, string path, string param)
        {
            Action<UUID, byte[]> handlerUpLoad = OnUpLoad;

            // Don't do this asynchronously, otherwise it's possible for the client to send set appearance information
            // on another thread which might send out avatar updates before the asset has been put into the asset
            // service.
            if (handlerUpLoad != null)
                handlerUpLoad(newAssetID, data);

            string res = String.Empty;
            LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
            uploadComplete.new_asset = newAssetID.ToString();
            uploadComplete.new_inventory_item = UUID.Zero;
            uploadComplete.state = "complete";

            res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

            httpListener.RemoveStreamHandler("POST", uploaderPath);

            // m_log.DebugFormat("[BAKED TEXTURE UPLOADER]: baked texture upload completed for {0}", newAssetID);

            return res;
        }
    }
}