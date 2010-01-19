using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HomeUsersSecurityServerConnector : ServiceConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IHomeUsersSecurityService m_HomeUsersService;

        public HomeUsersSecurityServerConnector(IConfigSource config, IHttpServer server) :
                base(config, server, String.Empty)
        {
            IConfig gridConfig = config.Configs["HomeUsersSecurityService"];
            if (gridConfig != null)
            {
                string serviceDll = gridConfig.GetString("LocalServiceModule", string.Empty);
                Object[] args = new Object[] { config };
                m_HomeUsersService = ServerUtils.LoadPlugin<IHomeUsersSecurityService>(serviceDll, args);
            }
            if (m_HomeUsersService == null)
                throw new Exception("HomeUsersSecurity server connector cannot proceed because of missing service");

            server.AddXmlRPCHandler("ep_get", GetEndPoint, false);
            server.AddXmlRPCHandler("ep_set", SetEndPoint, false);
            server.AddXmlRPCHandler("ep_remove", RemoveEndPoint, false);

        }

        public XmlRpcResponse GetEndPoint(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);

            IPEndPoint ep = m_HomeUsersService.GetEndPoint(sessionID);

            Hashtable hash = new Hashtable();
            if (ep == null)
                hash["result"] = "false";
            else
            {
                hash["result"] = "true";
                hash["ep_addr"] = ep.Address.ToString();
                hash["ep_port"] = ep.Port.ToString();
            }
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse SetEndPoint(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            string host = (string)requestData["ep_addr"];
            string portstr = (string)requestData["ep_port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            int port = 0;
            Int32.TryParse(portstr, out port);

            IPEndPoint ep = null;
            try
            {
                ep = new IPEndPoint(IPAddress.Parse(host), port);
            }
            catch
            {
                m_log.Debug("[HOME USERS SECURITY]: Exception in creating EndPoint");
            }

            m_HomeUsersService.SetEndPoint(sessionID, ep);

            Hashtable hash = new Hashtable();
            hash["result"] = "true";
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse RemoveEndPoint(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);

            m_HomeUsersService.RemoveEndPoint(sessionID);

            Hashtable hash = new Hashtable();
            hash["result"] = "true";
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

    }
}
