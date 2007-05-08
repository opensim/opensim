using System;
using System.Text;
using libsecondlife;

namespace libsecondlife.TestClient
{
    public class RegionInfoCommand : Command
    {
        public RegionInfoCommand(TestClient testClient)
		{
			Name = "regioninfo";
			Description = "Prints out info about all the current region";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine(Client.Network.CurrentSim.ToString());
            output.Append("Access: ");
            output.AppendLine(Client.Network.CurrentSim.Access.ToString());
            output.Append("Flags: ");
            output.AppendLine(Client.Network.CurrentSim.Flags.ToString());
            output.Append("TerrainBase0: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainBase0.ToStringHyphenated());
            output.Append("TerrainBase1: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainBase1.ToStringHyphenated());
            output.Append("TerrainBase2: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainBase2.ToStringHyphenated());
            output.Append("TerrainBase3: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainBase3.ToStringHyphenated());
            output.Append("TerrainDetail0: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainDetail0.ToStringHyphenated());
            output.Append("TerrainDetail1: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainDetail1.ToStringHyphenated());
            output.Append("TerrainDetail2: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainDetail2.ToStringHyphenated());
            output.Append("TerrainDetail3: ");
            output.AppendLine(Client.Network.CurrentSim.TerrainDetail3.ToStringHyphenated());
            output.Append("Water Height: ");
            output.AppendLine(Client.Network.CurrentSim.WaterHeight.ToString());

            return output.ToString();
        }
    }
}
