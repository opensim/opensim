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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

using OpenSim.Framework.Monitoring;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class XInventoryServicesConnector : BaseServiceConnector, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Number of requests made to the remote inventory service.
        /// </summary>
        public int RequestsMade { get; private set; }

        private string m_ServerURI = String.Empty;

        private int m_maxRetries = 0;

        /// <summary>
        /// Timeout for remote requests.
        /// </summary>
        /// <remarks>
        /// In this case, -1 is default timeout (100 seconds), not infinite.
        /// </remarks>
        private int m_requestTimeoutSecs = -1;
        private string m_configName = "InventoryService";

        private const double CACHE_EXPIRATION_SECONDS = 20.0;
        private static ExpiringCache<UUID, InventoryItemBase> m_ItemCache = new ExpiringCache<UUID,InventoryItemBase>();

        public XInventoryServicesConnector()
        {
        }

        public XInventoryServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public XInventoryServicesConnector(IConfigSource source, string configName)
            : base(source, configName)
        {
            m_configName = configName;
            Initialise(source);
        }

        public XInventoryServicesConnector(IConfigSource source)
            : base(source, "InventoryService")
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs[m_configName];
            if (config == null)
            {
                m_log.ErrorFormat("[INVENTORY CONNECTOR]: {0} missing from OpenSim.ini", m_configName);
                throw new Exception("Inventory connector init error");
            }

            string serviceURI = config.GetString("InventoryServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[INVENTORY CONNECTOR]: No Server URI named in section InventoryService");
                throw new Exception("Inventory connector init error");
            }
            m_ServerURI = serviceURI;

            m_requestTimeoutSecs = config.GetInt("RemoteRequestTimeout", m_requestTimeoutSecs);
            m_maxRetries = config.GetInt("MaxRetries", m_maxRetries);

            StatsManager.RegisterStat(
                new Stat(
                "RequestsMade",
                "Requests made",
                "Number of requests made to the remove inventory service",
                "requests",
                "inventory",
                serviceURI,
                StatType.Pull,
                MeasuresOfInterest.AverageChangeOverTime,
                s => s.Value = RequestsMade,
                StatVerbosity.Debug));
        }

        private bool CheckReturn(Dictionary<string, object> ret)
        {
            if (ret == null)
                return false;

            if (ret.Count == 0)
                return false;

            if (ret.ContainsKey("RESULT"))
            {
                if (ret["RESULT"] is string)
                {
                    bool result;

                    if (bool.TryParse((string)ret["RESULT"], out result))
                        return result;

                    return false;
                }
            }

            return true;
        }

        public bool CreateUserInventory(UUID principalID)
        {
            Dictionary<string,object> ret = MakeRequest("CREATEUSERINVENTORY",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() }
                    });

            return CheckReturn(ret);
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID principalID)
        {
            Dictionary<string,object> ret = MakeRequest("GETINVENTORYSKELETON",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() }
                    });

            if (!CheckReturn(ret))
                return null;

            Dictionary<string, object> folders = (Dictionary<string, object>)ret["FOLDERS"];

            List<InventoryFolderBase> fldrs = new List<InventoryFolderBase>();

            try
            {
                foreach (Object o in folders.Values)
                    fldrs.Add(BuildFolder((Dictionary<string, object>)o));
            }
            catch (Exception e)
            {
                m_log.Error("[XINVENTORY SERVICES CONNECTOR]: Exception unwrapping folder list: ", e);
            }

            return fldrs;
        }

        public InventoryFolderBase GetRootFolder(UUID principalID)
        {
            Dictionary<string,object> ret = MakeRequest("GETROOTFOLDER",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() }
                    });

            if (!CheckReturn(ret))
                return null;

            return BuildFolder((Dictionary<string, object>)ret["folder"]);
        }

        public InventoryFolderBase GetFolderForType(UUID principalID, FolderType type)
        {
            Dictionary<string,object> ret = MakeRequest("GETFOLDERFORTYPE",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "TYPE", ((int)type).ToString() }
                    });

            if (!CheckReturn(ret))
                return null;

            return BuildFolder((Dictionary<string, object>)ret["folder"]);
        }

        public InventoryCollection GetFolderContent(UUID principalID, UUID folderID)
        {
            InventoryCollection inventory = new InventoryCollection();
            inventory.Folders = new List<InventoryFolderBase>();
            inventory.Items = new List<InventoryItemBase>();
            inventory.OwnerID = principalID;

            try
            {
                Dictionary<string,object> ret = MakeRequest("GETFOLDERCONTENT",
                        new Dictionary<string,object> {
                            { "PRINCIPAL", principalID.ToString() },
                            { "FOLDER", folderID.ToString() }
                        });

                if (!CheckReturn(ret))
                    return null;

                Dictionary<string,object> folders = ret.ContainsKey("FOLDERS") ?
                    (Dictionary<string,object>)ret["FOLDERS"] : null;
                Dictionary<string,object> items = ret.ContainsKey("ITEMS") ?
                    (Dictionary<string, object>)ret["ITEMS"] : null;

                if (folders != null)
                    foreach (Object o in folders.Values) // getting the values directly, we don't care about the keys folder_i
                        inventory.Folders.Add(BuildFolder((Dictionary<string, object>)o));
                if (items != null)
                    foreach (Object o in items.Values) // getting the values directly, we don't care about the keys item_i
                        inventory.Items.Add(BuildItem((Dictionary<string, object>)o));
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: Exception in GetFolderContent: {0}", e.Message);
            }

            return inventory;
        }

        public virtual InventoryCollection[] GetMultipleFoldersContent(UUID principalID, UUID[] folderIDs)
        {
            InventoryCollection[] inventoryArr = new InventoryCollection[folderIDs.Length];
            // m_log.DebugFormat("[XXX]: In GetMultipleFoldersContent {0}", String.Join(",", folderIDs));
            try
            {
                Dictionary<string, object> resultSet = MakeRequest("GETMULTIPLEFOLDERSCONTENT",
                        new Dictionary<string, object> {
                            { "PRINCIPAL", principalID.ToString() },
                            { "FOLDERS", String.Join(",", folderIDs) },
                            { "COUNT", folderIDs.Length.ToString() }
                        });

                if (!CheckReturn(resultSet))
                    return null;

                int i = 0;
                foreach (KeyValuePair<string, object> kvp in resultSet)
                {
                    InventoryCollection inventory = new InventoryCollection();
                    if (kvp.Key.StartsWith("F_"))
                    {
                        UUID fid = UUID.Zero;
                        if (UUID.TryParse(kvp.Key.Substring(2), out fid) && fid == folderIDs[i])
                        {
                            inventory.Folders = new List<InventoryFolderBase>();
                            inventory.Items = new List<InventoryItemBase>();

                            Dictionary<string, object> ret = (Dictionary<string, object>)kvp.Value;

                            if (ret.ContainsKey("FID"))
                            {
                                if (!UUID.TryParse(ret["FID"].ToString(), out inventory.FolderID))
                                    m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: Could not parse folder id {0}", ret["FID"].ToString());
                            }
                            else
                                m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: FID key not present in response");

                            inventory.Version = -1;
                            if (ret.ContainsKey("VERSION"))
                                Int32.TryParse(ret["VERSION"].ToString(), out inventory.Version);
                            if (ret.ContainsKey("OWNER"))
                                UUID.TryParse(ret["OWNER"].ToString(), out inventory.OwnerID);

                            //m_log.DebugFormat("[XXX]: Received {0} ({1}) {2} {3}", inventory.FolderID, fid, inventory.Version, inventory.OwnerID);

                            Dictionary<string, object> folders =
                                    (Dictionary<string, object>)ret["FOLDERS"];
                            Dictionary<string, object> items =
                                    (Dictionary<string, object>)ret["ITEMS"];

                            foreach (Object o in folders.Values) // getting the values directly, we don't care about the keys folder_i
                            {
                                inventory.Folders.Add(BuildFolder((Dictionary<string, object>)o));
                            }
                            foreach (Object o in items.Values) // getting the values directly, we don't care about the keys item_i
                            {
                                inventory.Items.Add(BuildItem((Dictionary<string, object>)o));
                            }

                            inventoryArr[i] = inventory;
                        }
                        else
                        {
                            m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: Folder id does not match. Expected {0} got {1}",
                                folderIDs[i], fid);
                            m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: {0} {1}", String.Join(",", folderIDs), String.Join(",", resultSet.Keys));
                        }

                        i += 1;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: Exception in GetMultipleFoldersContent: {0}", e.Message);
            }

            return inventoryArr;
        }

        public List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        {
            Dictionary<string,object> ret = MakeRequest("GETFOLDERITEMS",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "FOLDER", folderID.ToString() }
                    });

            if (!CheckReturn(ret))
                return null;

            Dictionary<string, object> items = (Dictionary<string, object>)ret["ITEMS"];
            List<InventoryItemBase> fitems = new List<InventoryItemBase>();
            foreach (Object o in items.Values) // getting the values directly, we don't care about the keys item_i
                fitems.Add(BuildItem((Dictionary<string, object>)o));

            return fitems;
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            Dictionary<string,object> ret = MakeRequest("ADDFOLDER",
                    new Dictionary<string,object> {
                        { "ParentID", folder.ParentID.ToString() },
                        { "Type", folder.Type.ToString() },
                        { "Version", folder.Version.ToString() },
                        { "Name", folder.Name.ToString() },
                        { "Owner", folder.Owner.ToString() },
                        { "ID", folder.ID.ToString() }
                    });

            return CheckReturn(ret);
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            Dictionary<string,object> ret = MakeRequest("UPDATEFOLDER",
                    new Dictionary<string,object> {
                        { "ParentID", folder.ParentID.ToString() },
                        { "Type", folder.Type.ToString() },
                        { "Version", folder.Version.ToString() },
                        { "Name", folder.Name.ToString() },
                        { "Owner", folder.Owner.ToString() },
                        { "ID", folder.ID.ToString() }
                    });

            return CheckReturn(ret);
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            Dictionary<string,object> ret = MakeRequest("MOVEFOLDER",
                    new Dictionary<string,object> {
                        { "ParentID", folder.ParentID.ToString() },
                        { "ID", folder.ID.ToString() },
                        { "PRINCIPAL", folder.Owner.ToString() }
                    });

            return CheckReturn(ret);
        }

        public bool DeleteFolders(UUID principalID, List<UUID> folderIDs)
        {
            List<string> slist = new List<string>();

            foreach (UUID f in folderIDs)
                slist.Add(f.ToString());

            Dictionary<string,object> ret = MakeRequest("DELETEFOLDERS",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "FOLDERS", slist }
                    });

            return CheckReturn(ret);
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            Dictionary<string,object> ret = MakeRequest("PURGEFOLDER",
                    new Dictionary<string,object> {
                        { "ID", folder.ID.ToString() }
                    });

            return CheckReturn(ret);
        }

        public bool AddItem(InventoryItemBase item)
        {
            if (item.Description == null)
                item.Description = String.Empty;
            if (item.CreatorData == null)
                item.CreatorData = String.Empty;
            if (item.CreatorId == null)
                item.CreatorId = String.Empty;
            Dictionary<string, object> ret = MakeRequest("ADDITEM",
                    new Dictionary<string,object> {
                        { "AssetID", item.AssetID.ToString() },
                        { "AssetType", item.AssetType.ToString() },
                        { "Name", item.Name.ToString() },
                        { "Owner", item.Owner.ToString() },
                        { "ID", item.ID.ToString() },
                        { "InvType", item.InvType.ToString() },
                        { "Folder", item.Folder.ToString() },
                        { "CreatorId", item.CreatorId.ToString() },
                        { "CreatorData", item.CreatorData.ToString() },
                        { "Description", item.Description.ToString() },
                        { "NextPermissions", item.NextPermissions.ToString() },
                        { "CurrentPermissions", item.CurrentPermissions.ToString() },
                        { "BasePermissions", item.BasePermissions.ToString() },
                        { "EveryOnePermissions", item.EveryOnePermissions.ToString() },
                        { "GroupPermissions", item.GroupPermissions.ToString() },
                        { "GroupID", item.GroupID.ToString() },
                        { "GroupOwned", item.GroupOwned.ToString() },
                        { "SalePrice", item.SalePrice.ToString() },
                        { "SaleType", item.SaleType.ToString() },
                        { "Flags", item.Flags.ToString() },
                        { "CreationDate", item.CreationDate.ToString() }
                    });

            return CheckReturn(ret);
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            if (item.CreatorData == null)
                item.CreatorData = String.Empty;
            Dictionary<string,object> ret = MakeRequest("UPDATEITEM",
                    new Dictionary<string,object> {
                        { "AssetID", item.AssetID.ToString() },
                        { "AssetType", item.AssetType.ToString() },
                        { "Name", item.Name.ToString() },
                        { "Owner", item.Owner.ToString() },
                        { "ID", item.ID.ToString() },
                        { "InvType", item.InvType.ToString() },
                        { "Folder", item.Folder.ToString() },
                        { "CreatorId", item.CreatorId.ToString() },
                        { "CreatorData", item.CreatorData.ToString() },
                        { "Description", item.Description.ToString() },
                        { "NextPermissions", item.NextPermissions.ToString() },
                        { "CurrentPermissions", item.CurrentPermissions.ToString() },
                        { "BasePermissions", item.BasePermissions.ToString() },
                        { "EveryOnePermissions", item.EveryOnePermissions.ToString() },
                        { "GroupPermissions", item.GroupPermissions.ToString() },
                        { "GroupID", item.GroupID.ToString() },
                        { "GroupOwned", item.GroupOwned.ToString() },
                        { "SalePrice", item.SalePrice.ToString() },
                        { "SaleType", item.SaleType.ToString() },
                        { "Flags", item.Flags.ToString() },
                        { "CreationDate", item.CreationDate.ToString() }
                    });

            bool result = CheckReturn(ret);
            if (result)
            {
                m_ItemCache.AddOrUpdate(item.ID, item, CACHE_EXPIRATION_SECONDS);
            }

            return result;
        }

        public bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        {
            List<string> idlist = new List<string>();
            List<string> destlist = new List<string>();

            foreach (InventoryItemBase item in items)
            {
                idlist.Add(item.ID.ToString());
                destlist.Add(item.Folder.ToString());
            }

            Dictionary<string,object> ret = MakeRequest("MOVEITEMS",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "IDLIST", idlist },
                        { "DESTLIST", destlist }
                    });

            return CheckReturn(ret);
        }

        public bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        {
            List<string> slist = new List<string>();

            foreach (UUID f in itemIDs)
                slist.Add(f.ToString());

            Dictionary<string,object> ret = MakeRequest("DELETEITEMS",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "ITEMS", slist }
                    });

            return CheckReturn(ret);
        }

        public InventoryItemBase GetItem(UUID principalID, UUID itemID)
        {
            InventoryItemBase retrieved = null;
            if (m_ItemCache.TryGetValue(itemID, out retrieved))
            {
                return retrieved;
            }

            try
            {
                Dictionary<string, object> ret = MakeRequest("GETITEM",
                        new Dictionary<string, object> {
                        { "ID", itemID.ToString() },
                        { "PRINCIPAL", principalID.ToString() }
                    });

                if (!CheckReturn(ret))
                    return null;

                retrieved = BuildItem((Dictionary<string, object>)ret["item"]);
            }
            catch (Exception e)
            {
                m_log.Error("[XINVENTORY SERVICES CONNECTOR]: Exception in GetItem: ", e);
            }

            m_ItemCache.AddOrUpdate(itemID, retrieved, CACHE_EXPIRATION_SECONDS);

            return retrieved;
        }

        public virtual InventoryItemBase[] GetMultipleItems(UUID principalID, UUID[] itemIDs)
        {
            //m_log.DebugFormat("[XXX]: In GetMultipleItems {0}", String.Join(",", itemIDs));

            InventoryItemBase[] itemArr = new InventoryItemBase[itemIDs.Length];
            // Try to get them from the cache
            List<UUID> pending = new List<UUID>();
            InventoryItemBase item = null;
            int i = 0;

            foreach (UUID id in itemIDs)
            {
                if (m_ItemCache.TryGetValue(id, out item))
                    itemArr[i++] = item;
                else
                    pending.Add(id);
            }

            if (pending.Count == 0) // we're done, everything was in the cache
                return itemArr;

            try
            {
                Dictionary<string, object> resultSet = MakeRequest("GETMULTIPLEITEMS",
                        new Dictionary<string, object> {
                            { "PRINCIPAL", principalID.ToString() },
                            { "ITEMS", String.Join(",", pending.ToArray()) },
                            { "COUNT", pending.Count.ToString() }
                        });

                if (!CheckReturn(resultSet))
                {
                    if (i == 0)
                        return null;
                    else
                        return itemArr;
                }

                // carry over index i where we left above
                foreach (KeyValuePair<string, object> kvp in resultSet)
                {
                    InventoryCollection inventory = new InventoryCollection();
                    if (kvp.Key.StartsWith("item_"))
                    {
                        if (kvp.Value is Dictionary<string, object>)
                        {
                            item = BuildItem((Dictionary<string, object>)kvp.Value);
                            m_ItemCache.AddOrUpdate(item.ID, item, CACHE_EXPIRATION_SECONDS);
                            itemArr[i++] = item;
                        }
                        else
                            itemArr[i++] = null;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[XINVENTORY SERVICES CONNECTOR]: Exception in GetMultipleItems: {0}", e.Message);
            }

            return itemArr;
        }

        public InventoryFolderBase GetFolder(UUID principalID, UUID folderID)
        {
            try
            {
                Dictionary<string, object> ret = MakeRequest("GETFOLDER",
                        new Dictionary<string, object> {
                        { "ID", folderID.ToString() },
                        { "PRINCIPAL", principalID.ToString() }
                    });

                if (!CheckReturn(ret))
                    return null;

                return BuildFolder((Dictionary<string, object>)ret["folder"]);
            }
            catch (Exception e)
            {
                m_log.Error("[XINVENTORY SERVICES CONNECTOR]: Exception in GetFolder: ", e);
            }

            return null;
        }

        public List<InventoryItemBase> GetActiveGestures(UUID principalID)
        {
            Dictionary<string,object> ret = MakeRequest("GETACTIVEGESTURES",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() }
                    });

            if (!CheckReturn(ret))
                return null;

            List<InventoryItemBase> items = new List<InventoryItemBase>();

            foreach (Object o in ((Dictionary<string,object>)ret["ITEMS"]).Values)
                items.Add(BuildItem((Dictionary<string, object>)o));

            return items;
        }

        public int GetAssetPermissions(UUID principalID, UUID assetID)
        {
            Dictionary<string,object> ret = MakeRequest("GETASSETPERMISSIONS",
                    new Dictionary<string,object> {
                        { "PRINCIPAL", principalID.ToString() },
                        { "ASSET", assetID.ToString() }
                    });

            // We cannot use CheckReturn() here because valid values for RESULT are "false" (in the case of request failure) or an int
            if (ret == null)
                return 0;

            if (ret.ContainsKey("RESULT"))
            {
                if (ret["RESULT"] is string)
                {
                    int intResult;

                    if (int.TryParse ((string)ret["RESULT"], out intResult))
                        return intResult;
                }
            }

            return 0;
        }

        public bool HasInventoryForUser(UUID principalID)
        {
            return false;
        }

        // Helpers
        //
        private Dictionary<string,object> MakeRequest(string method,
                Dictionary<string,object> sendData)
        {
            // Add "METHOD" as the first key in the dictionary. This ensures that it will be
            // visible even when using partial logging ("debug http all 5").
            Dictionary<string, object> temp = sendData;
            sendData = new Dictionary<string,object>{ { "METHOD", method } };
            foreach (KeyValuePair<string, object> kvp in temp)
                sendData.Add(kvp.Key, kvp.Value);

            RequestsMade++;

            string reply = String.Empty;
            int retries = 0;

            do
            {
                reply = SynchronousRestFormsRequester.MakeRequest(
                    "POST", m_ServerURI + "/xinventory",
                     ServerUtils.BuildQueryString(sendData), m_requestTimeoutSecs, m_Auth);

                if (reply != String.Empty)
                    break;

                retries++;
            } while (retries <= m_maxRetries);

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(
                    reply);

            return replyData;
        }

        private InventoryFolderBase BuildFolder(Dictionary<string,object> data)
        {
            InventoryFolderBase folder = new InventoryFolderBase();

            try
            {
                folder.ParentID = new UUID(data["ParentID"].ToString());
                folder.Type = short.Parse(data["Type"].ToString());
                folder.Version = ushort.Parse(data["Version"].ToString());
                folder.Name = data["Name"].ToString();
                folder.Owner = new UUID(data["Owner"].ToString());
                folder.ID = new UUID(data["ID"].ToString());
            }
            catch (Exception e)
            {
                m_log.Error("[XINVENTORY SERVICES CONNECTOR]: Exception building folder: ", e);
            }

            return folder;
        }

        private InventoryItemBase BuildItem(Dictionary<string,object> data)
        {
            InventoryItemBase item = new InventoryItemBase();

            try
            {
                item.AssetID = new UUID(data["AssetID"].ToString());
                item.AssetType = int.Parse(data["AssetType"].ToString());
                item.Name = data["Name"].ToString();
                item.Owner = new UUID(data["Owner"].ToString());
                item.ID = new UUID(data["ID"].ToString());
                item.InvType = int.Parse(data["InvType"].ToString());
                item.Folder = new UUID(data["Folder"].ToString());
                item.CreatorId = data["CreatorId"].ToString();
                if (data.ContainsKey("CreatorData"))
                    item.CreatorData = data["CreatorData"].ToString();
                else
                    item.CreatorData = String.Empty;
                item.Description = data["Description"].ToString();
                item.NextPermissions = uint.Parse(data["NextPermissions"].ToString());
                item.CurrentPermissions = uint.Parse(data["CurrentPermissions"].ToString());
                item.BasePermissions = uint.Parse(data["BasePermissions"].ToString());
                item.EveryOnePermissions = uint.Parse(data["EveryOnePermissions"].ToString());
                item.GroupPermissions = uint.Parse(data["GroupPermissions"].ToString());
                item.GroupID = new UUID(data["GroupID"].ToString());
                item.GroupOwned = bool.Parse(data["GroupOwned"].ToString());
                item.SalePrice = int.Parse(data["SalePrice"].ToString());
                item.SaleType = byte.Parse(data["SaleType"].ToString());
                item.Flags = uint.Parse(data["Flags"].ToString());
                item.CreationDate = int.Parse(data["CreationDate"].ToString());
            }
            catch (Exception e)
            {
                m_log.Error("[XINVENTORY CONNECTOR]: Exception building item: ", e);
            }

            return item;
        }
    }
}
