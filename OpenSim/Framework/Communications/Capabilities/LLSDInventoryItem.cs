using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Capabilities
{ 
    [LLSDMap]
    public class LLSDInventoryItem
    {
        public LLUUID parent_id;
       
        public LLUUID asset_id;
        public LLUUID item_id;
       
        public string type;
        public string inv_type;
        public int flags;

        public LLSDSaleInfo sale_info;
        public string name;
        public string desc;
        public int created_at;

    }

    [LLSDMap]
    public class LLSDPermissions
    {
        public LLUUID creator_id;
        public LLUUID owner_id;
        public LLUUID group_id;
        public int base_mask;
        public int owner_mask;
        public int group_mask;
        public int everyone_mask;
        public int next_owner_mask;
        public bool is_owner_group;
    }

    [LLSDMap]
    public class LLSDSaleInfo
    {
        public int sale_price;
        public string sale_type;
    }

  /*  [LLSDMap]
    public class LLSDFolderItem
    {
        public LLUUID folder_id;
        public LLUUID parent_id;
        public int type;
        public string name;
    }*/

    [LLSDMap]
    public class LLSDInventoryDescendents
    {
        public LLSDArray folders= new LLSDArray();
    }

    [LLSDMap]
    public class LLSDFetchInventoryDescendents
    {
        public LLUUID folder_id;
        public LLUUID owner_id;
        public int sort_order;
        public bool fetch_folders;
        public bool fetch_items;
    }

    [LLSDMap]
    public class LLSDInventoryFolderContents
    {
        public LLUUID agent___id;
        public int descendents;
        public LLUUID folder___id; // the (three "_") "___" so the serialising knows to change this to a "-"
        public LLSDArray items = new LLSDArray();
        public LLUUID owner___id;
        public int version;
    }
}
