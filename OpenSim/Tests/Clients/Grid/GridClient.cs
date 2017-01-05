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
using log4net;
using log4net.Appender;
using log4net.Layout;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Services.Connectors;

namespace OpenSim.Tests.Clients.GridClient
{
    public class GridClient
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        public static void Main(string[] args)
        {
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout =
                new PatternLayout("%date [%thread] %-5level %logger [%property{NDC}] - %message%newline");
            log4net.Config.BasicConfigurator.Configure(consoleAppender);

            string serverURI = "http://127.0.0.1:8001";
            GridServicesConnector m_Connector = new GridServicesConnector(serverURI);

            GridRegion r1 = CreateRegion("Test Region 1", 1000, 1000);
            GridRegion r2 = CreateRegion("Test Region 2", 1001, 1000);
            GridRegion r3 = CreateRegion("Test Region 3", 1005, 1000);

            Console.WriteLine("[GRID CLIENT]: *** Registering region 1");
            string msg = m_Connector.RegisterRegion(UUID.Zero, r1);
            if (msg == String.Empty)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 1");
            else
                Console.WriteLine("[GRID CLIENT]: region 1 failed to register");

            Console.WriteLine("[GRID CLIENT]: *** Registering region 2");
            msg = m_Connector.RegisterRegion(UUID.Zero, r2);
            if (msg == String.Empty)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 2");
            else
                Console.WriteLine("[GRID CLIENT]: region 2 failed to register");

            Console.WriteLine("[GRID CLIENT]: *** Registering region 3");
            msg = m_Connector.RegisterRegion(UUID.Zero, r3);
            if (msg == String.Empty)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to register");


            bool success;
            Console.WriteLine("[GRID CLIENT]: *** Deregistering region 3");
            success = m_Connector.DeregisterRegion(r3.RegionID);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully deregistered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to deregister");
            Console.WriteLine("[GRID CLIENT]: *** Registering region 3 again");
            msg = m_Connector.RegisterRegion(UUID.Zero, r3);
            if (msg == String.Empty)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to register");

            Console.WriteLine("[GRID CLIENT]: *** GetNeighbours of region 1");
            List<GridRegion> regions = m_Connector.GetNeighbours(UUID.Zero, r1.RegionID);
            if (regions == null)
                Console.WriteLine("[GRID CLIENT]: GetNeighbours of region 1 failed");
            else if (regions.Count > 0)
            {
                if (regions.Count != 1)
                    Console.WriteLine("[GRID CLIENT]: GetNeighbours of region 1 returned more neighbours than expected: " + regions.Count);
                else
                    Console.WriteLine("[GRID CLIENT]: GetNeighbours of region 1 returned the right neighbour " + regions[0].RegionName);
            }
            else
                Console.WriteLine("[GRID CLIENT]: GetNeighbours of region 1 returned 0 neighbours");


            Console.WriteLine("[GRID CLIENT]: *** GetRegionByUUID of region 2 (this should succeed)");
            GridRegion region = m_Connector.GetRegionByUUID(UUID.Zero, r2.RegionID);
            if (region == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionByUUID returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionByUUID returned region " + region.RegionName);

            Console.WriteLine("[GRID CLIENT]: *** GetRegionByUUID of non-existent region (this should fail)");
            region = m_Connector.GetRegionByUUID(UUID.Zero, UUID.Random());
            if (region == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionByUUID returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionByUUID returned region " + region.RegionName);

            Console.WriteLine("[GRID CLIENT]: *** GetRegionByName of region 3 (this should succeed)");
            region = m_Connector.GetRegionByName(UUID.Zero, r3.RegionName);
            if (region == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionByName returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionByName returned region " + region.RegionName);

            Console.WriteLine("[GRID CLIENT]: *** GetRegionByName of non-existent region (this should fail)");
            region = m_Connector.GetRegionByName(UUID.Zero, "Foo");
            if (region == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionByName returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionByName returned region " + region.RegionName);

            Console.WriteLine("[GRID CLIENT]: *** GetRegionsByName (this should return 3 regions)");
            regions = m_Connector.GetRegionsByName(UUID.Zero, "Test", 10);
            if (regions == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionsByName returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionsByName returned " + regions.Count + " regions");

            Console.WriteLine("[GRID CLIENT]: *** GetRegionRange (this should return 2 regions)");
            regions = m_Connector.GetRegionRange(UUID.Zero,
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(1002),
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(1002) );
            if (regions == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionRange returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionRange returned " + regions.Count + " regions");
            Console.WriteLine("[GRID CLIENT]: *** GetRegionRange (this should return 0 regions)");
            regions = m_Connector.GetRegionRange(UUID.Zero,
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(950),
                (int)Util.RegionToWorldLoc(900), (int)Util.RegionToWorldLoc(950) );
            if (regions == null)
                Console.WriteLine("[GRID CLIENT]: GetRegionRange returned null");
            else
                Console.WriteLine("[GRID CLIENT]: GetRegionRange returned " + regions.Count + " regions");

            Console.Write("Proceed to deregister? Press enter...");
            Console.ReadLine();

            // Deregister them all
            Console.WriteLine("[GRID CLIENT]: *** Deregistering region 1");
            success = m_Connector.DeregisterRegion(r1.RegionID);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully deregistered region 1");
            else
                Console.WriteLine("[GRID CLIENT]: region 1 failed to deregister");
            Console.WriteLine("[GRID CLIENT]: *** Deregistering region 2");
            success = m_Connector.DeregisterRegion(r2.RegionID);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully deregistered region 2");
            else
                Console.WriteLine("[GRID CLIENT]: region 2 failed to deregister");
            Console.WriteLine("[GRID CLIENT]: *** Deregistering region 3");
            success = m_Connector.DeregisterRegion(r3.RegionID);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully deregistered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to deregister");

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
