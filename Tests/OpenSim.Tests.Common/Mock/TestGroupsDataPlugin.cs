using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenMetaverse;
using OpenSim.Data;

namespace OpenSim.Tests.Common.Mock
{
    public class TestGroupsDataPlugin : IGroupsData
    {
        class CompositeKey
        {
            private readonly string _key;
            public string Key
            {
                get { return _key;  }
            }

            public CompositeKey(UUID _k1, string _k2)
            {
                _key = _k1.ToString() + _k2;
            }

            public CompositeKey(UUID _k1, string _k2, string _k3)
            {
                _key = _k1.ToString() + _k2 + _k3;
            }

            public override bool Equals(object obj)
            {
                if (obj is CompositeKey)
                {
                    return Key == ((CompositeKey)obj).Key;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return Key;
            }
        }

        private Dictionary<UUID, GroupData> m_Groups;
        private Dictionary<CompositeKey, MembershipData> m_Membership;
        private Dictionary<CompositeKey, RoleData> m_Roles;
        private Dictionary<CompositeKey, RoleMembershipData> m_RoleMembership;
        private Dictionary<UUID, InvitationData> m_Invites;
        private Dictionary<UUID, NoticeData> m_Notices;
        private Dictionary<string, PrincipalData> m_Principals;

        public TestGroupsDataPlugin(string connectionString, string realm)
        {
            m_Groups = new Dictionary<UUID, GroupData>();
            m_Membership = new Dictionary<CompositeKey, MembershipData>();
            m_Roles = new Dictionary<CompositeKey, RoleData>();
            m_RoleMembership = new Dictionary<CompositeKey, RoleMembershipData>();
            m_Invites = new Dictionary<UUID, InvitationData>();
            m_Notices = new Dictionary<UUID, NoticeData>();
            m_Principals = new Dictionary<string, PrincipalData>();
        }

        #region groups table
        public bool StoreGroup(GroupData data)
        {
            return false;
        }

        public GroupData RetrieveGroup(UUID groupID)
        {
            if (m_Groups.ContainsKey(groupID))
                return m_Groups[groupID];

            return null;
        }

        public GroupData RetrieveGroup(string name)
        {
            return m_Groups.Values.First(g => g.Data.ContainsKey("Name") && g.Data["Name"] == name);
        }

        public GroupData[] RetrieveGroups(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                pattern = "1";

            IEnumerable<GroupData> groups = m_Groups.Values.Where(g => g.Data.ContainsKey("Name") && (g.Data["Name"].StartsWith(pattern) || g.Data["Name"].EndsWith(pattern)));

            return (groups != null) ? groups.ToArray() : new GroupData[0];
        }

        public bool DeleteGroup(UUID groupID)
        {
            return m_Groups.Remove(groupID);
        }

        public int GroupsCount()
        {
            return m_Groups.Count;
        }
        #endregion

        #region membership table
        public MembershipData RetrieveMember(UUID groupID, string pricipalID)
        {
            CompositeKey dkey = new CompositeKey(groupID, pricipalID);
            if (m_Membership.ContainsKey(dkey))
                return m_Membership[dkey];

            return null;
        }

        public MembershipData[] RetrieveMembers(UUID groupID)
        {
            IEnumerable<CompositeKey> keys = m_Membership.Keys.Where(k => k.Key.StartsWith(groupID.ToString()));
            return keys.Where(m_Membership.ContainsKey).Select(x => m_Membership[x]).ToArray();
        }

        public MembershipData[] RetrieveMemberships(string principalID)
        {
            IEnumerable<CompositeKey> keys = m_Membership.Keys.Where(k => k.Key.EndsWith(principalID.ToString()));
            return keys.Where(m_Membership.ContainsKey).Select(x => m_Membership[x]).ToArray();
        }

        public MembershipData[] RetrievePrincipalGroupMemberships(string principalID)
        {
            return RetrieveMemberships(principalID);
        }

        public MembershipData RetrievePrincipalGroupMembership(string principalID, UUID groupID)
        {
            CompositeKey dkey = new CompositeKey(groupID, principalID);
            if (m_Membership.ContainsKey(dkey))
                return m_Membership[dkey];
            return null;
        }

        public bool StoreMember(MembershipData data)
        {
            CompositeKey dkey = new CompositeKey(data.GroupID, data.PrincipalID);
            m_Membership[dkey] = data;
            return true;
        }

        public bool DeleteMember(UUID groupID, string principalID)
        {
            CompositeKey dkey = new CompositeKey(groupID, principalID);
            if (m_Membership.ContainsKey(dkey))
                return m_Membership.Remove(dkey);

            return false;
        }

        public int MemberCount(UUID groupID)
        {
            return m_Membership.Count;
        }
        #endregion

        #region roles table
        public bool StoreRole(RoleData data)
        {
            CompositeKey dkey = new CompositeKey(data.GroupID, data.RoleID.ToString());
            m_Roles[dkey] = data;
            return true;
        }

        public RoleData RetrieveRole(UUID groupID, UUID roleID)
        {
            CompositeKey dkey = new CompositeKey(groupID, roleID.ToString());
            if (m_Roles.ContainsKey(dkey))
                return m_Roles[dkey];

            return null;
        }

        public RoleData[] RetrieveRoles(UUID groupID)
        {
            IEnumerable<CompositeKey> keys = m_Roles.Keys.Where(k => k.Key.StartsWith(groupID.ToString()));
            return keys.Where(m_Roles.ContainsKey).Select(x => m_Roles[x]).ToArray();
        }

        public bool DeleteRole(UUID groupID, UUID roleID)
        {
            CompositeKey dkey = new CompositeKey(groupID, roleID.ToString());
            if (m_Roles.ContainsKey(dkey))
                return m_Roles.Remove(dkey);

            return false;
        }

        public int RoleCount(UUID groupID)
        {
            return m_Roles.Count;
        }
        #endregion

        #region rolememberhip table
        public RoleMembershipData[] RetrieveRolesMembers(UUID groupID)
        {
            IEnumerable<CompositeKey> keys = m_Roles.Keys.Where(k => k.Key.StartsWith(groupID.ToString()));
            return keys.Where(m_RoleMembership.ContainsKey).Select(x => m_RoleMembership[x]).ToArray();
        }

        public RoleMembershipData[] RetrieveRoleMembers(UUID groupID, UUID roleID)
        {
            IEnumerable<CompositeKey> keys = m_Roles.Keys.Where(k => k.Key.StartsWith(groupID.ToString() + roleID.ToString()));
            return keys.Where(m_RoleMembership.ContainsKey).Select(x => m_RoleMembership[x]).ToArray();
        }

        public RoleMembershipData[] RetrieveMemberRoles(UUID groupID, string principalID)
        {
            IEnumerable<CompositeKey> keys = m_Roles.Keys.Where(k => k.Key.StartsWith(groupID.ToString()) && k.Key.EndsWith(principalID));
            return keys.Where(m_RoleMembership.ContainsKey).Select(x => m_RoleMembership[x]).ToArray();
        }

        public RoleMembershipData RetrieveRoleMember(UUID groupID, UUID roleID, string principalID)
        {
            CompositeKey dkey = new CompositeKey(groupID, roleID.ToString(), principalID);
            if (m_RoleMembership.ContainsKey(dkey))
                return m_RoleMembership[dkey];

            return null;
        }

        public int RoleMemberCount(UUID groupID, UUID roleID)
        {
            return m_RoleMembership.Count;
        }

        public bool StoreRoleMember(RoleMembershipData data)
        {
            CompositeKey dkey = new CompositeKey(data.GroupID, data.RoleID.ToString(), data.PrincipalID);
            m_RoleMembership[dkey] = data;
            return true;
        }

        public bool DeleteRoleMember(RoleMembershipData data)
        {
            CompositeKey dkey = new CompositeKey(data.GroupID, data.RoleID.ToString(), data.PrincipalID);
            if (m_RoleMembership.ContainsKey(dkey))
                return m_RoleMembership.Remove(dkey);

            return false;
        }

        public bool DeleteMemberAllRoles(UUID groupID, string principalID)
        {
            List<CompositeKey> keys = m_RoleMembership.Keys.Where(k => k.Key.StartsWith(groupID.ToString()) && k.Key.EndsWith(principalID)).ToList();
            foreach (CompositeKey k in keys)
                m_RoleMembership.Remove(k);
            return true;
        }
        #endregion

        #region principals table
        public bool StorePrincipal(PrincipalData data)
        {
            m_Principals[data.PrincipalID] = data;
            return true;
        }

        public PrincipalData RetrievePrincipal(string principalID)
        {
            if (m_Principals.ContainsKey(principalID))
                return m_Principals[principalID];

            return null;
        }

        public bool DeletePrincipal(string principalID)
        {
            if (m_Principals.ContainsKey(principalID))
                return m_Principals.Remove(principalID);
            return false;
        }
        #endregion

        #region invites table
        public bool StoreInvitation(InvitationData data)
        {
            return false;
        }

        public InvitationData RetrieveInvitation(UUID inviteID)
        {
            return null;
        }

        public InvitationData RetrieveInvitation(UUID groupID, string principalID)
        {
            return null;
        }

        public bool DeleteInvite(UUID inviteID)
        {
            return false;
        }

        public void DeleteOldInvites()
        {
        }
        #endregion

        #region notices table
        public bool StoreNotice(NoticeData data)
        {
            return false;
        }

        public NoticeData RetrieveNotice(UUID noticeID)
        {
            return null;
        }

        public NoticeData[] RetrieveNotices(UUID groupID)
        {
            return new NoticeData[0];
        }

        public bool DeleteNotice(UUID noticeID)
        {
            return false;
        }

        public void DeleteOldNotices()
        {
        }
        #endregion

    }
}
