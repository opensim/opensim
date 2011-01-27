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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class UserAgentServerConnector : ServiceConnector
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAgentService m_HomeUsersService;

        public UserAgentServerConnector(IConfigSource config, IHttpServer server) :
                base(config, server, String.Empty)
        {
            IConfig gridConfig = config.Configs["UserAgentService"];
            if (gridConfig != null)
            {
                string serviceDll = gridConfig.GetString("LocalServiceModule", string.Empty);
                Object[] args = new Object[] { config };
                m_HomeUsersService = ServerUtils.LoadPlugin<IUserAgentService>(serviceDll, args);
            }
            if (m_HomeUsersService == null)
                throw new Exception("UserAgent server connector cannot proceed because of missing service");

            string loginServerIP = gridConfig.GetString("LoginServerIP", "127.0.0.1");
            bool proxy = gridConfig.GetBoolean("HasProxy", false);

            server.AddXmlRPCHandler("agent_is_coming_home", AgentIsComingHome, false);
            server.AddXmlRPCHandler("get_home_region", GetHomeRegion, false);
            server.AddXmlRPCHandler("verify_agent", VerifyAgent, false);
            server.AddXmlRPCHandler("verify_client", VerifyClient, false);
            server.AddXmlRPCHandler("logout_agent", LogoutAgent, false);

            server.AddHTTPHandler("/homeagent/", new HomeAgentHandler(m_HomeUsersService, loginServerIP, proxy).Handler);
        }

        public XmlRpcResponse GetHomeRegion(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string userID_str = (string)requestData["userID"];
            UUID userID = UUID.Zero;
            UUID.TryParse(userID_str, out userID);

            Vector3 position = Vector3.UnitY, lookAt = Vector3.UnitY;
            GridRegion regInfo = m_HomeUsersService.GetHomeRegion(userID, out position, out lookAt);

            Hashtable hash = new Hashtable();
            if (regInfo == null)
                hash["result"] = "false";
            else
            {
                hash["result"] = "true";
                hash["uuid"] = regInfo.RegionID.ToString();
                hash["x"] = regInfo.RegionLocX.ToString();
                hash["y"] = regInfo.RegionLocY.ToString();
                hash["region_name"] = regInfo.RegionName;
                hash["hostname"] = regInfo.ExternalHostName;
                hash["http_port"] = regInfo.HttpPort.ToString();
                hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
                hash["position"] = position.ToString();
                hash["lookAt"] = lookAt.ToString();
            }
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse AgentIsComingHome(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string gridName = (string)requestData["externalName"];

            bool success = m_HomeUsersService.AgentIsComingHome(sessionID, gridName);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse VerifyAgent(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string token = (string)requestData["token"];

            bool success = m_HomeUsersService.VerifyAgent(sessionID, token);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse VerifyClient(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string token = (string)requestData["token"];

            bool success = m_HomeUsersService.VerifyClient(sessionID, token);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse LogoutAgent(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string userID_str = (string)requestData["userID"];
            UUID userID = UUID.Zero;
            UUID.TryParse(userID_str, out userID);

            m_HomeUsersService.LogoutAgent(userID, sessionID);

            Hashtable hash = new Hashtable();
            hash["result"] = "true";
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

    }
}
