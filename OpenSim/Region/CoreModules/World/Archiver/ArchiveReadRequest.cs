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
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using System.Threading;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Handles an individual archive read request
    /// </summary>
    public class ArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Contains data used while dearchiving a single scene.
        /// </summary>
        private class DearchiveContext
        {
            public Scene Scene { get; set; }

            public List<string> SerialisedSceneObjects { get; set; }

            public List<string> SerialisedParcels { get; set; }

            public List<SceneObjectGroup> SceneObjects { get; set; }

            public DearchiveContext(Scene scene)
            {
                Scene = scene;
                SerialisedSceneObjects = new List<string>();
                SerialisedParcels = new List<string>();
                SceneObjects = new List<SceneObjectGroup>();
            }
        }
        

        /// <summary>
        /// The maximum major version of OAR that we can read.  Minor versions shouldn't need a max number since version
        /// bumps here should be compatible.
        /// </summary>
        public static int MAX_MAJOR_VERSION = 1;
        
        /// <summary>
        /// Has the control file been loaded for this archive?
        /// </summary>
        public bool ControlFileLoaded { get; private set; }

        protected string m_loadPath;
        protected Scene m_rootScene;
        protected Stream m_loadStream;
        protected Guid m_requestId;
        protected string m_errorMessage;

        /// <value>
        /// Should the archive being loaded be merged with what is already on the region?
        /// </value>
        protected bool m_merge;

        /// <value>
        /// Should we ignore any assets when reloading the archive?
        /// </value>
        protected bool m_skipAssets;

        /// <summary>
        /// Used to cache lookups for valid uuids.
        /// </summary>
        private IDictionary<UUID, bool> m_validUserUuids = new Dictionary<UUID, bool>();

        private IUserManagement m_UserMan;
        private IUserManagement UserManager
        {
            get
            {
                if (m_UserMan == null)
                {
                    m_UserMan = m_rootScene.RequestModuleInterface<IUserManagement>();
                }
                return m_UserMan;
            }
        }

        /// <summary>
        /// Used to cache lookups for valid groups.
        /// </summary>
        private IDictionary<UUID, bool> m_validGroupUuids = new Dictionary<UUID, bool>();

        private IGroupsModule m_groupsModule;

        private IAssetService m_assetService = null;


        public ArchiveReadRequest(Scene scene, string loadPath, bool merge, bool skipAssets, Guid requestId)
        {
            m_rootScene = scene;

            m_loadPath = loadPath;
            try
            {
                m_loadStream = new GZipStream(ArchiveHelpers.GetStream(loadPath), CompressionMode.Decompress);
            }
            catch (EntryPointNotFoundException e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                m_log.Error(e);
            }
        
            m_errorMessage = String.Empty;
            m_merge = merge;
            m_skipAssets = skipAssets;
            m_requestId = requestId;

            // Zero can never be a valid user id
            m_validUserUuids[UUID.Zero] = false;

            m_groupsModule = m_rootScene.RequestModuleInterface<IGroupsModule>();
            m_assetService = m_rootScene.AssetService;
        }

        public ArchiveReadRequest(Scene scene, Stream loadStream, bool merge, bool skipAssets, Guid requestId)
        {
            m_rootScene = scene;
            m_loadPath = null;
            m_loadStream = loadStream;
            m_merge = merge;
            m_skipAssets = skipAssets;
            m_requestId = requestId;

            // Zero can never be a valid user id
            m_validUserUuids[UUID.Zero] = false;

            m_groupsModule = m_rootScene.RequestModuleInterface<IGroupsModule>();
            m_assetService = m_rootScene.AssetService;
        }

        /// <summary>
        /// Dearchive the region embodied in this request.
        /// </summary>
        public void DearchiveRegion()
        {
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;

            DearchiveScenesInfo dearchivedScenes;

            // We dearchive all the scenes at once, because the files in the TAR archive might be mixed.
            // Therefore, we have to keep track of the dearchive context of all the scenes.
            Dictionary<UUID, DearchiveContext> sceneContexts = new Dictionary<UUID, DearchiveContext>();

            string fullPath = "NONE";
            TarArchiveReader archive = null;
            byte[] data;
            TarArchiveReader.TarEntryType entryType;

            try
            {
                FindAndLoadControlFile(out archive, out dearchivedScenes);

                while ((data = archive.ReadEntry(out fullPath, out entryType)) != null)
                {
                    //m_log.DebugFormat(
                    //    "[ARCHIVER]: Successfully read {0} ({1} bytes)", filePath, data.Length);
                    
                    if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                        continue;


                    // Find the scene that this file belongs to

                    Scene scene;
                    string filePath;
                    if (!dearchivedScenes.GetRegionFromPath(fullPath, out scene, out filePath))
                        continue;   // this file belongs to a region that we're not loading

                    DearchiveContext sceneContext = null;
                    if (scene != null)
                    {
                        if (!sceneContexts.TryGetValue(scene.RegionInfo.RegionID, out sceneContext))
                        {
                            sceneContext = new DearchiveContext(scene);
                            sceneContexts.Add(scene.RegionInfo.RegionID, sceneContext);
                        }
                    }


                    // Process the file

                    if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                    {
                        sceneContext.SerialisedSceneObjects.Add(Encoding.UTF8.GetString(data));
                    }
                    else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH) && !m_skipAssets)
                    {
                        if (LoadAsset(filePath, data))
                            successfulAssetRestores++;
                        else
                            failedAssetRestores++;

                        if ((successfulAssetRestores + failedAssetRestores) % 250 == 0)
                            m_log.Debug("[ARCHIVER]: Loaded " + successfulAssetRestores + " assets and failed to load " + failedAssetRestores + " assets...");
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.TERRAINS_PATH))
                    {
                        LoadTerrain(scene, filePath, data);
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.SETTINGS_PATH))
                    {
                        LoadRegionSettings(scene, filePath, data, dearchivedScenes);
                    } 
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.LANDDATA_PATH))
                    {
                        sceneContext.SerialisedParcels.Add(Encoding.UTF8.GetString(data));
                    } 
                    else if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                    {
                        // Ignore, because we already read the control file
                    }
                }

                //m_log.Debug("[ARCHIVER]: Reached end of archive");
            }
            catch (Exception e)
            {
                m_log.Error(
                    String.Format("[ARCHIVER]: Aborting load with error in archive file {0} ", fullPath), e);
                m_errorMessage += e.ToString();
                m_rootScene.EventManager.TriggerOarFileLoaded(m_requestId, new List<UUID>(), m_errorMessage);
                return;
            }
            finally
            {
                if (archive != null)
                    archive.Close();
            }

            if (!m_skipAssets)
            {
                m_log.InfoFormat("[ARCHIVER]: Restored {0} assets", successfulAssetRestores);

                if (failedAssetRestores > 0)
                {
                    m_log.ErrorFormat("[ARCHIVER]: Failed to load {0} assets", failedAssetRestores);
                    m_errorMessage += String.Format("Failed to load {0} assets", failedAssetRestores);
                }
            }

            foreach (DearchiveContext sceneContext in sceneContexts.Values)
            {
                m_log.InfoFormat("[ARCHIVER]: Loading region {0}", sceneContext.Scene.RegionInfo.RegionName);

                if (!m_merge)
                {
                    m_log.Info("[ARCHIVER]: Clearing all existing scene objects");
                    sceneContext.Scene.DeleteAllSceneObjects();
                }

                try
                {
                    LoadParcels(sceneContext.Scene, sceneContext.SerialisedParcels);
                    LoadObjects(sceneContext.Scene, sceneContext.SerialisedSceneObjects, sceneContext.SceneObjects);
                    
                    // Inform any interested parties that the region has changed. We waited until now so that all
                    // of the region's objects will be loaded when we send this notification.
                    IEstateModule estateModule = sceneContext.Scene.RequestModuleInterface<IEstateModule>();
                    if (estateModule != null)
                        estateModule.TriggerRegionInfoChange();
                }
                catch (Exception e)
                {
                    m_log.Error("[ARCHIVER]: Error loading parcels or objects ", e);
                    m_errorMessage += e.ToString();
                    m_rootScene.EventManager.TriggerOarFileLoaded(m_requestId, new List<UUID>(), m_errorMessage);
                    return;
                }
            }

            // Start the scripts. We delayed this because we want the OAR to finish loading ASAP, so
            // that users can enter the scene. If we allow the scripts to start in the loop above
            // then they significantly increase the time until the OAR finishes loading.
            Util.FireAndForget(delegate(object o)
            {
                Thread.Sleep(15000);
                m_log.Info("[ARCHIVER]: Starting scripts in scene objects");

                foreach (DearchiveContext sceneContext in sceneContexts.Values)
                {
                    foreach (SceneObjectGroup sceneObject in sceneContext.SceneObjects)
                    {
                        sceneObject.CreateScriptInstances(0, false, sceneContext.Scene.DefaultScriptEngine, 0); // StateSource.RegionStart
                        sceneObject.ResumeScripts();
                    }

                    sceneContext.SceneObjects.Clear();
                }
            });

            m_log.InfoFormat("[ARCHIVER]: Successfully loaded archive");

            m_rootScene.EventManager.TriggerOarFileLoaded(m_requestId, dearchivedScenes.GetLoadedScenes(), m_errorMessage);
        }

        /// <summary>
        /// Searches through the files in the archive for the control file, and reads it.
        /// We must read the control file first, in order to know which regions are available.
        /// </summary>
        /// <remarks>
        /// In most cases the control file *is* first, since that's how we create archives. However,
        /// it's possible that someone rewrote the archive externally so we can't rely on this fact.
        /// </remarks>
        /// <param name="archive"></param>
        /// <param name="dearchivedScenes"></param>
        private void FindAndLoadControlFile(out TarArchiveReader archive, out DearchiveScenesInfo dearchivedScenes)
        {
            archive = new TarArchiveReader(m_loadStream);
            dearchivedScenes = new DearchiveScenesInfo();

            string filePath;
            byte[] data;
            TarArchiveReader.TarEntryType entryType;
            bool firstFile = true;

            while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
            {
                if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                    continue;
                    
                if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                {
                    LoadControlFile(filePath, data, dearchivedScenes);

                    // Find which scenes are available in the simulator
                    ArchiveScenesGroup simulatorScenes = new ArchiveScenesGroup();
                    SceneManager.Instance.ForEachScene(delegate(Scene scene2)
                    {
                        simulatorScenes.AddScene(scene2);
                    });
                    simulatorScenes.CalcSceneLocations();
                    dearchivedScenes.SetSimulatorScenes(m_rootScene, simulatorScenes);

                    // If the control file wasn't the first file then reset the read pointer
                    if (!firstFile)
                    {
                        m_log.Warn("Control file wasn't the first file in the archive");
                        if (m_loadStream.CanSeek)
                        {
                            m_loadStream.Seek(0, SeekOrigin.Begin);
                        }
                        else if (m_loadPath != null)
                        {
                            archive.Close();
                            archive = null;
                            m_loadStream.Close();
                            m_loadStream = null;
                            m_loadStream = new GZipStream(ArchiveHelpers.GetStream(m_loadPath), CompressionMode.Decompress);
                            archive = new TarArchiveReader(m_loadStream);
                        }
                        else
                        {
                            // There isn't currently a scenario where this happens, but it's best to add a check just in case
                            throw new Exception("Error reading archive: control file wasn't the first file, and the input stream doesn't allow seeking");
                        }
                    }

                    return;
                }

                firstFile = false;
            }

            throw new Exception("Control file not found");
        }
        
        /// <summary>
        /// Load serialized scene objects.
        /// </summary>
        protected void LoadObjects(Scene scene, List<string> serialisedSceneObjects, List<SceneObjectGroup> sceneObjects)
        {
            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading {0} scene objects.  Please wait.", serialisedSceneObjects.Count);

            UUID oldTelehubUUID = scene.RegionInfo.RegionSettings.TelehubObject;

            IRegionSerialiserModule serialiser = scene.RequestModuleInterface<IRegionSerialiserModule>();
            int sceneObjectsLoadedCount = 0;

            foreach (string serialisedSceneObject in serialisedSceneObjects)
            {
                /*
                m_log.DebugFormat("[ARCHIVER]: Loading xml with raw size {0}", serialisedSceneObject.Length);

                // Really large xml files (multi megabyte) appear to cause
                // memory problems
                // when loading the xml.  But don't enable this check yet
                
                if (serialisedSceneObject.Length > 5000000)
                {
                    m_log.Error("[ARCHIVER]: Ignoring xml since size > 5000000);");
                    continue;
                }
                */

                SceneObjectGroup sceneObject = serialiser.DeserializeGroupFromXml2(serialisedSceneObject);

                bool isTelehub = (sceneObject.UUID == oldTelehubUUID) && (oldTelehubUUID != UUID.Zero);

                // For now, give all incoming scene objects new uuids.  This will allow scenes to be cloned
                // on the same region server and multiple examples a single object archive to be imported
                // to the same scene (when this is possible).
                sceneObject.ResetIDs();

                if (isTelehub)
                {
                    // Change the Telehub Object to the new UUID
                    scene.RegionInfo.RegionSettings.TelehubObject = sceneObject.UUID;
                    scene.RegionInfo.RegionSettings.Save();
                    oldTelehubUUID = UUID.Zero;
                }

                // Try to retain the original creator/owner/lastowner if their uuid is present on this grid
                // or creator data is present.  Otherwise, use the estate owner instead.
                foreach (SceneObjectPart part in sceneObject.Parts)
                {
                    if (part.CreatorData == null || part.CreatorData == string.Empty)
                    {
                        if (!ResolveUserUuid(scene, part.CreatorID))
                            part.CreatorID = scene.RegionInfo.EstateSettings.EstateOwner;
                    }
                    if (UserManager != null)
                        UserManager.AddUser(part.CreatorID, part.CreatorData);

                    if (!ResolveUserUuid(scene, part.OwnerID))
                        part.OwnerID = scene.RegionInfo.EstateSettings.EstateOwner;

                    if (!ResolveUserUuid(scene, part.LastOwnerID))
                        part.LastOwnerID = scene.RegionInfo.EstateSettings.EstateOwner;

                    if (!ResolveGroupUuid(part.GroupID))
                        part.GroupID = UUID.Zero;

                    // And zap any troublesome sit target information
//                    part.SitTargetOrientation = new Quaternion(0, 0, 0, 1);
//                    part.SitTargetPosition    = new Vector3(0, 0, 0);

                    // Fix ownership/creator of inventory items
                    // Not doing so results in inventory items
                    // being no copy/no mod for everyone
                    lock (part.TaskInventory)
                    {
                        TaskInventoryDictionary inv = part.TaskInventory;
                        foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv)
                        {
                            if (!ResolveUserUuid(scene, kvp.Value.OwnerID))
                            {
                                kvp.Value.OwnerID = scene.RegionInfo.EstateSettings.EstateOwner;
                            }

                            if (kvp.Value.CreatorData == null || kvp.Value.CreatorData == string.Empty)
                            {
                                if (!ResolveUserUuid(scene, kvp.Value.CreatorID))
                                    kvp.Value.CreatorID = scene.RegionInfo.EstateSettings.EstateOwner;
                            }

                            if (UserManager != null)
                                UserManager.AddUser(kvp.Value.CreatorID, kvp.Value.CreatorData);

                            if (!ResolveGroupUuid(kvp.Value.GroupID))
                                kvp.Value.GroupID = UUID.Zero;
                        }
                    }
                }

                if (scene.AddRestoredSceneObject(sceneObject, true, false))
                {
                    sceneObjectsLoadedCount++;
                    sceneObject.CreateScriptInstances(0, false, scene.DefaultScriptEngine, 0);
                    sceneObject.ResumeScripts();
                }
            }

            m_log.InfoFormat("[ARCHIVER]: Restored {0} scene objects to the scene", sceneObjectsLoadedCount);

            int ignoredObjects = serialisedSceneObjects.Count - sceneObjectsLoadedCount;

            if (ignoredObjects > 0)
                m_log.WarnFormat("[ARCHIVER]: Ignored {0} scene objects that already existed in the scene", ignoredObjects);

            if (oldTelehubUUID != UUID.Zero)
            {
                m_log.WarnFormat("Telehub object not found: {0}", oldTelehubUUID);
                scene.RegionInfo.RegionSettings.TelehubObject = UUID.Zero;
                scene.RegionInfo.RegionSettings.ClearSpawnPoints();
            }
        }
        
        /// <summary>
        /// Load serialized parcels.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="serialisedParcels"></param>
        protected void LoadParcels(Scene scene, List<string> serialisedParcels)
        {
            // Reload serialized parcels
            m_log.InfoFormat("[ARCHIVER]: Loading {0} parcels.  Please wait.", serialisedParcels.Count);
            List<LandData> landData = new List<LandData>();
            foreach (string serialisedParcel in serialisedParcels)
            {
                LandData parcel = LandDataSerializer.Deserialize(serialisedParcel);
                
                // Validate User and Group UUID's

                if (!ResolveUserUuid(scene, parcel.OwnerID))
                    parcel.OwnerID = m_rootScene.RegionInfo.EstateSettings.EstateOwner;

                if (!ResolveGroupUuid(parcel.GroupID))
                {
                    parcel.GroupID = UUID.Zero;
                    parcel.IsGroupOwned = false;
                }

                List<LandAccessEntry> accessList = new List<LandAccessEntry>();
                foreach (LandAccessEntry entry in parcel.ParcelAccessList)
                {
                    if (ResolveUserUuid(scene, entry.AgentID))
                        accessList.Add(entry);
                    // else, drop this access rule
                }
                parcel.ParcelAccessList = accessList;

//                m_log.DebugFormat(
//                    "[ARCHIVER]: Adding parcel {0}, local id {1}, area {2}", 
//                    parcel.Name, parcel.LocalID, parcel.Area);
                
                landData.Add(parcel);
            }

            if (!m_merge)
            {
                bool setupDefaultParcel = (landData.Count == 0);
                scene.LandChannel.Clear(setupDefaultParcel);
            }
            
            scene.EventManager.TriggerIncomingLandDataFromStorage(landData);
            m_log.InfoFormat("[ARCHIVER]: Restored {0} parcels.", landData.Count);
        }

        /// <summary>
        /// Look up the given user id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool ResolveUserUuid(Scene scene, UUID uuid)
        {
            if (!m_validUserUuids.ContainsKey(uuid))
            {
                UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, uuid);
                m_validUserUuids.Add(uuid, account != null);
            }

            return m_validUserUuids[uuid];
        }

        /// <summary>
        /// Look up the given group id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool ResolveGroupUuid(UUID uuid)
        {
            if (uuid == UUID.Zero)
                return true;    // this means the object has no group

            if (!m_validGroupUuids.ContainsKey(uuid))
            {
                bool exists;
                
                if (m_groupsModule == null)
                    exists = false;
                else
                    exists = (m_groupsModule.GetGroupRecord(uuid) != null);

                m_validGroupUuids.Add(uuid, exists);
            }

            return m_validGroupUuids[uuid];
        }

        /// Load an asset
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        /// <returns>true if asset was successfully loaded, false otherwise</returns>
        private bool LoadAsset(string assetPath, byte[] data)
        {
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);

            if (m_assetService.GetMetadata(uuid) != null)
            {
                // m_log.DebugFormat("[ARCHIVER]: found existing asset {0}",uuid);
                return true;
            }

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                if (assetType == (sbyte)AssetType.Unknown)
                    m_log.WarnFormat("[ARCHIVER]: Importing {0} byte asset {1} with unknown type", data.Length, uuid);

                //m_log.DebugFormat("[ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), String.Empty, assetType, UUID.Zero.ToString());
                asset.Data = data;

                // We're relying on the asset service to do the sensible thing and not store the asset if it already
                // exists.
                m_assetService.Store(asset);

                /**
                 * Create layers on decode for image assets.  This is likely to significantly increase the time to load archives so
                 * it might be best done when dearchive takes place on a separate thread
                if (asset.Type=AssetType.Texture)
                {
                    IJ2KDecoder cacheLayerDecode = scene.RequestModuleInterface<IJ2KDecoder>();
                    if (cacheLayerDecode != null)
                        cacheLayerDecode.syncdecode(asset.FullID, asset.Data);
                }
                */

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Tried to dearchive data with path {0} with an unknown type extension {1}",
                    assetPath, extension);

                return false;
            }
        }

        /// <summary>
        /// Load region settings data
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="settingsPath"></param>
        /// <param name="data"></param>
        /// <param name="dearchivedScenes"></param>
        /// <returns>
        /// true if settings were loaded successfully, false otherwise
        /// </returns>
        private bool LoadRegionSettings(Scene scene, string settingsPath, byte[] data, DearchiveScenesInfo dearchivedScenes)
        {
            RegionSettings loadedRegionSettings;

            try
            {
                loadedRegionSettings = RegionSettingsSerializer.Deserialize(data);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Could not parse region settings file {0}.  Ignoring.  Exception was {1}",
                    settingsPath, e);
                return false;
            }

            RegionSettings currentRegionSettings = scene.RegionInfo.RegionSettings;

            currentRegionSettings.AgentLimit = loadedRegionSettings.AgentLimit;
            currentRegionSettings.AllowDamage = loadedRegionSettings.AllowDamage;
            currentRegionSettings.AllowLandJoinDivide = loadedRegionSettings.AllowLandJoinDivide;
            currentRegionSettings.AllowLandResell = loadedRegionSettings.AllowLandResell;
            currentRegionSettings.BlockFly = loadedRegionSettings.BlockFly;
            currentRegionSettings.BlockShowInSearch = loadedRegionSettings.BlockShowInSearch;
            currentRegionSettings.BlockTerraform = loadedRegionSettings.BlockTerraform;
            currentRegionSettings.DisableCollisions = loadedRegionSettings.DisableCollisions;
            currentRegionSettings.DisablePhysics = loadedRegionSettings.DisablePhysics;
            currentRegionSettings.DisableScripts = loadedRegionSettings.DisableScripts;
            currentRegionSettings.Elevation1NE = loadedRegionSettings.Elevation1NE;
            currentRegionSettings.Elevation1NW = loadedRegionSettings.Elevation1NW;
            currentRegionSettings.Elevation1SE = loadedRegionSettings.Elevation1SE;
            currentRegionSettings.Elevation1SW = loadedRegionSettings.Elevation1SW;
            currentRegionSettings.Elevation2NE = loadedRegionSettings.Elevation2NE;
            currentRegionSettings.Elevation2NW = loadedRegionSettings.Elevation2NW;
            currentRegionSettings.Elevation2SE = loadedRegionSettings.Elevation2SE;
            currentRegionSettings.Elevation2SW = loadedRegionSettings.Elevation2SW;
            currentRegionSettings.FixedSun = loadedRegionSettings.FixedSun;
            currentRegionSettings.SunPosition = loadedRegionSettings.SunPosition;
            currentRegionSettings.ObjectBonus = loadedRegionSettings.ObjectBonus;
            currentRegionSettings.RestrictPushing = loadedRegionSettings.RestrictPushing;
            currentRegionSettings.TerrainLowerLimit = loadedRegionSettings.TerrainLowerLimit;
            currentRegionSettings.TerrainRaiseLimit = loadedRegionSettings.TerrainRaiseLimit;
            currentRegionSettings.TerrainTexture1 = loadedRegionSettings.TerrainTexture1;
            currentRegionSettings.TerrainTexture2 = loadedRegionSettings.TerrainTexture2;
            currentRegionSettings.TerrainTexture3 = loadedRegionSettings.TerrainTexture3;
            currentRegionSettings.TerrainTexture4 = loadedRegionSettings.TerrainTexture4;
            currentRegionSettings.UseEstateSun = loadedRegionSettings.UseEstateSun;
            currentRegionSettings.WaterHeight = loadedRegionSettings.WaterHeight;
            currentRegionSettings.TelehubObject = loadedRegionSettings.TelehubObject;
            currentRegionSettings.ClearSpawnPoints();
            foreach (SpawnPoint sp in loadedRegionSettings.SpawnPoints())
                currentRegionSettings.AddSpawnPoint(sp);

            currentRegionSettings.LoadedCreationDateTime = dearchivedScenes.LoadedCreationDateTime;
            currentRegionSettings.LoadedCreationID = dearchivedScenes.GetOriginalRegionID(scene.RegionInfo.RegionID).ToString();

            currentRegionSettings.Save();

            scene.TriggerEstateSunUpdate();
            
            IEstateModule estateModule = scene.RequestModuleInterface<IEstateModule>();
            if (estateModule != null)
                estateModule.sendRegionHandshakeToAll();

            return true;
        }

        /// <summary>
        /// Load terrain data
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="terrainPath"></param>
        /// <param name="data"></param>
        /// <returns>
        /// true if terrain was resolved successfully, false otherwise.
        /// </returns>
        private bool LoadTerrain(Scene scene, string terrainPath, byte[] data)
        {
            ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule>();

            MemoryStream ms = new MemoryStream(data);
            terrainModule.LoadFromStream(terrainPath, ms);
            ms.Close();

            m_log.DebugFormat("[ARCHIVER]: Restored terrain {0}", terrainPath);

            return true;
        }

        /// <summary>
        /// Load oar control file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="dearchivedScenes"></param>
        public DearchiveScenesInfo LoadControlFile(string path, byte[] data, DearchiveScenesInfo dearchivedScenes)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
            XmlParserContext context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);
            XmlTextReader xtr = new XmlTextReader(Encoding.ASCII.GetString(data), XmlNodeType.Document, context);

            // Loaded metadata will be empty if no information exists in the archive
            dearchivedScenes.LoadedCreationDateTime = 0;
            dearchivedScenes.DefaultOriginalID = "";

            bool multiRegion = false;

            while (xtr.Read()) 
            {
                if (xtr.NodeType == XmlNodeType.Element) 
                {
                    if (xtr.Name.ToString() == "archive")
                    {
                        int majorVersion = int.Parse(xtr["major_version"]);
                        int minorVersion = int.Parse(xtr["minor_version"]);
                        string version = string.Format("{0}.{1}", majorVersion, minorVersion);
                        
                        if (majorVersion > MAX_MAJOR_VERSION)
                        {
                            throw new Exception(
                                string.Format(
                                    "The OAR you are trying to load has major version number of {0} but this version of OpenSim can only load OARs with major version number {1} and below",
                                    majorVersion, MAX_MAJOR_VERSION));
                        }
                        
                        m_log.InfoFormat("[ARCHIVER]: Loading OAR with version {0}", version);
                    }
                    if (xtr.Name.ToString() == "datetime") 
                    {
                        int value;
                        if (Int32.TryParse(xtr.ReadElementContentAsString(), out value))
                            dearchivedScenes.LoadedCreationDateTime = value;
                    } 
                    else if (xtr.Name.ToString() == "row")
                    {
                        multiRegion = true;
                        dearchivedScenes.StartRow();
                    }
                    else if (xtr.Name.ToString() == "region")
                    {
                        dearchivedScenes.StartRegion();
                    }
                    else if (xtr.Name.ToString() == "id")
                    {
                        string id = xtr.ReadElementContentAsString();
                        dearchivedScenes.DefaultOriginalID = id;
                        if (multiRegion)
                            dearchivedScenes.SetRegionOriginalID(id);
                    }
                    else if (xtr.Name.ToString() == "dir")
                    {
                        dearchivedScenes.SetRegionDirectory(xtr.ReadElementContentAsString());
                    }
                }
            }

            dearchivedScenes.MultiRegionFormat = multiRegion;
            if (!multiRegion)
            {
                // Add the single scene
                dearchivedScenes.StartRow();
                dearchivedScenes.StartRegion();
                dearchivedScenes.SetRegionOriginalID(dearchivedScenes.DefaultOriginalID);
                dearchivedScenes.SetRegionDirectory("");
            }

            ControlFileLoaded = true;

            return dearchivedScenes;
        }
    }
}