using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers
{
    public delegate TResponse RestDeserialiseMethod<TRequest, TResponse>(TRequest request);

    public class RestDeserialisehandler<TRequest, TResponse> : BaseRequestHandler, IStreamHandler
        where TRequest : new()
    {
        private RestDeserialiseMethod<TRequest, TResponse> m_method;

        public RestDeserialisehandler(string httpMethod, string path, RestDeserialiseMethod<TRequest, TResponse> method)
            : base(httpMethod, path)
        {
            m_method = method;
        }

        public void Handle(string path, Stream request, Stream responseStream )
        {
            TRequest deserial;
            using (XmlTextReader xmlReader = new XmlTextReader(request))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(TRequest));
                deserial = (TRequest)deserializer.Deserialize(xmlReader);
            }

            TResponse response = m_method(deserial);

            using (XmlWriter xmlWriter = XmlTextWriter.Create( responseStream ))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TResponse));
                serializer.Serialize(xmlWriter, response );
            }
        }
    }
}
