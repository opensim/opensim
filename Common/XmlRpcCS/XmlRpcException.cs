namespace Nwc.XmlRpc
{
    using System;

    /// <summary>An XML-RPC Exception.</summary>
    /// <remarks>Maps a C# exception to an XML-RPC fault. Normal exceptions
    /// include a message so this adds the code needed by XML-RPC.</remarks>
    public class XmlRpcException : Exception
    {
        private int _code;

        /// <summary>Instantiate an <c>XmlRpcException</c> with a code and message.</summary>
        /// <param name="code"><c>Int</c> faultCode associated with this exception.</param>
        /// <param name="message"><c>String</c> faultMessage associated with this exception.</param>
        public XmlRpcException(int code, String message)
            : base(message)
        {
            _code = code;
        }

        /// <summary>The value of the faults message, i.e. the faultString.</summary>
        public String FaultString
        {
            get { return Message; }
        }

        /// <summary>The value of the faults code, i.e. the faultCode.</summary>
        public int FaultCode
        {
            get { return _code; }
        }

        /// <summary>Format the message to include the code.</summary>
        override public String ToString()
        {
            return "Code: " + FaultCode + " Message: " + base.ToString();
        }
    }
}
