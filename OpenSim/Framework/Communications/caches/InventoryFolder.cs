using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Data;

namespace OpenSim.Framework.Communications.Caches
{
    public class InventoryFolder : InventoryFolderBase
    {
        public Dictionary<LLUUID, InventoryFolder> SubFolders = new Dictionary<LLUUID, InventoryFolder>();
        public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();

        public InventoryFolder()
        {
        }

        public InventoryFolder HasSubFolder(LLUUID folderID)
        {
            InventoryFolder returnFolder = null;
            if (this.SubFolders.ContainsKey(folderID))
            {
                returnFolder = this.SubFolders[folderID];
            }
            else
            {
                foreach (InventoryFolder folder in this.SubFolders.Values)
                {
                   returnFolder = folder.HasSubFolder(folderID);
                   if (returnFolder != null)
                   {
                       break;
                   }
                }
            }
            return returnFolder;
        }

        public InventoryFolder CreateNewSubFolder(LLUUID folderID, string folderName, ushort type)
        {
            InventoryFolder subFold = new InventoryFolder();
            subFold.name = folderName;
            subFold.folderID = folderID;
            subFold.type = type;
            subFold.parentID = this.folderID;
            subFold.agentID = this.agentID;
            this.SubFolders.Add(subFold.folderID, subFold);
            return subFold;
        }
    }
}
