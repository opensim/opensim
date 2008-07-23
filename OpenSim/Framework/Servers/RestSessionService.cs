using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers
{
    public class RestSessionObject<TRequest>
    {
        private string sid;
        private string aid;
        private TRequest request_body;

        public string SessionID
        {
            get { return sid; }
            set { sid = value; }
        }

        public string AvatarID
        {
            get { return aid; }
            set { aid = value; }
        }

        public TRequest Body
        {
            get { return request_body; }
            set { request_body = value; }
        }
    }

    public class SynchronousRestSessionObjectPoster<TRequest, TResponse>
    {
        public static TResponse BeginPostObject(string verb, string requestUrl, TRequest obj, string sid, string aid)
        {
            RestSessionObject<TRequest> sobj = new RestSessionObject<TRequest>();
            sobj.SessionID = sid;
            sobj.AvatarID = aid;
            sobj.Body = obj;

            Type type = typeof(RestSessionObject<TRequest>);

            WebRequest request = WebRequest.Create(requestUrl);
            request.Method = verb;
            request.ContentType = "text/xml";

            MemoryStream buffer = new MemoryStream();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                serializer.Serialize(writer, sobj);
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

    public class RestSessionObjectPosterResponse<TRequest, TResponse>
    {

        public ReturnResponse<TResponse> ResponseCallback;

        public void BeginPostObject(string requestUrl, TRequest obj, string sid, string aid)
        {
            BeginPostObject("POST", requestUrl, obj, sid, aid);
        }

        public void BeginPostObject(string verb, string requestUrl, TRequest obj, string sid, string aid)
        {
            RestSessionObject<TRequest> sobj = new RestSessionObject<TRequest>();
            sobj.SessionID = sid;
            sobj.AvatarID = aid;
            sobj.Body = obj;

            Type type = typeof(RestSessionObject<TRequest>);

            WebRequest request = WebRequest.Create(requestUrl);
            request.Method = verb;
            request.ContentType = "text/xml";

            MemoryStream buffer = new MemoryStream();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                serializer.Serialize(writer, sobj);
                writer.Flush();
            }

            int length = (int)buffer.Length;
            request.ContentLength = length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(buffer.ToArray(), 0, length);
            // IAsyncResult result = request.BeginGetResponse(AsyncCallback, request);
            request.BeginGetResponse(AsyncCallback, request);
        }

        private void AsyncCallback(IAsyncResult result)
        {
            WebRequest request = (WebRequest)result.AsyncState;
            using (WebResponse resp = request.EndGetResponse(result))
            {
                TResponse deserial;
                XmlSerializer deserializer = new XmlSerializer(typeof(TResponse));
                Stream stream = resp.GetResponseStream();

                // This is currently a bad debug stanza since it gobbles us the response...
                //                StreamReader reader = new StreamReader(stream);
                //                m_log.DebugFormat("[REST OBJECT POSTER RESPONSE]: Received {0}", reader.ReadToEnd());

                deserial = (TResponse)deserializer.Deserialize(stream);

                if (deserial != null && ResponseCallback != null)
                {
                    ResponseCallback(deserial);
                }
            }
        }
    }

    public delegate bool CheckIdentityMethod(string sid, string aid);

    public class RestDeserialiseSecureHandler<TRequest, TResponse> : BaseRequestHandler, IStreamHandler
        where TRequest : new()
    {
        private RestDeserialiseMethod<TRequest, TResponse> m_method;
        private CheckIdentityMethod m_smethod;

        public RestDeserialiseSecureHandler(string httpMethod, string path, RestDeserialiseMethod<TRequest, TResponse> method, CheckIdentityMethod smethod)
            : base(httpMethod, path)
        {
            m_smethod = smethod;
            m_method = method;
        }

        public void Handle(string path, Stream request, Stream responseStream,
                           OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            RestSessionObject<TRequest> deserial;
            using (XmlTextReader xmlReader = new XmlTextReader(request))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(RestSessionObject<TRequest>));
                deserial = (RestSessionObject<TRequest>)deserializer.Deserialize(xmlReader);
            }

            TResponse response = default(TResponse);
            if (m_smethod(deserial.SessionID, deserial.AvatarID))
            {
                response = m_method(deserial.Body);
            }

            using (XmlWriter xmlWriter = XmlTextWriter.Create(responseStream))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TResponse));
                serializer.Serialize(xmlWriter, response);
            }
        }
    }

    public delegate bool CheckTrustedSourceMethod(IPEndPoint peer);

    public class RestDeserialiseTrustedHandler<TRequest, TResponse> : BaseRequestHandler, IStreamHandler
        where TRequest : new()
    {
        private RestDeserialiseMethod<TRequest, TResponse> m_method;
        private CheckTrustedSourceMethod m_tmethod;

        public RestDeserialiseTrustedHandler(string httpMethod, string path, RestDeserialiseMethod<TRequest, TResponse> method, CheckTrustedSourceMethod tmethod)
            : base(httpMethod, path)
        {
            m_tmethod = tmethod;
            m_method = method;
        }

        public void Handle(string path, Stream request, Stream responseStream,
                           OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            TRequest deserial;
            using (XmlTextReader xmlReader = new XmlTextReader(request))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(TRequest));
                deserial = (TRequest)deserializer.Deserialize(xmlReader);
            }

            TResponse response = default(TResponse);
            if (m_tmethod(httpRequest.RemoteIPEndPoint))
            {
                response = m_method(deserial);
            }

            using (XmlWriter xmlWriter = XmlTextWriter.Create(responseStream))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TResponse));
                serializer.Serialize(xmlWriter, response);
            }
        }
    }

}
