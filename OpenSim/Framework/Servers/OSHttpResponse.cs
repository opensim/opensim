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
 *     * Neither the name of the OpenSim Project nor the
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

using System.Collections;
using System.IO;
using System.Net;
using System.Text;

namespace OpenSim.Framework.Servers
{
    public class OSHttpResponse
    {
        private string _contentType;
        private bool _contentTypeSet;
        public string ContentType
        {
            get { return _contentType; }
            set
            {
                _contentType = value;
                _contentTypeSet = true;
            }
        }
        public bool IsContentTypeSet
        {
            get { return _contentTypeSet; }
        }

        private long _contentLength64;
        public long ContentLength64
        {
            get { return _contentLength64; }
            set
            {
                _contentLength64 = value;
                if (null != _resp) _resp.ContentLength64 = value;
            }
        }

        private Encoding _contentEncoding;
        public Encoding ContentEncoding
        {
            get { return _contentEncoding; }
            set
            {
                _contentEncoding = value;
                if (null != _resp) _resp.ContentEncoding = value;
            }
        }

        public WebHeaderCollection Headers;
        // public CookieCollection Cookies;

        private bool _keepAlive;
        public bool KeepAlive
        {
            get { return _keepAlive; }
            set
            {
                _keepAlive = value;
                if (null != _resp) _resp.KeepAlive = value;
            }
        }

        public Stream OutputStream;

        private string _redirectLocation;
        public string RedirectLocation
        {
            get { return _redirectLocation; }
            set
            {
                _redirectLocation = value;
                if (null != _resp) _resp.RedirectLocation = value;
            }
        }

        private bool _sendChunked;
        public bool SendChunked
        {
            get { return _sendChunked; }
            set
            {
                _sendChunked = value;
                if (null != _resp) _resp.SendChunked = value;
            }
        }

        private int _statusCode;
        public int StatusCode
        {
            get { return _statusCode; }
            set
            {
                _statusCode = value;
                if (null != _resp) _resp.StatusCode = value;
            }
        }

        private string _statusDescription;
        public string StatusDescription
        {
            get { return _statusDescription; }
            set
            {
                _statusDescription = value;
                if (null != _resp) _resp.StatusDescription = value;
            }
        }

        private HttpListenerResponse _resp;

        public OSHttpResponse()
        {
        }

        public OSHttpResponse(HttpListenerResponse resp)
        {
            ContentEncoding = resp.ContentEncoding;
            ContentLength64 = resp.ContentLength64;
            _contentType = resp.ContentType;
            Headers = resp.Headers;
            // Cookies = resp.Cookies;
            KeepAlive = resp.KeepAlive;
            OutputStream = resp.OutputStream;
            RedirectLocation = resp.RedirectLocation;
            SendChunked = resp.SendChunked;
            StatusCode = resp.StatusCode;
            StatusDescription = resp.StatusDescription;

            _contentTypeSet = false;

            _resp = resp;
        }

        public void AddHeader(string key, string value)
        {
            Headers.Add(key, value);
        }
    }
}
