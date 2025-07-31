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
    public class PresenceClient
    {
        [Test]
        public void Presence_001()
        {
            PresenceServicesConnector m_Connector = new PresenceServicesConnector(DemonServer.Address);

            UUID user1 = UUID.Random();
            UUID session1 = UUID.Random();
            UUID region1 = UUID.Random();

            bool success = m_Connector.LoginAgent(user1.ToString(), session1, UUID.Zero);
            Assert.AreEqual(success, true, "Failed to add user session");

            PresenceInfo pinfo = m_Connector.GetAgent(session1);
            Assert.AreNotEqual(pinfo, null, "Unable to retrieve session");
            Assert.AreEqual(pinfo.UserID, user1.ToString(), "Retrieved session does not match expected userID");
            Assert.AreNotEqual(pinfo.RegionID, region1, "Retrieved session is unexpectedly in region");

            success = m_Connector.ReportAgent(session1, region1);
            Assert.AreEqual(success, true, "Failed to report session in region 1");

            pinfo = m_Connector.GetAgent(session1);
            Assert.AreNotEqual(pinfo, null, "Unable to session presence");
            Assert.AreEqual(pinfo.UserID, user1.ToString(), "Retrieved session does not match expected userID");
            Assert.AreEqual(pinfo.RegionID, region1, "Retrieved session is not in expected region");

            success = m_Connector.LogoutAgent(session1);
            Assert.AreEqual(success, true, "Failed to remove session");

            pinfo = m_Connector.GetAgent(session1);
            Assert.AreEqual(pinfo, null, "Session is still there, even though it shouldn't");

            success = m_Connector.ReportAgent(session1, UUID.Random());
            Assert.AreEqual(success, false, "Remove non-existing session should fail");
        }

    }
}
