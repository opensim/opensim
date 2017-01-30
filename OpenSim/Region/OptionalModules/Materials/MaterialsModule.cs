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
using System.Security.Cryptography; // for computing md5 hash
using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSimAssetType = OpenSim.Framework.SLUtil.OpenSimAssetType;

using Ionic.Zlib;

// You will need to uncomment these lines if you are adding a region module to some other assembly which does not already
// specify its assembly.  Otherwise, the region modules in the assembly will not be picked up when OpenSimulator scans
// the available DLLs
//[assembly: Addin("MaterialsModule", "1.0")]
//[assembly: AddinDependency("OpenSim", "0.8.1")]

namespace OpenSim.Region.OptionalModules.Materials
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MaterialsModule")]
    public class MaterialsModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "MaterialsModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        IAssetCache m_cache;
        private Scene m_scene = null;
        private bool m_enabled = false;
        private int m_maxMaterialsPerTransaction = 50;

        public Dictionary<UUID, OSDMap> m_Materials = new Dictionary<UUID, OSDMap>();
        public Dictionary<UUID, int> m_MaterialsRefCount = new Dictionary<UUID, int>();

        private Dictionary<ulong, AssetBase> m_changes = new Dictionary<ulong, AssetBase>();
        private Dictionary<ulong, double> m_changesTime = new Dictionary<ulong, double>();

        public void Initialise(IConfigSource source)
        {
            m_enabled = true; // default is enabled

            IConfig config = source.Configs["Materials"];
            if (config != null)
            {
                m_enabled = config.GetBoolean("enable_materials", m_enabled);
                m_maxMaterialsPerTransaction = config.GetInt("MaxMaterialsPerTransaction", m_maxMaterialsPerTransaction);
            }

            if (m_enabled)
                m_log.DebugFormat("[Materials]: Initialized");
        }

        public void Close()
        {
            if (!m_enabled)
                return;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnObjectAddedToScene += EventManager_OnObjectAddedToScene;
            m_scene.EventManager.OnBackup += EventManager_OnBackup;
        }

        private void EventManager_OnBackup(ISimulationDataService datastore, bool forcedBackup)
        {
            List<AssetBase> toStore;
            List<ulong> hashlist;


            lock (m_Materials)
            {
                if(m_changes.Count == 0)
                    return;

                if(forcedBackup)
                {
                    toStore = new List<AssetBase>(m_changes.Values);
                    m_changes.Clear();
                    m_changesTime.Clear();
                }
                else
                {
                    toStore = new List<AssetBase>();
                    hashlist = new List<ulong>();
                    double storetime = Util.GetTimeStampMS() - 60000;
                    foreach(KeyValuePair<ulong,double> kvp in m_changesTime)
                    {
                        if(kvp.Value < storetime)
                        {
                            toStore.Add(m_changes[kvp.Key]);
                            hashlist.Add(kvp.Key);
                        }
                    }
                    foreach(ulong u  in hashlist)
                    {
                        m_changesTime.Remove(u);
                        m_changes.Remove(u);
                    }
                }

            if(toStore.Count > 0)
                Util.FireAndForget(delegate
                {
                    foreach(AssetBase a in toStore)
                    {
                        a.Local = false;
                        m_scene.AssetService.Store(a);
                    }
                });
            }
        }

        private void EventManager_OnObjectAddedToScene(SceneObjectGroup obj)
        {
            foreach (var part in obj.Parts)
                if (part != null)
                    GetStoredMaterialsInPart(part);
        }

        private void OnRegisterCaps(OpenMetaverse.UUID agentID, OpenSim.Framework.Capabilities.Caps caps)
        {
            string capsBase = "/CAPS/" + caps.CapsObjectPath;

            IRequestHandler renderMaterialsPostHandler
                = new RestStreamHandler("POST", capsBase + "/",
                    (request, path, param, httpRequest, httpResponse)
                        => RenderMaterialsPostCap(request, agentID),
                    "RenderMaterials", null);
            caps.RegisterHandler("RenderMaterials", renderMaterialsPostHandler);

            // OpenSimulator CAPs infrastructure seems to be somewhat hostile towards any CAP that requires both GET
            // and POST handlers, (at least at the time this was originally written), so we first set up a POST
            // handler normally and then add a GET handler via MainServer

            IRequestHandler renderMaterialsGetHandler
                = new RestStreamHandler("GET", capsBase + "/",
                    (request, path, param, httpRequest, httpResponse)
                        => RenderMaterialsGetCap(request),
                    "RenderMaterials", null);
            MainServer.Instance.AddStreamHandler(renderMaterialsGetHandler);

            // materials viewer seems to use either POST or PUT, so assign POST handler for PUT as well
            IRequestHandler renderMaterialsPutHandler
                = new RestStreamHandler("PUT", capsBase + "/",
                    (request, path, param, httpRequest, httpResponse)
                        => RenderMaterialsPutCap(request, agentID),
                    "RenderMaterials", null);
            MainServer.Instance.AddStreamHandler(renderMaterialsPutHandler);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene.EventManager.OnObjectAddedToScene -= EventManager_OnObjectAddedToScene;
            m_scene.EventManager.OnBackup -= EventManager_OnBackup;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled) return;

            m_cache = scene.RequestModuleInterface<IAssetCache>();
            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if (featuresModule != null)
                featuresModule.OnSimulatorFeaturesRequest += OnSimulatorFeaturesRequest;
        }

        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features)
        {
            features["MaxMaterialsPerTransaction"] = m_maxMaterialsPerTransaction;
        }

        /// <summary>
        /// Finds any legacy materials stored in DynAttrs that may exist for this part and add them to 'm_regionMaterials'.
        /// </summary>
        /// <param name="part"></param>
        private void GetLegacyStoredMaterialsInPart(SceneObjectPart part)
        {
            if (part.DynAttrs == null)
                return;

            OSD OSMaterials = null;
            OSDArray matsArr = null;

            lock (part.DynAttrs)
            {
                if (part.DynAttrs.ContainsStore("OpenSim", "Materials"))
                {
                    OSDMap materialsStore = part.DynAttrs.GetStore("OpenSim", "Materials");

                    if (materialsStore == null)
                        return;

                    materialsStore.TryGetValue("Materials", out OSMaterials);
                }

                if (OSMaterials != null && OSMaterials is OSDArray)
                    matsArr = OSMaterials as OSDArray;
                else
                    return;
            }

            if (matsArr == null)
                return;

            foreach (OSD elemOsd in matsArr)
            {
                if (elemOsd != null && elemOsd is OSDMap)
                {
                    OSDMap matMap = elemOsd as OSDMap;
                    if (matMap.ContainsKey("ID") && matMap.ContainsKey("Material"))
                    {
                        try
                        {
                            lock (m_Materials)
                            {
                                UUID id = matMap["ID"].AsUUID();
                                if(m_Materials.ContainsKey(id))
                                    m_MaterialsRefCount[id]++;
                                else
                                {
                                    m_Materials[id] = (OSDMap)matMap["Material"];
                                    m_MaterialsRefCount[id] = 1;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Warn("[Materials]: exception decoding persisted legacy material: " + e.ToString());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find the materials used in the SOP, and add them to 'm_regionMaterials'.
        /// </summary>
        private void GetStoredMaterialsInPart(SceneObjectPart part)
        {
            if (part.Shape == null)
                return;

            var te = new Primitive.TextureEntry(part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
            if (te == null)
                return;

            GetLegacyStoredMaterialsInPart(part);

            if (te.DefaultTexture != null)
                GetStoredMaterialInFace(part, te.DefaultTexture);
            else
                m_log.WarnFormat(
                    "[Materials]: Default texture for part {0} (part of object {1}) in {2} unexpectedly null.  Ignoring.",
                    part.Name, part.ParentGroup.Name, m_scene.Name);

            foreach (Primitive.TextureEntryFace face in te.FaceTextures)
            {
                if (face != null)
                    GetStoredMaterialInFace(part, face);
            }
        }

        /// <summary>
        /// Find the materials used in one Face, and add them to 'm_regionMaterials'.
        /// </summary>
        private void GetStoredMaterialInFace(SceneObjectPart part, Primitive.TextureEntryFace face)
        {
            UUID id = face.MaterialID;
            if (id == UUID.Zero)
                return;

            lock (m_Materials)
            {
                if (m_Materials.ContainsKey(id))
                {
                    m_MaterialsRefCount[id]++;
                    return;
                }

                AssetBase matAsset = m_scene.AssetService.Get(id.ToString());
                if (matAsset == null || matAsset.Data == null || matAsset.Data.Length == 0 )
                {
                    //m_log.WarnFormat("[Materials]: Prim \"{0}\" ({1}) contains unknown material ID {2}", part.Name, part.UUID, id);
                    return;
                }

                byte[] data = matAsset.Data;
                OSDMap mat;
                try
                {
                    mat = (OSDMap)OSDParser.DeserializeLLSDXml(data);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[Materials]: cannot decode material asset {0}: {1}", id, e.Message);
                    return;
                }

                m_Materials[id] = mat;
                m_MaterialsRefCount[id] = 1;
            }
        }

        public string RenderMaterialsPostCap(string request, UUID agentID)
        {
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();

            OSDArray respArr = new OSDArray();

            if (req.ContainsKey("Zipped"))
            {
                OSD osd = null;

                byte[] inBytes = req["Zipped"].AsBinary();

                try
                {
                    osd = ZDecompressBytesToOsd(inBytes);

                    if (osd != null && osd is OSDArray)
                    {
                        foreach (OSD elem in (OSDArray)osd)
                        {
                            try
                            {
                                UUID id = new UUID(elem.AsBinary(), 0);

                                lock (m_Materials)
                                {
                                    if (m_Materials.ContainsKey(id))
                                    {
                                        OSDMap matMap = new OSDMap();
                                        matMap["ID"] = OSD.FromBinary(id.GetBytes());
                                        matMap["Material"] = m_Materials[id];
                                        respArr.Add(matMap);
                                    }
                                    else
                                    {
                                        m_log.Warn("[Materials]: request for unknown material ID: " + id.ToString());

                                        // Theoretically we could try to load the material from the assets service,
                                        // but that shouldn't be necessary because the viewer should only request
                                        // materials that exist in a prim on the region, and all of these materials
                                        // are already stored in m_regionMaterials.
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                m_log.Error("Error getting materials in response to viewer request", e);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[Materials]: exception decoding zipped CAP payload ", e);
                    //return "";
                }
            }

            resp["Zipped"] = ZCompressOSD(respArr, false);
            string response = OSDParser.SerializeLLSDXmlString(resp);

            //m_log.Debug("[Materials]: cap request: " + request);
            //m_log.Debug("[Materials]: cap request (zipped portion): " + ZippedOsdBytesToString(req["Zipped"].AsBinary()));
            //m_log.Debug("[Materials]: cap response: " + response);
            return response;
        }

        public string RenderMaterialsPutCap(string request, UUID agentID)
        {
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();

            OSDMap materialsFromViewer = null;

            OSDArray respArr = new OSDArray();

            HashSet<SceneObjectPart> parts = new HashSet<SceneObjectPart>();
            if (req.ContainsKey("Zipped"))
            {
                OSD osd = null;

                byte[] inBytes = req["Zipped"].AsBinary();

                try
                {
                    osd = ZDecompressBytesToOsd(inBytes);

                    if (osd != null && osd is OSDMap)
                    {
                        materialsFromViewer = osd as OSDMap;

                        if (materialsFromViewer.ContainsKey("FullMaterialsPerFace"))
                        {
                            OSD matsOsd = materialsFromViewer["FullMaterialsPerFace"];
                            if (matsOsd is OSDArray)
                            {
                                OSDArray matsArr = matsOsd as OSDArray;

                                try
                                {
                                    foreach (OSDMap matsMap in matsArr)
                                    {
                                        uint primLocalID = 0;
                                        try {
                                            primLocalID = matsMap["ID"].AsUInteger();
                                        }
                                        catch (Exception e) {
                                            m_log.Warn("[Materials]: cannot decode \"ID\" from matsMap: " + e.Message);
                                            continue;
                                        }

                                        SceneObjectPart sop = m_scene.GetSceneObjectPart(primLocalID);
                                        if (sop == null)
                                        {
                                            m_log.WarnFormat("[Materials]: SOP not found for localId: {0}", primLocalID.ToString());
                                            continue;
                                        }

                                        if (!m_scene.Permissions.CanEditObject(sop.UUID, agentID))
                                        {
                                            m_log.WarnFormat("User {0} can't edit object {1} {2}", agentID, sop.Name, sop.UUID);
                                            continue;
                                        }

                                        OSDMap mat = null;
                                        try
                                        {
                                            mat = matsMap["Material"] as OSDMap;
                                        }
                                        catch (Exception e)
                                        {
                                            m_log.Warn("[Materials]: cannot decode \"Material\" from matsMap: " + e.Message);
                                            continue;
                                        }

                                        Primitive.TextureEntry te = new Primitive.TextureEntry(sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);
                                        if (te == null)
                                        {
                                            m_log.WarnFormat("[Materials]: Error in TextureEntry for SOP {0} {1}", sop.Name, sop.UUID);
                                            continue;
                                        }

                                        UUID id;
                                        if (mat == null)
                                        {
                                            // This happens then the user removes a material from a prim
                                            id = UUID.Zero;
                                        }
                                        else
                                        {
                                            id = getNewID(mat);
                                        }

                                        int face = -1;
                                        UUID oldid = UUID.Zero;
                                        if (matsMap.ContainsKey("Face"))
                                        {
                                            face = matsMap["Face"].AsInteger();
                                            Primitive.TextureEntryFace faceEntry = te.CreateFace((uint)face);
                                            oldid = faceEntry.MaterialID;
                                            faceEntry.MaterialID = id;
                                        }
                                        else
                                        {
                                            if (te.DefaultTexture == null)
                                                m_log.WarnFormat("[Materials]: TextureEntry.DefaultTexture is null in {0} {1}", sop.Name, sop.UUID);
                                            else
                                            {
                                                oldid = te.DefaultTexture.MaterialID;
                                                te.DefaultTexture.MaterialID = id;
                                            }
                                        }

                                        //m_log.DebugFormat("[Materials]: in \"{0}\" {1}, setting material ID for face {2} to {3}", sop.Name, sop.UUID, face, id);

                                        // We can't use sop.UpdateTextureEntry(te) because it filters, so do it manually
                                        sop.Shape.TextureEntry = te.GetBytes();

                                        lock(m_Materials)
                                        {
                                            if(oldid != UUID.Zero && m_MaterialsRefCount.ContainsKey(oldid))
                                            {
                                                m_MaterialsRefCount[oldid]--;
                                                if(m_MaterialsRefCount[oldid] <= 0)
                                                {
                                                    m_Materials.Remove(oldid);
                                                    m_MaterialsRefCount.Remove(oldid);
                                                    m_cache.Expire(oldid.ToString());
                                                }
                                            }

                                            if(id != UUID.Zero)
                                            {
                                                AssetBase asset = CacheMaterialAsAsset(id, agentID, mat, sop);
                                                if(asset != null)
                                                {
                                                    ulong materialHash = (ulong)primLocalID << 32;
                                                    if(face < 0)
                                                        materialHash += 0xffffffff;
                                                    else
                                                        materialHash +=(ulong)face;

                                                    m_changes[materialHash] = asset;
                                                    m_changesTime[materialHash] = Util.GetTimeStampMS();
                                                }
                                            }
                                        }

                                        if(!parts.Contains(sop))
                                            parts.Add(sop);
                                    }

                                    foreach(SceneObjectPart sop in parts)
                                    {
                                        if (sop.ParentGroup != null && !sop.ParentGroup.IsDeleted)
                                        {
                                            sop.TriggerScriptChangedEvent(Changed.TEXTURE);
                                            sop.ScheduleFullUpdate();
                                            sop.ParentGroup.HasGroupChanged = true;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    m_log.Warn("[Materials]: exception processing received material ", e);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[Materials]: exception decoding zipped CAP payload ", e);
                    //return "";
                }
            }

            resp["Zipped"] = ZCompressOSD(respArr, false);
            string response = OSDParser.SerializeLLSDXmlString(resp);

            //m_log.Debug("[Materials]: cap request: " + request);
            //m_log.Debug("[Materials]: cap request (zipped portion): " + ZippedOsdBytesToString(req["Zipped"].AsBinary()));
            //m_log.Debug("[Materials]: cap response: " + response);
            return response;
        }

        private UUID getNewID(OSDMap mat)
        {
            // ugly and done twice but keep compatibility for now
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(OSDParser.SerializeLLSDXmlString(mat));
            using (var md5 = MD5.Create())
                return new UUID(md5.ComputeHash(data), 0);
        }

        private AssetBase CacheMaterialAsAsset(UUID id, UUID agentID, OSDMap mat, SceneObjectPart sop)
        {
            AssetBase asset = null;
            lock (m_Materials)
            {
                if (!m_Materials.ContainsKey(id))
                {
                    m_Materials[id] = mat;
                    m_MaterialsRefCount[id] = 1;

                    byte[] data = System.Text.Encoding.ASCII.GetBytes(OSDParser.SerializeLLSDXmlString(mat));

                    // This asset might exist already, but it's ok to try to store it again
                    string name = "Material " + ChooseMaterialName(mat, sop);
                    name = name.Substring(0, Math.Min(64, name.Length)).Trim();
                    asset = new AssetBase(id, name, (sbyte)OpenSimAssetType.Material, agentID.ToString());
                    asset.Data = data;
                    asset.Local = true;
                    m_cache.Cache(asset);
                }
                else
                    m_MaterialsRefCount[id]++;
            }
            return asset;
        }

        private UUID StoreMaterialAsAsset(UUID agentID, OSDMap mat, SceneObjectPart sop)
        {
            UUID id;
            // Material UUID = hash of the material's data.
            // This makes materials deduplicate across the entire grid (but isn't otherwise required).
            byte[] data = System.Text.Encoding.ASCII.GetBytes(OSDParser.SerializeLLSDXmlString(mat));
            using (var md5 = MD5.Create())
                id = new UUID(md5.ComputeHash(data), 0);

            lock (m_Materials)
            {
                if (!m_Materials.ContainsKey(id))
                {
                    m_Materials[id] = mat;
                    m_MaterialsRefCount[id] = 1;

                    // This asset might exist already, but it's ok to try to store it again
                    string name = "Material " + ChooseMaterialName(mat, sop);
                    name = name.Substring(0, Math.Min(64, name.Length)).Trim();
                    AssetBase asset = new AssetBase(id, name, (sbyte)OpenSimAssetType.Material, agentID.ToString());
                    asset.Data = data;
                    m_scene.AssetService.Store(asset);
                }
                else
                    m_MaterialsRefCount[id]++;
            }
            return id;
        }

        /// <summary>
        /// Use heuristics to choose a good name for the material.
        /// </summary>
        private string ChooseMaterialName(OSDMap mat, SceneObjectPart sop)
        {
            UUID normMap = mat["NormMap"].AsUUID();
            if (normMap != UUID.Zero)
            {
                AssetBase asset = m_scene.AssetService.GetCached(normMap.ToString());
                if ((asset != null) && (asset.Name.Length > 0) && !asset.Name.Equals("From IAR"))
                    return asset.Name;
            }

            UUID specMap = mat["SpecMap"].AsUUID();
            if (specMap != UUID.Zero)
            {
                AssetBase asset = m_scene.AssetService.GetCached(specMap.ToString());
                if ((asset != null) && (asset.Name.Length > 0) && !asset.Name.Equals("From IAR"))
                    return asset.Name;
            }

            if (sop.Name != "Primitive")
                return sop.Name;

            if ((sop.ParentGroup != null) && (sop.ParentGroup.Name != "Primitive"))
                return sop.ParentGroup.Name;

            return "";
        }


        public string RenderMaterialsGetCap(string request)
        {
            OSDMap resp = new OSDMap();
            int matsCount = 0;
            OSDArray allOsd = new OSDArray();

            lock (m_Materials)
            {
                foreach (KeyValuePair<UUID, OSDMap> kvp in m_Materials)
                {
                    OSDMap matMap = new OSDMap();

                    matMap["ID"] = OSD.FromBinary(kvp.Key.GetBytes());
                    matMap["Material"] = kvp.Value;
                    allOsd.Add(matMap);
                    matsCount++;
                }
            }

            resp["Zipped"] = ZCompressOSD(allOsd, false);

            return OSDParser.SerializeLLSDXmlString(resp);
        }

        private static string ZippedOsdBytesToString(byte[] bytes)
        {
            try
            {
                return OSDParser.SerializeJsonString(ZDecompressBytesToOsd(bytes));
            }
            catch (Exception e)
            {
                return "ZippedOsdBytesToString caught an exception: " + e.ToString();
            }
        }

        /// <summary>
        /// computes a UUID by hashing a OSD object
        /// </summary>
        /// <param name="osd"></param>
        /// <returns></returns>
        private static UUID HashOsd(OSD osd)
        {
            byte[] data = OSDParser.SerializeLLSDBinary(osd, false);
            using (var md5 = MD5.Create())
                return new UUID(md5.ComputeHash(data), 0);
        }

        public static OSD ZCompressOSD(OSD inOsd, bool useHeader)
        {
            OSD osd = null;

            byte[] data = OSDParser.SerializeLLSDBinary(inOsd, useHeader);

            using (MemoryStream msSinkCompressed = new MemoryStream())
            {
                using (Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(msSinkCompressed,
                    Ionic.Zlib.CompressionMode.Compress, CompressionLevel.BestCompression, true))
                {
                    zOut.Write(data, 0, data.Length);
                }

                msSinkCompressed.Seek(0L, SeekOrigin.Begin);
                osd = OSD.FromBinary(msSinkCompressed.ToArray());
            }

            return osd;
        }


        public static OSD ZDecompressBytesToOsd(byte[] input)
        {
            OSD osd = null;

            using (MemoryStream msSinkUnCompressed = new MemoryStream())
            {
                using (Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(msSinkUnCompressed, CompressionMode.Decompress, true))
                {
                    zOut.Write(input, 0, input.Length);
                }

                msSinkUnCompressed.Seek(0L, SeekOrigin.Begin);
                osd = OSDParser.DeserializeLLSDBinary(msSinkUnCompressed.ToArray());
            }

            return osd;
        }
    }
}
