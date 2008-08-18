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
using System.Reflection;
using System.Threading;
using libsecondlife;
using log4net;
using OpenSim.Framework.AssetLoader.Filesystem;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications.Cache
{
    public abstract class AssetServerBase : IAssetServer
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IAssetReceiver m_receiver;
        protected BlockingQueue<AssetRequest> m_assetRequests;
        protected Thread m_localAssetServerThread;
        protected IAssetProviderPlugin m_assetProvider;

        // Temporarily hardcoded - should be a plugin
        protected IAssetLoader assetLoader = new AssetLoaderFileSystem();

        public abstract void StoreAsset(AssetBase asset);

        /// <summary>
        /// This method must be implemented by a subclass to retrieve the asset named in the
        /// AssetRequest.  If the asset is not found, null should be returned.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception">
        /// Thrown if the request failed for some other reason than that the
        /// asset cannot be found.
        /// </exception>
        protected abstract AssetBase GetAsset(AssetRequest req);

        /// <summary>
        /// Process an asset request.  This method will call GetAsset(AssetRequest req)
        /// on the subclass.
        /// </summary>
        /// <param name="req"></param>
        protected virtual void ProcessRequest(AssetRequest req)
        {
            AssetBase asset;

            try
            {
                asset = GetAsset(req);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ASSET]: Asset request for {0} threw exception {1}", req.AssetID, e);

                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddAssetServiceRequestFailure();

                m_receiver.AssetNotFound(req.AssetID, req.IsTexture);

                return;
            }

            if (asset != null)
            {
                //m_log.DebugFormat("[ASSET]: Asset {0} received from asset server", req.AssetID);

                m_receiver.AssetReceived(asset, req.IsTexture);
            }
            else
            {
                //m_log.WarnFormat("[ASSET]: Asset {0} not found by asset server", req.AssetID);

                m_receiver.AssetNotFound(req.AssetID, req.IsTexture);
            }
        }

        public virtual void LoadDefaultAssets()
        {
            m_log.Info("[ASSET SERVER]: Setting up asset database");

            assetLoader.ForEachDefaultXmlAsset(StoreAsset);
        }

        public AssetServerBase()
        {
            m_log.Info("[ASSET SERVER]: Starting asset storage system");
            m_assetRequests = new BlockingQueue<AssetRequest>();

            m_localAssetServerThread = new Thread(RunRequests);
            m_localAssetServerThread.Name = "LocalAssetServerThread";
            m_localAssetServerThread.IsBackground = true;
            m_localAssetServerThread.Start();
            ThreadTracker.Add(m_localAssetServerThread);
        }

        private void RunRequests()
        {
            while (true) // Since it's a 'blocking queue'
            {
                try
                {
                    AssetRequest req = m_assetRequests.Dequeue();

                    ProcessRequest(req);
                }
                catch (Exception e)
                {
                    m_log.Error("[ASSET SERVER]: " + e.ToString());
                }
            }
        }

        /// <summary>
        /// The receiver will be called back with asset data once it comes in.
        /// </summary>
        /// <param name="receiver"></param>
        public void SetReceiver(IAssetReceiver receiver)
        {
            m_receiver = receiver;
        }

        public void RequestAsset(LLUUID assetID, bool isTexture)
        {
            AssetRequest req = new AssetRequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            m_assetRequests.Enqueue(req);

            #if DEBUG
            //m_log.InfoFormat("[ASSET SERVER]: Added {0} to request queue", assetID);
            #endif
        }

        public virtual void UpdateAsset(AssetBase asset)
        {
            m_assetProvider.UpdateAsset(asset);
        }

        public virtual void Close()
        {
            m_localAssetServerThread.Abort();
        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
        }
    }
}
