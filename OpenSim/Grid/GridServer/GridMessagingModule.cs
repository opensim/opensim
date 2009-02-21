using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Nwc.XmlRpc;
using log4net;
using OpenSim.Framework.Servers;
using OpenSim.Framework;

namespace OpenSim.Grid.GridServer
{
    public class GridMessagingModule : IGridMessagingModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected GridDBService m_gridDBService;
        protected IGridCore m_gridCore;

        protected GridConfig m_config;

        /// <value>
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </value>
        private string m_opensimVersion;

        protected BaseHttpServer m_httpServer;

        // This is here so that the grid server can hand out MessageServer settings to regions on registration
        private List<MessageServerInfo> _MessageServers = new List<MessageServerInfo>();

        public List<MessageServerInfo> MessageServers
        {
            get { return _MessageServers; }
        }

        public GridMessagingModule(string opensimVersion, GridDBService gridDBService, IGridCore gridCore, GridConfig config)
        {
            m_opensimVersion = opensimVersion;
            m_gridDBService = gridDBService;
            m_gridCore = gridCore;
            m_config = config;
            m_httpServer = m_gridCore.GetHttpServer();
        }

        public void Initialise()
        {
            m_gridCore.RegisterInterface<IGridMessagingModule>(this);
            // Message Server ---> Grid Server
            m_httpServer.AddXmlRPCHandler("register_messageserver", XmlRPCRegisterMessageServer);
            m_httpServer.AddXmlRPCHandler("deregister_messageserver", XmlRPCDeRegisterMessageServer);

        }

        public XmlRpcResponse XmlRPCRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["URI"];
                string sendkey = (string)requestData["sendkey"];
                string recvkey = (string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                if (!_MessageServers.Contains(m))
                    _MessageServers.Add(m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }

        public XmlRpcResponse XmlRPCDeRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["uri"];
                string sendkey = (string)requestData["sendkey"];
                string recvkey = (string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                if (_MessageServers.Contains(m))
                    _MessageServers.Remove(m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }
    }
}
