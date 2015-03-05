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
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using Mono.Addins;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GetMeshModule")]
    public class GetMeshModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private Scene m_scene;
        private IAssetService m_AssetService;
        private bool m_Enabled = true;
        private string m_URL;
        private string m_URL2;
        private string m_RedirectURL = null;
        private string m_RedirectURL2 = null;

        #region Region Module interfaceBase Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];
            if (config == null)
                return;

            m_URL = config.GetString("Cap_GetMesh", string.Empty);
            // Cap doesn't exist
            if (m_URL != string.Empty)
            {
                m_Enabled = true;
                m_RedirectURL = config.GetString("GetMeshRedirectURL");
            }

            m_URL2 = config.GetString("Cap_GetMesh2", string.Empty);
            // Cap doesn't exist
            if (m_URL2 != string.Empty)
            {
                m_Enabled = true;
                m_RedirectURL2 = config.GetString("GetMesh2RedirectURL");
            }
        }

        public void AddRegion(Scene pScene)
        {
            if (!m_Enabled)
                return;

            m_scene = pScene;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_AssetService = m_scene.RequestModuleInterface<IAssetService>();
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }


        public void Close() { }

        public string Name { get { return "GetMeshModule"; } }

        #endregion


        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capID = UUID.Random();
            bool getMeshRegistered = false;

            if (m_URL == string.Empty)
            {

            }
            else if (m_URL == "localhost")
            {
                getMeshRegistered = true;
                caps.RegisterHandler(
                    "GetMesh",
                    new GetMeshHandler("/CAPS/" + capID + "/", m_AssetService, "GetMesh", agentID.ToString(), m_RedirectURL));
            }
            else
            {
                caps.RegisterHandler("GetMesh", m_URL);
            }

            if(m_URL2 == string.Empty)
            {

            }
            else if (m_URL2 == "localhost")
            {
                if (!getMeshRegistered)
                {
                    caps.RegisterHandler(
                        "GetMesh2",
                        new GetMeshHandler("/CAPS/" + capID + "/", m_AssetService, "GetMesh2", agentID.ToString(), m_RedirectURL2));
                }
            }
            else
            {
                caps.RegisterHandler("GetMesh2", m_URL2);
            }
        }

    }
}
