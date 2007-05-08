using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class ImCommand : Command
    {
        string ToAvatarName = String.Empty;
        ManualResetEvent NameSearchEvent = new ManualResetEvent(false);
        Dictionary<string, LLUUID> Name2Key = new Dictionary<string, LLUUID>();

        public ImCommand(TestClient testClient)
        {
            testClient.Avatars.OnAvatarNameSearch += new AvatarManager.AvatarNameSearchCallback(Avatars_OnAvatarNameSearch);

            Name = "im";
            Description = "Instant message someone. Usage: im [firstname] [lastname] [message]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length < 3)
                return "Usage: im [firstname] [lastname] [message]";

            ToAvatarName = args[0] + " " + args[1];

            // Build the message
            string message = String.Empty;
            for (int ct = 2; ct < args.Length; ct++)
                message += args[ct] + " ";
            message = message.TrimEnd();
            if (message.Length > 1023) message = message.Remove(1023);

            if (!Name2Key.ContainsKey(ToAvatarName.ToLower()))
            {
                // Send the Query
                Client.Avatars.RequestAvatarNameSearch(ToAvatarName, LLUUID.Random());

                NameSearchEvent.WaitOne(6000, false);
            }

            if (Name2Key.ContainsKey(ToAvatarName.ToLower()))
            {
                LLUUID id = Name2Key[ToAvatarName.ToLower()];

                Client.Self.InstantMessage(id, message, id);
                return "Instant Messaged " + id.ToStringHyphenated() + " with message: " + message;
            }
            else
            {
                return "Name lookup for " + ToAvatarName + " failed";
            }
        }

        void Avatars_OnAvatarNameSearch(LLUUID queryID, Dictionary<LLUUID, string> avatars)
        {
            foreach (KeyValuePair<LLUUID, string> kvp in avatars)
            {
                if (kvp.Value.ToLower() == ToAvatarName.ToLower())
                {
                    Name2Key[ToAvatarName.ToLower()] = kvp.Key;
                    NameSearchEvent.Set();
                    return;
                }
            }
        }
    }
}
