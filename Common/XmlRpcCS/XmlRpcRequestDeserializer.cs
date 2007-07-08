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
    using System.Diagnostics;
    using System.IO;
    using System.Xml;

    /// <summary>Class to deserialize XML data representing a request.</summary>
    public class XmlRpcRequestDeserializer : XmlRpcDeserializer
    {
        static private XmlRpcRequestDeserializer _singleton;
        /// <summary>A static singleton instance of this deserializer.</summary>
        [Obsolete("This object is now thread safe, just use an instance.", false)]
        static public XmlRpcRequestDeserializer Singleton
        {
            get
            {
                if (_singleton == null)
                    _singleton = new XmlRpcRequestDeserializer();

                return _singleton;
            }
        }

        /// <summary>Static method that parses XML data into a request using the Singleton.</summary>
        /// <param name="xmlData"><c>StreamReader</c> containing an XML-RPC request.</param>
        /// <returns><c>XmlRpcRequest</c> object resulting from the parse.</returns>
        override public Object Deserialize(TextReader xmlData)
        {
            XmlTextReader reader = new XmlTextReader(xmlData);
            
            XmlRpcRequest request = new XmlRpcRequest();
            bool done = false;

            lock (this)
            {
                Reset();
                while (!done && reader.Read())
                {
                    DeserializeNode(reader); // Parent parse...
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.EndElement:
                            switch (reader.Name)
                            {
                                case METHOD_NAME:
                                    request.MethodName = _text;
                                    break;
                                case METHOD_CALL:
                                    done = true;
                                    break;
                                case PARAM:
                                    request.Params.Add(_value);
                                    _text = null;
                                    break;
                            }
                            break;
                    }
                }
            }
            return request;
        }
    }
}
