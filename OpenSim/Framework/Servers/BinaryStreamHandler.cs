using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{

    public class BinaryStreamHandler : BaseStreamHandler
    {
        BinaryMethod m_restMethod;

        override public byte[] Handle(string path, Stream request)
        {
            byte[] data = ReadFully(request);
            string param = GetParam(path);
            string responseString = m_restMethod(data, path, param);

            return Encoding.UTF8.GetBytes(responseString);
        }

        public BinaryStreamHandler(string httpMethod, string path, BinaryMethod restMethod)
            : base(httpMethod, path)
        {
            m_restMethod = restMethod;
        }

        public byte[] ReadFully(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }
    }

}
