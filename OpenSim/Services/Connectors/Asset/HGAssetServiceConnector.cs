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

using log4net;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.Connectors
{
    public class HGAssetServiceConnector : IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, AssetServicesConnector> m_connectors = new Dictionary<string, AssetServicesConnector>();

        public HGAssetServiceConnector(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                // string name = moduleConfig.GetString("AssetServices", "");

                IConfig assetConfig = source.Configs["AssetService"];
                if (assetConfig == null)
                {
                    m_log.Error("[HG ASSET SERVICE]: AssetService missing from OpenSim.ini");
                    return;
                }

                m_log.Info("[HG ASSET SERVICE]: HG asset service enabled");
            }
        }

        private bool StringToUrlAndAssetID(string id, out string url, out string assetID)
        {
            url = String.Empty;
            assetID = String.Empty;

            Uri assetUri;

            if (Uri.TryCreate(id, UriKind.Absolute, out assetUri) &&
                    assetUri.Scheme == Uri.UriSchemeHttp)
            {
                url = "http://" + assetUri.Authority;
                assetID = assetUri.LocalPath.Trim(new char[] {'/'});
                return true;
            }

            return false;
        }

        private IAssetService GetConnector(string url)
        {
            AssetServicesConnector connector = null;
            lock (m_connectors)
            {
                if (m_connectors.ContainsKey(url))
                {
                    connector = m_connectors[url];
                }
                else
                {
                    // We're instantiating this class explicitly, but this won't
                    // work in general, because the remote grid may be running
                    // an asset server that has a different protocol.
                    // Eventually we will want a piece of protocol asking
                    // the remote server about its kind. Definitely cool thing to do!
                    connector = new AssetServicesConnector(url);
                    m_connectors.Add(url, connector);
                }
            }
            return connector;
        }

        public AssetBase Get(string id)
        {
            string url = string.Empty;
            string assetID = string.Empty;

            if (StringToUrlAndAssetID(id, out url, out assetID))
            {
                IAssetService connector = GetConnector(url);
                return connector.Get(assetID);
            }

            return null;
        }

        public AssetBase GetCached(string id)
        {
            string url = string.Empty;
            string assetID = string.Empty;

            if (StringToUrlAndAssetID(id, out url, out assetID))
            {
                IAssetService connector = GetConnector(url);
                return connector.GetCached(assetID);
            }

            return null;
        }

        public AssetMetadata GetMetadata(string id)
        {
            string url = string.Empty;
            string assetID = string.Empty;

            if (StringToUrlAndAssetID(id, out url, out assetID))
            {
                IAssetService connector = GetConnector(url);
                return connector.GetMetadata(assetID);
            }

            return null;
        }

        public byte[] GetData(string id)
        {
            return null;
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            string url = string.Empty;
            string assetID = string.Empty;

            if (StringToUrlAndAssetID(id, out url, out assetID))
            {
                IAssetService connector = GetConnector(url);
                return connector.Get(assetID, sender, handler);
            }

            return false;
        }

        public string Store(AssetBase asset)
        {
            string url = string.Empty;
            string assetID = string.Empty;

            if (StringToUrlAndAssetID(asset.ID, out url, out assetID))
            {
                IAssetService connector = GetConnector(url);
                // Restore the assetID to a simple UUID
                asset.ID = assetID;
                return connector.Store(asset);
            }

            return String.Empty;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public bool Delete(string id)
        {
            return false;
        }
    }
}
