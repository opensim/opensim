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
