using System;
using System.IO;
using System.Net;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Servers.HttpServer;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.ClientStack.Linden
{
    public delegate UUID UpdateItem(UUID itemID, UUID objectID, byte[] data);
    public delegate UUID ItemUpdatedCallback(UUID userID, UUID itemID, UUID objectID, byte[] data);

    public partial class BunchOfCaps
    {
        /// <summary>
        /// Called by the items updates handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public void UpdateInventoryItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            m_log.Debug("[CAPS]: UpdateInventoryItemAsset Request in region: " + m_regionName + "\n");

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            UUID itemID = UUID.Zero;
            UUID objectID = UUID.Zero;

            try
            {
                if (map.TryGetValue("item_id", out OSD itmp))
                    itemID = itmp;
                if (map.TryGetValue("task_id", out OSD tmp))
                    objectID = tmp;
            }
            catch { }

            if (itemID == UUID.Zero)
            {
                LLSDAssetUploadError error = new LLSDAssetUploadError();
                error.message = "failed to recode request";
                error.identifier = UUID.Zero;
                httpResponse.RawBuffer = Util.UTF8.GetBytes(LLSDHelpers.SerialiseLLSDReply(error));
                return;
            }

            if (objectID != UUID.Zero)
            {
                SceneObjectPart sop = m_Scene.GetSceneObjectPart(objectID);
                if (sop == null)
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "object not found";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                if (!m_Scene.Permissions.CanEditObjectInventory(objectID, m_AgentID))
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "No permissions to edit objec";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }
            }

            string uploaderPath = GetNewCapPath();

            ItemUpdater uploader = new ItemUpdater(itemID, objectID, uploaderPath, m_HostCapsObj.HttpListener, m_dumpAssetsToFile);
            uploader.OnUpLoad += ItemUpdated;

            m_HostCapsObj.HttpListener.AddStreamHandler(
                new BinaryStreamHandler(
                    "POST", uploaderPath, uploader.uploaderCaps, "UpdateInventoryItemAsset", null));

            string protocol = m_HostCapsObj.SSLCaps ? "https://" : "http://";

            string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + uploaderPath;

            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";

            // m_log.InfoFormat("[CAPS]: UpdateAgentInventoryAsset response: {0}",
            //                             LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

            httpResponse.RawBuffer = Util.UTF8.GetBytes(LLSDHelpers.SerialiseLLSDReply(uploadResponse));
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
        private UUID objectID;
        private IHttpServer httpListener;
        private bool m_dumpAssetToFile;

        public ItemUpdater(UUID inventoryItem, UUID objectid, string path, IHttpServer httpServer, bool dumpAssetToFile)
        {
            m_dumpAssetToFile = dumpAssetToFile;

            inventoryItemID = inventoryItem;
            objectID = objectid;
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
            httpListener.RemoveStreamHandler("POST", uploaderPath);

            UUID inv = inventoryItemID;
            string res = String.Empty;

            UUID assetID = UUID.Zero;
            handlerUpdateItem = OnUpLoad;
            if (handlerUpdateItem != null)
                assetID = handlerUpdateItem(inv, objectID, data);

            if (assetID == UUID.Zero)
            {
                LLSDAssetUploadError uperror = new LLSDAssetUploadError();
                uperror.message = "Failed to update inventory item asset";
                uperror.identifier = inv;
                res = LLSDHelpers.SerialiseLLSDReply(uperror);
            }
            else
            {
                LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                uploadComplete.new_asset = assetID.ToString();
                uploadComplete.new_inventory_item = inv;
                uploadComplete.state = "complete";
                res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
            }

            if (m_dumpAssetToFile)
            {
                SaveAssetToFile("updateditem" + Util.RandomClass.Next(1, 1000) + ".dat", data);
            }

            return res;
        }

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