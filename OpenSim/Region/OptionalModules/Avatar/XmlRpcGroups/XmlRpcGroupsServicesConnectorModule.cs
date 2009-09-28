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

using Nwc.XmlRpc;

using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class XmlRpcGroupsServicesConnectorModule : ISharedRegionModule, IGroupsServicesConnector
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        public const GroupPowers m_DefaultEveryonePowers = GroupPowers.AllowSetHome | 
            GroupPowers.Accountable | 
            GroupPowers.JoinChat | 
            GroupPowers.AllowVoiceChat | 
            GroupPowers.ReceiveNotices | 
            GroupPowers.StartProposal | 
            GroupPowers.VoteOnProposal;

        private bool m_connectorEnabled = false;

        private string m_serviceURL = string.Empty;

        private bool m_disableKeepAlive = false;

        private string m_groupReadKey  = string.Empty;
        private string m_groupWriteKey = string.Empty;


        #region IRegionModuleBase Members

        public string Name
        {
            get { return "XmlRpcGroupsServicesConnector"; }
        }

        // this module is not intended to be replaced, but there should only be 1 of them.
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            if (groupsConfig == null)
            {
                // Do not run this module by default.
                return;
            }
            else
            {
                // if groups aren't enabled, we're not needed.
                // if we're not specified as the connector to use, then we're not wanted
                if ((groupsConfig.GetBoolean("Enabled", false) == false)
                    || (groupsConfig.GetString("ServicesConnectorModule", "Default") != Name))
                {
                    m_connectorEnabled = false;
                    return;
                }

                m_log.InfoFormat("[GROUPS-CONNECTOR]: Initializing {0}", this.Name);

                m_serviceURL = groupsConfig.GetString("XmlRpcServiceURL", string.Empty);
                if ((m_serviceURL == null) ||
                    (m_serviceURL == string.Empty))
                {
                    m_log.ErrorFormat("Please specify a valid URL for XmlRpcServiceURL in OpenSim.ini, [Groups]");
                    m_connectorEnabled = false;
                    return;
                }

                m_disableKeepAlive = groupsConfig.GetBoolean("XmlRpcDisableKeepAlive", false);

                m_groupReadKey = groupsConfig.GetString("XmlRpcServiceReadKey", string.Empty);
                m_groupWriteKey = groupsConfig.GetString("XmlRpcServiceWriteKey", string.Empty);

                // If we got all the config options we need, lets start'er'up
                m_connectorEnabled = true;
            }
        }

        public void Close()
        {
            m_log.InfoFormat("[GROUPS-CONNECTOR]: Closing {0}", this.Name);
        }

        public void AddRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            if (m_connectorEnabled)
                scene.RegisterModuleInterface<IGroupsServicesConnector>(this);
        }

        public void RemoveRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            if (scene.RequestModuleInterface<IGroupsServicesConnector>() == this)
                scene.UnregisterModuleInterface<IGroupsServicesConnector>(this);
        }

        public void RegionLoaded(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            // TODO: May want to consider listenning for Agent Connections so we can pre-cache group info
            // scene.EventManager.OnNewClient += OnNewClient;
        }

        #endregion

        #region ISharedRegionModule Members

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion



        #region IGroupsServicesConnector Members

        /// <summary>
        /// Create a Group, including Everyone and Owners Role, place FounderID in both groups, select Owner as selected role, and newly created group as agent's active role.
        /// </summary>
        public UUID CreateGroup(GroupRequestID requestID, string name, string charter, bool showInList, UUID insigniaID, 
                                int membershipFee, bool openEnrollment, bool allowPublish, 
                                bool maturePublish, UUID founderID)
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




            Hashtable respData = XmlRpcCall(requestID, "groups.createGroup", param);

            if (respData.Contains("error"))
            {
                // UUID is not nullable

                return UUID.Zero;
            }

            return UUID.Parse((string)respData["GroupID"]);
        }

        public void UpdateGroup(GroupRequestID requestID, UUID groupID, string charter, bool showInList, 
                                UUID insigniaID, int membershipFee, bool openEnrollment, 
                                bool allowPublish, bool maturePublish)
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

            XmlRpcCall(requestID, "groups.updateGroup", param);
        }

        public void AddGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID, string name, string description, 
                                 string title, ulong powers)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["RoleID"] = roleID.ToString();
            param["Name"] = name;
            param["Description"] = description;
            param["Title"] = title;
            param["Powers"] = powers.ToString();

            XmlRpcCall(requestID, "groups.addRoleToGroup", param);
        }

        public void RemoveGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = groupID.ToString();
            param["RoleID"] = roleID.ToString();

            XmlRpcCall(requestID, "groups.removeRoleFromGroup", param);
        }

        public void UpdateGroupRole(GroupRequestID requestID, UUID groupID, UUID roleID, string name, string description, 
                                    string title, ulong powers)
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

            XmlRpcCall(requestID, "groups.updateGroupRole", param);
        }

        public GroupRecord GetGroupRecord(GroupRequestID requestID, UUID GroupID, string GroupName)
        {
            Hashtable param = new Hashtable();
            if (GroupID != UUID.Zero)
            {
                param["GroupID"] = GroupID.ToString();
            }
            if ((GroupName != null) && (GroupName != string.Empty))
            {
                param["Name"] = GroupName.ToString();
            }

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroup", param);

            if (respData.Contains("error"))
            {
                return null;
            }

            return GroupProfileHashtableToGroupRecord(respData);

        }

        public GroupProfileData GetMemberGroupProfile(GroupRequestID requestID, UUID GroupID, UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroup", param);

            if (respData.Contains("error"))
            {
                // GroupProfileData is not nullable
                return new GroupProfileData();
            }

            GroupMembershipData MemberInfo = GetAgentGroupMembership(requestID, AgentID, GroupID);
            GroupProfileData MemberGroupProfile = GroupProfileHashtableToGroupProfileData(respData);

            MemberGroupProfile.MemberTitle = MemberInfo.GroupTitle;
            MemberGroupProfile.PowersMask = MemberInfo.GroupPowers;

            return MemberGroupProfile;

        }



        public void SetAgentActiveGroup(GroupRequestID requestID, UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            XmlRpcCall(requestID, "groups.setAgentActiveGroup", param);
        }

        public void SetAgentActiveGroupRole(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["SelectedRoleID"] = RoleID.ToString();

            XmlRpcCall(requestID, "groups.setAgentGroupInfo", param);
        }

        public void SetAgentGroupInfo(GroupRequestID requestID, UUID AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["AcceptNotices"] = AcceptNotices ? "1" : "0";
            param["ListInProfile"] = ListInProfile ? "1" : "0";

            XmlRpcCall(requestID, "groups.setAgentGroupInfo", param);

        }

        public void AddAgentToGroupInvite(GroupRequestID requestID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();
            param["AgentID"] = agentID.ToString();
            param["RoleID"] = roleID.ToString();
            param["GroupID"] = groupID.ToString();

            XmlRpcCall(requestID, "groups.addAgentToGroupInvite", param);

        }

        public GroupInviteInfo GetAgentToGroupInvite(GroupRequestID requestID, UUID inviteID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getAgentToGroupInvite", param);

            if (respData.Contains("error"))
            {
                return null;
            }

            GroupInviteInfo inviteInfo = new GroupInviteInfo();
            inviteInfo.InviteID = inviteID;
            inviteInfo.GroupID = UUID.Parse((string)respData["GroupID"]);
            inviteInfo.RoleID = UUID.Parse((string)respData["RoleID"]);
            inviteInfo.AgentID = UUID.Parse((string)respData["AgentID"]);

            return inviteInfo;
        }

        public void RemoveAgentToGroupInvite(GroupRequestID requestID, UUID inviteID)
        {
            Hashtable param = new Hashtable();
            param["InviteID"] = inviteID.ToString();

            XmlRpcCall(requestID, "groups.removeAgentToGroupInvite", param);
        }

        public void AddAgentToGroup(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            XmlRpcCall(requestID, "groups.addAgentToGroup", param);
        }

        public void RemoveAgentFromGroup(GroupRequestID requestID, UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            XmlRpcCall(requestID, "groups.removeAgentFromGroup", param);
        }

        public void AddAgentToGroupRole(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            XmlRpcCall(requestID, "groups.addAgentToGroupRole", param);
        }

        public void RemoveAgentFromGroupRole(GroupRequestID requestID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();
            param["RoleID"] = RoleID.ToString();

            XmlRpcCall(requestID, "groups.removeAgentFromGroupRole", param);
        }


        public List<DirGroupsReplyData> FindGroups(GroupRequestID requestID, string search)
        {
            Hashtable param = new Hashtable();
            param["Search"] = search;

            Hashtable respData = XmlRpcCall(requestID, "groups.findGroups", param);

            List<DirGroupsReplyData> findings = new List<DirGroupsReplyData>();

            if (!respData.Contains("error"))
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

        public GroupMembershipData GetAgentGroupMembership(GroupRequestID requestID, UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getAgentGroupMembership", param);

            if (respData.Contains("error"))
            {
                return null;
            }

            GroupMembershipData data = HashTableToGroupMembershipData(respData);

            return data;
        }

        public GroupMembershipData GetAgentActiveMembership(GroupRequestID requestID, UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getAgentActiveMembership", param);

            if (respData.Contains("error"))
            {
                return null;
            }

            return HashTableToGroupMembershipData(respData);
        }


        public List<GroupMembershipData> GetAgentGroupMemberships(GroupRequestID requestID, UUID AgentID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getAgentGroupMemberships", param);

            List<GroupMembershipData> memberships = new List<GroupMembershipData>();

            if (!respData.Contains("error"))
            {
                foreach (object membership in respData.Values)
                {
                    memberships.Add(HashTableToGroupMembershipData((Hashtable)membership));
                }
            }
            
            return memberships;
        }

        public List<GroupRolesData> GetAgentGroupRoles(GroupRequestID requestID, UUID AgentID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["AgentID"] = AgentID.ToString();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getAgentRoles", param);

            List<GroupRolesData> Roles = new List<GroupRolesData>();

            if (respData.Contains("error"))
            {
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

        public List<GroupRolesData> GetGroupRoles(GroupRequestID requestID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroupRoles", param);

            List<GroupRolesData> Roles = new List<GroupRolesData>();

            if (respData.Contains("error"))
            {
                return Roles;
            }

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



        public List<GroupMembersData> GetGroupMembers(GroupRequestID requestID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroupMembers", param);

            List<GroupMembersData> members = new List<GroupMembersData>();

            if (respData.Contains("error"))
            {
                return members;
            }

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

        public List<GroupRoleMembersData> GetGroupRoleMembers(GroupRequestID requestID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroupRoleMembers", param);

            List<GroupRoleMembersData> members = new List<GroupRoleMembersData>();

            if (!respData.Contains("error"))
            {
                foreach (Hashtable membership in respData.Values)
                {
                    GroupRoleMembersData data = new GroupRoleMembersData();

                    data.MemberID = new UUID((string)membership["AgentID"]);
                    data.RoleID = new UUID((string)membership["RoleID"]);

                    members.Add(data);
                }
            }
            return members;
        }

        public List<GroupNoticeData> GetGroupNotices(GroupRequestID requestID, UUID GroupID)
        {
            Hashtable param = new Hashtable();
            param["GroupID"] = GroupID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroupNotices", param);

            List<GroupNoticeData> values = new List<GroupNoticeData>();

            if (!respData.Contains("error"))
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
        public GroupNoticeInfo GetGroupNotice(GroupRequestID requestID, UUID noticeID)
        {
            Hashtable param = new Hashtable();
            param["NoticeID"] = noticeID.ToString();

            Hashtable respData = XmlRpcCall(requestID, "groups.getGroupNotice", param);


            if (respData.Contains("error"))
            {
                return null;
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
        public void AddGroupNotice(GroupRequestID requestID, UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket)
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

            XmlRpcCall(requestID, "groups.addGroupNotice", param);
        }
        #endregion

        #region XmlRpcHashtableMarshalling
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
            group.GroupID = UUID.Parse((string)groupProfile["GroupID"]);
            group.GroupName = groupProfile["Name"].ToString();
            if (groupProfile["Charter"] != null)
            {
                group.Charter = (string)groupProfile["Charter"];
            }
            group.ShowInList = ((string)groupProfile["ShowInList"]) == "1";
            group.GroupPicture = UUID.Parse((string)groupProfile["InsigniaID"]);
            group.MembershipFee = int.Parse((string)groupProfile["MembershipFee"]);
            group.OpenEnrollment = ((string)groupProfile["OpenEnrollment"]) == "1";
            group.AllowPublish = ((string)groupProfile["AllowPublish"]) == "1";
            group.MaturePublish = ((string)groupProfile["MaturePublish"]) == "1";
            group.FounderID = UUID.Parse((string)groupProfile["FounderID"]);
            group.OwnerRoleID = UUID.Parse((string)groupProfile["OwnerRoleID"]);

            return group;
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
            if (respData["Charter"] != null)
            {
                data.Charter = (string)respData["Charter"];
            }
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

        #endregion

        /// <summary>
        /// Encapsulate the XmlRpc call to standardize security and error handling.
        /// </summary>
        private Hashtable XmlRpcCall(GroupRequestID requestID, string function, Hashtable param)
        {
            if (requestID == null)
            {
                requestID = new GroupRequestID();
            }
            param.Add("RequestingAgentID", requestID.AgentID.ToString());
            param.Add("RequestingAgentUserService", requestID.UserServiceURL);
            param.Add("RequestingSessionID", requestID.SessionID.ToString());
            

            param.Add("ReadKey", m_groupReadKey);
            param.Add("WriteKey", m_groupWriteKey);


            IList parameters = new ArrayList();
            parameters.Add(param);

            ConfigurableKeepAliveXmlRpcRequest req;
            req = new ConfigurableKeepAliveXmlRpcRequest(function, parameters, m_disableKeepAlive);

            XmlRpcResponse resp = null;

            try
            {
                resp = req.Send(m_serviceURL, 10000);
            }
            catch (Exception e)
            {
                

                m_log.ErrorFormat("[XMLRPCGROUPDATA]: An error has occured while attempting to access the XmlRpcGroups server method: {0}", function);
                m_log.ErrorFormat("[XMLRPCGROUPDATA]: {0} ", e.ToString());

                foreach (string ResponseLine in req.RequestResponse.Split(new string[] { Environment.NewLine },StringSplitOptions.None))
                {
                    m_log.ErrorFormat("[XMLRPCGROUPDATA]: {0} ", ResponseLine);
                }

                foreach (string key in param.Keys)
                {
                    m_log.WarnFormat("[XMLRPCGROUPDATA]: {0} :: {1}", key, param[key].ToString());
                }

                Hashtable respData = new Hashtable();
                respData.Add("error", e.ToString());
                return respData;
            }

            if (resp.Value is Hashtable)
            {
                Hashtable respData = (Hashtable)resp.Value;
                if (respData.Contains("error") && !respData.Contains("succeed"))
                {
                    LogRespDataToConsoleError(respData);
                }

                return respData;
            }

            m_log.ErrorFormat("[XMLRPCGROUPDATA]: The XmlRpc server returned a {1} instead of a hashtable for {0}", function, resp.Value.GetType().ToString());

            if (resp.Value is ArrayList)
            {
                ArrayList al = (ArrayList)resp.Value;
                m_log.ErrorFormat("[XMLRPCGROUPDATA]: Contains {0} elements", al.Count);

                foreach (object o in al)
                {
                    m_log.ErrorFormat("[XMLRPCGROUPDATA]: {0} :: {1}", o.GetType().ToString(), o.ToString());
                }
            }
            else
            {
                m_log.ErrorFormat("[XMLRPCGROUPDATA]: Function returned: {0}", resp.Value.ToString());
            }

            Hashtable error = new Hashtable();
            error.Add("error", "invalid return value");
            return error;
        }

        private void LogRespDataToConsoleError(Hashtable respData)
        {
            m_log.Error("[XMLRPCGROUPDATA]: Error:");

            foreach (string key in respData.Keys)
            {
                m_log.ErrorFormat("[XMLRPCGROUPDATA]: Key: {0}", key);

                string[] lines = respData[key].ToString().Split(new char[] { '\n' });
                foreach (string line in lines)
                {
                    m_log.ErrorFormat("[XMLRPCGROUPDATA]: {0}", line);
                }

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

namespace Nwc.XmlRpc
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Xml;
    using System.Net;
    using System.Text;
    using System.Reflection;

    /// <summary>Class supporting the request side of an XML-RPC transaction.</summary>
    public class ConfigurableKeepAliveXmlRpcRequest : XmlRpcRequest
    {
        private Encoding _encoding = new ASCIIEncoding();
        private XmlRpcRequestSerializer _serializer = new XmlRpcRequestSerializer();
        private XmlRpcResponseDeserializer _deserializer = new XmlRpcResponseDeserializer();
        private bool _disableKeepAlive = true;

        public string RequestResponse = String.Empty;

        /// <summary>Instantiate an <c>XmlRpcRequest</c> for a specified method and parameters.</summary>
        /// <param name="methodName"><c>String</c> designating the <i>object.method</i> on the server the request
        /// should be directed to.</param>
        /// <param name="parameters"><c>ArrayList</c> of XML-RPC type parameters to invoke the request with.</param>
        public ConfigurableKeepAliveXmlRpcRequest(String methodName, IList parameters, bool disableKeepAlive)
        {
            MethodName = methodName;
            _params = parameters;
            _disableKeepAlive = disableKeepAlive;
        }

        /// <summary>Send the request to the server.</summary>
        /// <param name="url"><c>String</c> The url of the XML-RPC server.</param>
        /// <returns><c>XmlRpcResponse</c> The response generated.</returns>
        public XmlRpcResponse Send(String url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (request == null)
                throw new XmlRpcException(XmlRpcErrorCodes.TRANSPORT_ERROR,
                              XmlRpcErrorCodes.TRANSPORT_ERROR_MSG + ": Could not create request with " + url);
            request.Method = "POST";
            request.ContentType = "text/xml";
            request.AllowWriteStreamBuffering = true;
            request.KeepAlive = !_disableKeepAlive;

            Stream stream = request.GetRequestStream();
            XmlTextWriter xml = new XmlTextWriter(stream, _encoding);
            _serializer.Serialize(xml, this);
            xml.Flush();
            xml.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader input = new StreamReader(response.GetResponseStream());

            string inputXml = input.ReadToEnd();
            XmlRpcResponse resp;
            try
            {
                resp = (XmlRpcResponse)_deserializer.Deserialize(inputXml);
            }
            catch (Exception e)
            {
                RequestResponse = inputXml;
                throw e;
            }
            input.Close();
            response.Close();
            return resp;
        }
    }
}
