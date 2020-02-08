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
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSimAssetType = OpenSim.Framework.SLUtil.OpenSimAssetType;

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

        /// <summary>
        /// Is gathering complete?
        /// </summary>
        public bool Complete { get { return m_assetUuidsToInspect.Count <= 0; } }

        /// <summary>
        /// The dictionary of UUIDs gathered so far.  If Complete == true then this is all the reachable UUIDs.
        /// </summary>
        /// <value>The gathered uuids.</value>
        public IDictionary<UUID, sbyte> GatheredUuids { get; private set; }
        public HashSet<UUID> FailedUUIDs { get; private set; }
        public HashSet<UUID> UncertainAssetsUUIDs { get; private set; }
        public int possibleNotAssetCount { get; set; }
        public int ErrorCount { get; private set; }
        private bool verbose = true;

        /// <summary>
        /// Gets the next UUID to inspect.
        /// </summary>
        /// <value>If there is no next UUID then returns null</value>
        public UUID? NextUuidToInspect
        {
            get
            {
                if (Complete)
                    return null;
                else
                    return m_assetUuidsToInspect.Peek();
            }
        }

        protected IAssetService m_assetService;

        protected Queue<UUID> m_assetUuidsToInspect;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Region.Framework.Scenes.UuidGatherer"/> class.
        /// </summary>
        /// <remarks>In this case the collection of gathered assets will start out blank.</remarks>
        /// <param name="assetService">
        /// Asset service.
        /// </param>
        public UuidGatherer(IAssetService assetService) : this(assetService, new Dictionary<UUID, sbyte>(),
                new HashSet <UUID>(),new HashSet <UUID>()) {}
        public UuidGatherer(IAssetService assetService, IDictionary<UUID, sbyte> collector) : this(assetService, collector,
            new HashSet <UUID>(), new HashSet <UUID>()) {}

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Region.Framework.Scenes.UuidGatherer"/> class.
        /// </summary>
        /// <param name="assetService">
        /// Asset service.
        /// </param>
        /// <param name="collector">
        /// Gathered UUIDs will be collected in this dictionary.
        /// It can be pre-populated if you want to stop the gatherer from analyzing assets that have already been fetched and inspected.
        /// </param>
        public UuidGatherer(IAssetService assetService, IDictionary<UUID, sbyte> collector, HashSet <UUID> failedIDs, HashSet <UUID> uncertainAssetsUUIDs)
        {
            m_assetService = assetService;
            GatheredUuids = collector;

            // FIXME: Not efficient for searching, can improve.
            m_assetUuidsToInspect = new Queue<UUID>();
            FailedUUIDs = failedIDs;
            UncertainAssetsUUIDs = uncertainAssetsUUIDs;
            ErrorCount = 0;
            possibleNotAssetCount = 0;
        }

        /// <summary>
        /// Adds the asset uuid for inspection during the gathering process.
        /// </summary>
        /// <returns><c>true</c>, if for inspection was added, <c>false</c> otherwise.</returns>
        /// <param name="uuid">UUID.</param>
        public bool AddForInspection(UUID uuid)
        {
            if(uuid == UUID.Zero)
                return false;

            if(FailedUUIDs.Contains(uuid))
            {
                if(UncertainAssetsUUIDs.Contains(uuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return false;
            }
            if(GatheredUuids.ContainsKey(uuid))
                return false;
            if (m_assetUuidsToInspect.Contains(uuid))
                return false;

//            m_log.DebugFormat("[UUID GATHERER]: Adding asset {0} for inspection", uuid);

            m_assetUuidsToInspect.Enqueue(uuid);
            return true;
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
        public void AddForInspection(SceneObjectGroup sceneObject)
        {
            //            m_log.DebugFormat(
            //                "[UUID GATHERER]: Getting assets for object {0}, {1}", sceneObject.Name, sceneObject.UUID);
            if(sceneObject.IsDeleted)
                return;

            SceneObjectPart[] parts = sceneObject.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                //                m_log.DebugFormat(
                //                    "[UUID GATHERER]: Getting part {0}, {1} for object {2}", part.Name, part.UUID, sceneObject.UUID);

                try
                {
                    Primitive.TextureEntry textureEntry = part.Shape.Textures;
                    if (textureEntry != null)
                    {
                        // Get the prim's default texture.  This will be used for faces which don't have their own texture
                        if (textureEntry.DefaultTexture != null)
                            RecordTextureEntryAssetUuids(textureEntry.DefaultTexture);

                        if (textureEntry.FaceTextures != null)
                        {
                            // Loop through the rest of the texture faces (a non-null face means the face is different from DefaultTexture)
                            foreach (Primitive.TextureEntryFace texture in textureEntry.FaceTextures)
                            {
                                if (texture != null)
                                    RecordTextureEntryAssetUuids(texture);
                            }
                        }
                    }

                    // If the prim is a sculpt then preserve this information too
                    if (part.Shape.SculptTexture != UUID.Zero)
                        GatheredUuids[part.Shape.SculptTexture] = (sbyte)AssetType.Texture;

                    if (part.Shape.ProjectionTextureUUID != UUID.Zero)
                        GatheredUuids[part.Shape.ProjectionTextureUUID] = (sbyte)AssetType.Texture;

                    UUID collisionSound = part.CollisionSound;
                    if ( collisionSound != UUID.Zero &&
                                collisionSound != part.invalidCollisionSoundUUID)
                        GatheredUuids[collisionSound] = (sbyte)AssetType.Sound;

                    if (part.ParticleSystem.Length > 0)
                    {
                        try
                        {
                            Primitive.ParticleSystem ps = new Primitive.ParticleSystem(part.ParticleSystem, 0);
                            if (ps.Texture != UUID.Zero)
                                GatheredUuids[ps.Texture] = (sbyte)AssetType.Texture;
                        }
                        catch (Exception)
                        {
                            m_log.WarnFormat(
                                "[UUID GATHERER]: Could not check particle system for part {0} {1} in object {2} {3} since it is corrupt.  Continuing.",
                                part.Name, part.UUID, sceneObject.Name, sceneObject.UUID);
                        }
                    }

                    TaskInventoryDictionary taskDictionary = (TaskInventoryDictionary)part.TaskInventory.Clone();

                    // Now analyze this prim's inventory items to preserve all the uuids that they reference
                    foreach (TaskInventoryItem tii in taskDictionary.Values)
                    {
                        //                        m_log.DebugFormat(
                        //                            "[ARCHIVER]: Analysing item {0} asset type {1} in {2} {3}",
                        //                            tii.Name, tii.Type, part.Name, part.UUID);
                        AddForInspection(tii.AssetID, (sbyte)tii.Type);
                    }

                    // FIXME: We need to make gathering modular but we cannot yet, since gatherers are not guaranteed
                    // to be called with scene objects that are in a scene (e.g. in the case of hg asset mapping and
                    // inventory transfer.  There needs to be a way for a module to register a method without assuming a
                    // Scene.EventManager is present.
                    //                    part.ParentGroup.Scene.EventManager.TriggerGatherUuids(part, assetUuids);


                    // still needed to retrieve textures used as materials for any parts containing legacy materials stored in DynAttrs
                    RecordMaterialsUuids(part);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[UUID GATHERER]: Failed to get part - {0}", e);
                }
            }
        }

        /// <summary>
        /// Gathers the next set of assets returned by the next uuid to get from the asset service.
        /// </summary>
        /// <returns>false if gathering is already complete, true otherwise</returns>
        public bool GatherNext()
        {
            if (Complete)
                return false;

            UUID nextToInspect = m_assetUuidsToInspect.Dequeue();

//            m_log.DebugFormat("[UUID GATHERER]: Inspecting asset {0}", nextToInspect);

            GetAssetUuids(nextToInspect);

            return m_assetUuidsToInspect.Count > 0;
        }

        /// <summary>
        /// Gathers all remaining asset UUIDS no matter how many calls are required to the asset service.
        /// </summary>
        /// <returns>false if gathering is already complete, true otherwise</returns>
        public bool GatherAll(bool report = false)
        {
            if (Complete)
                return false;
            if(report)
                verbose = false;

            while (GatherNext());

            if (report && FailedUUIDs.Count > 0)
            {
                StringBuilder sb = new StringBuilder(512);
                int i = FailedUUIDs.Count;
                sb.Append("[UUID GATHERER]: UUIDs that are not assets or really missing assets:\n\t");
                foreach (UUID id in FailedUUIDs)
                {
                    sb.Append(id);
                    if (--i > 0)
                        sb.Append(',');
                }
                m_log.Debug(sb.ToString());
            }

            return true;
        }

        /// <summary>
        /// Gather all the asset uuids associated with the asset referenced by a given uuid
        /// </summary>
        /// <remarks>
        /// This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// This method assumes that the asset type associated with this asset in persistent storage is correct (which
        /// should always be the case).  So with this method we always need to retrieve asset data even if the asset
        /// is of a type which is known not to reference any other assets
        /// </remarks>
        /// <param name="assetUuid">The uuid of the asset for which to gather referenced assets</param>
        private void GetAssetUuids(UUID assetUuid)
        {
            if(assetUuid == UUID.Zero)
                return;

            if(FailedUUIDs.Contains(assetUuid))
            {
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }

            // avoid infinite loops
            if (GatheredUuids.ContainsKey(assetUuid))
                return;

            AssetBase assetBase;
            try
            {
                assetBase = GetAsset(assetUuid);
            }
            catch (Exception e)
            {
                if(verbose)
                    m_log.ErrorFormat("[UUID GATHERER]: Failed to get asset {0} : {1}", assetUuid, e.Message);
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
                return;
            }

            if(assetBase == null)
            {
//                m_log.ErrorFormat("[UUID GATHERER]: asset {0} not found", assetUuid);
                FailedUUIDs.Add(assetUuid);
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }

            if(UncertainAssetsUUIDs.Contains(assetUuid))
                UncertainAssetsUUIDs.Remove(assetUuid);

            sbyte assetType = assetBase.Type;

            if(assetBase.Data == null || assetBase.Data.Length == 0)
            {
//                m_log.ErrorFormat("[UUID GATHERER]: asset {0}, type {1} has no data", assetUuid, assetType);
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
                return;
            }

            GatheredUuids[assetUuid] = assetType;
            try
            {
                if ((sbyte)AssetType.Bodypart == assetType || (sbyte)AssetType.Clothing == assetType)
                {
                    RecordWearableAssetUuids(assetBase);
                }
                else if ((sbyte)AssetType.Gesture == assetType)
                {
                    RecordGestureAssetUuids(assetBase);
                }
                else if ((sbyte)AssetType.Notecard == assetType)
                {
                    RecordNoteCardEmbeddedAssetUuids(assetBase);
                }
                else if ((sbyte)AssetType.LSLText == assetType)
                {
                    RecordTextEmbeddedAssetUuids(assetBase);
                }
                else if ((sbyte)OpenSimAssetType.Material == assetType)
                {
                    RecordMaterialAssetUuids(assetBase);
                }
                else if ((sbyte)AssetType.Object == assetType)
                {
                    RecordSceneObjectAssetUuids(assetBase);
                }
            }
            catch (Exception e)
            {
                if(verbose)
                    m_log.ErrorFormat("[UUID GATHERER]: Failed to gather uuids for asset with id {0} type {1}: {2}", assetUuid, assetType, e.Message);
                GatheredUuids.Remove(assetUuid);
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
            }
        }

        private void AddForInspection(UUID assetUuid, sbyte assetType)
        {
            if(assetUuid == UUID.Zero)
                return;

            // Here, we want to collect uuids which require further asset fetches but mark the others as gathered
            if(FailedUUIDs.Contains(assetUuid))
            {
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }
            if(GatheredUuids.ContainsKey(assetUuid))
                return;
            try
            {
                if ((sbyte)AssetType.Bodypart == assetType
                    || (sbyte)AssetType.Clothing == assetType
                    || (sbyte)AssetType.Gesture == assetType
                    || (sbyte)AssetType.Notecard == assetType
                    || (sbyte)AssetType.LSLText == assetType
                    || (sbyte)OpenSimAssetType.Material == assetType
                    || (sbyte)AssetType.Object == assetType)
                {
                    AddForInspection(assetUuid);
                }
                else
                {
                    GatheredUuids[assetUuid] = assetType;
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
        /// Collect all the asset uuids found in one face of a Texture Entry.
        /// </summary>
        private void RecordTextureEntryAssetUuids(Primitive.TextureEntryFace texture)
        {
            GatheredUuids[texture.TextureID] = (sbyte)AssetType.Texture;

            if (texture.MaterialID != UUID.Zero)
                AddForInspection(texture.MaterialID);
        }

        /// <summary>
        /// Gather all of the texture asset UUIDs used to reference "Materials" such as normal and specular maps
        /// stored in legacy format in part.DynAttrs
        /// </summary>
        /// <param name="part"></param>
        private void RecordMaterialsUuids(SceneObjectPart part)
        {
            // scan thru the dynAttrs map of this part for any textures used as materials
            OSD osdMaterials = null;
            if(part.DynAttrs == null)
                return;

            lock (part.DynAttrs)
            {
                if (part.DynAttrs.ContainsStore("OpenSim", "Materials"))
                {
                    OSDMap materialsStore = part.DynAttrs.GetStore("OpenSim", "Materials");

                    if (materialsStore == null)
                        return;

                    materialsStore.TryGetValue("Materials", out osdMaterials);
                }

                if (osdMaterials != null)
                {
                    //m_log.Info("[UUID Gatherer]: found Materials: " + OSDParser.SerializeJsonString(osd));

                    if (osdMaterials is OSDArray)
                    {
                        OSDArray matsArr = osdMaterials as OSDArray;
                        foreach (OSDMap matMap in matsArr)
                        {
                            try
                            {
                                if (matMap.ContainsKey("Material"))
                                {
                                    OSDMap mat = matMap["Material"] as OSDMap;
                                    if (mat.ContainsKey("NormMap"))
                                    {
                                        UUID normalMapId = mat["NormMap"].AsUUID();
                                        if (normalMapId != UUID.Zero)
                                        {
                                            GatheredUuids[normalMapId] = (sbyte)AssetType.Texture;
                                            //m_log.Info("[UUID Gatherer]: found normal map ID: " + normalMapId.ToString());
                                        }
                                    }
                                    if (mat.ContainsKey("SpecMap"))
                                    {
                                        UUID specularMapId = mat["SpecMap"].AsUUID();
                                        if (specularMapId != UUID.Zero)
                                        {
                                            GatheredUuids[specularMapId] = (sbyte)AssetType.Texture;
                                            //m_log.Info("[UUID Gatherer]: found specular map ID: " + specularMapId.ToString());
                                        }
                                    }
                                }

                            }
                            catch (Exception e)
                            {
                                m_log.Warn("[UUID Gatherer]: exception getting materials: " + e.Message);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get an asset synchronously, potentially using an asynchronous callback.  If the
        /// asynchronous callback is used, we will wait for it to complete.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        protected virtual AssetBase GetAsset(UUID uuid)
        {
            return m_assetService.Get(uuid.ToString());
        }

        /// <summary>
        /// Record the asset uuids embedded within the given text (e.g. a script).
        /// </summary>
        /// <param name="textAsset"></param>
        private void RecordTextEmbeddedAssetUuids(AssetBase textAsset)
        {
            // m_log.DebugFormat("[ASSET GATHERER]: Getting assets for uuid references in asset {0}", embeddingAssetId);

            string text = Utils.BytesToString(textAsset.Data);
            if(text.Length < 36)
                return;

            List<UUID> ids = Util.GetUUIDsOnString(ref text, 0);
            if (ids == null || ids.Count == 0)
                return;

            for (int i = 0; i < ids.Count; ++i)
            {
                if (ids[i] == UUID.Zero)
                    continue;
                if (!UncertainAssetsUUIDs.Contains(ids[i]))
                    UncertainAssetsUUIDs.Add(ids[i]);
                AddForInspection(ids[i]);
            }
        }

        private void RecordNoteCardEmbeddedAssetUuids(AssetBase textAsset)
        {
            List<UUID> ids = SLUtil.GetEmbeddedAssetIDs(textAsset.Data);
            if(ids == null || ids.Count == 0)
                return;

            for(int i = 0; i < ids.Count; ++i)
            {
                if (ids[i] == UUID.Zero)
                    continue;
                if (!UncertainAssetsUUIDs.Contains(ids[i]))
                    UncertainAssetsUUIDs.Add(ids[i]);
                AddForInspection(ids[i]);
            }
        }

        /// <summary>
        /// Record the uuids referenced by the given wearable asset
        /// </summary>
        /// <param name="assetBase"></param>
        private void RecordWearableAssetUuids(AssetBase assetBase)
        {
            //m_log.Debug(new System.Text.ASCIIEncoding().GetString(bodypartAsset.Data));
            AssetWearable wearableAsset = new AssetBodypart(assetBase.FullID, assetBase.Data);
            wearableAsset.Decode();

            //m_log.DebugFormat(
            //    "[ARCHIVER]: Wearable asset {0} references {1} assets", wearableAssetUuid, wearableAsset.Textures.Count);

            foreach (UUID uuid in wearableAsset.Textures.Values)
                GatheredUuids[uuid] = (sbyte)AssetType.Texture;
        }

        /// <summary>
        /// Get all the asset uuids associated with a given object.  This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </summary>
        /// <param name="sceneObjectAsset"></param>
        private void RecordSceneObjectAssetUuids(AssetBase sceneObjectAsset)
        {
            string xml = Utils.BytesToString(sceneObjectAsset.Data);

            CoalescedSceneObjects coa;
            if (CoalescedSceneObjectsSerializer.TryFromXml(xml, out coa))
            {
                foreach (SceneObjectGroup sog in coa.Objects)
                    AddForInspection(sog);
            }
            else
            {
                SceneObjectGroup sog = SceneObjectSerializer.FromOriginalXmlFormat(xml);

                if (null != sog)
                    AddForInspection(sog);
            }
        }

        /// <summary>
        /// Get the asset uuid associated with a gesture
        /// </summary>
        /// <param name="gestureAsset"></param>
        private void RecordGestureAssetUuids(AssetBase gestureAsset)
        {
            using (MemoryStream ms = new MemoryStream(gestureAsset.Data))
                using (StreamReader sr = new StreamReader(ms))
            {
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
                        GatheredUuids[uuid] = (sbyte)AssetType.Animation;    // the asset is either an Animation or a Sound, but this distinction isn't important
                }
            }
        }

        /// <summary>
        /// Get the asset uuid's referenced in a material.
        /// </summary>
        private void RecordMaterialAssetUuids(AssetBase materialAsset)
        {
            OSDMap mat;
            try
            {
                mat = (OSDMap)OSDParser.DeserializeLLSDXml(materialAsset.Data);
            }
            catch (Exception e)
            {
               m_log.WarnFormat("[Materials]: cannot decode material asset {0}: {1}", materialAsset.ID, e.Message);
               return;
            }

            UUID normMap = mat["NormMap"].AsUUID();
            if (normMap != UUID.Zero)
                GatheredUuids[normMap] = (sbyte)AssetType.Texture;

            UUID specMap = mat["SpecMap"].AsUUID();
            if (specMap != UUID.Zero)
                GatheredUuids[specMap] = (sbyte)AssetType.Texture;
        }
    }

    public class HGUuidGatherer : UuidGatherer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_assetServerURL;

        public HGUuidGatherer(IAssetService assetService, string assetServerURL)
            : this(assetService, assetServerURL, new Dictionary<UUID, sbyte>()) {}

        public HGUuidGatherer(IAssetService assetService, string assetServerURL, IDictionary<UUID, sbyte> collector)
            : base(assetService, collector)
        {
            m_assetServerURL = assetServerURL;
            if (!String.IsNullOrWhiteSpace(assetServerURL) && !m_assetServerURL.EndsWith("/") && !m_assetServerURL.EndsWith("="))
                m_assetServerURL = m_assetServerURL + "/";
        }

        protected override AssetBase GetAsset(UUID uuid)
        {
            if (String.IsNullOrWhiteSpace(m_assetServerURL))
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
