using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class LogoutCommand : Command
    {
        public LogoutCommand(TestClient testClient)
        {
            Name = "logout";
            Description = "Log this avatar out";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            string name = Client.ToString();
			Client.ClientManager.Logout(Client);
            return "Logged " + name + " out";
        }
    }
}
