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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Handles an individual archive read request
    /// </summary>
    public class ArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static ASCIIEncoding m_asciiEncoding = new ASCIIEncoding();

        private Scene m_scene;
        private Stream m_loadStream;
        private Guid m_requestId;
        private string m_errorMessage;

        /// <value>
        /// Should the archive being loaded be merged with what is already on the region?
        /// </value>
        private bool m_merge;

        /// <summary>
        /// Used to cache lookups for valid uuids.
        /// </summary>
        private IDictionary<UUID, bool> m_validUserUuids = new Dictionary<UUID, bool>();

        public ArchiveReadRequest(Scene scene, string loadPath, bool merge, Guid requestId)
        {
            m_scene = scene;
            m_loadStream = new GZipStream(GetStream(loadPath), CompressionMode.Decompress);
            m_errorMessage = String.Empty;
            m_merge = merge;
            m_requestId = requestId;
        }

        public ArchiveReadRequest(Scene scene, Stream loadStream, bool merge, Guid requestId)
        {
            m_scene = scene;
            m_loadStream = loadStream;
            m_merge = merge;
            m_requestId = requestId;
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
            string filePath = "NONE";
            
            try
            {
                TarArchiveReader archive = new TarArchiveReader(m_loadStream);
               
                byte[] data;
                TarArchiveReader.TarEntryType entryType;

                while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
                {
                    //m_log.DebugFormat(
                    //    "[ARCHIVER]: Successfully read {0} ({1} bytes)", filePath, data.Length);
                    
                    if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                        continue;

                    if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                    {
                        serialisedSceneObjects.Add(m_asciiEncoding.GetString(data));
                    }
                    else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                    {
                        if (LoadAsset(filePath, data))
                            successfulAssetRestores++;
                        else
                            failedAssetRestores++;
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.TERRAINS_PATH))
                    {
                        LoadTerrain(filePath, data);
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.SETTINGS_PATH))
                    {
                        LoadRegionSettings(filePath, data);
                    } 
                    else if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                    {
                        LoadControlFile(filePath, data);
                    }
                }

                //m_log.Debug("[ARCHIVER]: Reached end of archive");

                archive.Close();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Aborting load with error in archive file {0}.  {1}", filePath, e);
                m_errorMessage += e.ToString();
                m_scene.EventManager.TriggerOarFileLoaded(m_requestId, m_errorMessage);
                return;
            }

            m_log.InfoFormat("[ARCHIVER]: Restored {0} assets", successfulAssetRestores);

            if (failedAssetRestores > 0)
            {
                m_log.ErrorFormat("[ARCHIVER]: Failed to load {0} assets", failedAssetRestores);
                m_errorMessage += String.Format("Failed to load {0} assets", failedAssetRestores);
            }

            if (!m_merge)
            {
                m_log.Info("[ARCHIVER]: Clearing all existing scene objects");
                m_scene.DeleteAllSceneObjects();
            }

            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading {0} scene objects.  Please wait.", serialisedSceneObjects.Count);

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

                // For now, give all incoming scene objects new uuids.  This will allow scenes to be cloned
                // on the same region server and multiple examples a single object archive to be imported
                // to the same scene (when this is possible).
                sceneObject.ResetIDs();

                // Try to retain the original creator/owner/lastowner if their uuid is present on this grid
                // otherwise, use the master avatar uuid instead
                UUID masterAvatarId = m_scene.RegionInfo.MasterAvatarAssignedUUID;

                if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                    masterAvatarId = m_scene.RegionInfo.EstateSettings.EstateOwner;

                foreach (SceneObjectPart part in sceneObject.Children.Values)
                {
                    if (!ResolveUserUuid(part.CreatorID))
                        part.CreatorID = masterAvatarId;

                    if (!ResolveUserUuid(part.OwnerID))
                        part.OwnerID = masterAvatarId;

                    if (!ResolveUserUuid(part.LastOwnerID))
                        part.LastOwnerID = masterAvatarId;

                    // And zap any troublesome sit target information
                    part.SitTargetOrientation = new Quaternion(0, 0, 0, 1);
                    part.SitTargetPosition    = new Vector3(0, 0, 0);

                    // Fix ownership/creator of inventory items
                    // Not doing so results in inventory items
                    // being no copy/no mod for everyone
                    lock (part.TaskInventory)
                    {
                        TaskInventoryDictionary inv = part.TaskInventory;
                        foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv)
                        {
                            if (!ResolveUserUuid(kvp.Value.OwnerID))
                            {
                                kvp.Value.OwnerID = masterAvatarId;
                            }
                            if (!ResolveUserUuid(kvp.Value.CreatorID))
                            {
                                kvp.Value.CreatorID = masterAvatarId;
                            }
                        }
                    }
                }

                if (m_scene.AddRestoredSceneObject(sceneObject, true, false))
                {
                    sceneObjectsLoadedCount++;
                    sceneObject.CreateScriptInstances(0, false, m_scene.DefaultScriptEngine, 0);
                }
            }

            m_log.InfoFormat("[ARCHIVER]: Restored {0} scene objects to the scene", sceneObjectsLoadedCount);

            int ignoredObjects = serialisedSceneObjects.Count - sceneObjectsLoadedCount;

            if (ignoredObjects > 0)
                m_log.WarnFormat("[ARCHIVER]: Ignored {0} scene objects that already existed in the scene", ignoredObjects);

            m_log.InfoFormat("[ARCHIVER]: Successfully loaded archive");

            m_scene.EventManager.TriggerOarFileLoaded(m_requestId, m_errorMessage);
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
                CachedUserInfo profile = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(uuid);
                if (profile != null && profile.UserProfile != null)
                    m_validUserUuids.Add(uuid, true);
                else
                    m_validUserUuids.Add(uuid, false);
            }

            if (m_validUserUuids[uuid])
                return true;
            else
                return false;
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

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                //m_log.DebugFormat("[ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), String.Empty);
                asset.Type = assetType;
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

            IEstateModule estateModule = m_scene.RequestModuleInterface<IEstateModule>();
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
        /// Resolve path to a working FileStream
        /// </summary>
        private Stream GetStream(string path)
        {
            if (File.Exists(path))
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            }
            else
            {
                try
                {
                    Uri uri = new Uri(path);
                    if (uri.Scheme == "file")
                    {
                        return new FileStream(uri.AbsolutePath, FileMode.Open, FileAccess.Read);
                    }
                    else
                    {
                        if (uri.Scheme != "http")
                            throw new Exception(String.Format("Unsupported URI scheme ({0})", path));

                        // OK, now we know we have an HTTP URI to work with

                        return URIFetch(uri);
                    }
                }
                catch (UriFormatException)
                {
                    // In many cases the user will put in a plain old filename that cannot be found so assume that
                    // this is the problem rather than confusing the issue with a UriFormatException
                    throw new Exception(String.Format("Cannot find file {0}", path));
                }
            }
        }

        private static Stream URIFetch(Uri uri)
        {
            HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(uri);

            // request.Credentials = credentials;

            request.ContentLength = 0;
            request.KeepAlive     = false;

            WebResponse response = request.GetResponse();
            Stream file = response.GetResponseStream();

            if (response.ContentType != "application/x-oar")
                throw new Exception(String.Format("{0} does not identify an OAR file", uri.ToString()));

            if (response.ContentLength == 0)
                throw new Exception(String.Format("{0} returned an empty file", uri.ToString()));

            // return new BufferedStream(file, (int) response.ContentLength);
            return new BufferedStream(file, 1000000);
        }

        /// <summary>
        /// Load oar control file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        private void LoadControlFile(string path, byte[] data)
        {
            //Create the XmlNamespaceManager.
            NameTable nt = new NameTable();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nt);

            // Create the XmlParserContext.
            XmlParserContext context = new XmlParserContext(null, nsmgr, null, XmlSpace.None);

            XmlTextReader xtr 
                = new XmlTextReader(m_asciiEncoding.GetString(data), XmlNodeType.Document, context);

            RegionSettings currentRegionSettings = m_scene.RegionInfo.RegionSettings;

            // Loaded metadata will empty if no information exists in the archive
            currentRegionSettings.LoadedCreationDateTime = 0;
            currentRegionSettings.LoadedCreationID = "";

            while (xtr.Read()) 
            {
                if (xtr.NodeType == XmlNodeType.Element) 
                {
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
        }
    }
}
