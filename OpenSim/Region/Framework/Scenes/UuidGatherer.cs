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
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Gather uuids for a given entity.
    /// </summary>
    /// <remarks>
    /// This does a deep inspection of the entity to retrieve all the assets it uses (whether as textures, as scripts
    /// contained in inventory, as scripts contained in objects contained in another object's inventory, etc.  Assets
    /// are only retrieved when they are necessary to carry out the inspection (i.e. a serialized object needs to be
    /// retrieved to work out which assets it references).
    /// </remarks>
    public class UuidGatherer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IAssetService m_assetService;

//        /// <summary>
//        /// Used as a temporary store of an asset which represents an object.  This can be a null if no appropriate
//        /// asset was found by the asset service.
//        /// </summary>
//        private AssetBase m_requestedObjectAsset;
//
//        /// <summary>
//        /// Signal whether we are currently waiting for the asset service to deliver an asset.
//        /// </summary>
//        private bool m_waitingForObjectAsset;
                
        public UuidGatherer(IAssetService assetService)
        {
            m_assetService = assetService;
        }
                
        /// <summary>
        /// Gather all the asset uuids associated with the asset referenced by a given uuid
        /// </summary>
        /// <remarks>
        /// This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </remarks>
        /// <param name="assetUuid">The uuid of the asset for which to gather referenced assets</param>
        /// <param name="assetType">The type of the asset for the uuid given</param>
        /// <param name="assetUuids">The assets gathered</param>
        public void GatherAssetUuids(UUID assetUuid, AssetType assetType, IDictionary<UUID, AssetType> assetUuids)
        {
            // avoid infinite loops
            if (assetUuids.ContainsKey(assetUuid))
                return;

            try
            {               
                assetUuids[assetUuid] = assetType;
    
                if (AssetType.Bodypart == assetType || AssetType.Clothing == assetType)
                {
                    GetWearableAssetUuids(assetUuid, assetUuids);
                }
                else if (AssetType.Gesture == assetType)
                {
                    GetGestureAssetUuids(assetUuid, assetUuids);
                }
                else if (AssetType.Notecard == assetType)
                {
                    GetTextEmbeddedAssetUuids(assetUuid, assetUuids);
                }
                else if (AssetType.LSLText == assetType)
                {
                    GetTextEmbeddedAssetUuids(assetUuid, assetUuids);
                }
                else if (AssetType.Object == assetType)
                {
                    GetSceneObjectAssetUuids(assetUuid, assetUuids);
                }
            }
            catch (Exception)
            {
                m_log.ErrorFormat(
                    "[UUID GATHERER]: Failed to gather uuids for asset id {0}, type {1}", 
                    assetUuid, assetType);
                throw;
            }
        }

        /// <summary>
        /// Gather all the asset uuids associated with a given object.
        /// </summary>
        /// <remarks>
        /// This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </remarks>
        /// <param name="sceneObject">The scene object for which to gather assets</param>
        /// <param name="assetUuids">The assets gathered</param>
        public void GatherAssetUuids(SceneObjectGroup sceneObject, IDictionary<UUID, AssetType> assetUuids)
        {
//            m_log.DebugFormat(
//                "[ASSET GATHERER]: Getting assets for object {0}, {1}", sceneObject.Name, sceneObject.UUID);

            SceneObjectPart[] parts = sceneObject.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

//                m_log.DebugFormat(
//                    "[ARCHIVER]: Getting part {0}, {1} for object {2}", part.Name, part.UUID, sceneObject.UUID);

                try
                {
                    Primitive.TextureEntry textureEntry = part.Shape.Textures;
                    if (textureEntry != null)
                    {
                        // Get the prim's default texture.  This will be used for faces which don't have their own texture
                        if (textureEntry.DefaultTexture != null)
                            assetUuids[textureEntry.DefaultTexture.TextureID] = AssetType.Texture;

                        if (textureEntry.FaceTextures != null)
                        {
                            // Loop through the rest of the texture faces (a non-null face means the face is different from DefaultTexture)
                            foreach (Primitive.TextureEntryFace texture in textureEntry.FaceTextures)
                            {
                                if (texture != null)
                                    assetUuids[texture.TextureID] = AssetType.Texture;
                            }
                        }
                    }
                    
                    // If the prim is a sculpt then preserve this information too
                    if (part.Shape.SculptTexture != UUID.Zero)
                        assetUuids[part.Shape.SculptTexture] = AssetType.Texture;
                    
                    TaskInventoryDictionary taskDictionary = (TaskInventoryDictionary)part.TaskInventory.Clone();
                    
                    // Now analyze this prim's inventory items to preserve all the uuids that they reference
                    foreach (TaskInventoryItem tii in taskDictionary.Values)
                    {
//                        m_log.DebugFormat(
//                            "[ARCHIVER]: Analysing item {0} asset type {1} in {2} {3}", 
//                            tii.Name, tii.Type, part.Name, part.UUID);

                        if (!assetUuids.ContainsKey(tii.AssetID))
                            GatherAssetUuids(tii.AssetID, (AssetType)tii.Type, assetUuids);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[UUID GATHERER]: Failed to get part - {0}", e);
                    m_log.DebugFormat(
                        "[UUID GATHERER]: Texture entry length for prim was {0} (min is 46)", 
                        part.Shape.TextureEntry.Length);
                }
            }
        }
        
//        /// <summary>
//        /// The callback made when we request the asset for an object from the asset service.
//        /// </summary>
//        private void AssetReceived(string id, Object sender, AssetBase asset)
//        {
//            lock (this)
//            {
//                m_requestedObjectAsset = asset;
//                m_waitingForObjectAsset = false;
//                Monitor.Pulse(this);
//            }
//        }

        /// <summary>
        /// Get an asset synchronously, potentially using an asynchronous callback.  If the
        /// asynchronous callback is used, we will wait for it to complete.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        protected virtual AssetBase GetAsset(UUID uuid)
        {
            return m_assetService.Get(uuid.ToString());

            // XXX: Switching to do this synchronously where the call was async before but we always waited for it
            // to complete anyway!
//            m_waitingForObjectAsset = true;
//            m_assetCache.Get(uuid.ToString(), this, AssetReceived);
//
//            // The asset cache callback can either
//            //
//            // 1. Complete on the same thread (if the asset is already in the cache) or
//            // 2. Come in via a different thread (if we need to go fetch it).
//            //
//            // The code below handles both these alternatives.
//            lock (this)
//            {
//                if (m_waitingForObjectAsset)
//                {
//                    Monitor.Wait(this);
//                    m_waitingForObjectAsset = false;
//                }
//            }
//
//            return m_requestedObjectAsset;
        }

        /// <summary>
        /// Record the asset uuids embedded within the given script.
        /// </summary>
        /// <param name="scriptUuid"></param>
        /// <param name="assetUuids">Dictionary in which to record the references</param>
        private void GetTextEmbeddedAssetUuids(UUID embeddingAssetId, IDictionary<UUID, AssetType> assetUuids)
        {
//            m_log.DebugFormat("[ASSET GATHERER]: Getting assets for uuid references in asset {0}", embeddingAssetId);

            AssetBase embeddingAsset = GetAsset(embeddingAssetId);

            if (null != embeddingAsset)
            {
                string script = Utils.BytesToString(embeddingAsset.Data);
//                m_log.DebugFormat("[ARCHIVER]: Script {0}", script);
                MatchCollection uuidMatches = Util.PermissiveUUIDPattern.Matches(script);
//                m_log.DebugFormat("[ARCHIVER]: Found {0} matches in text", uuidMatches.Count);

                foreach (Match uuidMatch in uuidMatches)
                {
                    UUID uuid = new UUID(uuidMatch.Value);
//                    m_log.DebugFormat("[ARCHIVER]: Recording {0} in text", uuid);

                    // Assume AssetIDs embedded are textures.
                    assetUuids[uuid] = AssetType.Texture;
                }
            }
        }

        /// <summary>
        /// Record the uuids referenced by the given wearable asset
        /// </summary>
        /// <param name="wearableAssetUuid"></param>
        /// <param name="assetUuids">Dictionary in which to record the references</param>
        private void GetWearableAssetUuids(UUID wearableAssetUuid, IDictionary<UUID, AssetType> assetUuids)
        {
            AssetBase assetBase = GetAsset(wearableAssetUuid);

            if (null != assetBase)
            {
                //m_log.Debug(new System.Text.ASCIIEncoding().GetString(bodypartAsset.Data));
                AssetWearable wearableAsset = new AssetBodypart(wearableAssetUuid, assetBase.Data);
                wearableAsset.Decode();
    
                //m_log.DebugFormat(
                //    "[ARCHIVER]: Wearable asset {0} references {1} assets", wearableAssetUuid, wearableAsset.Textures.Count);
    
                foreach (UUID uuid in wearableAsset.Textures.Values)
                {
                    assetUuids[uuid] = AssetType.Texture;
                }
            }
        }

        /// <summary>
        /// Get all the asset uuids associated with a given object.  This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="assetUuids"></param>
        private void GetSceneObjectAssetUuids(UUID sceneObjectUuid, IDictionary<UUID, AssetType> assetUuids)
        {
            AssetBase objectAsset = GetAsset(sceneObjectUuid);

            if (null != objectAsset)
            {
                string xml = Utils.BytesToString(objectAsset.Data);
                
                CoalescedSceneObjects coa;
                if (CoalescedSceneObjectsSerializer.TryFromXml(xml, out coa))
                {
                    foreach (SceneObjectGroup sog in coa.Objects)
                        GatherAssetUuids(sog, assetUuids);
                }
                else
                {
                    SceneObjectGroup sog = SceneObjectSerializer.FromOriginalXmlFormat(xml);
    
                    if (null != sog)
                        GatherAssetUuids(sog, assetUuids);
                }
            }
        }

        /// <summary>
        /// Get the asset uuid associated with a gesture
        /// </summary>
        /// <param name="gestureUuid"></param>
        /// <param name="assetUuids"></param>
        private void GetGestureAssetUuids(UUID gestureUuid, IDictionary<UUID, AssetType> assetUuids)
        {
            AssetBase assetBase = GetAsset(gestureUuid);
            if (null == assetBase)
                return;

            MemoryStream ms = new MemoryStream(assetBase.Data);
            StreamReader sr = new StreamReader(ms);

            sr.ReadLine(); // Unknown (Version?)
            sr.ReadLine(); // Unknown
            sr.ReadLine(); // Unknown
            sr.ReadLine(); // Name
            sr.ReadLine(); // Comment ?
            int count = Convert.ToInt32(sr.ReadLine()); // Item count

            for (int i = 0 ; i < count ; i++)
            {
                string type = sr.ReadLine();
                if (type == null)
                    break;
                string name = sr.ReadLine();
                if (name == null)
                    break;
                string id = sr.ReadLine();
                if (id == null)
                    break;
                string unknown = sr.ReadLine();
                if (unknown == null)
                    break;

                // If it can be parsed as a UUID, it is an asset ID
                UUID uuid;
                if (UUID.TryParse(id, out uuid))
                    assetUuids[uuid] = AssetType.Animation;
            }
        }
    }

    public class HGUuidGatherer : UuidGatherer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_assetServerURL;

        public HGUuidGatherer(IAssetService assetService, string assetServerURL)
            : base(assetService)
        {
            m_assetServerURL = assetServerURL;
            if (!m_assetServerURL.EndsWith("/") && !m_assetServerURL.EndsWith("="))
                m_assetServerURL = m_assetServerURL + "/";
        }

        protected override AssetBase GetAsset(UUID uuid)
        {
            if (string.Empty == m_assetServerURL)
                return base.GetAsset(uuid);
            else
                return FetchAsset(uuid);
        }

        public AssetBase FetchAsset(UUID assetID)
        {

            // Test if it's already here
            AssetBase asset = m_assetService.Get(assetID.ToString());
            if (asset == null)
            {
                // It's not, so fetch it from abroad
                asset = m_assetService.Get(m_assetServerURL + assetID.ToString());
                if (asset != null)
                    m_log.DebugFormat("[HGUUIDGatherer]: Copied asset {0} from {1} to local asset server", assetID, m_assetServerURL);
                else
                    m_log.DebugFormat("[HGUUIDGatherer]: Failed to fetch asset {0} from {1}", assetID, m_assetServerURL);
            }
            //else
            //    m_log.DebugFormat("[HGUUIDGatherer]: Asset {0} from {1} was already here", assetID, m_assetServerURL);

            return asset;
        }
    }
}
