using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class FindSimCommand : Command
    {
        public FindSimCommand(TestClient testClient)
        {
            Name = "findsim";
            Description = "Searches for a simulator and returns information about it. Usage: findsim [Simulator Name]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: findsim [Simulator Name]";

            // Build the simulator name from the args list
            string simName = string.Empty;
            for (int i = 0; i < args.Length; i++)
                simName += args[i] + " ";
            simName = simName.TrimEnd().ToLower();

            //if (!GridDataCached[Client])
            //{
            //    Client.Grid.RequestAllSims(GridManager.MapLayerType.Objects);
            //    System.Threading.Thread.Sleep(5000);
            //    GridDataCached[Client] = true;
            //}

            GridRegion region;

            if (Client.Grid.GetGridRegion(simName, out region))
                return String.Format("{0}: handle={1} ({2},{3})", region.Name, region.RegionHandle, region.X, region.Y);
            else
                return "Lookup of " + simName + " failed";
        }
    }
}
