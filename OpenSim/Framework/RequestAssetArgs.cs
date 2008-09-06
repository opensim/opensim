using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class RequestAssetArgs : EventArgs
    {
        public int ChannelType;
        public float Priority;
        public int SourceType;
        public UUID TransferID;
    }
}
