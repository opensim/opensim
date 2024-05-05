using System;
using OpenMetaverse;

namespace OSHttpServer.Parser
{
    /// <summary>
    /// Event arguments used when a new header have been parsed.
    /// </summary>
    public class HeaderEventArgs : EventArgs
    {
        public osUTF8Slice Name;
        public osUTF8Slice Value;
    }
}