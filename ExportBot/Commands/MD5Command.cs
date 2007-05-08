using System;
using libsecondlife;

namespace libsecondlife.TestClient
{
    public class MD5Command : Command
    {
        public MD5Command(TestClient testClient)
        {
            Name = "md5";
            Description = "Creates an MD5 hash from a given password. Usage: md5 [password]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length == 1)
                return Helpers.MD5(args[0]);
            else
                return "Usage: md5 [password]";
        }
    }
}
