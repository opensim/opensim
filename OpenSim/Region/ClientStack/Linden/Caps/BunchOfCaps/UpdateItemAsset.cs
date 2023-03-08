using System;
using System.Collections;

using System.IO;
using System.Net;
using System.Text;
using System.Threading;

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
        public void UpdateNotecardItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            UpdateInventoryItemAsset(httpRequest, httpResponse, map, (byte)AssetType.Notecard);
        }

        public void UpdateAnimSetItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            //UpdateInventoryItemAsset(httpRequest, httpResponse, map, CustomInventoryType.AnimationSet);
        }

        public void UpdateScriptItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            UpdateInventoryItemAsset(httpRequest, httpResponse, map, (byte)AssetType.LSLText);
        }

        public void UpdateSettingsItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            UpdateInventoryItemAsset(httpRequest, httpResponse, map, (byte)AssetType.Settings);
        }

        public void UpdateMaterialItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            UpdateInventoryItemAsset(httpRequest, httpResponse, map, (byte)AssetType.Material);
        }
         
        public void UpdateGestureItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            UpdateInventoryItemAsset(httpRequest, httpResponse, map, (byte)AssetType.Gesture);
        }

        private void UpdateInventoryItemAsset(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map, byte atype, bool taskSript = false)
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

            if (itemID.IsZero())
            {
                LLSDAssetUploadError error = new LLSDAssetUploadError();
                error.message = "failed to recode request";
                error.identifier = UUID.Zero;
                httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                return;
            }

            if (!objectID.IsZero())
            {
                SceneObjectPart sop = m_Scene.GetSceneObjectPart(objectID);
                if (sop == null)
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "object not found";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                if (!m_Scene.Permissions.CanEditObjectInventory(objectID, m_AgentID))
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "No permissions to edit objec";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }
            }

            string uploaderPath = GetNewCapPath();

            string protocol = m_HostCapsObj.SSLCaps ? "https://" : "http://";
            string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + uploaderPath;
            LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
            uploadResponse.uploader = uploaderURL;
            uploadResponse.state = "upload";

            ItemUpdater uploader = new ItemUpdater(itemID, objectID, atype, uploaderPath, m_HostCapsObj.HttpListener, m_dumpAssetsToFile);
            uploader.m_remoteAdress = httpRequest.RemoteIPEndPoint.Address;

            uploader.OnUpLoad += ItemUpdated;

            var uploaderHandler = new SimpleBinaryHandler("POST", uploaderPath, uploader.process);

            uploaderHandler.MaxDataSize = 10000000; // change per asset type?
            
            m_HostCapsObj.HttpListener.AddSimpleStreamHandler(uploaderHandler);

            // m_log.InfoFormat("[CAPS]: UpdateAgentInventoryAsset response: {0}",
            //                             LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

            httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(uploadResponse));
        }

        /// <summary>
        /// Called when new asset data for an inventory item update has been uploaded.
        /// </summary>
        /// <param name="itemID">Item to update</param>
        /// <param name="data">New asset data</param>
        /// <returns></returns>
        public UUID ItemUpdated(UUID itemID, UUID objectID, byte[] data)
        {
            if (ItemUpdatedCall != null)
            {
                return ItemUpdatedCall(m_HostCapsObj.AgentID, itemID, objectID, data);
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Called by the script task update handler.  Provides a URL to which the client can upload a new asset.
        /// </summary>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns></returns>
        public void UpdateScriptTaskInventory(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
        {
            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            try
            {
                //m_log.Debug("[CAPS]: ScriptTaskInventory Request in region: " + m_regionName);
                //m_log.DebugFormat("[CAPS]: request: {0}, path: {1}, param: {2}", request, path, param);

                UUID itemID = UUID.Zero;
                UUID objectID = UUID.Zero;
                bool is_script_running = false;
                OSD tmp;
                try
                {
                    if (map.TryGetValue("item_id", out tmp))
                        itemID = tmp;
                    if (map.TryGetValue("task_id", out tmp))
                        objectID = tmp;
                    if (map.TryGetValue("is_script_running", out tmp))
                        is_script_running = tmp;
                }
                catch { }

                if (itemID.IsZero() || objectID.IsZero())
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "failed to recode request";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(objectID);
                if (sop == null)
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "object not found";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                if (!m_Scene.Permissions.CanEditObjectInventory(objectID, m_AgentID))
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "No permissions to edit objec";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                if (!m_Scene.Permissions.CanEditScript(itemID, objectID, m_AgentID))
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "No permissions to edit script";
                    error.identifier = UUID.Zero;
                    httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }

                string uploaderPath = GetNewCapPath();
                string protocol = m_HostCapsObj.SSLCaps ? "https://" : "http://";
                string uploaderURL = protocol + m_HostCapsObj.HostName + ":" + m_HostCapsObj.Port.ToString() + uploaderPath;
                LLSDAssetUploadResponse uploadResponse = new LLSDAssetUploadResponse();
                uploadResponse.uploader = uploaderURL;
                uploadResponse.state = "upload";

                TaskInventoryScriptUpdater uploader = new TaskInventoryScriptUpdater(itemID, objectID, is_script_running,
                        uploaderPath, m_HostCapsObj.HttpListener, httpRequest.RemoteIPEndPoint.Address, m_dumpAssetsToFile);
                uploader.OnUpLoad += TaskScriptUpdated;

                var uploaderHandler = new SimpleBinaryHandler("POST", uploaderPath, uploader.process);

                uploaderHandler.MaxDataSize = 10000000; // change per asset type?

                m_HostCapsObj.HttpListener.AddSimpleStreamHandler(uploaderHandler);

                // m_log.InfoFormat("[CAPS]: " +
                //    "ScriptTaskInventory response: {0}",
                //       LLSDHelpers.SerialiseLLSDReply(uploadResponse)));

                httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(uploadResponse));
            }
            catch (Exception e)
            {
                m_log.Error("[UpdateScriptTaskInventory]: " + e.ToString());
            }
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

        static public bool ValidateAssetData(byte assetType, byte[] data)
        {
            return true;
        }

        /// <summary>
        /// This class is a callback invoked when a client sends asset data to
        /// an agent inventory notecard update url
        /// </summary>
        public class ItemUpdater : ExpiringCapBase
        {
            public event UpdateItem OnUpLoad = null;
            private UUID m_inventoryItemID;
            private UUID m_objectID;
            private bool m_dumpAssetToFile;
            public IPAddress m_remoteAdress;
            private byte m_assetType;

            public ItemUpdater(UUID inventoryItem, UUID objectid, byte aType, string path, IHttpServer httpServer, bool dumpAssetToFile):
                base(httpServer, path)
            {
                m_dumpAssetToFile = dumpAssetToFile;

                m_inventoryItemID = inventoryItem;
                m_objectID = objectid;
                m_httpListener = httpServer;
                m_assetType = aType;

                Start(30000);
            }

            /// <summary>
            /// Handle raw uploaded asset data.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public void process(IOSHttpRequest request, IOSHttpResponse response, byte[] data)
            {
                Stop();

                if (!request.RemoteIPEndPoint.Address.Equals(m_remoteAdress))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                string res = String.Empty;

                if (OnUpLoad == null)
                {
                    response.StatusCode = (int)HttpStatusCode.Gone;
                    return;
                }

                if (!BunchOfCaps.ValidateAssetData(m_assetType, data))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                UUID assetID = OnUpLoad(m_inventoryItemID, m_objectID, data);

                if (assetID.IsZero())
                {
                    LLSDAssetUploadError uperror = new LLSDAssetUploadError();
                    uperror.message = "Failed to update inventory item asset";
                    uperror.identifier = m_inventoryItemID;
                    res = LLSDHelpers.SerialiseLLSDReply(uperror);
                }
                else
                {
                    LLSDAssetUploadComplete uploadComplete = new LLSDAssetUploadComplete();
                    uploadComplete.new_asset = assetID.ToString();
                    uploadComplete.new_inventory_item = m_inventoryItemID;
                    uploadComplete.state = "complete";
                    res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);
                }

                if (m_dumpAssetToFile)
                {
                    Util.SaveAssetToFile("updateditem" + Random.Shared.Next(1, 1000) + ".dat", data);
                }

                response.StatusCode = (int)HttpStatusCode.OK;
                response.RawBuffer = Util.UTF8NBGetbytes(res);
            }
        }

        /// <summary>
        /// This class is a callback invoked when a client sends asset data to
        /// a task inventory script update url
        /// </summary>
        public class TaskInventoryScriptUpdater : ExpiringCapBase
        {
            public event UpdateTaskScript OnUpLoad;
            private UUID m_inventoryItemID;
            private UUID m_primID;
            private bool m_isScriptRunning;
            private bool m_dumpAssetToFile;
            public IPAddress m_remoteAddress;

            public TaskInventoryScriptUpdater(UUID inventoryItemID, UUID primID, bool isScriptRunning,
                                                string path, IHttpServer httpServer, IPAddress address,
                                                bool dumpAssetToFile) : base(httpServer, path)
            {
                m_dumpAssetToFile = dumpAssetToFile;
                m_inventoryItemID = inventoryItemID;
                m_primID = primID;
                m_isScriptRunning = isScriptRunning;
                m_remoteAddress = address;
                Start(30000);
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public void process(IOSHttpRequest request, IOSHttpResponse response, byte[] data)
            {
                Stop();

                if (!request.RemoteIPEndPoint.Address.Equals(m_remoteAddress))
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                if (OnUpLoad == null)
                {
                    response.StatusCode = (int)HttpStatusCode.Gone;
                    return;
                }

                if (!BunchOfCaps.ValidateAssetData((byte)AssetType.LSLText, data))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                response.StatusCode = (int)HttpStatusCode.OK;

                try
                {
                    string res = String.Empty;
                    LLSDTaskScriptUploadComplete uploadComplete = new LLSDTaskScriptUploadComplete();

                    ArrayList errors = new ArrayList();
                    OnUpLoad?.Invoke(m_inventoryItemID, m_primID, m_isScriptRunning, data, ref errors);

                    uploadComplete.new_asset = m_inventoryItemID;
                    uploadComplete.compiled = errors.Count > 0 ? false : true;
                    uploadComplete.state = "complete";
                    uploadComplete.errors = new OpenSim.Framework.Capabilities.OSDArray();
                    uploadComplete.errors.Array = errors;

                    res = LLSDHelpers.SerialiseLLSDReply(uploadComplete);

                    if (m_dumpAssetToFile)
                    {
                        Util.SaveAssetToFile("updatedtaskscript" + Random.Shared.Next(1, 1000) + ".dat", data);
                    }

                    // m_log.InfoFormat("[CAPS]: TaskInventoryScriptUpdater.uploaderCaps res: {0}", res);
                    response.RawBuffer = Util.UTF8NBGetbytes(res);
                }
                catch
                {
                    LLSDAssetUploadError error = new LLSDAssetUploadError();
                    error.message = "could not compile script";
                    error.identifier = UUID.Zero;
                    response.RawBuffer = Util.UTF8NBGetbytes(LLSDHelpers.SerialiseLLSDReply(error));
                    return;
                }
            }
        }
    }
}