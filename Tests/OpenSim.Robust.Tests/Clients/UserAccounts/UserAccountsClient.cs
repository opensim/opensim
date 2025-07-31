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
using System.Text;
using System.Reflection;

using OpenMetaverse;
using NUnit.Framework;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

namespace Robust.Tests
{
    [TestFixture]
    public class UserAccountsClient
    {
        [Test]
        public void UserAccounts_001()
        {
            UserAccountServicesConnector m_Connector = new UserAccountServicesConnector(DemonServer.Address);

            string first = "Completely";
            string last = "Clueless";
            string email = "foo@bar.com";

            UserAccount account = m_Connector.CreateUser(first, last, "123", email, UUID.Zero);
            Assert.IsNotNull(account, "Failed to create account " + first + " " + last);
            UUID user1 = account.PrincipalID;

            account = m_Connector.GetUserAccount(UUID.Zero, user1);
            Assert.NotNull(account, "Failed to retrieve account for user id " + user1);
            Assert.AreEqual(account.FirstName, first, "First name does not match");
            Assert.AreEqual(account.LastName, last, "Last name does not match");

            account = m_Connector.GetUserAccount(UUID.Zero, first, last);
            Assert.IsNotNull(account, "Failed to retrieve account for user " + first + " " + last);
            Assert.AreEqual(account.FirstName, first, "First name does not match (bis)");
            Assert.AreEqual(account.LastName, last, "Last name does not match (bis)");

            account.Email = "user@example.com";
            bool success = m_Connector.StoreUserAccount(account);
            Assert.IsTrue(success, "Failed to store existing account");

            account = m_Connector.GetUserAccount(UUID.Zero, user1);
            Assert.NotNull(account, "Failed to retrieve account for user id " + user1);
            Assert.AreEqual(account.Email, "user@example.com", "Incorrect email");

            account = new UserAccount(UUID.Zero, "DoesNot", "Exist", "xxx@xxx.com");
            success = m_Connector.StoreUserAccount(account);
            Assert.IsFalse(success, "Storing a non-existing account must fail");

            account = m_Connector.GetUserAccount(UUID.Zero, "DoesNot", "Exist");
            Assert.IsNull(account, "Account DoesNot Exist must not be there");

        }

    }
}
