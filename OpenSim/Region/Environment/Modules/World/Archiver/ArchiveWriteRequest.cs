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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Scenes;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using libsecondlife;
using log4net;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Method called when all the necessary assets for an archive request have been received.
    /// </summary>
    public delegate void AssetsRequestCallback(IDictionary<LLUUID, AssetBase> assets);

    /// <summary>
    /// Handles an individual archive write request
    /// </summary>
    public class ArchiveWriteRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private string m_savePath;

        private string m_serializedEntities;

        public ArchiveWriteRequest(Scene scene, string savePath)
        {
            m_scene = scene;
            m_savePath = savePath;

            ArchiveRegion();
        }

        protected void ArchiveRegion()
        {
            Dictionary<LLUUID, int> assetUuids = new Dictionary<LLUUID, int>();

            List<EntityBase> entities = m_scene.GetEntities();

            foreach (EntityBase entity in entities)
            {
                if (entity is SceneObjectGroup)
                {
                    SceneObjectGroup sceneObject = (SceneObjectGroup)entity;

                    foreach (SceneObjectPart part in sceneObject.GetParts())
                    {
                        // XXX: Not a great way to iterate through face textures, but there's no
                        // other way to tell how many faces there actually are
                        //int i = 0;
                        foreach (LLObject.TextureEntryFace texture in part.Shape.Textures.FaceTextures)
                        {
                            if (texture != null)
                            {
                                //m_log.DebugFormat("[ARCHIVER]: Got face {0}", i++);
                                assetUuids[texture.TextureID] = 1;
                            }
                        }

                        foreach (TaskInventoryItem tit in part.TaskInventory.Values)
                        {
                            if (tit.Type != (int)InventoryType.Object)
                            {
                                m_log.DebugFormat("[ARCHIVER]: Recording asset {0} in object {1}", tit.AssetID, part.UUID);
                                assetUuids[tit.AssetID] = 1;
                            }
                        }
                    }
                }
            }

            m_serializedEntities = SerializeObjects(entities);

            if (m_serializedEntities != null && m_serializedEntities.Length > 0)
            {
                m_log.DebugFormat("[ARCHIVER]: Successfully got serialization for {0} entities", entities.Count);
                m_log.DebugFormat("[ARCHIVER]: Requiring save of {0} textures", assetUuids.Count);

                // Asynchronously request all the assets required to perform this archive operation
                new AssetsRequest(ReceivedAllAssets, m_scene.AssetCache, assetUuids.Keys);
            }
        }

        protected internal void ReceivedAllAssets(IDictionary<LLUUID, AssetBase> assets)
        {
            m_log.DebugFormat("[ARCHIVER]: Received all {0} textures required", assets.Count);

            // XXX: Shouldn't hijack the asset async callback thread like this - this is only temporary

            TarArchiveWriter archive = new TarArchiveWriter();

            archive.AddFile(ArchiveConstants.PRIMS_PATH, m_serializedEntities);
            
            AssetsArchiver assetsArchiver = new AssetsArchiver(assets);
            assetsArchiver.Archive(archive);

            archive.WriteTar(m_savePath);

            m_log.InfoFormat("[ARCHIVER]: Wrote out OpenSimulator archive {0}", m_savePath);
        }

        /// <summary>
        /// Get an xml representation of the given scene objects.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        protected static string SerializeObjects(List<EntityBase> entities)
        {
            string serialization = "<scene>";

            List<string> serObjects = new List<string>();

            foreach (EntityBase ent in entities)
            {
                if (ent is SceneObjectGroup)
                {
                    serObjects.Add(((SceneObjectGroup) ent).ToXmlString2());
                }
            }

            foreach (string serObject in serObjects)
                serialization += serObject;

            serialization += "</scene>";

            return serialization;
        }
    }

    /// <summary>
    /// Encapsulate the asynchronous requests for the assets required for an archive operation
    /// </summary>
    class AssetsRequest
    {
        /// <summary>
        /// Callback used when all the assets requested have been received.
        /// </summary>
        protected AssetsRequestCallback m_assetsRequestCallback;

        /// <summary>
        /// Assets retrieved in this request
        /// </summary>
        protected Dictionary<LLUUID, AssetBase> m_assets = new Dictionary<LLUUID, AssetBase>();

        /// <summary>
        /// Record the number of asset replies required so we know when we've finished
        /// </summary>
        private int m_repliesRequired;

        /// <summary>
        /// Asset cache used to request the assets
        /// </summary>
        protected AssetCache m_assetCache;

        protected internal AssetsRequest(AssetsRequestCallback assetsRequestCallback, AssetCache assetCache, ICollection<LLUUID> uuids)
        {
            m_assetsRequestCallback = assetsRequestCallback;
            m_assetCache = assetCache;
            m_repliesRequired = uuids.Count;

            // We can stop here if there are no assets to fetch
            if (m_repliesRequired == 0)
                m_assetsRequestCallback(m_assets);

            foreach (LLUUID uuid in uuids)
            {
                m_assetCache.GetAsset(uuid, AssetRequestCallback, true);
            }
        }

        /// <summary>
        /// Called back by the asset cache when it has the asset
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="asset"></param>
        public void AssetRequestCallback(LLUUID assetID, AssetBase asset)
        {
            m_assets[assetID] = asset;

            if (m_assets.Count == m_repliesRequired)
            {
                // We want to stop using the asset cache thread asap as we now need to do the actual work of producing the archive
                Thread newThread = new Thread(PerformAssetsRequestCallback);
                newThread.Name = "OpenSimulator archiving thread post assets receipt";
                newThread.Start();
            }
        }

        /// <summary>
        /// Perform the callback on the original requester of the assets
        /// </summary>
        protected void PerformAssetsRequestCallback()
        {
            m_assetsRequestCallback(m_assets);
        }
    }
}
