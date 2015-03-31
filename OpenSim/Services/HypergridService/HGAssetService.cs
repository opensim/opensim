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
using System.Reflection;
using System.Xml;

using Nini.Config;
using log4net;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.AssetService;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Hypergrid asset service. It serves the IAssetService interface,
    /// but implements it in ways that are appropriate for inter-grid
    /// asset exchanges.
    /// </summary>
    public class HGAssetService : OpenSim.Services.AssetService.AssetService, IAssetService
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        private string m_HomeURL;
        private IUserAccountService m_UserAccountService;

        private UserAccountCache m_Cache;

        private AssetPermissions m_AssetPerms;

        public HGAssetService(IConfigSource config, string configName) : base(config, configName)
        {
            m_log.Debug("[HGAsset Service]: Starting");
            IConfig assetConfig = config.Configs[configName];
            if (assetConfig == null)
                throw new Exception("No HGAssetService configuration");

            string userAccountsDll = assetConfig.GetString("UserAccountsService", string.Empty);
            if (userAccountsDll == string.Empty)
                throw new Exception("Please specify UserAccountsService in HGAssetService configuration");

            Object[] args = new Object[] { config };
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(userAccountsDll, args);
            if (m_UserAccountService == null)
                throw new Exception(String.Format("Unable to create UserAccountService from {0}", userAccountsDll));

            m_HomeURL = Util.GetConfigVarFromSections<string>(config, "HomeURI",
                new string[] { "Startup", "Hypergrid", configName }, string.Empty);
            if (m_HomeURL == string.Empty)
                throw new Exception("[HGAssetService] No HomeURI specified");

            m_Cache = UserAccountCache.CreateUserAccountCache(m_UserAccountService);

            // Permissions
            m_AssetPerms = new AssetPermissions(assetConfig);

        }

        #region IAssetService overrides
        public override AssetBase Get(string id)
        {
            AssetBase asset = base.Get(id);

            if (asset == null)
                return null;

            if (!m_AssetPerms.AllowedExport(asset.Type))
                return null;

            if (asset.Metadata.Type == (sbyte)AssetType.Object)
                asset.Data = AdjustIdentifiers(asset.Data);

            AdjustIdentifiers(asset.Metadata);

            return asset;
        }

        public override AssetMetadata GetMetadata(string id)
        {
            AssetMetadata meta = base.GetMetadata(id);

            if (meta == null)
                return null;

            AdjustIdentifiers(meta);

            return meta;
        }

        public override byte[] GetData(string id)
        {
            AssetBase asset = Get(id);

            if (asset == null)
                return null;

            if (!m_AssetPerms.AllowedExport(asset.Type))
                return null;

            return asset.Data;
        }

        //public virtual bool Get(string id, Object sender, AssetRetrieved handler)

        public override string Store(AssetBase asset)
        {
            if (!m_AssetPerms.AllowedImport(asset.Type))
                return string.Empty;

            return base.Store(asset);
        }

        public override bool Delete(string id)
        {
            // NOGO
            return false;
        }

        #endregion 

        protected void AdjustIdentifiers(AssetMetadata meta)
        {
            if (meta == null || m_Cache == null)
                return;

            UserAccount creator = m_Cache.GetUser(meta.CreatorID);
            if (creator != null)
                meta.CreatorID = meta.CreatorID + ";" + m_HomeURL + "/" + creator.FirstName + " " + creator.LastName;
        }

        protected byte[] AdjustIdentifiers(byte[] data)
        {
            string xml = Utils.BytesToString(data);
            return Utils.StringToBytes(ExternalRepresentationUtils.RewriteSOP(xml, m_HomeURL, m_Cache, UUID.Zero));
        }

    }

}
