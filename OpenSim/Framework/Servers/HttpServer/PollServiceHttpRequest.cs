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
using System.Reflection;
using System.Text;
using HttpServer;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceHttpRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly PollServiceEventArgs PollServiceArgs;
        public readonly IHttpClientContext HttpContext;
        public readonly IHttpRequest Request;
        public readonly int RequestTime;
        public readonly UUID RequestID;

        public PollServiceHttpRequest(
            PollServiceEventArgs pPollServiceArgs, IHttpClientContext pHttpContext, IHttpRequest pRequest)
        {
            PollServiceArgs = pPollServiceArgs;
            HttpContext = pHttpContext;
            Request = pRequest;
            RequestTime = System.Environment.TickCount;
            RequestID = UUID.Random();
        }

        internal void DoHTTPGruntWork(BaseHttpServer server, Hashtable responsedata)
        {
            OSHttpResponse response
                = new OSHttpResponse(new HttpResponse(HttpContext, Request), HttpContext);

            byte[] buffer = server.DoHTTPGruntWork(responsedata, response);

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[POLL SERVICE WORKER THREAD]: Error ", ex);
            }
            finally
            {
                //response.OutputStream.Close();
                try
                {
                    response.OutputStream.Flush();
                    response.Send();

                    //if (!response.KeepAlive && response.ReuseContext)
                    //    response.FreeContext();
                }
                catch (Exception e)
                {
                    m_log.Warn("[POLL SERVICE WORKER THREAD]: Error ", e);
                }

                PollServiceArgs.RequestsHandled++;
            }
        }
    }
}