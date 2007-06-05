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
    using System.Collections;
    using System.IO;
    using System.Xml;

    /// <summary>Class designed to represent an XML-RPC response.</summary>
    public class XmlRpcResponse
    {
        private Object _value;
        /// <summary><c>bool</c> indicating if this response represents a fault.</summary>
        public bool IsFault;

        /// <summary>Basic constructor</summary>
        public XmlRpcResponse()
        {
            Value = null;
            IsFault = false;
        }

        /// <summary>Constructor for a fault.</summary>
        /// <param name="code"><c>int</c> the numeric faultCode value.</param>
        /// <param name="message"><c>String</c> the faultString value.</param>
        public XmlRpcResponse(int code, String message)
            : this()
        {
            SetFault(code, message);
        }

        /// <summary>The data value of the response, may be fault data.</summary>
        public Object Value
        {
            get { return _value; }
            set
            {
                IsFault = false;
                _value = value;
            }
        }

        /// <summary>The faultCode if this is a fault.</summary>
        public int FaultCode
        {
            get
            {
                if (!IsFault)
                    return 0;
                else
                    return (int)((Hashtable)_value)[XmlRpcXmlTokens.FAULT_CODE];
            }
        }

        /// <summary>The faultString if this is a fault.</summary>
        public String FaultString
        {
            get
            {
                if (!IsFault)
                    return "";
                else
                    return (String)((Hashtable)_value)[XmlRpcXmlTokens.FAULT_STRING];
            }
        }

        /// <summary>Set this response to be a fault.</summary>
        /// <param name="code"><c>int</c> the numeric faultCode value.</param>
        /// <param name="message"><c>String</c> the faultString value.</param>
        public void SetFault(int code, String message)
        {
            Hashtable fault = new Hashtable();
            fault.Add("faultCode", code);
            fault.Add("faultString", message);
            Value = fault;
            IsFault = true;
        }

        /// <summary>Form a useful string representation of the object, in this case the XML response.</summary>
        /// <returns><c>String</c> The XML serialized XML-RPC response.</returns>
        override public String ToString()
        {
            return XmlRpcResponseSerializer.Singleton.Serialize(this);
        }
    }
}
