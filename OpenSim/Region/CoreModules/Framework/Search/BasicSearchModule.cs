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
using System.IO;
using System.Reflection;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;

using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using Nini.Config;
using Mono.Addins;

using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;

namespace OpenSim.Region.CoreModules.Framework.Search
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BasicSearchModule")]
    public class BasicSearchModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled;
        protected List<Scene> m_Scenes = new List<Scene>();

        private IGroupsModule m_GroupsService = null;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            string umanmod = config.Configs["Modules"].GetString("SearchModule", Name);
            if (umanmod == Name)
            {
                m_Enabled = true;
                m_log.DebugFormat("[BASIC SEARCH MODULE]: {0} is enabled", Name);
            }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public virtual string Name
        {
            get { return "BasicSearchModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_Scenes.Add(scene);

                scene.EventManager.OnMakeRootAgent += new Action<ScenePresence>(EventManager_OnMakeRootAgent);
                scene.EventManager.OnMakeChildAgent += new EventManager.OnMakeChildAgentDelegate(EventManager_OnMakeChildAgent);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_Scenes.Remove(scene);

                scene.EventManager.OnMakeRootAgent -= new Action<ScenePresence>(EventManager_OnMakeRootAgent);
                scene.EventManager.OnMakeChildAgent -= new EventManager.OnMakeChildAgentDelegate(EventManager_OnMakeChildAgent);
            }
        }

        public void RegionLoaded(Scene s)
        {
            if (!m_Enabled)
                return;

            if (m_GroupsService == null)
            {
                m_GroupsService = s.RequestModuleInterface<IGroupsModule>();

                // No Groups Service Connector, then group search won't work...
                if (m_GroupsService == null)
                    m_log.Warn("[BASIC SEARCH MODULE]: Could not get IGroupsModule");
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_Scenes.Clear();
        }

        #endregion ISharedRegionModule


        #region Event Handlers

        void EventManager_OnMakeRootAgent(ScenePresence sp)
        {
            sp.ControllingClient.OnDirFindQuery += OnDirFindQuery;
        }

        void EventManager_OnMakeChildAgent(ScenePresence sp)
        {
            sp.ControllingClient.OnDirFindQuery -= OnDirFindQuery;
        }

        void OnDirFindQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart)
        {
            queryText = queryText.Trim();

            if (((DirFindFlags)queryFlags & DirFindFlags.People) == DirFindFlags.People)
            {
                if (string.IsNullOrEmpty(queryText))
                    remoteClient.SendDirPeopleReply(queryID, new DirPeopleReplyData[0]);

                List<UserAccount> accounts = m_Scenes[0].UserAccountService.GetUserAccounts(m_Scenes[0].RegionInfo.ScopeID, queryText);
                DirPeopleReplyData[] hits = new DirPeopleReplyData[accounts.Count];
                int i = 0;
                foreach (UserAccount acc in accounts)
                {
                    DirPeopleReplyData d = new DirPeopleReplyData();
                    d.agentID = acc.PrincipalID;
                    d.firstName = acc.FirstName;
                    d.lastName = acc.LastName;
                    d.online = false;

                    hits[i++] = d;
                }

                // TODO: This currently ignores pretty much all the query flags including Mature and sort order
                remoteClient.SendDirPeopleReply(queryID, hits);
            }
            else if (((DirFindFlags)queryFlags & DirFindFlags.Groups) == DirFindFlags.Groups)
            {
                if (m_GroupsService == null)
                {
                    m_log.Warn("[BASIC SEARCH MODULE]: Groups service is not available. Unable to search groups.");
                    remoteClient.SendAlertMessage("Groups search is not enabled");
                    return;
                }

                if (string.IsNullOrEmpty(queryText))
                    remoteClient.SendDirGroupsReply(queryID, new DirGroupsReplyData[0]);

                // TODO: This currently ignores pretty much all the query flags including Mature and sort order
                remoteClient.SendDirGroupsReply(queryID, m_GroupsService.FindGroups(remoteClient, queryText).ToArray());
            }

        }

        #endregion Event Handlers

    }

}
