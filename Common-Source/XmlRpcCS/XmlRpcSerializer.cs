namespace Nwc.XmlRpc
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Xml;

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
            StringWriter strBuf = new StringWriter();
            XmlTextWriter xml = new XmlTextWriter(strBuf);
            xml.Formatting = Formatting.Indented;
            xml.Indentation = 4;
            Serialize(xml, obj);
            xml.Flush();
            String returns = strBuf.ToString();
            xml.Close();
            return returns;
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
