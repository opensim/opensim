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
