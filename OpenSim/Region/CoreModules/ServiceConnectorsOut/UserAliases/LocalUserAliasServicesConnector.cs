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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAliases
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalUserAliasServicesConnector")]
    public class LocalUserAliasServicesConnector : ISharedRegionModule, IUserAliasService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public IUserAliasService UserAliasService { get; private set; }

        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalUserAliasServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("UserAliasServices", "");
                if (name == Name)
                {
                    IConfig userConfig = source.Configs["UserAliasService"];
                    if (userConfig == null)
                    {
                        m_log.Error("[LOCAL USER ALIAS SERVICE CONNECTOR]: UserAliasService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = userConfig.GetString("LocalServiceModule", String.Empty);

                    if (serviceDll.Length == 0)
                    {
                        m_log.Error("[LOCAL USER ALIAS SERVICE CONNECTOR]: No LocalServiceModule named in section UserAliasService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    UserAliasService = ServerUtils.LoadPlugin<IUserAliasService>(serviceDll, args);

                    if (UserAliasService == null)
                    {
                        m_log.ErrorFormat(
                            "[USER ALIAS SERVICE CONNECTOR]: Cannot load user account alias specified as {0}", serviceDll);
                        return;
                    }

                    m_Enabled = true;

                    m_log.Info("[LOCAL USER ACCOUNT SERVICE CONNECTOR]: Local user connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;
        }

        public void Close()
        {
            if (!m_Enabled)
                return;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IUserAliasService>(UserAliasService);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[LOCAL USER ALIAS SERVICE CONNECTOR]: Enabled local user aliases for region {0}", scene.RegionInfo.RegionName);
        }

        #endregion

        #region IUserAliasService

        public UserAlias GetUserForAlias(UUID aliasID)
        {
            var alias = UserAliasService.GetUserForAlias(aliasID);
            return alias;
        }

        public List<UserAlias> GetUserAliases(UUID userID)
        {
            var userAliases = UserAliasService.GetUserAliases(userID);
            return userAliases;
        }

        public UserAlias CreateAlias(UUID AliasID, UUID UserID, string Description)
        {
            var useralias = UserAliasService.CreateAlias(AliasID, UserID, Description); 
            return useralias;
        }

        public bool DeleteAlias(UUID aliasID)
        {
            var result =  UserAliasService.DeleteAlias(aliasID);
            return result;
        }

        #endregion
    }
}
