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
using Nini.Config;
using log4net;
using Mono.Addins;
using System.Reflection;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;
using OpenSim.Framework;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAccounts
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteUserAccountServicesConnector")]
    public class RemoteUserAccountServicesConnector : UserAccountServicesConnector,
            ISharedRegionModule, IUserAccountService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private UserAccountCache m_Cache;

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteUserAccountServicesConnector"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("UserAccountServices", "");
                if (name == Name)
                {
                    IConfig userConfig = source.Configs["UserAccountService"];
                    if (userConfig == null)
                    {
                        m_log.Error("[USER CONNECTOR]: UserAccountService missing from OpenSim.ini");
                        return;
                    }

                    m_Enabled = true;

                    base.Initialise(source);
                    m_Cache = new UserAccountCache();

                    m_log.Info("[USER CONNECTOR]: Remote users enabled");
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

            scene.RegisterModuleInterface<IUserAccountService>(this);
            scene.RegisterModuleInterface<IUserAccountCacheModule>(m_Cache);

            scene.EventManager.OnNewClient += OnNewClient;
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
        }

        // When a user actually enters the sim, clear them from
        // cache so the sim will have the current values for
        // flags, title, etc. And country, don't forget country!
        private void OnNewClient(IClientAPI client)
        {
            m_Cache.Remove(client.Name);
        }

        #region Overwritten methods from IUserAccountService

        public override UserAccount GetUserAccount(UUID scopeID, UUID userID)
        {
            bool inCache = false;
            UserAccount account = m_Cache.Get(userID, out inCache);
            if (inCache)
                return account;

            account = base.GetUserAccount(scopeID, userID);
            m_Cache.Cache(userID, account);

            return account;
        }

        public override UserAccount GetUserAccount(UUID scopeID, string firstName, string lastName)
        {
            bool inCache = false;
            UserAccount account = m_Cache.Get(firstName + " " + lastName, out inCache);
            if (inCache)
                return account;

            account = base.GetUserAccount(scopeID, firstName, lastName);
            if (account != null)
                m_Cache.Cache(account.PrincipalID, account);

            return account;
        }

        public override bool StoreUserAccount(UserAccount data)
        {
            // This remote connector refuses to serve this method
            return false;
        }

        #endregion
    }
}
