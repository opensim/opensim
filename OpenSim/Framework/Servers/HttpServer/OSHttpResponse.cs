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

using System.IO;
using System.Net;
using System.Text;
using OSHttpServer;
using System.Runtime.CompilerServices;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// OSHttpResponse is the OpenSim representation of an HTTP
    /// response.
    /// </summary>
    public class OSHttpResponse : IOSHttpResponse
    {
        /// <summary>
        /// Content type property.
        /// </summary>
        /// <remarks>
        /// Setting this property will also set IsContentTypeSet to
        /// true.
        /// </remarks>
        public virtual string ContentType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.ContentType; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.ContentType = value; }
        }

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
        public long ContentLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.ContentLength; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.ContentLength = value; }
        }

        /// <summary>
        /// Alias for ContentLength.
        /// </summary>
        public long ContentLength64
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ContentLength; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { ContentLength = value; }
        }

        /// <summary>
        /// Encoding of the body content.
        /// </summary>
        public Encoding ContentEncoding
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Encoding; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Encoding = value; }
        }

        public bool KeepAlive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Connection == ConnectionType.KeepAlive; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Connection = value ? ConnectionType.KeepAlive : ConnectionType.Close; }
        }

        /// <summary>
        /// Get or set the keep alive timeout property (default is
        /// 20). Setting this to 0 also disables KeepAlive. Setting
        /// this to something else but 0 also enable KeepAlive.
        /// </summary>
        public int KeepAliveTimeout
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.KeepAlive; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value == 0)
                {
                    _httpResponse.Connection = ConnectionType.Close;
                    _httpResponse.KeepAlive = 0;
                }
                else
                {
                    _httpResponse.Connection = ConnectionType.KeepAlive;
                    _httpResponse.KeepAlive = value;
                }
            }
        }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        /// <remarks>
        /// On its way out...
        /// </remarks>
        public Stream OutputStream
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Body; }
        }

        public string ProtocolVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.ProtocolVersion; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.ProtocolVersion = value; }
        }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        public Stream Body
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Body; }
        }

        public byte[] RawBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.RawBuffer; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.RawBuffer = value; }
        }

        public int RawBufferStart
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.RawBufferStart; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.RawBufferStart = value; }
        }

        public int RawBufferLen
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.RawBufferLen; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.RawBufferLen = value; }
        }

        public int Priority
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Priority; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Priority = value; }
        }

        /// <summary>
        /// Chunk transfers.
        /// </summary>
        public bool SendChunked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Chunked; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Chunked = value; }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public virtual int StatusCode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (int)_httpResponse.Status; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Status = (HttpStatusCode)value; }
        }

        public double RequestTS
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.RequestTS; }
        }

        /// <summary>
        /// HTTP status description.
        /// </summary>
        public string StatusDescription
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _httpResponse.Reason; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _httpResponse.Reason = value; }
        }

        protected readonly IHttpResponse _httpResponse;

        public OSHttpResponse() {}

        public OSHttpResponse(IHttpResponse resp)
        {
            _httpResponse = resp;
        }

        /// <summary>
        /// Instantiate an OSHttpResponse object from an OSHttpRequest
        /// object.
        /// </summary
        /// <param name="req">Incoming OSHttpRequest to which we are
        /// replying</param>
        public OSHttpResponse(OSHttpRequest req)
        {
            _httpResponse = new HttpResponse(req.IHttpRequest);
        }

        public OSHttpResponse(HttpResponse resp)
        {
            _httpResponse = resp;
        }

        /// <summary>
        /// Set response as a http redirect
        /// </summary>
        /// <param name="url">redirection target url</param>
        /// <param name="redirStatusCode">the response Status, must be Found, Redirect, Moved,MovedPermanently,RedirectKeepVerb, RedirectMethod, TemporaryRedirect. Defaults to Redirect</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Redirect(string url, HttpStatusCode redirStatusCode = HttpStatusCode.Redirect)
        {
            _httpResponse.Redirect(url, redirStatusCode);
        }

        /// <summary>
        /// Add a header field and content to the response.
        /// </summary>
        /// <param name="key">string containing the header field
        /// name</param>
        /// <param name="value">string containing the header field
        /// value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddHeader(string key, string value)
        {
            _httpResponse.AddHeader(key, value);
        }

        /// <summary>
        /// Send the response back to the remote client
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send()
        {
            _httpResponse.Chunked = false;
            _httpResponse.Send();
        }
    }
}