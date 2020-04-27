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
using OpenSim.Framework.ServiceAuth;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// simple Base streamed request handler.
    /// for well defined simple uri paths, any http method
    /// </summary>
    /// <remarks>
    /// Inheriting classes should override ProcessRequest() rather than Handle()
    /// </remarks>
    public class SimpleStreamHandler : SimpleBaseRequestHandler, ISimpleStreamHandler
    {
        protected IServiceAuth m_Auth;
        protected SimpleStreamMethod m_processRequest;

        public SimpleStreamHandler(string path) : base(path) { }
        public SimpleStreamHandler(string path, string name) : base(path, name) { }

        public SimpleStreamHandler(string path, SimpleStreamMethod processRequest) : base(path)
        {
            m_processRequest = processRequest;
        }
        public SimpleStreamHandler(string path, SimpleStreamMethod processRequest, string name) : base(path, name)
        {
            m_processRequest = processRequest;
        }

        public SimpleStreamHandler(string path, IServiceAuth auth) : base(path)
        {
            m_Auth = auth;
        }

        public SimpleStreamHandler(string path, IServiceAuth auth, SimpleStreamMethod processRequest)
            : base(path)
        {
            m_Auth = auth;
            m_processRequest = processRequest;
        }

        public SimpleStreamHandler(string path, IServiceAuth auth, SimpleStreamMethod processRequest, string name)
            : base(path, name)
        {
            m_Auth = auth;
            m_processRequest = processRequest;
        }

        public virtual void Handle(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            RequestsReceived++;

            if (m_Auth != null)
            {
                HttpStatusCode statusCode;

                if (!m_Auth.Authenticate(httpRequest.Headers, httpResponse.AddHeader, out statusCode))
                {
                    httpResponse.StatusCode = (int)statusCode;
                    return;
                }
            }

            try
            {
                if (m_processRequest != null)
                    m_processRequest(httpRequest, httpResponse);
                else
                    ProcessRequest(httpRequest, httpResponse);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            RequestsHandled++;
        }

        protected virtual void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
        }
    }
}