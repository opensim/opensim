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

        public bool PostAsset(string url, AssetBase asset)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            if (!url.EndsWith("/") && !url.EndsWith("="))
                url = url + "/";

            if (asset == null)
            {
                m_log.Warn("[HG ASSET MAPPER]: Tried to post asset to remote server, but asset not in local cache.");
                return false;
            }

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
                m_log.DebugFormat("[HG ASSET MAPPER]: Asset server {0} did not accept {1}", url, asset.ID);
                return false;
            }
            else {
                m_log.DebugFormat("[HG ASSET MAPPER]: Posted copy of asset {0} from local asset server to {1}", asset1.ID, url);
                return true;
            }
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

        protected void TransformXml(XmlReader reader, XmlWriter writer)
        {
//            m_log.DebugFormat("[HG ASSET MAPPER]: Transforming XML");

            int sopDepth = -1;
            UserAccount creator = null;
            bool hasCreatorData = false;

            while (reader.Read())
            {
//                Console.WriteLine("Depth: {0}, name {1}", reader.Depth, reader.Name);

                switch (reader.NodeType)
                {
                    case XmlNodeType.Attribute:
//                    Console.WriteLine("FOUND ATTRIBUTE {0}", reader.Name);
                    writer.WriteAttributeString(reader.Prefix, reader.Name, reader.NamespaceURI, reader.Value);
                    break;

                    case XmlNodeType.CDATA:
                    writer.WriteCData(reader.Value);
                    break;

                    case XmlNodeType.Comment:
                    writer.WriteComment(reader.Value);
                    break;

                    case XmlNodeType.DocumentType:
                    writer.WriteDocType(reader.Name, reader.Value, null, null);
                    break;

                    case XmlNodeType.Element: 
//                    m_log.DebugFormat("Depth {0} at element {1}", reader.Depth, reader.Name);

                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);

                    if (reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                            writer.WriteAttributeString(reader.Prefix, reader.Name, reader.NamespaceURI, reader.Value);

                        reader.MoveToElement();
                    }

                    if (reader.LocalName == "SceneObjectPart")
                    {
                        if (sopDepth < 0)
                        {
                            sopDepth = reader.Depth;
//                            m_log.DebugFormat("[HG ASSET MAPPER]: Set sopDepth to {0}", sopDepth);
                        }
                    }
                    else
                    {
                        if (sopDepth >= 0 && reader.Depth == sopDepth + 1)
                        {
                            if (reader.Name == "CreatorID")
                            {
                                reader.Read();
                                if (reader.NodeType == XmlNodeType.Element && reader.Name == "Guid" || reader.Name == "UUID")
                                {
                                    reader.Read();

                                    if (reader.NodeType == XmlNodeType.Text)
                                    {
                                        UUID uuid = UUID.Zero;
                                        UUID.TryParse(reader.Value, out uuid);
                                        creator = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid);
                                        writer.WriteElementString("UUID", reader.Value);
                                        reader.Read();
                                    }
                                    else
                                    {
                                        // If we unexpected run across mixed content in this node, still carry on
                                        // transforming the subtree (this replicates earlier behaviour).
                                        TransformXml(reader, writer);
                                    }
                                }
                                else
                                {
                                    // If we unexpected run across mixed content in this node, still carry on
                                    // transforming the subtree (this replicates earlier behaviour).
                                    TransformXml(reader, writer);
                                }
                            }
                            else if (reader.Name == "CreatorData")
                            {
                                reader.Read();
                                if (reader.NodeType == XmlNodeType.Text)
                                {
                                    hasCreatorData = true;
                                    writer.WriteString(reader.Value);
                                }
                                else
                                {
                                    // If we unexpected run across mixed content in this node, still carry on
                                    // transforming the subtree (this replicates earlier behaviour).
                                    TransformXml(reader, writer);
                                }
                            }
                        }
                    }
                                        
                    if (reader.IsEmptyElement)
                    {
//                        m_log.DebugFormat("[HG ASSET MAPPER]: Writing end for empty element {0}", reader.Name);
                        writer.WriteEndElement();
                    }

                    break;

                    case XmlNodeType.EndElement:
//                    m_log.DebugFormat("Depth {0} at EndElement", reader.Depth);
                    if (sopDepth == reader.Depth)
                    {
                        if (!hasCreatorData && creator != null)
                            writer.WriteElementString(reader.Prefix, "CreatorData", reader.NamespaceURI, string.Format("{0};{1} {2}", m_HomeURI, creator.FirstName, creator.LastName));

//                        m_log.DebugFormat("[HG ASSET MAPPER]: Reset sopDepth");
                        sopDepth = -1;
                        creator = null;
                        hasCreatorData = false;
                    }
                    writer.WriteEndElement();
                    break;

                    case XmlNodeType.EntityReference:
                    writer.WriteEntityRef(reader.Name);
                    break;

                    case XmlNodeType.ProcessingInstruction:
                    writer.WriteProcessingInstruction(reader.Name, reader.Value);
                    break;

                    case XmlNodeType.Text:
                    writer.WriteString(reader.Value);
                    break;

                    case XmlNodeType.XmlDeclaration:
                    // For various reasons, not all serializations have xml declarations (or consistent ones) 
                    // and as it's embedded inside a byte stream we don't need it anyway, so ignore.
                    break;

                    default:
                    m_log.WarnFormat(
                        "[HG ASSET MAPPER]: Unrecognized node {0} in asset XML transform in {1}", 
                        reader.NodeType, m_scene.Name);
                    break;
                }
            }
        }

        protected string RewriteSOP(string xmlData)
        {
//            Console.WriteLine("Input XML [{0}]", xmlData);

            using (StringWriter sw = new StringWriter())
            using (XmlTextWriter writer = new XmlTextWriter(sw))
            using (XmlTextReader wrappedReader = new XmlTextReader(xmlData, XmlNodeType.Element, null))
            using (XmlReader reader = XmlReader.Create(wrappedReader, new XmlReaderSettings() { IgnoreWhitespace = true, ConformanceLevel = ConformanceLevel.Fragment }))
            {
                TransformXml(reader, writer);

//                Console.WriteLine("Output: [{0}]", sw.ToString());

                return sw.ToString();
            }

            // We are now taking the more complex streaming approach above because some assets can be very large
            // and can trigger higher CPU use or possibly memory problems.
//            XmlDocument doc = new XmlDocument();
//            doc.LoadXml(xml);
//            XmlNodeList sops = doc.GetElementsByTagName("SceneObjectPart");
//
//            foreach (XmlNode sop in sops)
//            {
//                UserAccount creator = null;
//                bool hasCreatorData = false;
//                XmlNodeList nodes = sop.ChildNodes;
//                foreach (XmlNode node in nodes)
//                {
//                    if (node.Name == "CreatorID")
//                    {
//                        UUID uuid = UUID.Zero;
//                        UUID.TryParse(node.InnerText, out uuid);
//                        creator = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid);
//                    }
//                    if (node.Name == "CreatorData" && node.InnerText != null && node.InnerText != string.Empty)
//                        hasCreatorData = true;
//
//                    //if (node.Name == "OwnerID")
//                    //{
//                    //    UserAccount owner = GetUser(node.InnerText);
//                    //    if (owner != null)
//                    //        node.InnerText = m_ProfileServiceURL + "/" + node.InnerText + "/" + owner.FirstName + " " + owner.LastName;
//                    //}
//                }
//
//                if (!hasCreatorData && creator != null)
//                {
//                    XmlElement creatorData = doc.CreateElement("CreatorData");
//                    creatorData.InnerText = m_HomeURI + ";" + creator.FirstName + " " + creator.LastName;
//                    sop.AppendChild(creatorData);
//                }
//            }
//
//            using (StringWriter wr = new StringWriter())
//            {
//                doc.Save(wr);
//                return wr.ToString();
//            }
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
            m_log.DebugFormat("[HG ASSET MAPPER]: Starting to send asset {0} with children to asset server {1}", assetID, userAssetURL);

            // Find all the embedded assets

            AssetBase asset = m_scene.AssetService.Get(assetID.ToString());
            if (asset == null)
            {
                m_log.DebugFormat("[HG ASSET MAPPER]: Something wrong with asset {0}, it could not be found", assetID);
                return;
            }

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

            bool[] exist = m_scene.AssetService.AssetsExist(remoteAssetIDs);

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

            foreach (UUID uuid in uuidGatherer.GatheredUuids.Keys)
            {
                if (!existSet.Contains(uuid.ToString()))
                {
                    asset = m_scene.AssetService.Get(uuid.ToString());
                    if (asset == null)
                    {
                        m_log.DebugFormat("[HG ASSET MAPPER]: Could not find asset {0}", uuid);
                    }
                    else
                    {
                        try
                        {
                            success &= PostAsset(userAssetURL, asset);
                        }
                        catch (Exception e)
                        {
                            m_log.Error(
                                string.Format(
                                    "[HG ASSET MAPPER]: Failed to post asset {0} (type {1}, length {2}) referenced from {3} to {4} with exception  ", 
                                    asset.ID, asset.Type, asset.Data.Length, assetID, userAssetURL), 
                                e);

                            // For debugging purposes for now we will continue to throw the exception up the stack as was already happening.  However, after
                            // debugging we may want to simply report the failure if we can tell this is due to a failure
                            // with a particular asset and not a destination network failure where all asset posts will fail (and
                            // generate large amounts of log spam).
                            throw e;
                        }
                    }
                }
                else
                {
                    m_log.DebugFormat(
                        "[HG ASSET MAPPER]: Didn't post asset {0} referenced from {1} because it already exists in asset server {2}", 
                        uuid, assetID, userAssetURL);
                }
            }

            if (!success)
                m_log.DebugFormat("[HG ASSET MAPPER]: Problems sending asset {0} with children to asset server {1}", assetID, userAssetURL);
            else
                m_log.DebugFormat("[HG ASSET MAPPER]: Successfully sent asset {0} with children to asset server {1}", assetID, userAssetURL);
        }

        #endregion

    }
}
