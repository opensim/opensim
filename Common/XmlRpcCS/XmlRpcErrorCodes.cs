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
