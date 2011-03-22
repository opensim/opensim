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
using System.Text.RegularExpressions;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <sumary>
    /// Any OSHttpHandler must return one of the following results:
    /// <list type = "table">
    ///   <listheader>
    ///     <term>result code</term>
    ///     <description>meaning</description>
    ///   </listheader>
    ///   <item>
    ///     <term>Pass</term>
    ///     <description>handler did not process the request</request>
    ///   </item>
    ///   <item>
    ///     <term>Done</term>
    ///     <description>handler did process the request, OSHttpServer
    ///       can clean up and close the request</request>
    ///   </item>
    /// </list>
    /// </summary>
    public enum OSHttpHandlerResult
    {
        Unprocessed,
        Pass,
        Done,
    }

    /// <summary>
    /// An OSHttpHandler that matches on the "content-type" header can
    /// supply an OSHttpContentTypeChecker delegate which will be
    /// invoked by the request matcher in OSHttpRequestPump.
    /// </summary>
    /// <returns>true if the handler is interested in the content;
    /// false otherwise</returns>
    public delegate bool OSHttpContentTypeChecker(OSHttpRequest req);

    public abstract class OSHttpHandler
    {
        /// <summary>
        /// Regular expression used to match against method of
        /// the incoming HTTP request. If you want to match any string
        /// either use '.*' or null. To match on the empty string use
        /// '^$'.
        /// </summary>
        public virtual Regex Method
        {
            get { return _method; }
        }
        protected Regex _method;

        /// <summary>
        /// Regular expression used to match against path of the
        /// incoming HTTP request. If you want to match any string
        /// either use '.*' or null. To match on the empty string use
        /// '^$'.
        /// </summary>
        public virtual Regex Path
        {
            get { return _path; }
        }
        protected Regex _path;

        /// <summary>
        /// Dictionary of (query name, regular expression) tuples,
        /// allowing us to match on URI query fields.
        /// </summary>
        public virtual Dictionary<string, Regex> Query
        {
            get { return _query; }
        }
        protected Dictionary<string, Regex> _query;

        /// <summary>
        /// Dictionary of (header name, regular expression) tuples,
        /// allowing us to match on HTTP header fields.
        /// </summary>
        public virtual Dictionary<string, Regex> Headers
        {
            get { return _headers; }
        }
        protected Dictionary<string, Regex> _headers;

        /// <summary>
        /// Dictionary of (header name, regular expression) tuples,
        /// allowing us to match on HTTP header fields.
        /// </summary>
        /// <remarks>
        /// This feature is currently not implemented as it requires
        /// (trivial) changes to HttpServer.HttpListener that have not
        /// been implemented.
        /// </remarks>
        public virtual Regex IPEndPointWhitelist
        {
            get { return _ipEndPointRegex; }
        }
        protected Regex _ipEndPointRegex;


        /// <summary>
        /// Base class constructor.
        /// </summary>
        /// <param name="path">null or path regex</param>
        /// <param name="headers">null or dictionary of header
        /// regexs</param>
        /// <param name="contentType">null or content type
        /// regex</param>
        /// <param name="whitelist">null or IP address regex</param>
        public OSHttpHandler(Regex method, Regex path, Dictionary<string, Regex> query,
                             Dictionary<string, Regex> headers, Regex contentType, Regex whitelist)
        {
            _method = method;
            _path = path;
            _query = query;
            _ipEndPointRegex = whitelist;

            if (null == _headers && null != contentType)
            {
                _headers = new Dictionary<string, Regex>();
                _headers.Add("content-type", contentType);
            }
        }


        /// <summary>
        /// Process an incoming OSHttpRequest that matched our
        /// requirements.
        /// </summary>
        /// <returns>
        /// OSHttpHandlerResult.Pass if we are after all not
        /// interested in the request; OSHttpHandlerResult.Done if we
        /// did process the request.
        /// </returns>
        public abstract OSHttpHandlerResult Process(OSHttpRequest request);

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            sw.WriteLine("{0}", base.ToString());
            sw.WriteLine("    method regex     {0}", null == Method ? "null" : Method.ToString());
            sw.WriteLine("    path regex       {0}", null == Path ? "null": Path.ToString());
            foreach (string tag in Headers.Keys)
            {
                sw.WriteLine("    header           {0} : {1}", tag, Headers[tag].ToString());
            }
            sw.WriteLine("    IP whitelist     {0}", null == IPEndPointWhitelist ? "null" : IPEndPointWhitelist.ToString());
            sw.WriteLine();
            sw.Close();
            return sw.ToString();
        }
    }
}
