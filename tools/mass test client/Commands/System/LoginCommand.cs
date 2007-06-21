using System;
using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class LoginCommand : Command
    {
        public LoginCommand(TestClient testClient)
        {
            Name = "login";
            Description = "Logs in another avatar";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 3 && args.Length != 4)
                return "usage: login firstname lastname password [simname]";

            SecondLife newClient = Client.ClientManager.Login(args);

            if (newClient.Network.Connected)
            {
                return "Logged in " + newClient.ToString();
            }
            else
            {
                return "Failed to login: " + newClient.Network.LoginMessage;
            }
        }
    }
}
