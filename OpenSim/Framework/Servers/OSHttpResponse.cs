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

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using HttpServer;

namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// OSHttpResponse is the OpenSim representation of an HTTP
    /// response.
    /// </summary>
    /// <remarks>
    /// OSHttpResponse is currently dual "homed" in that it support
    /// both the .NET HttpListenerResponse and the HttpServer
    /// HttpResponse (similar to OSHttpRequest); this duality is only
    /// temporary and the .NET usage will disappear once the switch to
    /// HttpServer is completed.
    /// </remarks>
    public class OSHttpResponse
    {

        // property code below is a bit messy, will all resolve to
        // harmony once we've completed the switch

        /// <summary>
        /// Content type property.
        /// </summary>
        /// <remarks>
        /// Setting this property will also set IsContentTypeSet to
        /// true.
        /// </remarks>
        public string ContentType
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.ContentType;
                else
                    return _httpListenerResponse.ContentType;
            }
            set
            {
                if (HttpServer)
                {
                    _httpResponse.ContentType = value;
                }
                else
                {
                    _httpListenerResponse.ContentType = value;
                    _contentTypeSet = true;
                }
            }
        }

        /// <summary>
        /// Boolean property indicating whether the content type
        /// property actively has been set.
        /// </summary>
        /// <remarks>
        /// IsContentTypeSet will go away together with .NET base.
        /// </remarks>
        public bool IsContentTypeSet
        {
            get { return _contentTypeSet; }
        }
        private bool _contentTypeSet;


        /// <summary>
        /// Length of the body content; 0 if there is no body.
        /// </summary>
        public long ContentLength
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.ContentLength;
                else
                    return _httpListenerResponse.ContentLength64;
            }
            set
            {
                if (HttpServer)
                    _httpResponse.ContentLength = value;
                else
                    _httpListenerResponse.ContentLength64 = value;
            }
        }

        /// <summary>
        /// Alias for ContentLength.
        /// </summary>
        public long ContentLength64
        {
            get { return ContentLength; }
            set { ContentLength = value; }
        }

        /// <summary>
        /// Encoding of the body content.
        /// </summary>
        public Encoding ContentEncoding
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.Encoding;
                else
                    return _httpListenerResponse.ContentEncoding;
            }

            set
            {
                if (HttpServer)
                    _httpResponse.Encoding = value;
                else
                    _httpListenerResponse.ContentEncoding = value;
            }
        }

        /// <summary>
        /// Headers of the response.
        /// </summary>
        public WebHeaderCollection Headers
        {
            get
            {
                if (HttpServer)
                    return null;
                else
                    return _httpListenerResponse.Headers;
            }
        }

        /// <summary>
        /// Get or set the keep alive property.
        /// </summary>
        public bool KeepAlive
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.Connection == ConnectionType.KeepAlive;
                else
                    return _httpListenerResponse.KeepAlive;
            }

            set
            {
                if (HttpServer)
                {
                    if (value == true)
                        _httpResponse.Connection = ConnectionType.KeepAlive;
                    else
                        _httpResponse.Connection = ConnectionType.Close;
                }
                else
                    _httpListenerResponse.KeepAlive = value;
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
            get
            {
                if (HttpServer)
                    return _httpResponse.Body;
                else
                    return _httpListenerResponse.OutputStream;
            }
        }

        public string ProtocolVersion
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.ProtocolVersion;
                else
                    return _httpListenerResponse.ProtocolVersion.ToString();
            }
            set
            {
                if (HttpServer)
                    _httpResponse.ProtocolVersion = value;
                else
                    _httpListenerResponse.ProtocolVersion = new Version(value); ;
                
            }
        }

        /// <summary>
        /// Return the output stream feeding the body.
        /// </summary>
        public Stream Body
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.Body;
                throw new Exception("[OSHttpResponse] mixed .NET and HttpServer access");
            }
        }

        /// <summary>
        /// Set a redirct location.
        /// </summary>
        public string RedirectLocation
        {
            // get { return _redirectLocation; }
            set
            {
                if (HttpServer)
                    _httpResponse.Redirect(value);
                else
                    _httpListenerResponse.RedirectLocation = value;
            }
        }


        /// <summary>
        /// Chunk transfers.
        /// </summary>
        public bool SendChunked
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.Chunked;
                else
                    return _httpListenerResponse.SendChunked;
            }

            set
            {
                if (HttpServer)
                    _httpResponse.Chunked = value;
                else
                    _httpListenerResponse.SendChunked = value;
            }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode
        {
            get
            {
                if (HttpServer)
                    return (int)_httpResponse.Status;
                else
                    return _httpListenerResponse.StatusCode;
            }

            set
            {
                if (HttpServer)
                    _httpResponse.Status = (HttpStatusCode)value;
                else
                    _httpListenerResponse.StatusCode = value;
            }
        }


        /// <summary>
        /// HTTP status description.
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (HttpServer)
                    return _httpResponse.Reason;
                else
                    return _httpListenerResponse.StatusDescription;
            }

            set
            {
                if (HttpServer)
                    _httpResponse.Reason = value;
                else
                    _httpListenerResponse.StatusDescription = value;
            }
        }


        internal bool HttpServer
        {
            get { return null != _httpResponse; }
        }
        private HttpResponse _httpResponse;
        private HttpListenerResponse _httpListenerResponse;

        internal HttpResponse HttpResponse
        {
             get { return _httpResponse; }
        }

        public OSHttpResponse()
        {
        }

        /// <summary>
        /// Instantiate an OSHttpResponse object based on an
        /// underlying .NET HttpListenerResponse.
        /// </summary>
        /// <remarks>
        /// Almost deprecated; will go west to make once HttpServer
        /// base takes over.
        /// </remarks>
        public OSHttpResponse(HttpListenerResponse resp)
        {
            _httpListenerResponse = resp;
        }
        public OSHttpResponse(HttpServer.HttpResponse resp)
        {
            _httpResponse = resp;
        }
        /// <summary>
        /// Instantiate an OSHttpResponse object from an OSHttpRequest
        /// object.
        /// </summary
        /// <param name="req">Incoming OSHttpRequest to which we are
        /// replying</param>
        // public OSHttpResponse(OSHttpRequest req)
        // {
        //     _httpResponse = new HttpResponse(req.HttpClientContext, req.HttpRequest);
        // }

        /// <summary>
        /// Add a header field and content to the response.
        /// </summary>
        /// <param name="key">string containing the header field
        /// name</param>
        /// <param name="value">string containing the header field
        /// value</param>
        public void AddHeader(string key, string value)
        {
            if (HttpServer)
                _httpResponse.AddHeader(key, value);
            else
                _httpListenerResponse.Headers.Add(key, value);
        }

        /// <summary>
        /// Send the response back to the remote client
        /// </summary>
        public void Send()
        {
            if (HttpServer)
            {
                _httpResponse.Body.Flush();
                _httpResponse.Send();
                
            }
            else
            {
                OutputStream.Close();
            }
        }
    }
}
