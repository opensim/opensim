using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public class RestStreamHandler : BaseStreamHandler
    {
        RestMethod m_restMethod;

        override public byte[] Handle(string path, Stream request )
        {
            Encoding encoding = Encoding.UTF8;
            StreamReader streamReader = new StreamReader(request, encoding);

            string requestBody = streamReader.ReadToEnd();
            streamReader.Close();

            string param = GetParam(path);
            string responseString = m_restMethod(requestBody, path, param );

            return Encoding.UTF8.GetBytes(responseString);
        }

        public RestStreamHandler(string httpMethod, string path, RestMethod restMethod) : base( path, httpMethod )
        {
            m_restMethod = restMethod;
        }
    }
}
