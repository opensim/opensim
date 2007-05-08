using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class QuitCommand: Command
    {
        public QuitCommand(TestClient testClient)
		{
			Name = "quit";
			Description = "Log all avatars out and shut down";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			Client.ClientManager.LogoutAll();
            Client.ClientManager.Running = false;
            return "All avatars logged out";
		}
    }
}
