using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public delegate string BinaryMethod(byte[] data, string path, string param);

    public class BinaryStreamHandler : BaseStreamHandler
    {
        BinaryMethod m_method;

        override public byte[] Handle(string path, Stream request)
        {
            byte[] data = ReadFully(request);
            string param = GetParam(path);
            string responseString = m_method(data, path, param);
            
            return Encoding.UTF8.GetBytes(responseString);
        }

        public BinaryStreamHandler(string httpMethod, string path, BinaryMethod binaryMethod)
            : base(httpMethod, path)
        {
            m_method = binaryMethod;
        }

        private byte[] ReadFully(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);

                    if (read <= 0)
                    {
                        return ms.ToArray();
                    }
                    
                    ms.Write(buffer, 0, read);
                }
            }           
        }
    }

}
