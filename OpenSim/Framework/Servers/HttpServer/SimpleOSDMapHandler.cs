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

using System.Net;
using OpenSim.Framework.ServiceAuth;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// simple OSD streamed request handler.
    /// for well defined simple uri paths, single http method and a OSDMap encoded body
    /// </summary>
    /// <remarks>
    /// Inheriting classes should override ProcessRequest() rather than Handle()
    /// </remarks>
    public class SimpleOSDMapHandler : SimpleBaseRequestHandler, ISimpleStreamHandler
    {
        protected string m_httMethod;
        protected IServiceAuth m_Auth;
        protected SimpleOSDMapMethod m_processRequest;

        public SimpleOSDMapHandler(string httpmethod, string path) : base(path)
        {
            m_httMethod = httpmethod.ToUpper();
        }
        public SimpleOSDMapHandler(string httpmethod, string path, string name) : base(path, name)
        {
            m_httMethod = httpmethod.ToUpper();
        }
        public SimpleOSDMapHandler(string httpmethod, string path, SimpleOSDMapMethod processRequest) : base(path)
        {
            m_httMethod = httpmethod.ToUpper();
            m_processRequest = processRequest;
        }
        public SimpleOSDMapHandler(string httpmethod, string path, SimpleOSDMapMethod processRequest, string name) : base(path, name)
        {
            m_httMethod = httpmethod.ToUpper();
            m_processRequest = processRequest;
        }

        public SimpleOSDMapHandler(string httpmethod, string path, IServiceAuth auth) : base(path)
        {
            m_httMethod = httpmethod.ToUpper();
            m_Auth = auth;
        }

        public SimpleOSDMapHandler(string httpmethod, string path, IServiceAuth auth, SimpleOSDMapMethod processRequest)
            : base(path)
        {
            m_httMethod = httpmethod.ToUpper();
            m_Auth = auth;
            m_processRequest = processRequest;
        }

        public SimpleOSDMapHandler(string httpmethod, string path, IServiceAuth auth, SimpleOSDMapMethod processRequest, string name)
            : base(path, name)
        {
            m_httMethod = httpmethod.ToUpper();
            m_Auth = auth;
            m_processRequest = processRequest;
        }

        public virtual void Handle(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            RequestsReceived++;
            if(httpRequest.HttpMethod != m_httMethod)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            OSDMap args;
            try
            {
                args = (OSDMap)OSDParser.Deserialize(httpRequest.InputStream);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            if (args == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (m_Auth != null)
            {
                if (!m_Auth.Authenticate(httpRequest.Headers, httpResponse.AddHeader, out HttpStatusCode statusCode))
                {
                    httpResponse.StatusCode = (int)statusCode;
                    return;
                }
            }
            try
            {
                if(m_processRequest != null)
                    m_processRequest(httpRequest, httpResponse, args);
                else
                    ProcessRequest(httpRequest, httpResponse, args);
            }
            catch
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            RequestsHandled++;
        }

        protected virtual void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap args)
        {
        }
    }
}