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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common
{
    public class TestOSHttpRequest : IOSHttpRequest
    {
        public string[] AcceptTypes
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public Encoding ContentEncoding
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public long ContentLength
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public long ContentLength64
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public string ContentType
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public HttpCookieCollection Cookies
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public bool HasEntityBody
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public NameValueCollection Headers { get; set; }

        public string HttpMethod
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public Stream InputStream
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public bool IsSecured
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public bool KeepAlive
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public NameValueCollection QueryString
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public Hashtable Query
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public string RawUrl
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public Uri Url { get; set; }

        public string UserAgent
        {
            get
            {
                throw new NotImplementedException ();
            }
        }

        public TestOSHttpRequest()
        {
            Headers = new NameValueCollection();
        }
    }
}