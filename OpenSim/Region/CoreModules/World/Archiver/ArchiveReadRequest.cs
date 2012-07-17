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

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Handles an individual archive read request
    /// </summary>
    public class ArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// The maximum major version of OAR that we can read.  Minor versions shouldn't need a max number since version
        /// bumps here should be compatible.
        /// </summary>
        public static int MAX_MAJOR_VERSION = 1;
        
        /// <summary>
        /// Has the control file been loaded for this archive?
        /// </summary>
        public bool ControlFileLoaded { get; private set; }        

        protected Scene m_scene;
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
                    m_UserMan = m_scene.RequestModuleInterface<IUserManagement>();
                }
                return m_UserMan;
            }
        }

        public ArchiveReadRequest(Scene scene, string loadPath, bool merge, bool skipAssets, Guid requestId)
        {
            m_scene = scene;

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
        }

        public ArchiveReadRequest(Scene scene, Stream loadStream, bool merge, bool skipAssets, Guid requestId)
        {
            m_scene = scene;
            m_loadStream = loadStream;
            m_merge = merge;
            m_skipAssets = skipAssets;
            m_requestId = requestId;

            // Zero can never be a valid user id
            m_validUserUuids[UUID.Zero] = false;
        }

        /// <summary>
        /// Dearchive the region embodied in this request.
        /// </summary>
        public void DearchiveRegion()
        {
            // The same code can handle dearchiving 0.1 and 0.2 OpenSim Archive versions
            DearchiveRegion0DotStar();
        }

        private void DearchiveRegion0DotStar()
        {
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;
            List<string> serialisedSceneObjects = new List<string>();
            List<string> serialisedParcels = new List<string>();
            string filePath = "NONE";

            TarArchiveReader archive = new TarArchiveReader(m_loadStream);
            byte[] data;
            TarArchiveReader.TarEntryType entryType;
            
            try
            {
                while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
                {
                    //m_log.DebugFormat(
                    //    "[ARCHIVER]: Successfully read {0} ({1} bytes)", filePath, data.Length);
                    
                    if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                        continue;

                    if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                    {
                        serialisedSceneObjects.Add(Encoding.UTF8.GetString(data));
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
                        LoadTerrain(filePath, data);
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.SETTINGS_PATH))
                    {
                        LoadRegionSettings(filePath, data);
                    } 
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.LANDDATA_PATH))
                    {
                        serialisedParcels.Add(Encoding.UTF8.GetString(data));
                    } 
                    else if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                    {
                        LoadControlFile(filePath, data);
                    }
                }

                //m_log.Debug("[ARCHIVER]: Reached end of archive");
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Aborting load with error in archive file {0}.  {1}", filePath, e);
                m_errorMessage += e.ToString();
                m_scene.EventManager.TriggerOarFileLoaded(m_requestId, m_errorMessage);
                return;
            }
            finally
            {
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

            if (!m_merge)
            {
                m_log.Info("[ARCHIVER]: Clearing all existing scene objects");
                m_scene.DeleteAllSceneObjects();
            }

            LoadParcels(serialisedParcels);
            LoadObjects(serialisedSceneObjects);

            m_log.InfoFormat("[ARCHIVER]: Successfully loaded archive");

            m_scene.EventManager.TriggerOarFileLoaded(m_requestId, m_errorMessage);
        }
        
        /// <summary>
        /// Load serialized scene objects.
        /// </summary>
        /// <param name="serialisedSceneObjects"></param>
        protected void LoadObjects(List<string> serialisedSceneObjects)
        {
            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading {0} scene objects.  Please wait.", serialisedSceneObjects.Count);

            UUID oldTelehubUUID = m_scene.RegionInfo.RegionSettings.TelehubObject;

            IRegionSerialiserModule serialiser = m_scene.RequestModuleInterface<IRegionSerialiserModule>();
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

                bool isTelehub = (sceneObject.UUID == oldTelehubUUID);

                // For now, give all incoming scene objects new uuids.  This will allow scenes to be cloned
                // on the same region server and multiple examples a single object archive to be imported
                // to the same scene (when this is possible).
                sceneObject.ResetIDs();

                if (isTelehub)
                {
                    // Change the Telehub Object to the new UUID
                    m_scene.RegionInfo.RegionSettings.TelehubObject = sceneObject.UUID;
                    m_scene.RegionInfo.RegionSettings.Save();
                    oldTelehubUUID = UUID.Zero;
                }

                // Try to retain the original creator/owner/lastowner if their uuid is present on this grid
                // or creator data is present.  Otherwise, use the estate owner instead.
                foreach (SceneObjectPart part in sceneObject.Parts)
                {
                    if (part.CreatorData == null || part.CreatorData == string.Empty)
                    {
                        if (!ResolveUserUuid(part.CreatorID))
                            part.CreatorID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                    }
                    if (UserManager != null)
                        UserManager.AddUser(part.CreatorID, part.CreatorData);

                    if (!ResolveUserUuid(part.OwnerID))
                        part.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;

                    if (!ResolveUserUuid(part.LastOwnerID))
                        part.LastOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;

                    // And zap any troublesome sit target information
//                    part.SitTargetOrientation = new Quaternion(0, 0, 0, 1);
//                    part.SitTargetPosition    = new Vector3(0, 0, 0);

                    // Fix ownership/creator of inventory items
                    // Not doing so results in inventory items
                    // being no copy/no mod for everyone
                    lock (part.TaskInventory)
                    {
                        if (!ResolveUserUuid(part.CreatorID))
                            part.CreatorID = m_scene.RegionInfo.EstateSettings.EstateOwner;
    
                        if (!ResolveUserUuid(part.OwnerID))
                            part.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
    
                        if (!ResolveUserUuid(part.LastOwnerID))
                            part.LastOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
    
                        // And zap any troublesome sit target information
                        part.SitTargetOrientation = new Quaternion(0, 0, 0, 1);
                        part.SitTargetPosition    = new Vector3(0, 0, 0);
    
                        // Fix ownership/creator of inventory items
                        // Not doing so results in inventory items
                        // being no copy/no mod for everyone
                        part.TaskInventory.LockItemsForRead(true);
                        TaskInventoryDictionary inv = part.TaskInventory;
                        foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv)
                        {
                            if (!ResolveUserUuid(kvp.Value.OwnerID))
                            {
                                kvp.Value.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                            }
                            if (kvp.Value.CreatorData == null || kvp.Value.CreatorData == string.Empty)
                            {
                                if (!ResolveUserUuid(kvp.Value.CreatorID))
                                    kvp.Value.CreatorID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                            }
                            if (UserManager != null)
                                UserManager.AddUser(kvp.Value.CreatorID, kvp.Value.CreatorData);
                        }
                        part.TaskInventory.LockItemsForRead(false);
                    }
                }

                if (m_scene.AddRestoredSceneObject(sceneObject, true, false))
                {
                    sceneObjectsLoadedCount++;
                    sceneObject.CreateScriptInstances(0, false, m_scene.DefaultScriptEngine, 0);
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
                m_scene.RegionInfo.RegionSettings.TelehubObject = UUID.Zero;
                m_scene.RegionInfo.RegionSettings.ClearSpawnPoints();
            }
        }
        
        /// <summary>
        /// Load serialized parcels.
        /// </summary>
        /// <param name="serialisedParcels"></param>
        protected void LoadParcels(List<string> serialisedParcels)
        {
            // Reload serialized parcels
            m_log.InfoFormat("[ARCHIVER]: Loading {0} parcels.  Please wait.", serialisedParcels.Count);
            List<LandData> landData = new List<LandData>();
            foreach (string serialisedParcel in serialisedParcels)
            {
                LandData parcel = LandDataSerializer.Deserialize(serialisedParcel);
                if (!ResolveUserUuid(parcel.OwnerID))
                    parcel.OwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
                
//                m_log.DebugFormat(
//                    "[ARCHIVER]: Adding parcel {0}, local id {1}, area {2}", 
//                    parcel.Name, parcel.LocalID, parcel.Area);
                
                landData.Add(parcel);
            }

            if (!m_merge)
            {
                bool setupDefaultParcel = (landData.Count == 0);
                m_scene.LandChannel.Clear(setupDefaultParcel);
            }
            
            m_scene.EventManager.TriggerIncomingLandDataFromStorage(landData);
            m_log.InfoFormat("[ARCHIVER]: Restored {0} parcels.", landData.Count);
        }

        /// <summary>
        /// Look up the given user id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool ResolveUserUuid(UUID uuid)
        {
            if (!m_validUserUuids.ContainsKey(uuid))
            {
                UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, uuid);
                m_validUserUuids.Add(uuid, account != null);
            }

            return m_validUserUuids[uuid];
        }

        /// <summary>
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

            if (m_scene.AssetService.GetMetadata(uuid) != null)
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
                m_scene.AssetService.Store(asset);

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
        /// <param name="settingsPath"></param>
        /// <param name="data"></param>
        /// <returns>
        /// true if settings were loaded successfully, false otherwise
        /// </returns>
        private bool LoadRegionSettings(string settingsPath, byte[] data)
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

            RegionSettings currentRegionSettings = m_scene.RegionInfo.RegionSettings;

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

            currentRegionSettings.Save();

            m_scene.TriggerEstateSunUpdate();
            
            IEstateModule estateModule = m_scene.RequestModuleInterface<IEstateModule>();

            if (estateModule != null)
                estateModule.sendRegionHandshakeToAll();

            return true;
        }

        /// <summary>
        /// Load terrain data
        /// </summary>
        /// <param name="terrainPath"></param>
        /// <param name="data"></param>
        /// <returns>
        /// true if terrain was resolved successfully, false otherwise.
        /// </returns>
        private bool LoadTerrain(string terrainPath, byte[] data)
        {
            ITerrainModule terrainModule = m_scene.RequestModuleInterface<ITerrainModule>();

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
        public void LoadControlFile(string path, byte[] data)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
            XmlParserContext context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);
            XmlTextReader xtr = new XmlTextReader(Encoding.ASCII.GetString(data), XmlNodeType.Document, context);

            RegionSettings currentRegionSettings = m_scene.RegionInfo.RegionSettings;

            // Loaded metadata will empty if no information exists in the archive
            currentRegionSettings.LoadedCreationDateTime = 0;
            currentRegionSettings.LoadedCreationID = "";

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
                            currentRegionSettings.LoadedCreationDateTime = value;
                    } 
                    else if (xtr.Name.ToString() == "id") 
                    {
                        currentRegionSettings.LoadedCreationID = xtr.ReadElementContentAsString();
                    }
                }
            }
            
            currentRegionSettings.Save();
            
            ControlFileLoaded = true;
        }
    }
}