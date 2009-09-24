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
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        
        public static void Main(string[] args)
        {
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout =
                new PatternLayout("%date [%thread] %-5level %logger [%property{NDC}] - %message%newline");
            log4net.Config.BasicConfigurator.Configure(consoleAppender);

            string serverURI = "http://127.0.0.1:8002";
            GridServicesConnector m_Connector = new GridServicesConnector(serverURI);

            GridRegion r1 = CreateRegion("Test Region 1", 1000, 1000);
            GridRegion r2 = CreateRegion("Test Region 2", 1001, 1000);
            GridRegion r3 = CreateRegion("Test Region 3", 1005, 1000);

            Console.WriteLine("[GRID CLIENT]: Registering region 1"); 
            bool success = m_Connector.RegisterRegion(UUID.Zero, r1);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 1");
            else
                Console.WriteLine("[GRID CLIENT]: region 1 failed to register");

            Console.WriteLine("[GRID CLIENT]: Registering region 2");
            success = m_Connector.RegisterRegion(UUID.Zero, r2);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 2");
            else
                Console.WriteLine("[GRID CLIENT]: region 2 failed to register");

            Console.WriteLine("[GRID CLIENT]: Registering region 3");
            success = m_Connector.RegisterRegion(UUID.Zero, r3);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to register");


            Console.WriteLine("[GRID CLIENT]: Deregistering region 3");
            success = m_Connector.DeregisterRegion(r3.RegionID);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully deregistered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to deregister");
            Console.WriteLine("[GRID CLIENT]: Registering region 3 again");
            success = m_Connector.RegisterRegion(UUID.Zero, r3);
            if (success)
                Console.WriteLine("[GRID CLIENT]: Successfully registered region 3");
            else
                Console.WriteLine("[GRID CLIENT]: region 3 failed to register");

            Console.WriteLine("[GRID CLIENT]: GetNeighbours of region 1");
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


        }

        private static GridRegion CreateRegion(string name, uint xcell, uint ycell)
        {
            GridRegion region = new GridRegion(xcell, ycell);
            region.RegionName = name;
            region.RegionID = UUID.Random();
            region.ExternalHostName = "127.0.0.1";
            region.HttpPort = 9000;
            region.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
          
            return region;
        }
    }
}
