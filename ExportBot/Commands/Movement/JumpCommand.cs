using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class JumpCommand: Command
    {
        public JumpCommand(TestClient testClient)
		{
			Name = "jump";
			Description = "Teleports to the specified height. (e.g. \"jump 1000\")";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			if (args.Length != 1)
                return "usage: jump 1000";

			float height = 0;
			float.TryParse(args[0], out height);

			Client.Self.Teleport
			(
				Client.Network.CurrentSim.Name,
				new LLVector3(Client.Self.Position.X, Client.Self.Position.Y, Client.Self.Position.Z + height)
			);

            return "Jumped " + height;
		}
    }
}
