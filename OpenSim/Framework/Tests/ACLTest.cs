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
using NUnit.Framework;
using System.Collections.Generic;


namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class ACLTest
    {
        #region Tests

        /// <summary>
        /// ACL Test class
        /// </summary>
        [Test]
        public void ACLTest01()
        {
            ACL acl = new ACL();

            Role Guests = new Role("Guests");
            acl.AddRole(Guests);

            Role[] parents = new Role[1];
            parents[0] = Guests;

            Role JoeGuest = new Role("JoeGuest", parents);
            acl.AddRole(JoeGuest);

            Resource CanBuild = new Resource("CanBuild");
            acl.AddResource(CanBuild);


            acl.GrantPermission("Guests", "CanBuild");

            Permission perm = acl.HasPermission("JoeGuest", "CanBuild");
            Assert.That(perm == Permission.Allow, "JoeGuest should have permission to build");
            perm = Permission.None;
            try
            {
                perm = acl.HasPermission("unknownGuest", "CanBuild");
                
            }
            catch (KeyNotFoundException)
            {
                
               
            }
            catch (Exception)
            {
                Assert.That(false,"Exception thrown should have been KeyNotFoundException");
            }
            Assert.That(perm == Permission.None,"Permission None should be set because exception should have been thrown");
            
        }

        [Test]
        public void KnownButPermissionDenyAndPermissionNoneUserTest()
        {
            ACL acl = new ACL();

            Role Guests = new Role("Guests");
            acl.AddRole(Guests);
            Role Administrators = new Role("Administrators");
            acl.AddRole(Administrators);
            Role[] Guestparents = new Role[1];
            Role[] Adminparents = new Role[1];

            Guestparents[0] = Guests;
            Adminparents[0] = Administrators;

            Role JoeGuest = new Role("JoeGuest", Guestparents);
            acl.AddRole(JoeGuest);

            Resource CanBuild = new Resource("CanBuild");
            acl.AddResource(CanBuild);

            Resource CanScript = new Resource("CanScript");
            acl.AddResource(CanScript);

            Resource CanRestart = new Resource("CanRestart");
            acl.AddResource(CanRestart);

            acl.GrantPermission("Guests", "CanBuild");
            acl.DenyPermission("Guests", "CanRestart");

            acl.GrantPermission("Administrators", "CanScript");

            acl.GrantPermission("Administrators", "CanRestart");
            Permission setPermission = acl.HasPermission("JoeGuest", "CanRestart");
            Assert.That(setPermission == Permission.Deny, "Guests Should not be able to restart");
            Assert.That(acl.HasPermission("JoeGuest", "CanScript") == Permission.None,
                        "No Explicit Permissions set so should be Permission.None");
        }

        #endregion
    }
}
