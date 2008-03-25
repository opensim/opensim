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
 *     * Neither the name of the OpenSim Project nor the
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
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Region.Capabilities
{
    public delegate void UpLoadedAsset(
        string assetName, string description, LLUUID assetID, LLUUID inventoryItem, LLUUID parentFolder,
        byte[] data, string inventoryType, string assetType);

    public delegate LLUUID UpdateItem(LLUUID itemID, byte[] data);

    public delegate void UpdateTaskScript(LLUUID itemID, LLUUID primID, bool isScriptRunning, byte[] data);

    public delegate void NewInventoryItem(LLUUID userID, InventoryItemBase item);

    public delegate LLUUID ItemUpdatedCallback(LLUUID userID, LLUUID itemID, byte[] data);

    public delegate void TaskScriptUpdatedCallback(LLUUID userID, LLUUID itemID, LLUUID primID,
                                                   bool isScriptRunning, byte[] data);

    public delegate List<InventoryItemBase> FetchInventoryDescendentsCAPS(LLUUID agentID, LLUUID folderID, LLUUID ownerID,
                                                   bool fetchFolders, bool fetchItems, int sortOrder);

    public class Caps
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string m_httpListenerHostName;
        private uint m_httpListenPort;

        /// <summary>
        /// This is the uuid portion of every CAPS path.  It is used to make capability urls private to the requester.
        /// </summary>
        private string m_capsObjectPath;        
        public string CapsObjectPath { get { return m_capsObjectPath; } }
        
        private string m_requestPath = "0000/";
        private string m_mapLayerPath = "0001/";
        private string m_newInventory = "0002/";
        //private string m_requestTexture = "0003/";
        private string m_notecardUpdatePath = "0004/";
        private string m_notecardTaskUpdatePath = "0005/";
        private string m_fetchInventoryPath = "0006/";
        private string m_parcelVoiceInfoRequestPath = "0007/";
        private string m_provisionVoiceAccountRequestPath = "0008/";

        //private string eventQueue = "0100/";
        private BaseHttpServer m_httpListener;
        private LLUUID m_agentID;
        private AssetCache m_assetCache;
        private int m_eventQueueCount = 1;
        private Queue<string> m_capsEventQueue = new Queue<string>();
        private bool m_dumpAssetsToFile;

        // These are callbacks which will be setup by the scene so that we can update scene data when we 
        // receive capability calls
        public NewInventoryItem AddNewInventoryItem = null;
        public ItemUpdatedCallback ItemUpdatedCall = null;
        public TaskScriptUpdatedCallback TaskScriptUpdatedCall = null;
        //
        public FetchInventoryDescendentsCAPS CAPSFetchInventoryDescendents = null;


        public Caps(AssetCache assetCache, BaseHttpServer httpServer, string httpListen, uint httpPort, string capsPath,
                    LLUUID agent, bool dumpAssetsToFile)
        {
            m_assetCache = assetCache;
            m_capsObjectPath = capsPath;
            m_httpListener = httpServer;
            m_httpListenerHostName = httpListen;
            m_httpListenPort = httpPort;
            m_agentID = agent;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RegisterHandlers()
        {
            string capsBase = "/CAPS/" + m_capsObjectPath;
            
            try
            {
                m_httpListener.RemoveStreamHandler("POST", capsBase + m_mapLayerPath);
                m_httpListener.AddStreamHandler(
                    new LLSDStreamhandler<LLSDMapRequest, LLSDMapLayerResponse>("POST", capsBase + m_mapLayerPath,
                                                                                GetMapLayer));
                
                m_httpListener.RemoveStreamHandler("POST", capsBase + m_newInventory);                
                m_httpListener.AddStreamHandler(
                    new LLSDStreamhandler<LLSDAssetUploadRequest, LLSDAssetUploadResponse>("POST",
                                                                                           capsBase + m_newInventory,
                                                                                           NewAgentInventoryRequest));

               // m_httpListener.AddStreamHandler(
                 //  new LLSDStreamhandler<LLSDFetchInventoryDescendents, LLSDInventoryDescendents>("POST",
              //                                                                            capsBase + m_fetchInventory,
              //                                                                            FetchInventory));


                AddLegacyCapsHandler(m_httpListener, m_requestPath, CapsRequest);
                //AddLegacyCapsHandler(m_httpListener, m_requestTexture , RequestTexture);
                AddLegacyCapsHandler(m_httpListener, m_parcelVoiceInfoRequestPath, ParcelVoiceInfoRequest);
                AddLegacyCapsHandler(m_httpListener, m_provisionVoiceAccountRequestPath, ProvisionVoiceAccountRequest);
                AddLegacyCapsHandler(m_httpListener, m_notecardUpdatePath, NoteCardAgentInventory);
                AddLegacyCapsHandler(m_httpListener, m_notecardTaskUpdatePath, ScriptTaskInventory);
                AddLegacyCapsHandler(m_httpListener, m_fetchInventoryPath, FetchInventoryRequest);
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }
        }


        //[Obsolete("Use BaseHttpServer.AddStreamHandler(new LLSDStreamHandler( LLSDMethod delegate )) instead.")]
        //Commented out the obsolete as at this time the first caps request can not use the new Caps method 
        //as the sent type is a array and not a map and the deserialising doesn't deal properly with arrays.
        private void AddLegacyCapsHandler(BaseHttpServer httpListener, string path, RestMethod restMethod)
        {
            string capsBase = "/CAPS/" + m_capsObjectPath;
            httpListener.RemoveStreamHandler("POST", capsBase + path); 
            httpListener.AddStreamHandler(new RestStreamHandler("POST", capsBase + path, restMethod));
        }

        /// <summary>
        /// Construct a client response detailing all the capabilities this server can provide.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string CapsRequest(string request, string path, string param)
        {
            //Console.WriteLine("caps request " + request);
            string result = LLSDHelpers.SerialiseLLSDReply(GetCapabilities());
            return result;
        }

        /// <summary>
        /// Return an LLSDCapsDetails listing all the capabilities this server can provide
        /// </summary>
        /// <returns></returns>
        protected LLSDCapsDetails GetCapabilities()
        {
            LLSDCapsDetails caps = new LLSDCapsDetails();
            string capsBaseUrl = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + "/CAPS/" +
                                 m_capsObjectPath;
            caps.MapLayer = capsBaseUrl + m_mapLayerPath;
            // caps.RequestTextureDownload = capsBaseUrl + m_requestTexture;
            caps.NewFileAgentInventory = capsBaseUrl + m_newInventory;
            caps.UpdateNotecardAgentInventory = capsBaseUrl + m_notecardUpdatePath;
            caps.UpdateScriptAgentInventory = capsBaseUrl + m_notecardUpdatePath;
            caps.UpdateScriptTaskInventory = capsBaseUrl + m_notecardTaskUpdatePath;
            caps.FetchInventoryDescendents = capsBaseUrl + m_fetchInventoryPath;
            caps.ParcelVoiceInfoRequest = capsBaseUrl + m_parcelVoiceInfoRequestPath;
            caps.ProvisionVoiceAccountRequest = capsBaseUrl + m_provisionVoiceAccountRequestPath;

            return caps;
        }

        public string FetchInventoryRequest(string request, string path, string param)
        {
            request = request.Replace("<llsd><map><key>folders</key><array>", "<llsd>");
            request = request.Replace("</map></array></map>", "</map>");

            //Console.WriteLine("inventory request " + request);
            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Helpers.StringToField(request));
            LLSDFetchInventoryDescendents llsdRequest = new LLSDFetchInventoryDescendents();
            LLSDHelpers.DeserialiseLLSDMap(hash, llsdRequest);
            LLSDInventoryDescendents reply = FetchInventory(llsdRequest);
            string response = LLSDHelpers.SerialiseLLSDReply(reply);
            return response;
        }

        private LLSDInventoryDescendents FetchInventory(LLSDFetchInventoryDescendents invFetch)
        {
            LLSDInventoryDescendents reply = new LLSDInventoryDescendents();
            LLSDInventoryFolderContents contents = new LLSDInventoryFolderContents();
            contents.agent___id = m_agentID;
            contents.owner___id =  invFetch.owner_id;
            contents.folder___id = invFetch.folder_id;
            contents.version = 1; //FixMe
            contents.descendents = 0;
            reply.folders.Array.Add(contents);
            List<InventoryItemBase> itemList = null;
            if (CAPSFetchInventoryDescendents != null)
            {
                itemList = CAPSFetchInventoryDescendents(m_agentID, invFetch.folder_id, invFetch.owner_id, invFetch.fetch_folders, invFetch.fetch_items, invFetch.sort_order);
            }
            if (itemList != null)
            {
                foreach (InventoryItemBase invItem in itemList)
                {
                    contents.items.Array.Add(ConvertInventoryItem(invItem));
                }
            }
            contents.descendents = contents.items.Array.Count;
            return reply;
        }

        private LLSDInventoryItem ConvertInventoryItem(InventoryItemBase invItem)
        {
            LLSDInventoryItem llsdItem = new LLSDInventoryItem();
            llsdItem.asset_id = invItem.assetID;
            llsdItem.created_at = 1000;
            llsdItem.desc = invItem.inventoryDescription;
            llsdItem.flags = 0;
            llsdItem.item_id = invItem.inventoryID;
            llsdItem.name = invItem.inventoryName;
            llsdItem.parent_id = invItem.parentFolderID;
            llsdItem.type = Enum.GetName(typeof(AssetType), invItem.assetType).ToLower();
            llsdItem.inv_type = Enum.GetName(typeof(InventoryType), invItem.invType).ToLower();
            llsdItem.permissions = new LLSDPermissions();
            llsdItem.permissions.creator_id = invItem.creatorsID;
            llsdItem.permissions.base_mask = (int)invItem.inventoryBasePermissions;
            llsdItem.permissions.everyone_mask = (int)invItem.inventoryEveryOnePermissions;
            llsdItem.permissions.group_id = LLUUID.Zero;
            llsdItem.permissions.group_mask = 0;
            llsdItem.permissions.is_owner_group = false;
            llsdItem.permissions.next_owner_mask = (int)invItem.inventoryNextPermissions;
            llsdItem.permissions.owner_id = m_agentID; // FixMe
            llsdItem.permissions.owner_mask = (int)invItem.inventoryCurrentPermissions;
            llsdItem.sale_info = new LLSDSaleInfo();
            llsdItem.sale_info.sale_price = 10;
            llsdItem.sale_info.sale_type = "not";

            return llsdItem;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetLLSDMapLayerResponse());
            return mapResponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected LLSDMapLayer GetLLSDMapLayerResponse()
        {
            LLSDMapLayer mapLayer = new LLSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
            mapLayer.ImageID = new LLUUID("00000000-0000-1111-9999-000000000006");
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
            Console.WriteLine("texture request " + request);
            // Needs implementing (added to remove compiler warning)
            return String.Empty;
        }

        #region EventQueue (Currently not enabled)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string ProcessEventQueue(string request, string path, string param)
        {
            string res = String.Empty;

            if (m_capsEventQueue.Count > 0)
            {
                lock (m_capsEventQueue)
                {
                    string item = m_capsEventQueue.Dequeue();
                    res = item;
                }
            }
            else
            {
                res = CreateEmptyEventResponse();
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="caps"></param>
        /// <param name="ipAddressPort"></param>
        /// <returns></returns>
        public string CreateEstablishAgentComms(string caps, string ipAddressPort)
        {
            LLSDCapEvent eventItem = new LLSDCapEvent();
            eventItem.id = m_eventQueueCount;
            //should be creating a EstablishAgentComms item, but there isn't a class for it yet
            eventItem.events.Array.Add(new LLSDEmpty());
            string res = LLSDHelpers.SerialiseLLSDReply(eventItem);
            m_eventQueueCount++;

            m_capsEventQueue.Enqueue(res);
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string CreateEmptyEventResponse()
        {
            LLSDCapEvent eventItem = new LLSDCapEvent();
            eventItem.id = m_eventQueueCount;
            eventItem.events.Array.Add(new LLSDEmpty());
            string res = LLSDHelpers.SerialiseLLSDReply(eventItem);
            m_eventQueueCount++;
            return res;
        }

        #endregion

        /// <summary>
        /// Called by the script task update handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string ScriptTaskInventory(string request, string path, string param)
        {
            try
            {
//                m_log.DebugFormat("[CAPS]: request: {0}, path: {1}, param: {2}", request, path, param);

                Hashtable hash = (Hashtable) LLSD.LLSDDeserialize(Helpers.StringToField(request));
                LLSDTaskScriptUpdate llsdUpdateRequest = new LLSDTaskScriptUpdate();
                LLSDHelpers.DeserialiseLLSDMap(hash, llsdUpdateRequest);

                string capsBase = "/CAPS/" + m_capsObjectPath;
                string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

                TaskInventoryScriptUpdater uploader =
                    new TaskInventoryScriptUpdater(
                        llsdUpdateRequest.item_id,
                        llsdUpdateRequest.task_id,
                        llsdUpdateRequest.is_script_running,
                        capsBase + uploaderPath,
                        m_httpListener,
                        m_dumpAssetsToFile);
                uploader.OnUpLoad += TaskScriptUpdated;

                m_httpListener.AddStreamHandler(
                    new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
                string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase +
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


        public string ParcelVoiceInfoRequest(string request, string path, string param)
        {
            try
            {
                m_log.DebugFormat("[CAPS]: request: {0}, path: {1}, param: {2}", request, path, param);

                //Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Helpers.StringToField(request));
                //LLSDTaskScriptUpdate llsdUpdateRequest = new LLSDTaskScriptUpdate();
                //LLSDHelpers.DeserialiseLLSDMap(hash, llsdUpdateRequest);

                //string capsBase = "/CAPS/" + m_capsObjectPath;
                //string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

                //TaskInventoryScriptUpdater uploader =
                    //new TaskInventoryScriptUpdater(
                        //llsdUpdateRequest.item_id,
                        //llsdUpdateRequest.task_id,
                        //llsdUpdateRequest.is_script_running,
                        //capsBase + uploaderPath,
                        //m_httpListener,
                        //m_dumpAssetsToFile);
                //uploader.OnUpLoad += TaskScriptUpdated;

                //m_httpListener.AddStreamHandler(
                    //new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
                //string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase +
                                     //uploaderPath;

                //LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                //uploadResponse.uploader = uploaderURL;
                //uploadResponse.state = "upload";

                //                m_log.InfoFormat("[CAPS]: " +
                //                                 "ScriptTaskInventory response: {0}",
                //                                 LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

                return LLSDHelpers.SerialiseLLSDReply("<llsd><map><key>parcel_local_id</key><integer>16</integer><key>region_name</key><string>Teravus Test</string><key>voice_credentials</key><map><key>channel_uri</key><string>sip:conference@192.168.1.127\nsip:user@192.168.1.127</string></map></map></llsd>");
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }

            return null;
        }

        public string ProvisionVoiceAccountRequest(string request, string path, string param)
        {
            try
            {
                m_log.DebugFormat("[CAPS]: request: {0}, path: {1}, param: {2}", request, path, param);

                //Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Helpers.StringToField(request));
                //LLSDTaskScriptUpdate llsdUpdateRequest = new LLSDTaskScriptUpdate();
                //LLSDHelpers.DeserialiseLLSDMap(hash, llsdUpdateRequest);

                //string capsBase = "/CAPS/" + m_capsObjectPath;
                //string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

                //TaskInventoryScriptUpdater uploader =
                //new TaskInventoryScriptUpdater(
                //llsdUpdateRequest.item_id,
                //llsdUpdateRequest.task_id,
                //llsdUpdateRequest.is_script_running,
                //capsBase + uploaderPath,
                //m_httpListener,
                //m_dumpAssetsToFile);
                //uploader.OnUpLoad += TaskScriptUpdated;

                //m_httpListener.AddStreamHandler(
                //new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
                //string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase +
                //uploaderPath;

                //LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                //uploadResponse.uploader = uploaderURL;
                //uploadResponse.state = "upload";

                //                m_log.InfoFormat("[CAPS]: " +
                //                                 "ScriptTaskInventory response: {0}",
                //                                 LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

                return LLSDHelpers.SerialiseLLSDReply("<llsd><map><key>events</key><array><map><key>body</key><map><key>major_version</key><integer>1</integer><key>minor_version</key><integer>0</integer><key>region_name</key><string>Teravus Test</string></map><key>message</key><string>RequiredVoiceVersion</string></map></array><key>id</key><integer>152477222</integer></map></llsd>");
            }
            catch (Exception e)
            {
                m_log.Error("[CAPS]: " + e.ToString());
            }

            return null;
        }
        /// <summary>
        /// Called by the notecard update handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string NoteCardAgentInventory(string request, string path, string param)
        {
            //libsecondlife.StructuredData.LLSDMap hash = (libsecondlife.StructuredData.LLSDMap)libsecondlife.StructuredData.LLSDParser.DeserializeBinary(Helpers.StringToField(request));
            Hashtable hash = (Hashtable) LLSD.LLSDDeserialize(Helpers.StringToField(request));
            LLSDItemUpdate llsdRequest = new LLSDItemUpdate();
            LLSDHelpers.DeserialiseLLSDMap(hash, llsdRequest);

            string capsBase = "/CAPS/" + m_capsObjectPath;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            ItemUpdater uploader =
                new ItemUpdater(llsdRequest.item_id, capsBase + uploaderPath, m_httpListener, m_dumpAssetsToFile);
            uploader.OnUpLoad += ItemUpdated;

            m_httpListener.AddStreamHandler(
                new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
            string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase +
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
        /// 
        /// </summary>
        /// <param name="llsdRequest"></param>
        /// <returns></returns>
        public LLSDAssetUploadResponse NewAgentInventoryRequest(LLSDAssetUploadRequest llsdRequest)
        {
            //Console.WriteLine("asset upload request via CAPS" + llsdRequest.inventory_type +" , "+ llsdRequest.asset_type);

            string assetName = llsdRequest.name;
            string assetDes = llsdRequest.description;
            string capsBase = "/CAPS/" + m_capsObjectPath;
            LLUUID newAsset = LLUUID.Random();
            LLUUID newInvItem = LLUUID.Random();
            LLUUID parentFolder = llsdRequest.folder_id;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            AssetUploader uploader =
                new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, llsdRequest.inventory_type,
                                  llsdRequest.asset_type, capsBase + uploaderPath, m_httpListener, m_dumpAssetsToFile);
            m_httpListener.AddStreamHandler(
                new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
            string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase +
                                 uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";
            uploader.OnUpLoad += UploadCompleteHandler;
            return uploadResponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="inventoryItem"></param>
        /// <param name="data"></param>
        public void UploadCompleteHandler(string assetName, string assetDescription, LLUUID assetID,
                                          LLUUID inventoryItem, LLUUID parentFolder, byte[] data, string inventoryType,
                                          string assetType)
        {
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

            AssetBase asset;
            asset = new AssetBase();
            asset.FullID = assetID;
            asset.Type = assType;
            asset.InvType = inType;
            asset.Name = assetName;
            asset.Data = data;
            m_assetCache.AddAsset(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = m_agentID;
            item.creatorsID = m_agentID;
            item.inventoryID = inventoryItem;
            item.assetID = asset.FullID;
            item.inventoryDescription = assetDescription;
            item.inventoryName = assetName;
            item.assetType = assType;
            item.invType = inType;
            item.parentFolderID = parentFolder;
            item.inventoryCurrentPermissions = 2147483647;
            item.inventoryNextPermissions = 2147483647;

            if (AddNewInventoryItem != null)
            {
                AddNewInventoryItem(m_agentID, item);
            }
        }

        /// <summary>
        /// Called when new asset data for an agent inventory item update has been uploaded.
        /// </summary>
        /// <param name="itemID">Item to update</param>
        /// <param name="data">New asset data</param>
        /// <returns></returns>
        public LLUUID ItemUpdated(LLUUID itemID, byte[] data)
        {
            if (ItemUpdatedCall != null)
            {
                return ItemUpdatedCall(m_agentID, itemID, data);
            }

            return LLUUID.Zero;
        }

        /// <summary>
        /// Called when new asset data for an agent inventory item update has been uploaded.
        /// </summary>
        /// <param name="itemID">Item to update</param>
        /// <param name="primID">Prim containing item to update</param>
        /// <param name="isScriptRunning">Signals whether the script to update is currently running</param>
        /// <param name="data">New asset data</param>        
        public void TaskScriptUpdated(LLUUID itemID, LLUUID primID, bool isScriptRunning, byte[] data)
        {
            if (TaskScriptUpdatedCall != null)
            {
                TaskScriptUpdatedCall(m_agentID, itemID, primID, isScriptRunning, data);
            }
        }

        public class AssetUploader
        {
            public event UpLoadedAsset OnUpLoad;
            private UpLoadedAsset handlerUpLoad = null;

            private string uploaderPath = String.Empty;
            private LLUUID newAssetID;
            private LLUUID inventoryItemID;
            private LLUUID parentFolder;
            private BaseHttpServer httpListener;
            private bool m_dumpAssetsToFile;
            private string m_assetName = String.Empty;
            private string m_assetDes = String.Empty;

            private string m_invType = String.Empty;
            private string m_assetType = String.Empty;

            public AssetUploader(string assetName, string description, LLUUID assetID, LLUUID inventoryItem,
                                 LLUUID parentFolderID, string invType, string assetType, string path,
                                 BaseHttpServer httpServer, bool dumpAssetsToFile)
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
            /// 
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public string uploaderCaps(byte[] data, string path, string param)
            {
                LLUUID inv = inventoryItemID;
                string res = String.Empty;
                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                uploadComplete.new_asset = newAssetID.ToString();
                uploadComplete.new_inventory_item = inv;
                uploadComplete.state = "complete";

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

                httpListener.RemoveStreamHandler("POST", uploaderPath);

                if (m_dumpAssetsToFile)
                {
                    SaveAssetToFile(m_assetName + ".jp2", data);
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
            private void SaveAssetToFile(string filename, byte[] data)
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
            private LLUUID inventoryItemID;
            private BaseHttpServer httpListener;
            private bool m_dumpAssetToFile;

            public ItemUpdater(LLUUID inventoryItem, string path, BaseHttpServer httpServer, bool dumpAssetToFile)
            {
                m_dumpAssetToFile = dumpAssetToFile;

                inventoryItemID = inventoryItem;
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
                LLUUID inv = inventoryItemID;
                string res = String.Empty;
                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                LLUUID assetID = LLUUID.Zero;
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
            private void SaveAssetToFile(string filename, byte[] data)
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
            public event UpdateTaskScript OnUpLoad;

            private UpdateTaskScript handlerUpdateTaskScript = null;

            private string uploaderPath = String.Empty;
            private LLUUID inventoryItemID;
            private LLUUID primID;
            private bool isScriptRunning;
            private BaseHttpServer httpListener;
            private bool m_dumpAssetToFile;

            public TaskInventoryScriptUpdater(LLUUID inventoryItemID, LLUUID primID, int isScriptRunning,
                                              string path, BaseHttpServer httpServer, bool dumpAssetToFile)
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
                    LLSDTaskInventoryUploadComplete uploadComplete = new LLSDTaskInventoryUploadComplete();

                    handlerUpdateTaskScript = OnUpLoad;
                    if (handlerUpdateTaskScript != null)
                    {
                        handlerUpdateTaskScript(inventoryItemID, primID, isScriptRunning, data);
                    }

                    uploadComplete.item_id = inventoryItemID;
                    uploadComplete.task_id = primID;
                    uploadComplete.state = "complete";

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
            private void SaveAssetToFile(string filename, byte[] data)
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
}
