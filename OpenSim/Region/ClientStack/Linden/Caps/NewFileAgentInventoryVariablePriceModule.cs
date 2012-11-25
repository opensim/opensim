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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using Mono.Addins;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Framework.Capabilities;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "NewFileAgentInventoryVariablePriceModule")]
    public class NewFileAgentInventoryVariablePriceModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private Scene m_scene;
//        private IAssetService m_assetService;
        private bool m_dumpAssetsToFile = false;
        private bool m_enabled = true;
        private int  m_levelUpload = 0;

        #region Region Module interfaceBase Members


        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig meshConfig = source.Configs["Mesh"];
            if (meshConfig == null)
                return;

            m_enabled = meshConfig.GetBoolean("AllowMeshUpload", true);
            m_levelUpload = meshConfig.GetInt("LevelUpload", 0);
        }

        public void AddRegion(Scene pScene)
        {
            m_scene = pScene;
        }

        public void RemoveRegion(Scene scene)
        {
            
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            
//            m_assetService = m_scene.RequestModuleInterface<IAssetService>();
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        #endregion


        #region Region Module interface

       

        public void Close() { }

        public string Name { get { return "NewFileAgentInventoryVariablePriceModule"; } }


        public void RegisterCaps(UUID agentID, Caps caps)
        {
            if(!m_enabled)
                return;

            UUID capID = UUID.Random();

//            m_log.Debug("[NEW FILE AGENT INVENTORY VARIABLE PRICE]: /CAPS/" + capID);
            caps.RegisterHandler(
                "NewFileAgentInventoryVariablePrice",
                new LLSDStreamhandler<LLSDAssetUploadRequest, LLSDNewFileAngentInventoryVariablePriceReplyResponse>(
                    "POST",
                    "/CAPS/" + capID.ToString(),
                    req => NewAgentInventoryRequest(req, agentID),
                    "NewFileAgentInventoryVariablePrice",
                    agentID.ToString()));         
        }

        #endregion

        public LLSDNewFileAngentInventoryVariablePriceReplyResponse NewAgentInventoryRequest(LLSDAssetUploadRequest llsdRequest, UUID agentID)
        {
            //TODO:  The Mesh uploader uploads many types of content. If you're going to implement a Money based limit
            // you need to be aware of this

            //if (llsdRequest.asset_type == "texture" ||
           //     llsdRequest.asset_type == "animation" ||
           //     llsdRequest.asset_type == "sound")
           // {
                // check user level

            ScenePresence avatar = null;
            IClientAPI client = null;
            m_scene.TryGetScenePresence(agentID, out avatar);

            if (avatar != null)
            {
                client = avatar.ControllingClient;

                if (avatar.UserLevel < m_levelUpload)
                {
                    if (client != null)
                        client.SendAgentAlertMessage("Unable to upload asset. Insufficient permissions.", false);

                    LLSDNewFileAngentInventoryVariablePriceReplyResponse errorResponse = new LLSDNewFileAngentInventoryVariablePriceReplyResponse();
                    errorResponse.rsvp = "";
                    errorResponse.state = "error";
                    return errorResponse;
                }
            }

            // check funds
            IMoneyModule mm = m_scene.RequestModuleInterface<IMoneyModule>();

            if (mm != null)
            {
                if (!mm.UploadCovered(agentID, mm.UploadCharge))
                {
                    if (client != null)
                        client.SendAgentAlertMessage("Unable to upload asset. Insufficient funds.", false);

                    LLSDNewFileAngentInventoryVariablePriceReplyResponse errorResponse = new LLSDNewFileAngentInventoryVariablePriceReplyResponse();
                    errorResponse.rsvp = "";
                    errorResponse.state = "error";
                    return errorResponse;
                }
            }

           // }

            string assetName = llsdRequest.name;
            string assetDes = llsdRequest.description;
            string capsBase = "/CAPS/NewFileAgentInventoryVariablePrice/";
            UUID newAsset = UUID.Random();
            UUID newInvItem = UUID.Random();
            UUID parentFolder = llsdRequest.folder_id;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000") + "/";

            AssetUploader uploader =
                new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, llsdRequest.inventory_type,
                                  llsdRequest.asset_type, capsBase + uploaderPath, MainServer.Instance, m_dumpAssetsToFile);

            MainServer.Instance.AddStreamHandler(
                new BinaryStreamHandler(
                    "POST",
                    capsBase + uploaderPath,
                    uploader.uploaderCaps,
                    "NewFileAgentInventoryVariablePrice",
                    agentID.ToString()));

            string protocol = "http://";

            if (MainServer.Instance.UseSSL)
                protocol = "https://";

            string uploaderURL = protocol + m_scene.RegionInfo.ExternalHostName + ":" + MainServer.Instance.Port.ToString() + capsBase +
                                 uploaderPath;


            LLSDNewFileAngentInventoryVariablePriceReplyResponse uploadResponse = new LLSDNewFileAngentInventoryVariablePriceReplyResponse();
            
            uploadResponse.rsvp = uploaderURL;
            uploadResponse.state = "upload";
            uploadResponse.resource_cost = 0;
            uploadResponse.upload_price = 0;

            uploader.OnUpLoad += //UploadCompleteHandler;
                
                delegate(
                string passetName, string passetDescription, UUID passetID,
                UUID pinventoryItem, UUID pparentFolder, byte[] pdata, string pinventoryType,
                string passetType)
               {
                   UploadCompleteHandler(passetName, passetDescription,  passetID,
                                          pinventoryItem, pparentFolder, pdata,  pinventoryType,
                                          passetType,agentID);
               };

            return uploadResponse;
        }

        public void UploadCompleteHandler(string assetName, string assetDescription, UUID assetID,
                                          UUID inventoryItem, UUID parentFolder, byte[] data, string inventoryType,
                                          string assetType,UUID AgentID)
        {
//            m_log.DebugFormat(
//                "[NEW FILE AGENT INVENTORY VARIABLE PRICE MODULE]: Upload complete for {0}", inventoryItem);

            sbyte assType = 0;
            sbyte inType = 0;

            if (inventoryType == "sound")
            {
                inType = 1;
                assType = 1;
            }
            else if (inventoryType == "animation")
            {
                inType = 19;
                assType = 20;
            }
            else if (inventoryType == "wearable")
            {
                inType = 18;
                switch (assetType)
                {
                    case "bodypart":
                        assType = 13;
                        break;
                    case "clothing":
                        assType = 5;
                        break;
                }
            }
            else if (inventoryType == "mesh")
            {
                inType = (sbyte)InventoryType.Mesh; 
                assType = (sbyte)AssetType.Mesh;
            }

            AssetBase asset;
            asset = new AssetBase(assetID, assetName, assType, AgentID.ToString());
            asset.Data = data;
    
            if (m_scene.AssetService != null)
                m_scene.AssetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Owner = AgentID;
            item.CreatorId = AgentID.ToString();
            item.ID = inventoryItem;
            item.AssetID = asset.FullID;
            item.Description = assetDescription;
            item.Name = assetName;
            item.AssetType = assType;
            item.InvType = inType;
            item.Folder = parentFolder;
            item.CurrentPermissions
                = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer);
            item.BasePermissions = (uint)PermissionMask.All;
            item.EveryOnePermissions = 0;
            item.NextPermissions = (uint)PermissionMask.All;
            item.CreationDate = Util.UnixTimeSinceEpoch();
            m_scene.AddInventoryItem(item);
        }
    }
}
