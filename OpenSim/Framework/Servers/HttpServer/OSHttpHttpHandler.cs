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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Servers.HttpServer
{
    public delegate XmlRpcResponse OSHttpHttpProcessor(XmlRpcRequest request);

    public class OSHttpHttpHandler: OSHttpHandler
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // contains handler for processing HTTP Request
        private GenericHTTPMethod _handler;

        /// <summary>
        /// Instantiate an HTTP handler.
        /// </summary>
        /// <param name="handler">a GenericHTTPMethod</param>
        /// <param name="method">null or HTTP method regex</param>
        /// <param name="path">null or path regex</param>
        /// <param name="query">null or dictionary with query regexs</param>
        /// <param name="headers">null or dictionary with header
        /// regexs</param>
        /// <param name="whitelist">null or IP address whitelist</param>
        public OSHttpHttpHandler(GenericHTTPMethod handler, Regex method, Regex path,
                                 Dictionary<string, Regex> query,
                                 Dictionary<string, Regex> headers, Regex whitelist)
            : base(method, path, query, headers, new Regex(@"^text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                   whitelist)
        {
            _handler = handler;
        }

        /// <summary>
        /// Instantiate an HTTP handler.
        /// </summary>
        /// <param name="handler">a GenericHTTPMethod</param>
        public OSHttpHttpHandler(GenericHTTPMethod handler)
            : this(handler, new Regex(@"^GET$", RegexOptions.IgnoreCase | RegexOptions.Compiled), null, null, null, null)
        {
        }

        /// <summary>
        /// Invoked by OSHttpRequestPump.
        /// </summary>
        public override OSHttpHandlerResult Process(OSHttpRequest request)
        {
            // call handler method
            Hashtable responseData = _handler(request.Query);

            int responseCode = (int)responseData["int_response_code"];
            string responseString = (string)responseData["str_response_string"];
            string contentType = (string)responseData["content_type"];

            //Even though only one other part of the entire code uses HTTPHandlers, we shouldn't expect this
            //and should check for NullReferenceExceptions

            if (string.IsNullOrEmpty(contentType))
            {
                contentType = "text/html";
            }

            OSHttpResponse response = new OSHttpResponse(request);

            // We're forgoing the usual error status codes here because the client
            // ignores anything but 200 and 301

            response.StatusCode = (int)OSHttpStatusCode.SuccessOk;

            if (responseCode == (int)OSHttpStatusCode.RedirectMovedPermanently)
            {
                response.RedirectLocation = (string)responseData["str_redirect_location"];
                response.StatusCode = responseCode;
            }

            response.AddHeader("Content-type", contentType);

            byte[] buffer;

            if (!contentType.Contains("image"))
            {
                buffer = Encoding.UTF8.GetBytes(responseString);
            }
            else
            {
                buffer = Convert.FromBase64String(responseString);
            }

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            try
            {
                response.Body.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                _log.ErrorFormat("[OSHttpHttpHandler]: Error:  {0}", ex.Message);
            }
            finally
            {
                response.Send();
            }

            return OSHttpHandlerResult.Done;
        }
    }
}
