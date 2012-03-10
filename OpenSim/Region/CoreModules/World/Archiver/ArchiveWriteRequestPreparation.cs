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
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Prepare to write out an archive.
    /// </summary>
    public class ArchiveWriteRequestPreparation
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The minimum major version of OAR that we can write.
        /// </summary>
        public static int MIN_MAJOR_VERSION = 0;       
                
        /// <summary>
        /// The maximum major version of OAR that we can write.
        /// </summary>
        public static int MAX_MAJOR_VERSION = 0;

        /// <summary>
        /// Determine whether this archive will save assets.  Default is true.
        /// </summary>
        public bool SaveAssets { get; set; }
        
        protected Scene m_scene;
        protected Stream m_saveStream;
        protected Guid m_requestId;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="savePath">The path to which to save data.</param>
        /// <param name="requestId">The id associated with this request</param>
        /// <exception cref="System.IO.IOException">
        /// If there was a problem opening a stream for the file specified by the savePath
        /// </exception>
        public ArchiveWriteRequestPreparation(Scene scene, string savePath, Guid requestId) : this(scene, requestId)
        {
            try
            {
                m_saveStream = new GZipStream(new FileStream(savePath, FileMode.Create), CompressionMode.Compress);
            }
            catch (EntryPointNotFoundException e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                m_log.ErrorFormat("{0} {1}", e.Message, e.StackTrace);
            }
        }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="saveStream">The stream to which to save data.</param>
        /// <param name="requestId">The id associated with this request</param>
        public ArchiveWriteRequestPreparation(Scene scene, Stream saveStream, Guid requestId) : this(scene, requestId)
        {
            m_saveStream = saveStream;
        }

        protected ArchiveWriteRequestPreparation(Scene scene, Guid requestId)
        {
            m_scene = scene;
            m_requestId = requestId;

            SaveAssets = true;
        }

        /// <summary>
        /// Archive the region requested.
        /// </summary>
        /// <exception cref="System.IO.IOException">if there was an io problem with creating the file</exception>
        public void ArchiveRegion(Dictionary<string, object> options)
        {
            if (options.ContainsKey("noassets") && (bool)options["noassets"])
                SaveAssets = false;

            try
            {
                Dictionary<UUID, AssetType> assetUuids = new Dictionary<UUID, AssetType>();
    
                EntityBase[] entities = m_scene.GetEntities();
                List<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();

                string checkPermissions = null;
                int numObjectsSkippedPermissions = 0;
                Object temp;
                if (options.TryGetValue("checkPermissions", out temp))
                    checkPermissions = (string)temp;
         
                // Filter entities so that we only have scene objects.
                // FIXME: Would be nicer to have this as a proper list in SceneGraph, since lots of methods
                // end up having to do this
                foreach (EntityBase entity in entities)
                {
                    if (entity is SceneObjectGroup)
                    {
                        SceneObjectGroup sceneObject = (SceneObjectGroup)entity;

                        if (!sceneObject.IsDeleted && !sceneObject.IsAttachment)
                        {
                            if (!CanUserArchiveObject(m_scene.RegionInfo.EstateSettings.EstateOwner, sceneObject, checkPermissions))
                            {
                                // The user isn't allowed to copy/transfer this object, so it will not be included in the OAR.
                                ++numObjectsSkippedPermissions;
                            }
                            else
                            {
                                sceneObjects.Add(sceneObject);
                            }
                        }
                    }
                }

                if (SaveAssets)
                {
                    UuidGatherer assetGatherer = new UuidGatherer(m_scene.AssetService);

                    foreach (SceneObjectGroup sceneObject in sceneObjects)
                    {
                        assetGatherer.GatherAssetUuids(sceneObject, assetUuids);
                    }

                    m_log.DebugFormat(
                        "[ARCHIVER]: {0} scene objects to serialize requiring save of {1} assets",
                        sceneObjects.Count, assetUuids.Count);
                }
                else
                {
                    m_log.DebugFormat("[ARCHIVER]: Not saving assets since --noassets was specified");
                }

                if (numObjectsSkippedPermissions > 0)
                {
                    m_log.DebugFormat(
                        "[ARCHIVER]: {0} scene objects skipped due to lack of permissions",
                        numObjectsSkippedPermissions);
                }

                // Make sure that we also request terrain texture assets
                RegionSettings regionSettings = m_scene.RegionInfo.RegionSettings;
    
                if (regionSettings.TerrainTexture1 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_1)
                    assetUuids[regionSettings.TerrainTexture1] = AssetType.Texture;
                
                if (regionSettings.TerrainTexture2 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_2)
                    assetUuids[regionSettings.TerrainTexture2] = AssetType.Texture;
                
                if (regionSettings.TerrainTexture3 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_3)
                    assetUuids[regionSettings.TerrainTexture3] = AssetType.Texture;
                
                if (regionSettings.TerrainTexture4 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_4)
                    assetUuids[regionSettings.TerrainTexture4] = AssetType.Texture;
    
                TarArchiveWriter archiveWriter = new TarArchiveWriter(m_saveStream);
                
                // Asynchronously request all the assets required to perform this archive operation
                ArchiveWriteRequestExecution awre
                    = new ArchiveWriteRequestExecution(
                        sceneObjects,
                        m_scene.RequestModuleInterface<ITerrainModule>(),
                        m_scene.RequestModuleInterface<IRegionSerialiserModule>(),
                        m_scene,
                        archiveWriter,
                        m_requestId,
                        options);
    
                m_log.InfoFormat("[ARCHIVER]: Creating archive file.  This may take some time.");
    
                // Write out control file.  This has to be done first so that subsequent loaders will see this file first
                // XXX: I know this is a weak way of doing it since external non-OAR aware tar executables will not do this
                archiveWriter.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, CreateControlFile(options));
                m_log.InfoFormat("[ARCHIVER]: Added control file to archive.");

                if (SaveAssets)
                {
                    AssetsRequest ar
                        = new AssetsRequest(
                            new AssetsArchiver(archiveWriter), assetUuids,
                            m_scene.AssetService, m_scene.UserAccountService, 
                            m_scene.RegionInfo.ScopeID, options, awre.ReceivedAllAssets);

                    Util.FireAndForget(o => ar.Execute());
                }
                else
                {
                    awre.ReceivedAllAssets(new List<UUID>(), new List<UUID>());
                }
            }
            catch (Exception) 
            {
                m_saveStream.Close();
                throw;
            }    
        }

        /// <summary>
        /// Checks whether the user has permission to export an object group to an OAR.
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="objGroup">The object group</param>
        /// <param name="checkPermissions">Which permissions to check: "C" = Copy, "T" = Transfer</param>
        /// <returns>Whether the user is allowed to export the object to an OAR</returns>
        private bool CanUserArchiveObject(UUID user, SceneObjectGroup objGroup, string checkPermissions)
        {
            if (checkPermissions == null)
                return true;

            IPermissionsModule module = m_scene.RequestModuleInterface<IPermissionsModule>();
            if (module == null)
                return true;    // this shouldn't happen

            // Check whether the user is permitted to export all of the parts in the SOG. If any
            // part can't be exported then the entire SOG can't be exported.

            bool permitted = true;
            //int primNumber = 1;

            foreach (SceneObjectPart obj in objGroup.Parts)
            {
                uint perm;
                PermissionClass permissionClass = module.GetPermissionClass(user, obj);
                switch (permissionClass)
                {
                    case PermissionClass.Owner:
                        perm = obj.BaseMask;
                        break;
                    case PermissionClass.Group:
                        perm = obj.GroupMask | obj.EveryoneMask;
                        break;
                    case PermissionClass.Everyone:
                    default:
                        perm = obj.EveryoneMask;
                        break;
                }

                bool canCopy = (perm & (uint)PermissionMask.Copy) != 0;
                bool canTransfer = (perm & (uint)PermissionMask.Transfer) != 0;

                // Special case: if Everyone can copy the object then this implies it can also be
                // Transferred.
                // However, if the user is the Owner then we don't check EveryoneMask, because it seems that the mask
                // always (incorrectly) includes the Copy bit set in this case. But that's a mistake: the viewer
                // does NOT show that the object has Everyone-Copy permissions, and doesn't allow it to be copied.
                if (permissionClass != PermissionClass.Owner)
                    canTransfer |= (obj.EveryoneMask & (uint)PermissionMask.Copy) != 0;

                bool partPermitted = true;
                if (checkPermissions.Contains("C") && !canCopy)
                    partPermitted = false;
                if (checkPermissions.Contains("T") && !canTransfer)
                    partPermitted = false;

                //string name = (objGroup.PrimCount == 1) ? objGroup.Name : string.Format("{0} ({1}/{2})", obj.Name, primNumber, objGroup.PrimCount);
                //m_log.DebugFormat("[ARCHIVER]: Object permissions: {0}: Base={1:X4}, Owner={2:X4}, Everyone={3:X4}, permissionClass={4}, checkPermissions={5}, canCopy={6}, canTransfer={7}, permitted={8}",
                //    name, obj.BaseMask, obj.OwnerMask, obj.EveryoneMask,
                //    permissionClass, checkPermissions, canCopy, canTransfer, permitted);

                if (!partPermitted)
                {
                    permitted = false;
                    break;
                }

                //++primNumber;
            }

            return permitted;
        }

        /// <summary>
        /// Create the control file for the most up to date archive
        /// </summary>
        /// <returns></returns>
        public string CreateControlFile(Dictionary<string, object> options)
        {
            int majorVersion = MAX_MAJOR_VERSION, minorVersion = 7;
//
//            if (options.ContainsKey("version"))
//            {
//                string[] parts = options["version"].ToString().Split('.');
//                if (parts.Length >= 1)
//                {
//                    majorVersion = Int32.Parse(parts[0]);                    
//                                        
//                    if (parts.Length >= 2)
//                        minorVersion = Int32.Parse(parts[1]);
//                }
//            }
//            
//            if (majorVersion < MIN_MAJOR_VERSION || majorVersion > MAX_MAJOR_VERSION)
//            {
//                throw new Exception(
//                    string.Format(
//                        "OAR version number for save must be between {0} and {1}", 
//                        MIN_MAJOR_VERSION, MAX_MAJOR_VERSION));
//            }
//            else if (majorVersion == MAX_MAJOR_VERSION)
//            {
//                // Force 1.0
//                minorVersion = 0;
//            }
//            else if (majorVersion == MIN_MAJOR_VERSION)
//            {
//                // Force 0.4
//                minorVersion = 4;                                        
//            }
            
            m_log.InfoFormat("[ARCHIVER]: Creating version {0}.{1} OAR", majorVersion, minorVersion);
            //if (majorVersion == 1)
            //{
            //    m_log.WarnFormat("[ARCHIVER]: Please be aware that version 1.0 OARs are not compatible with OpenSim 0.7.0.2 and earlier.  Please use the --version=0 option if you want to produce a compatible OAR");
            //}            
            
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;
            xtw.WriteStartDocument();
            xtw.WriteStartElement("archive");
            xtw.WriteAttributeString("major_version", majorVersion.ToString());
            xtw.WriteAttributeString("minor_version", minorVersion.ToString());

            xtw.WriteStartElement("creation_info");
            DateTime now = DateTime.UtcNow;
            TimeSpan t = now - new DateTime(1970, 1, 1);
            xtw.WriteElementString("datetime", ((int)t.TotalSeconds).ToString());
            xtw.WriteElementString("id", UUID.Random().ToString());
            xtw.WriteEndElement();

            xtw.WriteElementString("assets_included", SaveAssets.ToString());

            xtw.WriteEndElement();

            xtw.Flush();
            xtw.Close();

            String s = sw.ToString();
            sw.Close();

            return s;
        }
    }
}
