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
using System.Net;
using System.Reflection;
using System.Text;
using OSHttpServer;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceHttpRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly PollServiceEventArgs PollServiceArgs;
        public readonly IHttpRequest Request;
        public readonly int RequestTime;
        public readonly UUID RequestID;

        public PollServiceHttpRequest(PollServiceEventArgs pPollServiceArgs, IHttpRequest pRequest)
        {
            PollServiceArgs = pPollServiceArgs;
            Request = pRequest;
            RequestTime = System.Environment.TickCount;
            RequestID = UUID.Random();
        }

        internal void DoHTTPGruntWork(Hashtable responsedata)
        {
            if (Request.Body.CanRead)
                Request.Body.Dispose();

            if(responsedata.Contains("h"))
            {
                OSHttpResponse r = (OSHttpResponse)responsedata["h"];
                try
                {
                    r.Send();
                }
                catch { }
                PollServiceArgs.RequestsHandled++;
                return;
            }

            OSHttpResponse response = new OSHttpResponse(new HttpResponse(Request));

            if (responsedata == null)
            {
                SendNoContentError(response);
                return;
            }

            int responsecode = 200;
            string responseString = String.Empty;
            string contentType;
            byte[] buffer = null;
            int rangeStart = 0;
            int rangeLen = -1;

            try
            {
                //m_log.Info("[BASE HTTP SERVER]: Doing HTTP Grunt work with response");
                if(responsedata["int_response_code"] != null)
                    responsecode = (int)responsedata["int_response_code"];

                if (responsedata["bin_response_data"] != null)
                {
                    buffer = (byte[])responsedata["bin_response_data"];
                    responsedata["bin_response_data"] = null;

                    if (responsedata["bin_start"] != null)
                        rangeStart = (int)responsedata["bin_start"];

                    if (responsedata["int_bytes"] != null)
                        rangeLen = (int)responsedata["int_bytes"];
                }
                else
                    responseString = (string)responsedata["str_response_string"];

                contentType = (string)responsedata["content_type"];
                if (responseString == null)
                    responseString = String.Empty;
            }
            catch
            {
                SendNoContentError(response);
                return;
            }

            if (responsecode == (int)HttpStatusCode.Moved)
            {
                response.Redirect((string)responsedata["str_redirect_location"], HttpStatusCode.Moved);
                response.KeepAlive = false;
                PollServiceArgs.RequestsHandled++;
                response.Send();
                return;
            }

            response.StatusCode = responsecode;

            if (responsedata.ContainsKey("http_protocol_version"))
                response.ProtocolVersion = (string)responsedata["http_protocol_version"];

            if (responsedata.ContainsKey("keepalive"))
                response.KeepAlive = (bool)responsedata["keepalive"];

            if (responsedata.ContainsKey("keepaliveTimeout"))
                response.KeepAliveTimeout = (int)responsedata["keepaliveTimeout"];

            if (responsedata.ContainsKey("error_status_text"))
                response.StatusDescription = (string)responsedata["error_status_text"];

            // Cross-Origin Resource Sharing with simple requests
            if (responsedata.ContainsKey("access_control_allow_origin"))
                response.AddHeader("Access-Control-Allow-Origin", (string)responsedata["access_control_allow_origin"]);

            if (string.IsNullOrEmpty(contentType))
                response.AddHeader("Content-Type", "text/html");
            else
                response.AddHeader("Content-Type", contentType);

            if (responsedata.ContainsKey("headers"))
            {
                Hashtable headerdata = (Hashtable)responsedata["headers"];

                foreach (string header in headerdata.Keys)
                    response.AddHeader(header, headerdata[header].ToString());
            }

            if(buffer == null)
            {
                if (contentType != null && (!(contentType.Contains("image")
                    || contentType.Contains("x-shockwave-flash")
                    || contentType.Contains("application/x-oar")
                    || contentType.Contains("application/vnd.ll.mesh"))))
                {
                    // Text
                    buffer = Encoding.UTF8.GetBytes(responseString);
                }
                else
                {
                    // Binary!
                    buffer = Convert.FromBase64String(responseString);
                }
                response.ContentEncoding = Encoding.UTF8;
            }

            if (rangeStart < 0 || rangeStart > buffer.Length)
                rangeStart = 0;

            if (rangeLen < 0)
                rangeLen = buffer.Length;
            else if (rangeLen + rangeStart > buffer.Length)
                rangeLen = buffer.Length - rangeStart;

            response.ContentLength64 = rangeLen;

            try
            {
                if(rangeLen > 0)
                {
                    response.RawBufferStart = rangeStart;
                    response.RawBufferLen = rangeLen;
                    response.RawBuffer = buffer;
                    //response.OutputStream.Write(buffer, rangeStart, rangeLen);
                }

                buffer = null;

                response.Send();
            }
            catch (Exception ex)
            {
                if(ex is System.Net.Sockets.SocketException)
                {
                    // only mute connection reset by peer so we are not totally blind for now
                    if(((System.Net.Sockets.SocketException)ex).SocketErrorCode != System.Net.Sockets.SocketError.ConnectionReset)
                         m_log.Warn("[POLL SERVICE WORKER THREAD]: Error ", ex);
                }
                else
                    m_log.Warn("[POLL SERVICE WORKER THREAD]: Error ", ex);
            }

            PollServiceArgs.RequestsHandled++;
        }

        internal void SendNoContentError(OSHttpResponse response)
        {
            response.ContentLength64 = 0;
            response.ContentEncoding = Encoding.UTF8;
            response.StatusCode = 500;

            try
            {
                response.Send();
            }
            catch { }
            return;
        }

        internal void DoHTTPstop()
        {
            OSHttpResponse response = new OSHttpResponse(new HttpResponse(Request));

            if(Request.Body.CanRead)
                Request.Body.Dispose();

            response.ContentLength64 = 0;
            response.ContentEncoding = Encoding.UTF8;
            response.KeepAlive = false;
            response.StatusCode = 503;

            try
            {
                response.Send();
            }
            catch
            {
            }
        }
    }
}