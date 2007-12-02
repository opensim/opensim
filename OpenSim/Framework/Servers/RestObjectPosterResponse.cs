using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers
{
    public delegate void ReturnResponse<T>(T reponse);

    public class RestObjectPosterResponse<TResponse>
    {
        public ReturnResponse<TResponse> ResponseCallback;

        public void BeginPostObject<TRequest>(string requestUrl, TRequest obj)
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
            IAsyncResult result = request.BeginGetResponse(AsyncCallback, request);
        }

        private void AsyncCallback(IAsyncResult result)
        {
            WebRequest request = (WebRequest)result.AsyncState;
            using (WebResponse resp = request.EndGetResponse(result))
            {
                TResponse deserial;
                XmlSerializer deserializer = new XmlSerializer(typeof(TResponse));
                deserial = (TResponse)deserializer.Deserialize(resp.GetResponseStream());

                if (deserial != null && ResponseCallback != null)
                {
                    ResponseCallback(deserial);
                }
            }
        }
    }
}
