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
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Simulation
{
    public class AgentGetHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public AgentGetHandler(ISimulationService service, IAuthenticationService authentication) :
                base("GET", "/agent")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Not implemented yet
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }

    public class AgentPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ISimulationService m_SimulationService;
        private IAuthenticationService m_AuthenticationService;
        // TODO: unused: private bool m_AllowForeignGuests;

        public AgentPostHandler(ISimulationService service, IAuthenticationService authentication, bool foreignGuests) :
            base("POST", "/agent")
        {
            m_SimulationService = service;
            m_AuthenticationService = authentication;
            // TODO: unused: m_AllowForeignGuests = foreignGuests;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            byte[] result = new byte[0];

            UUID agentID;
            string action;
            ulong regionHandle;
            if (!RestHandlerUtils.GetParams(path, out agentID, out regionHandle, out action))
            {
                m_log.InfoFormat("[AgentPostHandler]: Invalid parameters for agent message {0}", path);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Invalid parameters for agent message " + path;

                return result;
            }

            if (m_AuthenticationService != null)
            {
                // Authentication
                string authority = string.Empty;
                string authToken = string.Empty;
                if (!RestHandlerUtils.GetAuthentication(httpRequest, out authority, out authToken))
                {
                    m_log.InfoFormat("[AgentPostHandler]: Authentication failed for agent message {0}", path);
                    httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return result;
                }
                // TODO: Rethink this
                //if (!m_AuthenticationService.VerifyKey(agentID, authToken))
                //{
                //    m_log.InfoFormat("[AgentPostHandler]: Authentication failed for agent message {0}", path);
                //    httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                //    return result;
                //}
                m_log.DebugFormat("[AgentPostHandler]: Authentication succeeded for {0}", agentID);
            }

            OSDMap args = Util.GetOSDMap(request, (int)httpRequest.ContentLength);
            if (args == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Unable to retrieve data";
                m_log.DebugFormat("[AgentPostHandler]: Unable to retrieve data for post {0}", path);
                return result;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            AgentCircuitData aCircuit = new AgentCircuitData();
            try
            {
                aCircuit.UnpackAgentCircuitData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[AgentPostHandler]: exception on unpacking CreateAgent message {0}", ex.Message);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.StatusDescription = "Problems with data deserialization";
                return result;
            }

            string reason = string.Empty;

            // We need to clean up a few things in the user service before I can do this
            //if (m_AllowForeignGuests)
            //    m_regionClient.AdjustUserInformation(aCircuit);

            // Finally!
            bool success = m_SimulationService.CreateAgent(regionhandle, aCircuit, out reason);

            OSDMap resp = new OSDMap(1);

            resp["success"] = OSD.FromBoolean(success);

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp));
        }
    }

    public class AgentPutHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public AgentPutHandler(ISimulationService service, IAuthenticationService authentication) :
            base("PUT", "/agent")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Not implemented yet
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }

    public class AgentDeleteHandler : BaseStreamHandler
    {
        // TODO: unused: private ISimulationService m_SimulationService;
        // TODO: unused: private IAuthenticationService m_AuthenticationService;

        public AgentDeleteHandler(ISimulationService service, IAuthenticationService authentication) :
            base("DELETE", "/agent")
        {
            // TODO: unused: m_SimulationService = service;
            // TODO: unused: m_AuthenticationService = authentication;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // Not implemented yet
            httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
            return new byte[] { };
        }
    }
}
