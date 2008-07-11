using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class RequestAssetArgs : EventArgs
    {
        public int ChannelType;
        public float Priority;
        public int SourceType;
        public LLUUID TransferID;
    }
}