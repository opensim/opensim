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

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// HTTP status codes (almost) as defined by W3C in http://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html and IETF in http://tools.ietf.org/html/rfc6585
    /// </summary>
    public enum OSHttpStatusCode : int
    {
        #region 1xx Informational status codes providing a provisional response.

        /// <summary>
        /// 100 Tells client that to keep on going sending its request
        /// </summary>
        InfoContinue = 100,

        /// <summary>
        /// 101 Server understands request, proposes to switch to different application level protocol
        /// </summary>
        InfoSwitchingProtocols = 101,

        #endregion

        #region 2xx Success codes

        /// <summary>
        /// 200 Request successful
        /// </summary>
        SuccessOk = 200,

        /// <summary>
        /// 201 Request successful, new resource created
        /// </summary>
        SuccessOkCreated = 201,

        /// <summary>
        /// 202 Request accepted, processing still on-going
        /// </summary>
        SuccessOkAccepted = 202,

        /// <summary>
        /// 203 Request successful, meta information not authoritative
        /// </summary>
        SuccessOkNonAuthoritativeInformation = 203,

        /// <summary>
        /// 204 Request successful, nothing to return in the body
        /// </summary>
        SuccessOkNoContent = 204,

        /// <summary>
        /// 205 Request successful, reset displayed content
        /// </summary>
        SuccessOkResetContent = 205,

        /// <summary>
        /// 206 Request successful, partial content returned
        /// </summary>
        SuccessOkPartialContent = 206,

        #endregion

        #region 3xx Redirect code: user agent needs to go somewhere else

        /// <summary>
        /// 300 Redirect: different presentation forms available, take a pick
        /// </summary>
        RedirectMultipleChoices = 300,

        /// <summary>
        /// 301 Redirect: requested resource has moved and now lives somewhere else
        /// </summary>
        RedirectMovedPermanently = 301,

        /// <summary>
        /// 302 Redirect: Resource temporarily somewhere else, location might change
        /// </summary>
        RedirectFound = 302,

        /// <summary>
        /// 303 Redirect: See other as result of a POST
        /// </summary>
        RedirectSeeOther = 303,

        /// <summary>
        /// 304 Redirect: Resource still the same as before
        /// </summary>
        RedirectNotModified = 304,

        /// <summary>
        /// 305 Redirect: Resource must be accessed via proxy provided in location field
        /// </summary>
        RedirectUseProxy = 305,

        /// <summary>
        /// 307 Redirect: Resource temporarily somewhere else, location might change
        /// </summary>
        RedirectMovedTemporarily = 307,

        #endregion

        #region 4xx Client error: the client borked the request

        /// <summary>
        /// 400 Client error: bad request, server does not grok what the client wants
        /// </summary>
        ClientErrorBadRequest = 400,

        /// <summary>
        /// 401 Client error: the client is not authorized, response provides WWW-Authenticate header field with a challenge
        /// </summary>
        ClientErrorUnauthorized = 401,

        /// <summary>
        /// 402 Client error: Payment required (reserved for future use)
        /// </summary>
        ClientErrorPaymentRequired = 402,

        /// <summary>
        /// 403 Client error: Server understood request, will not deliver, do not try again.
        ClientErrorForbidden = 403,

        /// <summary>
        /// 404 Client error: Server cannot find anything matching the client request.
        /// </summary>
        ClientErrorNotFound = 404,

        /// <summary>
        /// 405 Client error: The method specified by the client in the request is not allowed for the resource requested
        /// </summary>
        ClientErrorMethodNotAllowed = 405,

        /// <summary>
        /// 406 Client error: Server cannot generate suitable response for the resource and content characteristics requested by the client
        /// </summary>
        ClientErrorNotAcceptable = 406,

        /// <summary>
        /// 407 Client error: Similar to 401, Server requests that client authenticate itself with the proxy first
        /// </summary>
        ClientErrorProxyAuthRequired = 407,

        /// <summary>
        /// 408 Client error: Server got impatient with client and decided to give up waiting for the client's request to arrive
        /// </summary>
        ClientErrorRequestTimeout = 408,

        /// <summary>
        /// 409 Client error: Server could not fulfill the request for a resource as there is a conflict with the current state of the resource but thinks client can do something about this
        /// </summary>
        ClientErrorConflict = 409,

        /// <summary>
        /// 410 Client error: The resource has moved somewhere else, but server has no clue where.
        /// </summary>
        ClientErrorGone = 410,

        /// <summary>
        /// 411 Client error: The server is picky again and insists on having a content-length header field in the request
        /// </summary>
        ClientErrorLengthRequired = 411,

        /// <summary>
        /// 412 Client error: one or more preconditions supplied in the client's request is false
        /// </summary>
        ClientErrorPreconditionFailed = 412,

        /// <summary>
        /// 413 Client error: For fear of reflux, the server refuses to swallow that much data.
        /// </summary>
        ClientErrorRequestEntityToLarge = 413,

        /// <summary>
        /// 414 Client error: The server considers the Request-URI to be indecently long and refuses to even look at it.
        /// </summary>
        ClientErrorRequestURITooLong = 414,

        /// <summary>
        /// 415 Client error: The server has no clue about the media type requested by the client (contrary to popular belief it is not a warez server)
        /// </summary>
        ClientErrorUnsupportedMediaType = 415,

        /// <summary>
        /// 416 Client error: The requested range cannot be delivered by the server.
        /// </summary>
        ClientErrorRequestRangeNotSatisfiable = 416,

        /// <summary>
        /// 417 Client error: The expectations of the client as expressed in one or more Expect header fields cannot be met by the server, the server is awfully sorry about this.
        /// </summary>
        ClientErrorExpectationFailed = 417,

        /// <summary>
        /// 428 Client error :The 428 status code indicates that the origin server requires the request to be conditional.
        /// </summary>
        ClientErrorPreconditionRequired = 428,

        /// <summary>
        /// 429 Client error: The 429 status code indicates that the user has sent too many requests in a given amount of time ("rate limiting").
        /// </summary>
        ClientErrorTooManyRequests = 429,

        /// <summary>
        /// 431 Client error: The 431 status code indicates that the server is unwilling to process the request because its header fields are too large.  The request MAY be resubmitted after reducing the size of the request header fields.
        /// </summary>
        ClientErrorRequestHeaderFieldsTooLarge = 431,

        /// <summary>
        /// 499 Client error: Wildcard error.
        /// </summary>
        ClientErrorJoker = 499,

        #endregion

        #region 5xx Server errors (rare)

        /// <summary>
        /// 500 Server error: something really strange and unexpected happened
        /// </summary>
        ServerErrorInternalError = 500,

        /// <summary>
        /// 501 Server error: The server does not do the functionality required to carry out the client request. not at all. certainly not before breakfast. but also not after breakfast.
        /// </summary>
        ServerErrorNotImplemented = 501,

        /// <summary>
        /// 502 Server error: While acting as a proxy or a gateway, the server got ditched by the upstream server and as a consequence regretfully cannot fulfill the client's request
        /// </summary>
        ServerErrorBadGateway = 502,

        /// <summary>
        /// 503 Server error: Due to unforseen circumstances the server cannot currently deliver the service requested. Retry-After header might indicate when to try again.
        /// </summary>
        ServerErrorServiceUnavailable = 503,

        /// <summary>
        /// 504 Server error: The server blames the upstream server for not being able to deliver the service requested and claims that the upstream server is too slow delivering the goods.
        /// </summary>
        ServerErrorGatewayTimeout = 504,

        /// <summary>
        /// 505 Server error: The server does not support the HTTP version conveyed in the client's request.
        /// </summary>
        ServerErrorHttpVersionNotSupported = 505,

        /// <summary>
        /// 511 Server error: The 511 status code indicates that the client needs to authenticate to gain network access.
        /// </summary>
        ServerErrorNetworkAuthenticationRequired = 511,

        #endregion
    }
}
