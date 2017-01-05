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
using OpenSim.Framework;
using System.IO;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// BaseStreamHandlerBasicDOSProtector Base streamed request handler.
    /// </summary>
    /// <remarks>
    /// Inheriting classes should override ProcessRequest() rather than Handle()
    /// </remarks>
    public abstract class BaseStreamHandlerBasicDOSProtector : BaseRequestHandler, IStreamedRequestHandler
    {

        private readonly BasicDosProtectorOptions _options;
        private readonly BasicDOSProtector _dosProtector;

        protected BaseStreamHandlerBasicDOSProtector(string httpMethod, string path, BasicDosProtectorOptions options) : this(httpMethod, path, null, null, options) {}

        protected BaseStreamHandlerBasicDOSProtector(string httpMethod, string path, string name, string description, BasicDosProtectorOptions options)
            : base(httpMethod, path, name, description)
        {
            _options = options;
            _dosProtector = new BasicDOSProtector(_options);
        }

        public virtual byte[] Handle(
            string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            byte[] result;
            RequestsReceived++;
            string clientstring = GetClientString(httpRequest);
            string endpoint = GetRemoteAddr(httpRequest);
            if (_dosProtector.Process(clientstring, endpoint))
                result = ProcessRequest(path, request, httpRequest, httpResponse);
            else
                result = ThrottledRequest(path, request, httpRequest, httpResponse);
            if (_options.MaxConcurrentSessions > 0)
                _dosProtector.ProcessEnd(clientstring, endpoint);

            RequestsHandled++;

            return result;
        }

        protected virtual byte[] ProcessRequest(
            string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            return null;
        }

        protected virtual byte[] ThrottledRequest(
            string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            return new byte[0];
        }


        private string GetRemoteAddr(IOSHttpRequest httpRequest)
        {
            string remoteaddr = string.Empty;
            if (httpRequest.Headers["remote_addr"] != null)
                remoteaddr = httpRequest.Headers["remote_addr"];

            return remoteaddr;
        }

        private string GetClientString(IOSHttpRequest httpRequest)
        {
            string clientstring = string.Empty;

            if (_options.AllowXForwardedFor && httpRequest.Headers["x-forwarded-for"] != null)
                clientstring = httpRequest.Headers["x-forwarded-for"];
            else
                clientstring = GetRemoteAddr(httpRequest);

            return clientstring;

        }
    }
}
