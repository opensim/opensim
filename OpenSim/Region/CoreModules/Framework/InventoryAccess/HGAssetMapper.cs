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
using System.Reflection;
using System.Text;

using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess
{
    public class HGAssetMapper
    {
        #region Fields
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // This maps between inventory server urls and inventory server clients
//        private Dictionary<string, InventoryClient> m_inventoryServers = new Dictionary<string, InventoryClient>();

        private Scene m_scene;
        private string m_HomeURI;

        #endregion

        #region Constructor

        public HGAssetMapper(Scene scene, string homeURL)
        {
            m_scene = scene;
            m_HomeURI = homeURL;
        }

        #endregion

        #region Internal functions

        private AssetMetadata FetchMetadata(string url, UUID assetID)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + "/";

            AssetMetadata meta = m_scene.AssetService.GetMetadata(url + assetID.ToString());

            if (meta != null)
                m_log.DebugFormat("[HG ASSET MAPPER]: Fetched metadata for asset {0} of type {1} from {2} ", assetID, meta.Type, url);
            else
                m_log.DebugFormat("[HG ASSET MAPPER]: Unable to fetched metadata for asset {0} from {1} ", assetID, url);

            return meta;
        }

        private AssetBase FetchAsset(string url, UUID assetID)
        {
            // Test if it's already here
            AssetBase asset = m_scene.AssetService.Get(assetID.ToString());
            if (asset == null)
            {
                if (string.IsNullOrEmpty(url))
                    return null;

                if (!url.EndsWith("/") && !url.EndsWith("="))
                    url = url + "/";

                asset = m_scene.AssetService.Get(url + assetID.ToString());

                //if (asset != null)
                //    m_log.DebugFormat("[HG ASSET MAPPER]: Fetched asset {0} of type {1} from {2} ", assetID, asset.Metadata.Type, url);
                //else
                //    m_log.DebugFormat("[HG ASSET MAPPER]: Unable to fetch asset {0} from {1} ", assetID, url);

            }

            return asset;
        }

        public bool PostAsset(string url, AssetBase asset, bool verbose = true)
        {
            if (asset == null)
            {
                m_log.Warn("[HG ASSET MAPPER]: Tried to post asset to remote server, but asset not in local cache.");
                return false;
            }

            if (string.IsNullOrEmpty(url))
                return false;

            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + "/";

            // See long comment in AssetCache.AddAsset
            if (asset.Temporary || asset.Local)
                return true;

            // We need to copy the asset into a new asset, because
            // we need to set its ID to be URL+UUID, so that the
            // HGAssetService dispatches it to the remote grid.
            // It's not pretty, but the best that can be done while
            // not having a global naming infrastructure
            AssetBase asset1 = new AssetBase(asset.FullID, asset.Name, asset.Type, asset.Metadata.CreatorID);
            Copy(asset, asset1);
            asset1.ID = url + asset.ID;

            AdjustIdentifiers(asset1.Metadata);
            if (asset1.Metadata.Type == (sbyte)AssetType.Object)
                asset1.Data = AdjustIdentifiers(asset.Data);
            else
                asset1.Data = asset.Data;

            string id = m_scene.AssetService.Store(asset1);
            if (String.IsNullOrEmpty(id))
            {
                if (verbose)
                    m_log.DebugFormat("[HG ASSET MAPPER]: Asset server {0} did not accept {1}", url, asset.ID);
                return false;
            }

            if (verbose)
                m_log.DebugFormat("[HG ASSET MAPPER]: Posted copy of asset {0} from local asset server to {1}", asset1.ID, url);
            return true;
        }

        private void Copy(AssetBase from, AssetBase to)
        {
            //to.Data        = from.Data; // don't copy this, it's copied elsewhere
            to.Description = from.Description;
            to.FullID      = from.FullID;
            to.ID          = from.ID;
            to.Local       = from.Local;
            to.Name        = from.Name;
            to.Temporary   = from.Temporary;
            to.Type        = from.Type;

        }

        private void AdjustIdentifiers(AssetMetadata meta)
        {
            if (!string.IsNullOrEmpty(meta.CreatorID))
            {
                if(UUID.TryParse(meta.CreatorID, out UUID uuid))
                {
                    UserAccount creator = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid);
                    if (creator != null)
                        meta.CreatorID = m_HomeURI + ";" + creator.FirstName + " " + creator.LastName;
                }
            }
        }

        protected byte[] AdjustIdentifiers(byte[] data)
        {
            string xml = Utils.BytesToString(data);
            return Utils.StringToBytes(RewriteSOP(xml));
        }

        protected string RewriteSOP(string xmlData)
        {
//            Console.WriteLine("Input XML [{0}]", xmlData);
            return ExternalRepresentationUtils.RewriteSOP(xmlData, m_scene.Name, m_HomeURI, m_scene.UserAccountService, m_scene.RegionInfo.ScopeID);

        }

        // TODO: unused
        // private void Dump(Dictionary<UUID, bool> lst)
        // {
        //     m_log.Debug("XXX -------- UUID DUMP ------- XXX");
        //     foreach (KeyValuePair<UUID, bool> kvp in lst)
        //         m_log.Debug(" >> " + kvp.Key + " (texture? " + kvp.Value + ")");
        //     m_log.Debug("XXX -------- UUID DUMP ------- XXX");
        // }

        #endregion


        #region Public interface

        public void Get(UUID assetID, UUID ownerID, string userAssetURL)
        {
            // Get the item from the remote asset server onto the local AssetService

            AssetMetadata meta = FetchMetadata(userAssetURL, assetID);
            if (meta == null)
                return;

            // The act of gathering UUIDs downloads some assets from the remote server
            // but not all...
            HGUuidGatherer uuidGatherer = new HGUuidGatherer(m_scene.AssetService, userAssetURL);
            uuidGatherer.AddForInspection(assetID);
            uuidGatherer.GatherAll();

            m_log.DebugFormat("[HG ASSET MAPPER]: Preparing to get {0} assets", uuidGatherer.GatheredUuids.Count);
            bool success = true;
            foreach (UUID uuid in uuidGatherer.GatheredUuids.Keys)
                if (FetchAsset(userAssetURL, uuid) == null)
                    success = false;

            // maybe all pieces got here...
            if (!success)
                m_log.DebugFormat("[HG ASSET MAPPER]: Problems getting item {0} from asset server {1}", assetID, userAssetURL);
            else
                m_log.DebugFormat("[HG ASSET MAPPER]: Successfully got item {0} from asset server {1}", assetID, userAssetURL);
        }

        public void Post(UUID assetID, UUID ownerID, string userAssetURL)
        {
            AssetBase asset = m_scene.AssetService.Get(assetID.ToString());
            if (asset == null)
            {
                m_log.DebugFormat("[HG ASSET MAPPER POST]: Something wrong with asset {0}, it could not be found", assetID);
                return;
            }
            m_log.DebugFormat("[HG ASSET MAPPER  POST]: Starting to send asset {0} to asset server {1}", assetID, userAssetURL);

            // Find all the embedded assets
            HGUuidGatherer uuidGatherer = new HGUuidGatherer(m_scene.AssetService, string.Empty);
            uuidGatherer.AddForInspection(asset.FullID);
            uuidGatherer.GatherAll();

            // Check which assets already exist in the destination server

            string url = userAssetURL;
            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + "/";

            string[] remoteAssetIDs = new string[uuidGatherer.GatheredUuids.Count];
            int i = 0;
            foreach (UUID id in uuidGatherer.GatheredUuids.Keys)
                remoteAssetIDs[i++] = url + id.ToString();

            bool[] exist;
            try
            {
                exist = m_scene.AssetService.AssetsExist(remoteAssetIDs);
            }
            catch
            {
                m_log.DebugFormat("[HG ASSET MAPPER POST]: Problems sending asset {0} to asset server {1}", assetID, userAssetURL);
                return;
            }

            var existSet = new HashSet<string>();
            i = 0;
            foreach (UUID id in uuidGatherer.GatheredUuids.Keys)
            {
                if (exist[i])
                    existSet.Add(id.ToString());
                ++i;
            }

            // Send only those assets which don't already exist in the destination server

            bool success = true;
            var notFound = new List<string>();
            var posted = new List<string>();

            foreach (UUID uuid in uuidGatherer.GatheredUuids.Keys)
            {
                string idstr = uuid.ToString();
                if (existSet.Contains(idstr))
                    continue;

                asset = m_scene.AssetService.Get(idstr);
                if (asset == null)
                {   
                    notFound.Add(idstr);
                    continue;
                }

                try
                {
                    bool b = PostAsset(userAssetURL, asset, false);
                    if(b)
                        posted.Add(idstr);
                    success &= b;
                }
                catch (Exception e)
                {
                    m_log.Error(
                        string.Format(
                            "[HG ASSET MAPPER POST]: Failed to post asset {0} (type {1}, length {2}) referenced from {3} to {4} with exception  ",
                            asset.ID, asset.Type, asset.Data.Length, assetID, userAssetURL),
                        e);

                    // For debugging purposes for now we will continue to throw the exception up the stack as was already happening.  However, after
                    // debugging we may want to simply report the failure if we can tell this is due to a failure
                    // with a particular asset and not a destination network failure where all asset posts will fail (and
                    // generate large amounts of log spam).
                    throw e;
                }
            }
            StringBuilder sb = null;
            if (notFound.Count > 0)
            {
                if (sb == null)
                    sb = new StringBuilder(512);
                i = notFound.Count;
                sb.Append("[HG ASSET MAPPER POST]: Missing assets:\n\t");
                for (int j = 0; j < notFound.Count; ++j)
                {
                    sb.Append(notFound[j]);
                    if (i < j)
                        sb.Append(',');
                }
                m_log.Debug(sb.ToString());
                sb.Clear();
            }
            if (existSet.Count > 0)
            {
                if (sb == null) 
                    sb = new StringBuilder(512);
                i = existSet.Count;
                sb.Append("[HG ASSET MAPPER POST]: Already at destination server:\n\t");
                foreach (UUID id in existSet)
                {
                    sb.Append(id);
                    if (--i > 0)
                        sb.Append(',');
                }
                m_log.Debug(sb.ToString());
                sb.Clear();
            }
            if (posted.Count > 0)
            {
                if (sb == null) 
                    sb = new StringBuilder(512);
                i = posted.Count;
                sb.Append("[HG ASSET MAPPER POST]: Posted assets:\n\t");
                for (int j = 0; j < posted.Count; ++j)
                {
                    sb.Append(posted[j]);
                    if (i < j)
                        sb.Append(',');
                }
                m_log.Debug(sb.ToString());
            }

            if (!success)
                m_log.DebugFormat("[HG ASSET MAPPER POST]: Problems sending asset {0} to asset server {1}", assetID, userAssetURL);
            else
                m_log.DebugFormat("[HG ASSET MAPPER POST]: Successfully sent asset {0} to asset server {1}", assetID, userAssetURL);
        }

        #endregion

    }
}
