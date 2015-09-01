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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Connects to the SimianGrid asset service
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianAssetServiceConnector")]
    public class SimianAssetServiceConnector : IAssetService, ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        private static string ZeroID = UUID.Zero.ToString();

        private string m_serverUrl = String.Empty;
        private IImprovedAssetCache m_cache;
        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene)
        {
            if (m_cache == null)
            {
                IImprovedAssetCache cache = scene.RequestModuleInterface<IImprovedAssetCache>();
                if (cache is ISharedRegionModule)
                    m_cache = cache;
            }
        }
        public void PostInitialise() { }
        public void Close() { }

        public SimianAssetServiceConnector() { }
        public string Name { get { return "SimianAssetServiceConnector"; } }
        public void AddRegion(Scene scene) { if (m_Enabled) { scene.RegisterModuleInterface<IAssetService>(this); } }
        public void RemoveRegion(Scene scene) { if (m_Enabled) { scene.UnregisterModuleInterface<IAssetService>(this); } }
        
        #endregion ISharedRegionModule

        public SimianAssetServiceConnector(IConfigSource source)
        {
            CommonInit(source);
        }

        public SimianAssetServiceConnector(string url)
        {
            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + '/';
            m_serverUrl = url;
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");
                if (name == Name)
                    CommonInit(source);
            }
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["AssetService"];
            if (gridConfig != null)
            {
                string serviceUrl = gridConfig.GetString("AssetServerURI");
                if (!String.IsNullOrEmpty(serviceUrl))
                {
                    if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                        serviceUrl = serviceUrl + '/';
                    m_serverUrl = serviceUrl;
                }
            }

            if (String.IsNullOrEmpty(m_serverUrl))
                m_log.Info("[SIMIAN ASSET CONNECTOR]: No AssetServerURI specified, disabling connector");
            else
                m_Enabled = true;
        }

#region IAssetService

        public AssetBase Get(string id)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            // Cache fetch
            if (m_cache != null)
            {
                AssetBase asset = m_cache.Get(id);
                if (asset != null)
                    return asset;
            }

            return SimianGetOperation(id);
        }
        

        public AssetBase GetCached(string id)
        {
            if (m_cache != null)
                return m_cache.Get(id);

            return null;
        }

        /// <summary>
        /// Get an asset's metadata
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public AssetMetadata GetMetadata(string id)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            // Cache fetch
            if (m_cache != null)
            {
                AssetBase asset = m_cache.Get(id);
                if (asset != null)
                    return asset.Metadata;
            }

            // return GetRemoteMetadata(id);
            return SimianGetMetadataOperation(id);
        }
        
        public byte[] GetData(string id)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            AssetBase asset = Get(id);

            if (asset != null)
                return asset.Data;

            return null;
        }

        /// <summary>
        /// Get an asset asynchronously
        /// </summary>
        /// <param name="id">The asset id</param>
        /// <param name="sender">Represents the requester.  Passed back via the handler</param>
        /// <param name="handler">The handler to call back once the asset has been retrieved</param>
        /// <returns>True if the id was parseable, false otherwise</returns>
        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            // Cache fetch
            if (m_cache != null)
            {
                AssetBase asset = m_cache.Get(id);
                if (asset != null)
                {
                    handler(id, sender, asset);
                    return true;
                }
            }

            Util.FireAndForget(
                delegate(object o)
                {
                    AssetBase asset = SimianGetOperation(id);
                    handler(id, sender, asset);
                }, null, "SimianAssetServiceConnector.GetFromService"
            );

            return true;
        }

        public bool[] AssetsExist(string[] ids)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            bool[] exist = new bool[ids.Length];

            for (int i = 0; i < ids.Length; i++)
            {
                AssetMetadata metadata = GetMetadata(ids[i]);
                if (metadata != null)
                    exist[i] = true;
            }

            return exist;
        }

        /// <summary>
        /// Creates a new asset
        /// </summary>
        /// Returns a random ID if none is passed into it
        /// <param name="asset"></param>
        /// <returns></returns>
        public string Store(AssetBase asset)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            bool storedInCache = false;

            // AssetID handling
            if (String.IsNullOrEmpty(asset.ID) || asset.ID == ZeroID)
            {
                asset.FullID = UUID.Random();
                asset.ID = asset.FullID.ToString();
            }

            // Cache handling
            if (m_cache != null)
            {
                m_cache.Cache(asset);
                storedInCache = true;
            }

            // Local asset handling
            if (asset.Local)
            {
                if (!storedInCache)
                {
                    m_log.Error("Cannot store local " + asset.Metadata.ContentType + " asset without an asset cache");
                    asset.ID = null;
                    asset.FullID = UUID.Zero;
                }

                return asset.ID;
            }

            return SimianStoreOperation(asset);
        }
        
        /// <summary>
        /// Update an asset's content
        /// </summary>
        /// Attachments and bare scripts need this!!
        /// <param name="id"> </param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool UpdateContent(string id, byte[] data)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            AssetBase asset = Get(id);

            if (asset == null)
            {
                m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: Failed to fetch asset {0} for updating", id);
                return false;
            }

            asset.Data = data;

            string result = Store(asset);
            return !String.IsNullOrEmpty(result);
        }

        /// <summary>
        /// Delete an asset
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool Delete(string id)
        {
            if (String.IsNullOrEmpty(m_serverUrl))
            {
                m_log.Error("[SIMIAN ASSET CONNECTOR]: No AssetServerURI configured");
                throw new InvalidOperationException();
            }

            if (m_cache != null)
                m_cache.Expire(id);

            return SimianDeleteOperation(id);
        }
        
#endregion IAssetService

#region SimianOperations
        /// <summary>
        /// Invokes the xRemoveAsset operation on the simian server to delete an asset
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool SimianDeleteOperation(string id)
        {
            try
            {
                NameValueCollection requestArgs = new NameValueCollection
                    {
                            { "RequestMethod", "xRemoveAsset" },
                            { "AssetID", id }
                    };

                OSDMap response = SimianGrid.PostToService(m_serverUrl,requestArgs);
                if (! response["Success"].AsBoolean())
                {
                    m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: failed to delete asset; {0}",response["Message"].AsString());
                    return false;
                }
                
                return true;
                
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: failed to delete asset {0}; {1}", id, ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Invokes the xAddAsset operation on the simian server to create or update an asset
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string SimianStoreOperation(AssetBase asset)
        {
            try
            {
                NameValueCollection requestArgs = new NameValueCollection
                    {
                            { "RequestMethod", "xAddAsset" },
                            { "ContentType", asset.Metadata.ContentType },
                            { "EncodedData", Convert.ToBase64String(asset.Data) },
                            { "AssetID", asset.FullID.ToString() },
                            { "CreatorID", asset.Metadata.CreatorID },
                            { "Temporary", asset.Temporary ? "1" : "0" },
                            { "Name", asset.Name }
                    };
                
                OSDMap response = SimianGrid.PostToService(m_serverUrl,requestArgs);
                if (! response["Success"].AsBoolean())
                {
                    m_log.WarnFormat("[SIMIAN ASSET CONNECTOR] failed to store asset; {0}",response["Message"].AsString());
                    return null;
                }

                // asset.ID is always set before calling this function
                return asset.ID;
                
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[SIMIAN ASSET CONNECTOR] failed to store asset; {0}",ex.Message);
            }
            
            return null;
        }

        /// <summary>
        /// Invokes the xGetAsset operation on the simian server to get data associated with an asset
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private AssetBase SimianGetOperation(string id)
        {
            try 
            {
                NameValueCollection requestArgs = new NameValueCollection
                    {
                            { "RequestMethod", "xGetAsset" },
                            { "ID", id } 
                    };

                OSDMap response = SimianGrid.PostToService(m_serverUrl,requestArgs);
                if (! response["Success"].AsBoolean())
                {
                    m_log.WarnFormat("[SIMIAN ASSET CONNECTOR] Failed to get asset; {0}",response["Message"].AsString());
                    return null;
                }
            
                AssetBase asset = new AssetBase();

                asset.ID = id;
                asset.Name = String.Empty;
                asset.Metadata.ContentType = response["ContentType"].AsString(); // this will also set the asset Type property
                asset.CreatorID = response["CreatorID"].AsString();
                asset.Data = System.Convert.FromBase64String(response["EncodedData"].AsString());
                asset.Local = false;
                asset.Temporary = response["Temporary"];

                return asset;
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: failed to retrieve asset {0}; {1}", id, ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Invokes the xGetAssetMetadata operation on the simian server to retrieve metadata for an asset
        /// This operation is generally used to determine if an asset exists in the database
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private AssetMetadata SimianGetMetadataOperation(string id)
        {
            try
            {
                NameValueCollection requestArgs = new NameValueCollection
                    {
                            { "RequestMethod", "xGetAssetMetadata" },
                            { "ID", id } 
                    };

                OSDMap response = SimianGrid.PostToService(m_serverUrl,requestArgs);
                if (! response["Success"].AsBoolean())
                {
                    // this is not really an error, this call is used to test existence
                    // m_log.DebugFormat("[SIMIAN ASSET CONNECTOR] Failed to get asset metadata; {0}",response["Message"].AsString());
                    return null;
                }
            
                AssetMetadata metadata = new AssetMetadata();
                metadata.ID = id;
                metadata.ContentType = response["ContentType"].AsString();
                metadata.CreatorID = response["CreatorID"].AsString();
                metadata.Local = false;
                metadata.Temporary = response["Temporary"];

                string lastModifiedStr = response["Last-Modified"].AsString();
                if (! String.IsNullOrEmpty(lastModifiedStr))
                {
                    DateTime lastModified;
                    if (DateTime.TryParse(lastModifiedStr, out lastModified))
                        metadata.CreationDate = lastModified;
                }

                return metadata;
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: Failed to get asset metadata; {0}", ex.Message);
            }

            return null;
        }
#endregion

        // private AssetMetadata GetRemoteMetadata(string id)
        // {
        //     Uri url;
        //     AssetMetadata metadata = null;

        //     // Determine if id is an absolute URL or a grid-relative UUID
        //     if (!Uri.TryCreate(id, UriKind.Absolute, out url))
        //         url = new Uri(m_serverUrl + id);

        //     try
        //     {
        //         HttpWebRequest request = UntrustedHttpWebRequest.Create(url);
        //         request.Method = "HEAD";

        //         using (WebResponse response = request.GetResponse())
        //         {
        //             using (Stream responseStream = response.GetResponseStream())
        //             {
        //                 // Create the metadata object
        //                 metadata = new AssetMetadata();
        //                 metadata.ContentType = response.ContentType;
        //                 metadata.ID = id;

        //                 UUID uuid;
        //                 if (UUID.TryParse(id, out uuid))
        //                     metadata.FullID = uuid;

        //                 string lastModifiedStr = response.Headers.Get("Last-Modified");
        //                 if (!String.IsNullOrEmpty(lastModifiedStr))
        //                 {
        //                     DateTime lastModified;
        //                     if (DateTime.TryParse(lastModifiedStr, out lastModified))
        //                         metadata.CreationDate = lastModified;
        //                 }
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         m_log.Warn("[SIMIAN ASSET CONNECTOR]: Asset HEAD from " + url + " failed: " + ex.Message);
        //     }

        //     return metadata;
        // }

        // private AssetBase GetRemote(string id)
        // {
        //     AssetBase asset = null;
        //     Uri url;

        //     // Determine if id is an absolute URL or a grid-relative UUID
        //     if (!Uri.TryCreate(id, UriKind.Absolute, out url))
        //         url = new Uri(m_serverUrl + id);

        //     try
        //     {
        //         HttpWebRequest request = UntrustedHttpWebRequest.Create(url);

        //         using (WebResponse response = request.GetResponse())
        //         {
        //             using (Stream responseStream = response.GetResponseStream())
        //             {
        //                 string creatorID = response.Headers.GetOne("X-Asset-Creator-Id") ?? String.Empty;

        //                 // Create the asset object
        //                 asset = new AssetBase(id, String.Empty, SLUtil.ContentTypeToSLAssetType(response.ContentType), creatorID);

        //                 UUID assetID;
        //                 if (UUID.TryParse(id, out assetID))
        //                     asset.FullID = assetID;

        //                 // Grab the asset data from the response stream
        //                 using (MemoryStream stream = new MemoryStream())
        //                 {
        //                     responseStream.CopyStream(stream, Int32.MaxValue);
        //                     asset.Data = stream.ToArray();
        //                 }
        //             }
        //         }

        //         // Cache store
        //         if (m_cache != null && asset != null)
        //             m_cache.Cache(asset);

        //         return asset;
        //     }
        //     catch (Exception ex)
        //     {
        //         m_log.Warn("[SIMIAN ASSET CONNECTOR]: Asset GET from " + url + " failed: " + ex.Message);
        //         return null;
        //     }
        // }

        // private string StoreRemote(AssetBase asset)
        // {
        //     // Distinguish public and private assets
        //     bool isPublic = true;
        //     switch ((AssetType)asset.Type)
        //     {
        //         case AssetType.CallingCard:
        //         case AssetType.Gesture:
        //         case AssetType.LSLBytecode:
        //         case AssetType.LSLText:
        //             isPublic = false;
        //             break;
        //     }

        //     string errorMessage = null;
            
        //     // Build the remote storage request
        //     List<MultipartForm.Element> postParameters = new List<MultipartForm.Element>()
        //     {
        //         new MultipartForm.Parameter("AssetID", asset.FullID.ToString()),
        //         new MultipartForm.Parameter("CreatorID", asset.Metadata.CreatorID),
        //         new MultipartForm.Parameter("Temporary", asset.Temporary ? "1" : "0"),
        //         new MultipartForm.Parameter("Public", isPublic ? "1" : "0"),
        //         new MultipartForm.File("Asset", asset.Name, asset.Metadata.ContentType, asset.Data)
        //     };

        //     // Make the remote storage request
        //     try
        //     {
        //         // Simian does not require the asset ID to be in the URL because it's in the post data.
        //         // By appending it to the URL also, we allow caching proxies (squid) to invalidate asset URLs
        //         HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(m_serverUrl + asset.FullID.ToString());

        //         using (HttpWebResponse response = MultipartForm.Post(request, postParameters))
        //         {
        //             using (Stream responseStream = response.GetResponseStream())
        //             {
        //                 string responseStr = null;

        //                 try
        //                 {
        //                     responseStr = responseStream.GetStreamString();
        //                     OSD responseOSD = OSDParser.Deserialize(responseStr);
        //                     if (responseOSD.Type == OSDType.Map)
        //                     {
        //                         OSDMap responseMap = (OSDMap)responseOSD;
        //                         if (responseMap["Success"].AsBoolean())
        //                             return asset.ID;
        //                         else
        //                             errorMessage = "Upload failed: " + responseMap["Message"].AsString();
        //                     }
        //                     else
        //                     {
        //                         errorMessage = "Response format was invalid:\n" + responseStr;
        //                     }
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     if (!String.IsNullOrEmpty(responseStr))
        //                         errorMessage = "Failed to parse the response:\n" + responseStr;
        //                     else
        //                         errorMessage = "Failed to retrieve the response: " + ex.Message;
        //                 }
        //             }
        //         }
        //     }
        //     catch (WebException ex)
        //     {
        //         errorMessage = ex.Message;
        //     }

        //     m_log.WarnFormat("[SIMIAN ASSET CONNECTOR]: Failed to store asset \"{0}\" ({1}, {2}): {3}",
        //         asset.Name, asset.ID, asset.Metadata.ContentType, errorMessage);

        //     return null;
        // }
    }
}
