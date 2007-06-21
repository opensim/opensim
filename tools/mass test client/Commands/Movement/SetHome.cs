using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class SetHomeCommand : Command
    {
		public SetHomeCommand(TestClient testClient)
        {
            Name = "sethome";
            Description = "Sets home to the current location.";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
			Client.Self.SetHome();
            return "Home Set";
        }
    }
}
