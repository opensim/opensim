using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.PolicyManager
{
    #region ACL Core Class
    /// <summary>
    /// Access Control List Engine
    /// </summary>
    public class ACL
    {
        Dictionary<string, Role> Roles = new Dictionary<string, Role>();
        Dictionary<string, Resource> Resources = new Dictionary<string, Resource>();

        public ACL AddRole(Role role)
        {
            if (Roles.ContainsKey(role.Name))
                throw new AlreadyContainsRoleException(role);

            Roles.Add(role.Name, role);

            return this;
        }

        public ACL AddResource(Resource resource)
        {
            Resources.Add(resource.Name, resource);

            return this;
        }

        public Permission HasPermission(string role, string resource)
        {
            if (!Roles.ContainsKey(role))
                throw new KeyNotFoundException();

            if (!Resources.ContainsKey(resource))
                throw new KeyNotFoundException();

            return Roles[role].RequestPermission(resource);
        }

        public ACL GrantPermission(string role, string resource)
        {
            if (!Roles.ContainsKey(role))
                throw new KeyNotFoundException();

            if (!Resources.ContainsKey(resource))
                throw new KeyNotFoundException();

            Roles[role].GivePermission(resource, Permission.Allow);

            return this;
        }

        public ACL DenyPermission(string role, string resource)
        {
            if (!Roles.ContainsKey(role))
                throw new KeyNotFoundException();

            if (!Resources.ContainsKey(resource))
                throw new KeyNotFoundException();

            Roles[role].GivePermission(resource, Permission.Deny);

            return this;
        }

        public ACL ResetPermission(string role, string resource)
        {
            if (!Roles.ContainsKey(role))
                throw new KeyNotFoundException();

            if (!Resources.ContainsKey(resource))
                throw new KeyNotFoundException();

            Roles[role].GivePermission(resource, Permission.None);

            return this;
        }
    }
    #endregion

    #region Exceptions
    /// <summary>
    /// Thrown when an ACL attempts to add a duplicate role.
    /// </summary>
    public class AlreadyContainsRoleException : Exception
    {
        protected Role m_role;

        public Role ErrorRole
        {
            get { return m_role; }
        }

        public AlreadyContainsRoleException(Role role)
        {
            m_role = role;
        }

        public override string ToString()
        {
            return "This ACL already contains a role called '" + m_role.Name + "'.";
        }
    }
    #endregion

    #region Roles and Resources

    /// <summary>
    /// Does this Role have permission to access a specified Resource?
    /// </summary>
    public enum Permission { Deny, None, Allow };

    /// <summary>
    /// A role class, for use with Users or Groups
    /// </summary>
    public class Role
    {
        private string m_name;
        private Role[] m_parents;
        private Dictionary<string, Permission> m_resources = new Dictionary<string, Permission>();

        public string Name
        {
            get { return m_name; }
        }

        public Permission RequestPermission(string resource)
        {
            return RequestPermission(resource, Permission.None);
        }

        public Permission RequestPermission(string resource, Permission current)
        {
            // Deny permissions always override any others
            if (current == Permission.Deny)
                return current;

            Permission temp = Permission.None;

            // Pickup non-None permissions
            if (m_resources.ContainsKey(resource) && m_resources[resource] != Permission.None)
                temp = m_resources[resource];

            if (m_parents != null)
            {
                foreach (Role parent in m_parents)
                {
                    temp = parent.RequestPermission(resource, temp);
                }
            }

            return temp;
        }

        public void GivePermission(string resource, Permission perm)
        {
            m_resources[resource] = perm;
        }

        public Role(string name)
        {
            m_name = name;
            m_parents = null;
        }

        public Role(string name, Role[] parents)
        {
            m_name = name;
            m_parents = parents;
        }
    }

    public class Resource
    {
        private string m_name;

        public string Name
        {
            get { return m_name; }
        }

        public Resource(string name)
        {
            m_name = name;
        }
    }

    #endregion

    #region Tests

    class ACLTester
    {
        public ACLTester()
        {
            ACL acl = new ACL();

            Role Guests = new Role("Guests");
            acl.AddRole(Guests);

            Role[] parents = new Role[0];
            parents[0] = Guests;

            Role JoeGuest = new Role("JoeGuest", parents);
            acl.AddRole(JoeGuest);

            Resource CanBuild = new Resource("CanBuild");
            acl.AddResource(CanBuild);


            acl.GrantPermission("Guests", "CanBuild");

            acl.HasPermission("JoeGuest", "CanBuild");

        }
    }

    #endregion
}
