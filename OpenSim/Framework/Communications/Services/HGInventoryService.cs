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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Clients;
using OpenSim.Framework.Communications.Cache;
using Caps = OpenSim.Framework.Capabilities.Caps;
using LLSDHelpers = OpenSim.Framework.Capabilities.LLSDHelpers;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Communications.Services
{ 
    public class HGInventoryService 
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private InventoryServiceBase m_inventoryService;
        IHttpServer httpServer;
        private string m_thisInventoryUrl = "http://localhost:9000";
        private string m_thisHostname = "127.0.0.1";
        private uint m_thisPort = 9000;

        // These two used for local access, standalone mode
        private UserManagerBase m_userService = null;
        IAssetService m_assetProvider = null;

        // These two used for remote access
        //string m_UserServerURL = string.Empty;
        string m_AssetServerURL = string.Empty;
        SynchronousGridAssetClient m_AssetClient = null;

        // Constructor for grid inventory server
        public HGInventoryService(InventoryServiceBase invService, string assetServiceURL, string userServiceURL, IHttpServer httpserver, string thisurl)
        {
            //m_UserServerURL = userServiceURL;
            m_AssetServerURL = assetServiceURL;

            m_AssetClient = new SynchronousGridAssetClient(m_AssetServerURL);

            Init(invService, thisurl, httpserver);
        }

        // Constructor for standalone mode
        public HGInventoryService(InventoryServiceBase invService, IAssetService assetService, UserManagerBase userService, IHttpServer httpserver, string thisurl)
        {
            m_userService = userService;
            m_assetProvider = assetService;

            Init(invService, thisurl, httpserver);
        }

        private void Init(InventoryServiceBase invService, string thisurl, IHttpServer httpserver)
        {
            m_inventoryService = invService;
            m_thisInventoryUrl = thisurl;
            if (!m_thisInventoryUrl.EndsWith("/"))
                m_thisInventoryUrl += "/";

            Uri uri = new Uri(m_thisInventoryUrl);
            if (uri != null)
            {
                m_thisHostname = uri.Host;
                m_thisPort = (uint)uri.Port;
            }

            httpServer = httpserver;

            AddHttpHandlers();
        }

        public virtual void AddHttpHandlers()
        {
            httpServer.AddHTTPHandler("/InvCap/", CapHandler);

            // Un-cap'ed for now
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<Guid, InventoryItemBase>(
                    "POST", "/GetItem/", GetInventoryItem, CheckAuthSession));

        }

        public InventoryItemBase GetInventoryItem(Guid id)
        {
            UUID itemID = new UUID(id);
            return m_inventoryService.GetInventoryItem(itemID);
        }

        public bool CheckAuthSession(string session_id, string avatar_id)
        {
            return true;
        }


        // In truth, this is not called from the outside, for standalones. I'm just making it
        // a handler already so that this can be reused for the InventoryServer.
        public string CreateCapUrl(Guid _userid)
        {
            UUID userID = new UUID(_userid);
            UUID random = UUID.Random();
            string url = m_thisInventoryUrl + random.ToString() + "/";
            m_log.InfoFormat("[HGStandaloneInvService] Creating Cap URL {0} for user {1}", url, userID.ToString());
            return url;
        }

        /// <summary>
        /// Return a user's entire inventory
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns>The user's inventory.  If an inventory cannot be found then an empty collection is returned.</returns>
        public InventoryCollection GetUserInventory(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.Info("[HGStandaloneInvModule]: Processing request for inventory of " + userID);

            // Uncomment me to simulate a slow responding inventory server
            //Thread.Sleep(16000);

            InventoryCollection invCollection = new InventoryCollection();

            List<InventoryFolderBase> allFolders = m_inventoryService.GetInventorySkeleton(userID);

            if (null == allFolders)
            {
                m_log.WarnFormat("[HGStandaloneInvModule]: No inventory found for user {0}", rawUserID);

                return invCollection;
            }

            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            foreach (InventoryFolderBase folder in allFolders)
            {
                List<InventoryItemBase> items = m_inventoryService.RequestFolderItems(folder.ID);

                if (items != null)
                {
                    allItems.InsertRange(0, items);
                }
            }

            invCollection.UserID = userID;
            invCollection.Folders = allFolders;
            invCollection.Items = allItems;

            //            foreach (InventoryFolderBase folder in invCollection.Folders)
            //            {
            //                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back folder {0} {1}", folder.Name, folder.ID);
            //            }
            //
            //            foreach (InventoryItemBase item in invCollection.Items)
            //            {
            //                m_log.DebugFormat("[GRID AGENT INVENTORY]: Sending back item {0} {1}, folder {2}", item.Name, item.ID, item.Folder);
            //            }

            m_log.InfoFormat(
                "[HGStandaloneInvModule]: Sending back inventory response to user {0} containing {1} folders and {2} items",
                invCollection.UserID, invCollection.Folders.Count, invCollection.Items.Count);

            return invCollection;
        }

        public InventoryCollection FetchDescendants(InventoryFolderBase fb)
        {
            m_log.Info("[HGStandaloneInvService]: Processing request for folder " + fb.ID);

            // Uncomment me to simulate a slow responding inventory server
            //Thread.Sleep(16000);

            InventoryCollection invCollection = new InventoryCollection();

            List<InventoryItemBase> items = m_inventoryService.RequestFolderItems(fb.ID);
            List<InventoryFolderBase> folders = m_inventoryService.RequestSubFolders(fb.ID);

            invCollection.UserID = fb.Owner;
            invCollection.Folders = folders;
            invCollection.Items = items;

            m_log.DebugFormat("[HGStandaloneInvService]: Found {0} items and {1} folders", items.Count, folders.Count);

            return invCollection;
        }

        public bool RemoveFolder(InventoryFolderBase folder)
        {
            m_log.Debug("[HGStandaloneInvService]: Removefolder: Operation not implemented yet.");
            return false;
        }

        public InventoryItemBase GetInventoryItem(InventoryItemBase item)
        {
            m_log.Info("[HGStandaloneInvService]: Get item " + item.ID);

            item = m_inventoryService.GetInventoryItem(item.ID);
            if (item == null)
                m_log.Debug("[HGStandaloneInvService]: null item");
            return item;
        }

        public InventoryItemBase AddItem(InventoryItemBase item)
        {
            m_log.DebugFormat("[HGStandaloneInvService]: Add item {0} from {1}", item.ID, item.Owner);
            if (m_inventoryService.AddItem(item))
                return item;
            else
            {
                item.ID = UUID.Zero;
                return item;
            }
        }

        public void AddUploadedInventoryItem(UUID agentID, InventoryItemBase item)
        {
            AddItem(item);
        }

        public InventoryItemBase UpdateItem(InventoryItemBase item)
        {
            m_log.DebugFormat("[HGStandaloneInvService]: Update item {0} from {1}", item.ID, item.Owner);
            InventoryItemBase it = m_inventoryService.GetInventoryItem(item.ID);
            item.CurrentPermissions = it.CurrentPermissions;
            item.AssetID = it.AssetID;
            if (m_inventoryService.UpdateItem(item))
                return item;
            else
            {
                item.ID = UUID.Zero;
                return item;
            }
        }

        public InventoryItemBase MoveItem(InventoryItemBase newitem)
        {
            m_log.DebugFormat("[HGStandaloneInvService]: Move item {0} from {1}", newitem.ID, newitem.Owner);
            InventoryItemBase Item = m_inventoryService.GetInventoryItem(newitem.ID);
            if (Item != null)
            {
                if (newitem.Name != String.Empty)
                {
                    Item.Name = newitem.Name;
                }
                Item.Folder = newitem.Folder;
                m_inventoryService.UpdateItem(Item);
                return Item;
            }
            else
            {
                m_log.Debug("[HGStandaloneInvService]: Failed to find item " + newitem.ID);
                newitem.ID = UUID.Zero;
                return newitem;
            }

        }

        public InventoryItemBase DeleteItem(InventoryItemBase item)
        {
            item = m_inventoryService.GetInventoryItem(item.ID);
            if (m_inventoryService.DeleteItem(item))
                return item;
            else
            {
                item.ID = UUID.Zero;
                return item;
            }
        }

        public InventoryItemBase CopyItem(InventoryItemBase olditem)
        {
            m_log.DebugFormat("[HGStandaloneInvService]: Copy item {0} from {1}", olditem.ID, olditem.Owner);
            InventoryItemBase Item = m_inventoryService.GetInventoryItem(olditem.ID); // this is the old item id
            // BIG HACK here
            UUID newID = olditem.AssetID;
            if (Item != null)
            {
                if (olditem.Name != String.Empty)
                {
                    Item.Name = olditem.Name;
                }
                Item.ID = newID;
                Item.Folder = olditem.Folder;
                Item.Owner = olditem.Owner;
                // There should be some tests here about the owner, etc but I'm going to ignore that
                // because I'm not sure it makes any sense
                // Also I should probably clone the asset...
                m_inventoryService.AddItem(Item);
                return Item;
            }
            else
            {
                m_log.Debug("[HGStandaloneInvService]: Failed to find item " + olditem.ID);
                olditem.ID = UUID.Zero;
                return olditem;
            }

        }

        /// <summary>
        /// Guid to UUID wrapper for same name IInventoryServices method
        /// </summary>
        /// <param name="rawUserID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);
            return m_inventoryService.GetInventorySkeleton(userID);
        }

        public List<InventoryItemBase> GetActiveGestures(Guid rawUserID)
        {
            UUID userID = new UUID(rawUserID);

            m_log.InfoFormat("[HGStandaloneInvService]: fetching active gestures for user {0}", userID);

            return m_inventoryService.GetActiveGestures(userID);
        }

        public AssetBase GetAsset(InventoryItemBase item)
        {
            m_log.Info("[HGStandaloneInvService]: Get asset " + item.AssetID + " for item " + item.ID);
            AssetBase asset = new AssetBase(item.AssetID, "NULL"); // send an asset with no data
            InventoryItemBase item2 = m_inventoryService.GetInventoryItem(item.ID);
            if (item2 == null)
            {
                m_log.Debug("[HGStandaloneInvService]: null item");
                return asset;
            }
            if (item2.Owner != item.Owner)
            {
                m_log.DebugFormat("[HGStandaloneInvService]: client with uuid {0} is trying to get an item of owner {1}", item.Owner, item2.Owner);
                return asset;
            }
            UUID assetID = item2.AssetID;
            if (assetID != item.AssetID)
            {
                m_log.WarnFormat("[HGStandaloneInvService]: asset IDs don't match {0}, {1}", item.AssetID, item2.AssetID);
            }

            // All good, get the asset
            //AssetBase theasset = m_assetProvider.FetchAsset(item.AssetID);
            AssetBase theasset = FetchAsset(assetID, (item.InvType == (int)InventoryType.Texture)); 

            m_log.Debug("[HGStandaloneInvService] Found asset " + ((theasset == null) ? "NULL" : "Not Null"));
            if (theasset != null)
            {
                asset = theasset;
                //m_log.Debug("  >> Sending assetID " + item.AssetID);
            }
            return asset;
        }

        public bool PostAsset(AssetBase asset)
        {
            m_log.Info("[HGStandaloneInvService]: Post asset " + asset.FullID);
            //m_assetProvider.CreateAsset(asset);
            StoreAsset(asset);
  
            return true;
        }

        public void PostAnAsset(AssetBase asset)
        {
            PostAsset(asset);
        }

        /// <summary>
        /// <see>CapsUpdatedInventoryItemAsset(IClientAPI, UUID, byte[])</see>
        /// </summary>
        public UUID UpdateInventoryItemAsset(UUID userID, UUID itemID, byte[] data)
        {
            m_log.Debug("[HGStandaloneInvService]: UpdateInventoryitemAsset for user " + userID + " item " + itemID);
            InventoryItemBase item = m_inventoryService.GetInventoryItem(itemID);

            if (item != null)
            {
                // We're still not dealing with permissions
                //if ((InventoryType)item.InvType == InventoryType.Notecard)
                //{
                //    if (!Permissions.CanEditNotecard(itemID, UUID.Zero, userID))
                //    {
                //        //remoteClient.SendAgentAlertMessage("Insufficient permissions to edit notecard", false);
                //        return UUID.Zero;
                //    }

                //    //remoteClient.SendAgentAlertMessage("Notecard saved", false);
                //}
                //else if ((InventoryType)item.InvType == InventoryType.LSL)
                //{
                //    if (!Permissions.CanEditScript(itemID, UUID.Zero, remoteClient.AgentId))
                //    {
                //        //remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                //        return UUID.Zero;
                //    }

                //    //remoteClient.SendAgentAlertMessage("Script saved", false);
                //}

                AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)item.AssetType, data);
                PostAsset(asset);

                item.AssetID = asset.FullID;
                item.Owner = userID;
                m_inventoryService.UpdateItem(item);

                return (asset.FullID);
            }
            return UUID.Zero;
        }

        private AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data)
        {
            AssetBase asset = new AssetBase();
            asset.Name = name;
            asset.Description = description;
            asset.Type = assetType;
            asset.FullID = UUID.Random();
            asset.Data = (data == null) ? new byte[1] : data;

            return asset;
        }

        #region Caps

        Dictionary<UUID, Hashtable> invCaps = new Dictionary<UUID, Hashtable>();

        public Hashtable CapHandler(Hashtable request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: InvCapHandler Called");

            m_log.Debug("---------------------------");
            m_log.Debug(" >> uri=" + request["uri"]);
            m_log.Debug(" >> content-type=" + request["content-type"]);
            m_log.Debug(" >> http-method=" + request["http-method"]);
            m_log.Debug("---------------------------\n");

            // these are requests if the type
            // http://inventoryserver/InvCap/uuuuuuuu-uuuu-uuuu-uuuu-uuuuuuuuuuuu/kkkkkkkk-kkkk-kkkk-kkkk-kkkkkkkkkkkk/

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/plain";

            UUID userID;
            string authToken = string.Empty;
            string authority = string.Empty;
            if (!GetParams(request, out userID, out authority, out authToken))
            {
                m_log.InfoFormat("[HGStandaloneInvService]: Invalid parameters for InvCap message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "Not found";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("GET"))
            {
                DoInvCapPost(request, responsedata, userID, authority, authToken);
                return responsedata;
            }
            //else if (method.Equals("DELETE"))
            //{
            //    DoAgentDelete(request, responsedata, agentID, action, regionHandle);

            //    return responsedata;
            //}
            else
            {
                m_log.InfoFormat("[HGStandaloneInvService]: method {0} not supported in agent message", method);
                responsedata["int_response_code"] = 405;
                responsedata["str_response_string"] = "Method not allowed";

                return responsedata;
            }

        }

        public virtual void DoInvCapPost(Hashtable request, Hashtable responsedata, UUID userID, string authority, string authToken)
        {

            // This is the meaning of POST agent

            // Check Auth Token
            if ((m_userService != null) && !(m_userService is IAuthentication))
            {
                m_log.Debug("[HGStandaloneInvService]: UserService is not IAuthentication. Denying access to inventory.");
                responsedata["int_response_code"] = 501;
                responsedata["str_response_string"] = "Not implemented";
                return;
            }

            bool success = VerifyKey(userID, authority, authToken);

            if (success)
            {

                m_log.DebugFormat("[HGStandaloneInvService]: User has been authorized. Creating service handlers.");
                
                // Then establish secret service handlers

                Hashtable usercaps = RegisterCaps(userID, authToken);

                responsedata["int_response_code"] = 200;
                //responsedata["str_response_string"] = "OK";
                responsedata["str_response_string"] = SerializeHashtable(usercaps);
            }
            else
            {
                m_log.DebugFormat("[HGStandaloneInvService]: User has is unauthorized. Denying service handlers.");
                responsedata["int_response_code"] = 403;
                responsedata["str_response_string"] = "Forbidden";
            }
        }


        /// <summary>
        /// Extract the params from a request.
        /// </summary>
        public static bool GetParams(Hashtable request, out UUID uuid, out string authority, out string authKey)
        {
            uuid = UUID.Zero;
            authority = string.Empty;
            authKey = string.Empty;

            string uri = (string)request["uri"];
            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if (parts.Length <= 1)
            {
                return false;
            }
            else
            {
                if (!UUID.TryParse(parts[1], out uuid))
                    return false;

                if (parts.Length >= 3)
                {
                    authKey = parts[2];
                    return true;
                }
            }

            Uri authUri;
            Hashtable headers = (Hashtable)request["headers"];

            // Authorization keys look like this:
            // http://orgrid.org:8002/<uuid>
            if (headers.ContainsKey("authorization"))
            {
                if (Uri.TryCreate((string)headers["authorization"], UriKind.Absolute, out authUri))
                {
                    authority = authUri.Authority;
                    authKey = authUri.PathAndQuery.Trim('/');
                    m_log.DebugFormat("[HGStandaloneInvService]: Got authority {0} and key {1}", authority, authKey);
                    return true;
                }
                else
                    m_log.Debug("[HGStandaloneInvService]: Wrong format for Authorization header: " + (string)headers["authorization"]);
            }
            else
                m_log.Debug("[HGStandaloneInvService]: Authorization header not found");

            return false;
        }

        string SerializeHashtable(Hashtable hash)
        {
            string result = string.Empty;
            foreach (object key in hash.Keys)
            {
                result += key.ToString() + "," + hash[key].ToString() + ";";
            }
            return result;
        }

        Hashtable RegisterCaps(UUID userID, string authToken)
        {
            lock (invCaps)
            {
                if (invCaps.ContainsKey(userID))
                {
                    // Remove the old ones
                    DeregisterCaps(httpServer, invCaps[userID]);
                    invCaps.Remove(userID);
                }
            }

            Caps caps = new Caps(null, httpServer, m_thisHostname, m_thisPort, authToken, userID, false, "Inventory");
            caps.RegisterInventoryServiceHandlers("/" + authToken + "/InventoryCap/");
            caps.ItemUpdatedCall = UpdateInventoryItemAsset;
            caps.AddNewInventoryItem = AddUploadedInventoryItem;
            caps.AddNewAsset = PostAnAsset;
            //caps.GetClient = 

            Hashtable capsHandlers = caps.CapsHandlers.CapsDetails;

            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<Guid, InventoryCollection>(
                                        "POST", AddAndGetCapUrl(authToken, "GetInventory", capsHandlers), GetUserInventory, CheckAuthSession));

            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, InventoryCollection>(
                                        "POST", AddAndGetCapUrl(authToken, "FetchDescendants", capsHandlers), FetchDescendants, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "NewFolder", capsHandlers), m_inventoryService.AddFolder, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "UpdateFolder", capsHandlers), m_inventoryService.UpdateFolder, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "MoveFolder", capsHandlers), m_inventoryService.MoveFolder, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "PurgeFolder", capsHandlers), m_inventoryService.PurgeFolder, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "RemoveFolder", capsHandlers), RemoveFolder, CheckAuthSession));

            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "GetItem", capsHandlers), GetInventoryItem, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "NewItem", capsHandlers), AddItem, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "UpdateItem", capsHandlers), UpdateItem, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "MoveItem", capsHandlers), MoveItem, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "DeleteItem", capsHandlers), DeleteItem, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                                        "POST", AddAndGetCapUrl(authToken, "CopyItem", capsHandlers), CopyItem, CheckAuthSession));

            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<InventoryItemBase, AssetBase>(
                                        "POST", AddAndGetCapUrl(authToken, "GetAsset", capsHandlers), GetAsset, CheckAuthSession));
            httpServer.AddStreamHandler(new RestDeserialiseSecureHandler<AssetBase, bool>(
                                        "POST", AddAndGetCapUrl(authToken, "PostAsset", capsHandlers), PostAsset, CheckAuthSession));

            lock (invCaps)
                invCaps.Add(userID, capsHandlers);

            return capsHandlers;
        }

        string AddAndGetCapUrl(string authToken, string capType, Hashtable caps)
        {
            string capUrl = "/" + authToken + "/" + capType + "/";

            m_log.Debug("[HGStandaloneInvService] Adding inventory cap " + capUrl);
            caps.Add(capType, capUrl);
            return capUrl;
        }

        void DeregisterCaps(IHttpServer httpServer, Hashtable caps)
        {
            foreach (string capUrl in caps.Values)
            {
                m_log.Debug("[HGStandaloneInvService] Removing inventory cap " + capUrl);
                httpServer.RemoveStreamHandler("POST", capUrl);
            }
        }

        #endregion Caps

        #region Local vs Remote

        bool VerifyKey(UUID userID, string authority, string key)
        {
            // Remote call to the Authorization server
            if (m_userService == null) 
                return AuthClient.VerifyKey("http://" + authority, userID, key);
            // local call
            else 
                return ((IAuthentication)m_userService).VerifyKey(userID, key);
        }

        AssetBase FetchAsset(UUID assetID, bool isTexture)
        {
            // Remote call to the Asset server
            if (m_assetProvider == null)
                return m_AssetClient.SyncGetAsset(assetID, isTexture);
            // local call
            else
                return m_assetProvider.Get(assetID.ToString());
        }

        void StoreAsset(AssetBase asset)
        {
            // Remote call to the Asset server
            if (m_assetProvider == null)
                m_AssetClient.StoreAsset(asset);
            // local call
            else
                m_assetProvider.Store(asset);
        }

        #endregion Local vs Remote
    }

    class SynchronousGridAssetClient : GridAssetClient
    {
        public SynchronousGridAssetClient(string url)
            : base(url)
        {
        }

        public AssetBase SyncGetAsset(UUID assetID, bool isTexture)
        {
            AssetRequest assReq = new AssetRequest();
            assReq.AssetID = assetID;
            assReq.IsTexture = isTexture;
            return base.GetAsset(assReq);
        }

    }
}
