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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Services.Connectors;

namespace Robust.Tests
{
    [TestFixture]
    public class GridClient
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        [Test]
        public void Grid_001()
        {
            GridServicesConnector m_Connector = new GridServicesConnector(DemonServer.Address);

            GridRegion r1 = CreateRegion("Test Region 1", 1000, 1000);
            GridRegion r2 = CreateRegion("Test Region 2", 1001, 1000);
            GridRegion r3 = CreateRegion("Test Region 3", 1005, 1000);

            string msg = m_Connector.RegisterRegion(UUID.Zero, r1);
            Assert.AreEqual(msg, string.Empty, "Region 1 failed to register");

            msg = m_Connector.RegisterRegion(UUID.Zero, r2);
            Assert.AreEqual(msg, string.Empty, "Region 2 failed to register");

            msg = m_Connector.RegisterRegion(UUID.Zero, r3);
            Assert.AreEqual(msg, string.Empty, "Region 3 failed to register");

            bool success;
            success = m_Connector.DeregisterRegion(r3.RegionID);
            Assert.AreEqual(success, true, "Region 3 failed to deregister");

            msg = m_Connector.RegisterRegion(UUID.Zero, r3);
            Assert.AreEqual(msg, string.Empty, "Region 3 failed to re-register");

            List<GridRegion> regions = m_Connector.GetNeighbours(UUID.Zero, r1.RegionID);
            Assert.AreNotEqual(regions, null, "GetNeighbours of region 1 failed");
            Assert.AreEqual(regions.Count, 1, "Region 1 should have 1 neighbor");
            Assert.AreEqual(regions[0].RegionName, "Test Region 2", "Region 1 has the wrong neighbor");

            GridRegion region = m_Connector.GetRegionByUUID(UUID.Zero, r2.RegionID);
            Assert.AreNotEqual(region, null, "GetRegionByUUID for region 2 failed");
            Assert.AreEqual(region.RegionName, "Test Region 2", "GetRegionByUUID of region 2 returned wrong region");

            region = m_Connector.GetRegionByUUID(UUID.Zero, UUID.Random());
            Assert.AreEqual(region, null, "Region with randon id should not exist");

            region = m_Connector.GetRegionByName(UUID.Zero, r3.RegionName);
            Assert.AreNotEqual(region, null, "GetRegionByUUID for region 3 failed");
            Assert.AreEqual(region.RegionName, "Test Region 3", "GetRegionByUUID of region 3 returned wrong region");

            region = m_Connector.GetRegionByName(UUID.Zero, "Foo");
            Assert.AreEqual(region, null, "Region Foo should not exist");

            regions = m_Connector.GetRegionsByName(UUID.Zero, "Test", 10);
            Assert.AreNotEqual(regions, null, "GetRegionsByName failed");
            Assert.AreEqual(regions.Count, 3, "GetRegionsByName should return 3");

            regions = m_Connector.GetRegionRange(UUID.Zero, 
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(1002),
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(1002) );
            Assert.AreNotEqual(regions, null, "GetRegionRange failed");
            Assert.AreEqual(regions.Count, 2, "GetRegionRange should return 2");

            regions = m_Connector.GetRegionRange(UUID.Zero,
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(950),
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(950) );
            Assert.AreNotEqual(regions, null, "GetRegionRange (bis) failed");
            Assert.AreEqual(regions.Count, 0, "GetRegionRange (bis) should return 0");

            // Deregister them all
            success = m_Connector.DeregisterRegion(r1.RegionID);
            Assert.AreEqual(success, true, "Region 1 failed to deregister");

            success = m_Connector.DeregisterRegion(r2.RegionID);
            Assert.AreEqual(success, true, "Region 2 failed to deregister");

            success = m_Connector.DeregisterRegion(r3.RegionID);
            Assert.AreEqual(success, true, "Region 3 failed to deregister");
        }

        private static GridRegion CreateRegion(string name, uint xcell, uint ycell)
        {
            GridRegion region = new GridRegion(xcell, ycell);
            region.RegionName = name;
            region.RegionID = UUID.Random();
            region.ExternalHostName = "127.0.0.1";
            region.HttpPort = 9000;
            region.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 9000);
          
            return region;
        }
    }
}
