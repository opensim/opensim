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
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Servers.HttpServer
{
    public delegate XmlRpcResponse OSHttpXmlRpcProcessor(XmlRpcRequest request);

    public class OSHttpXmlRpcHandler: OSHttpHandler
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// XmlRpcMethodMatch tries to reify (deserialize) an incoming
        /// XmlRpc request (and posts it to the "whiteboard") and
        /// checks whether the method name is one we are interested
        /// in.
        /// </summary>
        /// <returns>true if the handler is interested in the content;
        /// false otherwise</returns>
        protected bool XmlRpcMethodMatch(OSHttpRequest req)
        {
            XmlRpcRequest xmlRpcRequest = null;

            // check whether req is already reified
            // if not: reify (and post to whiteboard)
            try
            {
                if (req.Whiteboard.ContainsKey("xmlrequest"))
                {
                    xmlRpcRequest = req.Whiteboard["xmlrequest"] as XmlRpcRequest;
                }
                else
                {
                    StreamReader body = new StreamReader(req.InputStream);
                    string requestBody = body.ReadToEnd();
                    xmlRpcRequest = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
                    req.Whiteboard["xmlrequest"] = xmlRpcRequest;
                }
            }
            catch (XmlException)
            {
                _log.ErrorFormat("[OSHttpXmlRpcHandler] failed to deserialize XmlRpcRequest from {0}", req.ToString());
                return false;
            }

            // check against methodName
            if ((null != xmlRpcRequest)
                && !String.IsNullOrEmpty(xmlRpcRequest.MethodName)
                && xmlRpcRequest.MethodName == _methodName)
            {
                _log.DebugFormat("[OSHttpXmlRpcHandler] located handler {0} for {1}", _methodName, req.ToString());
                return true;
            }

            return false;
        }

        // contains handler for processing XmlRpc Request
        private XmlRpcMethod _handler;

        // contains XmlRpc method name
        private string _methodName;


        /// <summary>
        /// Instantiate an XmlRpc handler.
        /// </summary>
        /// <param name="handler">XmlRpcMethod
        /// delegate</param>
        /// <param name="methodName">XmlRpc method name</param>
        /// <param name="path">XmlRpc path prefix (regular expression)</param>
        /// <param name="headers">Dictionary with header names and
        /// regular expressions to match content of headers</param>
        /// <param name="whitelist">IP whitelist of remote end points
        /// to accept (regular expression)</param>
        /// <remarks>
        /// Except for handler and methodName, all other parameters
        /// can be null, in which case they are not taken into account
        /// when the handler is being looked up.
        /// </remarks>
        public OSHttpXmlRpcHandler(XmlRpcMethod handler, string methodName, Regex path,
                                   Dictionary<string, Regex> headers, Regex whitelist)
            : base(new Regex(@"^POST$", RegexOptions.IgnoreCase | RegexOptions.Compiled), path, null, headers,
                   new Regex(@"^(text|application)/xml", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                   whitelist)
        {
            _handler = handler;
            _methodName = methodName;
        }


        /// <summary>
        /// Instantiate an XmlRpc handler.
        /// </summary>
        /// <param name="handler">XmlRpcMethod
        /// delegate</param>
        /// <param name="methodName">XmlRpc method name</param>
        public OSHttpXmlRpcHandler(XmlRpcMethod handler, string methodName)
            : this(handler, methodName, null, null, null)
        {
        }


        /// <summary>
        /// Invoked by OSHttpRequestPump.
        /// </summary>
        public override OSHttpHandlerResult Process(OSHttpRequest request)
        {
            XmlRpcResponse xmlRpcResponse;
            string responseString;

            // check whether we are interested in this request
            if (!XmlRpcMethodMatch(request)) return OSHttpHandlerResult.Pass;


            OSHttpResponse resp = new OSHttpResponse(request);
            try
            {
                // reified XmlRpcRequest must still be on the whiteboard
                XmlRpcRequest xmlRpcRequest = request.Whiteboard["xmlrequest"] as XmlRpcRequest;
                xmlRpcResponse = _handler(xmlRpcRequest);
                responseString = XmlRpcResponseSerializer.Singleton.Serialize(xmlRpcResponse);

                resp.ContentType = "text/xml";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                resp.SendChunked = false;
                resp.ContentLength = buffer.Length;
                resp.ContentEncoding = Encoding.UTF8;

                resp.Body.Write(buffer, 0, buffer.Length);
                resp.Body.Flush();

                resp.Send();

            }
            catch (Exception ex)
            {
                _log.WarnFormat("[OSHttpXmlRpcHandler]: Error: {0}", ex.Message);
                return OSHttpHandlerResult.Pass;
            }
            return OSHttpHandlerResult.Done;
        }
    }
}
