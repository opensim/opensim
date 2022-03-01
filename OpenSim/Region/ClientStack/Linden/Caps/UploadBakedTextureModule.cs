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
using System.Net;
using System.Reflection;
using System.Timers;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Capabilities;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UploadBakedTextureModule")]
    public class UploadBakedTextureModule : ISharedRegionModule
    {
       private static readonly ILog m_log =LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int m_nscenes;
        IAssetCache m_assetCache = null;

        private string m_URL;

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_URL = config.GetString("Cap_UploadBakedTexture", string.Empty);
        }

        public void AddRegion(Scene s)
        {
        }

        public void RemoveRegion(Scene s)
        {
            s.EventManager.OnRegisterCaps -= RegisterCaps;
            --m_nscenes;
            if(m_nscenes <= 0)
                m_assetCache = null;
        }

        public void RegionLoaded(Scene s)
        {
            if (m_assetCache == null)
                m_assetCache = s.RequestModuleInterface <IAssetCache>();
            if (m_assetCache != null)
            {
                ++m_nscenes;
                s.EventManager.OnRegisterCaps += RegisterCaps;
            }
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "UploadBakedTextureModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            if (m_URL == "localhost")
            {
                caps.RegisterSimpleHandler("UploadBakedTexture",
                    new SimpleStreamHandler("/" + UUID.Random(), delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                    {
                        UploadBakedTexture(httpRequest, httpResponse, agentID, caps, m_assetCache);
                    }));
            }
            else if(!string.IsNullOrWhiteSpace(m_URL))
            {
                caps.RegisterHandler("UploadBakedTexture", m_URL);
            }
        }

        public void UploadBakedTexture(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID, Caps caps, IAssetCache cache)
        {
            if(httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            try
            {
                string capsBase = "/" + UUID.Random()+"-BK";
                string protocol = caps.SSLCaps ? "https://" : "http://";
                string uploaderURL = protocol + caps.HostName + ":" + caps.Port.ToString() + capsBase;

                LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                uploadResponse.uploader = uploaderURL;
                uploadResponse.state = "upload";

                BakedTextureUploader uploader =
                    new BakedTextureUploader(capsBase, caps.HttpListener, agentID, cache, httpRequest.RemoteIPEndPoint.Address);

                var uploaderHandler = new SimpleBinaryHandler("POST", capsBase, uploader.process);

                uploaderHandler.MaxDataSize = 6000000; // change per asset type?

                caps.HttpListener.AddSimpleStreamHandler(uploaderHandler);

                httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(uploadResponse));
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                return;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[UPLOAD BAKED TEXTURE HANDLER]: Error: {0}", e.Message);
            }
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }

    class BakedTextureUploader
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_uploaderPath = String.Empty;
        private IHttpServer m_httpListener;
        private UUID m_agentID = UUID.Zero;
        private IPAddress m_remoteAddress;
        private IAssetCache m_assetCache;
        private Timer m_timeout;

        public BakedTextureUploader(string path, IHttpServer httpServer, UUID agentID, IAssetCache cache, IPAddress remoteAddress)
        {
            m_uploaderPath = path;
            m_httpListener = httpServer;
            m_agentID = agentID;
            m_remoteAddress = remoteAddress;
            m_assetCache = cache;
            m_timeout = new Timer();
            m_timeout.Elapsed += Timeout;
            m_timeout.AutoReset = false;
            m_timeout.Interval = 30000;
            m_timeout.Start();
        }

        private void Timeout(Object source, ElapsedEventArgs e)
        {
            m_httpListener.RemoveSimpleStreamHandler(m_uploaderPath);
            m_timeout.Dispose();
        }

        /// <summary>
        /// Handle raw uploaded baked texture data.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public void process(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, byte[] data)
        {
            m_timeout.Stop();
            m_httpListener.RemoveSimpleStreamHandler(m_uploaderPath);
            m_timeout.Dispose();

            if (!httpRequest.RemoteIPEndPoint.Address.Equals(m_remoteAddress))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            // need to check if data is a baked
            try
            {
                UUID newAssetID = UUID.Random();
                AssetBase asset = new AssetBase(newAssetID, "Baked Texture", (sbyte)AssetType.Texture, m_agentID.ToString());
                asset.Data = data;
                asset.Temporary = true;
                asset.Local = true;
                //asset.Flags = AssetFlags.AvatarBake;
                m_assetCache.Cache(asset);

                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                uploadComplete.new_asset = newAssetID.ToString();
                uploadComplete.new_inventory_item = UUID.Zero;
                uploadComplete.state = "complete";

                httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(uploadComplete));
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                return;
            }
            catch { }
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
}

