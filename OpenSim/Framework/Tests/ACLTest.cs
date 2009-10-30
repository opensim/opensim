using System;
using NUnit.Framework;


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

            acl.HasPermission("JoeGuest", "CanBuild");

        }

        #endregion
    }
}
