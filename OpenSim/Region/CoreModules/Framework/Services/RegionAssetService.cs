/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 *     * Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice,
 *       this list of conditions and the following disclaimer in the documentation
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from
 *       this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.Services
{
    public class RegionAssetService : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;

        Scene m_scene;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for standalones in hypergrid mode
                enabled = !config.Configs["Startup"].GetBoolean("gridmode", true) && config.Configs["Startup"].GetBoolean("hypergrid", false);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[RegionAssetService]: Starting...");

                new AssetService(m_scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "RegionAssetService"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

    }

    public class AssetService
    {
        private IUserService m_userService;
        private bool m_doLookup = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AssetService(Scene m_scene)
        {
            AddHttpHandlers(m_scene);
            m_userService = m_scene.CommsManager.UserService;
        }

        protected void AddHttpHandlers(Scene m_scene)
        {
            IAssetDataPlugin m_assetProvider 
                = ((AssetServerBase)m_scene.CommsManager.AssetCache.AssetServer).AssetProviderPlugin;

            IHttpServer httpServer = m_scene.CommsManager.HttpServer;
            httpServer.AddStreamHandler(new GetAssetStreamHandler(m_assetProvider));
            httpServer.AddStreamHandler(new PostAssetStreamHandler(m_assetProvider));

        }
    }
}
