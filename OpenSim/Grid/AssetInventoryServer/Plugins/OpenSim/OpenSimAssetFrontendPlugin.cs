/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Reflection;
using System.Net;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimAssetFrontendPlugin : IAssetInventoryServerPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private AssetInventoryServer m_server;

        public OpenSimAssetFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;

            // Asset request
            m_server.HttpServer.AddStreamHandler(new AssetRequestHandler(server));

            // Asset creation
            m_server.HttpServer.AddStreamHandler(new AssetPostHandler(server));

            m_log.Info("[ASSET] OpenSim Asset Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "AssetInventoryServer OpenSim asset frontend"; }
        }

        #endregion IPlugin implementation

        public class AssetRequestHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public AssetRequestHandler(AssetInventoryServer server) : base("GET", "^/assets")
            public AssetRequestHandler(AssetInventoryServer server) : base("GET", "/assets")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] buffer = new byte[] {};
                UUID assetID;
                // Split the URL up to get the asset ID out
                string[] rawUrl = httpRequest.Url.PathAndQuery.Split('/');

                if (rawUrl.Length >= 3 && rawUrl[2].Length >= 36 && UUID.TryParse(rawUrl[2].Substring(0, 36), out assetID))
                {
                    BackendResponse dataResponse;

                    AssetBase asset = new AssetBase();
                    if ((dataResponse = m_server.StorageProvider.TryFetchDataMetadata(assetID, out asset)) == BackendResponse.Success)
                    {
                        XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
                        MemoryStream ms = new MemoryStream();
                        XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                        xs.Serialize(xw, asset);
                        xw.Flush();

                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else
                    {
                        m_log.WarnFormat("Failed to fetch asset data or metadata for {0}: {1}", assetID, dataResponse);
                        httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
                    }
                }
                else
                {
                    m_log.Warn("Unrecognized OpenSim asset request: " + httpRequest.Url.PathAndQuery);
                }

                return buffer;
            }
        }

        public class AssetPostHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public AssetPostHandler(AssetInventoryServer server) : base("POST", "/^assets")
            public AssetPostHandler(AssetInventoryServer server) : base("POST", "/assets")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                Metadata metadata = new Metadata();

                try
                {
                    AssetBase asset = (AssetBase) new XmlSerializer(typeof (AssetBase)).Deserialize(httpRequest.InputStream);

                    if (asset.Data != null && asset.Data.Length > 0)
                    {
                        metadata.ID = asset.Metadata.FullID;
                        metadata.ContentType = Utils.SLAssetTypeToContentType((int) asset.Metadata.Type);
                        metadata.Name = asset.Metadata.Name;
                        metadata.Description = asset.Metadata.Description;
                        metadata.Temporary = asset.Metadata.Temporary;

                        metadata.SHA1 = OpenMetaverse.Utils.SHA1(asset.Data);
                        metadata.CreationDate = DateTime.Now;

                        BackendResponse storageResponse = m_server.StorageProvider.TryCreateAsset(metadata, asset.Data);

                        if (storageResponse == BackendResponse.Success)
                            httpResponse.StatusCode = (int) HttpStatusCode.Created;
                        else if (storageResponse == BackendResponse.NotFound)
                            httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
                        else
                            httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                    else
                    {
                        m_log.Warn("AssetPostHandler called with no asset data");
                        httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                    }
                }
                catch (Exception ex)
                {
                    m_log.Warn("Failed to parse POST data (expecting AssetBase): " + ex.Message);
                    httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                }

                return null;
            }
        }
    }
}
