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
