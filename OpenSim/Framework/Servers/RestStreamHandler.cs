using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public class RestStreamHandler : IStreamHandler
    {
        RestMethod m_restMethod;

        private string m_contentType;
        public string ContentType
        {
            get { return m_contentType; }
        }

        private string m_httpMethod;
        public string HttpMethod
        {
            get { return m_httpMethod; }
        }


        public byte[] Handle(string path, Stream request )
        {
            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(request, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();

            string responseString = m_restMethod(requestBody, path, m_httpMethod);

            return Encoding.UTF8.GetBytes(responseString);
        }

        public RestStreamHandler(RestMethod restMethod, string httpMethod, string contentType)
        {
            m_restMethod = restMethod;
            m_httpMethod = httpMethod;
            m_contentType = contentType;
        }
    }
}
