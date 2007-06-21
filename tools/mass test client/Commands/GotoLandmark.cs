using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class GotoLandmarkCommand : Command
    {
		public GotoLandmarkCommand(TestClient testClient)
        {
            Name = "goto_landmark";
            Description = "Teleports to a Landmark ";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
			LLUUID landmark = new LLUUID();
			if ( ! LLUUID.TryParse(args[0], out landmark) ) {
				return "Invalid LLUID";
			} else {
				Console.WriteLine("Teleporting to " + landmark.ToString());
			}
			if ( Client.Self.Teleport(landmark) ) {
				return "Teleport Succesful";
			} else {
				return "Teleport Failed";
			}
        }
    }
}
