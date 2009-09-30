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
        public ArchiveWriteRequestPreparation(Scene scene, string savePath, Guid requestId)
        {
            m_scene = scene;
            m_saveStream = new GZipStream(new FileStream(savePath, FileMode.Create), CompressionMode.Compress);
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
        public void ArchiveRegion()
        {
            Dictionary<UUID, int> assetUuids = new Dictionary<UUID, int>();

            List<EntityBase> entities = m_scene.GetEntities();
            List<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();

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
                assetUuids[regionSettings.TerrainTexture1] = 1;
            
            if (regionSettings.TerrainTexture2 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_2)
                assetUuids[regionSettings.TerrainTexture2] = 1;
            
            if (regionSettings.TerrainTexture3 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_3)
                assetUuids[regionSettings.TerrainTexture3] = 1;
            
            if (regionSettings.TerrainTexture4 != RegionSettings.DEFAULT_TERRAIN_TEXTURE_4)
                assetUuids[regionSettings.TerrainTexture4] = 1;

            TarArchiveWriter archiveWriter = new TarArchiveWriter(m_saveStream);
            
            // Asynchronously request all the assets required to perform this archive operation
            ArchiveWriteRequestExecution awre
                = new ArchiveWriteRequestExecution(
                    sceneObjects,
                    m_scene.RequestModuleInterface<ITerrainModule>(),
                    m_scene.RequestModuleInterface<IRegionSerialiserModule>(),
                    m_scene,
                    archiveWriter,
                    m_requestId);
            
            new AssetsRequest(
                new AssetsArchiver(archiveWriter), assetUuids.Keys, 
                m_scene.AssetService, awre.ReceivedAllAssets).Execute();
        }
    }
}
