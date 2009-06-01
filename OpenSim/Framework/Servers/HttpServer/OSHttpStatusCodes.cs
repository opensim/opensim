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
    /// HTTP status codes (almost) as defined by W3C in
    /// http://www.w3.org/Protocols/rfc2616/rfc2616-sec10.html
    /// </summary>
    public enum OSHttpStatusCode: int
    {
        // 1xx Informational status codes providing a provisional
        // response.
        // 100 Tells client that to keep on going sending its request
        InfoContinue                          = 100,
        // 101 Server understands request, proposes to switch to different
        // application level protocol
        InfoSwitchingProtocols                = 101,


        // 2xx Success codes
        // 200 Request successful
        SuccessOk                             = 200,
        // 201 Request successful, new resource created
        SuccessOkCreated                      = 201,
        // 202 Request accepted, processing still on-going
        SuccessOkAccepted                     = 202,
        // 203 Request successful, meta information not authoritative
        SuccessOkNonAuthoritativeInformation  = 203,
        // 204 Request successful, nothing to return in the body
        SuccessOkNoContent                    = 204,
        // 205 Request successful, reset displayed content
        SuccessOkResetContent                 = 205,
        // 206 Request successful, partial content returned
        SuccessOkPartialContent               = 206,

        // 3xx Redirect code: user agent needs to go somewhere else
        // 300 Redirect: different presentation forms available, take
        // a pick
        RedirectMultipleChoices               = 300,
        // 301 Redirect: requested resource has moved and now lives
        // somewhere else
        RedirectMovedPermanently              = 301,
        // 302 Redirect: Resource temporarily somewhere else, location
        // might change
        RedirectFound                         = 302,
        // 303 Redirect: See other as result of a POST
        RedirectSeeOther                      = 303,
        // 304 Redirect: Resource still the same as before
        RedirectNotModified                   = 304,
        // 305 Redirect: Resource must be accessed via proxy provided
        // in location field
        RedirectUseProxy                      = 305,
        // 307 Redirect: Resource temporarily somewhere else, location
        // might change
        RedirectMovedTemporarily              = 307,

        // 4xx Client error: the client borked the request
        // 400 Client error: bad request, server does not grok what
        // the client wants
        ClientErrorBadRequest                 = 400,
        // 401 Client error: the client is not authorized, response
        // provides WWW-Authenticate header field with a challenge
        ClientErrorUnauthorized               = 401,
        // 402 Client error: Payment required (reserved for future use)
        ClientErrorPaymentRequired            = 402,
        // 403 Client error: Server understood request, will not
        // deliver, do not try again.
        ClientErrorForbidden                  = 403,
        // 404 Client error: Server cannot find anything matching the
        // client request.
        ClientErrorNotFound                   = 404,
        // 405 Client error: The method specified by the client in the
        // request is not allowed for the resource requested
        ClientErrorMethodNotAllowed           = 405,
        // 406 Client error: Server cannot generate suitable response
        // for the resource and content characteristics requested by
        // the client
        ClientErrorNotAcceptable              = 406,
        // 407 Client error: Similar to 401, Server requests that
        // client authenticate itself with the proxy first
        ClientErrorProxyAuthRequired          = 407,
        // 408 Client error: Server got impatient with client and
        // decided to give up waiting for the client's request to
        // arrive
        ClientErrorRequestTimeout             = 408,
        // 409 Client error: Server could not fulfill the request for
        // a resource as there is a conflict with the current state of
        // the resource but thinks client can do something about this
        ClientErrorConflict                   = 409,
        // 410 Client error: The resource has moved somewhere else,
        // but server has no clue where.
        ClientErrorGone                       = 410,
        // 411 Client error: The server is picky again and insists on
        // having a content-length header field in the request
        ClientErrorLengthRequired             = 411,
        // 412 Client error: one or more preconditions supplied in the
        // client's request is false
        ClientErrorPreconditionFailed         = 412,
        // 413 Client error: For fear of reflux, the server refuses to
        // swallow that much data.
        ClientErrorRequestEntityToLarge       = 413,
        // 414 Client error: The server considers the Request-URI to
        // be indecently long and refuses to even look at it.
        ClientErrorRequestURITooLong          = 414,
        // 415 Client error: The server has no clue about the media
        // type requested by the client (contrary to popular belief it
        // is not a warez server)
        ClientErrorUnsupportedMediaType       = 415,
        // 416 Client error: The requested range cannot be delivered
        // by the server.
        ClientErrorRequestRangeNotSatisfiable = 416,
        // 417 Client error: The expectations of the client as
        // expressed in one or more Expect header fields cannot be met
        // by the server, the server is awfully sorry about this.
        ClientErrorExpectationFailed          = 417,
        // 499 Client error: Wildcard error.
        ClientErrorJoker                      = 499,

        // 5xx Server errors (rare)
        // 500 Server error: something really strange and unexpected
        // happened
        ServerErrorInternalError              = 500,
        // 501 Server error: The server does not do the functionality
        // required to carry out the client request. not at
        // all. certainly not before breakfast. but also not after
        // breakfast.
        ServerErrorNotImplemented             = 501,
        // 502 Server error: While acting as a proxy or a gateway, the
        // server got ditched by the upstream server and as a
        // consequence regretfully cannot fulfill the client's request
        ServerErrorBadGateway                 = 502,
        // 503 Server error: Due to unforseen circumstances the server
        // cannot currently deliver the service requested. Retry-After
        // header might indicate when to try again.
        ServerErrorServiceUnavailable         = 503,
        // 504 Server error: The server blames the upstream server
        // for not being able to deliver the service requested and
        // claims that the upstream server is too slow delivering the
        // goods.
        ServerErrorGatewayTimeout             = 504,
        // 505 Server error: The server does not support the HTTP
        // version conveyed in the client's request.
        ServerErrorHttpVersionNotSupported    = 505,
    }
}
