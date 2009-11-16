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
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.Hypergrid
{
    public class HGStandaloneLoginModule : IRegionModule, ILoginServiceToRegionsConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected List<Scene> m_scenes = new List<Scene>();
        protected Scene m_firstScene;

        protected bool m_enabled = false; // Module is only enabled if running in standalone mode

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
       
        protected HGLoginAuthService m_loginService;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_firstScene == null)
            {
                m_firstScene = scene;

                IConfig startupConfig = source.Configs["Startup"];
                if (startupConfig != null)
                {
                    m_enabled = !startupConfig.GetBoolean("gridmode", false);
                }

                if (m_enabled)
                {
                    m_log.Debug("[HGLogin]: HGlogin module enabled");
                    bool authenticate = true;
                    string welcomeMessage = "Welcome to OpenSim";
                    IConfig standaloneConfig = source.Configs["StandAlone"];
                    if (standaloneConfig != null)
                    {
                        authenticate = standaloneConfig.GetBoolean("accounts_authenticate", true);
                        welcomeMessage = standaloneConfig.GetString("welcome_message");
                    }

                    //TODO: fix casting.
                    LibraryRootFolder rootFolder = m_firstScene.CommsManager.UserProfileCacheService.LibraryRoot as LibraryRootFolder;
                   
                    IHttpServer httpServer = MainServer.Instance;

                    //TODO: fix the casting of the user service, maybe by registering the userManagerBase with scenes, or refactoring so we just need a IUserService reference
                    m_loginService 
                        = new HGLoginAuthService(
                            (UserManagerBase)m_firstScene.CommsManager.UserAdminService, 
                            welcomeMessage, 
                            m_firstScene.CommsManager.InterServiceInventoryService, 
                            m_firstScene.CommsManager.NetworkServersInfo, 
                            authenticate, 
                            rootFolder, 
                            this);

                    httpServer.AddXmlRPCHandler("hg_login", m_loginService.XmlRpcLoginMethod);
                    httpServer.AddXmlRPCHandler("check_auth_session", m_loginService.XmlRPCCheckAuthSession, false);
                    httpServer.AddXmlRPCHandler("get_avatar_appearance", XmlRPCGetAvatarAppearance);
                    httpServer.AddXmlRPCHandler("update_avatar_appearance", XmlRPCUpdateAvatarAppearance);

                }
            }

            if (m_enabled)
            {
                AddScene(scene);
            }
        }

        public void PostInitialise()
        {

        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "HGStandaloneLoginModule"; }
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

        public bool NewUserConnection(ulong regionHandle, AgentCircuitData agent, out string reason)
        {
            reason = String.Empty;
            return true;
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

        public XmlRpcResponse XmlRPCGetAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            AvatarAppearance appearance;
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                appearance = m_firstScene.CommsManager.AvatarService.GetUserAppearance(new UUID((string)requestData["owner"]));
                if (appearance == null)
                {
                    responseData = new Hashtable();
                    responseData["error_type"] = "no appearance";
                    responseData["error_desc"] = "There was no appearance found for this avatar";
                }
                else
                {
                    responseData = appearance.ToHashTable();
                }
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }

            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCUpdateAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                AvatarAppearance appearance = new AvatarAppearance(requestData);
                
                // TODO: Sometime in the future we may have a database layer that is capable of updating appearance when
                // the TextureEntry is null. When that happens, this check can be removed
                if (appearance.Texture != null)
                    m_firstScene.CommsManager.AvatarService.UpdateUserAppearance(new UUID((string)requestData["owner"]), appearance);

                responseData = new Hashtable();
                responseData["returnString"] = "TRUE";
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }
            response.Value = responseData;
            return response;
        }
    }

}
