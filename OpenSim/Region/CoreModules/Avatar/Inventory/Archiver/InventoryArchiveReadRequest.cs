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
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.World.Archiver;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    public class InventoryArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected TarArchiveReader archive;
        private static ASCIIEncoding m_asciiEncoding = new ASCIIEncoding();

        private CachedUserInfo m_userInfo;
        private string m_invPath;
        
        /// <value>
        /// The stream from which the inventory archive will be loaded.
        /// </value>
        private Stream m_loadStream;
        
        CommunicationsManager commsManager;

        public InventoryArchiveReadRequest(
            CachedUserInfo userInfo, string invPath, string loadPath, CommunicationsManager commsManager)
            : this(
                userInfo,
                invPath, 
                new GZipStream(new FileStream(loadPath, FileMode.Open), CompressionMode.Decompress),
                commsManager)
        {
        }
        
        public InventoryArchiveReadRequest(
            CachedUserInfo userInfo, string invPath, Stream loadStream, CommunicationsManager commsManager)
        {
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_loadStream = loadStream;                        
            this.commsManager = commsManager;
        }        

        protected InventoryItemBase LoadInvItem(string contents)
        {
            InventoryItemBase item = new InventoryItemBase();
            StringReader sr = new StringReader(contents);
            XmlTextReader reader = new XmlTextReader(sr);

            if (contents.Equals("")) return null;

            reader.ReadStartElement("InventoryItem");
            reader.ReadStartElement("Name");
            item.Name = reader.ReadString();
            reader.ReadEndElement();
            reader.ReadStartElement("ID");
            item.ID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("InvType");
            item.InvType = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CreatorUUID");
            item.Creator = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CreationDate");
            item.CreationDate = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Owner");
            item.Owner = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadElementString("Description");
            reader.ReadStartElement("AssetType");
            item.AssetType = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("AssetID");
            item.AssetID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SaleType");
            item.SaleType = Convert.ToByte(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SalePrice");
            item.SalePrice = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("BasePermissions");
            item.BasePermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CurrentPermissions");
            item.CurrentPermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("EveryOnePermssions");
            item.EveryOnePermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("NextPermissions");
            item.NextPermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Flags");
            item.Flags = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupID");
            item.GroupID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupOwned");
            item.GroupOwned = Convert.ToBoolean(reader.ReadString());
            reader.ReadEndElement();

            return item;
        }

        /// <summary>
        /// Execute the request
        /// </summary>
        /// <returns>
        /// A list of the inventory nodes loaded.  If folders were loaded then only the root folders are
        /// returned
        /// </returns>
        public List<InventoryNodeBase> Execute()
        {
            string filePath = "ERROR";
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;
            int successfulItemRestores = 0;
            List<InventoryNodeBase> nodesLoaded = new List<InventoryNodeBase>();
            
            if (!m_userInfo.HasReceivedInventory)
            {
                // If the region server has access to the user admin service (by which users are created), 
                // then we'll assume that it's okay to fiddle with the user's inventory even if they are not on the 
                // server.
                //
                // FIXME: FetchInventory should probably be assumed to by async anyway, since even standalones might
                // use a remote inventory service, though this is vanishingly rare at the moment.
                if (null == commsManager.UserAdminService)
                {
                    m_log.ErrorFormat(
                        "[INVENTORY ARCHIVER]: Have not yet received inventory info for user {0} {1}",
                        m_userInfo.UserProfile.Name, m_userInfo.UserProfile.ID);

                    return nodesLoaded;
                }
                else
                {
                    m_userInfo.FetchInventory();
                }
            }

            InventoryFolderImpl rootDestinationFolder = m_userInfo.RootFolder.FindFolderByPath(m_invPath);

            if (null == rootDestinationFolder)
            {
                // Possibly provide an option later on to automatically create this folder if it does not exist
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: Inventory path {0} does not exist", m_invPath);

                return nodesLoaded;
            }

            archive = new TarArchiveReader(m_loadStream);
            
            // In order to load identically named folders, we need to keep track of the folders that we have already
            // created
            Dictionary <string, InventoryFolderImpl> foldersCreated = new Dictionary<string, InventoryFolderImpl>();

            byte[] data;
            TarArchiveReader.TarEntryType entryType;
            while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
            {
                if (entryType == TarArchiveReader.TarEntryType.TYPE_DIRECTORY) 
                {
                    m_log.WarnFormat("[INVENTORY ARCHIVER]: Ignoring directory entry {0}", filePath);
                } 
                else if (filePath.StartsWith(InventoryArchiveConstants.ASSETS_PATH))
                {
                    if (LoadAsset(filePath, data))
                        successfulAssetRestores++;
                    else
                        failedAssetRestores++;
                }
                else if (filePath.StartsWith(InventoryArchiveConstants.INVENTORY_PATH))
                {
                    InventoryItemBase item = LoadInvItem(m_asciiEncoding.GetString(data));

                    if (item != null)
                    {
                        // Don't use the item ID that's in the file
                        item.ID = UUID.Random();
                        
                        item.Creator = m_userInfo.UserProfile.ID;
                        item.Owner = m_userInfo.UserProfile.ID;
                        
                        string fsPath = filePath.Substring(InventoryArchiveConstants.INVENTORY_PATH.Length);
                        fsPath = fsPath.Remove(fsPath.LastIndexOf("/") + 1);
                        string originalFsPath = fsPath;
                        
                        m_log.DebugFormat("[INVENTORY ARCHIVER]: Loading to folder {0}", fsPath);
                        
                        InventoryFolderImpl foundFolder = null;
                        while (null == foundFolder && fsPath.Length > 0)
                        {
                            if (foldersCreated.ContainsKey(fsPath))
                            {
                                m_log.DebugFormat("[INVENTORY ARCHIVER]: Found previously created fs path {0}", fsPath);
                                foundFolder = foldersCreated[fsPath];
                            }
                            else
                            {
                                // Don't include the last slash
                                int penultimateSlashIndex = fsPath.LastIndexOf("/", fsPath.Length - 2);
                                
                                if (penultimateSlashIndex >= 0)
                                {
                                    fsPath = fsPath.Remove(penultimateSlashIndex + 1);
                                }
                                else
                                {
                                    m_log.DebugFormat(
                                        "[INVENTORY ARCHIVER]: Found no previously created fs path for {0}",
                                        originalFsPath);
                                    fsPath = string.Empty;
                                    foundFolder = rootDestinationFolder;
                                }
                            }                           
                        }
                        
                        string fsPathSectionToCreate = originalFsPath.Substring(fsPath.Length);
                        string[] rawDirsToCreate 
                            = fsPathSectionToCreate.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        int i = 0;
                        
                        while (i < rawDirsToCreate.Length)
                        {
                            m_log.DebugFormat("[INVENTORY ARCHIVER]: Creating folder {0}", rawDirsToCreate[i]);
                            
                            int identicalNameIdentifierIndex 
                                = rawDirsToCreate[i].LastIndexOf(
                                    InventoryArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR); 
                            string folderName = rawDirsToCreate[i].Remove(identicalNameIdentifierIndex);
                            
                            UUID newFolderId = UUID.Random();
                            m_userInfo.CreateFolder(
                                folderName, newFolderId, (ushort)AssetType.Folder, foundFolder.ID);
                            foundFolder = foundFolder.GetChildFolder(newFolderId);
                            
                            // Record that we have now created this folder
                            fsPath += rawDirsToCreate[i] + "/";
                            m_log.DebugFormat("[INVENTORY ARCHIVER]: Recording creation of fs path {0}", fsPath);
                            foldersCreated[fsPath] = foundFolder;
                            
                            if (0 == i)
                                nodesLoaded.Add(foundFolder);
                            
                            i++;
                        }
                        
                        /*
                        string[] rawFolders = filePath.Split(new char[] { '/' });                        
                        
                        // Find the folders that do exist along the path given
                        int i = 0;
                        bool noFolder = false;
                        InventoryFolderImpl foundFolder = rootDestinationFolder;
                        while (!noFolder && i < rawFolders.Length)
                        {
                            InventoryFolderImpl folder = foundFolder.FindFolderByPath(rawFolders[i]);
                            if (null != folder)
                            {
                                m_log.DebugFormat("[INVENTORY ARCHIVER]: Found folder {0}", folder.Name);
                                foundFolder = folder;
                                i++;
                            }
                            else
                            {
                                noFolder = true;
                            }                                   
                        }
                        
                        // Create any folders that did not previously exist
                        while (i < rawFolders.Length)
                        {
                            m_log.DebugFormat("[INVENTORY ARCHIVER]: Creating folder {0}", rawFolders[i]);
                            
                            UUID newFolderId = UUID.Random();
                            m_userInfo.CreateFolder(
                                rawFolders[i++], newFolderId, (ushort)AssetType.Folder, foundFolder.ID);
                            foundFolder = foundFolder.GetChildFolder(newFolderId);
                        } 
                        */                       

                        // Reset folder ID to the one in which we want to load it
                        item.Folder = foundFolder.ID;

                        m_userInfo.AddItem(item);
                        successfulItemRestores++;
                        
                        // If we're loading an item directly into the given destination folder then we need to record
                        // it separately from any loaded root folders
                        if (rootDestinationFolder == foundFolder)
                            nodesLoaded.Add(item);
                    }
                }
            }

            archive.Close();

            m_log.DebugFormat("[INVENTORY ARCHIVER]: Restored {0} assets", successfulAssetRestores);
            m_log.InfoFormat("[INVENTORY ARCHIVER]: Restored {0} items", successfulItemRestores);
            
            return nodesLoaded;
        }

        /// <summary>
        /// Load an asset
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        /// <returns>true if asset was successfully loaded, false otherwise</returns>
        private bool LoadAsset(string assetPath, byte[] data)
        {
            //IRegionSerialiser serialiser = scene.RequestModuleInterface<IRegionSerialiser>();
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, InventoryArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(InventoryArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                m_log.ErrorFormat(
                   "[INVENTORY ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, InventoryArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);

            if (InventoryArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = InventoryArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                //m_log.DebugFormat("[INVENTORY ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), "RandomName");

                asset.Type = assetType;
                asset.Data = data;

                commsManager.AssetCache.AddAsset(asset);

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                   "[INVENTORY ARCHIVER]: Tried to dearchive data with path {0} with an unknown type extension {1}",
                    assetPath, extension);

                return false;
            }
        }
    }
}
