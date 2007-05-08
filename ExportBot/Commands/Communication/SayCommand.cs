using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace libsecondlife.TestClient
{
    public class SayCommand: Command
    {
        public SayCommand(TestClient testClient)
		{
			Name = "say";
			Description = "Say something.  (usage: say (optional channel) whatever)";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
            int channel = 0;
            int startIndex = 0;
            
            if (args.Length < 1)
            {
                return "usage: say (optional channel) whatever";
            }
            else if (args.Length > 1)
            {
                if (Int32.TryParse(args[0], out channel))
					startIndex = 1;
            }

            StringBuilder message = new StringBuilder();

			for (int i = startIndex; i < args.Length; i++)
            {
                message.Append(args[i]);
                if (i != args.Length - 1) message.Append(" ");
            }

			Client.Self.Chat(message.ToString(), channel, MainAvatar.ChatType.Normal);

            return "Said " + message.ToString();
		}
    }
}
