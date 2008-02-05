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

using System;
using System.Collections.Generic;
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
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private LLUUID libOwner = new LLUUID("11111111-1111-0000-0000-000100bba000");
        
        /// <summary>
        /// Holds the root library folder and all its descendents.  This is really only used during inventory
        /// setup so that we don't have to repeatedly search the tree of library folders.
        /// </summary>
        protected Dictionary<LLUUID, InventoryFolderImpl> libraryFolders 
            = new Dictionary<LLUUID, InventoryFolderImpl>();

        public LibraryRootFolder()
        {
            m_log.Info("[LIBRARYINVENTORY]: Loading library inventory");
            
            agentID = libOwner;
            folderID = new LLUUID("00000112-000f-0000-0000-000100bba000");
            name = "OpenSim Library";
            parentID = LLUUID.Zero;
            type = (short) 8;
            version = (ushort) 1;
            
            libraryFolders.Add(folderID, this);
            
            LoadLibraries(Path.Combine(Util.inventoryDir(), "Libraries.xml"));

            // CreateLibraryItems();
        }

        /// <summary>
        /// Hardcoded item creation.  Please don't add any more items here - future items should be created 
        /// in the xml in the bin/inventory folder.
        /// </summary>
        ///
        /// Commented the following out due to sending it all through xml, remove this section once this is provin to work stable.
        ///
        //private void CreateLibraryItems()
        //{
        //    InventoryItemBase item =
        //        CreateItem(new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9"),
        //                   new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73"), "Default Shape", "Default Shape",
        //                   (int) AssetType.Bodypart, (int) InventoryType.Wearable, folderID);
        //    item.inventoryCurrentPermissions = 0;
        //    item.inventoryNextPermissions = 0;
        //    Items.Add(item.inventoryID, item);

        //    item =
        //        CreateItem(new LLUUID("77c41e39-38f9-f75a-024e-585989bfabc9"),
        //                   new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb"), "Default Skin", "Default Skin",
        //                   (int) AssetType.Bodypart, (int) InventoryType.Wearable, folderID);
        //    item.inventoryCurrentPermissions = 0;
        //    item.inventoryNextPermissions = 0;
        //    Items.Add(item.inventoryID, item);

        //    item =
        //        CreateItem(new LLUUID("77c41e39-38f9-f75a-0000-585989bf0000"),
        //                   new LLUUID("00000000-38f9-1111-024e-222222111110"), "Default Shirt", "Default Shirt",
        //                   (int) AssetType.Clothing, (int) InventoryType.Wearable, folderID);
        //    item.inventoryCurrentPermissions = 0;
        //    item.inventoryNextPermissions = 0;
        //    Items.Add(item.inventoryID, item);

        //    item =
        //        CreateItem(new LLUUID("77c41e39-38f9-f75a-0000-5859892f1111"),
        //                   new LLUUID("00000000-38f9-1111-024e-222222111120"), "Default Pants", "Default Pants",
        //                   (int) AssetType.Clothing, (int) InventoryType.Wearable, folderID);
        //    item.inventoryCurrentPermissions = 0;
        //    item.inventoryNextPermissions = 0;
        //    Items.Add(item.inventoryID, item);
        //}

        public InventoryItemBase CreateItem(LLUUID inventoryID, LLUUID assetID, string name, string description,
                                            int assetType, int invType, LLUUID parentFolderID)
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
        
        /// <summary>
        /// Use the asset set information at path to load assets
        /// </summary>
        /// <param name="path"></param>
        /// <param name="assets"></param>
        protected void LoadLibraries(string librariesControlPath)
        {
            m_log.Info(
                String.Format("LIBRARYINVENTORY", "Loading libraries control file {0}", librariesControlPath));
            
            LoadFromFile(librariesControlPath, "Libraries control", ReadLibraryFromConfig);
        }
        
        /// <summary>
        /// Read a library set from config
        /// </summary>
        /// <param name="config"></param>
        protected void ReadLibraryFromConfig(IConfig config)
        {
            string foldersPath 
                = Path.Combine(
                    Util.inventoryDir(), config.GetString("foldersFile", System.String.Empty));
            
            LoadFromFile(foldersPath, "Library folders", ReadFolderFromConfig);
            
            string itemsPath 
                = Path.Combine(
                    Util.inventoryDir(), config.GetString("itemsFile", System.String.Empty));
            
            LoadFromFile(itemsPath, "Library items", ReadItemFromConfig);
        }
        
        /// <summary>
        /// Read a library inventory folder from a loaded configuration
        /// </summary>
        /// <param name="source"></param>
        private void ReadFolderFromConfig(IConfig config)
        {                        
            InventoryFolderImpl folderInfo = new InventoryFolderImpl();
            
            folderInfo.folderID = new LLUUID(config.GetString("folderID", folderID.ToString()));
            folderInfo.name = config.GetString("name", "unknown");                
            folderInfo.parentID = new LLUUID(config.GetString("parentFolderID", folderID.ToString()));
            folderInfo.type = (short)config.GetInt("type", 8);
            
            folderInfo.agentID = libOwner;                
            folderInfo.version = 1;                
            
            if (libraryFolders.ContainsKey(folderInfo.parentID))
            {                
                InventoryFolderImpl parentFolder = libraryFolders[folderInfo.parentID];
                
                libraryFolders.Add(folderInfo.folderID, folderInfo);
                parentFolder.SubFolders.Add(folderInfo.folderID, folderInfo);
                
//                    m_log.Info(
//                        String.Format("[LIBRARYINVENTORY]: Adding folder {0} ({1})", folderInfo.name, folderInfo.folderID));
            }
            else
            {
                m_log.Warn(
                    String.Format("[LIBRARYINVENTORY]: " + 
                                  "Couldn't add folder {0} ({1}) since parent folder with ID {2} does not exist!",
                                  folderInfo.name, folderInfo.folderID, folderInfo.parentID));
            }
        }

        /// <summary>
        /// Read a library inventory item metadata from a loaded configuration
        /// </summary>
        /// <param name="source"></param>        
        private void ReadItemFromConfig(IConfig config)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID(config.GetString("inventoryID", folderID.ToString()));
            item.assetID = new LLUUID(config.GetString("assetID", LLUUID.Random().ToString()));
            item.parentFolderID = new LLUUID(config.GetString("folderID", folderID.ToString()));
            item.inventoryDescription = config.GetString("description", System.String.Empty);
            item.inventoryName = config.GetString("name", System.String.Empty);
            item.assetType = config.GetInt("assetType", 0);
            item.invType = config.GetInt("inventoryType", 0);
            item.inventoryCurrentPermissions = (uint)config.GetLong("currentPermissions", 0x7FFFFFFF);
            item.inventoryNextPermissions = (uint)config.GetLong("nextPermissions", 0x7FFFFFFF);
            item.inventoryEveryOnePermissions = (uint)config.GetLong("everyonePermissions", 0x7FFFFFFF);
            item.inventoryBasePermissions = (uint)config.GetLong("basePermissions", 0x7FFFFFFF);
            
            if (libraryFolders.ContainsKey(item.parentFolderID))
            {
                InventoryFolderImpl parentFolder = libraryFolders[item.parentFolderID];
                
                parentFolder.Items.Add(item.inventoryID, item);
            }
            else
            {
                m_log.Warn(
                    String.Format("[LIBRARYINVENTORY]: " +
                                  "Couldn't add item {0} ({1}) since parent folder with ID {2} does not exist!",
                                  item.inventoryName, item.inventoryID, item.parentFolderID));
            }                
        }
                
        private delegate void ConfigAction(IConfig config);        
        
        /// <summary>
        /// Load the given configuration at a path and perform an action on each Config contained within it
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileDescription"></param>
        /// <param name="action"></param>
        private void LoadFromFile(string path, string fileDescription, ConfigAction action)
        {            
            if (File.Exists(path))
            {
                try
                {
                    XmlConfigSource source = new XmlConfigSource(path);

                    for (int i = 0; i < source.Configs.Count; i++)
                    {    
                        action(source.Configs[i]);
                    }
                }
                catch (XmlException e)
                {
                    m_log.Error(
                        String.Format("[LIBRARYINVENTORY]: Error loading {0} : {1}", path, e));
                }                
            }
            else
            {
                m_log.Error(
                    String.Format("[LIBRARYINVENTORY]: {0} file {1} does not exist!", fileDescription, path));
            }            
        }
        
        /// <summary>
        /// Looks like a simple getter, but is written like this for some consistency with the other Request
        /// methods in the superclass
        /// </summary>
        /// <returns></returns>
        public Dictionary<LLUUID, InventoryFolderImpl> RequestSelfAndDescendentFolders()
        {
            return libraryFolders;
        }
    }
}
