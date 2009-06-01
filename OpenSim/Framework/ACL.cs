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

namespace OpenSim.Framework
{
    // ACL Class
    // Modelled after the structure of the Zend ACL Framework Library
    // with one key difference - the tree will search for all matching
    // permissions rather than just the first. Deny permissions will
    // override all others.

    #region ACL Core Class

    /// <summary>
    /// Access Control List Engine
    /// </summary>
    public class ACL
    {
        private Dictionary<string, Resource> Resources = new Dictionary<string, Resource>();
        private Dictionary<string, Role> Roles = new Dictionary<string, Role>();

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

        public AlreadyContainsRoleException(Role role)
        {
            m_role = role;
        }

        public Role ErrorRole
        {
            get { return m_role; }
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
    public enum Permission
    {
        Deny,
        None,
        Allow
    } ;

    /// <summary>
    /// A role class, for use with Users or Groups
    /// </summary>
    public class Role
    {
        private string m_name;
        private Role[] m_parents;
        private Dictionary<string, Permission> m_resources = new Dictionary<string, Permission>();

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
    }

    public class Resource
    {
        private string m_name;

        public Resource(string name)
        {
            m_name = name;
        }

        public string Name
        {
            get { return m_name; }
        }
    }

    #endregion

    #region Tests

    internal class ACLTester
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