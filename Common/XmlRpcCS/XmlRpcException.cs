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
