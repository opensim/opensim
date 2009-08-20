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

using System.Reflection;
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.NHibernate
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class NHibernateAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private NHibernateManager manager;
        public NHibernateManager Manager
        {
            get
            {
                return manager;
            }
        }

        override public void Dispose() { }

        public override void Initialise()
        {
            m_log.Info("[NHibernateGridData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public override void Initialise(string connect)
        {

            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateAssetData");
            manager = new NHibernateManager(connect, "AssetStore");

        }

        override public AssetBase GetAsset(UUID uuid)
        {
            return (AssetBase)manager.Get(typeof(AssetBase), uuid);
        }

        override public void StoreAsset(AssetBase asset)
        {
            AssetBase temp = (AssetBase)manager.Get(typeof(AssetBase), asset.FullID);
            if (temp == null)
            {
                m_log.InfoFormat("[NHIBERNATE] inserting asset {0}", asset.FullID);
                manager.Insert(asset);
            }
            else
            {
                m_log.InfoFormat("[NHIBERNATE] updating asset {0}", asset.FullID);
                manager.Update(asset);
            }
        }

        // private void LogAssetLoad(AssetBase asset)
        // {
        //     string temporary = asset.Temporary ? "Temporary" : "Stored";
        //     string local = asset.Local ? "Local" : "Remote";

        //     int assetLength = (asset.Data != null) ? asset.Data.Length : 0;

        //     m_log.Info("[SQLITE]: " +
        //                              string.Format("Loaded {6} {5} Asset: [{0}][{3}/{4}] \"{1}\":{2} ({7} bytes)",
        //                                            asset.FullID, asset.Name, asset.Description, asset.Type,
        //                                            asset.InvType, temporary, local, assetLength));
        // }

        override public bool ExistsAsset(UUID uuid)
        {
            m_log.InfoFormat("[NHIBERNATE] ExistsAsset: {0}", uuid);
            return (GetAsset(uuid) != null);
        }

        /// <summary>
        /// Returns a list of AssetMetadata objects. The list is a subset of
        /// the entire data set offset by <paramref name="start" /> containing
        /// <paramref name="count" /> elements.
        /// </summary>
        /// <param name="start">The number of results to discard from the total data set.</param>
        /// <param name="count">The number of rows the returned list should contain.</param>
        /// <returns>A list of AssetMetadata objects.</returns>
        public override List<AssetMetadata> FetchAssetMetadataSet(int start, int count)
        {
            List<AssetMetadata> retList = new List<AssetMetadata>(count);
            return retList;
        }

        public void DeleteAsset(UUID uuid)
        {

        }

        public override string Name {
            get { return "NHibernate"; }
        }

        public override string Version {
            get { return "0.1"; }
        }

    }
}
