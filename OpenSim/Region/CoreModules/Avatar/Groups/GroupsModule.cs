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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Groups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupsModule")]
    public class GroupsModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, GroupMembershipData> m_GroupMap =
                new Dictionary<UUID, GroupMembershipData>();

        private Dictionary<UUID, IClientAPI> m_ClientMap =
                new Dictionary<UUID, IClientAPI>();

        private UUID opensimulatorGroupID =
                new UUID("00000000-68f9-1111-024e-222222111123");

        private List<Scene> m_SceneList = new List<Scene>();

        private static GroupMembershipData osGroup =
                new GroupMembershipData();

        private bool m_Enabled = false;

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            if (groupsConfig == null)
            {
                m_log.Info("[GROUPS]: No configuration found. Using defaults");
            }
            else
            {
                m_Enabled = groupsConfig.GetBoolean("Enabled", false);
                if (!m_Enabled)
                {
                    m_log.Info("[GROUPS]: Groups disabled in configuration");
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != "Default")
                {
                    m_Enabled = false;
                    return;
                }
            }

        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_SceneList)
            {
                if (!m_SceneList.Contains(scene))
                {
                    if (m_SceneList.Count == 0)
                    {
                        osGroup.GroupID = opensimulatorGroupID;
                        osGroup.GroupName = "OpenSimulator Testing";
                        osGroup.GroupPowers =
                                (uint)(GroupPowers.AllowLandmark |
                                       GroupPowers.AllowSetHome);
                        m_GroupMap[opensimulatorGroupID] = osGroup;
                    }
                    m_SceneList.Add(scene);
                }
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            //            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_SceneList)
            {
                if (m_SceneList.Contains(scene))
                    m_SceneList.Remove(scene);
            }

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClientClosed -= OnClientClosed;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (!m_Enabled)
                return;

//            m_log.Debug("[GROUPS]: Shutting down group module.");

            lock (m_ClientMap)
            {
                m_ClientMap.Clear();
            }

            lock (m_GroupMap)
            {
                m_GroupMap.Clear();
            }
        }

        public string Name
        {
            get { return "GroupsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            // Subscribe to instant messages
//            client.OnInstantMessage += OnInstantMessage;
            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            client.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
            lock (m_ClientMap)
            {
                if (!m_ClientMap.ContainsKey(client.AgentId))
                {
                    m_ClientMap.Add(client.AgentId, client);
                }
            }
        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient,
                                              UUID AgentID, UUID SessionID)
        {
            UUID ActiveGroupID;
            string ActiveGroupName;
            ulong ActiveGroupPowers;

            string firstname = remoteClient.FirstName;
            string lastname = remoteClient.LastName;

            string ActiveGroupTitle = "I IZ N0T";

            ActiveGroupID = osGroup.GroupID;
            ActiveGroupName = osGroup.GroupName;
            ActiveGroupPowers = osGroup.GroupPowers;

            remoteClient.SendAgentDataUpdate(AgentID, ActiveGroupID, firstname,
                                             lastname, ActiveGroupPowers, ActiveGroupName,
                                             ActiveGroupTitle);
        }

//        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
//        {
//        }

//        private void OnGridInstantMessage(GridInstantMessage msg)
//        {
//            // Trigger the above event handler
//            OnInstantMessage(null, msg);
//        }

        private void HandleUUIDGroupNameRequest(UUID id,IClientAPI remote_client)
        {
            string groupnamereply = "Unknown";
            UUID groupUUID = UUID.Zero;

            lock (m_GroupMap)
            {
                if (m_GroupMap.ContainsKey(id))
                {
                    GroupMembershipData grp = m_GroupMap[id];
                    groupnamereply = grp.GroupName;
                    groupUUID = grp.GroupID;
                }
            }
            remote_client.SendGroupNameReply(groupUUID, groupnamereply);
        }

        public GroupMembershipData[] GetMembershipData(UUID agentID)
        {
            GroupMembershipData[] updateGroups = new GroupMembershipData[1];
            updateGroups[0] = osGroup;
            return updateGroups;
        }

        public GroupMembershipData GetActiveMembershipData(UUID agentID)
        {
            return osGroup;
        }

        private void OnClientClosed(UUID agentID, Scene scene)
        {
            lock (m_ClientMap)
            {
                if (m_ClientMap.ContainsKey(agentID))
                {
//                    IClientAPI cli = m_ClientMap[agentID];
//                    if (cli != null)
//                    {
//                        //m_log.Info("[GROUPS]: Removing all reference to groups for " + cli.Name);
//                    }
//                    else
//                    {
//                        //m_log.Info("[GROUPS]: Removing all reference to groups for " + agentID.ToString());
//                    }
                    m_ClientMap.Remove(agentID);
                }
            }
        }
    }
}
