using System;
using System.Collections.Generic;
using System.Text;

namespace libsecondlife.TestClient.Commands.Movement {
    class MovetoCommand : Command {
        public MovetoCommand(TestClient client) {
            Name = "moveto";
            Description = "Moves the avatar to the specified global position using simulator autopilot.";
        }
        public override string Execute(string[] args, LLUUID fromAgentID) {
            if (args.Length != 3)
                return "usage: moveto x y z";
            float x = Client.Self.Position.X + Client.regionX;
            float y = Client.Self.Position.Y + Client.regionY;
            float z = Client.Self.Position.Z;
            float.TryParse(args[0], out x);
            float.TryParse(args[1], out y);
            float.TryParse(args[2], out z);
            Client.Self.AutoPilot((ulong)x, (ulong)y, z);
            return "Attempting to move to <" + x + ", " + y + ", " + z + ">";
        }
    }
}
