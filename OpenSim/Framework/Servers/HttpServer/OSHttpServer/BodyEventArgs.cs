using System;

namespace OSHttpServer.Parser
{
    /// <summary>
    /// Arguments used when body bytes have come.
    /// </summary>
    public class BodyEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets buffer that contains the received bytes.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// number of bytes from <see cref="Offset"/> that should be parsed.
        /// </summary>
        public int Count;

        /// <summary>
        /// offset in buffer where to start processing.
        /// </summary>
        public int Offset;
    }
}