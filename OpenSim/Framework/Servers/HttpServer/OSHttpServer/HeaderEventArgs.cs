using System;

namespace OSHttpServer.Parser
{
    /// <summary>
    /// Event arguments used when a new header have been parsed.
    /// </summary>
    public class HeaderEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderEventArgs"/> class.
        /// </summary>
        /// <param name="name">Name of header.</param>
        /// <param name="value">Header value.</param>
        public HeaderEventArgs(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderEventArgs"/> class.
        /// </summary>
        public HeaderEventArgs()
        {
        }

        /// <summary>
        /// Gets or sets header name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets header value.
        /// </summary>
        public string Value { get; set; }
    }
}