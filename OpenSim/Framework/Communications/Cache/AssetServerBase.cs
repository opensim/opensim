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
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework.AssetLoader.Filesystem;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Communications.Cache
{
    public abstract class AssetServerBase : IAssetServer
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IAssetReceiver m_receiver;
        protected BlockingQueue<AssetRequest> m_assetRequests = new BlockingQueue<AssetRequest>();
        protected Thread m_localAssetServerThread;
        protected IAssetDataPlugin m_assetProvider;

        #region IPlugin
 
        /// <summary> 
        /// The methods and properties in this region are needed to implement
        /// the IPlugin interface and its local extensions.
        /// These can all be overridden as appropriate by a derived class.
        /// These methods are only applicable when a class is loaded by the
        /// IPlugin mechanism.
        ///
        /// Note that in the case of AssetServerBase, all initialization is
        /// performed by the default constructor, so nothing additional is
        /// required here. A derived class may wish to do more.
        /// </summary>

        public virtual string Name
        {
            // get { return "OpenSim.Framework.Communications.Cache.AssetServerBase"; }
            get { return "AssetServerBase"; }
        }

        public virtual string Version
        {
            get { return "1.0"; }
        }

        public virtual void Initialise()
        {
            m_log.Debug("[ASSET SERVER]: IPlugin null initialization");
        }

        public virtual void Initialise(ConfigSettings settings)
        {
            m_log.Debug("[ASSET SERVER]: IPlugin null configured initialization(1)");
            m_log.InfoFormat("[ASSET SERVER]: Initializing client [{0}/{1}", Name, Version);
        }

        public virtual void Initialise(ConfigSettings settings, string p_url)
        {
            m_log.Debug("[ASSET SERVER]: IPlugin null configured initialization(2)");
            m_log.InfoFormat("[ASSET SERVER]: Initializing client [{0}/{1}", Name, Version);
        }

        public virtual void Initialise(ConfigSettings settings, string p_url, string p_dir, bool p_t)
        {
            m_log.Debug("[ASSET SERVER]: IPlugin null configured initialization(3)");
            m_log.InfoFormat("[ASSET SERVER]: Initializing client [{0}/{1}", Name, Version);
        }

        public virtual void Dispose()
        {
            m_log.Debug("[ASSET SERVER]: dispose");
        }

        #endregion

        public IAssetDataPlugin AssetProviderPlugin
        {
            get { return m_assetProvider; }
        }

        // Temporarily hardcoded - should be a plugin
        protected IAssetLoader assetLoader = new AssetLoaderFileSystem();

        public virtual void Start()
        {
            m_log.Debug("[ASSET SERVER]: Starting asset server");

            m_localAssetServerThread = new Thread(RunRequests);
            m_localAssetServerThread.Name = "LocalAssetServerThread";
            m_localAssetServerThread.IsBackground = true;
            m_localAssetServerThread.Start();
            ThreadTracker.Add(m_localAssetServerThread);            
        }
        
        public virtual void Stop()
        {
            m_localAssetServerThread.Abort();
        }        
        
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
        /// Does the asset server have any waiting requests?
        /// </summary>
        /// 
        /// This does include any request that is currently being handled.  This information is not reliable where
        /// another thread may be processing requests.
        ///  
        /// <returns>
        /// True if there are waiting requests.  False if there are no waiting requests.
        /// </returns>
        public virtual bool HasWaitingRequests()
        {
            return m_assetRequests.Count() != 0;
        }
        
        /// <summary>
        /// Process an asset request.  This method will call GetAsset(AssetRequest req)
        /// on the subclass.
        /// </summary>
        public virtual void ProcessNextRequest()
        {
            AssetRequest req = m_assetRequests.Dequeue();            
            AssetBase asset;

            try
            {
                asset = GetAsset(req);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ASSET]: Asset request for {0} threw exception {1} - Stack Trace: {2}", req.AssetID, e, e.StackTrace);

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

        public virtual void LoadDefaultAssets(string pAssetSetsXml)
        {
            m_log.Info("[ASSET SERVER]: Setting up asset database");

            assetLoader.ForEachDefaultXmlAsset(pAssetSetsXml, StoreAsset);
        }

        private void RunRequests()
        {
            while (true) // Since it's a 'blocking queue'
            {
                try
                {                    
                    ProcessNextRequest();
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

        public void RequestAsset(UUID assetID, bool isTexture)
        {
            AssetRequest req = new AssetRequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            m_assetRequests.Enqueue(req);

            //m_log.DebugFormat("[ASSET SERVER]: Added {0} to request queue", assetID);
        }

        public virtual void UpdateAsset(AssetBase asset)
        {
            m_assetProvider.UpdateAsset(asset);
        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
        }
    }
}
