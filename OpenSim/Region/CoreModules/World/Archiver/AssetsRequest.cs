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
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Encapsulate the asynchronous requests for the assets required for an archive operation
    /// </summary>
    class AssetsRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// uuids to request
        /// </value>
        protected ICollection<UUID> m_uuids;

        /// <value>
        /// Callback used when all the assets requested have been received.
        /// </value>
        protected AssetsRequestCallback m_assetsRequestCallback;

        /// <value>
        /// List of assets that were found.  This will be passed back to the requester.
        /// </value>
        protected List<UUID> m_foundAssetUuids = new List<UUID>();
        
        /// <value>
        /// Maintain a list of assets that could not be found.  This will be passed back to the requester.
        /// </value>
        protected List<UUID> m_notFoundAssetUuids = new List<UUID>();

        /// <value>
        /// Record the number of asset replies required so we know when we've finished
        /// </value>
        private int m_repliesRequired;

        /// <value>
        /// Asset service used to request the assets
        /// </value>
        protected IAssetService m_assetService;

        protected AssetsArchiver m_assetsArchiver;

        protected internal AssetsRequest(
            AssetsArchiver assetsArchiver, ICollection<UUID> uuids, 
            IAssetService assetService, AssetsRequestCallback assetsRequestCallback)
        {
            m_assetsArchiver = assetsArchiver;
            m_uuids = uuids;
            m_assetsRequestCallback = assetsRequestCallback;
            m_assetService = assetService;
            m_repliesRequired = uuids.Count;
        }

        protected internal void Execute()
        {
            m_log.DebugFormat("[ARCHIVER]: AssetsRequest executed looking for {0} assets", m_repliesRequired);
            
            // We can stop here if there are no assets to fetch
            if (m_repliesRequired == 0)
                PerformAssetsRequestCallback();
            
            foreach (UUID uuid in m_uuids)
            {
                m_assetService.Get(uuid.ToString(), this, AssetRequestCallback);
            }
        }

        private bool done = false;
        /// <summary>
        /// Called back by the asset cache when it has the asset
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="asset"></param>
        public void AssetRequestCallback(string id, object sender, AssetBase asset)
        {
            try
            {
                lock (this)
                {
                    //m_log.DebugFormat("[ARCHIVER]: Received callback for asset {0}", id);
                    
                    if (asset != null)
                    {                
                        m_foundAssetUuids.Add(asset.FullID);
                        m_assetsArchiver.WriteAsset(asset);
                    }
                    else
                    {
                        m_notFoundAssetUuids.Add(new UUID(id));
                    }
        
                    if (m_foundAssetUuids.Count + m_notFoundAssetUuids.Count == m_repliesRequired)
                    {
                        if (done)
                            throw new Exception("AArgh");
                        
                        m_log.DebugFormat(
                            "[ARCHIVER]: Successfully added {0} assets ({1} assets missing)", 
                            m_foundAssetUuids.Count, m_notFoundAssetUuids.Count);
    
                        done = true;
                        
                        // We want to stop using the asset cache thread asap 
                        // as we now need to do the work of producing the rest of the archive
                        Thread newThread = new Thread(PerformAssetsRequestCallback);
                        newThread.Name = "OpenSimulator archiving thread post assets receipt";
                        newThread.Start();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ARCHIVER]: AssetRequestCallback failed with {0}", e);
            }
        }

        /// <summary>
        /// Perform the callback on the original requester of the assets
        /// </summary>
        protected void PerformAssetsRequestCallback()
        {
            try
            {
                m_assetsRequestCallback(m_foundAssetUuids, m_notFoundAssetUuids);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Terminating archive creation since asset requster callback failed with {0}", e);
            }
        }
    }
}
