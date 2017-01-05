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
using Mono.Addins;
using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Authorization
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteAuthorizationServicesConnector")]
    public class RemoteAuthorizationServicesConnector :
            AuthorizationServicesConnector, ISharedRegionModule, IAuthorizationService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private List<Scene> m_scenes = new List<Scene>();

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteAuthorizationServicesConnector"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AuthorizationServices", "");
                if (name == Name)
                {
                    IConfig authorizationConfig = source.Configs["AuthorizationService"];
                    if (authorizationConfig == null)
                    {
                        m_log.Info("[REMOTE AUTHORIZATION CONNECTOR]: AuthorizationService missing from OpenSim.ini");
                        return;
                    }

                    m_Enabled = true;

                    base.Initialise(source);

                    m_log.Info("[REMOTE AUTHORIZATION CONNECTOR]: Remote authorization enabled");
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

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                scene.RegisterModuleInterface<IAuthorizationService>(this);
            }

        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[REMOTE AUTHORIZATION CONNECTOR]: Enabled remote authorization for region {0}", scene.RegionInfo.RegionName);

        }

        public bool IsAuthorizedForRegion(
             string userID, string firstName, string lastName, string regionID, out string message)
        {
            m_log.InfoFormat(
                "[REMOTE AUTHORIZATION CONNECTOR]: IsAuthorizedForRegion checking {0} for region {1}", userID, regionID);

            bool isAuthorized = true;
            message = String.Empty;

            // get the scene this call is being made for
            Scene scene = null;
            lock (m_scenes)
            {
                foreach (Scene nextScene in m_scenes)
                {
                    if (nextScene.RegionInfo.RegionID.ToString() == regionID)
                    {
                        scene = nextScene;
                    }
                }
            }

            if (scene != null)
            {
                string mail = String.Empty;

                UserAccount account = scene.UserAccountService.GetUserAccount(UUID.Zero, new UUID(userID));

                //if account not found, we assume its a foreign visitor from HG, else use account data...
                if (account != null)
                {
                    mail = account.Email;
                    firstName = account.FirstName;
                    lastName = account.LastName;
                }

                isAuthorized
                    = IsAuthorizedForRegion(
                        userID, firstName, lastName, mail, scene.RegionInfo.RegionName, regionID, out message);
            }
            else
            {
                m_log.ErrorFormat(
                    "[REMOTE AUTHORIZATION CONNECTOR] IsAuthorizedForRegion, can't find scene to match region id of {0}",
                    regionID);
            }

            return isAuthorized;
        }
    }
}