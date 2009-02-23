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
 *     * Neither the name of the OpenSim Project nor the
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
        protected IUGAIMCore m_gridCore;

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

        public GridMessagingModule()
        { 
        }

        public void Initialise(string opensimVersion, GridDBService gridDBService, IUGAIMCore gridCore, GridConfig config)
        {
            m_opensimVersion = opensimVersion;
            m_gridDBService = gridDBService;
            m_gridCore = gridCore;
            m_config = config;

            m_gridCore.RegisterInterface<IGridMessagingModule>(this);

            RegisterHandlers();
        }

        public void PostInitialise()
        {

        }

        public void RegisterHandlers()
        {
            //have these in separate method as some servers restart the http server and reregister all the handlers.
            m_httpServer = m_gridCore.GetHttpServer();

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
