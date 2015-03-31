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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Client;
using OpenSim.Services.Interfaces;

using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.ClientStack.Linden
{
    public delegate void UpLoadedAsset(
    string assetName, string description, UUID assetID, UUID inventoryItem, UUID parentFolder,
    byte[] data, string inventoryType, string assetType);

    public delegate UUID UpdateItem(UUID itemID, byte[] data);

    public delegate void UpdateTaskScript(UUID itemID, UUID primID, bool isScriptRunning, byte[] data, ref ArrayList errors);

    public delegate void NewInventoryItem(UUID userID, InventoryItemBase item);

    public delegate void NewAsset(AssetBase asset);

    public delegate UUID ItemUpdatedCallback(UUID userID, UUID itemID, byte[] data);

    public delegate ArrayList TaskScriptUpdatedCallback(UUID userID, UUID itemID, UUID primID,
                                                   bool isScriptRunning, byte[] data);

    public delegate InventoryCollection FetchInventoryDescendentsCAPS(UUID agentID, UUID folderID, UUID ownerID,
                                                                          bool fetchFolders, bool fetchItems, int sortOrder, out int version);

    /// <summary>
    /// XXX Probably not a particularly nice way of allow us to get the scene presence from the scene (chiefly so that
    /// we can popup a message on the user's client if the inventory service has permanently failed).  But I didn't want
    /// to just pass the whole Scene into CAPS.
    /// </summary>
    public delegate IClientAPI GetClientDelegate(UUID agentID);

    public class BunchOfCaps
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;
        private Caps m_HostCapsObj;

        private static readonly string m_requestPath = "0000/";
        // private static readonly string m_mapLayerPath = "0001/";
        private static readonly string m_newInventory = "0002/";
        //private static readonly string m_requestTexture = "0003/";
        private static readonly string m_notecardUpdatePath = "0004/";
        private static readonly string m_notecardTaskUpdatePath = "0005/";
        //        private static readonly string m_fetchInventoryPath = "0006/";
        private static readonly string m_copyFromNotecardPath = "0007/";
        // private static readonly string m_remoteParcelRequestPath = "0009/";// This is in the LandManagementModule.
        private static readonly string m_getObjectPhysicsDataPath = "0101/";
        /* 0102 - 0103 RESERVED */
        private static readonly string m_UpdateAgentInformationPath = "0500/";
        
        // These are callbacks which will be setup by the scene so that we can update scene data when we
        // receive capability calls
        public NewInventoryItem AddNewInventoryItem = null;
        public NewAsset AddNewAsset = null;
        public ItemUpdatedCallback ItemUpdatedCall = null;
        public TaskScriptUpdatedCallback TaskScriptUpdatedCall = null;
        public FetchInventoryDescendentsCAPS CAPSFetchInventoryDescendents = null;
        public GetClientDelegate GetClient = null;

        private bool m_persistBakedTextures = false;
        private IAssetService m_assetService;
        private bool m_dumpAssetsToFile = false;
        private string m_regionName;
        private int m_levelUpload = 0;

        public BunchOfCaps(Scene scene, Caps caps)
        {
            m_Scene = scene;
            m_HostCapsObj = caps;
            IConfigSource config = m_Scene.Config;
            if (config != null)
            {
                IConfig sconfig = config.Configs["Startup"];
                if (sconfig != null)
                {
                    m_levelUpload = sconfig.GetInt("LevelUpload", 0);
                }

                IConfig appearanceConfig = config.Configs["Appearance"];
                if (appearanceConfig != null)
                {
                    m_persistBakedTextures = appearanceConfig.GetBoolean("PersistBakedTextures", m_persistBakedTextures);
                }
            }

            m_assetService = m_Scene.AssetService;
            m_regionName = m_Scene.RegionInfo.RegionName;

            RegisterHandlers();

            AddNewInventoryItem = m_Scene.AddUploadedInventoryItem;
            ItemUpdatedCall = m_Scene.CapsUpdateInventoryItemAsset;
            TaskScriptUpdatedCall = m_Scene.CapsUpdateTaskInventoryScriptAsset;
            GetClient = m_Scene.SceneGraph.GetControllingClient;
        }

        /// <summary>
        /// Register a bunch of CAPS http service handlers
        /// </summary>
        public void RegisterHandlers()
        {
            string capsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath;

            RegisterRegionServiceHandlers(capsBase);
            RegisterInventoryServiceHandlers(capsBase);
        }

        public void RegisterRegionServiceHandlers(string capsBase)
        {
            try
            {
                // the root of all evil
                m_HostCapsObj.RegisterHandler(
                    "SEED", new RestStreamHandler("POST", capsBase + m_requestPath, SeedCapRequest, "SEED", null));

//                m_log.DebugFormat(
//                    "[CAPS]: Registered seed capability {0} for {1}", capsBase + m_requestPath, m_HostCapsObj.AgentID);

                //m_capsHandlers["MapLayer"] =
                //    new LLSDStreamhandler<OSDMapRequest, OSDMapLayerResponse>("POST",
                //                                                                capsBase + m_mapLayerPath,
                //                                                                GetMapLayer);
                IRequestHandler req
                    = new RestStreamHandler(
                        "POST", capsBase + m_notecardTaskUpdatePath, ScriptTaskInventory, "UpdateScript", null);

                m_HostCapsObj.RegisterHandler("UpdateScriptTaskInventory", req);
                m_HostCapsObj.RegisterHandler("UpdateScriptTask", req);
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }
        }

        public void RegisterInventoryServiceHandlers(string capsBase)
        {
            try
            {
                // I don't think this one works...
                m_HostCapsObj.RegisterHandler(
                    "NewFileAgentInventory",
                    new LLSDStreamhandler<LLSDAssetUploadRequest, LLSDAssetUploadResponse>(
                        "POST",
                        capsBase + m_newInventory,
                        NewAgentInventoryRequest,
                        "NewFileAgentInventory",
                        null));

                IRequestHandler req
                    = new RestStreamHandler(
                        "POST", capsBase + m_notecardUpdatePath, NoteCardAgentInventory, "Update*", null);

                m_HostCapsObj.RegisterHandler("UpdateNotecardAgentInventory", req);
                m_HostCapsObj.RegisterHandler("UpdateScriptAgentInventory", req);
                m_HostCapsObj.RegisterHandler("UpdateScriptAgent", req);

                IRequestHandler getObjectPhysicsDataHandler 
                    = new RestStreamHandler(
                        "POST", capsBase + m_getObjectPhysicsDataPath, GetObjectPhysicsData, "GetObjectPhysicsData", null);
                m_HostCapsObj.RegisterHandler("GetObjectPhysicsData", getObjectPhysicsDataHandler);

                IRequestHandler UpdateAgentInformationHandler
                    = new RestStreamHandler(
                        "POST", capsBase + m_UpdateAgentInformationPath, UpdateAgentInformation, "UpdateAgentInformation", null);
                m_HostCapsObj.RegisterHandler("UpdateAgentInformation", UpdateAgentInformationHandler);

                m_HostCapsObj.RegisterHandler(
                    "CopyInventoryFromNotecard",
                    new RestStreamHandler(
                        "POST", capsBase + m_copyFromNotecardPath, CopyInventoryFromNotecard, "CopyInventoryFromNotecard", null));
             
                // As of RC 1.22.9 of the Linden client this is
                // supported

                //m_capsHandlers["WebFetchInventoryDescendents"] =new RestStreamHandler("POST", capsBase + m_fetchInventoryPath, FetchInventoryDescendentsRequest);

                // justincc: I've disabled the CAPS service for now to fix problems with selecting textures, and
                // subsequent inventory breakage, in the edit object pane (such as mantis 1085).  This requires
                // enhancements (probably filling out the folder part of the LLSD reply) to our CAPS service,
                // but when I went on the Linden grid, the
                // simulators I visited (version 1.21) were, surprisingly, no longer supplying this capability.  Instead,
                // the 1.19.1.4 client appeared to be happily flowing inventory data over UDP
                //
                // This is very probably just a temporary measure - once the CAPS service appears again on the Linden grid
                // we will be
                // able to get the data we need to implement the necessary part of the protocol to fix the issue above.
                //                m_capsHandlers["FetchInventoryDescendents"] =
                //                    new RestStreamHandler("POST", capsBase + m_fetchInventoryPath, FetchInventoryRequest);

                // m_capsHandlers["FetchInventoryDescendents"] =
                //     new LLSDStreamhandler<LLSDFetchInventoryDescendents, LLSDInventoryDescendents>("POST",
                //                                                                                    capsBase + m_fetchInventory,
                //                                                                                    FetchInventory));
                // m_capsHandlers["RequestTextureDownload"] = new RestStreamHandler("POST",
                //                                                                  capsBase + m_requestTexture,
                //                                                                  RequestTexture);
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }
        }

        /// <summary>
        /// Construct a client response detailing all the capabilities this server can provide.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns></returns>
        public string SeedCapRequest(string request, string path, string param,
                                  IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat(
//                "[CAPS]: Received SEED caps request in {0} for agent {1}", m_regionName, m_HostCapsObj.AgentID);

            if (!m_Scene.CheckClient(m_HostCapsObj.AgentID, httpRequest.RemoteIPEndPoint))
            {
                m_log.WarnFormat(
                    "[CAPS]: Unauthorized CAPS client {0} from {1}",
                    m_HostCapsObj.AgentID, httpRequest.RemoteIPEndPoint);

                return string.Empty;
            }

            OSDArray capsRequested = (OSDArray)OSDParser.DeserializeLLSDXml(request);
            List<string> validCaps = new List<string>();

            foreach (OSD c in capsRequested)
                validCaps.Add(c.AsString());

            string result = LLSDHelpers.SerialiseLLSDReply(m_HostCapsObj.GetCapsDetails(true, validCaps));

            //m_log.DebugFormat("[CAPS] CapsRequest {0}", result);

            return result;
        }

        /// <summary>
        /// Called by the script task update handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns></returns>
        public string ScriptTaskInventory(string request, string path, string param,
                                          IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            try
            {
//                m_log.Debug("[CAPS]: ScriptTaskInventory Request in region: " + m_regionName);
                //m_log.DebugFormat("[CAPS]: request: {0}, path: {1}, param: {2}", request, path, param);

                Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
                LLSDTaskScriptUpdate llsdUpdateRequest = new LLSDTaskScriptUpdate();
                LLSDHelpers.DeserialiseOSDMap(hash, llsdUpdateRequest);

                string capsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath;
                string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

                TaskInventoryScriptUpdater uploader =
                    new TaskInventoryScriptUpdater(
                        llsdUpdateRequest.item_id,
                        llsdUpdateRequest.task_id,
                        llsdUpdateRequest.is_script_running,
                        capsBase + uploaderPath,
                        m_HostCapsObj.HttpListener,
                        m_dumpAssetsToFile);
                uploader.OnUpLoad += TaskScriptUpdated;

                m_HostCapsObj.HttpListener.AddStreamHandler(
                    new BinaryStreamHandler(
                        "POST", capsBase + uploaderPath, uploader.uploaderCaps, "TaskInventoryScriptUpdater", null));

                string protocol = "http://";

                if (m_HostCapsObj.SSLCaps)
                    protocol = "https://";

                string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + capsBase +
                                     uploaderPath;

                LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                uploadResponse.uploader = uploaderURL;
                uploadResponse.state = "upload";

                //                m_log.InfoFormat("[CAPS]: " +
                //                                 "ScriptTaskInventory response: {0}",
                //                                 LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

                return LLSDHelpers.SerialiseLLSDReply(uploadResponse);
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Called when new asset data for an agent inventory item update has been uploaded.
        /// </summary>
        /// <param name="itemID">Item to update</param>
        /// <param name="primID">Prim containing item to update</param>
        /// <param name="isScriptRunning">Signals whether the script to update is currently running</param>
        /// <param name="data">New asset data</param>
        public void TaskScriptUpdated(UUID itemID, UUID primID, bool isScriptRunning, byte[] data, ref ArrayList errors)
        {
            if (TaskScriptUpdatedCall != null)
            {
                ArrayList e = TaskScriptUpdatedCall(m_HostCapsObj.AgentID, itemID, primID, isScriptRunning, data);
                foreach (Object item in e)
                    errors.Add(item);
            }
        }

        /// <summary>
        /// Called when new asset data for an agent inventory item update has been uploaded.
        /// </summary>
        /// <param name="itemID">Item to update</param>
        /// <param name="data">New asset data</param>
        /// <returns></returns>
        public UUID ItemUpdated(UUID itemID, byte[] data)
        {
            if (ItemUpdatedCall != null)
            {
                return ItemUpdatedCall(m_HostCapsObj.AgentID, itemID, data);
            }

            return UUID.Zero;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="llsdRequest"></param>
        /// <returns></returns>
        public LLSDAssetUploadResponse NewAgentInventoryRequest(LLSDAssetUploadRequest llsdRequest)
        {
            //m_log.Debug("[CAPS]: NewAgentInventoryRequest Request is: " + llsdRequest.ToString());
            //m_log.Debug("asset upload request via CAPS" + llsdRequest.inventory_type + " , " + llsdRequest.asset_type);

            if (llsdRequest.asset_type == "texture" ||
                llsdRequest.asset_type == "animation" ||
                llsdRequest.asset_type == "sound")
            {
                ScenePresence avatar = null;
                IClientAPI client = null;
                m_Scene.TryGetScenePresence(m_HostCapsObj.AgentID, out avatar);

                // check user level
                if (avatar != null)
                {
                    client = avatar.ControllingClient;

                    if (avatar.UserLevel < m_levelUpload)
                    {
                        if (client != null)
                            client.SendAgentAlertMessage("Unable to upload asset. Insufficient permissions.", false);

                        LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                        errorResponse.uploader = "";
                        errorResponse.state = "error";
                        return errorResponse;
                    }
                }

                // check funds
                if (client != null)
                {
                    IMoneyModule mm = m_Scene.RequestModuleInterface<IMoneyModule>();

                    if (mm != null)
                    {
                        if (!mm.UploadCovered(client.AgentId, mm.UploadCharge))
                        {
                            client.SendAgentAlertMessage("Unable to upload asset. Insufficient funds.", false);

                            LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                            errorResponse.uploader = "";
                            errorResponse.state = "error";
                            return errorResponse;
                        }
                    }
                }
            }

            string assetName = llsdRequest.name;
            string assetDes = llsdRequest.description;
            string capsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath;
            UUID newAsset = UUID.Random();
            UUID newInvItem = UUID.Random();
            UUID parentFolder = llsdRequest.folder_id;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            AssetUploader uploader =
                new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, llsdRequest.inventory_type,
                                  llsdRequest.asset_type, capsBase + uploaderPath, m_HostCapsObj.HttpListener, m_dumpAssetsToFile);

            m_HostCapsObj.HttpListener.AddStreamHandler(
                new BinaryStreamHandler(
                    "POST",
                    capsBase + uploaderPath,
                    uploader.uploaderCaps,
                    "NewAgentInventoryRequest",
                    m_HostCapsObj.AgentID.ToString()));

            string protocol = "http://";

            if (m_HostCapsObj.SSLCaps)
                protocol = "https://";

            string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + capsBase +
                                 uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";
            uploader.OnUpLoad += UploadCompleteHandler;
            return uploadResponse;
        }

        /// <summary>
        /// Convert raw uploaded data into the appropriate asset and item.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="inventoryItem"></param>
        /// <param name="data"></param>
        public void UploadCompleteHandler(string assetName, string assetDescription, UUID assetID,
                                          UUID inventoryItem, UUID parentFolder, byte[] data, string inventoryType,
                                          string assetType)
        {
            m_log.DebugFormat(
                "[BUNCH OF CAPS]: Uploaded asset {0} for inventory item {1}, inv type {2}, asset type {3}",
                assetID, inventoryItem, inventoryType, assetType);

            sbyte assType = 0;
            sbyte inType = 0;

            if (inventoryType == "sound")
            {
                inType = (sbyte)InventoryType.Sound;
                assType = (sbyte)AssetType.Sound;
            }
            else if (inventoryType == "snapshot")
            {
                inType = (sbyte)InventoryType.Snapshot;
            }
            else if (inventoryType == "animation")
            {
                inType = (sbyte)InventoryType.Animation;
                assType = (sbyte)AssetType.Animation;
            }
            else if (inventoryType == "wearable")
            {
                inType = (sbyte)InventoryType.Wearable;
                switch (assetType)
                {
                    case "bodypart":
                        assType = (sbyte)AssetType.Bodypart;
                        break;
                    case "clothing":
                        assType = (sbyte)AssetType.Clothing;
                        break;
                }
            }
            else if (inventoryType == "object")
            {
                inType = (sbyte)InventoryType.Object;
                assType = (sbyte)AssetType.Object;

                List<Vector3> positions = new List<Vector3>();
                List<Quaternion> rotations = new List<Quaternion>();
                OSDMap request = (OSDMap)OSDParser.DeserializeLLSDXml(data);
                OSDArray instance_list = (OSDArray)request["instance_list"];
                OSDArray mesh_list = (OSDArray)request["mesh_list"];
                OSDArray texture_list = (OSDArray)request["texture_list"];
                SceneObjectGroup grp = null;

                InventoryFolderBase textureUploadFolder = null;

                List<InventoryFolderBase> foldersToUpdate = new List<InventoryFolderBase>();
                List<InventoryItemBase> itemsToUpdate = new List<InventoryItemBase>();
                IClientInventory clientInv = null;
                
                if (texture_list.Count > 0)
                {
                    ScenePresence avatar = null;
                    m_Scene.TryGetScenePresence(m_HostCapsObj.AgentID, out avatar);

                    if (avatar != null)
                    {
                        IClientCore core = (IClientCore)avatar.ControllingClient;

                        if (core.TryGet<IClientInventory>(out clientInv))
                        {
                            var systemTextureFolder = m_Scene.InventoryService.GetFolderForType(m_HostCapsObj.AgentID, AssetType.Texture);
                            textureUploadFolder = new InventoryFolderBase(UUID.Random(), assetName, m_HostCapsObj.AgentID, (short)AssetType.Unknown, systemTextureFolder.ID, 1);
                            if (m_Scene.InventoryService.AddFolder(textureUploadFolder))
                            {
                                foldersToUpdate.Add(textureUploadFolder);

                                m_log.DebugFormat(
                                    "[BUNCH OF CAPS]: Created new folder '{0}' ({1}) for textures uploaded with mesh object {2}", 
                                    textureUploadFolder.Name, textureUploadFolder.ID, assetName);
                            }
                            else
                            {
                                textureUploadFolder = null;
                            }
                        }
                    }
                }

                List<UUID> textures = new List<UUID>();
                for (int i = 0; i < texture_list.Count; i++)
                {
                    AssetBase textureAsset = new AssetBase(UUID.Random(), assetName, (sbyte)AssetType.Texture, "");
                    textureAsset.Data = texture_list[i].AsBinary();
                    m_assetService.Store(textureAsset);
                    textures.Add(textureAsset.FullID);

                    if (textureUploadFolder != null)
                    {
                        InventoryItemBase textureItem = new InventoryItemBase();
                        textureItem.Owner = m_HostCapsObj.AgentID;
                        textureItem.CreatorId = m_HostCapsObj.AgentID.ToString();
                        textureItem.CreatorData = String.Empty;
                        textureItem.ID = UUID.Random();
                        textureItem.AssetID = textureAsset.FullID;
                        textureItem.Description = assetDescription;
                        textureItem.Name = assetName + " - Texture " + (i + 1).ToString();
                        textureItem.AssetType = (int)AssetType.Texture;
                        textureItem.InvType = (int)InventoryType.Texture;
                        textureItem.Folder = textureUploadFolder.ID;
                        textureItem.CurrentPermissions
                            = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer | PermissionMask.Export);
                        textureItem.BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                        textureItem.EveryOnePermissions = 0;
                        textureItem.NextPermissions = (uint)PermissionMask.All;
                        textureItem.CreationDate = Util.UnixTimeSinceEpoch();
                        m_Scene.InventoryService.AddItem(textureItem);
                        itemsToUpdate.Add(textureItem);

                        m_log.DebugFormat(
                            "[BUNCH OF CAPS]: Created new inventory item '{0}' ({1}) for texture uploaded with mesh object {2}", 
                            textureItem.Name, textureItem.ID, assetName);
                    }
                }

                if (clientInv != null && (foldersToUpdate.Count > 0 || itemsToUpdate.Count > 0))
                {
                    clientInv.SendBulkUpdateInventory(foldersToUpdate.ToArray(), itemsToUpdate.ToArray());
                }

                for (int i = 0; i < mesh_list.Count; i++)
                {
                    PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateBox();

                    Primitive.TextureEntry textureEntry
                        = new Primitive.TextureEntry(Primitive.TextureEntry.WHITE_TEXTURE);
                    OSDMap inner_instance_list = (OSDMap)instance_list[i];

                    OSDArray face_list = (OSDArray)inner_instance_list["face_list"];
                    for (uint face = 0; face < face_list.Count; face++)
                    {
                        OSDMap faceMap = (OSDMap)face_list[(int)face];
                        Primitive.TextureEntryFace f = pbs.Textures.CreateFace(face);
                        if(faceMap.ContainsKey("fullbright"))
                            f.Fullbright = faceMap["fullbright"].AsBoolean();
                        if (faceMap.ContainsKey ("diffuse_color"))
                            f.RGBA = faceMap["diffuse_color"].AsColor4();

                        int textureNum = faceMap["image"].AsInteger();
                        float imagerot = faceMap["imagerot"].AsInteger();
                        float offsets = (float)faceMap["offsets"].AsReal();
                        float offsett = (float)faceMap["offsett"].AsReal();
                        float scales = (float)faceMap["scales"].AsReal();
                        float scalet = (float)faceMap["scalet"].AsReal();

                        if(imagerot != 0)
                            f.Rotation = imagerot;

                        if(offsets != 0)
                            f.OffsetU = offsets;

                        if (offsett != 0)
                            f.OffsetV = offsett;

                        if (scales != 0)
                            f.RepeatU = scales;

                        if (scalet != 0)
                            f.RepeatV = scalet;

                        if (textures.Count > textureNum)
                            f.TextureID = textures[textureNum];
                        else
                            f.TextureID = Primitive.TextureEntry.WHITE_TEXTURE;

                        textureEntry.FaceTextures[face] = f;
                    }

                    pbs.TextureEntry = textureEntry.GetBytes();

                    AssetBase meshAsset = new AssetBase(UUID.Random(), assetName, (sbyte)AssetType.Mesh, "");
                    meshAsset.Data = mesh_list[i].AsBinary();
                    m_assetService.Store(meshAsset);

                    pbs.SculptEntry = true;
                    pbs.SculptTexture = meshAsset.FullID;
                    pbs.SculptType = (byte)SculptType.Mesh;
                    pbs.SculptData = meshAsset.Data;

                    Vector3 position = inner_instance_list["position"].AsVector3();
                    Vector3 scale = inner_instance_list["scale"].AsVector3();
                    Quaternion rotation = inner_instance_list["rotation"].AsQuaternion();

// no longer used - begin ------------------------
//                    int physicsShapeType = inner_instance_list["physics_shape_type"].AsInteger();
//                    int material = inner_instance_list["material"].AsInteger();
//                    int mesh = inner_instance_list["mesh"].AsInteger();

//                    OSDMap permissions = (OSDMap)inner_instance_list["permissions"];
//                    int base_mask = permissions["base_mask"].AsInteger();
//                    int everyone_mask = permissions["everyone_mask"].AsInteger();
//                    UUID creator_id = permissions["creator_id"].AsUUID();
//                    UUID group_id = permissions["group_id"].AsUUID();
//                    int group_mask = permissions["group_mask"].AsInteger();
//                    bool is_owner_group = permissions["is_owner_group"].AsBoolean();
//                    UUID last_owner_id = permissions["last_owner_id"].AsUUID();
//                    int next_owner_mask = permissions["next_owner_mask"].AsInteger();
//                    UUID owner_id = permissions["owner_id"].AsUUID();
//                    int owner_mask = permissions["owner_mask"].AsInteger();
// no longer used - end ------------------------

		      UUID owner_id = m_HostCapsObj.AgentID;

                    SceneObjectPart prim
                        = new SceneObjectPart(owner_id, pbs, position, Quaternion.Identity, Vector3.Zero);

                    prim.Scale = scale;
                    //prim.OffsetPosition = position;
                    rotations.Add(rotation);
                    positions.Add(position);
                    prim.UUID = UUID.Random();
                    prim.CreatorID = owner_id;
                    prim.OwnerID = owner_id;
                    prim.GroupID = UUID.Zero;
                    prim.LastOwnerID = prim.OwnerID;
                    prim.CreationDate = Util.UnixTimeSinceEpoch();
                    prim.Name = assetName;
                    prim.Description = "";

//                    prim.BaseMask = (uint)base_mask;
//                    prim.EveryoneMask = (uint)everyone_mask;
//                    prim.GroupMask = (uint)group_mask;
//                    prim.NextOwnerMask = (uint)next_owner_mask;
//                    prim.OwnerMask = (uint)owner_mask;

                    if (grp == null)
                        grp = new SceneObjectGroup(prim);
                    else
                        grp.AddPart(prim);
                }

                Vector3 rootPos = positions[0];

                if (grp.Parts.Length > 1)
                {
                    // Fix first link number
                    grp.RootPart.LinkNum++;

                    Quaternion rootRotConj = Quaternion.Conjugate(rotations[0]);
                    Quaternion tmprot;
                    Vector3 offset;

                    // fix children rotations and positions
                    for (int i = 1; i < rotations.Count; i++)
                    {
                        tmprot = rotations[i];
                        tmprot = rootRotConj * tmprot;

                        grp.Parts[i].RotationOffset = tmprot;

                        offset = positions[i] - rootPos;

                        offset *= rootRotConj;
                        grp.Parts[i].OffsetPosition = offset;
                    }

                    grp.AbsolutePosition = rootPos;
                    grp.UpdateGroupRotationR(rotations[0]);
                }
                else
                {
                    grp.AbsolutePosition = rootPos;
                    grp.UpdateGroupRotationR(rotations[0]);
                }

                data = ASCIIEncoding.ASCII.GetBytes(SceneObjectSerializer.ToOriginalXmlFormat(grp));
            }

            AssetBase asset;
            asset = new AssetBase(assetID, assetName, assType, m_HostCapsObj.AgentID.ToString());
            asset.Data = data;
            if (AddNewAsset != null)
                AddNewAsset(asset);
            else if (m_assetService != null)
                m_assetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Owner = m_HostCapsObj.AgentID;
            item.CreatorId = m_HostCapsObj.AgentID.ToString();
            item.CreatorData = String.Empty;
            item.ID = inventoryItem;
            item.AssetID = asset.FullID;
            item.Description = assetDescription;
            item.Name = assetName;
            item.AssetType = assType;
            item.InvType = inType;
            item.Folder = parentFolder;

            // If we set PermissionMask.All then when we rez the item the next permissions will replace the current
            // (owner) permissions.  This becomes a problem if next permissions are changed.
            item.CurrentPermissions
                = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer | PermissionMask.Export);

            item.BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
            item.EveryOnePermissions = 0;
            item.NextPermissions = (uint)PermissionMask.All;
            item.CreationDate = Util.UnixTimeSinceEpoch();

            if (AddNewInventoryItem != null)
            {
                AddNewInventoryItem(m_HostCapsObj.AgentID, item);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            m_log.Debug("[CAPS]: MapLayer Request in region: " + m_regionName);
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            return mapResponse;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected static OSDMapLayer GetOSDMapLayerResponse()
        {
            OSDMapLayer mapLayer = new OSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
            mapLayer.ImageID = new UUID("00000000-0000-1111-9999-000000000006");

            return mapLayer;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string RequestTexture(string request, string path, string param)
        {
            m_log.Debug("texture request " + request);
            // Needs implementing (added to remove compiler warning)
            return String.Empty;
        }


        /// <summary>
        /// Called by the notecard update handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string NoteCardAgentInventory(string request, string path, string param,
                                             IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.Debug("[CAPS]: NoteCardAgentInventory Request in region: " + m_regionName + "\n" + request);
            //m_log.Debug("[CAPS]: NoteCardAgentInventory Request is: " + request);

            //OpenMetaverse.StructuredData.OSDMap hash = (OpenMetaverse.StructuredData.OSDMap)OpenMetaverse.StructuredData.LLSDParser.DeserializeBinary(Utils.StringToBytes(request));
            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
            LLSDItemUpdate llsdRequest = new LLSDItemUpdate();
            LLSDHelpers.DeserialiseOSDMap(hash, llsdRequest);

            string capsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            ItemUpdater uploader =
                new ItemUpdater(llsdRequest.item_id, capsBase + uploaderPath, m_HostCapsObj.HttpListener, m_dumpAssetsToFile);
            uploader.OnUpLoad += ItemUpdated;

            m_HostCapsObj.HttpListener.AddStreamHandler(
                new BinaryStreamHandler(
                    "POST", capsBase + uploaderPath, uploader.uploaderCaps, "NoteCardAgentInventory", null));

            string protocol = "http://";

            if (m_HostCapsObj.SSLCaps)
                protocol = "https://";

            string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + capsBase +
                                 uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";

            //            m_log.InfoFormat("[CAPS]: " +
            //                             "NoteCardAgentInventory response: {0}",
            //                             LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

            return LLSDHelpers.SerialiseLLSDReply(uploadResponse);
        }

        /// <summary>
        /// Called by the CopyInventoryFromNotecard caps handler.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        public string CopyInventoryFromNotecard(string request, string path, string param,
                                             IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            Hashtable response = new Hashtable();
            response["int_response_code"] = 404;
            response["content_type"] = "text/plain";
            response["keepalive"] = false;
            response["str_response_string"] = "";

            try
            {
                OSDMap content = (OSDMap)OSDParser.DeserializeLLSDXml(request);
                UUID objectID = content["object-id"].AsUUID();
                UUID notecardID = content["notecard-id"].AsUUID();
                UUID folderID = content["folder-id"].AsUUID();
                UUID itemID = content["item-id"].AsUUID();

                //  m_log.InfoFormat("[CAPS]: CopyInventoryFromNotecard, FolderID:{0}, ItemID:{1}, NotecardID:{2}, ObjectID:{3}", folderID, itemID, notecardID, objectID);

                if (objectID != UUID.Zero)
                {
                    SceneObjectPart part = m_Scene.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
//                        TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(notecardID);
                        if (!m_Scene.Permissions.CanCopyObjectInventory(notecardID, objectID, m_HostCapsObj.AgentID))
                        {
                            return LLSDHelpers.SerialiseLLSDReply(response);
                        }
                    }
                }

                InventoryItemBase item = null;
                InventoryItemBase copyItem = null;
                IClientAPI client = null;

                m_Scene.TryGetClient(m_HostCapsObj.AgentID, out client);
                item = m_Scene.InventoryService.GetItem(new InventoryItemBase(itemID));
                if (item != null)
                {
                    string message;
                    copyItem = m_Scene.GiveInventoryItem(m_HostCapsObj.AgentID, item.Owner, itemID, folderID, out message);
                    if (client != null)
                    {
                        if (copyItem != null)
                        {
                            m_log.InfoFormat("[CAPS]: CopyInventoryFromNotecard, ItemID:{0}, FolderID:{1}", copyItem.ID, copyItem.Folder);
                            client.SendBulkUpdateInventory(copyItem);
                        }
                        else
                        {
                            client.SendAgentAlertMessage(message, false);
                        }
                    }
                }
                else
                {
                    m_log.ErrorFormat("[CAPS]: CopyInventoryFromNotecard - Failed to retrieve item {0} from notecard {1}", itemID, notecardID);
                    if (client != null)
                        client.SendAgentAlertMessage("Failed to retrieve item", false);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[CAPS]: CopyInventoryFromNotecard : {0}", e.ToString());
            }

            response["int_response_code"] = 200;
            return LLSDHelpers.SerialiseLLSDReply(response);
        }

        public string GetObjectPhysicsData(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap resp = new OSDMap();
            OSDArray object_ids = (OSDArray)req["object_ids"];

            for (int i = 0 ; i < object_ids.Count ; i++)
            {
                UUID uuid = object_ids[i].AsUUID();

                SceneObjectPart obj = m_Scene.GetSceneObjectPart(uuid);
                if (obj != null)
                {
                    OSDMap object_data = new OSDMap();

                    object_data["PhysicsShapeType"] = obj.PhysicsShapeType;
                    object_data["Density"] = obj.Density;
                    object_data["Friction"] = obj.Friction;
                    object_data["Restitution"] = obj.Restitution;
                    object_data["GravityMultiplier"] = obj.GravityModifier;

                    resp[uuid.ToString()] = object_data;
                }
            }

            string response = OSDParser.SerializeLLSDXmlString(resp);
            return response;
        }

        public string UpdateAgentInformation(string request, string path,
                string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            OSDMap accessPrefs = (OSDMap)req["access_prefs"];
            string desiredMaturity = accessPrefs["max"];

            OSDMap resp = new OSDMap();
            OSDMap respAccessPrefs = new OSDMap();
            respAccessPrefs["max"] = desiredMaturity;   // echoing the maturity back means success
            resp["access_prefs"] = respAccessPrefs;

            string response = OSDParser.SerializeLLSDXmlString(resp);
            return response; 
        }
    }

    public class AssetUploader
    {
        public event UpLoadedAsset OnUpLoad;
        private UpLoadedAsset handlerUpLoad = null;

        private string uploaderPath = String.Empty;
        private UUID newAssetID;
        private UUID inventoryItemID;
        private UUID parentFolder;
        private IHttpServer httpListener;
        private bool m_dumpAssetsToFile;
        private string m_assetName = String.Empty;
        private string m_assetDes = String.Empty;

        private string m_invType = String.Empty;
        private string m_assetType = String.Empty;

        public AssetUploader(string assetName, string description, UUID assetID, UUID inventoryItem,
                                UUID parentFolderID, string invType, string assetType, string path,
                                IHttpServer httpServer, bool dumpAssetsToFile)
        {
            m_assetName = assetName;
            m_assetDes = description;
            newAssetID = assetID;
            inventoryItemID = inventoryItem;
            uploaderPath = path;
            httpListener = httpServer;
            parentFolder = parentFolderID;
            m_assetType = assetType;
            m_invType = invType;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        /// <summary>
        /// Handle raw asset upload data via the capability.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string uploaderCaps(byte[] data, string path, string param)
        {
            UUID inv = inventoryItemID;
            string res = String.Empty;
            LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
            uploadComplete.new_asset = newAssetID.ToString();
            uploadComplete.new_inventory_item = inv;
            uploadComplete.state = "complete";

            res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

            httpListener.RemoveStreamHandler("POST", uploaderPath);

            // TODO: probably make this a better set of extensions here
            string extension = ".jp2";
            if (m_invType != "image")
            {
                extension = ".dat";
            }

            if (m_dumpAssetsToFile)
            {
                SaveAssetToFile(m_assetName + extension, data);
            }
            handlerUpLoad = OnUpLoad;
            if (handlerUpLoad != null)
            {
                handlerUpLoad(m_assetName, m_assetDes, newAssetID, inv, parentFolder, data, m_invType, m_assetType);
            }

            return res;
        }

        ///Left this in and commented in case there are unforseen issues
        //private void SaveAssetToFile(string filename, byte[] data)
        //{
        //    FileStream fs = File.Create(filename);
        //    BinaryWriter bw = new BinaryWriter(fs);
        //    bw.Write(data);
        //    bw.Close();
        //    fs.Close();
        //}

        private static void SaveAssetToFile(string filename, byte[] data)
        {
            string assetPath = "UserAssets";
            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            FileStream fs = File.Create(Path.Combine(assetPath, Util.safeFileName(filename)));
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(data);
            bw.Close();
            fs.Close();
        }
    }

    /// <summary>
    /// This class is a callback invoked when a client sends asset data to
    /// an agent inventory notecard update url
    /// </summary>
    public class ItemUpdater
    {
        public event UpdateItem OnUpLoad;

        private UpdateItem handlerUpdateItem = null;

        private string uploaderPath = String.Empty;
        private UUID inventoryItemID;
        private IHttpServer httpListener;
        private bool m_dumpAssetToFile;

        public ItemUpdater(UUID inventoryItem, string path, IHttpServer httpServer, bool dumpAssetToFile)
        {
            m_dumpAssetToFile = dumpAssetToFile;

            inventoryItemID = inventoryItem;
            uploaderPath = path;
            httpListener = httpServer;
        }

        /// <summary>
        /// Handle raw uploaded asset data.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string uploaderCaps(byte[] data, string path, string param)
        {
            UUID inv = inventoryItemID;
            string res = String.Empty;
            LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
            UUID assetID = UUID.Zero;
            handlerUpdateItem = OnUpLoad;
            if (handlerUpdateItem != null)
            {
                assetID = handlerUpdateItem(inv, data);
            }

            uploadComplete.new_asset = assetID.ToString();
            uploadComplete.new_inventory_item = inv;
            uploadComplete.state = "complete";

            res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

            httpListener.RemoveStreamHandler("POST", uploaderPath);

            if (m_dumpAssetToFile)
            {
                SaveAssetToFile("updateditem" + Util.RandomClass.Next(1, 1000) + ".dat", data);
            }

            return res;
        }

        ///Left this in and commented in case there are unforseen issues
        //private void SaveAssetToFile(string filename, byte[] data)
        //{
        //    FileStream fs = File.Create(filename);
        //    BinaryWriter bw = new BinaryWriter(fs);
        //    bw.Write(data);
        //    bw.Close();
        //    fs.Close();
        //}

        private static void SaveAssetToFile(string filename, byte[] data)
        {
            string assetPath = "UserAssets";
            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            FileStream fs = File.Create(Path.Combine(assetPath, filename));
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(data);
            bw.Close();
            fs.Close();
        }
    }

    /// <summary>
    /// This class is a callback invoked when a client sends asset data to
    /// a task inventory script update url
    /// </summary>
    public class TaskInventoryScriptUpdater
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event UpdateTaskScript OnUpLoad;

        private UpdateTaskScript handlerUpdateTaskScript = null;

        private string uploaderPath = String.Empty;
        private UUID inventoryItemID;
        private UUID primID;
        private bool isScriptRunning;
        private IHttpServer httpListener;
        private bool m_dumpAssetToFile;

        public TaskInventoryScriptUpdater(UUID inventoryItemID, UUID primID, int isScriptRunning,
                                            string path, IHttpServer httpServer, bool dumpAssetToFile)
        {
            m_dumpAssetToFile = dumpAssetToFile;

            this.inventoryItemID = inventoryItemID;
            this.primID = primID;

            // This comes in over the packet as an integer, but actually appears to be treated as a bool
            this.isScriptRunning = (0 == isScriptRunning ? false : true);

            uploaderPath = path;
            httpListener = httpServer;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string uploaderCaps(byte[] data, string path, string param)
        {
            try
            {
                //                    m_log.InfoFormat("[CAPS]: " +
                //                                     "TaskInventoryScriptUpdater received data: {0}, path: {1}, param: {2}",
                //                                     data, path, param));

                string res = String.Empty;
                LLSDTaskScriptUploadComplete uploadComplete = new LLSDTaskScriptUploadComplete();

                ArrayList errors = new ArrayList();
                handlerUpdateTaskScript = OnUpLoad;
                if (handlerUpdateTaskScript != null)
                {
                    handlerUpdateTaskScript(inventoryItemID, primID, isScriptRunning, data, ref errors);
                }

                uploadComplete.new_asset = inventoryItemID;
                uploadComplete.compiled = errors.Count > 0 ? false : true;
                uploadComplete.state = "complete";
                uploadComplete.errors = new OpenSim.Framework.Capabilities.OSDArray();
                uploadComplete.errors.Array = errors;

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

                httpListener.RemoveStreamHandler("POST", uploaderPath);

                if (m_dumpAssetToFile)
                {
                    SaveAssetToFile("updatedtaskscript" + Util.RandomClass.Next(1, 1000) + ".dat", data);
                }

                //                    m_log.InfoFormat("[CAPS]: TaskInventoryScriptUpdater.uploaderCaps res: {0}", res);

                return res;
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }

            // XXX Maybe this should be some meaningful error packet
            return null;
        }

        ///Left this in and commented in case there are unforseen issues
        //private void SaveAssetToFile(string filename, byte[] data)
        //{
        //    FileStream fs = File.Create(filename);
        //    BinaryWriter bw = new BinaryWriter(fs);
        //    bw.Write(data);
        //    bw.Close();
        //    fs.Close();
        //}
        private static void SaveAssetToFile(string filename, byte[] data)
        {
            string assetPath = "UserAssets";
            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }
            FileStream fs = File.Create(Path.Combine(assetPath, filename));
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(data);
            bw.Close();
            fs.Close();
        }
    }
}
