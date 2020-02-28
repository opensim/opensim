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
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.CoreModules.World.Land;
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
        /// Merging usually suppresses terrain and parcel loading
        /// </value>
        protected bool m_merge;
        protected bool m_mergeReplaceObjects;

        /// <value>
        /// If true, force the loading of terrain from the oar file
        /// </value>
        protected bool m_mergeTerrain;

        /// <value>
        /// If true, force the merge of parcels from the oar file
        /// </value>
        protected bool m_mergeParcels;

        /// <value>
        /// Should we ignore any assets when reloading the archive?
        /// </value>
        protected bool m_skipAssets;

        /// <value>
        /// Displacement added to each object as it is added to the world
        /// </value>
        protected Vector3 m_displacement = Vector3.Zero;

        /// <value>
        /// Rotation (in radians) to apply to the objects as they are loaded.
        /// </value>
        protected float m_rotation = 0f;

       /// <value>
        /// original oar region size. not using Constants.RegionSize
        /// </value>
        protected Vector3 m_incomingRegionSize = new Vector3(256f, 256f, float.MaxValue);

        /// <value>
        /// Center around which to apply the rotation relative to the original oar position
        /// </value>
        protected Vector3 m_rotationCenter = new Vector3(128f, 128f, 0f);

        /// <value>
        /// Corner 1 of a bounding cuboid which specifies which objects we load from the oar
        /// </value>
        protected Vector3 m_boundingOrigin = Vector3.Zero;

        /// <value>
        /// Size of a bounding cuboid which specifies which objects we load from the oar
        /// </value>
        protected Vector3 m_boundingSize = new Vector3(Constants.MaximumRegionSize, Constants.MaximumRegionSize, float.MaxValue);

        protected bool m_noObjects = false;
        protected bool m_boundingBox = false;
        protected bool m_debug = false;

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

        private UUID m_defaultUser;

        public ArchiveReadRequest(Scene scene, string loadPath, Guid requestId, Dictionary<string, object> options)
        {
            m_rootScene = scene;

            if (options.ContainsKey("default-user"))
            {
                m_defaultUser = (UUID)options["default-user"];
                m_log.InfoFormat("Using User {0} as default user", m_defaultUser.ToString());
            }
            else
            {
                m_defaultUser = scene.RegionInfo.EstateSettings.EstateOwner;
            }

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

            m_merge = options.ContainsKey("merge");
            m_mergeReplaceObjects = options.ContainsKey("mReplaceObjects");
            m_mergeTerrain = options.ContainsKey("merge-terrain");
            m_mergeParcels = options.ContainsKey("merge-parcels");
            m_noObjects = options.ContainsKey("no-objects");
            m_skipAssets = options.ContainsKey("skipAssets");
            m_requestId = requestId;
            m_displacement = options.ContainsKey("displacement") ? (Vector3)options["displacement"] : Vector3.Zero;
            m_rotation = options.ContainsKey("rotation") ? (float)options["rotation"] : 0f;

            m_boundingOrigin = Vector3.Zero;
            m_boundingSize = new Vector3(scene.RegionInfo.RegionSizeX, scene.RegionInfo.RegionSizeY, float.MaxValue);

            if (options.ContainsKey("bounding-origin"))
            {
                Vector3 boOption = (Vector3)options["bounding-origin"];
                if (boOption != m_boundingOrigin)
                {
                    m_boundingOrigin = boOption;
                }
                m_boundingBox = true;
            }

            if (options.ContainsKey("bounding-size"))
            {
                Vector3 bsOption = (Vector3)options["bounding-size"];
                bool clip = false;
                if (bsOption.X <= 0 || bsOption.X > m_boundingSize.X)
                {
                    bsOption.X = m_boundingSize.X;
                    clip = true;
                }
                if (bsOption.Y <= 0 || bsOption.Y > m_boundingSize.Y)
                {
                    bsOption.Y = m_boundingSize.Y;
                    clip = true;
                }
                if (bsOption != m_boundingSize)
                {
                    m_boundingSize = bsOption;
                    m_boundingBox = true;
                }
                if (clip) m_log.InfoFormat("[ARCHIVER]: The bounding cube specified is larger than the destination region! Clipping to {0}.", m_boundingSize.ToString());
            }

            m_debug = options.ContainsKey("debug");

            // Zero can never be a valid user id (or group)
            m_validUserUuids[UUID.Zero] = false;
            m_validGroupUuids[UUID.Zero] = false;

            m_groupsModule = m_rootScene.RequestModuleInterface<IGroupsModule>();
            m_assetService = m_rootScene.AssetService;
        }

        public ArchiveReadRequest(Scene scene, Stream loadStream, Guid requestId, Dictionary<string, object> options)
        {
            m_rootScene = scene;
            m_loadPath = null;
            m_loadStream = loadStream;
            m_skipAssets = options.ContainsKey("skipAssets");
            m_merge = options.ContainsKey("merge");
            m_mergeReplaceObjects = options.ContainsKey("mReplaceObjects");
            m_requestId = requestId;

            m_defaultUser = scene.RegionInfo.EstateSettings.EstateOwner;

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
            DearchiveRegion(true);
        }

        public void DearchiveRegion(bool shouldStartScripts)
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

                    if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH) && !m_noObjects)
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
                    else if (filePath.StartsWith(ArchiveConstants.TERRAINS_PATH) && (!m_merge || m_mergeTerrain))
                    {
                        LoadTerrain(scene, filePath, data);
                    }
                    else if (!m_merge && filePath.StartsWith(ArchiveConstants.SETTINGS_PATH))
                    {
                        LoadRegionSettings(scene, filePath, data, dearchivedScenes);
                    }
                    else if (filePath.StartsWith(ArchiveConstants.LANDDATA_PATH) && (!m_merge || m_mergeParcels))
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
            if (shouldStartScripts)
            {
                WorkManager.RunInThread(o =>
                {
                    Thread.Sleep(15000);
                    m_log.Info("[ARCHIVER]: Starting scripts in scene objects...");

                    foreach (DearchiveContext sceneContext in sceneContexts.Values)
                    {
                        foreach (SceneObjectGroup sceneObject in sceneContext.SceneObjects)
                        {
                            sceneObject.CreateScriptInstances(0, false, sceneContext.Scene.DefaultScriptEngine, 0); // StateSource.RegionStart
                            sceneObject.ResumeScripts();
                        }

                        sceneContext.SceneObjects.Clear();
                    }
                    m_log.Info("[ARCHIVER]: Start scripts done");
                }, null, string.Format("ReadArchiveStartScripts (request {0})", m_requestId));
            }

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
                        m_log.Warn("[ARCHIVER]: Control file wasn't the first file in the archive");
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
                            throw new Exception("[ARCHIVER]: Error reading archive: control file wasn't the first file, and the input stream doesn't allow seeking");
                        }
                    }

                    return;
                }

                firstFile = false;
            }

            throw new Exception("[ARCHIVER]: Control file not found");
        }

        /// <summary>
        /// Load serialized scene objects.
        /// </summary>
        protected void LoadObjects(Scene scene, List<string> serialisedSceneObjects, List<SceneObjectGroup> sceneObjects)
        {
            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading {0} scene objects.  Please wait.", serialisedSceneObjects.Count);

            // Convert rotation to radians
            double rotation = Math.PI * m_rotation / 180f;

            OpenMetaverse.Quaternion rot = OpenMetaverse.Quaternion.CreateFromAxisAngle(0, 0, 1, (float)rotation);

            UUID oldTelehubUUID = scene.RegionInfo.RegionSettings.TelehubObject;

            IRegionSerialiserModule serialiser = scene.RequestModuleInterface<IRegionSerialiserModule>();
            int sceneObjectsLoadedCount = 0;
            Vector3 boundingExtent = new Vector3(m_boundingOrigin.X + m_boundingSize.X, m_boundingOrigin.Y + m_boundingSize.Y, m_boundingOrigin.Z + m_boundingSize.Z);

            int mergeskip = 0;
            foreach (string serialisedSceneObject in serialisedSceneObjects)
            {
                SceneObjectGroup sceneObject = serialiser.DeserializeGroupFromXml2(serialisedSceneObject);
                if (m_merge)
                {
                    if(scene.TryGetSceneObjectGroup(sceneObject.UUID, out SceneObjectGroup oldSog))
                    {
                        ++mergeskip;
                        if (m_mergeReplaceObjects)
                            scene.DeleteSceneObject(oldSog, false);
                        else
                            continue;
                    }
                }

                Vector3 pos = sceneObject.AbsolutePosition;
                if (m_debug)
                    m_log.DebugFormat("[ARCHIVER]: Loading object from OAR with original scene position {0}.", pos.ToString());

                // Happily this does not do much to the object since it hasn't been added to the scene yet
                if (!sceneObject.IsAttachment)
                {
                    if (m_rotation != 0f)
                    {
                        //fix the rotation center to the middle of the incoming region now as it's otherwise hopelessly confusing on varRegions
                        //as it only works with objects and terrain (using old Merge method) and not parcels
                        m_rotationCenter.X = m_incomingRegionSize.X / 2;
                        m_rotationCenter.Y = m_incomingRegionSize.Y / 2;

                        // Rotate the object
                        sceneObject.RootPart.RotationOffset = rot * sceneObject.GroupRotation;
                        // Get object position relative to rotation axis
                        Vector3 offset = pos - m_rotationCenter;
                        // Rotate the object position
                        offset *= rot;
                        // Restore the object position back to relative to the region
                        pos = m_rotationCenter + offset;
                        if (m_debug) m_log.DebugFormat("[ARCHIVER]: After rotation, object from OAR is at scene position {0}.", pos.ToString());
                    }
                    if (m_boundingBox)
                    {
                        if (pos.X < m_boundingOrigin.X || pos.X >= boundingExtent.X
                            || pos.Y < m_boundingOrigin.Y || pos.Y >= boundingExtent.Y
                            || pos.Z < m_boundingOrigin.Z || pos.Z >= boundingExtent.Z)
                        {
                            if (m_debug) m_log.DebugFormat("[ARCHIVER]: Skipping object from OAR in scene because it's position {0} is outside of bounding cube.", pos.ToString());
                            continue;
                        }
                        //adjust object position to be relative to <0,0> so we can apply the displacement
                        pos.X -= m_boundingOrigin.X;
                        pos.Y -= m_boundingOrigin.Y;
                    }
                    if (m_displacement != Vector3.Zero)
                    {
                        pos += m_displacement;
                        if (m_debug) m_log.DebugFormat("[ARCHIVER]: After displacement, object from OAR is at scene position {0}.", pos.ToString());
                    }
                    sceneObject.AbsolutePosition = pos;
                }
                if (m_debug)
                    m_log.DebugFormat("[ARCHIVER]: Placing object from OAR in scene at position {0}.  ", pos.ToString());

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

                ModifySceneObject(scene, sceneObject);

                if (scene.AddRestoredSceneObject(sceneObject, true, false))
                {
                    sceneObjectsLoadedCount++;
                    sceneObject.CreateScriptInstances(0, false, scene.DefaultScriptEngine, 0);
                    sceneObject.ResumeScripts();
                }
            }

            m_log.InfoFormat("[ARCHIVER]: Loaded {0} scene objects to the scene", sceneObjectsLoadedCount);
            int ignoredObjects = serialisedSceneObjects.Count - sceneObjectsLoadedCount - mergeskip;

            if(mergeskip > 0)
            {
                if(m_mergeReplaceObjects)
                    m_log.InfoFormat("[ARCHIVER]:     Replaced {0} scene objects", mergeskip);
                else
                    m_log.InfoFormat("[ARCHIVER]:     Skipped {0} scene objects that already existed in the scene", mergeskip);
            }
            if (ignoredObjects > 0)
                m_log.WarnFormat("[ARCHIVER]:     Ignored {0} possible out of bounds", ignoredObjects);

            if (oldTelehubUUID != UUID.Zero)
            {
                m_log.WarnFormat("[ARCHIVER]: Telehub object not found: {0}", oldTelehubUUID);
                scene.RegionInfo.RegionSettings.TelehubObject = UUID.Zero;
                scene.RegionInfo.RegionSettings.ClearSpawnPoints();
            }
        }

        /// <summary>
        /// Optionally modify a loaded SceneObjectGroup. Currently this just ensures that the
        /// User IDs and Group IDs are valid, but other manipulations could be done as well.
        /// </summary>
        private void ModifySceneObject(Scene scene, SceneObjectGroup sceneObject)
        {
            // Try to retain the original creator/owner/lastowner if their uuid is present on this grid
            // or creator data is present.  Otherwise, use the estate owner instead.
            foreach (SceneObjectPart part in sceneObject.Parts)
            {
                if (string.IsNullOrEmpty(part.CreatorData))
                {
                    if (!ResolveUserUuid(scene, part.CreatorID))
                        part.CreatorID = m_defaultUser;
                }
                if (UserManager != null)
                    UserManager.AddUser(part.CreatorID, part.CreatorData);

                if (!(ResolveUserUuid(scene, part.OwnerID) || ResolveGroupUuid(part.OwnerID)))
                    part.OwnerID = m_defaultUser;

                if (!(ResolveUserUuid(scene, part.LastOwnerID) || ResolveGroupUuid(part.LastOwnerID)))
                    part.LastOwnerID = m_defaultUser;

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
/* avination code disabled for opensim
                    // And zap any troublesome sit target information
                    part.SitTargetOrientation = new Quaternion(0, 0, 0, 1);
                    part.SitTargetPosition = new Vector3(0, 0, 0);
*/
                    // Fix ownership/creator of inventory items
                    // Not doing so results in inventory items
                    // being no copy/no mod for everyone
                    part.TaskInventory.LockItemsForRead(true);

                    TaskInventoryDictionary inv = part.TaskInventory;
                    foreach (KeyValuePair<UUID, TaskInventoryItem> kvp in inv)
                    {
                        if (!(ResolveUserUuid(scene, kvp.Value.OwnerID) || ResolveGroupUuid(kvp.Value.OwnerID)))
                        {
                            kvp.Value.OwnerID = m_defaultUser;
                        }

                        if (string.IsNullOrEmpty(kvp.Value.CreatorData))
                        {
                            if (!ResolveUserUuid(scene, kvp.Value.CreatorID))
                                kvp.Value.CreatorID = m_defaultUser;
                        }

                        if (UserManager != null)
                            UserManager.AddUser(kvp.Value.CreatorID, kvp.Value.CreatorData);

                        if (!ResolveGroupUuid(kvp.Value.GroupID))
                            kvp.Value.GroupID = UUID.Zero;
                    }
                    part.TaskInventory.LockItemsForRead(false);

                }
            }
        }

        /// <summary>
        /// Load serialized parcels.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="serialisedParcels"></param>
        protected void LoadParcels(Scene scene, List<string> serialisedParcels)
        {
            if(serialisedParcels.Count == 0)
            {
                m_log.Info("[ARCHIVER]: No parcels to load, or skiping load");
                return;
            }

            // Reload serialized parcels
            m_log.InfoFormat("[ARCHIVER]: Loading {0} parcels.  Please wait.", serialisedParcels.Count);
            List<LandData> landData = new List<LandData>();
            ILandObject landObject = scene.RequestModuleInterface<ILandObject>();
            List<ILandObject> parcels;
            Vector3 parcelDisp = new Vector3(m_displacement.X, m_displacement.Y, 0f);
            Vector2 displacement = new Vector2(m_displacement.X, m_displacement.Y);
            Vector2 boundingOrigin = new Vector2(m_boundingOrigin.X, m_boundingOrigin.Y);
            Vector2 boundingSize = new Vector2(m_boundingSize.X, m_boundingSize.Y);
            Vector2 regionSize = new Vector2(scene.RegionInfo.RegionSizeX, scene.RegionInfo.RegionSizeY);

            // Gather any existing parcels before we add any more. Later as we add parcels we can check if the new parcel
            // data overlays any of the old data, and we can modify and remove (if empty) the old parcel so that there's no conflict
            bool domerge = m_merge & m_mergeParcels;

            parcels = scene.LandChannel.AllParcels();

            foreach (string serialisedParcel in serialisedParcels)
            {
                LandData parcel = LandDataSerializer.Deserialize(serialisedParcel);
                bool overrideRegionSize = true;  //use the src land parcel data size not the dst region size
                bool isEmptyNow;
                Vector3 AABBMin;
                Vector3 AABBMax;

                // create a new LandObject that we can use to manipulate the incoming source parcel data
                // this is ok, but just beware that some of the LandObject functions (that we haven't used here) still
                // assume we're always using the destination region size
                LandData ld = new LandData();
                landObject = new LandObject(ld, scene);
                landObject.LandData = parcel;

                bool[,] srcLandBitmap = landObject.ConvertBytesToLandBitmap(overrideRegionSize);
                if (landObject.IsLandBitmapEmpty(srcLandBitmap))
                {
                    m_log.InfoFormat("[ARCHIVER]: Skipping source parcel {0} with GlobalID: {1} LocalID: {2} that has no claimed land.",
                        parcel.Name, parcel.GlobalID, parcel.LocalID);
                    continue;
                }
                //m_log.DebugFormat("[ARCHIVER]: Showing claimed land for source parcel: {0} with GlobalID: {1} LocalID: {2}.",
                //   parcel.Name, parcel.GlobalID, parcel.LocalID);
                //landObject.DebugLandBitmap(srcLandBitmap);

                bool[,] dstLandBitmap = landObject.RemapLandBitmap(srcLandBitmap, displacement, m_rotation, boundingOrigin, boundingSize, regionSize, out isEmptyNow, out AABBMin, out AABBMax);
                if (isEmptyNow)
                {
                    m_log.WarnFormat("[ARCHIVER]: Not adding destination parcel {0} with GlobalID: {1} LocalID: {2} because, after applying rotation, bounding and displacement, it has no claimed land.",
                        parcel.Name, parcel.GlobalID, parcel.LocalID);
                    continue;
                }
                //m_log.DebugFormat("[ARCHIVER]: Showing claimed land for destination parcel: {0} with GlobalID: {1} LocalID: {2} after applying rotation, bounding and displacement.",
                //    parcel.Name, parcel.GlobalID, parcel.LocalID);
                //landObject.DebugLandBitmap(dstLandBitmap);

                landObject.LandBitmap = dstLandBitmap;
                parcel.Bitmap = landObject.ConvertLandBitmapToBytes();
                parcel.AABBMin = AABBMin;
                parcel.AABBMax = AABBMax;

                if (domerge)
                {
                    // give the remapped parcel a new GlobalID, in case we're using the same OAR twice and a bounding cube, displacement and --merge
                    parcel.GlobalID = UUID.Random();

                    //now check if the area of this new incoming parcel overlays an area in any existing parcels
                    //and if so modify or lose the existing parcels
                    for (int i = 0; i < parcels.Count; i++)
                    {
                        if (parcels[i] != null)
                        {
                            bool[,] modLandBitmap = parcels[i].ConvertBytesToLandBitmap(overrideRegionSize);
                            modLandBitmap = parcels[i].RemoveFromLandBitmap(modLandBitmap, dstLandBitmap, out isEmptyNow, out AABBMin, out AABBMax);
                            if (isEmptyNow)
                            {
                                parcels[i] = null;
                            }
                            else
                            {
                                parcels[i].LandBitmap = modLandBitmap;
                                parcels[i].LandData.Bitmap = parcels[i].ConvertLandBitmapToBytes();
                                parcels[i].LandData.AABBMin = AABBMin;
                                parcels[i].LandData.AABBMax = AABBMax;
                            }
                        }
                    }
                }

                // Validate User and Group UUID's

                if (!ResolveGroupUuid(parcel.GroupID))
                    parcel.GroupID = UUID.Zero;

                if (parcel.IsGroupOwned)
                {
                    if (parcel.GroupID != UUID.Zero)
                    {
                        // In group-owned parcels, OwnerID=GroupID. This should already be the case, but let's make sure.
                        parcel.OwnerID = parcel.GroupID;
                    }
                    else
                    {
                        parcel.OwnerID = m_rootScene.RegionInfo.EstateSettings.EstateOwner;
                        parcel.IsGroupOwned = false;
                    }
                }
                else
                {
                    if (!ResolveUserUuid(scene, parcel.OwnerID))
                        parcel.OwnerID = m_rootScene.RegionInfo.EstateSettings.EstateOwner;
                }

                List<LandAccessEntry> accessList = new List<LandAccessEntry>();
                foreach (LandAccessEntry entry in parcel.ParcelAccessList)
                {
                    if (ResolveUserUuid(scene, entry.AgentID))
                        accessList.Add(entry);
                    // else, drop this access rule
                }
                parcel.ParcelAccessList = accessList;

                if (m_debug) m_log.DebugFormat("[ARCHIVER]: Adding parcel {0}, local id {1}, owner {2}, group {3}, isGroupOwned {4}, area {5}",
                                                    parcel.Name, parcel.LocalID, parcel.OwnerID, parcel.GroupID, parcel.IsGroupOwned, parcel.Area);

                landData.Add(parcel);
            }

            m_log.InfoFormat("[ARCHIVER]: Clearing {0} parcels.", parcels.Count);
            bool setupDefaultParcel = (landData.Count == 0);
            scene.LandChannel.Clear(setupDefaultParcel);

            if (domerge)
            {
                int j = 0;
                for (int i = 0; i < parcels.Count; i++) //if merging then we need to also add back in any existing parcels
                {
                    if (parcels[i] != null)
                    {
                        landData.Add(parcels[i].LandData);
                        j++;
                    }
                }
                m_log.InfoFormat("[ARCHIVER]: Keeping {0} old parcels.", j);
            }

            scene.EventManager.TriggerIncomingLandDataFromStorage(landData);
            m_log.InfoFormat("[ARCHIVER]: Added {0} total parcels.", landData.Count);
        }

        /// <summary>
        /// Look up the given user id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool ResolveUserUuid(Scene scene, UUID uuid)
        {
            lock (m_validUserUuids)
            {
                if (!m_validUserUuids.ContainsKey(uuid))
                {
                    // Note: we call GetUserAccount() inside the lock because this UserID is likely
                    // to occur many times, and we only want to query the users service once.
                    UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, uuid);
                    m_validUserUuids.Add(uuid, account != null);
                }

                return m_validUserUuids[uuid];
            }
        }

        /// <summary>
        /// Look up the given group id to check whether it's one that is valid for this grid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        private bool ResolveGroupUuid(UUID uuid)
        {
            lock (m_validGroupUuids)
            {
                if (!m_validGroupUuids.ContainsKey(uuid))
                {
                    bool exists;
                    if (m_groupsModule == null)
                    {
                        exists = false;
                    }
                    else
                    {
                        // Note: we call GetGroupRecord() inside the lock because this GroupID is likely
                        // to occur many times, and we only want to query the groups service once.
                        exists = (m_groupsModule.GetGroupRecord(uuid) != null);
                    }
                    m_validGroupUuids.Add(uuid, exists);
                }

                return m_validGroupUuids[uuid];
            }
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
                sbyte asype = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                // m_log.DebugFormat("[ARCHIVER]: found existing asset {0}",uuid);
                return true;
            }

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                if (assetType == (sbyte)AssetType.Unknown)
                {
                    m_log.WarnFormat("[ARCHIVER]: Importing {0} byte asset {1} with unknown type", data.Length, uuid);
                }
                else if (assetType == (sbyte)AssetType.Object)
                {
                    data = SceneObjectSerializer.ModifySerializedObject(UUID.Parse(uuid), data,
                        sog =>
                        {
                            ModifySceneObject(m_rootScene, sog);
                            return true;
                        });

                    if (data == null)
                        return false;
                }

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
            using (MemoryStream ms = new MemoryStream(data))
            {
                if (m_displacement != Vector3.Zero || m_rotation != 0f || m_boundingBox)
                {
                    Vector2 boundingOrigin = new Vector2(m_boundingOrigin.X, m_boundingOrigin.Y);
                    Vector2 boundingSize = new Vector2(m_boundingSize.X, m_boundingSize.Y);
                    terrainModule.LoadFromStream(terrainPath, m_displacement, m_rotation, boundingOrigin, boundingSize, ms); ;
                }
                else
                {
                    terrainModule.LoadFromStream(terrainPath, ms);
                }
            }

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
                    else if (xtr.Name.ToString() == "datetime")
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
                        if(multiRegion)
                            dearchivedScenes.SetRegionOriginalID(id);
                    }
                    else if (xtr.Name.ToString() == "dir")
                    {
                        dearchivedScenes.SetRegionDirectory(xtr.ReadElementContentAsString());
                    }
                    else if (xtr.Name.ToString() == "size_in_meters")
                    {
                        Vector3 value;
                        string size = "<" + xtr.ReadElementContentAsString() + ",0>";
                        if (Vector3.TryParse(size, out value))
                        {
                            m_incomingRegionSize = value;
                            if(multiRegion)
                                dearchivedScenes.SetRegionSize(m_incomingRegionSize);
                            m_log.DebugFormat("[ARCHIVER]: Found region_size info {0}",
                                        m_incomingRegionSize.ToString());
                        }
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
                dearchivedScenes.SetRegionSize(m_incomingRegionSize);
            }

            ControlFileLoaded = true;
            if(xtr != null)
                xtr.Close();

            return dearchivedScenes;
        }
    }
}
