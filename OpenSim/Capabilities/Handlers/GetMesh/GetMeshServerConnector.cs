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

using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using System;

namespace OpenSim.Capabilities.Handlers
{
    public class GetMeshServerConnector : ServiceConnector
    {
        private IAssetService m_AssetService;
        private string m_ConfigName = "CapsService";

        public GetMeshServerConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            if (configName != String.Empty)
                m_ConfigName = configName;

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string assetService = serverConfig.GetString("AssetService", String.Empty);

            if (assetService == String.Empty)
                throw new Exception("No AssetService in config file");

            Object[] args = new Object[] { config };
            m_AssetService =
                    ServerUtils.LoadPlugin<IAssetService>(assetService, args);

            if (m_AssetService == null)
                throw new Exception(String.Format("Failed to load AssetService from {0}; config is {1}", assetService, m_ConfigName));

            string rurl = serverConfig.GetString("GetMeshRedirectURL");

            server.AddStreamHandler(
                new GetTextureHandler("/CAPS/GetMesh/" /*+ UUID.Random() */, m_AssetService, "GetMesh", null, rurl));

            rurl = serverConfig.GetString("GetMesh2RedirectURL");

            server.AddStreamHandler(
                new GetTextureHandler("/CAPS/GetMesh2/" /*+ UUID.Random() */, m_AssetService, "GetMesh2", null, rurl));
        }
    }
}