using OpenMetaverse;
using System;

namespace OSHttpServer.Parser
{
    /// <summary>
    /// Used when the request line have been successfully parsed.
    /// </summary>
    public class RequestLineEventArgs : EventArgs
    {
        /// <summary>
        /// http method.
        /// </summary>
        public osUTF8Slice HttpMethod;

        /// <summary>
        /// version of the HTTP protocol that the client want to use.
        /// </summary>
        public osUTF8Slice HttpVersion;

        /// <summary>
        /// requested URI path.
        /// </summary>
        public osUTF8Slice UriPath;
    }
}