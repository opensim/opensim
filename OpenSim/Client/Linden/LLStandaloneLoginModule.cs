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
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Client.Linden
{
    public class LLStandaloneLoginModule : ISharedRegionModule, ILoginServiceToRegionsConnector
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_scenes = new List<Scene>();
        protected Scene m_firstScene;

        protected bool m_enabled = false; // Module is only enabled if running in standalone mode

        protected bool authenticate;
        protected string welcomeMessage;

        public bool RegionLoginsEnabled
        {
            get
            {
                if (m_firstScene != null)
                {
                    return m_firstScene.SceneGridService.RegionLoginsEnabled;
                }
                else
                {
                    return false;
                }
            }
        }

        protected LLStandaloneLoginService m_loginService;

        #region IRegionModule Members

        public void Initialise(IConfigSource source)
        {
            IConfig startupConfig = source.Configs["Startup"];
            if (startupConfig != null)
            {
                m_enabled = !startupConfig.GetBoolean("gridmode", false);
            }

            if (m_enabled)
            {
                authenticate = true;
                welcomeMessage = "Welcome to OpenSim";
                IConfig standaloneConfig = source.Configs["StandAlone"];
                if (standaloneConfig != null)
                {
                    authenticate = standaloneConfig.GetBoolean("accounts_authenticate", true);
                    welcomeMessage = standaloneConfig.GetString("welcome_message");
                }
            }
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_enabled)
            {
                RemoveScene(scene);
            }
        }

        public void PostInitialise()
        {

        }

        public void Close()
        {

        }

        public void RegionLoaded(Scene scene)
        {
            if (m_firstScene == null)
            {
                m_firstScene = scene;

                if (m_enabled)
                {
                    //TODO: fix casting.
                    LibraryRootFolder rootFolder
                        = m_firstScene.CommsManager.UserProfileCacheService.LibraryRoot as LibraryRootFolder;

                    IHttpServer httpServer = MainServer.Instance;

                    //TODO: fix the casting of the user service, maybe by registering the userManagerBase with scenes, or refactoring so we just need a IUserService reference
                    m_loginService 
                        = new LLStandaloneLoginService(
                            (UserManagerBase)m_firstScene.CommsManager.UserAdminService, welcomeMessage, 
                            m_firstScene.InventoryService, m_firstScene.CommsManager.NetworkServersInfo, authenticate, 
                            rootFolder, this);

                    httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);

                    // provides the web form login
                    httpServer.AddHTTPHandler("login", m_loginService.ProcessHTMLLogin);

                    // Provides the LLSD login
                    httpServer.SetDefaultLLSDHandler(m_loginService.LLSDLoginMethod);
                }
            }

            if (m_enabled)
            {
                AddScene(scene);
            }
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LLStandaloneLoginModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        protected void AddScene(Scene scene)
        {
            lock (m_scenes)
            {
                if (!m_scenes.Contains(scene))
                {
                    m_scenes.Add(scene);
                }
            }
        }

        protected void RemoveScene(Scene scene)
        {
            lock (m_scenes)
            {
                if (m_scenes.Contains(scene))
                {
                    m_scenes.Remove(scene);
                }
            }
        }

        public bool NewUserConnection(ulong regionHandle, AgentCircuitData agent, out string reason)
        {
            Scene scene;
            if (TryGetRegion(regionHandle, out scene))
            {
                return scene.NewUserConnection(agent, out reason);
            }
            reason = "Region not found.";
            return false;
        }

        public void LogOffUserFromGrid(ulong regionHandle, UUID AvatarID, UUID RegionSecret, string message)
        {
            Scene scene;
            if (TryGetRegion(regionHandle, out scene))
            {
                 scene.HandleLogOffUserFromGrid(AvatarID, RegionSecret, message);
            }
        }

        public RegionInfo RequestNeighbourInfo(ulong regionhandle)
        {
            Scene scene;
            if (TryGetRegion(regionhandle, out scene))
            {
                return scene.RegionInfo;
            }
            return null;
        }

        public RegionInfo RequestClosestRegion(string region)
        {
            Scene scene;
            if (TryGetRegion(region, out scene))
            {
                return scene.RegionInfo;
            }
            else if (m_scenes.Count > 0)
            {
                return m_scenes[0].RegionInfo;
            }
            return null;
        }

        public RegionInfo RequestNeighbourInfo(UUID regionID)
        {
            Scene scene;
            if (TryGetRegion(regionID, out scene))
            {
                return scene.RegionInfo;
            }
            return null;
        }

        protected bool TryGetRegion(ulong regionHandle, out Scene scene)
        {
            lock (m_scenes)
            {
                foreach (Scene nextScene in m_scenes)
                {
                    if (nextScene.RegionInfo.RegionHandle == regionHandle)
                    {
                        scene = nextScene;
                        return true;
                    }
                }
            }

            scene = null;
            return false;
        }

        protected bool TryGetRegion(UUID regionID, out Scene scene)
        {
            lock (m_scenes)
            {
                foreach (Scene nextScene in m_scenes)
                {
                    if (nextScene.RegionInfo.RegionID == regionID)
                    {
                        scene = nextScene;
                        return true;
                    }
                }
            }

            scene = null;
            return false;
        }

        protected bool TryGetRegion(string regionName, out Scene scene)
        {
            lock (m_scenes)
            {
                foreach (Scene nextScene in m_scenes)
                {
                    if (nextScene.RegionInfo.RegionName.Equals(regionName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        scene = nextScene;
                        return true;
                    }
                }
            }

            scene = null;
            return false;
        }
    }
}
