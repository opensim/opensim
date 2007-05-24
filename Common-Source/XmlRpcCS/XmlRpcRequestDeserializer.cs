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
