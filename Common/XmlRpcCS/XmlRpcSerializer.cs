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
    using System.Text;

    /// <summary>Base class of classes serializing data to XML-RPC's XML format.</summary>
    /// <remarks>This class handles the basic type conversions like Integer to &lt;i4&gt;. </remarks>
    /// <seealso cref="XmlRpcXmlTokens"/>
    public class XmlRpcSerializer : XmlRpcXmlTokens
    {

        /// <summary>Serialize the <c>XmlRpcRequest</c> to the output stream.</summary>
        /// <param name="output">An <c>XmlTextWriter</c> stream to write data to.</param>
        /// <param name="obj">An <c>Object</c> to serialize.</param>
        /// <seealso cref="XmlRpcRequest"/>
        virtual public void Serialize(XmlTextWriter output, Object obj)
        {
        }

        /// <summary>Serialize the <c>XmlRpcRequest</c> to a String.</summary>
        /// <remarks>Note this may represent a real memory hog for a large request.</remarks>
        /// <param name="obj">An <c>Object</c> to serialize.</param>
        /// <returns><c>String</c> containing XML-RPC representation of the request.</returns>
        /// <seealso cref="XmlRpcRequest"/>
        public String Serialize(Object obj)
        {
            using (MemoryStream memStream = new MemoryStream(4096))
            {
                XmlTextWriter xml = new XmlTextWriter( memStream, null );
                xml.Formatting = Formatting.Indented;
                xml.Indentation = 4;                
                Serialize(xml, obj);
                xml.Flush();

                byte[] resultBytes = memStream.ToArray();
                
                UTF8Encoding encoder = new UTF8Encoding();
                String returns = encoder.GetString( resultBytes, 0, resultBytes.Length );
                xml.Close();
                return returns;
            }
        }

        /// <remarks>Serialize the object to the output stream.</remarks>
        /// <param name="output">An <c>XmlTextWriter</c> stream to write data to.</param>
        /// <param name="obj">An <c>Object</c> to serialize.</param>
        public void SerializeObject(XmlTextWriter output, Object obj)
        {
            if (obj == null)
                return;

            if (obj is byte[])
            {
                byte[] ba = (byte[])obj;
                output.WriteStartElement(BASE64);
                output.WriteBase64(ba, 0, ba.Length);
                output.WriteEndElement();
            }
            else if (obj is String)
            {
                output.WriteElementString(STRING, obj.ToString());
            }
            else if (obj is Int32)
            {
                output.WriteElementString(INT, obj.ToString());
            }
            else if (obj is DateTime)
            {
                output.WriteElementString(DATETIME, ((DateTime)obj).ToString(ISO_DATETIME));
            }
            else if (obj is Double)
            {
                output.WriteElementString(DOUBLE, obj.ToString());
            }
            else if (obj is Boolean)
            {
                output.WriteElementString(BOOLEAN, ((((Boolean)obj) == true) ? "1" : "0"));
            }
            else if (obj is IList)
            {
                output.WriteStartElement(ARRAY);
                output.WriteStartElement(DATA);
                if (((ArrayList)obj).Count > 0)
                {
                    foreach (Object member in ((IList)obj))
                    {
                        output.WriteStartElement(VALUE);
                        SerializeObject(output, member);
                        output.WriteEndElement();
                    }
                }
                output.WriteEndElement();
                output.WriteEndElement();
            }
            else if (obj is IDictionary)
            {
                IDictionary h = (IDictionary)obj;
                output.WriteStartElement(STRUCT);
                foreach (String key in h.Keys)
                {
                    output.WriteStartElement(MEMBER);
                    output.WriteElementString(NAME, key);
                    output.WriteStartElement(VALUE);
                    SerializeObject(output, h[key]);
                    output.WriteEndElement();
                    output.WriteEndElement();
                }
                output.WriteEndElement();
            }

        }
    }
}
