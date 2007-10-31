using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers
{
    public delegate string RestDeserialiseMethod<TRequest>(TRequest request);

    public class RestDeserialisehandler<TRequest> : BaseStreamHandler
        where TRequest : new()
    {
        private RestDeserialiseMethod<TRequest> m_method;

        public RestDeserialisehandler(string httpMethod, string path, RestDeserialiseMethod<TRequest> method)
            : base(httpMethod, path)
        {
            m_method = method;
        }

        public override byte[] Handle(string path, Stream request)
        {
            Type type = typeof(TRequest);

            TRequest deserial= default(TRequest);
            using (XmlTextReader xreader = new XmlTextReader(request))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                deserial = (TRequest)serializer.Deserialize(xreader);
            }

            string response = m_method(deserial);

            Encoding encoding = new UTF8Encoding(false);
            return encoding.GetBytes(response);

        }
    }
}
