using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class GoHomeCommand : Command
    {
		public GoHomeCommand(TestClient testClient)
        {
            Name = "gohome";
            Description = "Teleports home";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
			if ( Client.Self.GoHome() ) {
				return "Teleport Home Succesful";
			} else {
				return "Teleport Home Failed";
			}
        }
    }
}
