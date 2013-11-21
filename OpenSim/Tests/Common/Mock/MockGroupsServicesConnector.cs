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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Data.Null;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups;

namespace OpenSim.Tests.Common.Mock
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class MockGroupsServicesConnector : ISharedRegionModule, IGroupsServicesConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        IXGroupData m_data = new NullXGroupData(null, null);

        public string Name
        {
            get { return "MockGroupsServicesConnector"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_log.DebugFormat("[MOCK GROUPS SERVICES CONNECTOR]: Adding to region {0}", scene.RegionInfo.RegionName);
            scene.RegisterModuleInterface<IGroupsServicesConnector>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public UUID CreateGroup(UUID requestingAgentID, string name, string charter, bool showInList, UUID insigniaID, 
                                int membershipFee, bool openEnrollment, bool allowPublish, 
                                bool maturePublish, UUID founderID)
        {
            XGroup group = new XGroup()
            {
                groupID = UUID.Random(),
                ownerRoleID = UUID.Random(),
                name = name,
                charter = charter,
                showInList = showInList,
                insigniaID = insigniaID,
                membershipFee = membershipFee,
                openEnrollment = openEnrollment,
                allowPublish = allowPublish,
                maturePublish = maturePublish,
                founderID = founderID,
                everyonePowers = (ulong)XmlRpcGroupsServicesConnectorModule.DefaultEveryonePowers,
                ownersPowers = (ulong)XmlRpcGroupsServicesConnectorModule.DefaultOwnerPowers
            };

            if (m_data.StoreGroup(group))
            {
                m_log.DebugFormat("[MOCK GROUPS SERVICES CONNECTOR]: Created group {0} {1}", group.name, group.groupID);
                return group.groupID;
            }
            else
            {
                m_log.ErrorFormat("[MOCK GROUPS SERVICES CONNECTOR]: Failed to create group {0}", name);
                return UUID.Zero;
            }
        }

        public void UpdateGroup(UUID requestingAgentID, UUID groupID, string charter, bool showInList, 
                                UUID insigniaID, int membershipFee, bool openEnrollment, 
                                bool allowPublish, bool maturePublish)
        {
        }

        public void AddGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string description, 
                                 string title, ulong powers)
        {
        }

        public void RemoveGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID)
        {
        }

        public void UpdateGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string description, 
                                    string title, ulong powers)
        {
        }

        public GroupRecord GetGroupRecord(UUID requestingAgentID, UUID groupID, string groupName)
        {
            m_log.DebugFormat(
                "[MOCK GROUPS SERVICES CONNECTOR]: Processing GetGroupRecord() for groupID {0}, name {1}", 
                groupID, groupName);

            XGroup[] groups;
            string field, val;

            if (groupID != UUID.Zero)
            {
                field = "groupID";
                val = groupID.ToString();
            }
            else
            {
                field = "name";
                val = groupName;
            }

            groups = m_data.GetGroups(field, val);

            if (groups.Length == 0)
                return null;

            XGroup xg = groups[0];

            GroupRecord gr = new GroupRecord()
            {
                GroupID = xg.groupID,
                GroupName = xg.name,
                AllowPublish = xg.allowPublish,
                MaturePublish = xg.maturePublish,
                Charter = xg.charter,
                FounderID = xg.founderID,
                // FIXME: group picture storage location unknown
                MembershipFee = xg.membershipFee,
                OpenEnrollment = xg.openEnrollment,
                OwnerRoleID = xg.ownerRoleID,
                ShowInList = xg.showInList
            };

            return gr;
        }

        public GroupProfileData GetMemberGroupProfile(UUID requestingAgentID, UUID GroupID, UUID AgentID)
        {
            return default(GroupProfileData);
        }

        public void SetAgentActiveGroup(UUID requestingAgentID, UUID AgentID, UUID GroupID)
        {
        }

        public void SetAgentActiveGroupRole(UUID requestingAgentID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
        }

        public void SetAgentGroupInfo(UUID requestingAgentID, UUID AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
        }

        public void AddAgentToGroupInvite(UUID requestingAgentID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID)
        {
        }

        public GroupInviteInfo GetAgentToGroupInvite(UUID requestingAgentID, UUID inviteID)
        {
            return null;
        }

        public void RemoveAgentToGroupInvite(UUID requestingAgentID, UUID inviteID)
        {
        }

        public void AddAgentToGroup(UUID requestingAgentID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
        }

        public void RemoveAgentFromGroup(UUID requestingAgentID, UUID AgentID, UUID GroupID)
        {
        }

        public void AddAgentToGroupRole(UUID requestingAgentID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
        }

        public void RemoveAgentFromGroupRole(UUID requestingAgentID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
        }

        public List<DirGroupsReplyData> FindGroups(UUID requestingAgentID, string search)
        {
            return null;
        }

        public GroupMembershipData GetAgentGroupMembership(UUID requestingAgentID, UUID AgentID, UUID GroupID)
        {
            return null;
        }

        public GroupMembershipData GetAgentActiveMembership(UUID requestingAgentID, UUID AgentID)
        {
            return null;
        }

        public List<GroupMembershipData> GetAgentGroupMemberships(UUID requestingAgentID, UUID AgentID)
        {
            return new List<GroupMembershipData>();
        }

        public List<GroupRolesData> GetAgentGroupRoles(UUID requestingAgentID, UUID AgentID, UUID GroupID)
        {
            return null;
        }

        public List<GroupRolesData> GetGroupRoles(UUID requestingAgentID, UUID GroupID)
        {
            return null;
        }

        public List<GroupMembersData> GetGroupMembers(UUID requestingAgentID, UUID GroupID)
        {
            return null;
        }

        public List<GroupRoleMembersData> GetGroupRoleMembers(UUID requestingAgentID, UUID GroupID)
        {
            return null;
        }

        public List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, UUID GroupID)
        {
            return null;
        }
        
        public GroupNoticeInfo GetGroupNotice(UUID requestingAgentID, UUID noticeID)
        {
            return null;
        }
        
        public void AddGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket)
        {
        }

        public void ResetAgentGroupChatSessions(UUID agentID)
        {
        }

        public bool hasAgentBeenInvitedToGroupChatSession(UUID agentID, UUID groupID)
        {
            return false;
        }

        public bool hasAgentDroppedGroupChatSession(UUID agentID, UUID groupID)
        {
            return false;
        }

        public void AgentDroppedFromGroupChatSession(UUID agentID, UUID groupID)
        {
        }

        public void AgentInvitedToGroupChatSession(UUID agentID, UUID groupID)
        {
        }
    }
}