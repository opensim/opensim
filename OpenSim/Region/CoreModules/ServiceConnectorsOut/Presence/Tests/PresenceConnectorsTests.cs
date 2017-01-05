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
using System.IO;
using System.Reflection;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using Nini.Config;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Presence;
using OpenSim.Region.Framework.Scenes;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Presence.Tests
{
    [TestFixture]
    public class PresenceConnectorsTests : OpenSimTestCase
    {
        LocalPresenceServicesConnector m_LocalConnector;

        public override void SetUp()
        {
            base.SetUp();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("PresenceService");
            config.Configs["Modules"].Set("PresenceServices", "LocalPresenceServicesConnector");
            config.Configs["PresenceService"].Set("LocalServiceModule", "OpenSim.Services.PresenceService.dll:PresenceService");
            config.Configs["PresenceService"].Set("StorageProvider", "OpenSim.Data.Null.dll");

            m_LocalConnector = new LocalPresenceServicesConnector();
            m_LocalConnector.Initialise(config);

            // Let's stick in a test presence
            m_LocalConnector.m_PresenceService.LoginAgent(UUID.Zero.ToString(), UUID.Zero, UUID.Zero);
        }

        /// <summary>
        /// Test OpenSim Presence.
        /// </summary>
        [Test]
        public void TestPresenceV0_1()
        {
            SetUp();

                // Let's stick in a test presence
                /*
                PresenceData p = new PresenceData();
                p.SessionID = UUID.Zero;
                p.UserID = UUID.Zero.ToString();
                p.Data = new Dictionary<string, string>();
                p.Data["Online"] = true.ToString();
                m_presenceData.Add(UUID.Zero, p);
                */

            string user1 = UUID.Zero.ToString();
            UUID session1 = UUID.Zero;

            // this is not implemented by this connector
            //m_LocalConnector.LoginAgent(user1, session1, UUID.Zero);
            PresenceInfo result = m_LocalConnector.GetAgent(session1);
            Assert.IsNotNull(result, "Retrieved GetAgent is null");
            Assert.That(result.UserID, Is.EqualTo(user1), "Retrieved userID does not match");

            UUID region1 = UUID.Random();
            bool r = m_LocalConnector.ReportAgent(session1, region1);
            Assert.IsTrue(r, "First ReportAgent returned false");
            result = m_LocalConnector.GetAgent(session1);
            Assert.That(result.RegionID, Is.EqualTo(region1), "Agent is not in the right region (region1)");

            UUID region2 = UUID.Random();
            r = m_LocalConnector.ReportAgent(session1, region2);
            Assert.IsTrue(r, "Second ReportAgent returned false");
            result = m_LocalConnector.GetAgent(session1);
            Assert.That(result.RegionID, Is.EqualTo(region2), "Agent is not in the right region (region2)");

            r = m_LocalConnector.LogoutAgent(session1);
            Assert.IsTrue(r, "LogoutAgent returned false");
            result = m_LocalConnector.GetAgent(session1);
            Assert.IsNull(result, "Agent session is still stored after logout");

            r = m_LocalConnector.ReportAgent(session1, region1);
            Assert.IsFalse(r, "ReportAgent of non-logged in user returned true");
        }
    }
}
