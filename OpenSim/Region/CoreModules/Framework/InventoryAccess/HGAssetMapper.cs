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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;

using log4net;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

//using HyperGrid.Framework;
//using OpenSim.Region.Communications.Hypergrid;

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

        public AssetMetadata FetchMetadata(string url, UUID assetID)
        {
            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + "/";

            AssetMetadata meta = m_scene.AssetService.GetMetadata(url + assetID.ToString());

            if (meta != null)
                m_log.DebugFormat("[HG ASSET MAPPER]: Fetched metadata for asset {0} of type {1} from {2} ", assetID, meta.Type, url);
            else
                m_log.DebugFormat("[HG ASSET MAPPER]: Unable to fetched metadata for asset {0} from {1} ", assetID, url);

            return meta;
        }

        public bool PostAsset(string url, AssetBase asset)
        {
            if (asset != null)
            {
                if (!url.EndsWith("/") && !url.EndsWith("="))
                    url = url + "/";

                bool success = true;
                // See long comment in AssetCache.AddAsset
                if (!asset.Temporary || asset.Local)
                {
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
                    if (id == string.Empty)
                    {
                        m_log.DebugFormat("[HG ASSET MAPPER]: Asset server {0} did not accept {1}", url, asset.ID);
                        success = false;
                    }
                    else
                        m_log.DebugFormat("[HG ASSET MAPPER]: Posted copy of asset {0} from local asset server to {1}", asset1.ID, url);
                }
                return success;
            }
            else
                m_log.Warn("[HG ASSET MAPPER]: Tried to post asset to remote server, but asset not in local cache.");

            return false;
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
            if (meta.CreatorID != null && meta.CreatorID != string.Empty)
            {
                UUID uuid = UUID.Zero;
                UUID.TryParse(meta.CreatorID, out uuid);
                UserAccount creator = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid); 
                if (creator != null)
                    meta.CreatorID = m_HomeURI + ";" + creator.FirstName + " " + creator.LastName;
            }
        }

        protected byte[] AdjustIdentifiers(byte[] data)
        {
            string xml = Utils.BytesToString(data);
            return Utils.StringToBytes(RewriteSOP(xml));
        }

        protected string RewriteSOP(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList sops = doc.GetElementsByTagName("SceneObjectPart");

            foreach (XmlNode sop in sops)
            {
                UserAccount creator = null;
                bool hasCreatorData = false;
                XmlNodeList nodes = sop.ChildNodes;
                foreach (XmlNode node in nodes)
                {
                    if (node.Name == "CreatorID")
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(node.InnerText, out uuid);
                        creator = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid);
                    }
                    if (node.Name == "CreatorData" && node.InnerText != null && node.InnerText != string.Empty)
                        hasCreatorData = true;

                    //if (node.Name == "OwnerID")
                    //{
                    //    UserAccount owner = GetUser(node.InnerText);
                    //    if (owner != null)
                    //        node.InnerText = m_ProfileServiceURL + "/" + node.InnerText + "/" + owner.FirstName + " " + owner.LastName;
                    //}
                }

                if (!hasCreatorData && creator != null)
                {
                    XmlElement creatorData = doc.CreateElement("CreatorData");
                    creatorData.InnerText = m_HomeURI + ";" + creator.FirstName + " " + creator.LastName;
                    sop.AppendChild(creatorData);
                }
            }

            using (StringWriter wr = new StringWriter())
            {
                doc.Save(wr);
                return wr.ToString();
            }

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

            // The act of gathering UUIDs downloads the assets from the remote server
            Dictionary<UUID, AssetType> ids = new Dictionary<UUID, AssetType>();
            HGUuidGatherer uuidGatherer = new HGUuidGatherer(m_scene.AssetService, userAssetURL);
            uuidGatherer.GatherAssetUuids(assetID, (AssetType)meta.Type, ids);

        }


        public void Post(UUID assetID, UUID ownerID, string userAssetURL)
        {
                // Post the item from the local AssetCache onto the remote asset server
                // and place an entry in m_assetMap

            m_log.Debug("[HG ASSET MAPPER]: Posting object " + assetID + " to asset server " + userAssetURL);
            AssetBase asset = m_scene.AssetService.Get(assetID.ToString());
            if (asset != null)
            {
                Dictionary<UUID, AssetType> ids = new Dictionary<UUID, AssetType>();
                HGUuidGatherer uuidGatherer = new HGUuidGatherer(m_scene.AssetService, string.Empty);
                uuidGatherer.GatherAssetUuids(asset.FullID, (AssetType)asset.Type, ids);
                bool success = false;
                foreach (UUID uuid in ids.Keys)
                {
                    asset = m_scene.AssetService.Get(uuid.ToString());
                    if (asset == null)
                        m_log.DebugFormat("[HG ASSET MAPPER]: Could not find asset {0}", uuid);
                    else
                        success = PostAsset(userAssetURL, asset);
                }

                // maybe all pieces got there...
                if (!success)
                    m_log.DebugFormat("[HG ASSET MAPPER]: Problems posting item {0} to asset server {1}", assetID, userAssetURL);
                else
                    m_log.DebugFormat("[HG ASSET MAPPER]: Successfully posted item {0} to asset server {1}", assetID, userAssetURL);

            }
            else
                m_log.DebugFormat("[HG ASSET MAPPER]: Something wrong with asset {0}, it could not be found", assetID);

        }

        #endregion

    }
}
