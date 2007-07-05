/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
namespace Nwc.XmlRpc
{
    using System;
    using System.Xml;
    using System.Net.Sockets;
    using System.Text;

    /// <summary>The class is a container of the context of an XML-RPC dialog on the server side.</summary>
    /// <remarks>Instances of this class maintain the context for an individual XML-RPC server
    /// side dialog. Namely they manage an inbound deserializer and an outbound serializer. </remarks>
    public class XmlRpcResponder
    {
        private XmlRpcRequestDeserializer _deserializer = new XmlRpcRequestDeserializer();
        private XmlRpcResponseSerializer _serializer = new XmlRpcResponseSerializer();
        private XmlRpcServer _server;
        private TcpClient _client;
        private SimpleHttpRequest _httpReq;

        /// <summary>The SimpleHttpRequest based on the TcpClient.</summary>
        public SimpleHttpRequest HttpReq
        {
            get { return _httpReq; }
        }

        /// <summary>Basic constructor.</summary>
        /// <param name="server">XmlRpcServer that this XmlRpcResponder services.</param>
        /// <param name="client">TcpClient with the connection.</param>
        public XmlRpcResponder(XmlRpcServer server, TcpClient client)
        {
            _server = server;
            _client = client;
            _httpReq = new SimpleHttpRequest(_client);
        }

        /// <summary>Call close to insure proper shutdown.</summary>
        ~XmlRpcResponder()
        {
            Close();
        }

        ///<summary>Respond using this responders HttpReq.</summary>
        public void Respond()
        {
            Respond(HttpReq);
        }

        /// <summary>Handle an HTTP request containing an XML-RPC request.</summary>
        /// <remarks>This method deserializes the XML-RPC request, invokes the 
        /// described method, serializes the response (or fault) and sends the XML-RPC response
        /// back as a valid HTTP page.
        /// </remarks>
        /// <param name="httpReq"><c>SimpleHttpRequest</c> containing the request.</param>
        public void Respond(SimpleHttpRequest httpReq)
        {
            XmlRpcRequest xmlRpcReq = (XmlRpcRequest)_deserializer.Deserialize(httpReq.Input);
            XmlRpcResponse xmlRpcResp = new XmlRpcResponse();

            try
            {
                xmlRpcResp.Value = _server.Invoke(xmlRpcReq);
            }
            catch (XmlRpcException e)
            {
                xmlRpcResp.SetFault(e.FaultCode, e.FaultString);
            }
            catch (Exception e2)
            {
                xmlRpcResp.SetFault(XmlRpcErrorCodes.APPLICATION_ERROR,
                      XmlRpcErrorCodes.APPLICATION_ERROR_MSG + ": " + e2.Message);
            }

            if (Logger.Delegate != null)
                Logger.WriteEntry(xmlRpcResp.ToString(), LogLevel.Information);

            XmlRpcServer.HttpHeader(httpReq.Protocol, "text/xml", 0, " 200 OK", httpReq.Output);            
            httpReq.Output.Flush();
            
            XmlTextWriter xml = new XmlTextWriter(httpReq.Output);
            _serializer.Serialize(xml, xmlRpcResp);
            xml.Flush();
            httpReq.Output.Flush();
        }

        ///<summary>Close all contained resources, both the HttpReq and client.</summary>
        public void Close()
        {
            if (_httpReq != null)
            {
                _httpReq.Close();
                _httpReq = null;
            }

            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
        }
    }
}
