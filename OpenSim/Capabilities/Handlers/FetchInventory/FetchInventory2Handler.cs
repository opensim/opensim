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

using System.Net;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using log4net;

namespace OpenSim.Capabilities.Handlers
{
    public class FetchInventory2Handler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IInventoryService m_inventoryService;
        private UUID m_agentID;

        public FetchInventory2Handler(IInventoryService invService, UUID agentId)
        {
            m_inventoryService = invService;
            m_agentID = agentId;
        }

        public string FetchInventoryRequest(string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[FETCH INVENTORY HANDLER]: Received FetchInventory capability request {0}", request);

            OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml(Utils.StringToBytes(request));
            OSDArray itemsRequested = (OSDArray)requestmap["items"];

            UUID[] itemIDs = new UUID[itemsRequested.Count];
            int i = 0;

            foreach (OSDMap osdItemId in itemsRequested)
            {
                itemIDs[i++] = osdItemId["item_id"].AsUUID();
            }

            InventoryItemBase[] items;

            if (m_agentID.IsZero())
            {
                items = new InventoryItemBase[itemsRequested.Count];
                foreach (UUID id in itemIDs)
                    items[i++] = m_inventoryService.GetItem(UUID.Zero, id);
            }
            else
            {
                items = m_inventoryService.GetMultipleItems(m_agentID, itemIDs);
            }

            osUTF8 lsl = LLSDxmlEncode2.Start(4096);
            LLSDxmlEncode2.AddMap(lsl);

            if(m_agentID.IsZero() && items.Length > 0)
                LLSDxmlEncode2.AddElem("agent_id", items[0].Owner, lsl);
            else
                LLSDxmlEncode2.AddElem("agent_id", m_agentID, lsl);

            if(items is null || items.Length == 0)
            {
                LLSDxmlEncode2.AddEmptyArray("items", lsl);
            }
            else
            {
                LLSDxmlEncode2.AddArray("items", lsl);
                foreach (InventoryItemBase item in items)
                {
                    if (item is not null)
                        item.ToLLSDxml(lsl, 0xff);
                }
                LLSDxmlEncode2.AddEndArray(lsl);
            }            

            LLSDxmlEncode2.AddEndMap(lsl);
            return LLSDxmlEncode2.End(lsl);
        }

        public void FetchInventorySimpleRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap requestmap, ExpiringKey<UUID> BadRequests)
        {
            //m_log.DebugFormat("[FETCH INVENTORY HANDLER]: Received FetchInventory capability request {0}", request);

            if(BadRequests == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            OSDArray itemsRequested = (OSDArray)requestmap["items"];

            UUID[] itemIDs = new UUID[itemsRequested.Count];
            int i = 0;
            foreach (OSDMap osdItemId in itemsRequested)
            {
                UUID id = osdItemId["item_id"].AsUUID();
                if(!BadRequests.ContainsKey(id))
                    itemIDs[i++] = id;
            }

            InventoryItemBase[] items = null;
            try
            {
                // badrequests still not filled
                items = m_inventoryService.GetMultipleItems(m_agentID, itemIDs);
            }
            catch{ }

            osUTF8 lsl = LLSDxmlEncode2.Start(4096);
            LLSDxmlEncode2.AddMap(lsl);

            LLSDxmlEncode2.AddElem("agent_id", m_agentID, lsl);

            if (items == null || items.Length == 0)
            {
                LLSDxmlEncode2.AddEmptyArray("items", lsl);
            }
            else
            {
                LLSDxmlEncode2.AddArray("items", lsl);
                foreach (InventoryItemBase item in items)
                {
                    if (item != null)
                        item.ToLLSDxml(lsl, 0xff);
                }
                LLSDxmlEncode2.AddEndArray(lsl);
            }

            LLSDxmlEncode2.AddEndMap(lsl);
            httpResponse.RawBuffer = LLSDxmlEncode2.EndToBytes(lsl);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }
    }
}

