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
    using System.Xml;

    /// <summary>Class responsible for serializing an XML-RPC response.</summary>
    /// <remarks>This class handles the response envelope, depending on XmlRpcSerializer
    /// to serialize the payload.</remarks>
    /// <seealso cref="XmlRpcSerializer"/>
    public class XmlRpcResponseSerializer : XmlRpcSerializer
    {
        static private XmlRpcResponseSerializer _singleton;
        /// <summary>A static singleton instance of this deserializer.</summary>
        static public XmlRpcResponseSerializer Singleton
        {
            get
            {
                if (_singleton == null)
                    _singleton = new XmlRpcResponseSerializer();

                return _singleton;
            }
        }

        /// <summary>Serialize the <c>XmlRpcResponse</c> to the output stream.</summary>
        /// <param name="output">An <c>XmlTextWriter</c> stream to write data to.</param>
        /// <param name="obj">An <c>Object</c> to serialize.</param>
        /// <seealso cref="XmlRpcResponse"/>
        override public void Serialize(XmlTextWriter output, Object obj)
        {
            XmlRpcResponse response = (XmlRpcResponse)obj;

            output.WriteStartDocument();
            output.WriteStartElement(METHOD_RESPONSE);

            if (response.IsFault)
                output.WriteStartElement(FAULT);
            else
            {
                output.WriteStartElement(PARAMS);
                output.WriteStartElement(PARAM);
            }

            output.WriteStartElement(VALUE);

            SerializeObject(output, response.Value);

            output.WriteEndElement();

            output.WriteEndElement();
            if (!response.IsFault)
                output.WriteEndElement();
            output.WriteEndElement();
        }
    }
}
