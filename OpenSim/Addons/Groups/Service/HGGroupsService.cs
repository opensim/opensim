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
using System.Timers;
using log4net;
using Nini.Config;

using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Groups
{
    public class HGGroupsService : GroupsService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IOfflineIMService m_OfflineIM;
        private IUserAccountService m_UserAccounts;
        private string m_HomeURI;

        public HGGroupsService(IConfigSource config, IOfflineIMService im, IUserAccountService users, string homeURI)
            : base(config, string.Empty)
        {
            m_OfflineIM = im;
            m_UserAccounts = users;
            m_HomeURI = homeURI;
            if (!m_HomeURI.EndsWith("/"))
                m_HomeURI += "/";
        }


        #region HG specific operations

        public bool CreateGroupProxy(string RequestingAgentID, string agentID,  string accessToken, UUID groupID, string serviceLocation, string name, out string reason)
        {
            reason = string.Empty;
            Uri uri = null;
            try
            {
                uri = new Uri(serviceLocation);
            }
            catch (UriFormatException)
            {
                reason = "Bad location for group proxy";
                return false;
            }

            // Check if it already exists
            GroupData grec = m_Database.RetrieveGroup(groupID);
            if (grec == null ||
                (grec != null && grec.Data["Location"] != string.Empty && grec.Data["Location"].ToLower() != serviceLocation.ToLower()))
            {
                // Create the group
                grec = new GroupData();
                grec.GroupID = groupID;
                grec.Data = new Dictionary<string, string>();
                grec.Data["Name"] = name + " @ " + uri.Authority;
                grec.Data["Location"] = serviceLocation;
                grec.Data["Charter"] = string.Empty;
                grec.Data["InsigniaID"] = UUID.Zero.ToString();
                grec.Data["FounderID"] = UUID.Zero.ToString();
                grec.Data["MembershipFee"] = "0";
                grec.Data["OpenEnrollment"] = "0";
                grec.Data["ShowInList"] = "0";
                grec.Data["AllowPublish"] = "0";
                grec.Data["MaturePublish"] = "0";
                grec.Data["OwnerRoleID"] = UUID.Zero.ToString();


                if (!m_Database.StoreGroup(grec))
                    return false;
            }

            if (grec.Data["Location"] == string.Empty)
            {
                reason = "Cannot add proxy membership to non-proxy group";
                return false;
            }

            UUID uid = UUID.Zero;
            string url = string.Empty, first = string.Empty, last = string.Empty, tmp = string.Empty;
            Util.ParseUniversalUserIdentifier(RequestingAgentID, out uid, out url, out first, out last, out tmp);
            string fromName = first + "." + last + "@" + url;

            // Invite to group again
            InviteToGroup(fromName, groupID, new UUID(agentID), grec.Data["Name"]);

            // Stick the proxy membership in the DB already
            // we'll delete it if the agent declines the invitation
            MembershipData membership = new MembershipData();
            membership.PrincipalID = agentID;
            membership.GroupID = groupID;
            membership.Data = new Dictionary<string, string>();
            membership.Data["SelectedRoleID"] = UUID.Zero.ToString();
            membership.Data["Contribution"] = "0";
            membership.Data["ListInProfile"] = "1";
            membership.Data["AcceptNotices"] = "1";
            membership.Data["AccessToken"] = accessToken;

            m_Database.StoreMember(membership);

            return true;
        }

        public bool RemoveAgentFromGroup(string RequestingAgentID, string AgentID, UUID GroupID, string token)
        {
            // check the token
            MembershipData membership = m_Database.RetrieveMember(GroupID, AgentID);
            if (membership != null)
            {
                if (token != string.Empty && token.Equals(membership.Data["AccessToken"]))
                {
                    return RemoveAgentFromGroup(RequestingAgentID, AgentID, GroupID);
                }
                else
                {
                    m_log.DebugFormat("[Groups.HGGroupsService]: access token {0} did not match stored one {1}", token, membership.Data["AccessToken"]);
                    return false;
                }
            }
            else
            {
                m_log.DebugFormat("[Groups.HGGroupsService]: membership not found for {0}", AgentID);
                return false;
            }
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID, string groupName, string token)
        {
            // check the token
            if (!VerifyToken(GroupID, RequestingAgentID, token))
                return null;

            ExtendedGroupRecord grec;
            if (GroupID == UUID.Zero)
                grec = GetGroupRecord(RequestingAgentID, groupName);
            else
                grec = GetGroupRecord(RequestingAgentID, GroupID);

            if (grec != null)
                FillFounderUUI(grec);

            return grec;
        }

        public List<ExtendedGroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            if (!VerifyToken(GroupID, RequestingAgentID, token))
                return new List<ExtendedGroupMembersData>();

            List<ExtendedGroupMembersData> members = GetGroupMembers(RequestingAgentID, GroupID);

            // convert UUIDs to UUIs
            members.ForEach(delegate (ExtendedGroupMembersData m)
            {
                if (m.AgentID.ToString().Length == 36) // UUID
                {
                    UserAccount account = m_UserAccounts.GetUserAccount(UUID.Zero, new UUID(m.AgentID));
                    if (account != null)
                        m.AgentID = Util.UniversalIdentifier(account.PrincipalID, account.FirstName, account.LastName, m_HomeURI);
                }
            });

            return members;
        }

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID GroupID, string token)
        {
            if (!VerifyToken(GroupID, RequestingAgentID, token))
                return new List<GroupRolesData>();

            return GetGroupRoles(RequestingAgentID, GroupID);
        }

        public List<ExtendedGroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            if (!VerifyToken(GroupID, RequestingAgentID, token))
                return new List<ExtendedGroupRoleMembersData>();

            List<ExtendedGroupRoleMembersData> rolemembers = GetGroupRoleMembers(RequestingAgentID, GroupID);

            // convert UUIDs to UUIs
            rolemembers.ForEach(delegate(ExtendedGroupRoleMembersData m)
            {
                if (m.MemberID.ToString().Length == 36) // UUID
                {
                    UserAccount account = m_UserAccounts.GetUserAccount(UUID.Zero, new UUID(m.MemberID));
                    if (account != null)
                        m.MemberID = Util.UniversalIdentifier(account.PrincipalID, account.FirstName, account.LastName, m_HomeURI);
                }
            });

            return rolemembers;
        }

        public bool AddNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
            bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            // check that the group proxy exists
            ExtendedGroupRecord grec = GetGroupRecord(RequestingAgentID, groupID);
            if (grec == null)
            {
                m_log.DebugFormat("[Groups.HGGroupsService]: attempt at adding notice to non-existent group proxy");
                return false;
            }

            // check that the group is remote
            if (grec.ServiceLocation == string.Empty)
            {
                m_log.DebugFormat("[Groups.HGGroupsService]: attempt at adding notice to local (non-proxy) group");
                return false;
            }

            // check that there isn't already a notice with the same ID
            if (GetGroupNotice(RequestingAgentID, noticeID) != null)
            {
                m_log.DebugFormat("[Groups.HGGroupsService]: a notice with the same ID already exists", grec.ServiceLocation);
                return false;
            }

            // This has good intentions (security) but it will potentially DDS the origin...
            // We'll need to send a proof along with the message. Maybe encrypt the message
            // using key pairs
            //
            //// check that the notice actually exists in the origin
            //GroupsServiceHGConnector c = new GroupsServiceHGConnector(grec.ServiceLocation);
            //if (!c.VerifyNotice(noticeID, groupID))
            //{
            //    m_log.DebugFormat("[Groups.HGGroupsService]: notice does not exist at origin {0}", grec.ServiceLocation);
            //    return false;
            //}

            // ok, we're good!
            return _AddNotice(groupID, noticeID, fromName, subject, message, hasAttachment, attType, attName, attItemID, attOwnerID);
        }

        public bool VerifyNotice(UUID noticeID, UUID groupID)
        {
            GroupNoticeInfo notice = GetGroupNotice(string.Empty, noticeID);

            if (notice == null)
                return false;

            if (notice.GroupID != groupID)
                return false;

            return true;
        }

        #endregion

        private void InviteToGroup(string fromName, UUID groupID, UUID invitedAgentID, string groupName)
        {
            // Todo: Security check, probably also want to send some kind of notification
            UUID InviteID = UUID.Random();

            if (AddAgentToGroupInvite(InviteID, groupID, invitedAgentID.ToString()))
            {
                Guid inviteUUID = InviteID.Guid;

                GridInstantMessage msg = new GridInstantMessage();

                msg.imSessionID = inviteUUID;

                // msg.fromAgentID = agentID.Guid;
                msg.fromAgentID = groupID.Guid;
                msg.toAgentID = invitedAgentID.Guid;
                //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                msg.timestamp = 0;
                msg.fromAgentName = fromName;
                msg.message = string.Format("Please confirm your acceptance to join group {0}.", groupName);
                msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupInvitation;
                msg.fromGroup = true;
                msg.offline = (byte)0;
                msg.ParentEstateID = 0;
                msg.Position = Vector3.Zero;
                msg.RegionID = UUID.Zero.Guid;
                msg.binaryBucket = new byte[20];

                string reason = string.Empty;
                m_OfflineIM.StoreMessage(msg, out reason);

            }
        }

        private bool AddAgentToGroupInvite(UUID inviteID, UUID groupID, string agentID)
        {
            // Check whether the invitee is already a member of the group
            MembershipData m = m_Database.RetrieveMember(groupID, agentID);
            if (m != null)
                return false;

            // Check whether there are pending invitations and delete them
            InvitationData invite = m_Database.RetrieveInvitation(groupID, agentID);
            if (invite != null)
                m_Database.DeleteInvite(invite.InviteID);

            invite = new InvitationData();
            invite.InviteID = inviteID;
            invite.PrincipalID = agentID;
            invite.GroupID = groupID;
            invite.RoleID = UUID.Zero;
            invite.Data = new Dictionary<string, string>();

            return m_Database.StoreInvitation(invite);
        }

        private void FillFounderUUI(ExtendedGroupRecord grec)
        {
            UserAccount account = m_UserAccounts.GetUserAccount(UUID.Zero, grec.FounderID);
            if (account != null)
                grec.FounderUUI = Util.UniversalIdentifier(account.PrincipalID, account.FirstName, account.LastName, m_HomeURI);
        }

        private bool VerifyToken(UUID groupID, string agentID, string token)
        {
            // check the token
            MembershipData membership = m_Database.RetrieveMember(groupID, agentID);
            if (membership != null)
            {
                if (token != string.Empty && token.Equals(membership.Data["AccessToken"]))
                    return true;
                else
                    m_log.DebugFormat("[Groups.HGGroupsService]: access token {0} did not match stored one {1}", token, membership.Data["AccessToken"]);
            }
            else
                m_log.DebugFormat("[Groups.HGGroupsService]: membership not found for {0}", agentID);

            return false;
        }
    }
}
