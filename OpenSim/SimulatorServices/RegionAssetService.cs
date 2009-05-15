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

using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Servers.AssetServer.Handlers;

namespace OpenSim.Region.SimulatorServices
{
    public class RegionAssetService : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;
        
        private bool m_gridMode = false;
        Scene m_scene;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for standalones in hypergrid mode
                enabled = ((!config.Configs["Startup"].GetBoolean("gridmode", true)) &&
                    config.Configs["Startup"].GetBoolean("hypergrid", true)) ||
                    ((config.Configs["MXP"] != null) && config.Configs["MXP"].GetBoolean("Enabled", true));
                m_gridMode = config.Configs["Startup"].GetBoolean("gridmode", true);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[RegionAssetService]: Starting...");

                new AssetService(m_scene,m_gridMode);
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
        private bool m_doLookup = false;
        private bool m_gridMode = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
//        private static readonly ILog m_log
//            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AssetService(Scene m_scene, bool gridMode)
        {
            m_gridMode = gridMode;
            AddHttpHandlers(m_scene);
        }

        protected void AddHttpHandlers(Scene m_scene)
        {
            IHttpServer httpServer = m_scene.CommsManager.HttpServer;
            
            httpServer.AddStreamHandler(new AssetServerGetHandler(m_scene.AssetService));
            httpServer.AddStreamHandler(new AssetServerPostHandler(m_scene.AssetService));
            httpServer.AddStreamHandler(new AssetServerDeleteHandler(m_scene.AssetService));


        }
    }
}
