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
using System.Text;
using System.Net;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.BakedTextures
{
    public class XBakesConnector : ServiceConnector
    {
        private string m_ConfigName = "BakedTextureService";

        public XBakesConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            if (configName != string.Empty)
                m_ConfigName = configName;

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string bakesServiceName = serverConfig.GetString("LocalServiceModule", string.Empty);

            if (string.IsNullOrWhiteSpace(bakesServiceName))
                throw new Exception("No BakedTextureService in config file");

            object[] args = new object[] { config };
            IBakedTextureService bakesService = ServerUtils.LoadPlugin<IBakedTextureService>(bakesServiceName, args);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddSimpleStreamHandler(new BakesServerHandler(bakesService, auth), true);
        }
    }

    public class BakesServerHandler : SimpleStreamHandler
    {
        private IBakedTextureService m_BakesService;

        public BakesServerHandler(IBakedTextureService service, IServiceAuth auth) :
                base("/bakes", auth)
        {
            m_BakesService = service;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if(m_BakesService == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }
            switch (httpRequest.HttpMethod)
            {
                case "GET":
                    doGet(httpRequest, httpResponse);
                    break;
                case "POST":
                    doPost(httpRequest, httpResponse);
                    break;
                default:
                    httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
            }
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        private void doGet(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string[] p = SplitParams(httpRequest.UriPath);
            httpRequest.InputStream.Dispose();

            if (p.Length == 0)
                return;

            httpResponse.RawBuffer = m_BakesService.Get(p[0]);
        }

        private void doPost(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string[] p = SplitParams(httpRequest.UriPath);

            if (p.Length == 0)
                return;
            // httpRequest.InputStream is a memorystream with origin = 0
            // so no need to copy to another array
            MemoryStream ms = (MemoryStream)httpRequest.InputStream;
            int len = (int)ms.Length;
            byte[] data = ms.GetBuffer();
            httpRequest.InputStream.Dispose(); // the buffer stays in data
            m_BakesService.Store(p[0], data, len);
        }

        public string[] SplitParams(string path)
        {
            string param = GetParam(path);
            return param.Split(new char[] { '/', '?', '&' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
