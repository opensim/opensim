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
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using HttpServer;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimAssetFrontendPlugin : IAssetInventoryServerPlugin
    {
        private AssetInventoryServer server;

        public OpenSimAssetFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            // Asset request
            server.HttpServer.AddHandler("get", null, @"^/assets/", AssetRequestHandler);

            // Asset creation
            server.HttpServer.AddHandler("post", null, @"^/assets/", AssetPostHandler);

            Logger.Log.Info("[ASSET] OpenSim Asset Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            Logger.Log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
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

        bool AssetRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID assetID;
            // Split the URL up to get the asset ID out
            string[] rawUrl = request.Uri.PathAndQuery.Split('/');

            if (rawUrl.Length >= 3 && rawUrl[2].Length >= 36 && UUID.TryParse(rawUrl[2].Substring(0, 36), out assetID))
            {
                Metadata metadata;
                byte[] assetData;
                BackendResponse dataResponse;

                if ((dataResponse = server.StorageProvider.TryFetchDataMetadata(assetID, out metadata, out assetData)) == BackendResponse.Success)
                {
                    AssetBase asset = new AssetBase();
                    asset.Data = assetData;
                    asset.Metadata.FullID = metadata.ID;
                    asset.Metadata.Name = metadata.Name;
                    asset.Metadata.Description = metadata.Description;
                    asset.Metadata.CreationDate = metadata.CreationDate;
                    asset.Metadata.Type = (sbyte) Utils.ContentTypeToSLAssetType(metadata.ContentType);
                    asset.Metadata.Local = false;
                    asset.Metadata.Temporary = metadata.Temporary;

                    XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
                    MemoryStream ms = new MemoryStream();
                    XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                    xs.Serialize(xw, asset);
                    xw.Flush();

                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] buffer = ms.GetBuffer();

                    response.Status = HttpStatusCode.OK;
                    response.ContentType = "application/xml";
                    response.ContentLength = ms.Length;
                    response.Body.Write(buffer, 0, (int) ms.Length);
                    response.Body.Flush();
                }
                else
                {
                    Logger.Log.WarnFormat("Failed to fetch asset data or metadata for {0}: {1}", assetID, dataResponse);
                    response.Status = HttpStatusCode.NotFound;
                }
            }
            else
            {
                Logger.Log.Warn("Unrecognized OpenSim asset request: " + request.Uri.PathAndQuery);
            }

            return true;
        }

        bool AssetPostHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            Metadata metadata = new Metadata();

            try
            {
                AssetBase asset = (AssetBase) new XmlSerializer(typeof (AssetBase)).Deserialize(request.Body);

                if (asset.Data != null && asset.Data.Length > 0)
                {
                    metadata.ID = asset.Metadata.FullID;
                    metadata.ContentType = Utils.SLAssetTypeToContentType((int) asset.Metadata.Type);
                    metadata.Name = asset.Metadata.Name;
                    metadata.Description = asset.Metadata.Description;
                    metadata.Temporary = asset.Metadata.Temporary;

                    metadata.SHA1 = OpenMetaverse.Utils.SHA1(asset.Data);
                    metadata.CreationDate = DateTime.Now;

                    BackendResponse storageResponse = server.StorageProvider.TryCreateAsset(metadata, asset.Data);

                    if (storageResponse == BackendResponse.Success)
                        response.Status = HttpStatusCode.Created;
                    else if (storageResponse == BackendResponse.NotFound)
                        response.Status = HttpStatusCode.NotFound;
                    else
                        response.Status = HttpStatusCode.InternalServerError;
                }
                else
                {
                    Logger.Log.Warn("AssetPostHandler called with no asset data");
                    response.Status = HttpStatusCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse POST data (expecting AssetBase): " + ex.Message);
                response.Status = HttpStatusCode.BadRequest;
            }

            return true;
        }
    }
}
