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

using System.Reflection;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using log4net;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchLib2Handler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_inventoryService;
        private ILibraryService m_LibraryService;
        private UUID m_agentID;
        private UUID libOwner;

        public FetchLib2Handler(IInventoryService invService, ILibraryService libraryService, UUID agentId)

        {
            m_inventoryService = invService;
            m_agentID = agentId;
            m_LibraryService = libraryService;
            if(libraryService != null)
                libOwner = m_LibraryService.LibraryRootFolder.Owner;
        }

        public string FetchLibRequest(string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[FETCH INVENTORY HANDLER]: Received FetchInventory capability request {0}", request);

            if (m_LibraryService == null)
                return "<llsd><map><key><agent_id></key><uuid /><key>items</key><array /></map></llsd>";

            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));
            OSDArray itemsRequested = (OSDArray)requestmap["items"];

            if (m_agentID == UUID.Zero)
                return "<llsd><map><key><agent_id></key><uuid /><key>items</key><array /></map></llsd>";

            UUID[] itemIDs = new UUID[itemsRequested.Count];
            int i = 0;

            foreach (OSDMap osdItemId in itemsRequested)
                itemIDs[i++] = osdItemId["item_id"].AsUUID();

            InventoryItemBase[] items = null;

//            items = m_inventoryService.GetMultipleItems(libOwner, itemIDs);
            items = m_LibraryService.GetMultipleItems(itemIDs);

            StringBuilder lsl = LLSDxmlEncode.Start(2048);
            LLSDxmlEncode.AddMap(lsl);
            LLSDxmlEncode.AddElem("agent_id", m_agentID, lsl);
            if(items == null || items.Length == 0)
            {
                LLSDxmlEncode.AddEmptyArray("items",lsl);
            }
            else
            {
                LLSDxmlEncode.AddArray("items",lsl);
                foreach (InventoryItemBase item in items)
                {
                    if (item != null)
                    {
                        LLSDxmlEncode.AddMap(lsl);
                            LLSDxmlEncode.AddElem("parent_id", item.Folder, lsl);
                            LLSDxmlEncode.AddElem("asset_id", item.AssetID, lsl);
                            LLSDxmlEncode.AddElem("item_id", item.ID, lsl);

                            LLSDxmlEncode.AddMap("permissions",lsl);
                                LLSDxmlEncode.AddElem("creator_id", item.CreatorIdAsUuid, lsl);
                                LLSDxmlEncode.AddElem("owner_id", item.Owner, lsl);
                                LLSDxmlEncode.AddElem("group_id", item.GroupID, lsl);
                                LLSDxmlEncode.AddElem("base_mask", (int)item.CurrentPermissions, lsl);
                                LLSDxmlEncode.AddElem("owner_mask", (int)item.CurrentPermissions, lsl);
                                LLSDxmlEncode.AddElem("group_mask", (int)item.GroupPermissions, lsl);
                                LLSDxmlEncode.AddElem("everyone_mask", (int)item.EveryOnePermissions, lsl);
                                LLSDxmlEncode.AddElem("next_owner_mask", (int)item.NextPermissions, lsl);
                                LLSDxmlEncode.AddElem("is_owner_group", item.GroupOwned, lsl);               
                            LLSDxmlEncode.AddEndMap(lsl);

                            LLSDxmlEncode.AddElem("type", item.AssetType, lsl);               
                            LLSDxmlEncode.AddElem("inv_type", item.InvType, lsl);               
                            LLSDxmlEncode.AddElem("flags", ((int)item.Flags) & 0xff, lsl);               
                            LLSDxmlEncode.AddElem("flags", ((int)item.Flags) & 0xff, lsl);               

                            LLSDxmlEncode.AddMap("sale_info",lsl);
                                LLSDxmlEncode.AddElem("sale_price", item.SalePrice, lsl);               
                                LLSDxmlEncode.AddElem("sale_type", item.SaleType, lsl);               
                            LLSDxmlEncode.AddEndMap(lsl);

                            LLSDxmlEncode.AddElem("name", item.Name, lsl);               
                            LLSDxmlEncode.AddElem("desc", item.Description, lsl);               
                            LLSDxmlEncode.AddElem("created_at", item.CreationDate, lsl);               

                        LLSDxmlEncode.AddEndMap(lsl);
                    }
                }
                LLSDxmlEncode.AddEndArray(lsl);
            }            

            LLSDxmlEncode.AddEndMap(lsl);
            return LLSDxmlEncode.End(lsl);;
        }
    }
}