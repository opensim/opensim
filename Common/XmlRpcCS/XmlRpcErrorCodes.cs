/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
namespace Nwc.XmlRpc
{
    using System;

    /// <summary>Standard XML-RPC error codes.</summary>
    public class XmlRpcErrorCodes
    {
        /// <summary></summary>
        public const int PARSE_ERROR_MALFORMED = -32700;
        /// <summary></summary>
        public const String PARSE_ERROR_MALFORMED_MSG = "Parse Error, not well formed";

        /// <summary></summary>
        public const int PARSE_ERROR_ENCODING = -32701;
        /// <summary></summary>
        public const String PARSE_ERROR_ENCODING_MSG = "Parse Error, unsupported encoding";

        //
        // -32702 ---> parse error. invalid character for encoding
        // -32600 ---> server error. invalid xml-rpc. not conforming to spec.
        //

        /// <summary></summary>
        public const int SERVER_ERROR_METHOD = -32601;
        /// <summary></summary>
        public const String SERVER_ERROR_METHOD_MSG = "Server Error, requested method not found";

        /// <summary></summary>
        public const int SERVER_ERROR_PARAMS = -32602;
        /// <summary></summary>
        public const String SERVER_ERROR_PARAMS_MSG = "Server Error, invalid method parameters";

        //
        // -32603 ---> server error. internal xml-rpc error
        //

        /// <summary></summary>
        public const int APPLICATION_ERROR = -32500;
        /// <summary></summary>
        public const String APPLICATION_ERROR_MSG = "Application Error";

        //
        // -32400 ---> system error
        //

        /// <summary></summary>
        public const int TRANSPORT_ERROR = -32300;
        /// <summary></summary>
        public const String TRANSPORT_ERROR_MSG = "Transport Layer Error";
    }
}
