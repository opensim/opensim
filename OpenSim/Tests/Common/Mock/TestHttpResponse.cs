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

using System;
using System.IO;
using System.Net;
using System.Text;
using HttpServer;

namespace OpenSim.Tests.Common
{
/*
    public class TestHttpResponse: IHttpResponse
    {
        public Stream Body
        {
            get { return _body; }

            set { _body = value; }
        }
        private Stream _body;

        public string ProtocolVersion
        {
            get { return _protocolVersion; }
            set { _protocolVersion = value; }
        }
        private string _protocolVersion;

        public bool Chunked
        {
            get { return _chunked; }

            set { _chunked = value; }
        }
        private bool _chunked;

        public ConnectionType Connection
        {
            get { return _connection; }

            set { _connection = value; }
        }
        private ConnectionType _connection;

        public Encoding Encoding
        {
            get { return _encoding; }

            set { _encoding = value; }
        }
        private Encoding _encoding;

        public int KeepAlive
        {
            get { return _keepAlive; }

            set { _keepAlive = value; }
        }
        private int _keepAlive;

        public HttpStatusCode Status
        {
            get { return _status; }

            set { _status = value; }
        }
        private HttpStatusCode _status;

        public string Reason
        {
            get { return _reason; }

            set { _reason = value; }
        }
        private string _reason;

        public long ContentLength
        {
            get { return _contentLength; }

            set { _contentLength = value; }
        }
        private long _contentLength;

        public string ContentType
        {
            get { return _contentType; }

            set { _contentType = value; }
        }
        private string _contentType;

        public bool HeadersSent
        {
            get { return _headersSent; }
        }
        private bool _headersSent;

        public bool Sent
        {
            get { return _sent; }
        }
        private bool _sent;

        public ResponseCookies Cookies
        {
            get { return _cookies; }
        }
        private ResponseCookies _cookies = null;

        public TestHttpResponse()
        {
            _headersSent = false;
            _sent = false;
        }

        public void AddHeader(string name, string value) {}

        public void Send()
        {
            if (!_headersSent) SendHeaders();
            if (_sent) throw new InvalidOperationException("stuff already sent");
            _sent = true;
        }

        public void SendBody(byte[] buffer, int offset, int count)
        {
            if (!_headersSent) SendHeaders();
            _sent = true;
        }

        public void SendBody(byte[] buffer)
        {
            if (!_headersSent) SendHeaders();
            _sent = true;
        }

        public void SendHeaders()
        {
            if (_headersSent) throw new InvalidOperationException("headers already sent");
            _headersSent = true;
        }

        public void Redirect(Uri uri) {}
        public void Redirect(string url) {}
    }
*/
}