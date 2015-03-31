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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common
{
    public class TestOSHttpResponse : IOSHttpResponse
    {
        /// <summary>
        /// Content type property.
        /// </summary>
        /// <remarks>
        /// Setting this property will also set IsContentTypeSet to
        /// true.
        /// </remarks>
        public string ContentType { get; set; }

        /// <summary>
        /// Boolean property indicating whether the content type
        /// property actively has been set.
        /// </summary>
        /// <remarks>
        /// IsContentTypeSet will go away together with .NET base.
        /// </remarks>
        // public bool IsContentTypeSet
        // {
        //     get { return _contentTypeSet; }
        // }
        // private bool _contentTypeSet;

        /// <summary>
        /// Length of the body content; 0 if there is no body.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Alias for ContentLength.
        /// </summary>
        public long ContentLength64 { get; set; }

        /// <summary>
        /// Encoding of the body content.
        /// </summary>
        public Encoding ContentEncoding { get; set; }

        public bool KeepAlive { get; set; }

        /// <summary>
        /// Get or set the keep alive timeout property (default is
        /// 20). Setting this to 0 also disables KeepAlive. Setting
        /// this to something else but 0 also enable KeepAlive.
        /// </summary>
        public int KeepAliveTimeout { get; set; }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        /// <remarks>
        /// On its way out...
        /// </remarks>
        public Stream OutputStream { get; private set; }

        public string ProtocolVersion { get; set; }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        public Stream Body { get; private set; }

        /// <summary>
        /// Set a redirct location.
        /// </summary>
        public string RedirectLocation { private get; set; }

        /// <summary>
        /// Chunk transfers.
        /// </summary>
        public bool SendChunked { get; set; }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// HTTP status description.
        /// </summary>
        public string StatusDescription { get; set; }

        public bool ReuseContext { get; set; }

        /// <summary>
        /// Add a header field and content to the response.
        /// </summary>
        /// <param name="key">string containing the header field
        /// name</param>
        /// <param name="value">string containing the header field
        /// value</param>
        public void AddHeader(string key, string value) { throw new NotImplementedException(); }

        public void Send() { }
    }
}