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
using System.Timers;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Encapsulate the asynchronous requests for the assets required for an archive operation
    /// </summary>
    class AssetsRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Method called when all the necessary assets for an archive request have been received.
        /// </summary>
        public delegate void AssetsRequestCallback(
            ICollection<UUID> assetsFoundUuids, ICollection<UUID> assetsNotFoundUuids, bool timedOut);

        enum RequestState
        {
            Initial,
            Running,
            Completed,
            Aborted
        };

        /// <value>
        /// uuids to request
        /// </value>
        protected IDictionary<UUID, sbyte> m_uuids;
        private int m_previousErrorsCount;

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

        private System.Timers.Timer m_timeOutTimer;
        private bool m_timeout;

        /// <value>
        /// Asset service used to request the assets
        /// </value>
        protected IAssetService m_assetService;
        protected IUserAccountService m_userAccountService;
        protected UUID m_scopeID; // the grid ID

        protected AssetsArchiver m_assetsArchiver;

        protected Dictionary<string, object> m_options;

        protected internal AssetsRequest(
            AssetsArchiver assetsArchiver, IDictionary<UUID, sbyte> uuids,
            int previousErrorsCount,
            IAssetService assetService, IUserAccountService userService,
            UUID scope, Dictionary<string, object> options,
            AssetsRequestCallback assetsRequestCallback)
        {
            m_assetsArchiver = assetsArchiver;
            m_uuids = uuids;
            m_previousErrorsCount = previousErrorsCount;
            m_assetsRequestCallback = assetsRequestCallback;
            m_assetService = assetService;
            m_userAccountService = userService;
            m_scopeID = scope;
            m_options = options;
            m_repliesRequired = uuids.Count;
        }

        protected internal void Execute()
        {
            Culture.SetCurrentCulture();
            // We can stop here if there are no assets to fetch
            if (m_repliesRequired == 0)
            {
                PerformAssetsRequestCallback(false);
                return;
            }

            m_timeOutTimer = new System.Timers.Timer(90000);
            m_timeOutTimer .AutoReset = false;
            m_timeOutTimer.Elapsed += OnTimeout;
            m_timeout = false;
            int gccontrol = 0;

            foreach (KeyValuePair<UUID, sbyte> kvp in m_uuids)
            {
                string thiskey = kvp.Key.ToString();
                try
                {
                    m_timeOutTimer.Enabled = true;
                    AssetBase asset = m_assetService.Get(thiskey);
                    if(m_timeout)
                        break;

                    m_timeOutTimer.Enabled = false;

                    if(asset == null)
                    {
                        m_notFoundAssetUuids.Add(kvp.Key);
                        continue;
                    }

                    if(asset.FullID.IsZero())
                    {
                        if(!UUID.TryParse(thiskey, out UUID id) || id.IsZero())
                        {
                            m_log.InfoFormat($"[ARCHIVER]: cannot save asset {kvp.Key} because it has Invalid UUID");
                            m_notFoundAssetUuids.Add(kvp.Key);
                            continue;
                        }
                        asset.FullID = id;
                    }

                    sbyte assetType = kvp.Value;
                    if (assetType == (sbyte)AssetType.Unknown)
                    {
                        m_log.InfoFormat("[ARCHIVER]: Rewriting broken asset type for {0} to {1}", thiskey, SLUtil.AssetTypeFromCode(assetType));
                        asset.Type = assetType;
                    }

                    m_foundAssetUuids.Add(asset.FullID);
                    m_assetsArchiver.WriteAsset(PostProcess(asset));
                    if(++gccontrol > 10000)
                    {
                        gccontrol = 0;
                        GC.Collect();
                    }
                }

                catch (Exception e)
                {
                    m_log.ErrorFormat("[ARCHIVER]: Execute failed with {0}", e);
                }
            }

            m_timeOutTimer.Dispose();
            int totalerrors = m_notFoundAssetUuids.Count + m_previousErrorsCount;

            if(m_timeout)
                m_log.DebugFormat("[ARCHIVER]: Aborted because AssetService request timeout. Successfully added {0} assets", m_foundAssetUuids.Count);
            else if(totalerrors == 0)
                m_log.DebugFormat("[ARCHIVER]: Successfully added all {0} assets", m_foundAssetUuids.Count);
            else
                m_log.DebugFormat("[ARCHIVER]: Successfully added {0} assets ({1} of total possible assets requested were not found, were damaged or were not assets)",
                            m_foundAssetUuids.Count, totalerrors);

            GC.Collect();
            PerformAssetsRequestCallback(m_timeout);
        }
  
        private void OnTimeout(object source, ElapsedEventArgs args)
        {
            m_timeout = true;
        }

        /// <summary>
        /// Perform the callback on the original requester of the assets
        /// </summary>
        private void PerformAssetsRequestCallback(object o)
        {
            if(m_assetsRequestCallback == null)
                return;
            Culture.SetCurrentCulture();

            Boolean timedOut = (Boolean)o;

            try
            {
                m_assetsRequestCallback(m_foundAssetUuids, m_notFoundAssetUuids, timedOut);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Terminating archive creation since asset requster callback failed with {0}", e);
            }
        }

        private AssetBase PostProcess(AssetBase asset)
        {
            if (asset.Type == (sbyte)AssetType.Object && asset.Data != null && m_options.ContainsKey("home"))
            {
                //m_log.DebugFormat("[ARCHIVER]: Rewriting object data for {0}", asset.ID);
                string xml = ExternalRepresentationUtils.RewriteSOP(Utils.BytesToString(asset.Data), string.Empty, m_options["home"].ToString(), m_userAccountService, m_scopeID);
                asset.Data = Utils.StringToBytes(xml);
            }
            return asset;
        }
    }
}
