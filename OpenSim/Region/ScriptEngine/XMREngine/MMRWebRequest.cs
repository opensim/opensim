/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/**
 * @brief Perform web request
 */

using System;
using System.IO;
using System.Net;
using System.Text;

namespace OpenSim.Region.ScriptEngine.XMREngine {
    public class MMRWebRequest {
        public static bool allowFileURL = false;

        public static Stream MakeRequest (string verb, string requestUrl, string obj, int timeoutms)
        {
            /*
             * Pick apart the given URL and make sure we support it.
             * For file:// URLs, just return a read-only stream of the file.
             */
            Uri uri = new Uri (requestUrl);
            string supported = "http and https";
            if (allowFileURL && (verb == "GET")) {
                supported = "file, http and https";
                if (uri.Scheme == "file") {
                    return File.OpenRead (requestUrl.Substring (7));
                }
            }
            bool https = uri.Scheme == "https";
            if (!https && (uri.Scheme != "http")) {
                throw new WebException ("only support " + supported + ", not " + uri.Scheme);
            }
            string host = uri.Host;
            int port = uri.Port;
            if (port < 0) port = https ? 443 : 80;
            string path = uri.AbsolutePath;

            /*
             * Connect to the web server.
             */
            System.Net.Sockets.TcpClient tcpconnection = new System.Net.Sockets.TcpClient (host, port);
            if (timeoutms > 0) {
                tcpconnection.SendTimeout = timeoutms;
                tcpconnection.ReceiveTimeout = timeoutms;
            }

            try {

                /*
                 * Get TCP stream to/from web server.
                 * If HTTPS, wrap stream with SSL encryption.
                 */
                Stream tcpstream = tcpconnection.GetStream ();
                if (https) {
                    System.Net.Security.SslStream sslstream = new System.Net.Security.SslStream (tcpstream, false);
                    sslstream.AuthenticateAsClient (host);
                    tcpstream = sslstream;
                }

                /*
                 * Write request header to the web server.
                 * There might be some POST data as well to write to web server.
                 */
                WriteStream (tcpstream, verb + " " + path + " HTTP/1.1\r\n");
                WriteStream (tcpstream, "Host: " + host + "\r\n");
                if (obj != null) {
                    byte[] bytes = Encoding.UTF8.GetBytes (obj);

                    WriteStream (tcpstream, "Content-Length: " + bytes.Length + "\r\n");
                    WriteStream (tcpstream, "Content-Type: application/x-www-form-urlencoded\r\n");
                    WriteStream (tcpstream, "\r\n");
                    tcpstream.Write (bytes, 0, bytes.Length);
                } else {
                    WriteStream (tcpstream, "\r\n");
                }
                tcpstream.Flush ();

                /*
                 * Check for successful reply status line.
                 */
                string headerline = ReadStreamLine (tcpstream).Trim ();
                if (headerline != "HTTP/1.1 200 OK") throw new WebException ("status line " + headerline);

                /*
                 * Scan through header lines.
                 * The only ones we care about are Content-Length and Transfer-Encoding.
                 */
                bool chunked = false;
                int contentlength = -1;
                while ((headerline = ReadStreamLine (tcpstream).Trim ().ToLowerInvariant ()) != "") {
                    if (headerline.StartsWith ("content-length:")) {
                        contentlength = int.Parse (headerline.Substring (15));
                    }
                    if (headerline.StartsWith ("transfer-encoding:") && (headerline.Substring (18).Trim () == "chunked")) {
                        chunked = true;
                    }
                }

                /*
                 * Read response byte array as a series of chunks.
                 */
                if (chunked) {
                    return new ChunkedStreamReader (tcpstream);
                }

                /*
                 * Read response byte array with the exact length given by Content-Length.
                 */
                if (contentlength >= 0) {
                    return new LengthStreamReader (tcpstream, contentlength);
                }

                /*
                 * Don't know how it is being transferred.
                 */
                throw new WebException ("header missing content-length or transfer-encoding: chunked");
            } catch {
                tcpconnection.Close ();
                throw;
            }
        }

        /**
         * @brief Write the string out as ASCII bytes.
         */
        private static void WriteStream (Stream stream, string line)
        {
            byte[] bytes = Encoding.ASCII.GetBytes (line);
            stream.Write (bytes, 0, bytes.Length);
        }

        /**
         * @brief Read the next text line from a stream.
         * @returns string with \r\n trimmed off
         */
        private static string ReadStreamLine (Stream stream)
        {
            StringBuilder sb = new StringBuilder ();
            while (true) {
                int b = stream.ReadByte ();
                if (b < 0) break;
                if (b == '\n') break;
                if (b == '\r') continue;
                sb.Append ((char)b);
            }
            return sb.ToString ();
        }

        private class ChunkedStreamReader : Stream {
            private int chunklen;
            private Stream tcpstream;

            public ChunkedStreamReader (Stream tcpstream)
            {
                this.tcpstream = tcpstream;
            }

            public override bool CanRead    { get { return true;  } }
            public override bool CanSeek    { get { return false; } }
            public override bool CanTimeout { get { return false; } }
            public override bool CanWrite   { get { return false; } }
            public override long Length     { get { return 0; } }
            public override long Position   { get { return 0; } set { } }
            public override void Flush ()   { }
            public override long Seek (long offset, SeekOrigin origin) { return 0; }
            public override void SetLength (long length) { }
            public override void Write (byte[] buffer, int offset, int length) { }

            public override int Read (byte[] buffer, int offset, int length)
            {
                if (length <= 0) return 0;

                if (chunklen == 0) {
                    chunklen = int.Parse (ReadStreamLine (tcpstream), System.Globalization.NumberStyles.HexNumber);
                    if (chunklen < 0) throw new WebException ("negative chunk length");
                    if (chunklen == 0) chunklen = -1;
                }
                if (chunklen < 0) return 0;

                int maxread = (length < chunklen) ? length : chunklen;
                int lenread = tcpstream.Read (buffer, offset, maxread);
                chunklen -= lenread;
                if (chunklen == 0) {
                    int b = tcpstream.ReadByte ();
                    if (b == '\r') b = tcpstream.ReadByte ();
                    if (b != '\n') throw new WebException ("chunk not followed by \\r\\n");
                }
                return lenread;
            }

            public override void Close ()
            {
                chunklen = -1;
                if (tcpstream != null) {
                    tcpstream.Close ();
                    tcpstream = null;
                }
            }
        }

        private class LengthStreamReader : Stream {
            private int contentlength;
            private Stream tcpstream;

            public LengthStreamReader (Stream tcpstream, int contentlength)
            {
                this.tcpstream = tcpstream;
                this.contentlength = contentlength;
            }

            public override bool CanRead    { get { return true;  } }
            public override bool CanSeek    { get { return false; } }
            public override bool CanTimeout { get { return false; } }
            public override bool CanWrite   { get { return false; } }
            public override long Length     { get { return 0; } }
            public override long Position   { get { return 0; } set { } }
            public override void Flush ()   { }
            public override long Seek (long offset, SeekOrigin origin) { return 0; }
            public override void SetLength (long length) { }
            public override void Write (byte[] buffer, int offset, int length) { }

            public override int Read (byte[] buffer, int offset, int length)
            {
                if (length <= 0) return 0;
                if (contentlength <= 0) return 0;

                int maxread = (length < contentlength) ? length : contentlength;
                int lenread = tcpstream.Read (buffer, offset, maxread);
                contentlength -= lenread;
                return lenread;
            }

            public override void Close ()
            {
                contentlength = -1;
                if (tcpstream != null) {
                    tcpstream.Close ();
                    tcpstream = null;
                }
            }
        }
    }
}
