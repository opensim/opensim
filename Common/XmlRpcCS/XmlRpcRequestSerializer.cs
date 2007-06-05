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
    using System.IO;

    /// <summary>Class responsible for serializing an XML-RPC request.</summary>
    /// <remarks>This class handles the request envelope, depending on <c>XmlRpcSerializer</c>
    /// to serialize the payload.</remarks>
    /// <seealso cref="XmlRpcSerializer"/>
    public class XmlRpcRequestSerializer : XmlRpcSerializer
    {
        static private XmlRpcRequestSerializer _singleton;
        /// <summary>A static singleton instance of this deserializer.</summary>
        static public XmlRpcRequestSerializer Singleton
        {
            get
            {
                if (_singleton == null)
                    _singleton = new XmlRpcRequestSerializer();

                return _singleton;
            }
        }

        /// <summary>Serialize the <c>XmlRpcRequest</c> to the output stream.</summary>
        /// <param name="output">An <c>XmlTextWriter</c> stream to write data to.</param>
        /// <param name="obj">An <c>XmlRpcRequest</c> to serialize.</param>
        /// <seealso cref="XmlRpcRequest"/>
        override public void Serialize(XmlTextWriter output, Object obj)
        {
            XmlRpcRequest request = (XmlRpcRequest)obj;
            output.WriteStartDocument();
            output.WriteStartElement(METHOD_CALL);
            output.WriteElementString(METHOD_NAME, request.MethodName);
            output.WriteStartElement(PARAMS);
            foreach (Object param in request.Params)
            {
                output.WriteStartElement(PARAM);
                output.WriteStartElement(VALUE);
                SerializeObject(output, param);
                output.WriteEndElement();
                output.WriteEndElement();
            }

            output.WriteEndElement();
            output.WriteEndElement();
        }
    }
}
