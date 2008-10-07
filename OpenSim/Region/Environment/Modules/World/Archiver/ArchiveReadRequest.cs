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

using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Framework.Communications.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Handles an individual archive read request
    /// </summary>
    public class ArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static System.Text.ASCIIEncoding m_asciiEncoding = new System.Text.ASCIIEncoding();

        private Scene m_scene;
        private string m_loadPath;

        /// <summary>
        /// Used to cache lookups for valid uuids.
        /// </summary>
        private IDictionary<UUID, bool> m_validUserUuids = new Dictionary<UUID, bool>();

        public ArchiveReadRequest(Scene scene, string loadPath)
        {
            m_scene = scene;
            m_loadPath = loadPath;

            DearchiveRegion();
        }

        private void DearchiveRegion()
        {
            TarArchiveReader archive
                = new TarArchiveReader(
                    new GZipStream(new FileStream(m_loadPath, FileMode.Open), CompressionMode.Decompress));
            //AssetsDearchiver dearchiver = new AssetsDearchiver(m_scene.AssetCache);

            List<string> serialisedSceneObjects = new List<string>();
            string filePath = "ERROR";

            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;

            byte[] data;
            while ((data = archive.ReadEntry(out filePath)) != null)
            {
                //m_log.DebugFormat(
                //    "[ARCHIVER]: Successfully read {0} ({1} bytes)}", filePath, data.Length);

                if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                {
                    serialisedSceneObjects.Add(m_asciiEncoding.GetString(data));
                }
//                else if (filePath.Equals(ArchiveConstants.ASSETS_METADATA_PATH))
//                {
//                    string xml = m_asciiEncoding.GetString(data);
//                    dearchiver.AddAssetMetadata(xml);
//                }
                else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    if (LoadAsset(filePath, data))
                        successfulAssetRestores++;
                    else
                        failedAssetRestores++;
                }
                else if (filePath.StartsWith(ArchiveConstants.TERRAINS_PATH))
                {
                    LoadTerrain(filePath, data);
                }
            }

            //m_log.Debug("[ARCHIVER]: Reached end of archive");

            archive.Close();

            m_log.InfoFormat("[ARCHIVER]: Restored {0} assets", successfulAssetRestores);

            if (failedAssetRestores > 0)
                m_log.ErrorFormat("[ARCHIVER]: Failed to load {0} assets", failedAssetRestores);

            m_log.Info("[ARCHIVER]: Clearing all existing scene objects");
            m_scene.DeleteAllSceneObjects();

            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading {0} scene objects.  Please wait.", serialisedSceneObjects.Count);

            IRegionSerialiser serialiser = m_scene.RequestModuleInterface<IRegionSerialiser>();
            ICollection<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();

            foreach (string serialisedSceneObject in serialisedSceneObjects)
            {
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
                    if (!resolveUserUuid(part.CreatorID))
                        part.CreatorID = masterAvatarId;

                    if (!resolveUserUuid(part.OwnerID))
                        part.OwnerID = masterAvatarId;

                    if (!resolveUserUuid(part.LastOwnerID))
                        part.LastOwnerID = masterAvatarId;

                    // And zap any troublesome sit target information
                    part.SitTargetOrientation = new Quaternion(0,0,0,1);
                    part.SitTargetPosition    = new Vector3(0,0,0);

		    // Fix ownership/creator of inventory items
		    // Not doing so results in inventory items
		    // being no copy/no mod for everyone
		    TaskInventoryDictionary inv = part.TaskInventory;
		    foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv) {
			if (!resolveUserUuid(kvp.Value.OwnerID)) {
			    kvp.Value.OwnerID = masterAvatarId;
			}
			if (!resolveUserUuid(kvp.Value.CreatorID)) {
			    kvp.Value.CreatorID = masterAvatarId;
			}
		    }
                }

                if (m_scene.AddRestoredSceneObject(sceneObject, true, false))
                {
                    sceneObjects.Add(sceneObject);
                }
            }

            m_log.InfoFormat("[ARCHIVER]: Restored {0} scene objects to the scene", sceneObjects.Count);

            int ignoredObjects = serialisedSceneObjects.Count - sceneObjects.Count;

            if (ignoredObjects > 0)
                m_log.WarnFormat("[ARCHIVER]: Ignored {0} scene objects that already existed in the scene", ignoredObjects);

            m_log.InfoFormat("[ARCHIVER]: Successfully loaded archive");

            m_log.Debug("[ARCHIVER]: Starting scripts");

            foreach (SceneObjectGroup sceneObject in sceneObjects)
            {
                sceneObject.CreateScriptInstances(0, true, m_scene.DefaultScriptEngine);
            }
        }

        /// <summary>
        /// Look up the given user id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool resolveUserUuid(UUID uuid)
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

                m_scene.AssetCache.AddAsset(asset);

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
    }
}
