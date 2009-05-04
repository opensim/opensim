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
using System.IO;
using System.Reflection;
using System.Net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class ReferenceFrontendPlugin : IAssetInventoryServerPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        AssetInventoryServer m_server;

        public ReferenceFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;

            // Asset metadata request
            //m_server.HttpServer.AddStreamHandler(new MetadataRequestHandler(server));

            // Asset data request
            m_server.HttpServer.AddStreamHandler(new DataRequestHandler(server));

            // Asset creation
            //m_server.HttpServer.AddStreamHandler(new CreateRequestHandler(server));

            m_log.Info("[REFERENCEFRONTEND]: Reference Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[REFERENCEFRONTEND]: {0} cannot be default-initialized!", Name);
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
            get { return "ReferenceFrontend"; }
        }

        #endregion IPlugin implementation

        //public class MetadataRequestHandler : IStreamedRequestHandler
        //{
        //    AssetInventoryServer m_server;
        //    string m_contentType;
        //    string m_httpMethod;
        //    string m_path;

        //    public MetadataRequestHandler(AssetInventoryServer server)
        //    {
        //        m_server = server;
        //        m_contentType = null;
        //        m_httpMethod = "GET";
        //        m_path = @"^/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/metadata";
        //    }

        //    #region IStreamedRequestHandler implementation

        //    public string ContentType
        //    {
        //        get { return m_contentType; }
        //    }

        //    public string HttpMethod
        //    {
        //        get { return m_httpMethod; }
        //    }

        //    public string Path
        //    {
        //        get { return m_path; }
        //    }

        //    public byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        //    {
        //        byte[] serializedData = null;
        //        UUID assetID;
        //        // Split the URL up into an AssetID and a method
        //        string[] rawUrl = httpRequest.Url.PathAndQuery.Split('/');

        //        if (rawUrl.Length >= 3 && UUID.TryParse(rawUrl[1], out assetID))
        //        {
        //            UUID authToken = Utils.GetAuthToken(httpRequest);

        //            if (m_server.AuthorizationProvider.IsMetadataAuthorized(authToken, assetID))
        //            {
        //                AssetMetadata metadata;
        //                BackendResponse storageResponse = m_server.StorageProvider.TryFetchMetadata(assetID, out metadata);

        //                if (storageResponse == BackendResponse.Success)
        //                {
        //                    // If the asset data location wasn't specified in the metadata, specify it
        //                    // manually here by pointing back to this asset server
        //                    if (!metadata.Methods.ContainsKey("data"))
        //                    {
        //                        metadata.Methods["data"] = new Uri(String.Format("{0}://{1}/{2}/data",
        //                            httpRequest.Url.Scheme, httpRequest.Url.Authority, assetID));
        //                    }

        //                    serializedData = metadata.SerializeToBytes();

        //                    httpResponse.StatusCode = (int) HttpStatusCode.OK;
        //                    httpResponse.ContentType = "application/json";
        //                    httpResponse.ContentLength = serializedData.Length;
        //                    httpResponse.Body.Write(serializedData, 0, serializedData.Length);
        //                }
        //                else if (storageResponse == BackendResponse.NotFound)
        //                {
        //                    m_log.Warn("[REFERENCEFRONTEND]: Could not find metadata for asset " + assetID.ToString());
        //                    httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
        //                }
        //                else
        //                {
        //                    httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
        //                }
        //            }
        //            else
        //            {
        //                httpResponse.StatusCode = (int) HttpStatusCode.Forbidden;
        //            }

        //            return serializedData;
        //        }

        //        httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
        //        return serializedData;
        //    }

        //    #endregion IStreamedRequestHandler implementation
        //}

        public class DataRequestHandler : IStreamedRequestHandler
        {
            AssetInventoryServer m_server;
            string m_contentType;
            string m_httpMethod;
            string m_path;

            public DataRequestHandler(AssetInventoryServer server)
            {
                m_server = server;
                m_contentType = null;
                m_httpMethod = "GET";
                m_path = @"^/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/data";
            }

            #region IStreamedRequestHandler implementation

            public string ContentType
            {
                get { return m_contentType; }
            }

            public string HttpMethod
            {
                get { return m_httpMethod; }
            }

            public string Path
            {
                get { return m_path; }
            }

            public byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] assetData = null;
                UUID assetID;
                // Split the URL up into an AssetID and a method
                string[] rawUrl = httpRequest.Url.PathAndQuery.Split('/');

                if (rawUrl.Length >= 3 && UUID.TryParse(rawUrl[1], out assetID))
                {
                    UUID authToken = Utils.GetAuthToken(httpRequest);

                    if (m_server.AuthorizationProvider.IsDataAuthorized(authToken, assetID))
                    {
                        BackendResponse storageResponse = m_server.StorageProvider.TryFetchData(assetID, out assetData);

                        if (storageResponse == BackendResponse.Success)
                        {
                            httpResponse.StatusCode = (int) HttpStatusCode.OK;
                            httpResponse.ContentType = "application/octet-stream";
                            httpResponse.AddHeader("Content-Disposition", "attachment; filename=" + assetID.ToString());
                            httpResponse.ContentLength = assetData.Length;
                            httpResponse.Body.Write(assetData, 0, assetData.Length);
                        }
                        else if (storageResponse == BackendResponse.NotFound)
                        {
                            httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
                        }
                        else
                        {
                            httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                        }
                    }
                    else
                    {
                        httpResponse.StatusCode = (int) HttpStatusCode.Forbidden;
                    }

                    return assetData;
                }

                httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                return assetData;
            }

            #endregion IStreamedRequestHandler implementation
        }

        //public class CreateRequestHandler : IStreamedRequestHandler
        //{
        //    AssetInventoryServer m_server;
        //    string m_contentType;
        //    string m_httpMethod;
        //    string m_path;

        //    public CreateRequestHandler(AssetInventoryServer server)
        //    {
        //        m_server = server;
        //        m_contentType = null;
        //        m_httpMethod = "POST";
        //        m_path = "^/createasset";
        //    }

        //    #region IStreamedRequestHandler implementation

        //    public string ContentType
        //    {
        //        get { return m_contentType; }
        //    }

        //    public string HttpMethod
        //    {
        //        get { return m_httpMethod; }
        //    }

        //    public string Path
        //    {
        //        get { return m_path; }
        //    }

        //    public byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        //    {
        //        byte[] responseData = null;
        //        UUID authToken = Utils.GetAuthToken(httpRequest);

        //        if (m_server.AuthorizationProvider.IsCreateAuthorized(authToken))
        //        {
        //            try
        //            {
        //                OSD osdata = OSDParser.DeserializeJson(new StreamReader(httpRequest.InputStream).ReadToEnd());

        //                if (osdata.Type == OSDType.Map)
        //                {
        //                    OSDMap map = (OSDMap)osdata;
        //                    Metadata metadata = new Metadata();
        //                    metadata.Deserialize(map);

        //                    byte[] assetData = map["data"].AsBinary();

        //                    if (assetData != null && assetData.Length > 0)
        //                    {
        //                        BackendResponse storageResponse;

        //                        if (metadata.ID != UUID.Zero)
        //                            storageResponse = m_server.StorageProvider.TryCreateAsset(metadata, assetData);
        //                        else
        //                            storageResponse = m_server.StorageProvider.TryCreateAsset(metadata, assetData, out metadata.ID);

        //                        if (storageResponse == BackendResponse.Success)
        //                        {
        //                            httpResponse.StatusCode = (int) HttpStatusCode.Created;
        //                            OSDMap responseMap = new OSDMap(1);
        //                            responseMap["id"] = OSD.FromUUID(metadata.ID);
        //                            LitJson.JsonData jsonData = OSDParser.SerializeJson(responseMap);
        //                            responseData = System.Text.Encoding.UTF8.GetBytes(jsonData.ToJson());
        //                            httpResponse.Body.Write(responseData, 0, responseData.Length);
        //                            httpResponse.Body.Flush();
        //                        }
        //                        else if (storageResponse == BackendResponse.NotFound)
        //                        {
        //                            httpResponse.StatusCode = (int) HttpStatusCode.NotFound;
        //                        }
        //                        else
        //                        {
        //                            httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
        //                    }
        //                }
        //                else
        //                {
        //                    httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
        //                httpResponse.StatusDescription = ex.Message;
        //            }
        //        }
        //        else
        //        {
        //            httpResponse.StatusCode = (int) HttpStatusCode.Forbidden;
        //        }

        //        return responseData;
        //    }

        //    #endregion IStreamedRequestHandler implementation
        //}
    }
}
