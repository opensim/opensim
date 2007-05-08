using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class GotoCommand: Command
    {
        public GotoCommand(TestClient testClient)
		{
			Name = "goto";
			Description = "Teleport to a location (e.g. \"goto Hooper/100/100/30\")";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			if (args.Length < 1)
                return "usage: Destination should be specified as sim/x/y/z";

            string destination = String.Empty;

            // Handle multi-word sim names by combining the arguments
            foreach (string arg in args)
            {
                destination += arg + " ";
            }
            destination = destination.Trim();

            string[] tokens = destination.Split(new char[] { '/' });
            if (tokens.Length != 4)
                return "usage: Destination should be specified as sim/x/y/z";

            string sim = tokens[0];
			float x = Client.Self.Position.X;
			float y = Client.Self.Position.Y;
			float z = Client.Self.Position.Z;
            float.TryParse(tokens[1], out x);
            float.TryParse(tokens[2], out y);
            float.TryParse(tokens[3], out z);

            if (Client.Self.Teleport(sim, new LLVector3(x, y, z)))
            {
                return "Teleported to " + Client.Network.CurrentSim;
            }
            else
            {
                return "Teleport failed: " + Client.Self.TeleportMessage;
            }
		}
    }
}
