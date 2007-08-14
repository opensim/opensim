/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Data;

namespace OpenSim.Region.Capabilities
{
    public delegate void UpLoadedAsset(string assetName, string description, LLUUID assetID, LLUUID inventoryItem, LLUUID parentFolder, byte[] data);
    public delegate LLUUID UpdateItem(LLUUID itemID, byte[] data);
    public delegate void NewInventoryItem(LLUUID userID, InventoryItemBase item);
    public delegate LLUUID ItemUpdatedCallback(LLUUID userID, LLUUID itemID, byte[] data);

    public class Caps
    {
        private string m_httpListenerHostName;
        private int m_httpListenPort;
        private string m_capsObjectPath = "00001-";
        private string m_requestPath = "0000/";
        private string m_mapLayerPath = "0001/";
        private string m_newInventory = "0002/";
        //private string m_requestTexture = "0003/";
        private string m_notecardUpdatePath = "0004/";
        //private string eventQueue = "0100/";
        private BaseHttpServer httpListener;
        private LLUUID agentID;
        private AssetCache assetCache;
        private int eventQueueCount = 1;
        private Queue<string> CapsEventQueue = new Queue<string>();
        public NewInventoryItem AddNewInventoryItem = null;
        public ItemUpdatedCallback ItemUpdatedCall = null;

        public Caps(AssetCache assetCach, BaseHttpServer httpServer, string httpListen, int httpPort, string capsPath, LLUUID agent)
        {
            assetCache = assetCach;
            m_capsObjectPath = capsPath;
            httpListener = httpServer;
            m_httpListenerHostName = httpListen;
            m_httpListenPort = httpPort;
            agentID = agent;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RegisterHandlers()
        {
            Console.WriteLine("registering CAPS handlers");
            string capsBase = "/CAPS/" + m_capsObjectPath;
            try
            {
                httpListener.AddStreamHandler(new LLSDStreamhandler<LLSDMapRequest, LLSDMapLayerResponse>("POST", capsBase + m_mapLayerPath, this.GetMapLayer));
                httpListener.AddStreamHandler(new LLSDStreamhandler<LLSDAssetUploadRequest, LLSDAssetUploadResponse>("POST", capsBase + m_newInventory, this.NewAgentInventoryRequest));

                AddLegacyCapsHandler(httpListener, m_requestPath, CapsRequest);
                //AddLegacyCapsHandler(httpListener, m_requestTexture , RequestTexture);
                AddLegacyCapsHandler(httpListener, m_notecardUpdatePath, NoteCardAgentInventory);
            }
            catch
            {
            }
        }


        //[Obsolete("Use BaseHttpServer.AddStreamHandler(new LLSDStreamHandler( LLSDMethod delegate )) instead.")]
        //Commented out the obsolete as at this time the first caps request can not use the new Caps method 
        //as the sent type is a array and not a map and the deserialising doesn't deal properly with arrays.
        private void AddLegacyCapsHandler(BaseHttpServer httpListener, string path, RestMethod restMethod)
        {
            string capsBase = "/CAPS/" + m_capsObjectPath;
            httpListener.AddStreamHandler(new RestStreamHandler("POST", capsBase + path, restMethod));
        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string CapsRequest(string request, string path, string param)
        {
            //Console.WriteLine("caps request " + request);
            string result = LLSDHelpers.SerialiseLLSDReply(this.GetCapabilities());
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected LLSDCapsDetails GetCapabilities()
        {
            LLSDCapsDetails caps = new LLSDCapsDetails();
            string capsBaseUrl = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + "/CAPS/" + m_capsObjectPath;
            caps.MapLayer = capsBaseUrl + m_mapLayerPath;
            caps.NewFileAgentInventory = capsBaseUrl + m_newInventory;
            caps.UpdateNotecardAgentInventory = capsBaseUrl + m_notecardUpdatePath;
            caps.UpdateScriptAgentInventory = capsBaseUrl + m_notecardUpdatePath;
            return caps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(this.GetLLSDMapLayerResponse());
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
            mapLayer.ImageID = new LLUUID("00000000-0000-0000-9999-000000000006");
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
            return "";
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
            string res = "";
            
            if (this.CapsEventQueue.Count > 0)
            {
                lock (this.CapsEventQueue)
                {
                    string item = CapsEventQueue.Dequeue();
                    res = item;
                }
            }
            else
            {
                res = this.CreateEmptyEventResponse();
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
            eventItem.id = eventQueueCount;
            //should be creating a EstablishAgentComms item, but there isn't a class for it yet
            eventItem.events.Array.Add(new LLSDEmpty());
            string res = LLSDHelpers.SerialiseLLSDReply(eventItem);
            eventQueueCount++;
            
            this.CapsEventQueue.Enqueue(res);
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string CreateEmptyEventResponse()
        {
            LLSDCapEvent eventItem = new LLSDCapEvent();
            eventItem.id = eventQueueCount;
            eventItem.events.Array.Add(new LLSDEmpty());
            string res = LLSDHelpers.SerialiseLLSDReply(eventItem);
            eventQueueCount++;
            return res;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string NoteCardAgentInventory(string request, string path, string param)
        {
            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(Helpers.StringToField(request));
            LLSDItemUpdate llsdRequest = new LLSDItemUpdate();
            LLSDHelpers.DeserialiseLLSDMap(hash, llsdRequest);
          
            string capsBase = "/CAPS/" + m_capsObjectPath;
            LLUUID newInvItem = llsdRequest.item_id;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            ItemUpdater uploader = new ItemUpdater(newInvItem, capsBase + uploaderPath, this.httpListener);
            uploader.OnUpLoad += this.ItemUpdated;

            httpListener.AddStreamHandler(new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
            string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase + uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";
            
            return LLSDHelpers.SerialiseLLSDReply(uploadResponse);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="llsdRequest"></param>
        /// <returns></returns>
        public LLSDAssetUploadResponse NewAgentInventoryRequest(LLSDAssetUploadRequest llsdRequest)
        {
          //  Console.WriteLine("asset upload request via CAPS");
            
            string assetName = llsdRequest.name;
            string assetDes = llsdRequest.description;
            string capsBase = "/CAPS/" + m_capsObjectPath;
            LLUUID newAsset = LLUUID.Random();
            LLUUID newInvItem = LLUUID.Random();
            LLUUID parentFolder = llsdRequest.folder_id;
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");

            AssetUploader uploader = new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, "" , "", capsBase + uploaderPath, this.httpListener);
            httpListener.AddStreamHandler(new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));
            string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + capsBase + uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";
            uploader.OnUpLoad += this.UploadCompleteHandler;
            return uploadResponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="inventoryItem"></param>
        /// <param name="data"></param>
        public void UploadCompleteHandler(string assetName, string assetDescription, LLUUID assetID, LLUUID inventoryItem, LLUUID parentFolder,  byte[] data)
        {
            AssetBase asset;
            asset = new AssetBase();
            asset.FullID = assetID;
            asset.Type = 0;
            asset.InvType = 0;
            asset.Name = assetName; 
            asset.Data = data;
            this.assetCache.AddAsset(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = agentID;
            item.creatorsID = agentID;
            item.inventoryID =  inventoryItem;
            item.assetID = asset.FullID;
            item.inventoryDescription = assetDescription;
            item.inventoryName = assetName;
            item.assetType = 0;
            item.invType = 0;
            item.parentFolderID = parentFolder;
            item.inventoryCurrentPermissions = 2147483647;
            item.inventoryNextPermissions = 2147483647;

            if (AddNewInventoryItem != null)
            {
                AddNewInventoryItem(agentID, item);
            }

        }

        public LLUUID ItemUpdated(LLUUID itemID, byte[] data)
        {
            if (ItemUpdatedCall != null)
            {
               return ItemUpdatedCall(this.agentID, itemID, data);
            }
            return LLUUID.Zero;
        }

        public class AssetUploader
        {
            public event UpLoadedAsset OnUpLoad;

            private string uploaderPath = "";
            private LLUUID newAssetID;
            private LLUUID inventoryItemID;
            private LLUUID parentFolder; 
            private BaseHttpServer httpListener;
            private bool SaveAssets = false;
            private string m_assetName = "";
            private string m_assetDes = "";

            /// <summary>
            /// 
            /// </summary>
            /// <param name="assetID"></param>
            /// <param name="inventoryItem"></param>
            /// <param name="path"></param>
            /// <param name="httpServer"></param>
            public AssetUploader(string assetName, string description, LLUUID assetID, LLUUID inventoryItem, LLUUID parentFolderID, string invType, string assetType, string path, BaseHttpServer httpServer)
            {
                m_assetName = assetName;
                m_assetDes = description;
                newAssetID = assetID;
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                httpListener = httpServer;
                parentFolder = parentFolderID;
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
                LLUUID inv = this.inventoryItemID;
                string res = "";
                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                uploadComplete.new_asset = newAssetID.ToStringHyphenated();
                uploadComplete.new_inventory_item = inv;
                uploadComplete.state = "complete";

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
             
                httpListener.RemoveStreamHandler("POST", uploaderPath);

                if(this.SaveAssets)
                    this.SaveAssetToFile(m_assetName + ".jp2", data);

                if (OnUpLoad != null)
                {
                    OnUpLoad(m_assetName, m_assetDes, newAssetID, inv, parentFolder, data);
                }
                
                return res;
            }

            private void SaveAssetToFile(string filename, byte[] data)
            {
               FileStream fs = File.Create(filename);
               BinaryWriter bw = new BinaryWriter(fs);
               bw.Write(data);
               bw.Close();
               fs.Close();
            }
        }

        public class ItemUpdater
        {
            public event UpdateItem OnUpLoad;

            private string uploaderPath = "";
            private LLUUID inventoryItemID;
            private BaseHttpServer httpListener;
            private bool SaveAssets = false;


            /// <summary>
            /// 
            /// </summary>
            /// <param name="assetID"></param>
            /// <param name="inventoryItem"></param>
            /// <param name="path"></param>
            /// <param name="httpServer"></param>
            public ItemUpdater( LLUUID inventoryItem, string path, BaseHttpServer httpServer)
            {
              
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
                LLUUID inv = this.inventoryItemID;
                string res = "";
                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                LLUUID assetID = LLUUID.Zero;

                if (OnUpLoad != null)
                {
                    assetID = OnUpLoad(inv, data);
                }
               
                uploadComplete.new_asset = assetID.ToStringHyphenated();
                uploadComplete.new_inventory_item = inv;
                uploadComplete.state = "complete";

                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

                httpListener.RemoveStreamHandler("POST", uploaderPath);

                if (this.SaveAssets)
                    this.SaveAssetToFile("updateditem"+Util.RandomClass.Next(1,1000) + ".dat", data);

                return res;
            }

            private void SaveAssetToFile(string filename, byte[] data)
            {
                FileStream fs = File.Create(filename);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
        }
    }
}


