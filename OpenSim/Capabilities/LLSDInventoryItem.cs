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

using OpenMetaverse;

namespace OpenSim.Framework.Capabilities
{
    [OSDMap]
    public class LLSDInventoryItem
    {
        public UUID parent_id;

        public UUID asset_id;
        public UUID item_id;
        public LLSDPermissions permissions;
        public int type;
        public int inv_type;
        public int flags;

        public LLSDSaleInfo sale_info;
        public string name;
        public string desc;
        public int created_at;
    }

    [OSDMap]
    public class LLSDPermissions
    {
        public UUID creator_id;
        public UUID owner_id;
        public UUID group_id;
        public int base_mask;
        public int owner_mask;
        public int group_mask;
        public int everyone_mask;
        public int next_owner_mask;
        public bool is_owner_group;
    }

    [OSDMap]
    public class LLSDSaleInfo
    {
        public int sale_price;
        public int sale_type;
    }

    [OSDMap]
    public class LLSDInventoryDescendents
    {
        public OSDArray folders = new OSDArray();
    }

    [OSDMap]
    public class LLSDFetchInventoryDescendents
    {
        public UUID folder_id;
        public UUID owner_id;
        public int sort_order;
        public bool fetch_folders;
        public bool fetch_items;
    }

    [OSDMap]
    public class LLSDInventoryFolderContents
    {
        public UUID agent_id;
        public int descendents;
        public UUID folder_id;
        public OSDArray categories = new OSDArray();
        public OSDArray items = new OSDArray();
        public UUID owner_id;
        public int version;
    }

    [OSDMap]
    public class LLSDFetchInventory
    {
        public UUID agent_id;
        public OSDArray items = new OSDArray();
    }
}