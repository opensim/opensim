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

using log4net;
using System;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.CoreModules.ServiceConnectors.Asset
{
    public class RemoteAssetServicesConnector :
            ISharedRegionModule, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private string m_ServerURI = String.Empty;

        public string Name
        {
            get { return "RemoteAssetServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");
                if (name == Name)
                {
                    IConfig assetConfig = source.Configs["AssetService"];
                    if (assetConfig == null)
                    {
                        m_log.Error("[ASSET CONNECTOR]: AssetService missing from OpanSim.ini");
                        return;
                    }

                    string serviceURI = assetConfig.GetString("AssetServerURI",
                            String.Empty);

                    if (serviceURI == String.Empty)
                    {
                        m_log.Error("[ASSET CONNECTOR]: No Server URI named in section AssetService");
                        return;
                    }
                    m_Enabled = true;
                    m_ServerURI = serviceURI;

                    m_log.Info("[ASSET CONNECTOR]: Remote assets enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IAssetService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public AssetBase Get(string id)
        {
            string uri = m_ServerURI + "/assets/" + id;

            AssetBase asset = SynchronousRestObjectPoster.
                    BeginPostObject<int, AssetBase>("GET", uri, 0);
            return asset;
        }

        public AssetMetadata GetMetadata(string id)
        {
            string uri = m_ServerURI + "/assets/" + id + "/metadata";

            AssetMetadata asset = SynchronousRestObjectPoster.
                    BeginPostObject<int, AssetMetadata>("GET", uri, 0);
            return asset;
        }

        public byte[] GetData(string id)
        {
            RestClient rc = new RestClient(m_ServerURI);
            rc.AddResourcePath("assets");
            rc.AddResourcePath(id);
            rc.AddResourcePath("data");

            rc.RequestMethod = "GET";

            Stream s = rc.Request();

            if (s == null)
                return null;

            if (s.Length > 0)
            {
                byte[] ret = new byte[s.Length];
                s.Read(ret, 0, (int)s.Length);

                return ret;
            }

            return null;
        }

        public string Store(AssetBase asset)
        {
            string uri = m_ServerURI + "/assets/";

            string newID = SynchronousRestObjectPoster.
                    BeginPostObject<AssetBase, string>("POST", uri, asset);
            return newID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = new AssetBase();
            asset.ID = id;
            asset.Data = data;

            string uri = m_ServerURI + "/assets/" + id;

            return SynchronousRestObjectPoster.
                    BeginPostObject<AssetBase, bool>("POST", uri, asset);
        }

        public bool Delete(string id)
        {
            string uri = m_ServerURI + "/assets/" + id;

            return SynchronousRestObjectPoster.
                    BeginPostObject<int, bool>("DELETE", uri, 0);
            return false;
        }
    }
}
