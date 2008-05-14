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

using System;
using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Groups
{
    public class GroupsModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<LLUUID, GroupList> m_grouplistmap = new Dictionary<LLUUID, GroupList>();
        private Dictionary<LLUUID, GroupData> m_groupmap = new Dictionary<LLUUID, GroupData>();
        private Dictionary<LLUUID, IClientAPI> m_iclientmap = new Dictionary<LLUUID, IClientAPI>();
        private List<Scene> m_scene = new List<Scene>();

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_scene)
            {
                m_scene.Add(scene);
            }
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnGridInstantMessageToGroupsModule += OnGridInstantMessage;
            //scene.EventManager.
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_log.Info("[GROUP]: Shutting down group module.");
            lock (m_iclientmap)
            {
                m_iclientmap.Clear();
            }

            lock (m_groupmap)
            {
                m_groupmap.Clear();
            }

            lock (m_grouplistmap)
            {
                m_grouplistmap.Clear();
            }
            GC.Collect();
        }

        public string Name
        {
            get { return "GroupsModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {

            // Subscribe to instant messages
            client.OnInstantMessage += OnInstantMessage;
            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            lock (m_iclientmap)
            {
                if (!m_iclientmap.ContainsKey(client.AgentId))
                {
                    m_iclientmap.Add(client.AgentId, client);
                }
            }
            GroupData OpenSimulatorGroup = new GroupData();
            OpenSimulatorGroup.ActiveGroupTitle = "OpenSimulator Tester";
            OpenSimulatorGroup.GroupID = new LLUUID("00000000-68f9-1111-024e-222222111120");
            OpenSimulatorGroup.GroupMembers.Add(client.AgentId);
            OpenSimulatorGroup.groupName = "OpenSimulator Testing";
            OpenSimulatorGroup.ActiveGroupPowers = GroupPowers.LandAllowSetHome;
            OpenSimulatorGroup.GroupTitles.Add("OpenSimulator Tester");
            lock (m_groupmap)
            {
                if (!m_groupmap.ContainsKey(client.AgentId))
                {
                    m_groupmap.Add(client.AgentId, OpenSimulatorGroup);
                }
            }
            GroupList testGroupList = new GroupList();
            testGroupList.m_GroupList.Add(new LLUUID("00000000-68f9-1111-024e-222222111120"));

            lock (m_grouplistmap)
            {
                if (!m_grouplistmap.ContainsKey(client.AgentId))
                {
                    m_grouplistmap.Add(client.AgentId, testGroupList);
                }
            }
            m_log.Info("[GROUP]: Adding " + client.Name + " to OpenSimulator Tester group");
            GroupData[] updateGroups = new GroupData[1];
            updateGroups[0] = OpenSimulatorGroup;

            client.SendGroupMembership(updateGroups);
            

        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient, LLUUID AgentID, LLUUID SessionID)
        {
            // Adam, this is one of those impossible to refactor items without resorting to .Split hackery
            string firstname = remoteClient.FirstName;
            string lastname = remoteClient.LastName;

            LLUUID ActiveGroupID = LLUUID.Zero;
            uint ActiveGroupPowers = 0;
            string ActiveGroupName = "OpenSimulator Tester";
            string ActiveGroupTitle = "I IZ N0T";

            bool foundUser = false;

            lock (m_iclientmap)
            {
                if (m_iclientmap.ContainsKey(remoteClient.AgentId))
                {
                    foundUser = true;
                }
            }
            if (foundUser)
            {
                lock (m_groupmap)
                {
                    if (m_groupmap.ContainsKey(remoteClient.AgentId))
                    {
                        GroupData grp = m_groupmap[remoteClient.AgentId];
                        if (grp != null)
                        {
                            ActiveGroupID = grp.GroupID;
                            ActiveGroupName = grp.groupName;
                            ActiveGroupPowers = grp.groupPowers;
                            ActiveGroupTitle = grp.ActiveGroupTitle;
                        }

                        remoteClient.SendAgentDataUpdate(AgentID, ActiveGroupID, firstname, lastname, ActiveGroupPowers, ActiveGroupName, ActiveGroupTitle);
                    }
                }
            }
        }

        private void OnInstantMessage(IClientAPI client, LLUUID fromAgentID,
                                      LLUUID fromAgentSession, LLUUID toAgentID,
                                      LLUUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, LLVector3 Position, LLUUID RegionID,
                                      byte[] binaryBucket)
        {
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Trigger the above event handler
            OnInstantMessage(null, new LLUUID(msg.fromAgentID), new LLUUID(msg.fromAgentSession),
                             new LLUUID(msg.toAgentID), new LLUUID(msg.imSessionID), msg.timestamp, msg.fromAgentName,
                             msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID,
                             new LLVector3(msg.Position.x, msg.Position.y, msg.Position.z), new LLUUID(msg.RegionID),
                             msg.binaryBucket);
        }

        private void OnClientClosed(LLUUID agentID)
        {
            lock (m_iclientmap)
            {
                if (m_iclientmap.ContainsKey(agentID))
                {
                    IClientAPI cli = m_iclientmap[agentID];
                    if (cli != null)
                    {
                        m_log.Info("[GROUP]: Removing all reference to groups for " + cli.Name);
                    }
                    else
                    {
                        m_log.Info("[GROUP]: Removing all reference to groups for " + agentID.ToString());
                    }
                    m_iclientmap.Remove(agentID);
                }
            }

            lock (m_groupmap)
            {
                if (m_groupmap.ContainsKey(agentID))
                {
                    m_groupmap.Remove(agentID);
                }
            }

            lock (m_grouplistmap)
            {
                if (m_grouplistmap.ContainsKey(agentID))
                {
                    m_grouplistmap.Remove(agentID);
                }
            }
            GC.Collect();
        }
    }


}