namespace Nwc.XmlRpc
{
    using System;

    /// <summary>Class collecting <c>String</c> tokens that are part of XML-RPC files.</summary>
    public class XmlRpcXmlTokens
    {
        /// <summary>C# formatting string to describe an ISO 8601 date.</summary>
        public const String ISO_DATETIME = "yyyyMMdd\\THH\\:mm\\:ss";
        /// <summary>Base64 field indicator.</summary>
        /// <remarks>Corresponds to the &lt;base64&gt; tag.</remarks>
        public const String BASE64 = "base64";
        /// <summary>String field indicator.</summary>
        /// <remarks>Corresponds to the &lt;string&gt; tag.</remarks>
        public const String STRING = "string";
        /// <summary>Integer field integer.</summary>
        /// <remarks>Corresponds to the &lt;i4&gt; tag.</remarks>
        public const String INT = "i4";
        /// <summary>Alternate integer field indicator.</summary>
        /// <remarks>Corresponds to the &lt;int&gt; tag.</remarks>
        public const String ALT_INT = "int";
        /// <summary>Date field indicator.</summary>
        /// <remarks>Corresponds to the &lt;dateTime.iso8601&gt; tag.</remarks>
        public const String DATETIME = "dateTime.iso8601";
        /// <summary>Boolean field indicator.</summary>
        /// <remarks>Corresponds to the &lt;boolean&gt; tag.</remarks>
        public const String BOOLEAN = "boolean";
        /// <summary>Value token.</summary>
        /// <remarks>Corresponds to the &lt;value&gt; tag.</remarks>
        public const String VALUE = "value";
        /// <summary>Name token.</summary>
        /// <remarks>Corresponds to the &lt;name&gt; tag.</remarks>
        public const String NAME = "name";
        /// <summary>Array field indicator..</summary>
        /// <remarks>Corresponds to the &lt;array&gt; tag.</remarks>
        public const String ARRAY = "array";
        /// <summary>Data token.</summary>
        /// <remarks>Corresponds to the &lt;data&gt; tag.</remarks>
        public const String DATA = "data";
        /// <summary>Member token.</summary>
        /// <remarks>Corresponds to the &lt;member&gt; tag.</remarks>
        public const String MEMBER = "member";
        /// <summary>Stuct field indicator.</summary>
        /// <remarks>Corresponds to the &lt;struct&gt; tag.</remarks>
        public const String STRUCT = "struct";
        /// <summary>Double field indicator.</summary>
        /// <remarks>Corresponds to the &lt;double&gt; tag.</remarks>
        public const String DOUBLE = "double";
        /// <summary>Param token.</summary>
        /// <remarks>Corresponds to the &lt;param&gt; tag.</remarks>
        public const String PARAM = "param";
        /// <summary>Params token.</summary>
        /// <remarks>Corresponds to the &lt;params&gt; tag.</remarks>
        public const String PARAMS = "params";
        /// <summary>MethodCall token.</summary>
        /// <remarks>Corresponds to the &lt;methodCall&gt; tag.</remarks>
        public const String METHOD_CALL = "methodCall";
        /// <summary>MethodName token.</summary>
        /// <remarks>Corresponds to the &lt;methodName&gt; tag.</remarks>
        public const String METHOD_NAME = "methodName";
        /// <summary>MethodResponse token</summary>
        /// <remarks>Corresponds to the &lt;methodResponse&gt; tag.</remarks>
        public const String METHOD_RESPONSE = "methodResponse";
        /// <summary>Fault response token.</summary>
        /// <remarks>Corresponds to the &lt;fault&gt; tag.</remarks>
        public const String FAULT = "fault";
        /// <summary>FaultCode token.</summary>
        /// <remarks>Corresponds to the &lt;faultCode&gt; tag.</remarks>
        public const String FAULT_CODE = "faultCode";
        /// <summary>FaultString token.</summary>
        /// <remarks>Corresponds to the &lt;faultString&gt; tag.</remarks>
        public const String FAULT_STRING = "faultString";
    }
}


