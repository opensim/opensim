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
using System.Text.RegularExpressions;

namespace OpenSim.Framework.Servers
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
    ///     <term>Handled</term>
    ///     <description>handler did process the request, OSHttpServer
    ///       can clean up and close the request</request>
    ///   </item>
    ///   <item>
    ///     <term>Detached</term>
    ///     <description>handler handles the request, OSHttpServer
    ///       can forget about the request and should not touch it as
    ///       the handler has taken control</request>
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

    public interface OSHttpHandler
    {
        /// <summary>
        /// Regular expression used to match against path of incoming
        /// HTTP request. If you want to match any string either use
        /// '.*' or null. To match for the emtpy string use '^$'
        /// </summary>
        Regex Path 
        {
            get;
        }

        /// <summary>
        /// Dictionary of (header name, regular expression) tuples,
        /// allowing us to match on HTTP header fields.
        /// </summary>
        Dictionary<string, Regex> Headers
        { 
            get;
        }

        /// <summary>
        /// Dictionary of (header name, regular expression) tuples,
        /// allowing us to match on HTTP header fields.
        /// </summary>
        /// <remarks>
        /// This feature is currently not implemented as it requires
        /// (trivial) changes to HttpServer.HttpListener that have not
        /// been implemented.
        /// </remarks>
        Regex IPEndPointWhitelist
        {
            get;
        }


        /// <summary>
        /// An OSHttpHandler that matches on the "content-type" header can
        /// supply an OSHttpContentTypeChecker delegate which will be
        /// invoked by the request matcher in OSHttpRequestPump.
        /// </summary>
        /// <returns>true if the handler is interested in the content;
        /// false otherwise</returns>
        OSHttpContentTypeChecker ContentTypeChecker
        { 
            get;
        }

        OSHttpHandlerResult Process(OSHttpRequest request);
    }
}