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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

using OpenSim.Framework;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;

using log4net;
using Nini.Config;
using OpenMetaverse;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Services.InventoryService
{
    /// <summary>
    /// Basically a hack to give us a Inventory library while we don't have a inventory server
    /// once the server is fully implemented then should read the data from that
    /// </summary>
    public class LibraryService : ServiceBase, ILibraryService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private InventoryFolderImpl m_LibraryRootFolder;

        public InventoryFolderImpl LibraryRootFolder
        {
            get { return m_LibraryRootFolder; }
        }

        private UUID libOwner = new UUID("11111111-1111-0000-0000-000100bba000");

        /// <summary>
        /// Holds the root library folder and all its descendents.  This is really only used during inventory
        /// setup so that we don't have to repeatedly search the tree of library folders.
        /// </summary>
        protected Dictionary<UUID, InventoryFolderImpl> libraryFolders
            = new Dictionary<UUID, InventoryFolderImpl>();

        public LibraryService(IConfigSource config)
            : base(config)
        {
            string pLibrariesLocation = Path.Combine("inventory", "Libraries.xml");
            string pLibName = "OpenSim Library";

            IConfig libConfig = config.Configs["LibraryService"];
            if (libConfig != null)
            {
                pLibrariesLocation = libConfig.GetString("DefaultLibrary", pLibrariesLocation);
                pLibName = libConfig.GetString("LibraryName", pLibName);
            }

            m_log.Debug("[LIBRARY]: Starting library service...");

            m_LibraryRootFolder = new InventoryFolderImpl();
            m_LibraryRootFolder.Owner = libOwner;
            m_LibraryRootFolder.ID = new UUID("00000112-000f-0000-0000-000100bba000");
            m_LibraryRootFolder.Name = pLibName;
            m_LibraryRootFolder.ParentID = UUID.Zero;
            m_LibraryRootFolder.Type = (short)8;
            m_LibraryRootFolder.Version = (ushort)1;

            libraryFolders.Add(m_LibraryRootFolder.ID, m_LibraryRootFolder);

            LoadLibraries(pLibrariesLocation);
        }

        public InventoryItemBase CreateItem(UUID inventoryID, UUID assetID, string name, string description,
                                            int assetType, int invType, UUID parentFolderID)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = libOwner;
            item.CreatorId = libOwner.ToString();
            item.ID = inventoryID;
            item.AssetID = assetID;
            item.Description = description;
            item.Name = name;
            item.AssetType = assetType;
            item.InvType = invType;
            item.Folder = parentFolderID;
            item.BasePermissions = 0x7FFFFFFF;
            item.EveryOnePermissions = 0x7FFFFFFF;
            item.CurrentPermissions = 0x7FFFFFFF;
            item.NextPermissions = 0x7FFFFFFF;
            return item;
        }

        /// <summary>
        /// Use the asset set information at path to load assets
        /// </summary>
        /// <param name="path"></param>
        /// <param name="assets"></param>
        protected void LoadLibraries(string librariesControlPath)
        {
            m_log.InfoFormat("[LIBRARY INVENTORY]: Loading library control file {0}", librariesControlPath);
            LoadFromFile(librariesControlPath, "Libraries control", ReadLibraryFromConfig);
        }

        /// <summary>
        /// Read a library set from config
        /// </summary>
        /// <param name="config"></param>
        protected void ReadLibraryFromConfig(IConfig config, string path)
        {
            string basePath = Path.GetDirectoryName(path);
            string foldersPath
                = Path.Combine(
                    basePath, config.GetString("foldersFile", String.Empty));

            LoadFromFile(foldersPath, "Library folders", ReadFolderFromConfig);

            string itemsPath
                = Path.Combine(
                    basePath, config.GetString("itemsFile", String.Empty));

            LoadFromFile(itemsPath, "Library items", ReadItemFromConfig);
        }

        /// <summary>
        /// Read a library inventory folder from a loaded configuration
        /// </summary>
        /// <param name="source"></param>
        private void ReadFolderFromConfig(IConfig config, string path)
        {
            InventoryFolderImpl folderInfo = new InventoryFolderImpl();

            folderInfo.ID = new UUID(config.GetString("folderID", m_LibraryRootFolder.ID.ToString()));
            folderInfo.Name = config.GetString("name", "unknown");
            folderInfo.ParentID = new UUID(config.GetString("parentFolderID", m_LibraryRootFolder.ID.ToString()));
            folderInfo.Type = (short)config.GetInt("type", 8);

            folderInfo.Owner = libOwner;
            folderInfo.Version = 1;

            if (libraryFolders.ContainsKey(folderInfo.ParentID))
            {
                InventoryFolderImpl parentFolder = libraryFolders[folderInfo.ParentID];

                libraryFolders.Add(folderInfo.ID, folderInfo);
                parentFolder.AddChildFolder(folderInfo);

//                 m_log.InfoFormat("[LIBRARY INVENTORY]: Adding folder {0} ({1})", folderInfo.name, folderInfo.folderID);
            }
            else
            {
                m_log.WarnFormat(
                    "[LIBRARY INVENTORY]: Couldn't add folder {0} ({1}) since parent folder with ID {2} does not exist!",
                    folderInfo.Name, folderInfo.ID, folderInfo.ParentID);
            }
        }

        /// <summary>
        /// Read a library inventory item metadata from a loaded configuration
        /// </summary>
        /// <param name="source"></param>
        private void ReadItemFromConfig(IConfig config, string path)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = libOwner;
            item.CreatorId = libOwner.ToString();
            item.ID = new UUID(config.GetString("inventoryID", m_LibraryRootFolder.ID.ToString()));
            item.AssetID = new UUID(config.GetString("assetID", item.ID.ToString()));
            item.Folder = new UUID(config.GetString("folderID", m_LibraryRootFolder.ID.ToString()));
            item.Name = config.GetString("name", String.Empty);
            item.Description = config.GetString("description", item.Name);
            item.InvType = config.GetInt("inventoryType", 0);
            item.AssetType = config.GetInt("assetType", item.InvType);
            item.CurrentPermissions = (uint)config.GetLong("currentPermissions", (uint)PermissionMask.All);
            item.NextPermissions = (uint)config.GetLong("nextPermissions", (uint)PermissionMask.All);
            item.EveryOnePermissions
                = (uint)config.GetLong("everyonePermissions", (uint)PermissionMask.All - (uint)PermissionMask.Modify);
            item.BasePermissions = (uint)config.GetLong("basePermissions", (uint)PermissionMask.All);
            item.Flags = (uint)config.GetInt("flags", 0);

            if (libraryFolders.ContainsKey(item.Folder))
            {
                InventoryFolderImpl parentFolder = libraryFolders[item.Folder];
                try
                {
                    parentFolder.Items.Add(item.ID, item);
                }
                catch (Exception)
                {
                    m_log.WarnFormat("[LIBRARY INVENTORY] Item {1} [{0}] not added, duplicate item", item.ID, item.Name);
                }
            }
            else
            {
                m_log.WarnFormat(
                    "[LIBRARY INVENTORY]: Couldn't add item {0} ({1}) since parent folder with ID {2} does not exist!",
                    item.Name, item.ID, item.Folder);
            }
        }

        private delegate void ConfigAction(IConfig config, string path);

        /// <summary>
        /// Load the given configuration at a path and perform an action on each Config contained within it
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileDescription"></param>
        /// <param name="action"></param>
        private static void LoadFromFile(string path, string fileDescription, ConfigAction action)
        {
            if (File.Exists(path))
            {
                try
                {
                    XmlConfigSource source = new XmlConfigSource(path);

                    for (int i = 0; i < source.Configs.Count; i++)
                    {
                        action(source.Configs[i], path);
                    }
                }
                catch (XmlException e)
                {
                    m_log.ErrorFormat("[LIBRARY INVENTORY]: Error loading {0} : {1}", path, e);
                }
            }
            else
            {
                m_log.ErrorFormat("[LIBRARY INVENTORY]: {0} file {1} does not exist!", fileDescription, path);
            }
        }

        /// <summary>
        /// Looks like a simple getter, but is written like this for some consistency with the other Request
        /// methods in the superclass
        /// </summary>
        /// <returns></returns>
        public Dictionary<UUID, InventoryFolderImpl> GetAllFolders()
        {
            Dictionary<UUID, InventoryFolderImpl> fs = new Dictionary<UUID, InventoryFolderImpl>();
            fs.Add(m_LibraryRootFolder.ID, m_LibraryRootFolder);
            List<InventoryFolderImpl> fis = TraverseFolder(m_LibraryRootFolder);
            foreach (InventoryFolderImpl f in fis)
            {
                fs.Add(f.ID, f);
            }
            //return libraryFolders;
            return fs;
        }

        private List<InventoryFolderImpl> TraverseFolder(InventoryFolderImpl node)
        {
            List<InventoryFolderImpl> folders = node.RequestListOfFolderImpls();
            List<InventoryFolderImpl> subs = new List<InventoryFolderImpl>();
            foreach (InventoryFolderImpl f in folders)
                subs.AddRange(TraverseFolder(f));

            folders.AddRange(subs);
            return folders;
        }
    }
}
