using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AvatarPickerReplyDataArgs : EventArgs
    {
        public UUID AvatarID;
        public byte[] FirstName;
        public byte[] LastName;
    }
}
