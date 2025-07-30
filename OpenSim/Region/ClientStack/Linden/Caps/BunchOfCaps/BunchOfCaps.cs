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
using System.Timers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using PermissionMask = OpenSim.Framework.PermissionMask;
using Timer = System.Threading.Timer;

namespace OpenSim.Region.ClientStack.Linden
{
    public delegate void UpLoadedAsset(
        string assetName, string description, UUID assetID, UUID inventoryItem, UUID parentFolder,
        byte[] data, string inventoryType, string assetType,
        int cost, UUID texturesFolder, int nreqtextures, int nreqmeshs, int nreqinstances,
        bool IsAtestUpload, ref string error, ref int nextOwnerMask, ref int groupMask, ref int everyoneMask, int[] meshesSides);

    public delegate void UpdateTaskScript(UUID itemID, UUID primID, bool isScriptRunning, byte[] data, ref ArrayList errors);

    public delegate void NewInventoryItem(UUID userID, InventoryItemBase item, uint cost);

    public delegate void NewAsset(AssetBase asset);

    public delegate ArrayList TaskScriptUpdatedCallback(UUID userID, UUID itemID, UUID primID,
                                                   bool isScriptRunning, byte[] data);

    /// <summary>
    /// XXX Probably not a particularly nice way of allow us to get the scene presence from the scene (chiefly so that
    /// we can popup a message on the user's client if the inventory service has permanently failed).  But I didn't want
    /// to just pass the whole Scene into CAPS.
    /// </summary>
    public delegate IClientAPI GetClientDelegate(UUID agentID);

    public partial class BunchOfCaps
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;
        private UUID m_AgentID;
        private UUID m_scopeID;
        private Caps m_HostCapsObj;
        private ModelCost m_ModelCost;
        private BunchOfCapsConfigOptions ConfigOptions;

        // private static readonly string m_remoteParcelRequestPath = "0009/";// This is in the LandManagementModule.

        // These are callbacks which will be setup by the scene so that we can update scene data when we
        // receive capability calls
        public NewInventoryItem AddNewInventoryItem = null;
        public NewAsset AddNewAsset = null;
        public ItemUpdatedCallback ItemUpdatedCall = null;
        public TaskScriptUpdatedCallback TaskScriptUpdatedCall = null;
        public GetClientDelegate GetClient = null;
        
        private IAssetService m_assetService;
        private bool m_dumpAssetsToFile = false;
        private string m_regionName;

        private IUserManagement m_UserManager;
        private IUserAccountService m_userAccountService;
        private IMoneyModule m_moneyModule;

        private enum FileAgentInventoryState : int
        {
            idle = 0,
            processRequest = 1,
            waitUpload = 2,
            processUpload = 3
        }
        private FileAgentInventoryState m_FileAgentInventoryState = FileAgentInventoryState.idle;

        public BunchOfCaps(Scene scene, UUID agentID, Caps caps, BunchOfCapsConfigOptions configOptions)
        {
            m_Scene = scene;
            m_AgentID = agentID;
            m_HostCapsObj = caps;
            ConfigOptions = configOptions;

            //cache model upload cost provider
            m_ModelCost = configOptions.ModelCost;

            m_assetService = m_Scene.AssetService;
            m_regionName = m_Scene.RegionInfo.RegionName;
            m_UserManager = m_Scene.RequestModuleInterface<IUserManagement>();
            m_userAccountService = m_Scene.RequestModuleInterface<IUserAccountService>();
            m_moneyModule = m_Scene.RequestModuleInterface<IMoneyModule>();
            if (m_UserManager is null)
                m_log.Error("[CAPS]: GetDisplayNames disabled because user management component not found");

            UserAccount account = m_userAccountService?.GetUserAccount(m_Scene.RegionInfo.ScopeID, m_AgentID);
            if (account is null) // Hypergrid?
                m_scopeID = m_Scene.RegionInfo.ScopeID;
            else
                m_scopeID = account.ScopeID;

            AddNewInventoryItem = m_Scene.AddUploadedInventoryItem;
            ItemUpdatedCall = m_Scene.CapsUpdateItemAsset;
            TaskScriptUpdatedCall = m_Scene.CapsUpdateTaskInventoryScriptAsset;
            GetClient = m_Scene.SceneGraph.GetControllingClient;

            RegisterHandlers();

            m_FileAgentInventoryState = FileAgentInventoryState.idle;
        }

        public string GetNewCapPath()
        {
            return  "/" + UUID.Random();
        }

        /// <summary>
        /// Register a bunch of CAPS http service handlers
        /// </summary>
        public void RegisterHandlers()
        {
            // this path is also defined elsewhere so keeping it
            string seedcapsBase = "/CAPS/" + m_HostCapsObj.CapsObjectPath + "0000";

            m_HostCapsObj.RegisterSimpleHandler("SEED", new SimpleStreamHandler(seedcapsBase, SeedCapRequest));
            // m_log.DebugFormat(
            //     "[CAPS]: Registered seed capability {0} for {1}", seedcapsBase, m_HostCapsObj.AgentID);

            RegisterRegionServiceHandlers();
            RegisterInventoryServiceHandlers();
            RegisterOtherHandlers();
        }

        public void RegisterRegionServiceHandlers()
        {
            try
            {
                m_HostCapsObj.RegisterSimpleHandler("GetObjectPhysicsData",
                    new SimpleOSDMapHandler("POST", GetNewCapPath(), GetObjectPhysicsData));

                m_HostCapsObj.RegisterSimpleHandler("GetObjectCost",
                    new SimpleOSDMapHandler("POST", GetNewCapPath(), GetObjectCost));

                m_HostCapsObj.RegisterSimpleHandler("ResourceCostSelected",
                    new SimpleOSDMapHandler("POST", GetNewCapPath(), ResourceCostSelected));
 
                if(ConfigOptions.AllowCapHomeLocation)
                {
                    m_HostCapsObj.RegisterSimpleHandler("HomeLocation",
                        new SimpleStreamHandler(GetNewCapPath(), HomeLocation));
                }

                if (ConfigOptions.AllowCapGroupMemberData)
                {
                    m_HostCapsObj.RegisterSimpleHandler("GroupMemberData",
                        new SimpleStreamHandler(GetNewCapPath(), GroupMemberData));
                }

                if (ConfigOptions.AllowCapLandResources)
                {
                    m_HostCapsObj.RegisterSimpleHandler("LandResources",
                        new SimpleOSDMapHandler("POST", GetNewCapPath(), LandResources));
                }

                if (ConfigOptions.AllowCapAttachmentResources)
                {
                    m_HostCapsObj.RegisterSimpleHandler("AttachmentResources",
                        new SimpleStreamHandler(GetNewCapPath(), AttachmentResources));
                }

                m_HostCapsObj.RegisterSimpleHandler("DispatchRegionInfo",
                    new SimpleOSDMapHandler("POST", GetNewCapPath(), DispatchRegionInfo), true);
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: Error " + e.Message);
            }
        }

        public void RegisterInventoryServiceHandlers()
        {
            try
            {
                m_HostCapsObj.RegisterHandler("NewFileAgentInventory",
                    new LLSDStreamhandler<LLSDAssetUploadRequest, LLSDAssetUploadResponse>(
                        "POST", GetNewCapPath(), NewAgentInventoryRequest, "NewFileAgentInventory", null));

                SimpleOSDMapHandler oreq;
                if (ItemUpdatedCall is not null)
                {
                    // first sets the http handler, others only register the cap, using it
                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateNotecardItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateNotecardAgentInventory", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateNotecardTaskInventory", oreq, false); // a object inv

                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateAnimSetItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateAnimSetAgentInventory", oreq, true);
                    //m_HostCapsObj.RegisterSimpleHandler("UpdateAnimSetTaskInventory", oreq, false);

                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateScriptItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateScriptAgent", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateScriptAgentInventory", oreq, false); //legacy

                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateSettingsItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateSettingsAgentInventory", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateSettingsTaskInventory", oreq, false); // a object inv

                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateMaterialItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateMaterialAgentInventory", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateMaterialTaskInventory", oreq, false); // a object inv

                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateGestureItemAsset);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateGestureAgentInventory", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateGestureTaskInventory", oreq, false);
                }

                if (TaskScriptUpdatedCall is not null)
                {
                    oreq = new SimpleOSDMapHandler("POST", GetNewCapPath(), UpdateScriptTaskInventory);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateScriptTask", oreq, true);
                    m_HostCapsObj.RegisterSimpleHandler("UpdateScriptTaskInventory", oreq, true); //legacy
                }

                m_HostCapsObj.RegisterSimpleHandler("CopyInventoryFromNotecard",
                    new SimpleOSDMapHandler("POST", GetNewCapPath(), CopyInventoryFromNotecard));

                m_HostCapsObj.RegisterSimpleHandler("CreateInventoryCategory",
                    new SimpleStreamHandler(GetNewCapPath(), CreateInventoryCategory));
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }
        }

        public void RegisterOtherHandlers()
        {
            try
            {
                if (m_UserManager is not null)
                {
                    m_HostCapsObj.RegisterSimpleHandler("GetDisplayNames",
                        new SimpleStreamHandler(GetNewCapPath(), GetDisplayNames));
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: Error " + e.Message);
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
        public void SeedCapRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            m_log.Debug(
                $"[CAPS]: Received SEED caps request in {m_regionName} for agent {m_HostCapsObj.AgentID}");

            if(httpRequest.HttpMethod != "POST" || httpRequest.ContentType != "application/llsd+xml")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!m_HostCapsObj.WaitForActivation())
            {
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                httpResponse.AddHeader("Retry-After", "30");
                return;
            }

            if (!m_Scene.CheckClient(m_HostCapsObj.AgentID, httpRequest.RemoteIPEndPoint))
            {
                m_log.WarnFormat(
                    $"[CAPS]: Unauthorized CAPS client {m_HostCapsObj.AgentID} from {httpRequest.RemoteIPEndPoint}");
                httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            OSDArray capsRequested;
            try
            {
                capsRequested = (OSDArray)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            HashSet<string> validCaps = new();

            foreach (OSD c in capsRequested)
            {
                string cstr = c.AsString();
                if (string.IsNullOrEmpty(cstr))
                    continue;
                switch (cstr)
                {
                    case "SEED":
                        continue;
                    case "ViewerBenefits":
                        m_HostCapsObj.Flags |= Caps.CapsFlags.ViewerBenefits;
                        continue;
                    case "VTPBR":
                        if (m_Scene.RegionInfo.RegionSizeX == Constants.RegionSize &&
                            m_Scene.RegionInfo.RegionSizeY == Constants.RegionSize )
                        {
                            m_HostCapsObj.Flags |= Caps.CapsFlags.PBR | Caps.CapsFlags.TPBR;
                        }
                        else
                            m_HostCapsObj.Flags |= Caps.CapsFlags.PBR;
                        continue;
                    case "VETPBR":
                        m_HostCapsObj.Flags |= Caps.CapsFlags.PBR | Caps.CapsFlags.TPBR;
                        continue;
                    case "ObjectAnimation":
                         m_HostCapsObj.Flags |= Caps.CapsFlags.ObjectAnim;
                        break;
                    case "EnvironmentSettings":
                        m_HostCapsObj.Flags |= Caps.CapsFlags.WLEnv;
                        break;
                    case "ExtEnvironment":
                        m_HostCapsObj.Flags |= Caps.CapsFlags.AdvEnv;
                        break;
                    case "ModifyMaterialParams": // will not work if a viewer has no edit features
                        m_HostCapsObj.Flags |= Caps.CapsFlags.PBR;
                        break;
                    default:
                        break;
                }
                validCaps.Add(cstr);
            }

            osUTF8 sb = LLSDxmlEncode2.Start();
            LLSDxmlEncode2.AddMap(sb);
            m_HostCapsObj.GetCapsDetailsLLSDxml(validCaps, sb);
            LLSDxmlEncode2.AddEndMap(sb);

            httpResponse.RawBuffer = LLSDxmlEncode2.EndToBytes(sb);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            m_HostCapsObj.Flags |= Caps.CapsFlags.SentSeeds;
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

            // start by getting the client
            IClientAPI client = null;
            m_Scene.TryGetClient(m_HostCapsObj.AgentID, out client);

            // check current state so we only have one service at a time
            lock (m_ModelCost)
            {
                switch (m_FileAgentInventoryState)
                {
                    case FileAgentInventoryState.processRequest:
                    case FileAgentInventoryState.processUpload:
                        LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                        resperror.message = "Uploader busy processing previous request";
                        resperror.identifier = UUID.Zero;

                        LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                        errorResponse.uploader = "";
                        errorResponse.state = "error";
                        errorResponse.error = resperror;
                        return errorResponse;
                    case FileAgentInventoryState.waitUpload:
                        // todo stop current uploader server
                        break;
                    case FileAgentInventoryState.idle:
                    default:
                        break;
                }

                m_FileAgentInventoryState = FileAgentInventoryState.processRequest;
            }

            int cost = 0;
            int nreqtextures = 0;
            int nreqmeshs= 0;
            int nreqinstances = 0;
            bool IsAtestUpload = false;
            int[] meshesSides = null;

            string assetName = llsdRequest.name;

            LLSDAssetUploadResponseData meshcostdata = new LLSDAssetUploadResponseData();

            if (llsdRequest.asset_type == "texture" ||
                llsdRequest.asset_type == "animation" ||
                llsdRequest.asset_type == "animatn" ||    // this is the asset name actually used by viewers
                llsdRequest.asset_type == "mesh" ||
                llsdRequest.asset_type == "sound")
            {
                ScenePresence avatar = null;
                m_Scene.TryGetScenePresence(m_HostCapsObj.AgentID, out avatar);

                // check user level
                if (avatar is not null)
                {
                    if (avatar.GodController.UserLevel < ConfigOptions.levelUpload)
                    {
                        LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                        resperror.message = "Insufficient permissions to upload";
                        resperror.identifier = UUID.Zero;

                        LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                        errorResponse.uploader = "";
                        errorResponse.state = "error";
                        errorResponse.error = resperror;
                        lock (m_ModelCost)
                            m_FileAgentInventoryState = FileAgentInventoryState.idle;
                        return errorResponse;
                    }
                }

                // check test upload and funds
                if (client is not null)
                {
                    IMoneyModule mm = m_Scene.RequestModuleInterface<IMoneyModule>();

                    int baseCost = 0;
                    if (mm is not null)
                        baseCost = mm.UploadCharge;

                    string warning = String.Empty;

                    if (llsdRequest.asset_type == "mesh")
                    {
                        string error;
                        int modelcost;

                        if (!m_ModelCost.MeshModelCost(llsdRequest.asset_resources, baseCost, out modelcost,
                            meshcostdata, out error, ref warning, out meshesSides))
                        {
                            LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                            resperror.message = error;
                            resperror.identifier = UUID.Zero;

                            LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                            errorResponse.uploader = "";
                            errorResponse.state = "error";
                            errorResponse.error = resperror;

                            lock (m_ModelCost)
                                m_FileAgentInventoryState = FileAgentInventoryState.idle;
                            return errorResponse;
                        }
                        cost = modelcost;
                    }
                    else
                    {
                        cost = baseCost;
                    }

                    if (cost > 0 && mm != null)
                    {
                        // check for test upload

                        if (ConfigOptions.ForceFreeTestUpload) // all are test
                        {
                            if (!(assetName.Length > 5 && assetName.StartsWith("TEST-"))) // has normal name lets change it
                                assetName = "TEST-" + assetName;

                            IsAtestUpload = true;
                        }

                        else if (ConfigOptions.enableFreeTestUpload) // only if prefixed with "TEST-"
                        {

                            IsAtestUpload = (assetName.Length > 5 && assetName.StartsWith("TEST-"));
                        }

                        if(IsAtestUpload) // let user know, still showing cost estimation
                            warning += "Upload will have no cost, for testing purposes only. Other uses are prohibited. Items will be local to region only, Inventory entry will be lost on logout";

                        // check funds
                        else
                        {
                            if (!mm.UploadCovered(client.AgentId, (int)cost))
                            {
                                LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                                resperror.message = "Insuficient funds";
                                resperror.identifier = UUID.Zero;

                                LLSDAssetUploadResponse errorResponse = new LLSDAssetUploadResponse();
                                errorResponse.uploader = "";
                                errorResponse.state = "error";
                                errorResponse.error = resperror;
                                lock (m_ModelCost)
                                    m_FileAgentInventoryState = FileAgentInventoryState.idle;
                                return errorResponse;
                            }
                        }
                    }
                    else if (ConfigOptions.enableFreeTestUpload) // only if prefixed with "TEST-"
                    {
                        IsAtestUpload = (assetName.Length > 5 && assetName.StartsWith("TEST-"));
                        if(IsAtestUpload)
                            warning += "Upload for testing purposes only. Items will be local to region only, Inventory entry will be lost on logout";
                    }

                    if (client != null && warning != String.Empty)
                        client.SendAgentAlertMessage(warning, true);
                }
            }

            string assetDes = llsdRequest.description;
            UUID newAsset = UUID.Random();
            UUID newInvItem = UUID.Random();
            UUID parentFolder = llsdRequest.folder_id;
            string uploaderPath = GetNewCapPath();
            UUID texturesFolder = UUID.Zero;

            if(!IsAtestUpload && ConfigOptions.enableModelUploadTextureToInventory)
                texturesFolder = llsdRequest.texture_folder_id;

            AssetUploader uploader =
                new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, llsdRequest.inventory_type,
                        llsdRequest.asset_type, uploaderPath, m_HostCapsObj.HttpListener, m_dumpAssetsToFile, cost,
                        texturesFolder, nreqtextures, nreqmeshs, nreqinstances, IsAtestUpload,
                        llsdRequest.next_owner_mask, llsdRequest.group_mask, llsdRequest.everyone_mask, meshesSides);

            m_HostCapsObj.HttpListener.AddStreamHandler(
                new BinaryStreamHandler(
                    "POST",
                    uploaderPath,
                    uploader.uploaderCaps,
                    "NewAgentInventoryRequest",
                    m_HostCapsObj.AgentID.ToString()));

            string protocol = "http://";
            if (m_HostCapsObj.SSLCaps)
                protocol = "https://";

            string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";
            uploadResponse.upload_price = (int)cost;

            if (llsdRequest.asset_type == "mesh")
            {
                uploadResponse.data = meshcostdata;
            }

            uploader.OnUpLoad += UploadCompleteHandler;

            lock (m_ModelCost)
                m_FileAgentInventoryState = FileAgentInventoryState.waitUpload;

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
                                          string assetType, int cost,
                                          UUID texturesFolder, int nreqtextures, int nreqmeshs, int nreqinstances,
                                          bool IsAtestUpload, ref string error,
                                          ref int nextOwnerMask, ref int groupMask, ref int everyoneMask, int[] meshesSides)
        {
            lock (m_ModelCost)
                m_FileAgentInventoryState = FileAgentInventoryState.processUpload;

            m_log.DebugFormat(
                "[BUNCH OF CAPS]: Uploaded asset {0} for inventory item {1}, inv type {2}, asset type {3}",
                assetID, inventoryItem, inventoryType, assetType);

            sbyte assType = 0;
            sbyte inType = 0;

            IClientAPI client = null;

            UUID owner_id = m_HostCapsObj.AgentID;
            UUID creatorID;

            bool istest = IsAtestUpload && ConfigOptions.enableFreeTestUpload;

            bool restrictPerms = ConfigOptions.RestrictFreeTestUploadPerms && istest;

            if (istest && ConfigOptions.testAssetsCreatorID.IsNotZero())
                creatorID = ConfigOptions.testAssetsCreatorID;
            else
                creatorID = owner_id;

            string creatorIDstr = creatorID.ToString();

            IMoneyModule mm = m_Scene.RequestModuleInterface<IMoneyModule>();
            if (mm != null)
            {
                // make sure client still has enougth credit
                if (!mm.UploadCovered(m_HostCapsObj.AgentID, (int)cost))
                {
                    error = "Insufficient funds.";
                    return;
                }
            }

            // strings to types
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
            else if (inventoryType == "animset")
            {
                inType = (sbyte)CustomInventoryType.AnimationSet;
                assType = (sbyte)CustomAssetType.AnimationSet;
                m_log.Debug("got animset upload request");
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
                if (assetType == "mesh") // this code for now is for mesh models uploads only
                {
                    inType = (sbyte)InventoryType.Object;
                    assType = (sbyte)AssetType.Object;

                    List<Vector3> positions = new List<Vector3>();
                    List<Quaternion> rotations = new List<Quaternion>();
                    OSDMap request = (OSDMap)OSDParser.DeserializeLLSDXml(data);

                    // compare and get updated information
/* does nothing still we do need something to avoid special viewer to upload something diferent from the cost estimation
                    bool mismatchError = true;

                    while (mismatchError)
                    {
                        mismatchError = false;
                    }

                    if (mismatchError)
                    {
                        error = "Upload and fee estimation information don't match";
                        lock (m_ModelCost)
                            m_FileAgentInventoryState = FileAgentInventoryState.idle;

                        return;
                    }
*/
                    OSDArray instance_list = (OSDArray)request["instance_list"];
                    OSDArray mesh_list = (OSDArray)request["mesh_list"];
                    OSDArray texture_list = (OSDArray)request["texture_list"];
                    SceneObjectGroup grp = null;

                    // create and store texture assets
                    bool doTextInv = (!istest && ConfigOptions.enableModelUploadTextureToInventory &&
                                    texturesFolder != UUID.Zero);


                    List<UUID> textures = new List<UUID>();


//                    if (doTextInv)
                        m_Scene.TryGetClient(m_HostCapsObj.AgentID, out client);

                    if(client == null) // don't put textures in inventory if there is no client
                        doTextInv = false;

                    for (int i = 0; i < texture_list.Count; i++)
                    {
                        AssetBase textureAsset = new AssetBase(UUID.Random(), assetName, (sbyte)AssetType.Texture, creatorIDstr);
                        textureAsset.Data = texture_list[i].AsBinary();
                        if (istest)
                            textureAsset.Local = true;
                        m_assetService.Store(textureAsset);
                        textures.Add(textureAsset.FullID);

                        if (doTextInv)
                        {
                            string name = assetName;
                            if (name.Length > 25)
                                name = name.Substring(0, 24);
                            name += "_Texture#" + i.ToString();
                            InventoryItemBase texitem = new InventoryItemBase();
                            texitem.Owner = m_HostCapsObj.AgentID;
                            texitem.CreatorId = creatorIDstr;
                            texitem.CreatorData = String.Empty;
                            texitem.ID = UUID.Random();
                            texitem.AssetID = textureAsset.FullID;
                            texitem.Description = "mesh model texture";
                            texitem.Name = name;
                            texitem.AssetType = (int)AssetType.Texture;
                            texitem.InvType = (int)InventoryType.Texture;
                            texitem.Folder = texturesFolder;

                            texitem.CurrentPermissions
                                = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer | PermissionMask.Export);

                            texitem.BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                            texitem.EveryOnePermissions = 0;
                            texitem.NextPermissions = (uint)PermissionMask.All;
                            texitem.CreationDate = Util.UnixTimeSinceEpoch();

                            m_Scene.AddInventoryItem(client, texitem);
                            texitem = null;
                        }
                    }

                    // create and store meshs assets
                    List<UUID> meshAssets = new List<UUID>();
                    List<bool> meshAvatarSkeletons = new List<bool>();
                    List<bool> meshAvatarColliders = new List<bool>();

                    bool curAvSkeleton;
                    bool curAvCollider;
                    for (int i = 0; i < mesh_list.Count; i++)
                    {
                        curAvSkeleton = false;
                        curAvCollider = false;

                        // we do need to parse the mesh now
                        OSD osd = OSDParser.DeserializeLLSDBinary(mesh_list[i]);
                        if (osd is OSDMap)
                        {
                            OSDMap mosd = (OSDMap)osd;
                            if (mosd.ContainsKey("skeleton"))
                            {
                                OSDMap skeleton = (OSDMap)mosd["skeleton"];
                                int sksize = skeleton["size"].AsInteger();
                                if (sksize > 0)
                                    curAvSkeleton = true;
                            }
                        }

                        AssetBase meshAsset = new AssetBase(UUID.Random(), assetName, (sbyte)AssetType.Mesh, creatorIDstr);
                        meshAsset.Data = mesh_list[i].AsBinary();
                        if (istest)
                            meshAsset.Local = true;
                        m_assetService.Store(meshAsset);
                        meshAssets.Add(meshAsset.FullID);
                        meshAvatarSkeletons.Add(curAvSkeleton);
                        meshAvatarColliders.Add(curAvCollider);

                        // test code
                        if (curAvSkeleton && client != null)
                        {
                            string name = assetName;
                            if (name.Length > 25)
                                name = name.Substring(0, 24);
                            name += "_Mesh#" + i.ToString();
                            InventoryItemBase meshitem = new InventoryItemBase();
                            meshitem.Owner = m_HostCapsObj.AgentID;
                            meshitem.CreatorId = creatorIDstr;
                            meshitem.CreatorData = String.Empty;
                            meshitem.ID = UUID.Random();
                            meshitem.AssetID = meshAsset.FullID;
                            meshitem.Description = "mesh ";
                            meshitem.Name = name;
                            meshitem.AssetType = (int)AssetType.Mesh;
                            meshitem.InvType = (int)InventoryType.Mesh;
                            //                            meshitem.Folder = UUID.Zero; // send to default

                            meshitem.Folder = parentFolder; // dont let it go to folder Meshes that viewers dont show

                            // If we set PermissionMask.All then when we rez the item the next permissions will replace the current
                            // (owner) permissions.  This becomes a problem if next permissions are changed.
                            meshitem.CurrentPermissions
                                = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer);

                            meshitem.BasePermissions = (uint)PermissionMask.All;
                            meshitem.EveryOnePermissions = 0;
                            meshitem.NextPermissions = (uint)PermissionMask.All;
                            meshitem.CreationDate = Util.UnixTimeSinceEpoch();

                            m_Scene.AddInventoryItem(client, meshitem);
                            meshitem = null;
                        }
                    }

                    int skipedMeshs = 0;
                    float primScaleMin = m_ModelCost.PrimScaleMin;
                    
                    OSD tmp;
                    // build prims from instances
                    for (int i = 0; i < instance_list.Count; i++)
                    {
                        OSDMap inner_instance_list = (OSDMap)instance_list[i];

                        // skip prims that are 2 small
                        Vector3 scale = inner_instance_list["scale"].AsVector3();

                        if (scale.X < primScaleMin || scale.Y < primScaleMin || scale.Z < primScaleMin)
                        {
                            skipedMeshs++;
                            continue;
                        }

                        OSDArray face_list = (OSDArray)inner_instance_list["face_list"];
                        
                        PrimitiveBaseShape pbs = null;
                        if (inner_instance_list.TryGetValue("mesh", out tmp)) // seems to happen always but ...
                        {
                            int meshindx = tmp.AsInteger();
                            if (meshindx >= 0 && meshAssets.Count > meshindx)
                            {
                                if(meshesSides != null && meshesSides.Length > meshindx)
                                    pbs = PrimitiveBaseShape.CreateMesh(meshesSides[meshindx], meshAssets[meshindx]);
                                else
                                    pbs = PrimitiveBaseShape.CreateMesh(face_list.Count, meshAssets[meshindx]);
                            }
                        }

                        pbs ??= PrimitiveBaseShape.CreateBox(); //fallback

                        Primitive.TextureEntry textureEntry = new(Primitive.TextureEntry.WHITE_TEXTURE);

                        for (uint face = 0; face < face_list.Count; face++)
                        {
                            OSDMap faceMap = (OSDMap)face_list[(int)face];

                            Primitive.TextureEntryFace f = textureEntry.CreateFace(face); //clone the default

                            if (faceMap.TryGetBool("fullbright", out bool fullbright))
                                f.Fullbright = fullbright;

                            if (faceMap.TryGetColor4("diffuse_color", out Color4 rgba))
                                f.RGBA = rgba;

                            if(faceMap.TryGetInt("image", out int textureNum) && textureNum >= 0 && textureNum < textures.Count)
                                f.TextureID = textures[textureNum];

                            if(faceMap.TryGetFloat("imagerot", out float imagerot) && imagerot != 0)
                                f.Rotation = imagerot;

                            if(faceMap.TryGetFloat("offsets", out float offsets) && offsets != 0)
                                f.OffsetU = offsets;

                            if(faceMap.TryGetFloat("offsett", out float offsett) && offsett != 0)
                                f.OffsetV = offsett;

                            if(faceMap.TryGetFloat("scales", out float scales) && scales != 0)
                                f.RepeatU = scales;

                            if(faceMap.TryGetFloat("scalet", out float scalet) && scalet != 0)
                                f.RepeatV = scalet;

                            textureEntry.FaceTextures[face] = f;
                        }

                        pbs.TextureEntry = textureEntry.GetBytes(face_list.Count);

                        Vector3 position = inner_instance_list["position"].AsVector3();
                        Quaternion rotation = inner_instance_list["rotation"].AsQuaternion();

                        byte physicsShapeType = (byte)PhysShapeType.convex; // default is simple convex
                        if (inner_instance_list.ContainsKey("physics_shape_type"))
                            physicsShapeType = (byte)inner_instance_list["physics_shape_type"].AsInteger();
                        byte material = (byte)Material.Wood;
                        if (inner_instance_list.ContainsKey("material"))
                            material = (byte)inner_instance_list["material"].AsInteger();

                        SceneObjectPart prim
                            = new SceneObjectPart(owner_id, pbs, position, Quaternion.Identity, Vector3.Zero);

                        prim.Scale = scale;
                        rotations.Add(rotation);
                        positions.Add(position);
                        prim.UUID = UUID.Random();
                        prim.CreatorID = creatorID;
                        prim.OwnerID = owner_id;
                        prim.GroupID = UUID.Zero;
                        prim.LastOwnerID = creatorID;
                        prim.RezzerID = creatorID;
                        prim.CreationDate = Util.UnixTimeSinceEpoch();

                        if (grp == null)
                            prim.Name = assetName;
                        else
                            prim.Name = assetName + "#" + i.ToString();

                        prim.EveryoneMask = 0;
                        prim.GroupMask = 0;

                        if (restrictPerms)
                        {
                            prim.BaseMask = (uint)(PermissionMask.Move | PermissionMask.Modify);
                            prim.OwnerMask = (uint)(PermissionMask.Move | PermissionMask.Modify);
                            prim.NextOwnerMask = 0;
                        }
                        else
                        {
                            prim.BaseMask = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                            prim.OwnerMask = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                            prim.GroupMask = prim.BaseMask & (uint)groupMask;
                            prim.EveryoneMask = prim.BaseMask & (uint)everyoneMask;
                            prim.NextOwnerMask = prim.BaseMask & (uint)nextOwnerMask;
                            // If the viewer gives us bogus permissions, revert to the SL
                            // default of transfer only.
                            if ((prim.NextOwnerMask & (uint)PermissionMask.All) == 0)
                                prim.NextOwnerMask = (uint)PermissionMask.Transfer;
                        }

                        if(istest)
                            prim.Description = "For testing only. Other uses are prohibited";
                        else
                            prim.Description = "";

                        prim.Material = material;
                        prim.PhysicsShapeType = physicsShapeType;

//                    prim.BaseMask = (uint)base_mask;
//                    prim.EveryoneMask = (uint)everyone_mask;
//                    prim.GroupMask = (uint)group_mask;
//                    prim.NextOwnerMask = (uint)next_owner_mask;
//                    prim.OwnerMask = (uint)owner_mask;

                        if (grp == null)
                        {
                            grp = new SceneObjectGroup(prim);
                            grp.LastOwnerID = creatorID;
                            grp.RezzerID = creatorID;
                        }
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

                else // not a mesh model
                {
                    m_log.ErrorFormat("[CAPS Asset Upload] got unsuported assetType for object upload");
                    return;
                }
            }

            AssetBase asset;
            asset = new AssetBase(assetID, assetName, assType, creatorIDstr);
            asset.Data = data;
            if (istest)
                asset.Local = true;
            if (AddNewAsset != null)
                AddNewAsset(asset);
            else if (m_assetService != null)
                m_assetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Owner = m_HostCapsObj.AgentID;
            item.CreatorId = creatorIDstr;
            item.CreatorData = String.Empty;
            item.ID = inventoryItem;
            item.AssetID = asset.FullID;
            if (istest)
            {
                item.Description = "For testing only. Other uses are prohibited";
                item.Flags = (uint) (InventoryItemFlags.SharedSingleReference);
            }
            else
                item.Description = assetDescription;
            item.Name = assetName;
            item.AssetType = assType;
            item.InvType = inType;
            item.Folder = parentFolder;

            // If we set PermissionMask.All then when we rez the item the next permissions will replace the current
            // (owner) permissions.  This becomes a problem if next permissions are changed.

            if (inType == (sbyte)CustomInventoryType.AnimationSet)
            {
                AnimationSet.setCreateItemPermitions(item);
            }

            else if (restrictPerms)
            {
                item.BasePermissions = (uint)(PermissionMask.Move | PermissionMask.Modify);
                item.CurrentPermissions = (uint)(PermissionMask.Move | PermissionMask.Modify);
                item.GroupPermissions = 0;
                item.EveryOnePermissions = 0;
                item.NextPermissions = 0;
            }
            else
            {
                item.BasePermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                item.CurrentPermissions = (uint)PermissionMask.All | (uint)PermissionMask.Export;
                item.GroupPermissions = item.BasePermissions & (uint)groupMask;
                item.EveryOnePermissions = item.BasePermissions & (uint)everyoneMask;
                item.NextPermissions = item.BasePermissions & (uint)nextOwnerMask;
                if ((item.NextPermissions & (uint)PermissionMask.All) == 0)
                    item.NextPermissions = (uint)PermissionMask.Transfer;
            }

            item.CreationDate = Util.UnixTimeSinceEpoch();

            everyoneMask = (int)item.EveryOnePermissions;
            groupMask = (int)item.GroupPermissions;
            nextOwnerMask = (int)item.NextPermissions;

            m_Scene.TryGetClient(m_HostCapsObj.AgentID, out client);

            if (AddNewInventoryItem != null)
            {
                if (istest)
                {
                    m_Scene.AddInventoryItem(client, item);
/*
                    AddNewInventoryItem(m_HostCapsObj.AgentID, item, 0);
                    if (client != null)
                        client.SendAgentAlertMessage("Upload will have no cost, for personal test purposes only. Other uses are forbiden. Items may not work on a another region" , true);
 */
                }
                else
                {
                    AddNewInventoryItem(m_HostCapsObj.AgentID, item, (uint)cost);
//                    if (client != null)
//                    {
//                        // let users see anything..  i don't so far
//                        string str;
//                        if (cost > 0)
//                            // dont remember where is money unit name to put here
//                            str = "Upload complete. charged " + cost.ToString() + "$";
//                        else
//                            str = "Upload complete";
//                        client.SendAgentAlertMessage(str, true);
//                    }
                }
            }

            lock (m_ModelCost)
                m_FileAgentInventoryState = FileAgentInventoryState.idle;
        }

        public void CreateInventoryCategory(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if(httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            if (m_Scene.InventoryService == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
                return;
            }

            ScenePresence sp = m_Scene.GetScenePresence(m_AgentID);
            if (sp == null || sp.IsDeleted)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                httpResponse.AddHeader("Retry-After", "60");
                return;
            }

            OSDMap req;
            OSD tmp;
            try
            {
                req = (OSDMap)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            try
            {
                while (true) // kinda goto
                {
                    if (!req.TryGetValue("folder_id", out tmp) || !(tmp is OSDUUID))
                        break;
                    UUID folderID = tmp.AsUUID();

                    if(folderID == UUID.Zero)
                        break;

                    if (!req.TryGetValue("parent_id", out tmp) || !(tmp is OSDUUID))
                        break;
                    UUID parentID = tmp.AsUUID();

                    if (!req.TryGetValue("name", out tmp) || !(tmp is OSDString))
                        break;
                    string folderName = tmp.AsString();

                    if(string.IsNullOrEmpty(folderName))
                        break;

                    if(folderName.Length > 63)
                        folderName = folderName.Substring(0, 63);

                    if (!req.TryGetValue("type", out tmp) || !(tmp is OSDInteger))
                        break;
                    int folderType = tmp.AsInteger();

                    InventoryFolderBase folder = new InventoryFolderBase(folderID, folderName, m_AgentID, (short)folderType, parentID, 1);
                    if (!m_Scene.InventoryService.AddFolder(folder))
                        break;

                    // costly double check plus possible service changes
                    folder = m_Scene.InventoryService.GetFolder(m_AgentID, folderID);
                    if (folder == null)
                        break;

                    osUTF8 sb = LLSDxmlEncode2.Start();
                    LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("folder_id", folder.ID, sb);
                    LLSDxmlEncode2.AddElem("name", folder.Name, sb);
                    LLSDxmlEncode2.AddElem("parent_id", folder.ParentID, sb);
                    LLSDxmlEncode2.AddElem("type", folder.Type, sb);
                    LLSDxmlEncode2.AddEndMap(sb);

                    httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(sb);
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
            }
            catch { }

            m_log.Debug("[CAPS]: CreateInventoryCategory failed to process request");
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
        }

        /// <summary>
        /// Called by the CopyInventoryFromNotecard caps handler.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>

        public void CopyInventoryFromNotecard(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap content)
        {
            InventoryItemBase copyItem = null;
            IClientAPI client = null;

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            try
            {
                UUID objectID = content["object-id"].AsUUID();
                UUID notecardID = content["notecard-id"].AsUUID();
                UUID folderID = content["folder-id"].AsUUID();
                UUID itemID = content["item-id"].AsUUID();

                //  m_log.InfoFormat("[CAPS]: CopyInventoryFromNotecard, FolderID:{0}, ItemID:{1}, NotecardID:{2}, ObjectID:{3}", folderID, itemID, notecardID, objectID);

                UUID noteAssetID = UUID.Zero;
                UUID agentID = m_HostCapsObj.AgentID;

                m_Scene.TryGetClient(agentID, out client);

                if (!objectID.IsZero())
                {
                    SceneObjectPart part = m_Scene.GetSceneObjectPart(objectID);
                    if(part == null)
                        throw new Exception("failed to find object with notecard item" + notecardID.ToString());

                    TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(notecardID);
                    if (taskItem == null || taskItem.AssetID.IsZero())
                        throw new Exception("Failed to find notecard item" + notecardID.ToString());

                    if (!m_Scene.Permissions.CanCopyObjectInventory(notecardID, objectID, agentID))
                        throw new Exception("No permission to copy notecard from object");

                    noteAssetID = taskItem.AssetID;
                }
                else
                {
                    // we may have the item around...
                    InventoryItemBase localitem = m_Scene.InventoryService.GetItem(agentID, itemID);
                    if (localitem != null)
                    {
                        string message;
                        copyItem = m_Scene.GiveInventoryItem(agentID, localitem.Owner, itemID, folderID, out message);
                        if (copyItem == null)
                            throw new Exception("Failed to find notecard item" + notecardID.ToString());

                        m_log.InfoFormat("[CAPS]: CopyInventoryFromNotecard, ItemID:{0}, FolderID:{1}", copyItem.ID, copyItem.Folder);
                        if (client != null)
                            client.SendBulkUpdateInventory(copyItem);
                        return;
                    }

                    if (!notecardID.IsZero())
                    {
                        InventoryItemBase noteItem = m_Scene.InventoryService.GetItem(agentID, notecardID);
                        if (noteItem == null || noteItem.AssetID.IsZero())
                            throw new Exception("Failed to find notecard item" + notecardID.ToString());
                        noteAssetID = noteItem.AssetID;
                    }
                }

                AssetBase noteAsset = m_Scene.AssetService.Get(noteAssetID.ToString());
                if (noteAsset == null || noteAsset.Type != (sbyte)AssetType.Notecard)
                    throw new Exception("Failed to find the notecard asset" + notecardID.ToString());

                InventoryItemBase item = SLUtil.GetEmbeddedItem(noteAsset.Data, itemID);
                if(item == null)
                    throw new Exception("Failed to find the notecard item" + notecardID.ToString());

                if (!m_Scene.Permissions.CanTransferUserInventory(itemID, item.Owner, agentID))
                    throw new Exception("Notecard item permissions check fail" + notecardID.ToString());

                if (!m_Scene.Permissions.BypassPermissions())
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                        throw new Exception("Notecard item permissions check fail" + notecardID.ToString());
                }

                // check if we do have the item asset
                noteAsset = m_Scene.AssetService.Get(item.AssetID.ToString());
                if (noteAsset == null)
                    throw new Exception("Failed to find the notecard " + notecardID.ToString() +" item asset");

                // find where to put it
                InventoryFolderBase folder = null;
                if (!folderID.IsZero())
                    folder = m_Scene.InventoryService.GetFolder(agentID, folderID);

                if (folder == null && Enum.IsDefined(typeof(FolderType), (sbyte)item.AssetType))
                    folder = m_Scene.InventoryService.GetFolderForType(agentID, (FolderType)item.AssetType);

                if (folder == null)
                    folder = m_Scene.InventoryService.GetRootFolder(agentID);

                if (folder == null)
                    throw new Exception("Failed to find a folder for the notecard item" + notecardID.ToString());

                item.Folder = folder.ID;

                // do change owner permissions (c&p from scene inventory code)
                if (m_Scene.Permissions.PropagatePermissions() && item.Owner != agentID)
                {
                    uint permsMask = ~((uint)PermissionMask.Copy |
                                        (uint)PermissionMask.Transfer |
                                        (uint)PermissionMask.Modify |
                                        (uint)PermissionMask.Export);

                    uint nextPerms = permsMask | (item.NextPermissions &
                                        ((uint)PermissionMask.Copy |
                                        (uint)PermissionMask.Transfer |
                                        (uint)PermissionMask.Modify));

                    if (nextPerms == permsMask)
                        nextPerms |= (uint)PermissionMask.Transfer;

                    uint basePerms = item.BasePermissions | (uint)PermissionMask.Move;
                    uint ownerPerms = item.CurrentPermissions;

                    uint foldedPerms = (item.CurrentPermissions & (uint)PermissionMask.FoldedMask) << (int)PermissionMask.FoldingShift;
                    if (foldedPerms != 0 && item.InvType == (int)InventoryType.Object)
                    {
                        foldedPerms |= permsMask;

                        bool isRootMod = (item.CurrentPermissions &
                                            (uint)PermissionMask.Modify) != 0 ?
                                            true : false;

                        ownerPerms &= foldedPerms;
                        basePerms &= foldedPerms;

                        if (isRootMod)
                        {
                            ownerPerms |= (uint)PermissionMask.Modify;
                            basePerms |= (uint)PermissionMask.Modify;
                        }
                    }

                    ownerPerms &= nextPerms;
                    basePerms &= nextPerms;
                    basePerms &= ~(uint)PermissionMask.FoldedMask;
                    basePerms |= ((basePerms >> 13) & 7) | (((basePerms & (uint)PermissionMask.Export) != 0) ? (uint)PermissionMask.FoldedExport : 0);
                    item.BasePermissions = basePerms;
                    item.CurrentPermissions = ownerPerms;
                    item.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                    item.Flags &= ~(uint)(InventoryItemFlags.ObjectOverwriteBase | InventoryItemFlags.ObjectOverwriteOwner | InventoryItemFlags.ObjectOverwriteGroup | InventoryItemFlags.ObjectOverwriteEveryone | InventoryItemFlags.ObjectOverwriteNextOwner);
                    item.NextPermissions = item.NextPermissions;
                    item.EveryOnePermissions = item.EveryOnePermissions & nextPerms;
                }
                else
                {
                    //??
                    item.EveryOnePermissions &= item.NextPermissions;
                }

                item.GroupPermissions = 0; // we killed the group
                item.Owner = agentID;

                if (!m_Scene.InventoryService.AddItem(item))
                    throw new Exception("Failed create the notecard item" + notecardID.ToString());

                m_log.InfoFormat("[CAPS]: CopyInventoryFromNotecard, ItemID:{0} FolderID:{1}", item.ID, item.Folder);
                if (client != null)
                    client.SendBulkUpdateInventory(item);
                return;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[CAPS]: CopyInventoryFromNotecard : {0}", e.Message);
                copyItem = null;
            }

            if(copyItem == null)
            {
                if (client != null)
                    client.SendAlertMessage("Failed to retrieve item");
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        public void GetObjectPhysicsData(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap req)
        {
            OSDArray object_ids;
            try
            {
                object_ids = (OSDArray)req["object_ids"];
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            osUTF8 lsl = LLSDxmlEncode2.Start();
            
            if(object_ids.Count == 0)
                LLSDxmlEncode2.AddEmptyMap(lsl);
            else
            {
                LLSDxmlEncode2.AddMap(lsl);
                for (int i = 0 ; i < object_ids.Count ; i++)
                {
                    UUID uuid = object_ids[i].AsUUID();

                    SceneObjectPart obj = m_Scene.GetSceneObjectPart(uuid);
                    if (obj != null)
                    {                  
                        LLSDxmlEncode2.AddMap(uuid.ToString(),lsl);

                        LLSDxmlEncode2.AddElem("PhysicsShapeType", obj.PhysicsShapeType, lsl);
                        LLSDxmlEncode2.AddElem("Density", obj.Density, lsl);
                        LLSDxmlEncode2.AddElem("Friction", obj.Friction, lsl);
                        LLSDxmlEncode2.AddElem("Restitution", obj.Restitution, lsl);
                        LLSDxmlEncode2.AddElem("GravityMultiplier", obj.GravityModifier, lsl);

                        LLSDxmlEncode2.AddEndMap(lsl);
                    }
                LLSDxmlEncode2.AddEndMap(lsl);
                }
            }

            httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(lsl);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public void GetObjectCost(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap req)
        {
            OSDArray object_ids;
            try
            {
                object_ids = (OSDArray)req["object_ids"];
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            osUTF8 lsl = LLSDxmlEncode2.Start(512);
            
            if(object_ids.Count == 0)
                LLSDxmlEncode2.AddEmptyMap(lsl);
            else
            {
                bool haveone = false;
                LLSDxmlEncode2.AddMap(lsl);
                for (int i = 0; i < object_ids.Count; i++)
                {
                    UUID uuid = object_ids[i].AsUUID();

                    SceneObjectPart part = m_Scene.GetSceneObjectPart(uuid);
                    SceneObjectGroup grp = null;
                    if (part != null)
                        grp = part.ParentGroup;
                    if (grp != null)
                    {
                        haveone = true;

                        grp.GetResourcesCosts(part, out float linksetCost, out float linksetPhysCost, out float partCost, out float partPhysCost);

                        LLSDxmlEncode2.AddMap(uuid.ToString(), lsl);

                        LLSDxmlEncode2.AddElem("linked_set_resource_cost", linksetCost, lsl);
                        LLSDxmlEncode2.AddElem("resource_cost", partCost, lsl);
                        LLSDxmlEncode2.AddElem("physics_cost", partPhysCost, lsl);
                        LLSDxmlEncode2.AddElem("linked_set_physics_cost", linksetPhysCost, lsl);
                        LLSDxmlEncode2.AddElem("resource_limiting_type", "legacy", lsl);

                        LLSDxmlEncode2.AddEndMap(lsl);
                    }
                }
                if(!haveone)
                {
                    LLSDxmlEncode2.AddMap(UUID.Zero.ToString(), lsl);
                    LLSDxmlEncode2.AddElem("linked_set_resource_cost", 0, lsl);
                    LLSDxmlEncode2.AddElem("resource_cost", 0, lsl);
                    LLSDxmlEncode2.AddElem("physics_cost", 0, lsl);
                    LLSDxmlEncode2.AddElem("linked_set_physics_cost", 0, lsl);
                    LLSDxmlEncode2.AddElem("resource_limiting_type", "legacy", lsl);
                    LLSDxmlEncode2.AddEndMap(lsl);
                }
                LLSDxmlEncode2.AddEndMap(lsl);
            }

            httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(lsl);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public struct AttachmentScriptInfo
        {
            public UUID id;
            public string name;
            public Vector3 pos;
            public int memory;
            public int urls;
        };

        public void AttachmentResources(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if(m_Scene.TryGetScenePresence(m_AgentID, out ScenePresence sp) && !sp.IsChildAgent && !sp.IsDeleted && !sp.IsInTransit)
            {
                int totalmem = 0;
                int totalurls = 0;
                List<SceneObjectGroup> atts = sp.GetAttachments();
                Dictionary<byte, List<AttachmentScriptInfo>> perAttPoints = null;
                if (atts.Count > 0)
                {
                    IUrlModule urlModule = m_Scene.RequestModuleInterface<IUrlModule>();
                    perAttPoints = new Dictionary<byte, List<AttachmentScriptInfo>>();
                    foreach (SceneObjectGroup so in atts)
                    {
                        byte attp = so.GetAttachmentPoint();
                        if(!so.ScriptsMemory(out int mem))
                            continue;
                        int urls_used = 0;
                        totalmem += mem;
                        if (urlModule != null)
                        {
                            urls_used = urlModule.GetUrlCount(so.UUID);
                            totalurls += urls_used;
                        }
                        AttachmentScriptInfo info = new AttachmentScriptInfo()
                        {
                            id = so.UUID,
                            name = so.Name,
                            memory = mem,
                            urls = urls_used,
                            pos = so.AbsolutePosition
                        };
                        if(perAttPoints.TryGetValue(attp, out List<AttachmentScriptInfo> la))
                            la.Add(info);
                        else
                            perAttPoints[attp] = new List<AttachmentScriptInfo>(){  info };
                    }
                }
                osUTF8 sb = LLSDxmlEncode2.Start();
                LLSDxmlEncode2.AddMap(sb);

                if (atts.Count > 0)
                {
                    LLSDxmlEncode2.AddArray("attachments", sb);
                    foreach (KeyValuePair<byte, List<AttachmentScriptInfo>> kvp in perAttPoints)
                    {
                        LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("location", SLUtil.GetAttachmentName(kvp.Key), sb);
                        LLSDxmlEncode2.AddArray("objects", sb);
                        foreach(AttachmentScriptInfo asi in kvp.Value)
                        {
                            LLSDxmlEncode2.AddMap(sb);
                            LLSDxmlEncode2.AddElem("id", asi.id, sb);
                            LLSDxmlEncode2.AddElem("is_group_owned", (int)0, sb);
                            LLSDxmlEncode2.AddElem("location", asi.pos, sb);
                            LLSDxmlEncode2.AddElem("name", asi.name, sb);
                            LLSDxmlEncode2.AddElem("owner_id", m_AgentID, sb);
                            LLSDxmlEncode2.AddMap("resources", sb);
                            if (asi.memory > 0)
                                LLSDxmlEncode2.AddElem("memory", asi.memory, sb);
                            if (asi.urls > 0)
                                LLSDxmlEncode2.AddElem("urls", asi.urls, sb);
                            LLSDxmlEncode2.AddEndMap(sb);
                            LLSDxmlEncode2.AddEndMap(sb);
                        }
                        LLSDxmlEncode2.AddEndArray(sb);
                        LLSDxmlEncode2.AddEndMap(sb);
                    }
                    LLSDxmlEncode2.AddEndArray(sb); //attachments
                }
                else
                    LLSDxmlEncode2.AddEmptyArray("attachments", sb);

                LLSDxmlEncode2.AddMap("summary", sb);
                LLSDxmlEncode2.AddArray("available", sb);

                int maxurls = totalurls <= 38? 38: totalurls; // we don't limit this
                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", maxurls, sb);
                LLSDxmlEncode2.AddElem("type", "urls", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", (int)-1, sb);
                LLSDxmlEncode2.AddElem("type", "memory", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddEndArray(sb); //available

                LLSDxmlEncode2.AddArray("used", sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount",totalurls, sb);
                LLSDxmlEncode2.AddElem("type", "urls", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", totalmem, sb);
                LLSDxmlEncode2.AddElem("type", "memory", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddEndArray(sb); //used

                LLSDxmlEncode2.AddEndMap(sb); // summary

                LLSDxmlEncode2.AddEndMap(sb);

                httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(sb);
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                return;
            }
            httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
        }

        public class ScriptInfoForParcel
        {
            public UUID id;
            public UUID owner;
            public string name;
            public int memory;
            public int urls;
            public bool groupOwned;
            public Vector3 pos;
        };

        public class ParcelScriptInfo
        {
            public UUID id;
            public string name;
            public int localID;
            public List<ScriptInfoForParcel> objects;
        };

        public void LandResources(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap req)
        {
            if (!m_Scene.TryGetScenePresence(m_AgentID, out ScenePresence sp))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            UUID parcelOwner;
            LandData landdata = null;
            ulong myHandler = m_Scene.RegionInfo.RegionHandle;
            if (req.TryGetValue("parcel_id", out OSD tmp) && tmp is OSDUUID)
            {
                UUID parcelID = tmp.AsUUID();
                if (Util.ParseFakeParcelID(parcelID, out ulong regionHandle, out uint x, out uint y) && regionHandle == myHandler)
                {
                    ILandObject land = m_Scene.LandChannel.GetLandObjectClippedXY(x, y);
                    if (land != null)
                        landdata = land.LandData;
                    land = null;
                }
            }
            if(landdata == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            parcelOwner = landdata.OwnerID;

            int showType = 0;
            if (sp.IsGod || m_Scene.Permissions.IsEstateManager(m_AgentID))
                showType = 1;
            else
            {
                if (parcelOwner == m_AgentID)
                    showType = 2;
                else if (!landdata.GroupID.IsZero())
                {
                    ulong powers = sp.ControllingClient.GetGroupPowers(landdata.GroupID);
                    if ((powers & (ulong)(GroupPowers.ReturnGroupOwned | GroupPowers.ReturnGroupSet | GroupPowers.ReturnNonGroup)) != 0)
                        showType = 2;
                }
            }
            landdata = null;

            int totalmem = 0;
            int totalurls = 0;
            bool ownerparcels = showType != 1;
            bool showdetail = showType != 0;

            List<ParcelScriptInfo> parcelsInfo = null;
            IUrlModule urlModule = m_Scene.RequestModuleInterface<IUrlModule>();

            List<ILandObject>  allParcels = m_Scene.LandChannel.AllParcels();
            if (showdetail)
                parcelsInfo = new List<ParcelScriptInfo>(allParcels.Count);

            for (int p = 0; p < allParcels.Count; ++p)
            {
                ILandObject parcel = allParcels[p];
                landdata = parcel.LandData;
                if (landdata == null)
                    continue;
                if(ownerparcels && landdata.OwnerID != parcelOwner)
                    continue;

                ParcelScriptInfo pi = null;
                if (showdetail)
                {
                    pi = new ParcelScriptInfo
                    {
                        name = landdata.Name,
                        localID = landdata.LocalID,
                        id = landdata.FakeID,
                        objects = new List<ScriptInfoForParcel>()
                    };
                }

                ISceneObject[] isops = parcel.GetSceneObjectGroups();
                for(int i = 0; i < isops.Length; ++i)
                {
                    SceneObjectGroup so = isops[i] as SceneObjectGroup;
                    if(so == null || so.IsDeleted || so.inTransit || so.IsAttachment)
                        continue;

                    if(!so.ScriptsMemory(out int mem))
                        continue;

                    int urls_used = 0;
                    totalmem += mem;
                    if (urlModule != null)
                    {
                        urls_used = urlModule.GetUrlCount(so.UUID);
                        totalurls += urls_used;
                    }

                    if (showdetail)
                    {
                        ScriptInfoForParcel sip = new ScriptInfoForParcel()
                        {
                            id = so.UUID,
                            owner = so.OwnerID,
                            name = so.Name,
                            memory = mem,
                            urls = urls_used,
                            groupOwned = (so.OwnerID == so.GroupID),
                            pos = so.AbsolutePosition
                        };
                        pi.objects.Add(sip);
                    }
                }
                if (showdetail)
                    parcelsInfo.Add(pi);
            }
            landdata = null;

            osUTF8 lsl = LLSDxmlEncode2.Start();
            LLSDxmlEncode2.AddMap(lsl);

            string baseurl = m_HostCapsObj.SSLCaps ? "https://" : "http://";
            baseurl += m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString();

            string SRSPath = GetNewCapPath();
            ScriptResourceSummary srs =
                new ScriptResourceSummary(m_Scene, m_AgentID, m_HostCapsObj.HttpListener, SRSPath, httpRequest.RemoteIPEndPoint.Address,
                    totalmem, totalurls);
            m_HostCapsObj.HttpListener.AddSimpleStreamHandler(new SimpleStreamHandler(SRSPath, srs.ScriptResourceSummaryCap));
            string SRSURL = baseurl + SRSPath;
            LLSDxmlEncode2.AddElem("ScriptResourceSummary", SRSURL, lsl);

            if(showdetail)
            {
                string SRDPath = GetNewCapPath();
                string SRDURL = baseurl + SRDPath;
                ScriptResourceDetails srd =
                    new ScriptResourceDetails(m_Scene, m_AgentID, m_HostCapsObj.HttpListener, SRDPath, httpRequest.RemoteIPEndPoint.Address,
                    parcelsInfo);
                m_HostCapsObj.HttpListener.AddSimpleStreamHandler(new SimpleStreamHandler(SRDPath, srd.ScriptResourceDetailsCap));
                LLSDxmlEncode2.AddElem("ScriptResourceDetails", SRDURL, lsl);
            }

            LLSDxmlEncode2.AddEndMap(lsl);
            httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(lsl);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            return;
        }

        public void ResourceCostSelected(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap req)
        {
            float phys=0;
            float stream=0;
            float simul=0;

            if (req.ContainsKey("selected_roots"))
            {
                OSDArray object_ids = (OSDArray)req["selected_roots"];

                // should go by SOG suming costs for all parts
                // ll v3 works ok with several objects select we get the list and adds ok
                // FS calls per object so results are wrong guess fs bug
                for (int i = 0; i < object_ids.Count; i++)
                {
                    UUID uuid = object_ids[i].AsUUID();
                    float Physc;
                    float simulc;
                    float streamc;

                    SceneObjectGroup grp = m_Scene.GetGroupByPrim(uuid);
                    if (grp != null)
                    {
                        grp.GetSelectedCosts(out Physc, out streamc, out simulc);
                        phys += Physc;
                        stream += streamc;
                        simul += simulc;
                    }
                }
            }
            else if (req.ContainsKey("selected_prims"))
            {
                OSDArray object_ids = (OSDArray)req["selected_prims"];

                // don't see in use in any of the 2 viewers
                // guess it should be for edit linked but... nothing
                // should go to SOP per part
                for (int i = 0; i < object_ids.Count; i++)
                {
                    UUID uuid = object_ids[i].AsUUID();

                    SceneObjectPart part = m_Scene.GetSceneObjectPart(uuid);
                    if (part != null)
                    {
                        phys += part.PhysicsCost;
                        stream += part.StreamingCost;
                        simul += part.SimulationCost;
                    }
                }
            }

            osUTF8 lsl = LLSDxmlEncode2.Start();
            LLSDxmlEncode2.AddMap(lsl);

            LLSDxmlEncode2.AddMap("selected", lsl);

            LLSDxmlEncode2.AddElem("physics", phys, lsl);
            LLSDxmlEncode2.AddElem("streaming", stream, lsl);
            LLSDxmlEncode2.AddElem("simulation", simul, lsl);

            LLSDxmlEncode2.AddEndMap(lsl);
            LLSDxmlEncode2.AddEndMap(lsl);

            // resp["transaction_id"] = "undef";
            httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(lsl);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public bool OSDMapTOVector3(OSDMap map, out Vector3 v)
        {
            v = Vector3.Zero;
            if(!map.ContainsKey("X"))
                return false;
            if(!map.ContainsKey("Y"))
                return false;
            if(!map.ContainsKey("Z"))
                return false;
            v.X = (float)map["X"].AsReal();
            v.Y = (float)map["Y"].AsReal();
            v.Z = (float)map["Z"].AsReal();
            return true;
        }

        public void HomeLocation(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            bool fail = true;
            string message = "Set Home request failed";
            //int locationID = 1;
            Vector3 pos = Vector3.Zero;
            Vector3 lookAt = Vector3.Zero;

            IClientAPI client = null;
            ScenePresence sp;

            while(true)
            {
                if(m_Scene.GridUserService == null)
                    break;

                if(m_Scene.UserManagementModule == null)
                    break;

                m_Scene.TryGetScenePresence(m_AgentID, out sp);
                if(sp == null || sp.IsChildAgent || sp.IsDeleted)
                    break;

                if(sp.IsInTransit && !sp.IsInLocalTransit)
                    break;

                client = sp.ControllingClient;

                if(!m_Scene.UserManagementModule.IsLocalGridUser(m_AgentID))
                    break;

                OSDMap req;
                try
                {
                    req = (OSDMap)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
                }
                catch
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                OSD tmp;
                if (!req.TryGetValue("HomeLocation", out tmp) || !(tmp is OSDMap))
                    break;

                OSDMap HLocation = (OSDMap)tmp;

                if(!HLocation.TryGetValue("LocationPos", out tmp) || !(tmp is OSDMap))
                    break;
                if (!OSDMapTOVector3((OSDMap)tmp, out pos))
                    break;

                if (!HLocation.TryGetValue("LocationLookAt", out tmp) || !(tmp is OSDMap))
                    break;
                if (!OSDMapTOVector3((OSDMap)tmp, out lookAt))
                    break;

                //locationID = HLocation["LocationId"].AsInteger();

                ILandObject land = m_Scene.LandChannel.GetLandObject(pos);
                if(land == null)
                    break;

                ulong gpowers = client.GetGroupPowers(land.LandData.GroupID);
                SceneObjectGroup telehub = null;
                if (!m_Scene.RegionInfo.RegionSettings.TelehubObject.IsZero())
                // Does the telehub exist in the scene?
                    telehub = m_Scene.GetSceneObjectGroup(m_Scene.RegionInfo.RegionSettings.TelehubObject);

                if (!m_Scene.Permissions.IsAdministrator(m_AgentID) && // (a) gods and land managers can set home
                    !m_Scene.Permissions.IsGod(m_AgentID) &&
                    m_AgentID != land.LandData.OwnerID && // (b) land owners can set home
                    // (c) members of the land-associated group in roles that can set home
                    ((gpowers & (ulong)GroupPowers.AllowSetHome) != (ulong)GroupPowers.AllowSetHome) &&
                    // (d) parcels with telehubs can be the home of anyone
                    (telehub == null || !land.ContainsPoint((int)telehub.AbsolutePosition.X, (int)telehub.AbsolutePosition.Y)))
                {
                    message = "You are not allowed to set your home location in this parcel.";
                    break;
                }

                string userId;
                UUID test;
                if (!m_Scene.UserManagementModule.GetUserUUI(m_AgentID, out userId))
                {
                    message = "Set Home request failed. (User Lookup)";
                    break;
                }

                if (!UUID.TryParse(userId, out test))
                {
                    message = "Set Home request failed. (HG visitor)";
                    break;
                }

                if (m_Scene.GridUserService.SetHome(userId, land.RegionUUID, pos, lookAt))
                    fail = false;

                break;
            }

            OSDMap resp = new OSDMap();

            if(fail)
            {
                if(client != null)
                    client.SendAlertMessage(message);
                resp["success"] = "false";
            }
            else
            {
                // so its http but still needs a udp reply to inform user? crap :p
                if(client != null)
                   client.SendAlertMessage("Home position set.","HomePositionSet");

                resp["success"] = "true";
                OSDMap homeloc = new OSDMap();
                OSDMap homelocpos = new OSDMap();
                // for some odd reason viewers send pos as reals but read as integer
                homelocpos["X"] = new OSDReal(pos.X);
                homelocpos["Y"] = new OSDReal(pos.Y);
                homelocpos["Z"] = new OSDReal(pos.Z);
                homeloc["LocationPos"] = homelocpos;

                resp["HomeLocation"] = homeloc;
            }

            httpResponse.RawBuffer = Util.UTF8NBGetbytes(OSDParser.SerializeLLSDXmlString(resp));
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        private static int CompareRolesByMembersDesc(GroupRolesData x, GroupRolesData y)
        {
            return -(x.Members.CompareTo(y.Members));
        }

        public void GroupMemberData(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            OSDMap resp;
            UUID groupID = UUID.Zero;

            while(true)
            {
                IGroupsModule m_GroupsModule = m_Scene.RequestModuleInterface<IGroupsModule>();
                if(m_GroupsModule == null)
                    break;

                m_Scene.TryGetScenePresence(m_AgentID, out ScenePresence sp);
                if(sp == null || sp.IsChildAgent || sp.IsDeleted)
                    break;
                
                if(sp.IsInTransit && !sp.IsInLocalTransit)
                    break;

                IClientAPI client = sp.ControllingClient;

                OSDMap req;
                try
                {
                    req = (OSDMap)OSDParser.DeserializeLLSDXml(httpRequest.InputStream);
                }
                catch
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                OSD tmp;
                if(!req.TryGetValue("group_id", out tmp) || tmp is not OSDUUID)
                    break;

                groupID = tmp.AsUUID();
                if(groupID.IsZero())
                    groupID = sp.ControllingClient.ActiveGroupId;

                if(groupID.IsZero())
                    break;

                List<GroupRolesData> roles = m_GroupsModule.GroupRoleDataRequest(client, groupID);
                if(roles == null || roles.Count == 0)
                    break;

                List<GroupMembersData> members = m_GroupsModule.GroupMembersRequest(client, groupID);
                if(members == null || members.Count == 0)
                    break;

                int memberCount = members.Count;

                Dictionary<string,int> titles = [];
                Dictionary<ulong,int> powers = [];

                // build titles array and index
                roles.Sort(CompareRolesByMembersDesc);

                int i = 0;
                OSDArray osdtitles = [];
                foreach(GroupRolesData grd in roles)
                {
                    ref int powerentry = ref CollectionsMarshal.GetValueRefOrAddDefault(powers, grd.Powers, out bool _);
                    powerentry++;

                    if(grd.Title == null)
                    {
                        if(!titles.ContainsKey(string.Empty))
                        {
                            titles[string.Empty] = i++;
                            osdtitles.Add(new OSDString(string.Empty));
                        }
                    }
                    else if(!titles.ContainsKey(grd.Title))
                    {
                        titles[grd.Title] = i++;
                        osdtitles.Add(new OSDString(grd.Title));
                    }
                }

                if(titles.Count == 0)
                    break;

                ulong defaultPowers = 0;
                int maxPowers = -1;
                foreach(KeyValuePair<ulong,int> kvp in powers)
                {
                    if (kvp.Value > maxPowers)
                    {
                        defaultPowers = kvp.Key;
                        maxPowers = kvp.Value;
                    }
                }

                OSDMap osdmembers = [];
                foreach(GroupMembersData gmd in members)
                {
                    OSDMap m = [];
                    if(gmd.OnlineStatus != null && gmd.OnlineStatus != "")
                        m["last_login"] = new OSDString(gmd.OnlineStatus);
                    if(gmd.AgentPowers != defaultPowers)
                        m["powers"] = new OSDString((gmd.AgentPowers).ToString("X"));
                    if(gmd.Title != null)
                    { 
                        if(titles.TryGetValue(gmd.Title, out int value) && value != 0)
                            m["title"] = new OSDInteger(value);
                    }
                    else if(titles.TryGetValue(string.Empty, out int ovalue) && ovalue != 0)
                        m["title"] = new OSDInteger(ovalue);
                    if(gmd.IsOwner)
                        m["owner"] = new OSDString("true");
                    if(gmd.Contribution != 0)
                        m["donated_square_meters"] = new OSDInteger(gmd.Contribution);

                    osdmembers[(gmd.AgentID).ToString()] = m;
                }

                OSDMap osddefaults = new()
                {
                    ["default_powers"] = new OSDString(defaultPowers.ToString("X"))
                };

                 resp = new OSDMap
                {
                    ["group_id"] = new OSDUUID(groupID),
                    ["agent_id"] = new OSDUUID(m_AgentID),
                    ["member_count"] = new OSDInteger(memberCount),
                    ["defaults"] = osddefaults,
                    ["titles"] = osdtitles,
                    ["members"] = osdmembers
                };

                httpResponse.RawBuffer = Util.UTF8NBGetbytes(OSDParser.SerializeLLSDXmlString(resp));
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
                return;
            }

            resp = new()
            {
                ["group_id"] = new OSDUUID(groupID),
                ["agent_id"] = new OSDUUID(m_AgentID),
                ["member_count"] = new OSDInteger(0),
                ["defaults"] = new OSDMap(),
                ["titles"] = new OSDArray(),
                ["members"] = new OSDMap()
            };
            httpResponse.RawBuffer = Util.UTF8NBGetbytes(OSDParser.SerializeLLSDXmlString(resp));
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public void GetDisplayNames(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (httpRequest.HttpMethod != "GET")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            ScenePresence sp = m_Scene.GetScenePresence(m_AgentID);
            if(sp == null || sp.IsDeleted)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Gone;
                return;
            }
            if(sp.IsInTransit && !sp.IsInLocalTransit)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                httpResponse.AddHeader("Retry-After","30");
                return;
            }

            // Full content request
            NameValueCollection query = httpRequest.QueryString;
            string[] ids = query.GetValues("ids");

            osUTF8 lsl;
            if(ids.Length == 0)
            {
                lsl = LLSDxmlEncode2.Start();
                LLSDxmlEncode2.AddMap(lsl);
                LLSDxmlEncode2.AddEmptyArray("agents", lsl);
            }
            else
            {
                List<UserData> names = m_UserManager.GetKnownUsers(ids, m_scopeID);
                lsl = LLSDxmlEncode2.Start(names.Count * 256 + 256);

                LLSDxmlEncode2.AddMap(lsl);
                if (names.Count == 0)
                    LLSDxmlEncode2.AddEmptyArray("agents", lsl);
                else
                {
                    LLSDxmlEncode2.AddArray("agents", lsl);

                    foreach (UserData ud in names)
                    {
                        // dont tell about unknown users, we can't send them back on Bad either
                        if (string.IsNullOrEmpty(ud.FirstName) || ud.FirstName.Equals("Unkown"))
                            continue;

                        string fullname = ud.FirstName + " " + ud.LastName;
                        LLSDxmlEncode2.AddMap(lsl);
                        LLSDxmlEncode2.AddElem("username", fullname, lsl);
                        LLSDxmlEncode2.AddElem("display_name", fullname, lsl);
                        LLSDxmlEncode2.AddElem("display_name_next_update", DateTime.UtcNow.AddDays(8), lsl);
                        LLSDxmlEncode2.AddElem("display_name_expires", DateTime.UtcNow.AddMonths(1), lsl);
                        LLSDxmlEncode2.AddElem("legacy_first_name", ud.FirstName, lsl);
                        LLSDxmlEncode2.AddElem("legacy_last_name", ud.LastName, lsl);
                        LLSDxmlEncode2.AddElem("id", ud.Id, lsl);
                        LLSDxmlEncode2.AddElem("is_display_name_default", true, lsl);
                        LLSDxmlEncode2.AddEndMap(lsl);
                    }
                    LLSDxmlEncode2.AddEndArray(lsl);
                }
            }
            LLSDxmlEncode2.AddEndMap(lsl);

            httpResponse.RawBuffer = LLSDxmlEncode2.EndToNBBytes(lsl);
            httpResponse.ContentType = "application/llsd+xml";
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        public class AssetUploader
        {
            private static readonly ILog m_log =
                LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            private int m_cost;
            private string m_error = String.Empty;

            private System.Timers.Timer m_timeoutTimer;
            private UUID m_texturesFolder;
            private int m_nreqtextures;
            private int m_nreqmeshs;
            private int m_nreqinstances;
            private bool m_IsAtestUpload;

            private int m_nextOwnerMask;
            private int m_groupMask;
            private int m_everyoneMask;
            private int[] m_meshesSides;

            public AssetUploader(string assetName, string description, UUID assetID, UUID inventoryItem,
                                    UUID parentFolderID, string invType, string assetType, string path,
                                    IHttpServer httpServer, bool dumpAssetsToFile,
                                    int totalCost, UUID texturesFolder, int nreqtextures, int nreqmeshs, int nreqinstances,
                                    bool IsAtestUpload, int nextOwnerMask, int groupMask, int everyoneMask, int[] meshesSides)
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
                m_cost = totalCost;

                m_texturesFolder = texturesFolder;
                m_nreqtextures = nreqtextures;
                m_nreqmeshs = nreqmeshs;
                m_nreqinstances = nreqinstances;
                m_IsAtestUpload = IsAtestUpload;

                m_timeoutTimer = new System.Timers.Timer();
                m_timeoutTimer.Elapsed += TimedOut;
                m_timeoutTimer.Interval = 120000;
                m_timeoutTimer.AutoReset = false;
                m_timeoutTimer.Start();

                m_nextOwnerMask = nextOwnerMask;
                m_groupMask = groupMask;
                m_everyoneMask = everyoneMask;

                m_meshesSides = meshesSides;
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
    /*
                uploadComplete.new_asset = newAssetID.ToString();
                uploadComplete.new_inventory_item = inv;
                uploadComplete.state = "complete";

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
    */
                m_timeoutTimer.Stop();
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
                    handlerUpLoad(m_assetName, m_assetDes, newAssetID, inv, parentFolder, data, m_invType, m_assetType,
                        m_cost, m_texturesFolder, m_nreqtextures, m_nreqmeshs, m_nreqinstances, m_IsAtestUpload,
                        ref m_error, ref m_nextOwnerMask, ref m_groupMask, ref m_everyoneMask, m_meshesSides);
                }

                uploadComplete.new_next_owner_mask = m_nextOwnerMask;
                uploadComplete.new_group_mask = m_groupMask;
                uploadComplete.new_everyone_mask = m_everyoneMask;

                if (m_error == String.Empty)
                {
                    uploadComplete.new_asset = newAssetID.ToString();
                    uploadComplete.new_inventory_item = inv;
                    //                if (m_texturesFolder != UUID.Zero)
                    //                    uploadComplete.new_texture_folder_id = m_texturesFolder;
                   if (m_IsAtestUpload)
                   {
                      LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                      resperror.message = "Upload SUCCESSFUL for testing purposes only. Other uses are prohibited. Item will not work after 48 hours or on other regions";
                      resperror.identifier = inv;
                    
                      uploadComplete.error = resperror;
                   }
                   uploadComplete.state = "complete";
                }
                else
                {
                    LLSDAssetUploadError resperror = new LLSDAssetUploadError();
                    resperror.message = m_error;
                    resperror.identifier = inv;

                    uploadComplete.error = resperror;
                    uploadComplete.state = "failed";
                }

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
                return res;
            }

            private void TimedOut(object sender, ElapsedEventArgs args)
            {
                m_log.InfoFormat("[CAPS]: Removing URL and handler for timed out mesh upload");
                httpListener.RemoveStreamHandler("POST", uploaderPath);
            }

            private static void SaveAssetToFile(string filename, byte[] data)
            {
                string assetPath = "UserAssets";
                if (!Directory.Exists(assetPath))
                {
                    Directory.CreateDirectory(assetPath);
                }
                FileStream fs = File.Create(Path.Combine(assetPath, Util.SafeFileName(filename)));
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
        }

        public class ExpiringCapBase
        {
            protected IHttpServer m_httpListener;
            protected string m_mypath;
            protected Timer m_timeoutTimer;

            public ExpiringCapBase(IHttpServer httpServer, string path)
            {
                m_httpListener = httpServer;
                m_mypath = path;
            }

            public virtual void Start(int timeout)
            {
                m_timeoutTimer = new Timer(Timedout, null, timeout, Timeout.Infinite);
            }

            public virtual void Stop()
            {
                m_httpListener.RemoveSimpleStreamHandler(m_mypath);
                m_timeoutTimer.Dispose();
                m_timeoutTimer = null;
            }

            public virtual void Timedout(object state)
            {
                Stop();
                m_log.InfoFormat("[CAPS]: Removing URL and handler for timed out service");
            }
        }

        public class ScriptResourceSummary : ExpiringCapBase
        {
            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            private Scene m_scene;
            private UUID m_agentID;
            private int m_memory;
            private int m_urls;
            private IPAddress m_address;

            public ScriptResourceSummary(Scene scene, UUID agentID, IHttpServer httpServer, string path, IPAddress address, 
                int memory, int urls) : base(httpServer, path)
            {
                m_address = address;
                m_scene = scene;
                m_agentID = agentID;
                m_memory = memory;
                m_urls  = urls;

                Start(30000);
            }

            /// <summary>
            /// Handle raw asset upload data via the capability.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public void ScriptResourceSummaryCap(IOSHttpRequest request, IOSHttpResponse response)
            {
                Stop();

                if (!request.RemoteIPEndPoint.Address.Equals(m_address))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                if (m_scene.ShuttingDown || !m_scene.TryGetScenePresence(m_agentID, out ScenePresence sp) || sp.IsChildAgent || sp.IsInTransit)
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                osUTF8 sb = LLSDxmlEncode2.Start();
                LLSDxmlEncode2.AddMap(sb);

                LLSDxmlEncode2.AddMap("summary", sb);
                LLSDxmlEncode2.AddArray("available", sb);

                int maxurls = m_urls + 5000; // we don't limit this
                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", maxurls, sb);
                LLSDxmlEncode2.AddElem("type", "urls", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", (int)-1, sb);
                LLSDxmlEncode2.AddElem("type", "memory", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddEndArray(sb); //available

                LLSDxmlEncode2.AddArray("used", sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", m_urls, sb);
                LLSDxmlEncode2.AddElem("type", "urls", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddMap(sb);
                LLSDxmlEncode2.AddElem("amount", m_memory, sb);
                LLSDxmlEncode2.AddElem("type", "memory", sb);
                LLSDxmlEncode2.AddEndMap(sb);

                LLSDxmlEncode2.AddEndArray(sb); //used

                LLSDxmlEncode2.AddEndMap(sb); // summary
                LLSDxmlEncode2.AddEndMap(sb);
                response.RawBuffer = LLSDxmlEncode2.EndToNBBytes(sb);
                response.StatusCode = (int)HttpStatusCode.OK;
            }
        }

        public class ScriptResourceDetails : ExpiringCapBase
        {
            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            private Scene m_scene;
            private UUID m_agentID;
            private List<ParcelScriptInfo> m_parcelsInfo;
            private IPAddress m_address;

            public ScriptResourceDetails(Scene scene, UUID agentID, IHttpServer httpServer, string path, IPAddress address,
                List<ParcelScriptInfo> parcelsInfo) :base(httpServer, path)
            {
                m_address = address;
                m_scene = scene;
                m_agentID = agentID;
                m_parcelsInfo = parcelsInfo;

                Start(30000);
            }

            /// <summary>
            /// Handle raw asset upload data via the capability.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public void ScriptResourceDetailsCap(IOSHttpRequest request, IOSHttpResponse response)
            {
                Stop();

                if (!request.RemoteIPEndPoint.Address.Equals(m_address))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                if (m_scene.ShuttingDown || !m_scene.TryGetScenePresence(m_agentID, out ScenePresence sp) || sp.IsChildAgent || sp.IsInTransit)
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                osUTF8 sb = LLSDxmlEncode2.Start();
                LLSDxmlEncode2.AddMap(sb);

                if (m_parcelsInfo.Count > 0)
                {
                    LLSDxmlEncode2.AddArray("parcels", sb);

                    foreach (ParcelScriptInfo ps in m_parcelsInfo)
                    {
                        LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("name", ps.name, sb);
                        LLSDxmlEncode2.AddElem("id", ps.id, sb);
                        LLSDxmlEncode2.AddElem("local_id", ps.localID, sb);
                        if(ps.objects.Count > 0)
                        {
                            LLSDxmlEncode2.AddArray("objects", sb);
                            foreach (ScriptInfoForParcel sip in ps.objects)
                            {
                                LLSDxmlEncode2.AddMap(sb);
                                LLSDxmlEncode2.AddElem("id", sip.id, sb);
                                LLSDxmlEncode2.AddElem("is_group_owned", sip.groupOwned, sb);
                                LLSDxmlEncode2.AddElem("location", sip.pos, sb);
                                LLSDxmlEncode2.AddElem("name", sip.name, sb);
                                LLSDxmlEncode2.AddElem("owner_id", sip.owner, sb);

                                LLSDxmlEncode2.AddMap("resources", sb);
                                    LLSDxmlEncode2.AddElem("memory", sip.memory, sb);
                                    LLSDxmlEncode2.AddElem("urls", sip.urls, sb);
                                LLSDxmlEncode2.AddEndMap(sb);

                                LLSDxmlEncode2.AddEndMap(sb);
                            }
                            LLSDxmlEncode2.AddEndArray(sb);
                        }
                        else
                            LLSDxmlEncode2.AddEmptyArray("objects", sb);

                        LLSDxmlEncode2.AddEndMap(sb);
                    }
                    LLSDxmlEncode2.AddEndArray(sb); //parcels
                }
                else
                    LLSDxmlEncode2.AddEmptyArray("parcels", sb);

                LLSDxmlEncode2.AddEndMap(sb);
                response.RawBuffer = LLSDxmlEncode2.EndToNBBytes(sb);
                response.StatusCode = (int)HttpStatusCode.OK;
            }
        }
    }
}