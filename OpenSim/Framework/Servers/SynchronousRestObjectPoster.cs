using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers
{
    public class SynchronousRestObjectPoster
    {
        public static TResponse BeginPostObject<TRequest, TResponse>(string requestUrl, TRequest obj)
        {
            Type type = typeof(TRequest);

            WebRequest request = WebRequest.Create(requestUrl);
            request.Method = "POST";
            request.ContentType = "text/xml";

            MemoryStream buffer = new MemoryStream();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                serializer.Serialize(writer, obj);
                writer.Flush();
            }

            int length = (int)buffer.Length;
            request.ContentLength = length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(buffer.ToArray(), 0, length);
            TResponse deserial = default(TResponse);
            using (WebResponse resp = request.GetResponse())
            {

                XmlSerializer deserializer = new XmlSerializer(typeof(TResponse));
                deserial = (TResponse)deserializer.Deserialize(resp.GetResponseStream());
            }
            return deserial;
        }

    }
}
