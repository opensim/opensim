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
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public enum HttpRequestConstants
    {
        HTTP_METHOD = 0,
        HTTP_MIMETYPE = 1,
        HTTP_BODY_MAXLENGTH = 2,
        HTTP_VERIFY_CERT = 3,
        HTTP_VERBOSE_THROTTLE = 4,
        HTTP_CUSTOM_HEADER = 5,
        HTTP_PRAGMA_NO_CACHE = 6
    }

    /// <summary>
    /// The initial status of the request before it is placed on the wire.
    /// </summary>
    /// <remarks>
    /// The request may still fail later on, in which case the normal HTTP status is set.
    /// </remarks>
    [Flags]
    public enum HttpInitialRequestStatus
    {
        OK = 1,
        DISALLOWED_BY_FILTER = 2
    }

    public interface IHttpRequestModule
    {
        UUID MakeHttpRequest(string url, string parameters, string body);
        /// <summary>
        /// Starts the http request.
        /// </summary>
        /// <remarks>
        /// This is carried out asynchronously unless it fails initial checks.  Results are fetched by the script engine
        /// HTTP requests module to be distributed back to scripts via a script event.
        /// </remarks>
        /// <returns>The ID of the request.  If the requested could not be performed then this is UUID.Zero</returns>
        /// <param name="localID">Local ID of the object containing the script making the request.</param>
        /// <param name="itemID">Item ID of the script making the request.</param>
        /// <param name="url">Url to request.</param>
        /// <param name="parameters">LSL parameters for the request.</param>
        /// <param name="headers">Extra headers for the request.</param>
        /// <param name="body">Body of the request.</param>
        /// <param name="status">
        /// Initial status of the request.  If OK then the request is actually made to the URL.  Subsequent status is
        /// then returned via IServiceRequest when the response is asynchronously fetched.
        /// </param>
        UUID StartHttpRequest(
            uint localID, UUID itemID, string url, List<string> parameters, Dictionary<string, string> headers, string body, 
            out HttpInitialRequestStatus status);

        /// <summary>
        /// Stop and remove all http requests for the given script.
        /// </summary>
        /// <param name='id'></param>
        void StopHttpRequest(uint m_localID, UUID m_itemID);
        IServiceRequest GetNextCompletedRequest();
        void RemoveCompletedRequest(UUID id);
    }
}
