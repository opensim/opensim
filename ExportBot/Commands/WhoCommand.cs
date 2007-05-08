using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class WhoCommand: Command
    {
        public WhoCommand(TestClient testClient)
		{
			Name = "who";
			Description = "Lists seen avatars.";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			StringBuilder result = new StringBuilder();
			foreach (Avatar av in Client.AvatarList.Values)
			{
				result.AppendFormat("\n{0} {1} {2}/{3} ID: {4}", av.Name, av.GroupName, 
                    (av.CurrentSim != null ? av.CurrentSim.Name : String.Empty), av.Position, av.ID);
			}

            return result.ToString();
		}
    }
}
