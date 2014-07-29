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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid;
using OpenSim.Region.Framework.Scenes;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid.Tests
{
    [TestFixture]
    public class GridConnectorsTests : OpenSimTestCase
    {
        LocalGridServicesConnector m_LocalConnector;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("GridService");
            config.Configs["Modules"].Set("GridServices", "LocalGridServicesConnector");
            config.Configs["GridService"].Set("LocalServiceModule", "OpenSim.Services.GridService.dll:GridService");
            config.Configs["GridService"].Set("StorageProvider", "OpenSim.Data.Null.dll");
            config.Configs["GridService"].Set("Region_Test_Region_1", "DefaultRegion");
            config.Configs["GridService"].Set("Region_Test_Region_2", "FallbackRegion");
            config.Configs["GridService"].Set("Region_Test_Region_3", "FallbackRegion");
            config.Configs["GridService"].Set("Region_Other_Region_4", "FallbackRegion");

            m_LocalConnector = new LocalGridServicesConnector(config);
        }

        /// <summary>
        /// Test region registration.
        /// </summary>
        [Test]
        public void TestRegisterRegion()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            // Create 4 regions
            GridRegion r1 = new GridRegion();
            r1.RegionName = "Test Region 1";
            r1.RegionID = new UUID(1);
            r1.RegionLocX = 1000 * (int)Constants.RegionSize;
            r1.RegionLocY = 1000 * (int)Constants.RegionSize;
            r1.ExternalHostName = "127.0.0.1";
            r1.HttpPort = 9001;
            r1.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
            Scene s = new Scene(new RegionInfo(), null);
            s.RegionInfo.RegionID = r1.RegionID;
            m_LocalConnector.AddRegion(s);
            
            GridRegion r2 = new GridRegion();
            r2.RegionName = "Test Region 2";
            r2.RegionID = new UUID(2);
            r2.RegionLocX = 1001 * (int)Constants.RegionSize;
            r2.RegionLocY = 1000 * (int)Constants.RegionSize;
            r2.ExternalHostName = "127.0.0.1";
            r2.HttpPort = 9002;
            r2.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
            s = new Scene(new RegionInfo(), null);
            s.RegionInfo.RegionID = r2.RegionID;
            m_LocalConnector.AddRegion(s);

            GridRegion r3 = new GridRegion();
            r3.RegionName = "Test Region 3";
            r3.RegionID = new UUID(3);
            r3.RegionLocX = 1005 * (int)Constants.RegionSize;
            r3.RegionLocY = 1000 * (int)Constants.RegionSize;
            r3.ExternalHostName = "127.0.0.1";
            r3.HttpPort = 9003;
            r3.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
            s = new Scene(new RegionInfo(), null);
            s.RegionInfo.RegionID = r3.RegionID;
            m_LocalConnector.AddRegion(s);

            GridRegion r4 = new GridRegion();
            r4.RegionName = "Other Region 4";
            r4.RegionID = new UUID(4);
            r4.RegionLocX = 1004 * (int)Constants.RegionSize;
            r4.RegionLocY = 1002 * (int)Constants.RegionSize;
            r4.ExternalHostName = "127.0.0.1";
            r4.HttpPort = 9004;
            r4.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
            s = new Scene(new RegionInfo(), null);
            s.RegionInfo.RegionID = r4.RegionID;
            m_LocalConnector.AddRegion(s);

            m_LocalConnector.RegisterRegion(UUID.Zero, r1);

            GridRegion result = m_LocalConnector.GetRegionByName(UUID.Zero, "Test");
            Assert.IsNull(result, "Retrieved GetRegionByName \"Test\" is not null");

            result = m_LocalConnector.GetRegionByName(UUID.Zero, "Test Region 1");
            Assert.IsNotNull(result, "Retrieved GetRegionByName is null");
            Assert.That(result.RegionName, Is.EqualTo("Test Region 1"), "Retrieved region's name does not match");

            m_LocalConnector.RegisterRegion(UUID.Zero, r2);
            m_LocalConnector.RegisterRegion(UUID.Zero, r3);
            m_LocalConnector.RegisterRegion(UUID.Zero, r4);

            result = m_LocalConnector.GetRegionByUUID(UUID.Zero, new UUID(1));
            Assert.IsNotNull(result, "Retrieved GetRegionByUUID is null");
            Assert.That(result.RegionID, Is.EqualTo(new UUID(1)), "Retrieved region's UUID does not match");

            result = m_LocalConnector.GetRegionByPosition(UUID.Zero, (int)Util.RegionToWorldLoc(1000), (int)Util.RegionToWorldLoc(1000));
            Assert.IsNotNull(result, "Retrieved GetRegionByPosition is null");
            Assert.That(result.RegionLocX, Is.EqualTo(1000 * (int)Constants.RegionSize), "Retrieved region's position does not match");

            List<GridRegion> results = m_LocalConnector.GetNeighbours(UUID.Zero, new UUID(1));
            Assert.IsNotNull(results, "Retrieved neighbours list is null");
            Assert.That(results.Count, Is.EqualTo(1), "Retrieved neighbour collection is greater than expected");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(2)), "Retrieved region's UUID does not match");

            results = m_LocalConnector.GetRegionsByName(UUID.Zero, "Test", 10);
            Assert.IsNotNull(results, "Retrieved GetRegionsByName collection is null");
            Assert.That(results.Count, Is.EqualTo(3), "Retrieved neighbour collection is less than expected");

            results = m_LocalConnector.GetRegionRange(UUID.Zero, 900 * (int)Constants.RegionSize, 1002 * (int)Constants.RegionSize,
                900 * (int)Constants.RegionSize, 1100 * (int)Constants.RegionSize);
            Assert.IsNotNull(results, "Retrieved GetRegionRange collection is null");
            Assert.That(results.Count, Is.EqualTo(2), "Retrieved neighbour collection is not the number expected");

            results = m_LocalConnector.GetDefaultRegions(UUID.Zero);
            Assert.IsNotNull(results, "Retrieved GetDefaultRegions collection is null");
            Assert.That(results.Count, Is.EqualTo(1), "Retrieved default regions collection has not the expected size");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(1)), "Retrieved default region's UUID does not match");

            results = m_LocalConnector.GetFallbackRegions(UUID.Zero, r1.RegionLocX, r1.RegionLocY);
            Assert.IsNotNull(results, "Retrieved GetFallbackRegions collection for region 1 is null");
            Assert.That(results.Count, Is.EqualTo(3), "Retrieved fallback regions collection for region 1 has not the expected size");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(2)), "Retrieved fallback regions for default region are not in the expected order 2-4-3");
            Assert.That(results[1].RegionID, Is.EqualTo(new UUID(4)), "Retrieved fallback regions for default region are not in the expected order 2-4-3");
            Assert.That(results[2].RegionID, Is.EqualTo(new UUID(3)), "Retrieved fallback regions for default region are not in the expected order 2-4-3");

            results = m_LocalConnector.GetFallbackRegions(UUID.Zero, r2.RegionLocX, r2.RegionLocY);
            Assert.IsNotNull(results, "Retrieved GetFallbackRegions collection for region 2 is null");
            Assert.That(results.Count, Is.EqualTo(3), "Retrieved fallback regions collection for region 2 has not the expected size");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(2)), "Retrieved fallback regions are not in the expected order 2-4-3");
            Assert.That(results[1].RegionID, Is.EqualTo(new UUID(4)), "Retrieved fallback regions are not in the expected order 2-4-3");
            Assert.That(results[2].RegionID, Is.EqualTo(new UUID(3)), "Retrieved fallback regions are not in the expected order 2-4-3");

            results = m_LocalConnector.GetFallbackRegions(UUID.Zero, r3.RegionLocX, r3.RegionLocY);
            Assert.IsNotNull(results, "Retrieved GetFallbackRegions collection for region 3 is null");
            Assert.That(results.Count, Is.EqualTo(3), "Retrieved fallback regions collection for region 3 has not the expected size");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(3)), "Retrieved fallback regions are not in the expected order 3-4-2");
            Assert.That(results[1].RegionID, Is.EqualTo(new UUID(4)), "Retrieved fallback regions are not in the expected order 3-4-2");
            Assert.That(results[2].RegionID, Is.EqualTo(new UUID(2)), "Retrieved fallback regions are not in the expected order 3-4-2");

            results = m_LocalConnector.GetFallbackRegions(UUID.Zero, r4.RegionLocX, r4.RegionLocY);
            Assert.IsNotNull(results, "Retrieved GetFallbackRegions collection for region 4 is null");
            Assert.That(results.Count, Is.EqualTo(3), "Retrieved fallback regions collection for region 4 has not the expected size");
            Assert.That(results[0].RegionID, Is.EqualTo(new UUID(4)), "Retrieved fallback regions are not in the expected order 4-3-2");
            Assert.That(results[1].RegionID, Is.EqualTo(new UUID(3)), "Retrieved fallback regions are not in the expected order 4-3-2");
            Assert.That(results[2].RegionID, Is.EqualTo(new UUID(2)), "Retrieved fallback regions are not in the expected order 4-3-2");

            results = m_LocalConnector.GetHyperlinks(UUID.Zero);
            Assert.IsNotNull(results, "Retrieved GetHyperlinks list is null");
            Assert.That(results.Count, Is.EqualTo(0), "Retrieved linked regions collection is not the number expected");
        }
    }
}