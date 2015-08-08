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
using System.Xml.Linq;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.World.Archiver;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    public class InventoryArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The maximum major version of archive that we can read.  Minor versions shouldn't need a max number since version
        /// bumps here should be compatible.
        /// </summary>
        public static int MAX_MAJOR_VERSION = 1;
        
        protected TarArchiveReader archive;

        private UserAccount m_userInfo;
        private string m_invPath;

        /// <value>
        /// ID of this request
        /// </value>
        protected UUID m_id;
        
        /// <summary>
        /// Do we want to merge this load with existing inventory?
        /// </summary>
        protected bool m_merge;

        protected IInventoryService m_InventoryService;
        protected IAssetService m_AssetService;
        protected IUserAccountService m_UserAccountService;

        private InventoryArchiverModule m_module;

        /// <value>
        /// The stream from which the inventory archive will be loaded.
        /// </value>
        private Stream m_loadStream;
        
        /// <summary>
        /// Has the control file been loaded for this archive?
        /// </summary>
        public bool ControlFileLoaded { get; private set; }
        
        /// <summary>
        /// Do we want to enforce the check.  IAR versions before 0.2 and 1.1 do not guarantee this order, so we can't
        /// enforce.
        /// </summary>
        public bool EnforceControlFileCheck { get; private set; }
        
        protected bool m_assetsLoaded;
        protected bool m_inventoryNodesLoaded;
        
        protected int m_successfulAssetRestores;
        protected int m_failedAssetRestores;
        protected int m_successfulItemRestores;                
        
        /// <summary>
        /// Root destination folder for the IAR load.
        /// </summary>
        protected InventoryFolderBase m_rootDestinationFolder;
        
        /// <summary>
        /// Inventory nodes loaded from the iar.
        /// </summary>
        protected HashSet<InventoryNodeBase> m_loadedNodes = new HashSet<InventoryNodeBase>();   
        
        /// <summary>
        /// In order to load identically named folders, we need to keep track of the folders that we have already
        /// resolved.
        /// </summary>
        Dictionary <string, InventoryFolderBase> m_resolvedFolders = new Dictionary<string, InventoryFolderBase>();        
                
        /// <summary>
        /// Record the creator id that should be associated with an asset.  This is used to adjust asset creator ids
        /// after OSP resolution (since OSP creators are only stored in the item
        /// </summary>
        protected Dictionary<UUID, UUID> m_creatorIdForAssetId = new Dictionary<UUID, UUID>();

        public InventoryArchiveReadRequest(
            IInventoryService inv, IAssetService assets, IUserAccountService uacc, UserAccount userInfo, string invPath, string loadPath, bool merge)
            : this(UUID.Zero, null,
                            inv,
                assets,
                uacc,
                userInfo,
                invPath,
                loadPath,
                merge)
        {
        }

        public InventoryArchiveReadRequest(
            UUID id, InventoryArchiverModule module, IInventoryService inv, IAssetService assets, IUserAccountService uacc, UserAccount userInfo, string invPath, string loadPath, bool merge)
            : this(
                id,
                module,
                inv,
                assets,
                uacc,
                userInfo,
                invPath,
                new GZipStream(ArchiveHelpers.GetStream(loadPath), CompressionMode.Decompress),
                merge)
        {
        }

        public InventoryArchiveReadRequest(
            UUID id, InventoryArchiverModule module, IInventoryService inv, IAssetService assets, IUserAccountService uacc, UserAccount userInfo, string invPath, Stream loadStream, bool merge)
        {
            m_id = id;
            m_InventoryService = inv;
            m_AssetService = assets;
            m_UserAccountService = uacc;
            m_merge = merge;
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_loadStream = loadStream;
            m_module = module;
            
            // FIXME: Do not perform this check since older versions of OpenSim do save the control file after other things
            // (I thought they weren't).  We will need to bump the version number and perform this check on all 
            // subsequent IAR versions only
            ControlFileLoaded = true;
        }

        /// <summary>
        /// Execute the request
        /// </summary>
        /// <remarks>
        /// Only call this once.  To load another IAR, construct another request object.
        /// </remarks>
        /// <returns>
        /// A list of the inventory nodes loaded.  If folders were loaded then only the root folders are
        /// returned
        /// </returns>
        /// <exception cref="System.Exception">Thrown if load fails.</exception>
        public HashSet<InventoryNodeBase> Execute()
        {
            try
            {
                Exception reportedException = null;

                string filePath = "ERROR";
               
                List<InventoryFolderBase> folderCandidates
                    = InventoryArchiveUtils.FindFoldersByPath(
                        m_InventoryService, m_userInfo.PrincipalID, m_invPath);
    
                if (folderCandidates.Count == 0)
                {
                    // Possibly provide an option later on to automatically create this folder if it does not exist
                    m_log.ErrorFormat("[INVENTORY ARCHIVER]: Inventory path {0} does not exist", m_invPath);
    
                    return m_loadedNodes;
                }
                
                m_rootDestinationFolder = folderCandidates[0];
                archive = new TarArchiveReader(m_loadStream);
                byte[] data;
                TarArchiveReader.TarEntryType entryType;

                while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
                {
                    if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                    {
                        LoadControlFile(filePath, data);
                    }                    
                    else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                    {
                        LoadAssetFile(filePath, data);
                    }
                    else if (filePath.StartsWith(ArchiveConstants.INVENTORY_PATH))
                    {
                        LoadInventoryFile(filePath, entryType, data);
                    }
                }
                
                archive.Close();

                m_log.DebugFormat(
                    "[INVENTORY ARCHIVER]: Successfully loaded {0} assets with {1} failures", 
                    m_successfulAssetRestores, m_failedAssetRestores);

                //Alicia: When this is called by LibraryModule or Tests, m_module will be null as event is not required
                if(m_module != null)
                    m_module.TriggerInventoryArchiveLoaded(m_id, true, m_userInfo, m_invPath, m_loadStream, reportedException, m_successfulItemRestores);
                
                return m_loadedNodes;
            }
            catch(Exception Ex)
            {
                // Trigger saved event with failed result and exception data
                if (m_module != null)
                    m_module.TriggerInventoryArchiveLoaded(m_id, false, m_userInfo, m_invPath, m_loadStream, Ex, 0);

                return m_loadedNodes;
            }
            finally
            {
                m_loadStream.Close();
            }
        }

        public void Close()
        {
            if (m_loadStream != null)
                m_loadStream.Close();
        }

        /// <summary>
        /// Replicate the inventory paths in the archive to the user's inventory as necessary.
        /// </summary>
        /// <param name="iarPath">The item archive path to replicate</param>
        /// <param name="rootDestinationFolder">The root folder for the inventory load</param>
        /// <param name="resolvedFolders">
        /// The folders that we have resolved so far for a given archive path.
        /// This method will add more folders if necessary
        /// </param>
        /// <param name="loadedNodes">
        /// Track the inventory nodes created.
        /// </param>
        /// <returns>The last user inventory folder created or found for the archive path</returns>
        public InventoryFolderBase ReplicateArchivePathToUserInventory(
            string iarPath, 
            InventoryFolderBase rootDestFolder, 
            Dictionary <string, InventoryFolderBase> resolvedFolders,
            HashSet<InventoryNodeBase> loadedNodes)
        {
            string iarPathExisting = iarPath;

//            m_log.DebugFormat(
//                "[INVENTORY ARCHIVER]: Loading folder {0} {1}", rootDestFolder.Name, rootDestFolder.ID);
                        
            InventoryFolderBase destFolder 
                = ResolveDestinationFolder(rootDestFolder, ref iarPathExisting, resolvedFolders);
            
//            m_log.DebugFormat(
//                "[INVENTORY ARCHIVER]: originalArchivePath [{0}], section already loaded [{1}]", 
//                iarPath, iarPathExisting);
            
            string iarPathToCreate = iarPath.Substring(iarPathExisting.Length);
            CreateFoldersForPath(destFolder, iarPathExisting, iarPathToCreate, resolvedFolders, loadedNodes);
            
            return destFolder;
        }

        /// <summary>
        /// Resolve a destination folder
        /// </summary>
        /// 
        /// We require here a root destination folder (usually the root of the user's inventory) and the archive
        /// path.  We also pass in a list of previously resolved folders in case we've found this one previously.
        /// 
        /// <param name="archivePath">
        /// The item archive path to resolve.  The portion of the path passed back is that
        /// which corresponds to the resolved desintation folder.
        /// <param name="rootDestinationFolder">
        /// The root folder for the inventory load
        /// </param>
        /// <param name="resolvedFolders">
        /// The folders that we have resolved so far for a given archive path.
        /// </param>
        /// <returns>
        /// The folder in the user's inventory that matches best the archive path given.  If no such folder was found
        /// then the passed in root destination folder is returned.
        /// </returns>
        protected InventoryFolderBase ResolveDestinationFolder(
            InventoryFolderBase rootDestFolder,
            ref string archivePath,
            Dictionary <string, InventoryFolderBase> resolvedFolders)
        {
//            string originalArchivePath = archivePath;

            while (archivePath.Length > 0)
            {
//                m_log.DebugFormat("[INVENTORY ARCHIVER]: Trying to resolve destination folder {0}", archivePath);
                
                if (resolvedFolders.ContainsKey(archivePath))
                {
//                    m_log.DebugFormat(
//                        "[INVENTORY ARCHIVER]: Found previously created folder from archive path {0}", archivePath);
                    return resolvedFolders[archivePath];
                }
                else
                {
                    if (m_merge)
                    {
                        // TODO: Using m_invPath is totally wrong - what we need to do is strip the uuid from the 
                        // iar name and try to find that instead.
                        string plainPath = ArchiveConstants.ExtractPlainPathFromIarPath(archivePath);
                        List<InventoryFolderBase> folderCandidates
                            = InventoryArchiveUtils.FindFoldersByPath(
                                m_InventoryService, m_userInfo.PrincipalID, plainPath);
            
                        if (folderCandidates.Count != 0)
                        {
                            InventoryFolderBase destFolder = folderCandidates[0];
                            resolvedFolders[archivePath] = destFolder;
                            return destFolder;
                        }
                    }
                    
                    // Don't include the last slash so find the penultimate one
                    int penultimateSlashIndex = archivePath.LastIndexOf("/", archivePath.Length - 2);

                    if (penultimateSlashIndex >= 0)
                    {
                        // Remove the last section of path so that we can see if we've already resolved the parent
                        archivePath = archivePath.Remove(penultimateSlashIndex + 1);
                    }
                    else
                    {
//                        m_log.DebugFormat(
//                            "[INVENTORY ARCHIVER]: Found no previously created folder for archive path {0}",
//                            originalArchivePath);
                        archivePath = string.Empty;
                        return rootDestFolder;
                    }
                }
            }
            
            return rootDestFolder;
        }
        
        /// <summary>
        /// Create a set of folders for the given path.
        /// </summary>
        /// <param name="destFolder">
        /// The root folder from which the creation will take place.
        /// </param>
        /// <param name="iarPathExisting">
        /// the part of the iar path that already exists
        /// </param>
        /// <param name="iarPathToReplicate">
        /// The path to replicate in the user's inventory from iar
        /// </param>
        /// <param name="resolvedFolders">
        /// The folders that we have resolved so far for a given archive path.
        /// </param>
        /// <param name="loadedNodes">
        /// Track the inventory nodes created.
        /// </param>
        protected void CreateFoldersForPath(
            InventoryFolderBase destFolder, 
            string iarPathExisting,
            string iarPathToReplicate, 
            Dictionary <string, InventoryFolderBase> resolvedFolders, 
            HashSet<InventoryNodeBase> loadedNodes)
        {
            string[] rawDirsToCreate = iarPathToReplicate.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < rawDirsToCreate.Length; i++)
            {
//                m_log.DebugFormat("[INVENTORY ARCHIVER]: Creating folder {0} from IAR", rawDirsToCreate[i]);

                if (!rawDirsToCreate[i].Contains(ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR))
                    continue;
                
                int identicalNameIdentifierIndex
                    = rawDirsToCreate[i].LastIndexOf(
                        ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR);

                string newFolderName = rawDirsToCreate[i].Remove(identicalNameIdentifierIndex);

                newFolderName = InventoryArchiveUtils.UnescapeArchivePath(newFolderName);
                UUID newFolderId = UUID.Random();

                destFolder 
                    = new InventoryFolderBase(
                        newFolderId, newFolderName, m_userInfo.PrincipalID,
                        (short)FolderType.None, destFolder.ID, 1);
                m_InventoryService.AddFolder(destFolder);

                // Record that we have now created this folder
                iarPathExisting += rawDirsToCreate[i] + "/";
                m_log.DebugFormat("[INVENTORY ARCHIVER]: Created folder {0} from IAR", iarPathExisting);
                resolvedFolders[iarPathExisting] = destFolder;

                if (0 == i)
                    loadedNodes.Add(destFolder);
            }
        }
        
        /// <summary>
        /// Load an item from the archive
        /// </summary>
        /// <param name="filePath">The archive path for the item</param>
        /// <param name="data">The raw item data</param>
        /// <param name="rootDestinationFolder">The root destination folder for loaded items</param>
        /// <param name="nodesLoaded">All the inventory nodes (items and folders) loaded so far</param>
        protected InventoryItemBase LoadItem(byte[] data, InventoryFolderBase loadFolder)
        {
            InventoryItemBase item = UserInventoryItemSerializer.Deserialize(data);
            
            // Don't use the item ID that's in the file
            item.ID = UUID.Random();

            UUID ospResolvedId = OspResolver.ResolveOspa(item.CreatorId, m_UserAccountService);
            if (UUID.Zero != ospResolvedId) // The user exists in this grid
            {
//                m_log.DebugFormat("[INVENTORY ARCHIVER]: Found creator {0} via OSPA resolution", ospResolvedId);
                
//                item.CreatorIdAsUuid = ospResolvedId;

                // Don't preserve the OSPA in the creator id (which actually gets persisted to the
                // database).  Instead, replace with the UUID that we found.
                item.CreatorId = ospResolvedId.ToString();
                item.CreatorData = string.Empty;
            }
            else if (string.IsNullOrEmpty(item.CreatorData))
            {
                item.CreatorId = m_userInfo.PrincipalID.ToString();
//                item.CreatorIdAsUuid = new UUID(item.CreatorId);
            }

            item.Owner = m_userInfo.PrincipalID;

            // Reset folder ID to the one in which we want to load it
            item.Folder = loadFolder.ID;

            // Record the creator id for the item's asset so that we can use it later, if necessary, when the asset
            // is loaded.
            // FIXME: This relies on the items coming before the assets in the TAR file.  Need to create stronger
            // checks for this, and maybe even an external tool for creating OARs which enforces this, rather than
            // relying on native tar tools.
            m_creatorIdForAssetId[item.AssetID] = item.CreatorIdAsUuid;

            if (!m_InventoryService.AddItem(item))
                m_log.WarnFormat("[INVENTORY ARCHIVER]: Unable to save item {0} in folder {1}", item.Name, item.Folder);
        
            return item;
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
            string rawUuid = filename.Remove(filename.Length - extension.Length);
            UUID assetId = new UUID(rawUuid);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                if (assetType == (sbyte)AssetType.Unknown)
                {
                    m_log.WarnFormat("[INVENTORY ARCHIVER]: Importing {0} byte asset {1} with unknown type", data.Length, assetId);
                }
                else if (assetType == (sbyte)AssetType.Object)
                {
                    if (m_creatorIdForAssetId.ContainsKey(assetId))
                    {
                        data = SceneObjectSerializer.ModifySerializedObject(assetId, data,
                            sog => {
                                bool modified = false;
                                
                                foreach (SceneObjectPart sop in sog.Parts)
                                {
                                    if (string.IsNullOrEmpty(sop.CreatorData))
                                    {
                                        sop.CreatorID = m_creatorIdForAssetId[assetId];
                                        modified = true;
                                    }
                                }
                                
                                return modified;
                            });
                        
                        if (data == null)
                            return false;
                    }
                }

                //m_log.DebugFormat("[INVENTORY ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(assetId, "From IAR", assetType, UUID.Zero.ToString());
                asset.Data = data;

                m_AssetService.Store(asset);

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

        /// <summary>
        /// Load control file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        public void LoadControlFile(string path, byte[] data)
        {
            XDocument doc = XDocument.Parse(Encoding.ASCII.GetString(data));
            XElement archiveElement = doc.Element("archive");
            int majorVersion = int.Parse(archiveElement.Attribute("major_version").Value);
            int minorVersion = int.Parse(archiveElement.Attribute("minor_version").Value);
            string version = string.Format("{0}.{1}", majorVersion, minorVersion);
                        
            if (majorVersion > MAX_MAJOR_VERSION)
            {
                throw new Exception(
                    string.Format(
                        "The IAR you are trying to load has major version number of {0} but this version of OpenSim can only load IARs with major version number {1} and below",
                        majorVersion, MAX_MAJOR_VERSION));
            }
            
            ControlFileLoaded = true;            
            m_log.InfoFormat("[INVENTORY ARCHIVER]: Loading IAR with version {0}", version);                        
        }
        
        /// <summary>
        /// Load inventory file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="entryType"></param>
        /// <param name="data"></param>        
        protected void LoadInventoryFile(string path, TarArchiveReader.TarEntryType entryType, byte[] data)
        {
            if (!ControlFileLoaded)
                throw new Exception(
                    string.Format(
                        "The IAR you are trying to load does not list {0} before {1}.  Aborting load", 
                        ArchiveConstants.CONTROL_FILE_PATH, ArchiveConstants.INVENTORY_PATH));
            
            if (m_assetsLoaded)
                throw new Exception(
                    string.Format(
                        "The IAR you are trying to load does not list all {0} before {1}.  Aborting load", 
                        ArchiveConstants.INVENTORY_PATH, ArchiveConstants.ASSETS_PATH));                            
            
            path = path.Substring(ArchiveConstants.INVENTORY_PATH.Length);
            
            // Trim off the file portion if we aren't already dealing with a directory path
            if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY != entryType)
                path = path.Remove(path.LastIndexOf("/") + 1);
            
            InventoryFolderBase foundFolder 
                = ReplicateArchivePathToUserInventory(
                    path, m_rootDestinationFolder, m_resolvedFolders, m_loadedNodes);

            if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY != entryType)
            {
                InventoryItemBase item = LoadItem(data, foundFolder);

                if (item != null)
                {
                    m_successfulItemRestores++;
                    
                    // If we aren't loading the folder containing the item then well need to update the 
                    // viewer separately for that item.
                    if (!m_loadedNodes.Contains(foundFolder))
                        m_loadedNodes.Add(item);
                }
            }
            
            m_inventoryNodesLoaded = true;            
        }
        
        /// <summary>
        /// Load asset file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        protected void LoadAssetFile(string path, byte[] data)
        {
            if (!ControlFileLoaded)
                throw new Exception(
                    string.Format(
                        "The IAR you are trying to load does not list {0} before {1}.  Aborting load", 
                        ArchiveConstants.CONTROL_FILE_PATH, ArchiveConstants.ASSETS_PATH));
            
            if (!m_inventoryNodesLoaded)
                throw new Exception(
                    string.Format(
                        "The IAR you are trying to load does not list all {0} before {1}.  Aborting load", 
                        ArchiveConstants.INVENTORY_PATH, ArchiveConstants.ASSETS_PATH));                            
            
            if (LoadAsset(path, data))
                m_successfulAssetRestores++;
            else
                m_failedAssetRestores++;

            if ((m_successfulAssetRestores) % 50 == 0)
                m_log.DebugFormat(
                    "[INVENTORY ARCHIVER]: Loaded {0} assets...", 
                    m_successfulAssetRestores);                       
            
            m_assetsLoaded = true;            
        }                 
    }
}
