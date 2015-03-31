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
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchInvDescHandler 
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_InventoryService;
        private ILibraryService m_LibraryService;
//        private object m_fetchLock = new Object();

        public FetchInvDescHandler(IInventoryService invService, ILibraryService libService) 
        {
            m_InventoryService = invService;
            m_LibraryService = libService;
        }

        public string FetchInventoryDescendentsRequest(string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            lock (m_fetchLock)
//            {
//                m_log.DebugFormat("[WEB FETCH INV DESC HANDLER]: Received request {0}", request);
    
                // nasty temporary hack here, the linden client falsely
                // identifies the uuid 00000000-0000-0000-0000-000000000000
                // as a string which breaks us
                //
                // correctly mark it as a uuid
                //
                request = request.Replace("<string>00000000-0000-0000-0000-000000000000</string>", "<uuid>00000000-0000-0000-0000-000000000000</uuid>");
    
                // another hack <integer>1</integer> results in a
                // System.ArgumentException: Object type System.Int32 cannot
                // be converted to target type: System.Boolean
                //
                request = request.Replace("<key>fetch_folders</key><integer>0</integer>", "<key>fetch_folders</key><boolean>0</boolean>");
                request = request.Replace("<key>fetch_folders</key><integer>1</integer>", "<key>fetch_folders</key><boolean>1</boolean>");
    
                Hashtable hash = new Hashtable();
                try
                {
                    hash = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));
                }
                catch (LLSD.LLSDParseException e)
                {
                    m_log.ErrorFormat("[WEB FETCH INV DESC HANDLER]: Fetch error: {0}{1}" + e.Message, e.StackTrace);
                    m_log.Error("Request: " + request);
                }
    
                ArrayList foldersrequested = (ArrayList)hash["folders"];
    
                string response = "";
                string bad_folders_response = "";

                for (int i = 0; i < foldersrequested.Count; i++)
                {
                    string inventoryitemstr = "";
                    Hashtable inventoryhash = (Hashtable)foldersrequested[i];

                    LLSDFetchInventoryDescendents llsdRequest = new LLSDFetchInventoryDescendents();

                    try
                    {
                        LLSDHelpers.DeserialiseOSDMap(inventoryhash, llsdRequest);
                    }
                    catch (Exception e)
                    {
                        m_log.Debug("[WEB FETCH INV DESC HANDLER]: caught exception doing OSD deserialize" + e);
                    }
                    LLSDInventoryDescendents reply = FetchInventoryReply(llsdRequest);

                    if (null == reply)
                    {
                        bad_folders_response += "<uuid>" + llsdRequest.folder_id.ToString() + "</uuid>";
                    }
                    else
                    {
                        inventoryitemstr = LLSDHelpers.SerialiseLLSDReply(reply);
                        inventoryitemstr = inventoryitemstr.Replace("<llsd><map><key>folders</key><array>", "");
                        inventoryitemstr = inventoryitemstr.Replace("</array></map></llsd>", "");
                    }

                    response += inventoryitemstr;
                }

                if (response.Length == 0)
                {
                    /* Viewers expect a bad_folders array when not available */
                    if (bad_folders_response.Length != 0)
                    {
                        response = "<llsd><map><key>bad_folders</key><array>" + bad_folders_response + "</array></map></llsd>";
                    }
                    else
                    {
                        response = "<llsd><map><key>folders</key><array /></map></llsd>";
                    }
                }
                else
                {
                    if (bad_folders_response.Length != 0)
                    {
                        response = "<llsd><map><key>folders</key><array>" + response + "</array><key>bad_folders</key><array>" + bad_folders_response + "</array></map></llsd>";
                    }
                    else
                    {
                        response = "<llsd><map><key>folders</key><array>" + response + "</array></map></llsd>";
                    }
                }

//                m_log.DebugFormat("[WEB FETCH INV DESC HANDLER]: Replying to CAPS fetch inventory request");
                //m_log.Debug("[WEB FETCH INV DESC HANDLER] "+response);

                return response;

//            }
        }

        /// <summary>
        /// Construct an LLSD reply packet to a CAPS inventory request
        /// </summary>
        /// <param name="invFetch"></param>
        /// <returns></returns>
        private LLSDInventoryDescendents FetchInventoryReply(LLSDFetchInventoryDescendents invFetch)
        {
            LLSDInventoryDescendents reply = new LLSDInventoryDescendents();
            LLSDInventoryFolderContents contents = new LLSDInventoryFolderContents();
            contents.agent_id = invFetch.owner_id;
            contents.owner_id = invFetch.owner_id;
            contents.folder_id = invFetch.folder_id;

            reply.folders.Array.Add(contents);
            InventoryCollection inv = new InventoryCollection();
            inv.Folders = new List<InventoryFolderBase>();
            inv.Items = new List<InventoryItemBase>();
            int version = 0;
            int descendents = 0;

            inv
                = Fetch(
                    invFetch.owner_id, invFetch.folder_id, invFetch.owner_id,
                    invFetch.fetch_folders, invFetch.fetch_items, invFetch.sort_order, out version, out descendents);

            if (inv != null && inv.Folders != null)
            {
                foreach (InventoryFolderBase invFolder in inv.Folders)
                {
                    contents.categories.Array.Add(ConvertInventoryFolder(invFolder));
                }

                descendents += inv.Folders.Count;
            }

            if (inv != null && inv.Items != null)
            {
                foreach (InventoryItemBase invItem in inv.Items)
                {
                    contents.items.Array.Add(ConvertInventoryItem(invItem));
                }
            }

            contents.descendents = descendents;
            contents.version = version;

//            m_log.DebugFormat(
//                "[WEB FETCH INV DESC HANDLER]: Replying to request for folder {0} (fetch items {1}, fetch folders {2}) with {3} items and {4} folders for agent {5}",
//                invFetch.folder_id,
//                invFetch.fetch_items,
//                invFetch.fetch_folders,
//                contents.items.Array.Count,
//                contents.categories.Array.Count,
//                invFetch.owner_id);

            return reply;
        }

        /// <summary>
        /// Handle the caps inventory descendents fetch.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        /// <param name="version"></param>
        /// <returns>An empty InventoryCollection if the inventory look up failed</returns>
        private InventoryCollection Fetch(
            UUID agentID, UUID folderID, UUID ownerID,
            bool fetchFolders, bool fetchItems, int sortOrder, out int version, out int descendents)
        {
            //m_log.DebugFormat(
            //    "[WEB FETCH INV DESC HANDLER]: Fetching folders ({0}), items ({1}) from {2} for agent {3}",
            //    fetchFolders, fetchItems, folderID, agentID);

            // FIXME MAYBE: We're not handling sortOrder!

            version = 0;
            descendents = 0;

            InventoryFolderImpl fold;
            if (m_LibraryService != null && m_LibraryService.LibraryRootFolder != null && agentID == m_LibraryService.LibraryRootFolder.Owner)
            {
                if ((fold = m_LibraryService.LibraryRootFolder.FindFolder(folderID)) != null)
                {
                    InventoryCollection ret = new InventoryCollection();
                    ret.Folders = new List<InventoryFolderBase>();
                    ret.Items = fold.RequestListOfItems();
                    descendents = ret.Folders.Count + ret.Items.Count;

                    return ret;
                }
            }

            InventoryCollection contents = new InventoryCollection();

            if (folderID != UUID.Zero)
            {
                InventoryCollection fetchedContents = m_InventoryService.GetFolderContent(agentID, folderID);

                if (fetchedContents == null)
                {
                    m_log.WarnFormat("[WEB FETCH INV DESC HANDLER]: Could not get contents of folder {0} for user {1}", folderID, agentID);
                    return contents;
                }

                contents = fetchedContents;
                InventoryFolderBase containingFolder = new InventoryFolderBase();
                containingFolder.ID = folderID;
                containingFolder.Owner = agentID;
                containingFolder = m_InventoryService.GetFolder(containingFolder);

                if (containingFolder != null)
                {
//                    m_log.DebugFormat(
//                        "[WEB FETCH INV DESC HANDLER]: Retrieved folder {0} {1} for agent id {2}",
//                        containingFolder.Name, containingFolder.ID, agentID);

                    version = containingFolder.Version;

                    if (fetchItems)
                    {
                        List<InventoryItemBase> itemsToReturn = contents.Items;
                        List<InventoryItemBase> originalItems = new List<InventoryItemBase>(itemsToReturn);

                        // descendents must only include the links, not the linked items we add
                        descendents = originalItems.Count;

                        // Add target items for links in this folder before the links themselves.
                        foreach (InventoryItemBase item in originalItems)
                        {
                            if (item.AssetType == (int)AssetType.Link)
                            {
                                InventoryItemBase linkedItem = m_InventoryService.GetItem(new InventoryItemBase(item.AssetID));

                                // Take care of genuinely broken links where the target doesn't exist
                                // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
                                // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
                                // rather than having to keep track of every folder requested in the recursion.
                                if (linkedItem != null && linkedItem.AssetType != (int)AssetType.Link)
                                    itemsToReturn.Insert(0, linkedItem);
                            }
                        }

                        // Now scan for folder links and insert the items they target and those links at the head of the return data
                        foreach (InventoryItemBase item in originalItems)
                        {
                            if (item.AssetType == (int)AssetType.LinkFolder)
                            {
                                InventoryCollection linkedFolderContents = m_InventoryService.GetFolderContent(ownerID, item.AssetID);
                                List<InventoryItemBase> links = linkedFolderContents.Items;

                                itemsToReturn.InsertRange(0, links);

                                foreach (InventoryItemBase link in linkedFolderContents.Items)
                                {
                                    // Take care of genuinely broken links where the target doesn't exist
                                    // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
                                    // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
                                    // rather than having to keep track of every folder requested in the recursion.
                                    if (link != null)
                                    {
//                                        m_log.DebugFormat(
//                                            "[WEB FETCH INV DESC HANDLER]: Adding item {0} {1} from folder {2} linked from {3}",
//                                            link.Name, (AssetType)link.AssetType, item.AssetID, containingFolder.Name);

                                        InventoryItemBase linkedItem
                                            = m_InventoryService.GetItem(new InventoryItemBase(link.AssetID));

                                        if (linkedItem != null)
                                            itemsToReturn.Insert(0, linkedItem);
                                    }
                                }
                            }
                        }
                    }

//                    foreach (InventoryItemBase item in contents.Items)
//                    {
//                        m_log.DebugFormat(
//                            "[WEB FETCH INV DESC HANDLER]: Returning item {0}, type {1}, parent {2} in {3} {4}",
//                            item.Name, (AssetType)item.AssetType, item.Folder, containingFolder.Name, containingFolder.ID);
//                    }

                    // =====

//
//                        foreach (InventoryItemBase linkedItem in linkedItemsToAdd)
//                        {
//                            m_log.DebugFormat(
//                                "[WEB FETCH INV DESC HANDLER]: Inserted linked item {0} for link in folder {1} for agent {2}",
//                                linkedItem.Name, folderID, agentID);
//
//                            contents.Items.Add(linkedItem);
//                        }
//
//                        // If the folder requested contains links, then we need to send those folders first, otherwise the links
//                        // will be broken in the viewer.
//                        HashSet<UUID> linkedItemFolderIdsToSend = new HashSet<UUID>();
//                        foreach (InventoryItemBase item in contents.Items)
//                        {
//                            if (item.AssetType == (int)AssetType.Link)
//                            {
//                                InventoryItemBase linkedItem = m_InventoryService.GetItem(new InventoryItemBase(item.AssetID));
//
//                                // Take care of genuinely broken links where the target doesn't exist
//                                // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
//                                // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
//                                // rather than having to keep track of every folder requested in the recursion.
//                                if (linkedItem != null && linkedItem.AssetType != (int)AssetType.Link)
//                                {
//                                    // We don't need to send the folder if source and destination of the link are in the same
//                                    // folder.
//                                    if (linkedItem.Folder != containingFolder.ID)
//                                        linkedItemFolderIdsToSend.Add(linkedItem.Folder);
//                                }
//                            }
//                        }
//    
//                        foreach (UUID linkedItemFolderId in linkedItemFolderIdsToSend)
//                        {
//                            m_log.DebugFormat(
//                                "[WEB FETCH INV DESC HANDLER]: Recursively fetching folder {0} linked by item in folder {1} for agent {2}",
//                                linkedItemFolderId, folderID, agentID);
//
//                            int dummyVersion;
//                            InventoryCollection linkedCollection
//                                = Fetch(
//                                    agentID, linkedItemFolderId, ownerID, fetchFolders, fetchItems, sortOrder, out dummyVersion);
//
//                            InventoryFolderBase linkedFolder = new InventoryFolderBase(linkedItemFolderId);
//                            linkedFolder.Owner = agentID;
//                            linkedFolder = m_InventoryService.GetFolder(linkedFolder);
//
////                            contents.Folders.AddRange(linkedCollection.Folders);
//
//                            contents.Folders.Add(linkedFolder);
//                            contents.Items.AddRange(linkedCollection.Items);
//                        }
//                    }
                }
            }
            else
            {
                // Lost items don't really need a version
                version = 1;
            }

            return contents;

        }
        /// <summary>
        /// Convert an internal inventory folder object into an LLSD object.
        /// </summary>
        /// <param name="invFolder"></param>
        /// <returns></returns>
        private LLSDInventoryFolder ConvertInventoryFolder(InventoryFolderBase invFolder)
        {
            LLSDInventoryFolder llsdFolder = new LLSDInventoryFolder();
            llsdFolder.folder_id = invFolder.ID;
            llsdFolder.parent_id = invFolder.ParentID;
            llsdFolder.name = invFolder.Name;
            llsdFolder.type = invFolder.Type;
            llsdFolder.preferred_type = -1;

            return llsdFolder;
        }

        /// <summary>
        /// Convert an internal inventory item object into an LLSD object.
        /// </summary>
        /// <param name="invItem"></param>
        /// <returns></returns>
        private LLSDInventoryItem ConvertInventoryItem(InventoryItemBase invItem)
        {
            LLSDInventoryItem llsdItem = new LLSDInventoryItem();
            llsdItem.asset_id = invItem.AssetID;
            llsdItem.created_at = invItem.CreationDate;
            llsdItem.desc = invItem.Description;
            llsdItem.flags = (int)invItem.Flags;
            llsdItem.item_id = invItem.ID;
            llsdItem.name = invItem.Name;
            llsdItem.parent_id = invItem.Folder;
            llsdItem.type = invItem.AssetType;
            llsdItem.inv_type = invItem.InvType;

            llsdItem.permissions = new LLSDPermissions();
            llsdItem.permissions.creator_id = invItem.CreatorIdAsUuid;
            llsdItem.permissions.base_mask = (int)invItem.CurrentPermissions;
            llsdItem.permissions.everyone_mask = (int)invItem.EveryOnePermissions;
            llsdItem.permissions.group_id = invItem.GroupID;
            llsdItem.permissions.group_mask = (int)invItem.GroupPermissions;
            llsdItem.permissions.is_owner_group = invItem.GroupOwned;
            llsdItem.permissions.next_owner_mask = (int)invItem.NextPermissions;
            llsdItem.permissions.owner_id = invItem.Owner;
            llsdItem.permissions.owner_mask = (int)invItem.CurrentPermissions;
            llsdItem.sale_info = new LLSDSaleInfo();
            llsdItem.sale_info.sale_price = invItem.SalePrice;
            llsdItem.sale_info.sale_type = invItem.SaleType;

            return llsdItem;
        }
    }
}