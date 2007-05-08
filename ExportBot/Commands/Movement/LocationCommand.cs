using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class LocationCommand: Command
    {
        public LocationCommand(TestClient testClient)
		{
			Name = "location";
			Description = "Show the location.";
		}

		public override string Execute(string[] args, LLUUID fromAgentID)
		{
            return "CurrentSim: '" + Client.Network.CurrentSim.ToString() + "' Position: " + 
                Client.Self.Position.ToString();
		}
    }
}
