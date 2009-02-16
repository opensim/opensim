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
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using HttpServer;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class ReferenceFrontendPlugin : IAssetInventoryServerPlugin
    {
        AssetInventoryServer server;

        public ReferenceFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            // Asset metadata request
            server.HttpServer.AddHandler("get", null, @"^/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/metadata",
                MetadataRequestHandler);

            // Asset data request
            server.HttpServer.AddHandler("get", null, @"^/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/data",
                DataRequestHandler);

            // Asset creation
            server.HttpServer.AddHandler("post", null, "^/createasset", CreateRequestHandler);

            Logger.Log.Info("[ASSET] Reference Frontend loaded.");
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
            get { return "AssetInventoryServer Reference asset frontend"; }
        }

        #endregion IPlugin implementation

        bool MetadataRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID assetID;
            // Split the URL up into an AssetID and a method
            string[] rawUrl = request.Uri.PathAndQuery.Split('/');

            if (rawUrl.Length >= 3 && UUID.TryParse(rawUrl[1], out assetID))
            {
                UUID authToken = Utils.GetAuthToken(request);

                if (server.AuthorizationProvider.IsMetadataAuthorized(authToken, assetID))
                {
                    Metadata metadata;
                    BackendResponse storageResponse = server.StorageProvider.TryFetchMetadata(assetID, out metadata);

                    if (storageResponse == BackendResponse.Success)
                    {
                        // If the asset data location wasn't specified in the metadata, specify it
                        // manually here by pointing back to this asset server
                        if (!metadata.Methods.ContainsKey("data"))
                        {
                            metadata.Methods["data"] = new Uri(String.Format("{0}://{1}/{2}/data",
                                request.Uri.Scheme, request.Uri.Authority, assetID));
                        }

                        byte[] serializedData = metadata.SerializeToBytes();

                        response.Status = HttpStatusCode.OK;
                        response.ContentType = "application/json";
                        response.ContentLength = serializedData.Length;
                        response.Body.Write(serializedData, 0, serializedData.Length);

                    }
                    else if (storageResponse == BackendResponse.NotFound)
                    {
                        Logger.Log.Warn("Could not find metadata for asset " + assetID.ToString());
                        response.Status = HttpStatusCode.NotFound;
                    }
                    else
                    {
                        response.Status = HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    response.Status = HttpStatusCode.Forbidden;
                }

                return true;
            }

            response.Status = HttpStatusCode.NotFound;
            return true;
        }

        bool DataRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID assetID;
            // Split the URL up into an AssetID and a method
            string[] rawUrl = request.Uri.PathAndQuery.Split('/');

            if (rawUrl.Length >= 3 && UUID.TryParse(rawUrl[1], out assetID))
            {
                UUID authToken = Utils.GetAuthToken(request);

                if (server.AuthorizationProvider.IsDataAuthorized(authToken, assetID))
                {
                    byte[] assetData;
                    BackendResponse storageResponse = server.StorageProvider.TryFetchData(assetID, out assetData);

                    if (storageResponse == BackendResponse.Success)
                    {
                        response.Status = HttpStatusCode.OK;
                        response.Status = HttpStatusCode.OK;
                        response.ContentType = "application/octet-stream";
                        response.AddHeader("Content-Disposition", "attachment; filename=" + assetID.ToString());
                        response.ContentLength = assetData.Length;
                        response.Body.Write(assetData, 0, assetData.Length);
                    }
                    else if (storageResponse == BackendResponse.NotFound)
                    {
                        response.Status = HttpStatusCode.NotFound;
                    }
                    else
                    {
                        response.Status = HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    response.Status = HttpStatusCode.Forbidden;
                }

                return true;
            }

            response.Status = HttpStatusCode.BadRequest;
            return true;
        }

        bool CreateRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID authToken = Utils.GetAuthToken(request);

            if (server.AuthorizationProvider.IsCreateAuthorized(authToken))
            {
                try
                {
                    OSD osdata = OSDParser.DeserializeJson(request.Body);

                    if (osdata.Type == OSDType.Map)
                    {
                        OSDMap map = (OSDMap)osdata;
                        Metadata metadata = new Metadata();
                        metadata.Deserialize(map);

                        byte[] assetData = map["data"].AsBinary();

                        if (assetData != null && assetData.Length > 0)
                        {
                            BackendResponse storageResponse;

                            if (metadata.ID != UUID.Zero)
                                storageResponse = server.StorageProvider.TryCreateAsset(metadata, assetData);
                            else
                                storageResponse = server.StorageProvider.TryCreateAsset(metadata, assetData, out metadata.ID);

                            if (storageResponse == BackendResponse.Success)
                            {
                                response.Status = HttpStatusCode.Created;
                                OSDMap responseMap = new OSDMap(1);
                                responseMap["id"] = OSD.FromUUID(metadata.ID);
                                LitJson.JsonData jsonData = OSDParser.SerializeJson(responseMap);
                                byte[] responseData = System.Text.Encoding.UTF8.GetBytes(jsonData.ToJson());
                                response.Body.Write(responseData, 0, responseData.Length);
                                response.Body.Flush();
                            }
                            else if (storageResponse == BackendResponse.NotFound)
                            {
                                response.Status = HttpStatusCode.NotFound;
                            }
                            else
                            {
                                response.Status = HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            response.Status = HttpStatusCode.BadRequest;
                        }
                    }
                    else
                    {
                        response.Status = HttpStatusCode.BadRequest;
                    }
                }
                catch (Exception ex)
                {
                    response.Status = HttpStatusCode.InternalServerError;
                    response.Reason = ex.Message;
                }
            }
            else
            {
                response.Status = HttpStatusCode.Forbidden;
            }

            return true;
        }
    }
}
