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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.IO;
using System.Xml;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Basically a hack to give us a Inventory library while we don't have a inventory server
    /// once the server is fully implemented then should read the data from that
    /// </summary>
    public class LibraryRootFolder : InventoryFolderImpl
    {
        private LLUUID libOwner = new LLUUID("11111111-1111-0000-0000-000100bba000");
        private InventoryFolderImpl m_textureFolder;

        public LibraryRootFolder()
        {
            agentID = libOwner;
            folderID = new LLUUID("00000112-000f-0000-0000-000100bba000");
            name = "OpenSim Library";
            parentID = LLUUID.Zero;
            type = (short)-1;
            version = (ushort)1;

            InventoryFolderImpl folderInfo = new InventoryFolderImpl();
            folderInfo.agentID = libOwner;
            folderInfo.folderID = new LLUUID("00000112-000f-0000-0000-000100bba001");
            folderInfo.name = "Texture Library";
            folderInfo.parentID = folderID;
            folderInfo.type = -1;
            folderInfo.version = 1;
            SubFolders.Add(folderInfo.folderID, folderInfo);
            m_textureFolder = folderInfo;

            CreateLibraryItems();

            string filePath = Path.Combine(Util.configDir(), "OpenSimLibrary.xml");
            if (File.Exists(filePath))
            {
                try
                {
                    XmlConfigSource source = new XmlConfigSource(filePath);
                    ReadItemsFromFile(source);
                }
                catch (XmlException e)
                {
                    MainLog.Instance.Error("INVENTORY", "Error loading " + filePath + ": " + e.ToString());
                }
            }
        }

        private void CreateLibraryItems()
        {
            InventoryItemBase item = CreateItem(LLUUID.Random(), new LLUUID("00000000-0000-0000-9999-000000000002"), "Plywood", "Plywood texture", (int)AssetType.Texture, (int)InventoryType.Texture, m_textureFolder.folderID);
            m_textureFolder.Items.Add(item.inventoryID, item);

            item = CreateItem(LLUUID.Random(), new LLUUID("00000000-0000-0000-9999-000000000003"), "Rocks", "Rocks texture", (int)AssetType.Texture, (int)InventoryType.Texture, m_textureFolder.folderID);
            m_textureFolder.Items.Add(item.inventoryID, item);

            item = CreateItem(LLUUID.Random(), new LLUUID("00000000-0000-0000-9999-000000000001"), "Bricks", "Bricks texture", (int)AssetType.Texture, (int)InventoryType.Texture, m_textureFolder.folderID);
            m_textureFolder.Items.Add(item.inventoryID, item);

            item = CreateItem(LLUUID.Random(), new LLUUID("00000000-0000-0000-9999-000000000004"), "Granite", "Granite texture", (int)AssetType.Texture, (int)InventoryType.Texture, m_textureFolder.folderID);
            m_textureFolder.Items.Add(item.inventoryID, item);

            item = CreateItem(LLUUID.Random(), new LLUUID("00000000-0000-0000-9999-000000000005"), "Hardwood", "Hardwood texture", (int)AssetType.Texture, (int)InventoryType.Texture, m_textureFolder.folderID);
            m_textureFolder.Items.Add(item.inventoryID, item);

            item = CreateItem(new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9"), new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73"), "Default Shape", "Default Shape", (int)AssetType.Bodypart, (int)InventoryType.Wearable, folderID);
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            Items.Add(item.inventoryID, item);

            item = CreateItem(new LLUUID("77c41e39-38f9-f75a-024e-585989bfabc9"), new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb"), "Default Skin", "Default Skin", (int)AssetType.Bodypart, (int)InventoryType.Wearable, folderID);
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            Items.Add(item.inventoryID, item);

            item = CreateItem(new LLUUID("77c41e39-38f9-f75a-0000-585989bf0000"), new LLUUID("00000000-38f9-1111-024e-222222111110"), "Default Shirt", "Default Shirt", (int)AssetType.Clothing, (int)InventoryType.Wearable, folderID);
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            Items.Add(item.inventoryID, item);

            item = CreateItem(new LLUUID("77c41e39-38f9-f75a-0000-5859892f1111"), new LLUUID("00000000-38f9-1111-024e-222222111120"), "Default Pants", "Default Pants", (int)AssetType.Clothing, (int)InventoryType.Wearable, folderID);
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            Items.Add(item.inventoryID, item);
        }

        public InventoryItemBase CreateItem(LLUUID inventoryID, LLUUID assetID, string name, string description, int assetType, int invType, LLUUID parentFolderID)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = inventoryID;
            item.assetID = assetID;
            item.inventoryDescription = description;
            item.inventoryName = name;
            item.assetType = assetType;
            item.invType = invType;
            item.parentFolderID = parentFolderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            return item;
        }

        private void ReadItemsFromFile(IConfigSource source)
        {
            for (int i = 0; i < source.Configs.Count; i++)
            {
                InventoryItemBase item = new InventoryItemBase();
                item.avatarID = libOwner;
                item.creatorsID = libOwner;
                item.inventoryID =
                    new LLUUID(source.Configs[i].GetString("inventoryID", LLUUID.Random().ToString()));
                item.assetID = new LLUUID(source.Configs[i].GetString("assetID", LLUUID.Random().ToString()));
                item.inventoryDescription = source.Configs[i].GetString("description", "");
                item.inventoryName = source.Configs[i].GetString("name", "");
                item.assetType = source.Configs[i].GetInt("assetType", 0);
                item.invType = source.Configs[i].GetInt("inventoryType", 0);
                item.inventoryCurrentPermissions = (uint)source.Configs[i].GetLong("currentPermissions", 0x7FFFFFFF);
                item.inventoryNextPermissions = (uint)source.Configs[i].GetLong("nextPermissions", 0x7FFFFFFF);
                item.inventoryEveryOnePermissions = (uint)source.Configs[i].GetLong("everyonePermissions", 0x7FFFFFFF);
                item.inventoryBasePermissions = (uint)source.Configs[i].GetLong("basePermissions", 0x7FFFFFFF);
                if (item.assetType == 0)
                {
                    item.parentFolderID = m_textureFolder.folderID;
                    m_textureFolder.Items.Add(item.inventoryID, item);
                }
                else
                {
                    item.parentFolderID = folderID;
                    Items.Add(item.inventoryID, item);
                }
            }
        }
    }
}
