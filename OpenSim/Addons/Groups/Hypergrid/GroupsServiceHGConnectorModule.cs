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
using System.Linq;
using System.Reflection;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using Mono.Addins;
using log4net;
using Nini.Config;

namespace OpenSim.Groups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupsServiceHGConnectorModule")]
    public class GroupsServiceHGConnectorModule : ISharedRegionModule, IGroupsServicesConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private IGroupsServicesConnector m_LocalGroupsConnector;
        private string m_LocalGroupsServiceLocation;
        private IUserManagement m_UserManagement;
        private IOfflineIMService m_OfflineIM;
        private IMessageTransferModule m_Messaging;
        private List<Scene> m_Scenes;
        private ForeignImporter m_ForeignImporter;
        private string m_ServiceLocation;
        private IConfigSource m_Config;

        private Dictionary<string, GroupsServiceHGConnector> m_NetworkConnectors = new Dictionary<string, GroupsServiceHGConnector>();
        private RemoteConnectorCacheWrapper m_CacheWrapper; // for caching info of external group services

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];
            if (groupsConfig == null)
                return;

            if ((groupsConfig.GetBoolean("Enabled", false) == false)
                    || (groupsConfig.GetString("ServicesConnectorModule", string.Empty) != Name))
            {
                return;
            }

            m_Config = config;
            m_ServiceLocation = groupsConfig.GetString("LocalService", "local"); // local or remote
            m_LocalGroupsServiceLocation = groupsConfig.GetString("GroupsExternalURI", "http://127.0.0.1");
            m_Scenes = new List<Scene>();

            m_Enabled = true;

            m_log.DebugFormat("[Groups]: Initializing {0} with LocalService {1}", this.Name, m_ServiceLocation);
        }

        public string Name
        {
            get { return "Groups HG Service Connector"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.DebugFormat("[Groups]: Registering {0} with {1}", this.Name, scene.RegionInfo.RegionName); 
            scene.RegisterModuleInterface<IGroupsServicesConnector>(this);
            m_Scenes.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<IGroupsServicesConnector>(this);
            m_Scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (m_UserManagement == null)
            {
                m_UserManagement = scene.RequestModuleInterface<IUserManagement>();
                m_OfflineIM = scene.RequestModuleInterface<IOfflineIMService>();
                m_Messaging = scene.RequestModuleInterface<IMessageTransferModule>();
                m_ForeignImporter = new ForeignImporter(m_UserManagement);

                if (m_ServiceLocation.Equals("local"))
                {
                    m_LocalGroupsConnector = new GroupsServiceLocalConnectorModule(m_Config, m_UserManagement);
                    // Also, if local, create the endpoint for the HGGroupsService
                    new HGGroupsServiceRobustConnector(m_Config, MainServer.Instance, string.Empty, 
                        scene.RequestModuleInterface<IOfflineIMService>(), scene.RequestModuleInterface<IUserAccountService>());

                }
                else
                    m_LocalGroupsConnector = new GroupsServiceRemoteConnectorModule(m_Config, m_UserManagement);

                m_CacheWrapper = new RemoteConnectorCacheWrapper(m_UserManagement);
            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            client.OnCompleteMovementToRegion += OnCompleteMovementToRegion;
        }

        void OnCompleteMovementToRegion(IClientAPI client, bool arg2)
        {
            object sp = null;
            if (client.Scene.TryGetScenePresence(client.AgentId, out sp))
            {
                if (sp is ScenePresence && ((ScenePresence)sp).PresenceType != PresenceType.Npc)
                {
                    AgentCircuitData aCircuit = ((ScenePresence)sp).Scene.AuthenticateHandler.GetAgentCircuitData(client.AgentId);
                    if (aCircuit != null && (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0 && 
                        m_OfflineIM != null && m_Messaging != null)
                    {
                        List<GridInstantMessage> ims = m_OfflineIM.GetMessages(aCircuit.AgentID);
                        if (ims != null && ims.Count > 0)
                            foreach (GridInstantMessage im in ims)
                                m_Messaging.SendInstantMessage(im, delegate(bool success) { });
                    }
                }
            }
        }

        #region IGroupsServicesConnector

        public UUID CreateGroup(UUID RequestingAgentID, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, 
            bool allowPublish, bool maturePublish, UUID founderID, out string reason)
        {
            reason = string.Empty;
            if (m_UserManagement.IsLocalGridUser(RequestingAgentID))
                return m_LocalGroupsConnector.CreateGroup(RequestingAgentID, name, charter, showInList, insigniaID, 
                    membershipFee, openEnrollment, allowPublish, maturePublish, founderID, out reason);
            else
            {
                reason = "Only local grid users are allowed to create a new group";
                return UUID.Zero;
            }
        }

        public bool UpdateGroup(string RequestingAgentID, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, 
            bool openEnrollment, bool allowPublish, bool maturePublish, out string reason)
        {
            reason = string.Empty;
            string url = string.Empty;
            string name = string.Empty;
            if (IsLocal(groupID, out url, out name))
                return m_LocalGroupsConnector.UpdateGroup(AgentUUI(RequestingAgentID), groupID, charter, showInList, insigniaID, membershipFee, 
                    openEnrollment, allowPublish, maturePublish, out reason);
            else
            {
                reason = "Changes to remote group not allowed. Please go to the group's original world.";
                return false;
            }
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID, string GroupName)
        {
            string url = string.Empty;
            string name = string.Empty;
            if (IsLocal(GroupID, out url, out name))
                return m_LocalGroupsConnector.GetGroupRecord(AgentUUI(RequestingAgentID), GroupID, GroupName);
            else if (url != string.Empty)
            {
                ExtendedGroupMembershipData membership = m_LocalGroupsConnector.GetAgentGroupMembership(RequestingAgentID, RequestingAgentID, GroupID);
                string accessToken = string.Empty;
                if (membership != null)
                    accessToken = membership.AccessToken;
                else
                    return null;

                GroupsServiceHGConnector c = GetConnector(url);
                if (c != null)
                {
                    ExtendedGroupRecord grec = m_CacheWrapper.GetGroupRecord(RequestingAgentID, GroupID, GroupName, delegate
                    {
                        return c.GetGroupRecord(AgentUUIForOutside(RequestingAgentID), GroupID, GroupName, accessToken);
                    });

                    if (grec != null)
                        ImportForeigner(grec.FounderUUI);
                    return grec;
                }
            }

            return null;
        }

        public List<DirGroupsReplyData> FindGroups(string RequestingAgentID, string search)
        {
            return m_LocalGroupsConnector.FindGroups(AgentUUI(RequestingAgentID), search);
        }

        public List<GroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID)
        {
            string url = string.Empty, gname = string.Empty;
            if (IsLocal(GroupID, out url, out gname))
                return m_LocalGroupsConnector.GetGroupMembers(AgentUUI(RequestingAgentID), GroupID);
            else if (!string.IsNullOrEmpty(url))
            {
                ExtendedGroupMembershipData membership = m_LocalGroupsConnector.GetAgentGroupMembership(RequestingAgentID, RequestingAgentID, GroupID);
                string accessToken = string.Empty;
                if (membership != null)
                    accessToken = membership.AccessToken;
                else
                    return null;

                GroupsServiceHGConnector c = GetConnector(url);
                if (c != null)
                {
                    return m_CacheWrapper.GetGroupMembers(RequestingAgentID, GroupID, delegate
                    {
                        return c.GetGroupMembers(AgentUUIForOutside(RequestingAgentID), GroupID, accessToken);
                    });

                }
            }
            return new List<GroupMembersData>();
        }

        public bool AddGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, out string reason)
        {
            reason = string.Empty;
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                return m_LocalGroupsConnector.AddGroupRole(AgentUUI(RequestingAgentID), groupID, roleID, name, description, title, powers, out reason);
            else
            {
                reason = "Operation not allowed outside this group's origin world.";
                return false;
            }
        }

        public bool UpdateGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                return m_LocalGroupsConnector.UpdateGroupRole(AgentUUI(RequestingAgentID), groupID, roleID, name, description, title, powers);
            else
            {
                return false;
            }

        }

        public void RemoveGroupRole(string RequestingAgentID, UUID groupID, UUID roleID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                m_LocalGroupsConnector.RemoveGroupRole(AgentUUI(RequestingAgentID), groupID, roleID);
            else
            {
                return;
            }
        }

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID groupID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                return m_LocalGroupsConnector.GetGroupRoles(AgentUUI(RequestingAgentID), groupID);
            else if (!string.IsNullOrEmpty(url))
            {
                ExtendedGroupMembershipData membership = m_LocalGroupsConnector.GetAgentGroupMembership(RequestingAgentID, RequestingAgentID, groupID);
                string accessToken = string.Empty;
                if (membership != null)
                    accessToken = membership.AccessToken;
                else
                    return null;

                GroupsServiceHGConnector c = GetConnector(url);
                if (c != null)
                {
                    return m_CacheWrapper.GetGroupRoles(RequestingAgentID, groupID, delegate
                    {
                        return c.GetGroupRoles(AgentUUIForOutside(RequestingAgentID), groupID, accessToken);
                    });

                }
            }

            return new List<GroupRolesData>();
        }

        public List<GroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID groupID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                return m_LocalGroupsConnector.GetGroupRoleMembers(AgentUUI(RequestingAgentID), groupID);
            else if (!string.IsNullOrEmpty(url))
            {
                ExtendedGroupMembershipData membership = m_LocalGroupsConnector.GetAgentGroupMembership(RequestingAgentID, RequestingAgentID, groupID);
                string accessToken = string.Empty;
                if (membership != null)
                    accessToken = membership.AccessToken;
                else
                    return null;

                GroupsServiceHGConnector c = GetConnector(url);
                if (c != null)
                {
                    return m_CacheWrapper.GetGroupRoleMembers(RequestingAgentID, groupID, delegate
                    {
                        return c.GetGroupRoleMembers(AgentUUIForOutside(RequestingAgentID), groupID, accessToken);
                    });

                }
            }
            
            return new List<GroupRoleMembersData>();
        }

        public bool AddAgentToGroup(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID, string token, out string reason)
        {
            string url = string.Empty;
            string name = string.Empty;
            reason = string.Empty;

            UUID uid = new UUID(AgentID);
            if (IsLocal(GroupID, out url, out name))
            {
                if (m_UserManagement.IsLocalGridUser(uid)) // local user
                {
                    // normal case: local group, local user
                    return m_LocalGroupsConnector.AddAgentToGroup(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, RoleID, token, out reason);
                }
                else // local group, foreign user
                {
                    // the user is accepting the  invitation, or joining, where the group resides
                    token = UUID.Random().ToString();
                    bool success = m_LocalGroupsConnector.AddAgentToGroup(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, RoleID, token, out reason);

                    if (success)
                    {
                        url = m_UserManagement.GetUserServerURL(uid, "GroupsServerURI");
                        if (url == string.Empty)
                        {
                            reason = "User doesn't have a groups server";
                            return false;
                        }

                        GroupsServiceHGConnector c = GetConnector(url);
                        if (c != null)
                            return c.CreateProxy(AgentUUI(RequestingAgentID), AgentID, token, GroupID, m_LocalGroupsServiceLocation, name, out reason);
                    }
                }
            }
            else if (m_UserManagement.IsLocalGridUser(uid)) // local user
            {
                // foreign group, local user. She's been added already by the HG service.
                // Let's just check
                if (m_LocalGroupsConnector.GetAgentGroupMembership(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID) != null)
                    return true;
            }

            reason = "Operation not allowed outside this group's origin world";
            return false;
        }


        public void RemoveAgentFromGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            string url = string.Empty, name = string.Empty;
            if (!IsLocal(GroupID, out url, out name) && url != string.Empty)
            {
                ExtendedGroupMembershipData membership = m_LocalGroupsConnector.GetAgentGroupMembership(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID);
                if (membership != null)
                {
                    GroupsServiceHGConnector c = GetConnector(url);
                    if (c != null)
                        c.RemoveAgentFromGroup(AgentUUIForOutside(AgentID), GroupID, membership.AccessToken);
                }
            }

            // remove from local service
            m_LocalGroupsConnector.RemoveAgentFromGroup(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID);
        }

        public bool AddAgentToGroupInvite(string RequestingAgentID, UUID inviteID, UUID groupID, UUID roleID, string agentID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
                return m_LocalGroupsConnector.AddAgentToGroupInvite(AgentUUI(RequestingAgentID), inviteID, groupID, roleID, AgentUUI(agentID));
            else
                return false;
        }

        public GroupInviteInfo GetAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            return m_LocalGroupsConnector.GetAgentToGroupInvite(AgentUUI(RequestingAgentID), inviteID); ;
        }

        public void RemoveAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            m_LocalGroupsConnector.RemoveAgentToGroupInvite(AgentUUI(RequestingAgentID), inviteID);
        }

        public void AddAgentToGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                m_LocalGroupsConnector.AddAgentToGroupRole(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, RoleID);

        }

        public void RemoveAgentFromGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                m_LocalGroupsConnector.RemoveAgentFromGroupRole(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, RoleID);
        }

        public List<GroupRolesData> GetAgentGroupRoles(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                return m_LocalGroupsConnector.GetAgentGroupRoles(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID);
            else
                return new List<GroupRolesData>();
        }

        public void SetAgentActiveGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                m_LocalGroupsConnector.SetAgentActiveGroup(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID);
        }

        public ExtendedGroupMembershipData GetAgentActiveMembership(string RequestingAgentID, string AgentID)
        {
            return m_LocalGroupsConnector.GetAgentActiveMembership(AgentUUI(RequestingAgentID), AgentUUI(AgentID));
        }

        public void SetAgentActiveGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                m_LocalGroupsConnector.SetAgentActiveGroupRole(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, RoleID);
        }

        public void UpdateMembership(string RequestingAgentID, string AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            m_LocalGroupsConnector.UpdateMembership(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID, AcceptNotices, ListInProfile);
        }

        public ExtendedGroupMembershipData GetAgentGroupMembership(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(GroupID, out url, out gname))
                return m_LocalGroupsConnector.GetAgentGroupMembership(AgentUUI(RequestingAgentID), AgentUUI(AgentID), GroupID);
            else
                return null;
        }

        public List<GroupMembershipData> GetAgentGroupMemberships(string RequestingAgentID, string AgentID)
        {
            return m_LocalGroupsConnector.GetAgentGroupMemberships(AgentUUI(RequestingAgentID), AgentUUI(AgentID));
        }

        public bool AddGroupNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
            bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            string url = string.Empty, gname = string.Empty;

            if (IsLocal(groupID, out url, out gname))
            {
                if (m_LocalGroupsConnector.AddGroupNotice(AgentUUI(RequestingAgentID), groupID, noticeID, fromName, subject, message,
                        hasAttachment, attType, attName, attItemID, AgentUUI(attOwnerID)))
                {
                    // then send the notice to every grid for which there are members in this group
                    List<GroupMembersData> members = m_LocalGroupsConnector.GetGroupMembers(AgentUUI(RequestingAgentID), groupID);
                    List<string> urls = new List<string>();
                    foreach (GroupMembersData m in members)
                    {
                        if (!m_UserManagement.IsLocalGridUser(m.AgentID))
                        {
                            string gURL = m_UserManagement.GetUserServerURL(m.AgentID, "GroupsServerURI");
                            if (!urls.Contains(gURL))
                                urls.Add(gURL);
                        }
                    }

                    // so we have the list of urls to send the notice to
                    // this may take a long time...
                    Util.FireAndForget(delegate
                    {
                        foreach (string u in urls)
                        {
                            GroupsServiceHGConnector c = GetConnector(u);
                            if (c != null)
                            {
                                c.AddNotice(AgentUUIForOutside(RequestingAgentID), groupID, noticeID, fromName, subject, message,
                                    hasAttachment, attType, attName, attItemID, AgentUUIForOutside(attOwnerID));
                            }
                        }
                    });

                    return true;
                }

                return false;
            }
            else
                return false;
        }

        public GroupNoticeInfo GetGroupNotice(string RequestingAgentID, UUID noticeID)
        {
            GroupNoticeInfo notice = m_LocalGroupsConnector.GetGroupNotice(AgentUUI(RequestingAgentID), noticeID);

            if (notice != null && notice.noticeData.HasAttachment && notice.noticeData.AttachmentOwnerID != null)
               ImportForeigner(notice.noticeData.AttachmentOwnerID);

            return notice;
        }

        public List<ExtendedGroupNoticeData> GetGroupNotices(string RequestingAgentID, UUID GroupID)
        {
            return m_LocalGroupsConnector.GetGroupNotices(AgentUUI(RequestingAgentID), GroupID);
        }

        public void ResetAgentGroupChatSessions(string agentID)
        {
        }

        public bool hasAgentBeenInvitedToGroupChatSession(string agentID, UUID groupID)
        {
            return false;
        }

        public bool hasAgentDroppedGroupChatSession(string agentID, UUID groupID)
        {
            return false;
        }

        public void AgentDroppedFromGroupChatSession(string agentID, UUID groupID)
        {
        }

        public void AgentInvitedToGroupChatSession(string agentID, UUID groupID)
        {
        }

        #endregion

        #region hypergrid groups

        private string AgentUUI(string AgentIDStr)
        {
            UUID AgentID = UUID.Zero;
            try
            {
                AgentID = new UUID(AgentIDStr);
            }
            catch (FormatException)
            {
                return AgentID.ToString();
            }

            if (m_UserManagement.IsLocalGridUser(AgentID))
                return AgentID.ToString();

            AgentCircuitData agent = null;
            foreach (Scene scene in m_Scenes)
            {
                agent = scene.AuthenticateHandler.GetAgentCircuitData(AgentID);
                if (agent != null)
                    break;
            }
            if (agent == null) // oops
                return AgentID.ToString();

            return Util.ProduceUserUniversalIdentifier(agent);
        }

        private string AgentUUIForOutside(string AgentIDStr)
        {
            UUID AgentID = UUID.Zero;
            try
            {
                AgentID = new UUID(AgentIDStr);
            }
            catch (FormatException)
            {
                return AgentID.ToString();
            }

            AgentCircuitData agent = null;
            foreach (Scene scene in m_Scenes)
            {
                agent = scene.AuthenticateHandler.GetAgentCircuitData(AgentID);
                if (agent != null)
                    break;
            }
            if (agent == null) // oops
                return AgentID.ToString();

            return Util.ProduceUserUniversalIdentifier(agent);
        }

        private UUID ImportForeigner(string uID)
        {
            UUID userID = UUID.Zero;
            string url = string.Empty, first = string.Empty, last = string.Empty, tmp = string.Empty;
            if (Util.ParseUniversalUserIdentifier(uID, out userID, out url, out first, out last, out tmp))
                m_UserManagement.AddUser(userID, first, last, url);
            
            return userID;
        }

        private bool IsLocal(UUID groupID, out string serviceLocation, out string name)
        {
            serviceLocation = string.Empty;
            name = string.Empty;
            if (groupID.Equals(UUID.Zero))
                return true;

            ExtendedGroupRecord group = m_LocalGroupsConnector.GetGroupRecord(UUID.Zero.ToString(), groupID, string.Empty);
            if (group == null)
            {
                //m_log.DebugFormat("[XXX]: IsLocal? group {0} not found -- no.", groupID);
                return false;
            }

            serviceLocation = group.ServiceLocation;
            name = group.GroupName;
            bool isLocal = (group.ServiceLocation == string.Empty);
            //m_log.DebugFormat("[XXX]: IsLocal? {0}", isLocal);
            return isLocal;
        }

        private GroupsServiceHGConnector GetConnector(string url)
        {
            lock (m_NetworkConnectors)
            {
                if (m_NetworkConnectors.ContainsKey(url))
                    return m_NetworkConnectors[url];

                GroupsServiceHGConnector c = new GroupsServiceHGConnector(url);
                m_NetworkConnectors[url] = c;
            }

            return m_NetworkConnectors[url];
        }
        #endregion
    }
}
