using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.Servers;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Types;
using OpenSim.Caches;
using libsecondlife;

namespace OpenSim.Region
{
    public delegate void UpLoadedTexture(LLUUID assetID, LLUUID inventoryItem, byte[] data);

    public class Caps
    {

        private string httpListenerAddress;
        private uint httpListenPort;
        private string capsObjectPath = "00001-";
        private string requestPath = "0000/";
        private string mapLayerPath = "0001/";
        private string newInventory = "0002/";
        private string requestTexture = "0003/";
        private string eventQueue = "0100/";
        private BaseHttpServer httpListener;
        private LLUUID agentID;
        private AssetCache assetCache;
        private int eventQueueCount = 1;
        private Queue<string> CapsEventQueue = new Queue<string>();

        public Caps(AssetCache assetCach, BaseHttpServer httpServer, string httpListen, uint httpPort, string capsPath, LLUUID agent)
        {
            assetCache = assetCach;
            capsObjectPath = capsPath;
            httpListener = httpServer;
            httpListenerAddress = httpListen;
            httpListenPort = httpPort;
            agentID = agent;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RegisterHandlers()
        {
            Console.WriteLine("registering CAPS handlers");
            httpListener.AddRestHandler("POST", "/CAPS/" + capsObjectPath + requestPath, CapsRequest);
            httpListener.AddRestHandler("POST", "/CAPS/" + capsObjectPath + mapLayerPath, MapLayer);
            httpListener.AddRestHandler("POST", "/CAPS/" + capsObjectPath + newInventory, NewAgentInventory);
            httpListener.AddRestHandler("POST", "/CAPS/" + capsObjectPath + eventQueue, ProcessEventQueue);
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
            Console.WriteLine("Caps Request " + request);
            string result = "<llsd><map>";
            result += this.GetCapabilities();
            result += "</map></llsd>";
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected string GetCapabilities()
        {
            string capURLS = "";

            capURLS += "<key>MapLayer</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + capsObjectPath + mapLayerPath + "</string>";
            capURLS += "<key>NewFileAgentInventory</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + capsObjectPath + newInventory + "</string>";
            //capURLS += "<key>RequestTextureDownload</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + capsObjectPath + requestTexture + "</string>";
            //capURLS += "<key>EventQueueGet</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + capsObjectPath + eventQueue + "</string>";
            return capURLS;
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
            string res = "<llsd><map><key>AgentData</key><map><key>Flags</key><integer>0</integer></map><key>LayerData</key><array>";
            res += this.BuildLLSDMapLayerResponse();
            res += "</array></map></llsd>";
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected string BuildLLSDMapLayerResponse()
        {
            string res = "";
            int left;
            int right;
            int top;
            int bottom;
            LLUUID image = null;

            left = 0;
            bottom = 0;
            top = 5000;
            right = 5000;
            image = new LLUUID("00000000-0000-0000-9999-000000000006");

            res += "<map><key>Left</key><integer>" + left + "</integer><key>Bottom</key><integer>" + bottom + "</integer><key>Top</key><integer>" + top + "</integer><key>Right</key><integer>" + right + "</integer><key>ImageID</key><uuid>" + image.ToStringHyphenated() + "</uuid></map>";

            return res;
        }

        public string ProcessEventQueue(string request, string path, string param)
        {
         //  Console.WriteLine("event queue request " + request);
            string res = "";
            int timer = 0;

           /*while ((timer < 200) || (this.CapsEventQueue.Count < 1))
            {
                timer++;
            }*/
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
            string res = "<llsd><map><key>id</key><integer>" + eventQueueCount + "</integer>";
            res += "<key>events</key><array><map>";
            res += "<key>message</key><string>EstablishAgentCommunication</string>";
            res += "<key>body</key><map>";
            res += "<key>sim-ip-and-port</key><string>"+ipAddressPort +"</string>";
            res += "<key>seed-capability</key><string>"+caps+"</string>";
            res += "<key>agent-id</key><uuid>"+this.agentID.ToStringHyphenated()+"</uuid>";
            res += "</map>";
            res += "</map></array>";
            res += "</map></llsd>";
            eventQueueCount++;
            this.CapsEventQueue.Enqueue(res);
            return res;
        }

        public string CreateEmptyEventResponse()
        {
            string res = "<llsd><map><key>id</key><integer>" + eventQueueCount + "</integer>";
            res += "<key>events</key><array><map>";
            res += "</map></array>";
            res += "</map></llsd>";
            eventQueueCount++;
            return res;
        }

        public string NewAgentInventory(string request, string path, string param)
        {
            //Console.WriteLine("received upload request:"+ request);
            string res = "";
            LLUUID newAsset = LLUUID.Random();
            LLUUID newInvItem = LLUUID.Random();
            string uploaderPath = capsObjectPath + Util.RandomClass.Next(5000, 8000).ToString("0000");
            AssetUploader uploader = new AssetUploader(newAsset, newInvItem, uploaderPath, this.httpListener);
            httpListener.AddRestHandler("POST", "/CAPS/" + uploaderPath, uploader.uploaderCaps);
            string uploaderURL = "http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + uploaderPath;
            Console.WriteLine("uploader url is " + uploaderURL);
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
            Console.WriteLine("upload handler called");
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
                Encoding _enc = System.Text.Encoding.UTF8;
                byte[] data = _enc.GetBytes(request);
                //Console.WriteLine("recieved upload " + Util.FieldToString(data));
                LLUUID inv = this.inventoryItemID;
                string res = "";
                res += "<llsd><map>";
                res += "<key>new_asset</key><string>" + newAssetID.ToStringHyphenated() + "</string>";
                res += "<key>new_inventory_item</key><uuid>" + inv.ToStringHyphenated() + "</uuid>";
                res += "<key>state</key><string>complete</string>";
                res += "</map></llsd>";

                Console.WriteLine("asset " + newAssetID.ToStringHyphenated() + " , inventory item " + inv.ToStringHyphenated());
                httpListener.RemoveRestHandler("POST", "/CAPS/" + uploaderPath);
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
