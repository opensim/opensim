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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Servers
{
    public delegate XmlRpcResponse OSHttpXmlRpcProcessor(XmlRpcRequest request);

    public class OSHttpXmlRpcHandler: OSHttpHandler
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Regular expression used to match against path of incoming
        /// HTTP request. If you want to match any string either use
        /// '.*' or null. To match for the emtpy string use '^$'
        /// </summary>
        public Regex Path 
        {
            get { return _pathsRegex; }
        }
        private Regex _pathsRegex;

        /// <summary>
        /// Dictionary of (header name, regular expression) tuples,
        /// allowing us to match on HTTP header fields.
        /// </summary>
        public Dictionary<string, Regex> Headers
        { 
            get { return _headers; }
        }
        private Dictionary<string, Regex> _headers;

        /// <summary>
        /// Regex to whitelist IP end points. A null value disables
        /// checking of IP end points.
        /// </summary>
        /// <remarks>
        /// This feature is currently not implemented as it requires
        /// (trivial) changes to HttpServer.HttpListener that have not
        /// been implemented.
        /// </remarks>
        public Regex IPEndPointWhitelist
        {
            get { return _ipEndPointRegex; }
        }
        private Regex _ipEndPointRegex;

        /// <summary>
        /// An OSHttpHandler that matches on the "content-type" header can
        /// supply an OSHttpContentTypeChecker delegate which will be
        /// invoked by the request matcher in OSHttpRequestPump.
        /// </summary>
        /// <returns>true if the handler is interested in the content;
        /// false otherwise</returns>
        public OSHttpContentTypeChecker ContentTypeChecker
        { 
            get 
            { 
                return delegate(OSHttpRequest req)
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
                };
            }
        }

        // contains handler for processing XmlRpc Request
        private OSHttpXmlRpcProcessor _handler;
        
        // contains XmlRpc method name
        private string _methodName;


        /// <summary>
        /// Instantiate an XmlRpc handler.
        /// </summary>
        /// <param name="handler">OSHttpXmlRpcProcessor
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
        public OSHttpXmlRpcHandler(OSHttpXmlRpcProcessor handler, string methodName, Regex path, 
                                   Dictionary<string, Regex> headers, Regex whitelist)
        {
            _handler = handler;
            _pathsRegex = path;
            _methodName = methodName;

            if (null == _headers) _headers = new Dictionary<string, Regex>();
            _headers.Add("content-type", new Regex(@"^(text|application)/xml", RegexOptions.IgnoreCase | 
                                                   RegexOptions.Compiled));

            _ipEndPointRegex = whitelist;
        }


        /// <summary>
        /// Instantiate an XmlRpc handler.
        /// </summary>
        /// <param name="handler">OSHttpXmlRpcProcessor
        /// delegate</param>
        /// <param name="methodName">XmlRpc method name</param>
        public OSHttpXmlRpcHandler(OSHttpXmlRpcProcessor handler, string methodName)
            : this(handler, methodName, null, null, null)
        {
        }


        /// <summary>
        /// Invoked by OSHttpRequestPump.
        /// </summary>
        public OSHttpHandlerResult Process(OSHttpRequest request) 
        {
            XmlRpcResponse xmlRpcResponse;
            string responseString;

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