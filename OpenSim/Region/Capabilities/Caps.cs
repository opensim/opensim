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
using libsecondlife;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Caches;

namespace OpenSim.Region.Capabilities
{
    public delegate void UpLoadedTexture(LLUUID assetID, LLUUID inventoryItem, byte[] data);

    public class Caps
    {
        private string m_httpListenerHostName;
        private int m_httpListenPort;
        private string m_capsObjectPath = "00001-";
        private string m_requestPath = "0000/";
        private string m_mapLayerPath = "0001/";
        private string m_newInventory = "0002/";
        private string m_requestTexture = "0003/";
        private string eventQueue = "0100/";
        private BaseHttpServer httpListener;
        private LLUUID agentID;
        private AssetCache assetCache;
        private int eventQueueCount = 1;
        private Queue<string> CapsEventQueue = new Queue<string>();

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

            AddLegacyCapsHandler( httpListener, m_mapLayerPath, MapLayer);

            //httpListener.AddStreamHandler(
            //    new LLSDStreamhandler<LLSDMapRequest, LLSDMapLayerResponse>("POST", capsBase + m_mapLayerPath, this.GetMapLayer ));

            AddLegacyCapsHandler(httpListener, m_requestPath, CapsRequest);                       
            AddLegacyCapsHandler(httpListener, m_newInventory, NewAgentInventory);
            AddLegacyCapsHandler( httpListener, eventQueue, ProcessEventQueue);
            AddLegacyCapsHandler( httpListener, m_requestTexture, RequestTexture);
        }

        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(this.BuildLLSDMapLayerResponse());
            return mapResponse;
        }

        [Obsolete("Use BaseHttpServer.AddStreamHandler(new LLSDStreamHandler( LLSDMethod delegate )) instead.")]
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
           // Console.WriteLine("Caps Request " + request);
            string result = ""; 
            result = LLSDHelpers.SerialiseLLSDReply(this.GetCapabilities());
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
            
            return caps;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string MapLayer(string request, string path, string param)
        {
            Encoding _enc = Encoding.UTF8;
            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(_enc.GetBytes(request));
            LLSDMapRequest mapReq = new LLSDMapRequest();
            LLSDHelpers.DeserialiseLLSDMap(hash, mapReq);

            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(this.BuildLLSDMapLayerResponse());
            string res = LLSDHelpers.SerialiseLLSDReply(mapResponse);

            return res;
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
            // Needs implementing (added to remove compiler warning)
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected LLSDMapLayer BuildLLSDMapLayerResponse()
        {
            LLSDMapLayer mapLayer = new LLSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
            mapLayer.ImageID = new LLUUID("00000000-0000-0000-9999-000000000006");

            return mapLayer;
        }

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

        public string CreateEmptyEventResponse()
        {
            LLSDCapEvent eventItem = new LLSDCapEvent();
            eventItem.id = eventQueueCount;
            eventItem.events.Array.Add(new LLSDEmpty());
            string res = LLSDHelpers.SerialiseLLSDReply(eventItem);
            eventQueueCount++;
            return res;
        }

        public string NewAgentInventory(string request, string path, string param)
        {
            //Console.WriteLine("received upload request:"+ request);
            string res = "";
            LLUUID newAsset = LLUUID.Random();
            LLUUID newInvItem = LLUUID.Random();
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000");
            AssetUploader uploader = new AssetUploader(newAsset, newInvItem, uploaderPath, this.httpListener);
            
            AddLegacyCapsHandler( httpListener, uploaderPath, uploader.uploaderCaps);
            
            string uploaderURL = "http://" + m_httpListenerHostName + ":" + m_httpListenPort.ToString() + "/CAPS/" + uploaderPath;
            //Console.WriteLine("uploader url is " + uploaderURL);
            res += "<llsd><map>";
            res += "<key>uploader</key><string>" + uploaderURL + "</string>";
            //res += "<key>success</key><boolean>true</boolean>";
            res += "<key>state</key><string>upload</string>";
            res += "</map></llsd>";
            uploader.OnUpLoad += this.UploadHandler;
            return res;
        }

        public void UploadHandler(LLUUID assetID, LLUUID inventoryItem, byte[] data)
        {
            // Console.WriteLine("upload handler called");
            AssetBase asset;
            asset = new AssetBase();
            asset.FullID = assetID;
            asset.Type = 0;
            asset.InvType = 0;
            asset.Name = "UploadedTexture" + Util.RandomClass.Next(1, 1000).ToString("000");
            asset.Data = data;
            this.assetCache.AddAsset(asset);
        }

        public class AssetUploader
        {
            public event UpLoadedTexture OnUpLoad;

            private string uploaderPath = "";
            private LLUUID newAssetID;
            private LLUUID inventoryItemID;
            private BaseHttpServer httpListener;
            public AssetUploader(LLUUID assetID, LLUUID inventoryItem, string path, BaseHttpServer httpServer)
            {
                newAssetID = assetID;
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                httpListener = httpServer;

            }

            public string uploaderCaps(string request, string path, string param)
            {
                Encoding _enc = Encoding.UTF8;
                byte[] data = _enc.GetBytes(request);
                //Console.WriteLine("recieved upload " + Util.FieldToString(data));
                LLUUID inv = this.inventoryItemID;
                string res = "";
                res += "<llsd><map>";
                res += "<key>new_asset</key><string>" + newAssetID.ToStringHyphenated() + "</string>";
                res += "<key>new_inventory_item</key><uuid>" + inv.ToStringHyphenated() + "</uuid>";
                res += "<key>state</key><string>complete</string>";
                res += "</map></llsd>";

               // Console.WriteLine("asset " + newAssetID.ToStringHyphenated() + " , inventory item " + inv.ToStringHyphenated());
                httpListener.RemoveStreamHandler("POST", "/CAPS/" + uploaderPath);
                
                if (OnUpLoad != null)
                {
                    OnUpLoad(newAssetID, inv, data);
                }

                /*FileStream fs = File.Create("upload.jp2");
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();*/
                return res;
            }
        }
    }
}
