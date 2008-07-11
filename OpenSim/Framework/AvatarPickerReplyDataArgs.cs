using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class AvatarPickerReplyDataArgs : EventArgs
    {
        public LLUUID AvatarID;
        public byte[] FirstName;
        public byte[] LastName;
    }
}