/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
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
            request.Timeout = 20000;

            using (MemoryStream buffer = new MemoryStream())
            {
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

                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(buffer.ToArray(), 0, length);
            }

            TResponse deserial = default(TResponse);
            using (WebResponse resp = request.GetResponse())
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(TResponse));

                using (Stream respStream = resp.GetResponseStream())
                    deserial = (TResponse)deserializer.Deserialize(respStream);
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
            request.Timeout = 10000;

            using (MemoryStream buffer = new MemoryStream())
            {
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

                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(buffer.ToArray(), 0, length);
            }

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
                if (stream != null)
                    stream.Close();

                if (deserial != null && ResponseCallback != null)
                {
                    ResponseCallback(deserial);
                }
            }
        }
    }

    public delegate bool CheckIdentityMethod(string sid, string aid);

    public class RestDeserialiseSecureHandler<TRequest, TResponse> : BaseOutputStreamHandler, IStreamHandler
        where TRequest : new()
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RestDeserialiseMethod<TRequest, TResponse> m_method;
        private CheckIdentityMethod m_smethod;

        public RestDeserialiseSecureHandler(
             string httpMethod, string path,
             RestDeserialiseMethod<TRequest, TResponse> method, CheckIdentityMethod smethod)
            : base(httpMethod, path)
        {
            m_smethod = smethod;
            m_method = method;
        }

        protected override void ProcessRequest(string path, Stream request, Stream responseStream,
                           IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            RestSessionObject<TRequest> deserial = default(RestSessionObject<TRequest>);
            bool fail = false;

            using (XmlTextReader xmlReader = new XmlTextReader(request))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(RestSessionObject<TRequest>));
                    deserial = (RestSessionObject<TRequest>)deserializer.Deserialize(xmlReader);
                }
                catch (Exception e)
                {
                    m_log.Error("[REST]: Deserialization problem. Ignoring request. " + e);
                    fail = true;
                }
            }

            TResponse response = default(TResponse);
            if (!fail && m_smethod(deserial.SessionID, deserial.AvatarID))
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

    public class RestDeserialiseTrustedHandler<TRequest, TResponse> : BaseOutputStreamHandler, IStreamHandler
        where TRequest : new()
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The operation to perform once trust has been established.
        /// </summary>
        private RestDeserialiseMethod<TRequest, TResponse> m_method;

        /// <summary>
        /// The method used to check whether a request is trusted.
        /// </summary>
        private CheckTrustedSourceMethod m_tmethod;

        public RestDeserialiseTrustedHandler(string httpMethod, string path, RestDeserialiseMethod<TRequest, TResponse> method, CheckTrustedSourceMethod tmethod)
            : base(httpMethod, path)
        {
            m_tmethod = tmethod;
            m_method = method;
        }

        protected override void ProcessRequest(string path, Stream request, Stream responseStream,
                           IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            TRequest deserial = default(TRequest);
            bool fail = false;

            using (XmlTextReader xmlReader = new XmlTextReader(request))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(TRequest));
                    deserial = (TRequest)deserializer.Deserialize(xmlReader);
                }
                catch (Exception e)
                {
                    m_log.Error("[REST]: Deserialization problem. Ignoring request. " + e);
                    fail = true;
                }
            }

            TResponse response = default(TResponse);
            if (!fail && m_tmethod(httpRequest.RemoteIPEndPoint))
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