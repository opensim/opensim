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

using System.Collections;
using System.IO;

namespace OpenSim.Framework.Servers.HttpServer
{
    public interface IRequestHandler
    {
        /// <summary>
        /// Name for this handler.
        /// </summary>
        /// <remarks>
        /// Used for diagnostics.  The path doesn't always describe what the handler does.  Can be null if none
        /// specified.
        /// </remarks>
        string Name { get; }

        /// <summary>
        /// Description for this handler.
        /// </summary>
        /// <remarks>
        /// Used for diagnostics.  The path doesn't always describe what the handler does.  Can be null if none
        /// specified.
        /// </remarks>
        string Description { get; }

        // Return response content type
        string ContentType { get; }

        // Return required http method
        string HttpMethod { get; }

        // Return path
        string Path { get; }

        /// <summary>
        /// Number of requests received by this handler
        /// </summary>
        int RequestsReceived { get; }

        /// <summary>
        /// Number of requests handled.
        /// </summary>
        /// <remarks>
        /// Should be equal to RequestsReceived unless requested are being handled slowly or there is deadlock.
        /// </remarks>
        int RequestsHandled { get; }
    }

    public interface IStreamedRequestHandler : IRequestHandler
    {
        // Handle request stream, return byte array
        byte[] Handle(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse);
    }

    public interface IStreamHandler : IRequestHandler
    {
        void Handle(string path, Stream request, Stream response, IOSHttpRequest httpReqbuest, IOSHttpResponse httpResponse);
    }

    public interface IGenericHTTPHandler : IRequestHandler
    {
        Hashtable Handle(string path, Hashtable request);
    }
}