using System;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
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
        private ISimulationService m_SimulationService;
        private IAuthenticationService m_AuthenticationService;

        public AgentGetHandler(ISimulationService service, IAuthenticationService authentication) :
                base("GET", "/agent")
        {
            m_SimulationService = service;
            m_AuthenticationService = authentication;
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
        private bool m_AllowForeignGuests;

        public AgentPostHandler(ISimulationService service, IAuthenticationService authentication, bool foreignGuests) :
            base("POST", "/agent")
        {
            m_SimulationService = service;
            m_AuthenticationService = authentication;
            m_AllowForeignGuests = foreignGuests;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            byte[] result = new byte[0];

            UUID agentID;
            string action;
            ulong regionHandle;
            if (!Utils.GetParams(path, out agentID, out regionHandle, out action))
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
                if (!Utils.GetAuthentication(httpRequest, out authority, out authToken))
                {
                    m_log.InfoFormat("[AgentPostHandler]: Authentication failed for agent message {0}", path);
                    httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return result;
                }
                if (!m_AuthenticationService.VerifyKey(agentID, authToken))
                {
                    m_log.InfoFormat("[AgentPostHandler]: Authentication failed for agent message {0}", path);
                    httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                    return result;
                }
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

            return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(resp));

        }
    }

    public class AgentPutHandler : BaseStreamHandler
    {
        private ISimulationService m_SimulationService;
        private IAuthenticationService m_AuthenticationService;

        public AgentPutHandler(ISimulationService service, IAuthenticationService authentication) :
            base("PUT", "/agent")
        {
            m_SimulationService = service;
            m_AuthenticationService = authentication;
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
        private ISimulationService m_SimulationService;
        private IAuthenticationService m_AuthenticationService;

        public AgentDeleteHandler(ISimulationService service, IAuthenticationService authentication) :
            base("DELETE", "/agent")
        {
            m_SimulationService = service;
            m_AuthenticationService = authentication;
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
