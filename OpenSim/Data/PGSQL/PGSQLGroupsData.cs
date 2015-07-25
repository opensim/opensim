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
using OpenSim.Framework;
using OpenMetaverse;
using log4net;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLGroupsData : IGroupsData
    {        
        private PGSqlGroupsGroupsHandler m_Groups;
        private PGSqlGroupsMembershipHandler m_Membership;
        private PGSqlGroupsRolesHandler m_Roles;
        private PGSqlGroupsRoleMembershipHandler m_RoleMembership;
        private PGSqlGroupsInvitesHandler m_Invites;
        private PGSqlGroupsNoticesHandler m_Notices;
        private PGSqlGroupsPrincipalsHandler m_Principals;

        public PGSQLGroupsData(string connectionString, string realm)
        {
            m_Groups = new PGSqlGroupsGroupsHandler(connectionString, realm + "_groups", realm + "_Store");
            m_Membership = new PGSqlGroupsMembershipHandler(connectionString, realm + "_membership");
            m_Roles = new PGSqlGroupsRolesHandler(connectionString, realm + "_roles");
            m_RoleMembership = new PGSqlGroupsRoleMembershipHandler(connectionString, realm + "_rolemembership");
            m_Invites = new PGSqlGroupsInvitesHandler(connectionString, realm + "_invites");
            m_Notices = new PGSqlGroupsNoticesHandler(connectionString, realm + "_notices");
            m_Principals = new PGSqlGroupsPrincipalsHandler(connectionString, realm + "_principals");
        }

        #region groups table
        public bool StoreGroup(GroupData data)
        {
            return m_Groups.Store(data);
        }

        public GroupData RetrieveGroup(UUID groupID)
        {
            GroupData[] groups = m_Groups.Get("GroupID", groupID.ToString());
            if (groups.Length > 0)
                return groups[0];

            return null;
        }

        public GroupData RetrieveGroup(string name)
        {
            GroupData[] groups = m_Groups.Get("Name", name);
            if (groups.Length > 0)
                return groups[0];

            return null;
        }

        public GroupData[] RetrieveGroups(string pattern)
        {
                       
            if (string.IsNullOrEmpty(pattern)) // True for where clause
            {
                pattern = " 1 ORDER BY lower(\"Name\") LIMIT 100";
                
                return m_Groups.Get(pattern);
            }
            else   
            {             
                pattern = " \"ShowInList\" = 1 AND lower(\"Name\") LIKE lower('%" + pattern + "%') ORDER BY lower(\"Name\") LIMIT 100";
        
                return m_Groups.Get(pattern, new NpgsqlParameter("pattern", pattern));
            }
        }

        public bool DeleteGroup(UUID groupID)
        {
            return m_Groups.Delete("GroupID", groupID.ToString());
        }

        public int GroupsCount()
        {
            return (int)m_Groups.GetCount(" \"Location\" = \"\"");
        }

        #endregion

        #region membership table
        public MembershipData[] RetrieveMembers(UUID groupID)
        {
            return m_Membership.Get("GroupID", groupID.ToString());
        }

        public MembershipData RetrieveMember(UUID groupID, string pricipalID)
        {
            MembershipData[] m = m_Membership.Get(new string[] { "GroupID", "PrincipalID" },
                                                  new string[] { groupID.ToString(), pricipalID });
            if (m != null && m.Length > 0)
                return m[0];

            return null;
        }

        public MembershipData[] RetrieveMemberships(string pricipalID)
        {
            return m_Membership.Get("PrincipalID", pricipalID.ToString());
        }

        public bool StoreMember(MembershipData data)
        {
            return m_Membership.Store(data);
        }

        public bool DeleteMember(UUID groupID, string pricipalID)
        {
            return m_Membership.Delete(new string[] { "GroupID", "PrincipalID" }, 
                                       new string[] { groupID.ToString(), pricipalID });
        }
        
        public int MemberCount(UUID groupID)
        {
            return (int)m_Membership.GetCount("GroupID", groupID.ToString());
        }
        #endregion

        #region roles table
        public bool StoreRole(RoleData data)
        {
            return m_Roles.Store(data);
        }

        public RoleData RetrieveRole(UUID groupID, UUID roleID)
        {
            RoleData[] data = m_Roles.Get(new string[] { "GroupID", "RoleID" },
                                          new string[] { groupID.ToString(), roleID.ToString() });

            if (data != null && data.Length > 0)
                return data[0];

            return null;
        }

        public RoleData[] RetrieveRoles(UUID groupID)
        {
            //return m_Roles.RetrieveRoles(groupID);
            return m_Roles.Get("GroupID", groupID.ToString());
        }

        public bool DeleteRole(UUID groupID, UUID roleID)
        {
            return m_Roles.Delete(new string[] { "GroupID", "RoleID" }, 
                                  new string[] { groupID.ToString(), roleID.ToString() });
        }

        public int RoleCount(UUID groupID)
        {
            return (int)m_Roles.GetCount("GroupID", groupID.ToString());
        }


        #endregion

        #region rolememberhip table
        public RoleMembershipData[] RetrieveRolesMembers(UUID groupID)
        {
            RoleMembershipData[] data = m_RoleMembership.Get("GroupID", groupID.ToString());

            return data;
        }

        public RoleMembershipData[] RetrieveRoleMembers(UUID groupID, UUID roleID)
        {
            RoleMembershipData[] data = m_RoleMembership.Get(new string[] { "GroupID", "RoleID" },
                                                             new string[] { groupID.ToString(), roleID.ToString() });

            return data;
        }

        public RoleMembershipData[] RetrieveMemberRoles(UUID groupID, string principalID)
        {
            RoleMembershipData[] data = m_RoleMembership.Get(new string[] { "GroupID", "PrincipalID" },
                                                             new string[] { groupID.ToString(), principalID.ToString() });

            return data;
        }

        public RoleMembershipData RetrieveRoleMember(UUID groupID, UUID roleID, string principalID)
        {
            RoleMembershipData[] data = m_RoleMembership.Get(new string[] { "GroupID", "RoleID", "PrincipalID" },
                                                             new string[] { groupID.ToString(), roleID.ToString(), principalID.ToString() });

            if (data != null && data.Length > 0)
                return data[0];

            return null;
        }

        public int RoleMemberCount(UUID groupID, UUID roleID)
        {
            return (int)m_RoleMembership.GetCount(new string[] { "GroupID", "RoleID" },
                                                  new string[] { groupID.ToString(), roleID.ToString() });
        }

        public bool StoreRoleMember(RoleMembershipData data)
        {
            return m_RoleMembership.Store(data);
        }

        public bool DeleteRoleMember(RoleMembershipData data)
        {
            return m_RoleMembership.Delete(new string[] { "GroupID", "RoleID", "PrincipalID"},
                                           new string[] { data.GroupID.ToString(), data.RoleID.ToString(), data.PrincipalID });
        }

        public bool DeleteMemberAllRoles(UUID groupID, string principalID)
        {
            return m_RoleMembership.Delete(new string[] { "GroupID", "PrincipalID" },
                                           new string[] { groupID.ToString(), principalID });
        }

        #endregion

        #region principals table
        public bool StorePrincipal(PrincipalData data)
        {
            return m_Principals.Store(data);
        }

        public PrincipalData RetrievePrincipal(string principalID)
        {
            PrincipalData[] p = m_Principals.Get("PrincipalID", principalID);
            if (p != null && p.Length > 0)
                return p[0];

            return null;
        }

        public bool DeletePrincipal(string principalID)
        {
            return m_Principals.Delete("PrincipalID", principalID);
        }
        #endregion

        #region invites table

        public bool StoreInvitation(InvitationData data)
        {
            return m_Invites.Store(data);
        }

        public InvitationData RetrieveInvitation(UUID inviteID)
        {
            InvitationData[] invites = m_Invites.Get("InviteID", inviteID.ToString());

            if (invites != null && invites.Length > 0)
                return invites[0];

            return null;
        }

        public InvitationData RetrieveInvitation(UUID groupID, string principalID)
        {
            InvitationData[] invites = m_Invites.Get(new string[] { "GroupID", "PrincipalID" },
                                                     new string[] { groupID.ToString(), principalID });

            if (invites != null && invites.Length > 0)
                return invites[0];

            return null;
        }

        public bool DeleteInvite(UUID inviteID)
        {
            return m_Invites.Delete("InviteID", inviteID.ToString());
        }

        public void DeleteOldInvites()
        {
            m_Invites.DeleteOld();
        }

        #endregion

        #region notices table

        public bool StoreNotice(NoticeData data)
        {
            return m_Notices.Store(data);
        }

        public NoticeData RetrieveNotice(UUID noticeID)
        {
            NoticeData[] notices = m_Notices.Get("NoticeID", noticeID.ToString());

            if (notices != null && notices.Length > 0)
                return notices[0];

            return null;
        }

        public NoticeData[] RetrieveNotices(UUID groupID)
        {
            NoticeData[] notices = m_Notices.Get("GroupID", groupID.ToString());

            return notices;
        }

        public bool DeleteNotice(UUID noticeID)
        {
            return m_Notices.Delete("NoticeID", noticeID.ToString());
        }

        public void DeleteOldNotices()
        {
            m_Notices.DeleteOld();
        }

        #endregion

        #region combinations
        public MembershipData RetrievePrincipalGroupMembership(string principalID, UUID groupID)
        {
            // TODO
            return null;
        }
        public MembershipData[] RetrievePrincipalGroupMemberships(string principalID)
        {
            // TODO
            return null;
        }

        #endregion
    }

    public class PGSqlGroupsGroupsHandler : PGSQLGenericTableHandler<GroupData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsGroupsHandler(string connectionString, string realm, string store) 
            : base(connectionString, realm, store)
        {
        }

    }

    public class PGSqlGroupsMembershipHandler : PGSQLGenericTableHandler<MembershipData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsMembershipHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }

    }

    public class PGSqlGroupsRolesHandler : PGSQLGenericTableHandler<RoleData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsRolesHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }

    }

    public class PGSqlGroupsRoleMembershipHandler : PGSQLGenericTableHandler<RoleMembershipData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsRoleMembershipHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }

    }

    public class PGSqlGroupsInvitesHandler : PGSQLGenericTableHandler<InvitationData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsInvitesHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }

        public void DeleteOld()
        {

            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where \"TMStamp\" < CURRENT_DATE - INTERVAL '2 week'", m_Realm);
                
                ExecuteNonQuery(cmd);
            }

        }
    }

    public class PGSqlGroupsNoticesHandler : PGSQLGenericTableHandler<NoticeData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsNoticesHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }

        public void DeleteOld()
        {

            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where \"TMStamp\" < CURRENT_DATE - INTERVAL '2 week'", m_Realm);
                
                ExecuteNonQuery(cmd);
            }

        }
    }

    public class PGSqlGroupsPrincipalsHandler : PGSQLGenericTableHandler<PrincipalData>
    {
        protected override Assembly Assembly
        {
            // WARNING! Moving migrations to this assembly!!!
            get { return GetType().Assembly; }
        }

        public PGSqlGroupsPrincipalsHandler(string connectionString, string realm) 
            : base(connectionString, realm, string.Empty)
        {
        }
    }
}
