using System;
using OpenMetaverse;

namespace OpenSim.Framework.Client
{
    public class ClientInstantMessageArgs : EventArgs
    {
        public IClientCore client;
        public string message;
        public DateTime time;
        public ClientInstantMessageSender sender;
    }

    public class ClientInstantMessageSender
    {
        public UUID ID;
        public bool online;
        public string name;
        public Vector3 position;
        public UUID regionID;
    }

    public delegate void ClientInstantMessage(Object sender, ClientInstantMessageArgs e);

    public class ClientInstantMessageParms
    {
        public ClientInstantMessageSender senderInfo;
    }

    // Porting Guide from old IM
    // SendIM(...)
    //      Loses FromAgentSession - this should be added by implementers manually.
    //      

    public interface IClientIM
    {
        void SendInstantMessage(UUID fromAgent, string message, UUID toAgent,
                        string fromName, byte dialog, uint timeStamp);

        void SendInstantMessage(UUID fromAgent, string message, UUID toAgent,
                                string fromName, byte dialog, uint timeStamp,
                                bool fromGroup, byte[] binaryBucket);
        event ImprovedInstantMessage OnInstantMessage;
    }
}
