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
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Osp;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.World.Archiver;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    public class InventoryArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected TarArchiveReader archive;

        private CachedUserInfo m_userInfo;
        private string m_invPath;

        /// <value>
        /// We only use this to request modules
        /// </value>
        protected Scene m_scene;

        /// <value>
        /// The stream from which the inventory archive will be loaded.
        /// </value>
        private Stream m_loadStream;

        public InventoryArchiveReadRequest(
            Scene scene, CachedUserInfo userInfo, string invPath, string loadPath)
            : this(
                scene,
                userInfo,
                invPath,
                new GZipStream(new FileStream(loadPath, FileMode.Open), CompressionMode.Decompress))
        {
        }

        public InventoryArchiveReadRequest(
            Scene scene, CachedUserInfo userInfo, string invPath, Stream loadStream)
        {
            m_scene = scene;
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_loadStream = loadStream;
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

            /*
            if (!m_userInfo.HasReceivedInventory)
            {
                // If the region server has access to the user admin service (by which users are created),
                // then we'll assume that it's okay to fiddle with the user's inventory even if they are not on the
                // server.
                //
                // FIXME: FetchInventory should probably be assumed to by async anyway, since even standalones might
                // use a remote inventory service, though this is vanishingly rare at the moment.
                if (null == m_scene.CommsManager.UserAdminService)
                {
                    m_log.ErrorFormat(
                        "[INVENTORY ARCHIVER]: Have not yet received inventory info for user {0} {1}",
                        m_userInfo.UserProfile.Name, m_userInfo.UserProfile.ID);

                    return nodesLoaded;
                }
                else
                {
                    m_userInfo.FetchInventory();
                    for (int i = 0 ; i < 50 ; i++)
                    {
                        if (m_userInfo.HasReceivedInventory == true)
                            break;
                        Thread.Sleep(200);
                    }
                }
            }
            */
           
            //InventoryFolderImpl rootDestinationFolder = m_userInfo.RootFolder.FindFolderByPath(m_invPath);
            InventoryFolderBase rootDestinationFolder 
                = InventoryArchiveUtils.FindFolderByPath(
                    m_scene.InventoryService, m_userInfo.UserProfile.ID, m_invPath);

            if (null == rootDestinationFolder)
            {
                // Possibly provide an option later on to automatically create this folder if it does not exist
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: Inventory path {0} does not exist", m_invPath);

                return nodesLoaded;
            }

            archive = new TarArchiveReader(m_loadStream);

            // In order to load identically named folders, we need to keep track of the folders that we have already
            // created
            Dictionary <string, InventoryFolderBase> foldersCreated = new Dictionary<string, InventoryFolderBase>();

            byte[] data;
            TarArchiveReader.TarEntryType entryType;
            while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
            {
                if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    if (LoadAsset(filePath, data))
                        successfulAssetRestores++;
                    else
                        failedAssetRestores++;
                }
                else if (filePath.StartsWith(ArchiveConstants.INVENTORY_PATH))
                {
                    InventoryFolderBase foundFolder 
                        = ReplicateArchivePathToUserInventory(
                            filePath, TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType, 
                            rootDestinationFolder, foldersCreated, nodesLoaded);

                    if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY != entryType)
                    {
                        InventoryItemBase item = UserInventoryItemSerializer.Deserialize(data);
                        
                        // Don't use the item ID that's in the file
                        item.ID = UUID.Random();

                        UUID ospResolvedId = OspResolver.ResolveOspa(item.CreatorId, m_scene.CommsManager); 
                        if (UUID.Zero != ospResolvedId)
                            item.CreatorIdAsUuid = ospResolvedId;
                        else
                            item.CreatorIdAsUuid = m_userInfo.UserProfile.ID;
                        
                        item.Owner = m_userInfo.UserProfile.ID;

                        // Reset folder ID to the one in which we want to load it
                        item.Folder = foundFolder.ID;

                        //m_userInfo.AddItem(item);
                        m_scene.InventoryService.AddItem(item);
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
        /// Replicate the inventory paths in the archive to the user's inventory as necessary.
        /// </summary>
        /// <param name="archivePath">The item archive path to replicate</param>
        /// <param name="isDir">Is the path we're dealing with a directory?</param>
        /// <param name="rootDestinationFolder">The root folder for the inventory load</param>
        /// <param name="foldersCreated">
        /// The folders created so far.  This method will add more folders if necessary
        /// </param>
        /// <param name="nodesLoaded">
        /// Track the inventory nodes created.  This is distinct from the folders created since for a particular folder
        /// chain, only the root node needs to be recorded
        /// </param>
        /// <returns>The last user inventory folder created or found for the archive path</returns>
        public InventoryFolderBase ReplicateArchivePathToUserInventory(
            string archivePath, 
            bool isDir, 
            InventoryFolderBase rootDestFolder, 
            Dictionary <string, InventoryFolderBase> foldersCreated,
            List<InventoryNodeBase> nodesLoaded)
        {
            archivePath = archivePath.Substring(ArchiveConstants.INVENTORY_PATH.Length);

            // Remove the file portion if we aren't already dealing with a directory path
            if (!isDir)
                archivePath = archivePath.Remove(archivePath.LastIndexOf("/") + 1);

            string originalArchivePath = archivePath;

            m_log.DebugFormat(
                "[INVENTORY ARCHIVER]: Loading to folder {0} {1}", rootDestFolder.Name, rootDestFolder.ID);

            InventoryFolderBase destFolder = null;

            // XXX: Nasty way of dealing with a path that has no directory component
            if (archivePath.Length > 0)
            {
                while (null == destFolder && archivePath.Length > 0)
                {
                    if (foldersCreated.ContainsKey(archivePath))
                    {
                        m_log.DebugFormat(
                            "[INVENTORY ARCHIVER]: Found previously created folder from archive path {0}", archivePath);
                        destFolder = foldersCreated[archivePath];
                    }
                    else
                    {
                        // Don't include the last slash
                        int penultimateSlashIndex = archivePath.LastIndexOf("/", archivePath.Length - 2);

                        if (penultimateSlashIndex >= 0)
                        {
                            archivePath = archivePath.Remove(penultimateSlashIndex + 1);
                        }
                        else
                        {
                            m_log.DebugFormat(
                                "[INVENTORY ARCHIVER]: Found no previously created folder for archive path {0}",
                                originalArchivePath);
                            archivePath = string.Empty;
                            destFolder = rootDestFolder;
                        }
                    }
                }
            }
            else
            {
                destFolder = rootDestFolder;
            }

            string archivePathSectionToCreate = originalArchivePath.Substring(archivePath.Length);
            string[] rawDirsToCreate
                = archivePathSectionToCreate.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;

            while (i < rawDirsToCreate.Length)
            {
                m_log.DebugFormat("[INVENTORY ARCHIVER]: Loading archived folder {0}", rawDirsToCreate[i]);

                int identicalNameIdentifierIndex
                    = rawDirsToCreate[i].LastIndexOf(
                        ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR);

                string newFolderName = rawDirsToCreate[i].Remove(identicalNameIdentifierIndex);
                UUID newFolderId = UUID.Random();

                // Asset type has to be Unknown here rather than Folder, otherwise the created folder can't be
                // deleted once the client has relogged.
                // The root folder appears to be labelled AssetType.Folder (shows up as "Category" in the client)
                // even though there is a AssetType.RootCategory
                destFolder 
                    = new InventoryFolderBase(
                        newFolderId, newFolderName, m_userInfo.UserProfile.ID, 
                        (short)AssetType.Unknown, destFolder.ID, 1);
                m_scene.InventoryService.AddFolder(destFolder);
                
//                UUID newFolderId = UUID.Random();
//                m_scene.InventoryService.AddFolder(
//                m_userInfo.CreateFolder(
//                    folderName, newFolderId, (ushort)AssetType.Folder, foundFolder.ID);

//                m_log.DebugFormat("[INVENTORY ARCHIVER]: Retrieving newly created folder {0}", folderName);
//                foundFolder = foundFolder.GetChildFolder(newFolderId);
//                m_log.DebugFormat(
//                    "[INVENTORY ARCHIVER]: Retrieved newly created folder {0} with ID {1}", 
//                    foundFolder.Name, foundFolder.ID);

                // Record that we have now created this folder
                archivePath += rawDirsToCreate[i] + "/";
                m_log.DebugFormat("[INVENTORY ARCHIVER]: Loaded archive path {0}", archivePath);
                foldersCreated[archivePath] = destFolder;

                if (0 == i)
                    nodesLoaded.Add(destFolder);

                i++;
            }
            
            return destFolder;
            
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
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                m_log.ErrorFormat(
                   "[INVENTORY ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                //m_log.DebugFormat("[INVENTORY ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), "RandomName");

                asset.Type = assetType;
                asset.Data = data;

                m_scene.AssetService.Store(asset);

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
