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
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Simulation;
using Utils = OpenSim.Server.Handlers.Simulation.Utils;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HomeAgentHandler : AgentPostHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IUserAgentService m_UserAgentService;

        private string m_LoginServerIP;

        public HomeAgentHandler(IUserAgentService userAgentService, string loginServerIP, bool proxy) :
            base("/homeagent")
        {
            m_UserAgentService = userAgentService;
            m_LoginServerIP = loginServerIP;
            m_Proxy = proxy;
        }

        protected override AgentDestinationData CreateAgentDestinationData()
        {
            return new ExtendedAgentDestinationData();
        }

        protected override void UnpackData(OSDMap args, AgentDestinationData d, Hashtable request)
        {
            base.UnpackData(args, d, request);
            ExtendedAgentDestinationData data = (ExtendedAgentDestinationData)d;
            try
            {
                if (args.ContainsKey("gatekeeper_host") && args["gatekeeper_host"] != null)
                    data.host = args["gatekeeper_host"].AsString();
                if (args.ContainsKey("gatekeeper_port") && args["gatekeeper_port"] != null)
                    Int32.TryParse(args["gatekeeper_port"].AsString(), out data.port);
                if (args.ContainsKey("gatekeeper_serveruri") && args["gatekeeper_serveruri"] != null)
                    data.gatekeeperServerURI = args["gatekeeper_serveruri"];
                if (args.ContainsKey("destination_serveruri") && args["destination_serveruri"] != null)
                    data.destinationServerURI = args["destination_serveruri"];

            }
            catch (InvalidCastException)
            {
                m_log.ErrorFormat("[HOME AGENT HANDLER]: Bad cast in UnpackData");
            }

            string callerIP = GetCallerIP(request);
            // Verify if this call came from the login server
            if (callerIP == m_LoginServerIP)
                data.fromLogin = true;

        }

        protected override GridRegion ExtractGatekeeper(AgentDestinationData d)
        {
            if (d is ExtendedAgentDestinationData)
            {
                ExtendedAgentDestinationData data = (ExtendedAgentDestinationData)d;
                GridRegion gatekeeper = new GridRegion();
                gatekeeper.ServerURI = data.gatekeeperServerURI;
                gatekeeper.ExternalHostName = data.host;
                gatekeeper.HttpPort = (uint)data.port;
                gatekeeper.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

                return gatekeeper;
            }
            else
                m_log.WarnFormat("[HOME AGENT HANDLER]: Wrong data type");

            return null;
        }


        protected override bool CreateAgent(GridRegion source, GridRegion gatekeeper, GridRegion destination,
            AgentCircuitData aCircuit, uint teleportFlags, bool fromLogin, EntityTransferContext ctx, out string reason)
        {
            return m_UserAgentService.LoginAgentToGrid(source, aCircuit, gatekeeper, destination, fromLogin, out reason);
        }

    }

    public class ExtendedAgentDestinationData : AgentDestinationData
    {
        public string host;
        public int port;
        public string gatekeeperServerURI;
        public string destinationServerURI;

    }

}
