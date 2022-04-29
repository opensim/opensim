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
        public string Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderEventArgs"/> class.
        /// </summary>
        public HeaderEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderEventArgs"/> class.
        /// </summary>
        /// <param name="name">Name of header.</param>
        /// <param name="value">Header value.</param>
        public HeaderEventArgs(osUTF8Slice name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}