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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
//using System.Text;

using Nwc.XmlRpc;

using log4net;
// using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
//using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{

    public class XmlRpcGroupDataProvider : IGroupDataProvider
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serviceURL = "http://osflotsam.org/xmlrpc.php";

        public const GroupPowers m_DefaultEveryonePowers = GroupPowers.AllowSetHome | GroupPowers.Accountable | GroupPowers.JoinChat | GroupPowers.AllowVoiceChat | GroupPowers.ReceiveNotices | GroupPowers.StartProposal | GroupPowers.VoteOnProposal;

        public XmlRpcGroupDataProvider(string serviceURL)
        {
            m_serviceURL = serviceURL;
        }

        /// <summary>
        /// Create a Group, including Everyone and Owners Role, place FounderID in both groups, select Owner as selected role, and newly created group as agent's active role.
        /// </summary>
        public UUID CreateGroup(string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish, UUID founderID)
        {
            UUID GroupID = UUID.Random();
            UUID OwnerRoleID = UUID.Random();

            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();
            param["Name"] = name;
            param["Charter"] = charter;
            param["ShowInList"] = showInList == true ? 1 : 0;
            param["InsigniaID"] = insigniaID.ToString();
            param["MembershipFee"] = 0;
            param["OpenEnrollment"] = openEnrollment == true ? 1 : 0;
            param["AllowPublish"] = allowPublish == true ? 1 : 0;
            param["MaturePublish"] = maturePublish == true ? 1 : 0;
            param["FounderID"] = founderID.ToString();
            param["EveryonePowers"] = ((ulong)m_DefaultEveryonePowers).ToString();
            param["OwnerRoleID"] = OwnerRoleID.ToString();

            // Would this be cleaner as (GroupPowers)ulong.MaxValue;
            GroupPowers OwnerPowers = GroupPowers.Accountable
                                    | GroupPowers.AllowEditLand
                                    | GroupPowers.AllowFly
                                    | GroupPowers.AllowLandmark
                                    | GroupPowers.AllowRez
                                    | GroupPowers.AllowSetHome
                                    | GroupPowers.AllowVoiceChat
                                    | GroupPowers.AssignMember
                                    | GroupPowers.AssignMemberLimited
                                    | GroupPowers.ChangeActions
                                    | GroupPowers.ChangeIdentity
                                    | GroupPowers.ChangeMedia
                                    | GroupPowers.ChangeOptions
                                    | GroupPowers.CreateRole
                                    | GroupPowers.DeedObject
                                    | GroupPowers.DeleteRole
                                    | GroupPowers.Eject
                                    | GroupPowers.FindPlaces
                                    | GroupPowers.Invite
                                    | GroupPowers.JoinChat
                                    | GroupPowers.LandChangeIdentity
                                    | GroupPowers.LandDeed
                                    | GroupPowers.LandDivideJoin
                                    | GroupPowers.LandEdit
                                    | GroupPowers.LandEjectAndFreeze
                                    | GroupPowers.LandGardening
                                    | GroupPowers.LandManageAllowed
                                    | GroupPowers.LandManageBanned
                                    | GroupPowers.LandManagePasses
                                    | GroupPowers.LandOptions
                                    | GroupPowers.LandRelease
                                    | GroupPowers.LandSetSale
                                    | GroupPowers.ModerateChat
                                    | GroupPowers.ObjectManipulate
                                    | GroupPowers.ObjectSetForSale
                                    | GroupPowers.ReceiveNotices
                                    | GroupPowers.RemoveMember
                                    | GroupPowers.ReturnGroupOwned
                                    | GroupPowers.ReturnGroupSet
                                    | GroupPowers.ReturnNonGroup
                                    | GroupPowers.RoleProperties
                                    | GroupPowers.SendNotices
                                    | GroupPowers.SetLandingPoint
                                    | GroupPowers.StartProposal
                                    | GroupPowers.VoteOnProposal;
            param["OwnersPowers"] = ((ulong)OwnerPowers).ToString();



            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.createGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }

            return UUID.Parse((string)respData["GroupID"]);
        }

        public void UpdateGroup(UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["Charter"] = charter;
            param["ShowInList"] = showInList == true ? 1 : 0;
            param["InsigniaID"] = insigniaID.ToString();
            param["MembershipFee"] = membershipFee;
            param["OpenEnrollment"] = openEnrollment == true ? 1 : 0;
            param["AllowPublish"] = allowPublish == true ? 1 : 0;
            param["MaturePublish"] = maturePublish == true ? 1 : 0;

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.updateGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void AddGroupRole(UUID groupID, UUID roleID, string name, string description, string title, ulong powers)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["RoleID"] = roleID.ToString();
            param["Name"] = name;
            param["Description"] = description;
            param["Title"] = title;
            param["Powers"] = powers.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.addRoleToGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void RemoveGroupRole(UUID groupID, UUID roleID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["RoleID"] = roleID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.removeRoleFromGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void UpdateGroupRole(UUID groupID, UUID roleID, string name, string description, string title, ulong powers)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["RoleID"] = roleID.ToString();
            if (name != null)
            {
                param["Name"] = name;
            }
            if (description != null)
            {
                param["Description"] = description;
            }
            if (title != null)
            {
                param["Title"] = title;
            }
            param["Powers"] = powers.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.updateGroupRole", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public GroupRecord GetGroupRecord(UUID GroupID, string GroupName)
        {
            Hashtable param = new Hashtable();
            if ((GroupID != null) && (GroupID != UUID.Zero))
            {
                param["GroupID"] = GroupID.ToString();
            }
            if ((GroupName != null) && (GroupName != string.Empty))
            {
                param["Name"] = GroupName.ToString();
            }


            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "Group Not Found")
                {
                    LogRespDataToConsoleError(respData);
                }
                return null;
            }

            return GroupProfileHashtableToGroupRecord(respData);

        }

        public GroupProfileData GetMemberGroupProfile(UUID GroupID, UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();


            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "Group Not Found")
                {
                    LogRespDataToConsoleError(respData);
                }
                return new GroupProfileData();
            }

            GroupMembershipData MemberInfo = GetAgentGroupMembership(AgentID, GroupID);
            GroupProfileData MemberGroupProfile = GroupProfileHashtableToGroupProfileData(respData);

            MemberGroupProfile.MemberTitle = MemberInfo.GroupTitle;
            MemberGroupProfile.PowersMask = MemberInfo.GroupPowers;

            return MemberGroupProfile;

        }

        private GroupProfileData GroupProfileHashtableToGroupProfileData(Hashtable groupProfile)
        {
            GroupProfileData group = new GroupProfileData();
            group.GroupID = UUID.Parse((string)groupProfile["GroupID"]);
            group.Name = (string)groupProfile["Name"];

            if (groupProfile["Charter"] != null)
            {
                group.Charter = (string)groupProfile["Charter"];
            }

            group.ShowInList = ((string)groupProfile["ShowInList"]) == "1";
            group.InsigniaID = UUID.Parse((string)groupProfile["InsigniaID"]);
            group.MembershipFee = int.Parse((string)groupProfile["MembershipFee"]);
            group.OpenEnrollment = ((string)groupProfile["OpenEnrollment"]) == "1";
            group.AllowPublish = ((string)groupProfile["AllowPublish"]) == "1";
            group.MaturePublish = ((string)groupProfile["MaturePublish"]) == "1";
            group.FounderID = UUID.Parse((string)groupProfile["FounderID"]);
            group.OwnerRole = UUID.Parse((string)groupProfile["OwnerRoleID"]);

            group.GroupMembershipCount = int.Parse((string)groupProfile["GroupMembershipCount"]);
            group.GroupRolesCount = int.Parse((string)groupProfile["GroupRolesCount"]);

            return group;
        }

        private GroupRecord GroupProfileHashtableToGroupRecord(Hashtable groupProfile)
        {

            GroupRecord group = new GroupRecord();
            m_log.Debug("GroupID");
            group.GroupID = UUID.Parse((string)groupProfile["GroupID"]);

            m_log.Debug("Name");
            group.GroupName = groupProfile["Name"].ToString();

            m_log.Debug("Charter");
            if (groupProfile["Charter"] != null)
            {
                group.Charter = (string)groupProfile["Charter"];
            }

            m_log.Debug("ShowInList");
            group.ShowInList = ((string)groupProfile["ShowInList"]) == "1";

            m_log.Debug("InsigniaID");
            group.GroupPicture = UUID.Parse((string)groupProfile["InsigniaID"]);

            m_log.Debug("MembershipFee");
            group.MembershipFee = int.Parse((string)groupProfile["MembershipFee"]);

            m_log.Debug("OpenEnrollment");
            group.OpenEnrollment = ((string)groupProfile["OpenEnrollment"]) == "1";

            m_log.Debug("AllowPublish");
            group.AllowPublish = ((string)groupProfile["AllowPublish"]) == "1";

            m_log.Debug("MaturePublish");
            group.MaturePublish = ((string)groupProfile["MaturePublish"]) == "1";

            m_log.Debug("FounderID");
            group.FounderID = UUID.Parse((string)groupProfile["FounderID"]);

            m_log.Debug("OwnerRoleID");
            group.OwnerRoleID = UUID.Parse((string)groupProfile["OwnerRoleID"]);

            return group;
        }


        public void SetAgentActiveGroup(UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.setAgentActiveGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }

        }

        public void SetAgentActiveGroupRole(UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["SelectedRoleID"] = RoleID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.setAgentGroupInfo", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }

        }

        public void SetAgentGroupInfo(UUID AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["AcceptNotices"] = AcceptNotices ? "1" : "0";
            param["ListInProfile"] = ListInProfile ? "1" : "0";

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.setAgentGroupInfo", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void AddAgentToGroupInvite(UUID inviteID, UUID groupID, UUID roleID, UUID agentID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();
            param["AgentID"] = agentID.ToString();
            param["RoleID"] = roleID.ToString();
            param["GroupID"] = groupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.addAgentToGroupInvite", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                if (respData["error"] != "Duplicate group invite requested")
                {
                    LogRespDataToConsoleError(respData);
                }
            }


        }

        public GroupInviteInfo GetAgentToGroupInvite(UUID inviteID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getAgentToGroupInvite", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;


            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);

                return null;
            }

            GroupInviteInfo inviteInfo = new GroupInviteInfo();
            inviteInfo.InviteID = inviteID;
            inviteInfo.GroupID = UUID.Parse((string)respData["GroupID"]);
            inviteInfo.RoleID = UUID.Parse((string)respData["RoleID"]);
            inviteInfo.AgentID = UUID.Parse((string)respData["AgentID"]);

            return inviteInfo;
        }

        public void RemoveAgentToGroupInvite(UUID inviteID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.removeAgentToGroupInvite", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void AddAgentToGroup(UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.addAgentToGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void RemoveAgentFromGroup(UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.removeAgentFromGroup", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void AddAgentToGroupRole(UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.addAgentToGroupRole", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }

        public void RemoveAgentFromGroupRole(UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.removeAgentFromGroupRole", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }


        public List<DirGroupsReplyData> FindGroups(string search)
        {
            Hashtable param = new Hashtable();
            param["Search"] = search;

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.findGroups", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            List<DirGroupsReplyData> findings = new List<DirGroupsReplyData>();

            if (respData.Contains("error"))
            {
                if (respData["error"].ToString() != "No groups found.")
                {
                    LogRespDataToConsoleError(respData);
                }
            }
            else
            {
                Hashtable results = (Hashtable)respData["results"];
                foreach (Hashtable groupFind in results.Values)
                {
                    DirGroupsReplyData data = new DirGroupsReplyData();
                    data.groupID = new UUID((string)groupFind["GroupID"]); ;
                    data.groupName = (string)groupFind["Name"];
                    data.members = int.Parse((string)groupFind["Members"]);
                    // data.searchOrder = order;

                    findings.Add(data);
                }
            }

            return findings;
        }

        public GroupMembershipData GetAgentGroupMembership(UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getAgentGroupMembership", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "None Found")
                {
                    LogRespDataToConsoleError(respData);
                }
                return null;
            }

            GroupMembershipData data = HashTableToGroupMembershipData(respData);

            return data;
        }

        public GroupMembershipData GetAgentActiveMembership(UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getAgentActiveMembership", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                if (respData["error"].ToString() == "No Active Group Specified")
                {
                    return null;
                }
                LogRespDataToConsoleError(respData);
                return null;
            }

            try
            {
                GroupMembershipData data = HashTableToGroupMembershipData(respData);
                return data;
            }
            catch (System.Exception e)
            {
                LogRespDataToConsoleError(respData);
                throw e;
            }
        }

        private void LogRespDataToConsoleError(Hashtable respData)
        {
            m_log.Error("[GROUPDATA] Error:");

            foreach (string key in respData.Keys)
            {
                m_log.ErrorFormat("[GROUPDATA] Key: {0}", key);

                object o = respData[key];

                string[] lines = respData[key].ToString().Split(new char[] { '\n' });
                foreach (string line in lines)
                {
                    m_log.ErrorFormat("[GROUPDATA] {0}", line);
                }

            }
        }

        public List<GroupMembershipData> GetAgentGroupMemberships(UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getAgentGroupMemberships", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            List<GroupMembershipData> memberships = new List<GroupMembershipData>();

            if (respData.Contains("error"))
            {
                if (respData["error"].ToString() != "No Memberships")
                {
                    LogRespDataToConsoleError(respData);
                }
            }
            else
            {
                foreach (object membership in respData.Values)
                {
                    memberships.Add(HashTableToGroupMembershipData((Hashtable)membership));
                }
            }
            return memberships;
        }

        public List<GroupRolesData> GetAgentGroupRoles(UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getAgentRoles", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            List<GroupRolesData> Roles = new List<GroupRolesData>();

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "None found")
                {
                    LogRespDataToConsoleError(respData);
                }
                return Roles;
            }

            foreach (Hashtable role in respData.Values)
            {
                GroupRolesData data = new GroupRolesData();
                data.RoleID = new UUID((string)role["RoleID"]);
                data.Name = (string)role["Name"];
                data.Description = (string)role["Description"];
                data.Powers = ulong.Parse((string)role["Powers"]);
                data.Title = (string)role["Title"];

                Roles.Add(data);
            }

            return Roles;


        }

        public List<GroupRolesData> GetGroupRoles(UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroupRoles", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
                return null;
            }

            List<GroupRolesData> Roles = new List<GroupRolesData>();
            foreach (Hashtable role in respData.Values)
            {
                GroupRolesData data = new GroupRolesData();
                data.Description = (string)role["Description"];
                data.Members = int.Parse((string)role["Members"]);
                data.Name = (string)role["Name"];
                data.Powers = ulong.Parse((string)role["Powers"]);
                data.RoleID = new UUID((string)role["RoleID"]);
                data.Title = (string)role["Title"];

                Roles.Add(data);
            }

            return Roles;

        }

        private static GroupMembershipData HashTableToGroupMembershipData(Hashtable respData)
        {
            GroupMembershipData data = new GroupMembershipData();
            data.AcceptNotices = ((string)respData["AcceptNotices"] == "1");
            data.Contribution = int.Parse((string)respData["Contribution"]);
            data.ListInProfile = ((string)respData["ListInProfile"] == "1");

            data.ActiveRole = new UUID((string)respData["SelectedRoleID"]);
            data.GroupTitle = (string)respData["Title"];

            data.GroupPowers = ulong.Parse((string)respData["GroupPowers"]);

            // Is this group the agent's active group

            data.GroupID = new UUID((string)respData["GroupID"]);

            UUID ActiveGroup = new UUID((string)respData["ActiveGroupID"]);
            data.Active = data.GroupID.Equals(ActiveGroup);

            data.AllowPublish = ((string)respData["AllowPublish"] == "1");
            data.Charter = (string)respData["Charter"];
            data.FounderID = new UUID((string)respData["FounderID"]);
            data.GroupID = new UUID((string)respData["GroupID"]);
            data.GroupName = (string)respData["GroupName"];
            data.GroupPicture = new UUID((string)respData["InsigniaID"]);
            data.MaturePublish = ((string)respData["MaturePublish"] == "1");
            data.MembershipFee = int.Parse((string)respData["MembershipFee"]);
            data.OpenEnrollment = ((string)respData["OpenEnrollment"] == "1");
            data.ShowInList = ((string)respData["ShowInList"] == "1");
            return data;
        }

        public List<GroupMembersData> GetGroupMembers(UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroupMembers", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
                return null;
            }

            List<GroupMembersData> members = new List<GroupMembersData>();
            foreach (Hashtable membership in respData.Values)
            {
                GroupMembersData data = new GroupMembersData();

                data.AcceptNotices = ((string)membership["AcceptNotices"]) == "1";
                data.AgentID = new UUID((string)membership["AgentID"]);
                data.Contribution = int.Parse((string)membership["Contribution"]);
                data.IsOwner = ((string)membership["IsOwner"]) == "1";
                data.ListInProfile = ((string)membership["ListInProfile"]) == "1";
                data.AgentPowers = ulong.Parse((string)membership["AgentPowers"]);
                data.Title = (string)membership["Title"];

                members.Add(data);
            }

            return members;

        }

        public List<GroupRoleMembersData> GetGroupRoleMembers(UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroupRoleMembers", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
                return null;
            }

            List<GroupRoleMembersData> members = new List<GroupRoleMembersData>();
            foreach (Hashtable membership in respData.Values)
            {
                GroupRoleMembersData data = new GroupRoleMembersData();

                data.MemberID = new UUID((string)membership["AgentID"]);
                data.RoleID = new UUID((string)membership["RoleID"]);

                members.Add(data);
            }

            return members;
        }

        public List<GroupNoticeData> GetGroupNotices(UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroupNotices", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            List<GroupNoticeData> values = new List<GroupNoticeData>();

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "No Notices")
                {
                    LogRespDataToConsoleError(respData);
                }
            }
            else
            {
                foreach (Hashtable value in respData.Values)
                {
                    GroupNoticeData data = new GroupNoticeData();
                    data.NoticeID = UUID.Parse((string)value["NoticeID"]);
                    data.Timestamp = uint.Parse((string)value["Timestamp"]);
                    data.FromName = (string)value["FromName"];
                    data.Subject = (string)value["Subject"];
                    data.HasAttachment = false;
                    data.AssetType = 0;

                    values.Add(data);
                }
            }
            return values;

        }
        public GroupNoticeInfo GetGroupNotice(UUID noticeID)
        {
            Hashtable param = new Hashtable();
            param["NoticeID"] = noticeID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.getGroupNotice", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            

            if (respData.Contains("error"))
            {
                if ((string)respData["error"] != "Group Notice Not Found")
                {
                    LogRespDataToConsoleError(respData);
                    return null;
                }
            }

            GroupNoticeInfo data = new GroupNoticeInfo();
            data.GroupID = UUID.Parse((string)respData["GroupID"]);
            data.Message = (string)respData["Message"];
            data.BinaryBucket = Utils.HexStringToBytes((string)respData["BinaryBucket"], true);
            data.noticeData.NoticeID = UUID.Parse((string)respData["NoticeID"]);
            data.noticeData.Timestamp = uint.Parse((string)respData["Timestamp"]);
            data.noticeData.FromName = (string)respData["FromName"];
            data.noticeData.Subject = (string)respData["Subject"];
            data.noticeData.HasAttachment = false;
            data.noticeData.AssetType = 0;

            if (data.Message == null)
            {
                data.Message = string.Empty;
            }

            return data;
        }
        public void AddGroupNotice(UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket)
        {
            string binBucket = OpenMetaverse.Utils.BytesToHexString(binaryBucket, "");

            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["NoticeID"] = noticeID.ToString();
            param["FromName"] = fromName;
            param["Subject"] = subject;
            param["Message"] = message;
            param["BinaryBucket"] = binBucket;
            param["TimeStamp"] = ((uint)Util.UnixTimeSinceEpoch()).ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("groups.addGroupNotice", parameters);
            XmlRpcResponse resp = req.Send(m_serviceURL, 10000);
            Hashtable respData = (Hashtable)resp.Value;

            List<GroupNoticeData> values = new List<GroupNoticeData>();

            if (respData.Contains("error"))
            {
                LogRespDataToConsoleError(respData);
            }
        }


    }

    public class GroupNoticeInfo
    {
        public GroupNoticeData noticeData = new GroupNoticeData();
        public UUID GroupID = UUID.Zero;
        public string Message = string.Empty;
        public byte[] BinaryBucket = new byte[0];
    }
}
