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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Encapsulate the asynchronous requests for the assets required for an archive operation
    /// </summary>
    class AssetsRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// uuids to request
        /// </summary>
        protected ICollection<UUID> m_uuids;

        /// <summary>
        /// Callback used when all the assets requested have been received.
        /// </summary>
        protected AssetsRequestCallback m_assetsRequestCallback;

        /// <summary>
        /// Assets retrieved in this request
        /// </summary>
        protected Dictionary<UUID, AssetBase> m_assets = new Dictionary<UUID, AssetBase>();

        /// <summary>
        /// Maintain a list of assets that could not be found.  This will be passed back to the requester.
        /// </summary>
        protected List<UUID> m_notFoundAssetUuids = new List<UUID>();

        /// <summary>
        /// Record the number of asset replies required so we know when we've finished
        /// </summary>
        private int m_repliesRequired;

        /// <summary>
        /// Asset cache used to request the assets
        /// </summary>
        protected AssetCache m_assetCache;

        protected internal AssetsRequest(ICollection<UUID> uuids, AssetCache assetCache, AssetsRequestCallback assetsRequestCallback)
        {
            m_uuids = uuids;
            m_assetsRequestCallback = assetsRequestCallback;
            m_assetCache = assetCache;
            m_repliesRequired = uuids.Count;
        }

        protected internal void Execute()
        {
            // We can stop here if there are no assets to fetch
            if (m_repliesRequired == 0)
                m_assetsRequestCallback(m_assets, m_notFoundAssetUuids);

            foreach (UUID uuid in m_uuids)
            {
                m_assetCache.GetAsset(uuid, AssetRequestCallback, true);
            }
        }

        /// <summary>
        /// Called back by the asset cache when it has the asset
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="asset"></param>
        public void AssetRequestCallback(UUID assetID, AssetBase asset)
        {
            if (asset != null)
                m_assets[assetID] = asset;
            else
                m_notFoundAssetUuids.Add(assetID);

            //m_log.DebugFormat(
            //    "[ARCHIVER]: Received {0} assets and notification of {1} missing assets", m_assets.Count, m_notFoundAssetUuids.Count);

            if (m_assets.Count + m_notFoundAssetUuids.Count == m_repliesRequired)
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
            try
            {
                m_assetsRequestCallback(m_assets, m_notFoundAssetUuids);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Terminating archive creation since asset requster callback failed with {0}", e);
            }
        }
    }
}
