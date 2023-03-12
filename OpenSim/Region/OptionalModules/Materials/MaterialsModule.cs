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
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using static OpenMetaverse.Primitive;
using static OpenMetaverse.Primitive.RenderMaterials;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSimAssetType = OpenSim.Framework.SLUtil.OpenSimAssetType;

using Ionic.Zlib;

namespace OpenSim.Region.OptionalModules.Materials
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MaterialsModule")]
    public class MaterialsModule : INonSharedRegionModule, IMaterialsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "MaterialsModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        IAssetCache m_cache;
        private Scene m_scene = null;
        private bool m_enabled = false;
        private int m_maxMaterialsPerTransaction = 50;
        private readonly object  materialslock = new();

        public Dictionary<UUID, FaceMaterial> m_Materials = new();
        public Dictionary<UUID, int> m_MaterialsRefCount = new();

        private readonly Dictionary<FaceMaterial, double> m_changed = new();
        private readonly Queue<UUID> delayedDelete = new();
        private bool m_storeBusy;

        private static readonly byte[] GetPutEmptyResponseBytes = osUTF8.GetASCIIBytes("<llsd><map><key>Zipped</key><binary>eNqLZgCCWAAChQC5</binary></map></llsd>");

        public void Initialise(IConfigSource source)
        {
            m_enabled = true; // default is enabled

            IConfig config = source.Configs["Materials"];
            if (config is not null)
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
            m_scene.RegisterModuleInterface<IMaterialsModule>(this);
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnObjectAddedToScene += EventManager_OnObjectAddedToScene;
            m_scene.EventManager.OnObjectBeingRemovedFromScene += EventManager_OnObjectDeleteFromScene;
            m_scene.EventManager.OnBackup += EventManager_OnBackup;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene.EventManager.OnObjectAddedToScene -= EventManager_OnObjectAddedToScene;
            m_scene.EventManager.OnObjectBeingRemovedFromScene -= EventManager_OnObjectDeleteFromScene;
            m_scene.EventManager.OnBackup -= EventManager_OnBackup;
            m_scene.UnregisterModuleInterface<IMaterialsModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled) return;

            m_cache = scene.RequestModuleInterface<IAssetCache>();
            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if (featuresModule is not null)
            {
                featuresModule.AddOpenSimExtraFeature("MaxMaterialsPerTransaction", m_maxMaterialsPerTransaction);
                featuresModule.AddOpenSimExtraFeature("RenderMaterialsCapability", 3.0f);
            }
        }

        private void EventManager_OnBackup(ISimulationDataService datastore, bool forcedBackup)
        {
            List<FaceMaterial> toStore = null;

            lock (materialslock)
            {
                if(m_storeBusy && !forcedBackup)
                    return;

                if(m_changed.Count == 0)
                {
                    if(forcedBackup)
                        return;

                    UUID id;
                    int throttle = 0;
                    while(delayedDelete.Count > 0 && throttle < 5)
                    {
                        id = delayedDelete.Dequeue();
                        if (m_Materials.ContainsKey(id))
                        {
                            if (m_MaterialsRefCount[id] <= 0)
                            {
                                m_Materials.Remove(id);
                                m_MaterialsRefCount.Remove(id);
                                m_cache.Expire(id.ToString());
                                ++throttle;
                            }
                        }
                    }
                    return;
                }

                if (forcedBackup)
                {
                    toStore = new List<FaceMaterial>(m_changed.Keys);
                    m_changed.Clear();
                }
                else
                {
                    toStore = new List<FaceMaterial>();
                    double storetime = Util.GetTimeStamp() - 30.0;
                    foreach(KeyValuePair<FaceMaterial, double> kvp in m_changed)
                    {
                        if(kvp.Value < storetime)
                        {
                            toStore.Add(kvp.Key);
                        }
                    }
                    foreach(FaceMaterial fm  in toStore)
                    {
                        m_changed.Remove(fm);
                    }
                }
            }

            if(toStore.Count > 0)
            {
                m_storeBusy = true;
                if (forcedBackup)
                {
                    foreach (FaceMaterial fm in toStore)
                    {
                        AssetBase a = MakeAsset(fm, false);
                        m_scene.AssetService.Store(a);
                    }
                    m_storeBusy = false;
                }
                else
                {
                    Util.FireAndForget(delegate
                    {
                        foreach (FaceMaterial fm in toStore)
                        {
                            AssetBase a = MakeAsset(fm, false);
                            m_scene.AssetService.Store(a);
                        }
                        m_storeBusy = false;
                    });
                }
            }
        }

        private void EventManager_OnObjectAddedToScene(SceneObjectGroup obj)
        {
            foreach (var part in obj.Parts)
            {
                if (part is not null)
                    GetStoredMaterialsInPart(part);
            }
        }

        private void EventManager_OnObjectDeleteFromScene(SceneObjectGroup obj)
        {
            foreach (var part in obj.Parts)
            {
                if (part is not null)
                    RemoveMaterialsInPart(part);
            }
        }

        private void OnRegisterCaps(UUID agentID, OpenSim.Framework.Capabilities.Caps caps)
        {
            caps.RegisterSimpleHandler("RenderMaterials", 
                new SimpleStreamHandler("/" + UUID.Random(),
                    (httpRequest, httpResponse)
                        => preprocess(httpRequest, httpResponse, agentID)
                ));

            caps.RegisterSimpleHandler("ModifyMaterialParams",
                new SimpleStreamHandler("/" + UUID.Random(),
                    (httpRequest, httpResponse)
                        => ModifyMaterialParams(httpRequest, httpResponse, agentID)
                ));
        }

        private void preprocess(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            switch (request.HttpMethod)
            {
                case "GET":
                    RenderMaterialsGetCap(request, response);
                    break;
                case "PUT":
                    RenderMaterialsPutCap(request, response, agentID);
                    break;
                case "POST":
                    RenderMaterialsPostCap(request, response, agentID);
                    break;
                default:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
            }
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        /// <summary>
        /// Finds any legacy materials stored in DynAttrs that may exist for this part and add them to 'm_regionMaterials'.
        /// </summary>
        /// <param name="part"></param>
        private bool GetLegacyStoredMaterialsInPart(SceneObjectPart part)
        {
            if (part.DynAttrs is null)
                return false;

            OSD OSMaterials = null;
            OSDArray matsArr;
            bool partchanged = false;

            lock (part.DynAttrs)
            {
                if (part.DynAttrs.ContainsStore("OpenSim", "Materials"))
                {
                    OSDMap materialsStore = part.DynAttrs.GetStore("OpenSim", "Materials");
                    if (materialsStore is null)
                        return false;

                    materialsStore.TryGetValue("Materials", out OSMaterials);
                    part.DynAttrs.RemoveStore("OpenSim", "Materials");
                    partchanged = true;
                }

                if (OSMaterials is not OSDArray)
                    return partchanged;
                matsArr = (OSDArray)OSMaterials;
            }

            if (matsArr is null)
                return partchanged;
            
            foreach (OSD elemOsd in matsArr)
            {
                if (elemOsd is OSDMap matMap)
                {
                    if (matMap.TryGetValue("ID", out OSD OSDID) && 
                        matMap.TryGetValue("Material", out OSD OSDMaterial) && OSDMaterial is OSDMap theMatMap)
                    {
                        try
                        {
                            lock (materialslock)
                            {
                                UUID id = OSDID.AsUUID();
                                if (m_Materials.ContainsKey(id))
                                    continue;

                                FaceMaterial fmat = new(theMatMap);
                                if (fmat is null ||
                                        (fmat.DiffuseAlphaMode == 1
                                        && fmat.NormalMapID.IsZero()
                                        && fmat.SpecularMapID.IsZero()))
                                    continue;

                                fmat.ID = id;
                                m_Materials[id] = fmat;
                                m_MaterialsRefCount[id] = 0;
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Warn("[Materials]: exception decoding persisted legacy material: " + e.ToString());
                        }
                    }
                }
            }
            return partchanged;
        }

        /// <summary>
        /// Find the materials used in the SOP, and add them to 'm_regionMaterials'.
        /// </summary>
        private void GetStoredMaterialsInPart(SceneObjectPart part)
        {
            if (part.Shape is null)
                return;

            var te = new Primitive.TextureEntry(part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
            if (te is null)
                return;

            bool partchanged = GetLegacyStoredMaterialsInPart(part);
            bool facechanged = false;

            if (te.DefaultTexture is not null)
                facechanged = GetStoredMaterialInFace(part, te.DefaultTexture);
            else
                m_log.WarnFormat(
                    "[Materials]: Default texture for part {0} (part of object {1}) in {2} unexpectedly null.  Ignoring.",
                    part.Name, part.ParentGroup.Name, m_scene.Name);

            foreach (Primitive.TextureEntryFace face in te.FaceTextures)
            {
                if (face is not null)
                    facechanged |= GetStoredMaterialInFace(part, face);
            }

            if(facechanged)
                part.Shape.TextureEntry = te.GetBytes(9);

            if(facechanged || partchanged)
            {
                if (part.ParentGroup is not null && !part.ParentGroup.IsDeleted)
                    part.ParentGroup.HasGroupChanged = true;
            }
        }

        /// <summary>
        /// Find the materials used in one Face, and add them to 'm_regionMaterials'.
        /// </summary>
        private bool GetStoredMaterialInFace(SceneObjectPart part, Primitive.TextureEntryFace face)
        {
            UUID id = face.MaterialID;
            if (id.IsZero())
                return false;

            OSDMap mat;
            lock (materialslock)
            {
                if(m_Materials.ContainsKey(id))
                {
                    m_MaterialsRefCount[id]++;
                    return false;
                }

                AssetBase matAsset = m_scene.AssetService.Get(id.ToString());
                if (matAsset is null || matAsset.Data is null || matAsset.Data.Length == 0 )
                {
                    // grid may just be down...
                    return false;
                }

                byte[] data = matAsset.Data;

                // string txt = System.Text.Encoding.ASCII.GetString(data);
                try
                {
                    mat = (OSDMap)OSDParser.DeserializeLLSDXml(data);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[Materials]: cannot decode material asset {0}: {1}", id, e.Message);
                    return false;
                }

                FaceMaterial fmat = new(mat);
                if(fmat is null ||
                        (fmat.DiffuseAlphaMode == 1
                        && fmat.NormalMapID.IsZero()
                        && fmat.SpecularMapID.IsZero()))
                {
                        face.MaterialID = UUID.Zero;
                        return true;
                }

                fmat.ID = id;

                if (m_Materials.ContainsKey(id))
                {
                    m_MaterialsRefCount[id]++;
                }
                else
                {
                    m_Materials[id] = fmat;
                    m_MaterialsRefCount[id] = 1;
                }
                return false;
            }
        }

        private void RemoveMaterialsInPart(SceneObjectPart part)
        {
            if (part.Shape is null)
                return;

            var te = new Primitive.TextureEntry(part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
            if (te is null)
                return;

            if (te.DefaultTexture is not null)
                RemoveMaterialInFace(te.DefaultTexture);

            foreach (Primitive.TextureEntryFace face in te.FaceTextures)
            {
                if(face is not null)
                    RemoveMaterialInFace(face);
            }
        }

       private void RemoveMaterialInFace(Primitive.TextureEntryFace face)
        {
            UUID id = face.MaterialID;
            if (id.IsZero())
                return;

            lock (materialslock)
            {
                if(m_Materials.ContainsKey(id))
                {
                    m_MaterialsRefCount[id]--;
                    if (m_MaterialsRefCount[id] == 0)
                        delayedDelete.Enqueue(id);
                }
            }
        }

        public void RenderMaterialsPostCap(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            OSDMap req;
            try
            {
                req = (OSDMap)OSDParser.DeserializeLLSDXml(request.InputStream);
            }
            catch
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            OSDArray respArr = new();

            if (req.TryGetValue("Zipped", out OSD tmpOSD))
            {
                OSD osd;
                byte[] inBytes = tmpOSD.AsBinary();

                try
                {
                    osd = ZDecompressBytesToOsd(inBytes);
                    if (osd is OSDArray OSDArrayosd)
                    {
                        foreach (OSD elem in OSDArrayosd)
                        {
                            try
                            {
                                UUID id = new(elem.AsBinary(), 0);

                                lock (materialslock)
                                {
                                    if (m_Materials.ContainsKey(id))
                                    {
                                        OSDMap matMap = new()
                                        {
                                            ["ID"] = OSD.FromBinary(id.GetBytes()),
                                            ["Material"] = m_Materials[id].toOSD()
                                        };
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
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }

            OSDMap resp = new()
            {
                ["Zipped"] = ZCompressOSD(respArr, false)
            };
            response.RawBuffer = Encoding.UTF8.GetBytes(OSDParser.SerializeLLSDXmlString(resp));

            //m_log.Debug("[Materials]: cap request: " + request);
            //m_log.Debug("[Materials]: cap request (zipped portion): " + ZippedOsdBytesToString(req["Zipped"].AsBinary()));
            //m_log.Debug("[Materials]: cap response: " + response);
        }

        public void RenderMaterialsPutCap(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            OSDMap req;
            try
            {
                 req = (OSDMap)OSDParser.DeserializeLLSDXml(request.InputStream);
            }
            catch
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (req.TryGetValue("Zipped", out OSD tmpOSD))
            {
                try
                {
                    byte[] inBytes = tmpOSD.AsBinary();
                    OSD osd = ZDecompressBytesToOsd(inBytes);

                    if (osd is OSDMap materialsFromViewer)
                    {
                        if (materialsFromViewer.TryGetValue("FullMaterialsPerFace", out tmpOSD) && (tmpOSD is OSDArray))
                        {
                            Dictionary<uint, SceneObjectPart> parts = new();
                            HashSet<uint> errorReported = new();
                            OSDArray matsArr = tmpOSD as OSDArray;
                            try
                            {
                                foreach (OSDMap matsMap in matsArr)
                                {
                                    uint primLocalID = 0;
                                    try
                                    {
                                        primLocalID = matsMap["ID"].AsUInteger();
                                    }
                                    catch (Exception e)
                                    {
                                        m_log.Warn("[Materials]: cannot decode \"ID\" from matsMap: " + e.Message);
                                        continue;
                                    }

                                    SceneObjectPart sop = m_scene.GetSceneObjectPart(primLocalID);
                                    if (sop is null)
                                    {
                                        m_log.WarnFormat("[Materials]: SOP not found for localId: {0}", primLocalID.ToString());
                                        continue;
                                    }

                                    if (!m_scene.Permissions.CanEditObject(sop.UUID, agentID))
                                    {
                                        if(!errorReported.Contains(primLocalID))
                                        {
                                            m_log.WarnFormat("[Materials]: User {0} can't edit object {1} {2}", agentID, sop.Name, sop.UUID);
                                            errorReported.Add(primLocalID);
                                        }
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

                                    Primitive.TextureEntry te = new(sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);
                                    if (te is null)
                                    {
                                        m_log.WarnFormat("[Materials]: Error in TextureEntry for SOP {0} {1}", sop.Name, sop.UUID);
                                        continue;
                                    }

                                    int face = -1;
                                    UUID oldid = UUID.Zero;
                                    Primitive.TextureEntryFace faceEntry = null;
                                    if (matsMap.TryGetValue("Face", out tmpOSD))
                                    {
                                        face = tmpOSD.AsInteger();
                                        faceEntry = te.CreateFace((uint)face);
                                    }
                                    else
                                        faceEntry = te.DefaultTexture;

                                    if (faceEntry is null)
                                        continue;

                                    UUID id;
                                    FaceMaterial newFaceMat = null;
                                    if (mat is null)
                                    {
                                        // This happens then the user removes a material from a prim
                                        id = UUID.Zero;
                                    }
                                    else
                                    {
                                        newFaceMat = new FaceMaterial(mat);
                                        if (newFaceMat.DiffuseAlphaMode == 1
                                                && newFaceMat.NormalMapID.IsZero()
                                                && newFaceMat.SpecularMapID.IsZero())
                                            id = UUID.Zero;
                                        else
                                        {
                                            newFaceMat.genID();
                                            id = newFaceMat.ID;
                                        }
                                    }

                                    oldid = faceEntry.MaterialID;

                                    if (oldid == id)
                                        continue;

                                    if (faceEntry is not null)
                                    {
                                        faceEntry.MaterialID = id;
                                        //m_log.DebugFormat("[Materials]: in \"{0}\" {1}, setting material ID for face {2} to {3}", sop.Name, sop.UUID, face, id);
                                        // We can't use sop.UpdateTextureEntry(te) because it filters, so do it manually
                                        sop.Shape.TextureEntry = te.GetBytes(9);
                                    }

                                    if (!oldid.IsZero())
                                        RemoveMaterial(oldid);

                                    lock (materialslock)
                                    {
                                        if (id.IsNotZero())
                                        {
                                            if (m_Materials.ContainsKey(id))
                                                m_MaterialsRefCount[id]++;
                                            else
                                            {
                                                m_Materials[id] = newFaceMat;
                                                m_MaterialsRefCount[id] = 1;
                                                m_changed[newFaceMat] = Util.GetTimeStamp();
                                            }
                                        }
                                    }

                                    parts[primLocalID] = sop;
                                }

                                foreach (SceneObjectPart sop in parts.Values)
                                {
                                    if (sop.ParentGroup is not null && !sop.ParentGroup.IsDeleted)
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
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                return;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[Materials]: exception decoding zipped CAP payload ", e);
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }

            //OSDMap resp = new OSDMap();
            //OSDArray respArr = new OSDArray();
            //resp["Zipped"] = ZCompressOSD(respArr, false);
            //string tmp = OSDParser.SerializeLLSDXmlString(resp);
            //response.RawBuffer = OSDParser.SerializeLLSDXmlToBytes(resp);

            response.RawBuffer = GetPutEmptyResponseBytes;
        }

        private AssetBase MakeAsset(FaceMaterial fm, bool local)
        {
            byte[] data = fm.toLLSDxml();
            AssetBase asset = new(fm.ID, "llmaterial", (sbyte)OpenSimAssetType.Material, "00000000-0000-0000-0000-000000000000")
            {
                Data = data,
                Local = local
            };
            return asset;
        }

        private byte[] CacheGet = null;
        private readonly object CacheGetLock = new();
        private double CacheGetTime = 0;

        public void RenderMaterialsGetCap(IOSHttpRequest request, IOSHttpResponse response)
        {
            lock(CacheGetLock)
            {
                OSDArray allOsd = new();
                double now = Util.GetTimeStamp();
                if(CacheGet is null || now - CacheGetTime > 30)
                {
                    CacheGetTime = now;

                    lock (m_Materials)
                    {
                        foreach (KeyValuePair<UUID, FaceMaterial> kvp in m_Materials)
                        {
                            OSDMap matMap = new()
                            {
                                ["ID"] = OSD.FromBinary(kvp.Key.GetBytes()),
                                ["Material"] = kvp.Value.toOSD()
                            };
                            allOsd.Add(matMap);
                        }
                    }

                    OSDMap resp = new()
                    {
                        ["Zipped"] = ZCompressOSD(allOsd, false)
                    };

                    CacheGet = OSDParser.SerializeLLSDXmlToBytes(resp);
                }
                response.RawBuffer = CacheGet ?? GetPutEmptyResponseBytes;
            }
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

        public static OSD ZCompressOSD(OSD inOsd, bool useHeader)
        {
            byte[] data = OSDParser.SerializeLLSDBinary(inOsd, useHeader);
            using (MemoryStream msSinkCompressed = new())
            {
                using (Ionic.Zlib.ZlibStream zOut = new Ionic.Zlib.ZlibStream(msSinkCompressed,
                    Ionic.Zlib.CompressionMode.Compress, CompressionLevel.BestCompression, true))
                {
                    zOut.Write(data, 0, data.Length);
                }

                msSinkCompressed.Seek(0L, SeekOrigin.Begin);
                return OSD.FromBinary(msSinkCompressed.ToArray());
            }
        }

        public static OSD ZDecompressBytesToOsd(byte[] input)
        {
            using (MemoryStream msSinkUnCompressed = new())
            {
                using (Ionic.Zlib.ZlibStream zOut = new(msSinkUnCompressed, CompressionMode.Decompress, true))
                {
                    zOut.Write(input, 0, input.Length);
                }

                msSinkUnCompressed.Seek(0L, SeekOrigin.Begin);
                return OSDParser.DeserializeLLSDBinary(msSinkUnCompressed.ToArray());
            }
        }

        public FaceMaterial GetMaterial(UUID ID)
        {
            if(m_Materials.TryGetValue(ID, out FaceMaterial fm))
                return fm;
            return null;
        }

        public FaceMaterial GetMaterialCopy(UUID ID)
        {
            if(m_Materials.TryGetValue(ID, out FaceMaterial fm))
                return new FaceMaterial(fm);
            return null;
        }

        public UUID AddNewMaterial(FaceMaterial fm)
        {
            if(fm.DiffuseAlphaMode == 1 && fm.NormalMapID.IsZero() && fm.SpecularMapID.IsZero())
            {
                fm.ID = UUID.Zero;
                return UUID.Zero;
            }

            fm.genID();
            UUID id = fm.ID;
            lock(materialslock)
            {
                if(m_Materials.ContainsKey(id))
                    m_MaterialsRefCount[id]++;
                else
                {
                    m_Materials[id] = fm;
                    m_MaterialsRefCount[id] = 1;
                    m_changed[fm] = Util.GetTimeStamp();
                }
            }
            return id;
        }

        public void RemoveMaterial(UUID id)
        {
            if(id.IsZero())
                return;

            lock(materialslock)
            {
                if(m_Materials.ContainsKey(id))
                {
                    m_MaterialsRefCount[id]--;
                    if(m_MaterialsRefCount[id] == 0)
                    {
                        FaceMaterial fm = m_Materials[id];
                        m_changed.Remove(fm);
                        delayedDelete.Enqueue(id);
                    }
                }
            }
        }

        public void ModifyMaterialParams(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID)
        {
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            try
            {
                OSDArray req = (OSDArray)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
                httpRequest.InputStream.Dispose();

                OSD tmp;
                HashSet<SceneObjectPart> changedSOPs = new();

                foreach (OSDMap map in req)
                {
                    if (!map.TryGetValue("object_id", out tmp))
                        continue;
                    UUID sopid = tmp.AsUUID();
                    if (sopid.IsZero())
                        continue;

                    SceneObjectPart sop = m_scene.GetSceneObjectPart(sopid);
                    if (sop is null)
                        continue;

                    PrimitiveBaseShape pbs = sop.Shape;
                    if (pbs is null)
                        continue;

                    if (!m_scene.Permissions.CanEditObject(sop.UUID, agentID))
                        continue;

                    if (!map.TryGetValue("side", out tmp))
                        continue;
                    int side = tmp.AsInteger();

                    string overridedata;
                    if (map.TryGetValue("gltf_json", out tmp))
                        overridedata = tmp.AsString().TrimEnd('\n');
                    else
                        overridedata = string.Empty;

                    if (map.TryGetValue("asset_id", out tmp))
                    {
                        UUID assetID = tmp.AsUUID();
                        if (assetID.IsNotZero())
                        {
                            if (pbs.RenderMaterials is null)
                            {
                                var entries = new Primitive.RenderMaterials.RenderMaterialEntry[1];
                                entries[0].te_index = (byte)side;
                                entries[0].id = assetID;
                                pbs.RenderMaterials = new Primitive.RenderMaterials { entries = entries };
                            }
                            else
                            {
                                if (pbs.RenderMaterials.entries is null)
                                {
                                    var entries = new Primitive.RenderMaterials.RenderMaterialEntry[1];
                                    entries[0].te_index = (byte)side;
                                    entries[0].id = assetID;
                                    pbs.RenderMaterials.entries = entries;
                                }
                                else
                                {
                                    int indx = 0;
                                    while (indx < pbs.RenderMaterials.entries.Length)
                                    {
                                        if (pbs.RenderMaterials.entries[indx].te_index == side)
                                        {
                                            pbs.RenderMaterials.entries[indx].id = assetID;
                                            break;
                                        }
                                        indx++;
                                    }
                                    if (indx == pbs.RenderMaterials.entries.Length)
                                    {
                                        Array.Resize(ref pbs.RenderMaterials.entries, indx + 1);
                                        pbs.RenderMaterials.entries[indx].te_index = (byte)side;
                                        pbs.RenderMaterials.entries[indx].id = assetID;
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(overridedata))
                                RemoveMaterialOverride(ref pbs.RenderMaterials.overrides, side);
                            else
                                AddMaterialOverride(ref pbs.RenderMaterials.overrides, overridedata, side);

                            changedSOPs.Add(sop);
                        }
                        else if(pbs.RenderMaterials is not null)
                        {
                            bool changed = RemoveMaterialEntry(ref pbs.RenderMaterials.entries, side);
                            changed |= RemoveMaterialOverride(ref pbs.RenderMaterials.overrides, side);

                            if(pbs.RenderMaterials.entries is null && pbs.RenderMaterials.overrides is null)
                                pbs.RenderMaterials = null;
                            if(changed)
                                changedSOPs.Add(sop);
                        }
                    }
                    else if (pbs.RenderMaterials is not null)
                    {
                        if (string.IsNullOrEmpty(overridedata))
                        {

                        }
                        else
                        {
                            if(AddMaterialOverride(ref pbs.RenderMaterials.overrides, overridedata, side))
                                changedSOPs.Add(sop);
                        }
                    }
                }
                foreach (SceneObjectPart sop in changedSOPs)
                {
                    sop.ParentGroup.HasGroupChanged = true;
                    sop.ScheduleFullUpdate();
                }

                httpResponse.RawBuffer = XMLkeyMaterialSucess;
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.RawBuffer = XMLkeyMaterialFail;
            }
        }

        public static readonly byte[] XMLkeyMaterialSucess = osUTF8.GetASCIIBytes("<llsd><map><key>success</key><integer>1</integer></map></llsd>\r\n");
        public static readonly byte[] XMLkeyMaterialFail = osUTF8.GetASCIIBytes("<llsd><map><key>success</key><integer>0</integer></map></llsd>\r\n");
        private static bool RemoveMaterialEntry(ref RenderMaterialEntry[] entries, int side)
        {
            if (entries is null || entries.Length == 0)
                return false;

            int indx = 0;
            while (entries[indx].te_index != side && indx < entries.Length)
                indx++;

            if (indx >= entries.Length)
                return false;

            if (entries.Length == 1)
                entries = null;
            else
            {
                var newentries = new Primitive.RenderMaterials.RenderMaterialEntry[entries.Length - 1];
                if (indx > 0)
                    Array.Copy(entries, newentries, indx);
                int left = newentries.Length - indx;
                if (left > 0)
                    Array.Copy(entries, indx + 1, newentries, indx, left);
                entries = newentries;
            }
            return true;
        }

        private static bool RemoveMaterialOverride(ref RenderMaterialOverrideEntry[] overrides, int side)
        {
            if (overrides is null || overrides.Length == 0)
                return false;

            int indx = 0;
            while(overrides[indx].te_index != side && indx < overrides.Length)
                indx++;

            if (indx >= overrides.Length)
                return false;

            if (overrides.Length == 1)
                overrides = null;
            else
            {
                var entries = new Primitive.RenderMaterials.RenderMaterialOverrideEntry[overrides.Length - 1];
                if (indx > 0)
                    Array.Copy(overrides, entries, indx);
                int left = entries.Length - indx;
                if (left > 0)
                    Array.Copy(overrides, indx + 1, entries, indx, left);
                overrides = entries;
            }
            return true;
        }
        
        private static bool AddMaterialOverride(ref RenderMaterials.RenderMaterialOverrideEntry[] overrides, string data, int side)
        {
            if (overrides is null)
            {
                var entries = new RenderMaterials.RenderMaterialOverrideEntry[1];
                entries[0].te_index = (byte)side;
                entries[0].data = data;
                overrides = entries;
                return true;
            }

            int indx = 0;
            while (indx < overrides.Length)
            {
                if (overrides[indx].te_index == side)
                {
                    overrides[indx].data = data;
                    return true;
                }
                indx++;
            }
            if (indx == overrides.Length)
            {
                Array.Resize(ref overrides, indx + 1);
                overrides[indx].te_index = (byte)side;
                if(overrides[indx].data != data)
                {
                    overrides[indx].data = data;
                    return true;
                }
            }
            return false;
        }
    }
}
