/* 
 * Copyright (c) Contributors, http://www.nsl.tuis.ac.jp
 *
 */


using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Net;
using System.Text;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using log4net;
using Nwc.XmlRpc;



namespace NSL.Network.XmlRpc
{
    public class NSLXmlRpcRequest : XmlRpcRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Encoding _encoding = new UTF8Encoding();
        private XmlRpcRequestSerializer _serializer = new XmlRpcRequestSerializer();
        private XmlRpcResponseDeserializer _deserializer = new XmlRpcResponseDeserializer();


        public NSLXmlRpcRequest()
        {
            _params = new ArrayList();
        }


        public NSLXmlRpcRequest(String methodName, IList parameters)
        {
            MethodName = methodName;
            _params = parameters;
        }


        public XmlRpcResponse certSend(String url, X509Certificate2 myClientCert, bool checkServerCert, Int32 timeout)
        {
            m_log.InfoFormat("[MONEY NSL RPC]: XmlRpcResponse certSend: connect to {0}", url);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (request == null)
            {
                throw new XmlRpcException(XmlRpcErrorCodes.TRANSPORT_ERROR, XmlRpcErrorCodes.TRANSPORT_ERROR_MSG + ": Could not create request with " + url);
            }

            request.Method = "POST";
            request.ContentType = "text/xml";
            request.AllowWriteStreamBuffering = true;
            request.Timeout = timeout;
            request.UserAgent = "NSLXmlRpcRequest";

            if (myClientCert != null)
            {
                request.ClientCertificates.Add(myClientCert);   // Own certificate
                m_log.ErrorFormat("[MONEY NSL RPC]: 111111111111111111111111111");
            }
            if (!checkServerCert) request.Headers.Add("NoVerifyCert", "true");    // Do not verify the certificate of the other party

            Stream stream = null;
            try
            {
                stream = request.GetRequestStream();
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[MONEY NSL RPC]: GetRequestStream Error: {0}", ex);
                stream = null;
            }
            if (stream == null) return null;

            //
            XmlTextWriter xml = new XmlTextWriter(stream, _encoding);
            _serializer.Serialize(xml, this);
            xml.Flush();
            xml.Close();

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[MONEY NSL RPC]: XmlRpcResponse certSend: GetResponse Error: {0}", ex.ToString());
            }
            StreamReader input = new StreamReader(response.GetResponseStream());

            string inputXml = input.ReadToEnd();
            XmlRpcResponse resp = (XmlRpcResponse)_deserializer.Deserialize(inputXml);

            input.Close();
            response.Close();
            return resp;
        }
    }
}
