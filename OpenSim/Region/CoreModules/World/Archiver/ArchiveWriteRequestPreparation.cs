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
        public ArchiveWriteRequestPreparation(Scene scene, string savePath, Guid requestId)
        {
            m_scene = scene;

            try
            {
                m_saveStream = new GZipStream(new FileStream(savePath, FileMode.Create), CompressionMode.Compress);
            }
            catch (EntryPointNotFoundException e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                        + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                m_log.Error(e);
            }
            
            m_requestId = requestId;
        }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="saveStream">The stream to which to save data.</param>
        /// <param name="requestId">The id associated with this request</param>
        public ArchiveWriteRequestPreparation(Scene scene, Stream saveStream, Guid requestId)
        {
            m_scene = scene;
            m_saveStream = saveStream;
            m_requestId = requestId;
        }

        /// <summary>
        /// Archive the region requested.
        /// </summary>
        /// <exception cref="System.IO.IOException">if there was an io problem with creating the file</exception>
        public void ArchiveRegion(Dictionary<string, object> options)
        {
            Dictionary<UUID, AssetType> assetUuids = new Dictionary<UUID, AssetType>();

            EntityBase[] entities = m_scene.GetEntities();
            List<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();

            /*
                foreach (ILandObject lo in m_scene.LandChannel.AllParcels())
                {
                    if (name == lo.LandData.Name)
                    {
                        // This is the parcel we want
                    }
                }
                */
     
            // Filter entities so that we only have scene objects.
            // FIXME: Would be nicer to have this as a proper list in SceneGraph, since lots of methods
            // end up having to do this
            foreach (EntityBase entity in entities)
            {
                if (entity is SceneObjectGroup)
                {
                    SceneObjectGroup sceneObject = (SceneObjectGroup)entity;
                    
                    if (!sceneObject.IsDeleted && !sceneObject.IsAttachment)
                        sceneObjects.Add((SceneObjectGroup)entity);
                }
            }
            
            UuidGatherer assetGatherer = new UuidGatherer(m_scene.AssetService);

            foreach (SceneObjectGroup sceneObject in sceneObjects)
            {
                assetGatherer.GatherAssetUuids(sceneObject, assetUuids);
            }

            m_log.DebugFormat(
                "[ARCHIVER]: {0} scene objects to serialize requiring save of {1} assets",
                sceneObjects.Count, assetUuids.Count);
            
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
            archiveWriter.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, Create0p2ControlFile(options));
            m_log.InfoFormat("[ARCHIVER]: Added control file to archive.");
            
            new AssetsRequest(
                new AssetsArchiver(archiveWriter), assetUuids, 
                m_scene.AssetService, awre.ReceivedAllAssets).Execute();
        }
        
        /// <summary>
        /// Create the control file for the most up to date archive
        /// </summary>
        /// <returns></returns>
        public static string Create0p2ControlFile(Dictionary<string, object> options)
        {
            int majorVersion = 0, minorVersion = 5;

            if (options.ContainsKey("version"))
            {
                minorVersion = 0;
                string[] parts = options["version"].ToString().Split('.');
                if (parts.Length >= 1)
                    majorVersion = Int32.Parse(parts[0]);
                if (parts.Length >= 2)
                    minorVersion = Int32.Parse(parts[1]);
            }
            
            m_log.InfoFormat("[ARCHIVER]: Creating version {0}.{1} OAR", majorVersion, minorVersion);
//            if (majorVersion == 1)
//            {
//                m_log.WarnFormat("[ARCHIVER]: Please be aware that version 1.0 OARs are not compatible with OpenSim 0.7.0.2 and earlier.  Please use the --version=0 option if you want to produce a compatible OAR");
//            }
            
            
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
            xtw.WriteEndElement();

            xtw.Flush();
            xtw.Close();

            String s = sw.ToString();
            sw.Close();

            return s;
        }
    }
}
