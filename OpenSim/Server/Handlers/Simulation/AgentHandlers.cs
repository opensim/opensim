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
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Simulation
{
    public class AgentHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ISimulationService m_SimulationService;

        public AgentHandler(ISimulationService sim)
        {
            m_SimulationService = sim;
        }

        public Hashtable Handler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: AgentHandler Called");

            m_log.Debug("---------------------------");
            m_log.Debug(" >> uri=" + request["uri"]);
            m_log.Debug(" >> content-type=" + request["content-type"]);
            m_log.Debug(" >> http-method=" + request["http-method"]);
            m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";
            responsedata["keepalive"] = false;


            UUID agentID;
            string action;
            ulong regionHandle;
            if (!Utils.GetParams((string)request["uri"], out agentID, out regionHandle, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("PUT"))
            {
                DoAgentPut(request, responsedata);
                return responsedata;
            }
            else if (method.Equals("POST"))
            {
                DoAgentPost(request, responsedata, agentID);
                return responsedata;
            }
            else if (method.Equals("GET"))
            {
                DoAgentGet(request, responsedata, agentID, regionHandle);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoAgentDelete(request, responsedata, agentID, action, regionHandle);
                return responsedata;
            }
            else
            {
                m_log.InfoFormat("[AGENT HANDLER]: method {0} not supported in agent message", method);
                responsedata["int_response_code"] = HttpStatusCode.MethodNotAllowed;
                responsedata["str_response_string"] = "Method not allowed";

                return responsedata;
            }

        }

        protected virtual void DoAgentPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
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
                m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildCreate message {0}", ex.Message);
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            OSDMap resp = new OSDMap(2);
            string reason = String.Empty;
            uint teleportFlags = 0;
            if (args.ContainsKey("teleport_flags"))
            {
                teleportFlags = args["teleport_flags"].AsUInteger();
            }

            // This is the meaning of POST agent
            //m_regionClient.AdjustUserInformation(aCircuit);
            bool result = m_SimulationService.CreateAgent(regionhandle, aCircuit, teleportFlags, out reason);

            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);

            // TODO: add reason if not String.Empty?
            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        protected virtual void DoAgentPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            string messageType;
            if (args["message_type"] != null)
                messageType = args["message_type"].AsString();
            else
            {
                m_log.Warn("[AGENT HANDLER]: Agent Put Message Type not found. ");
                messageType = "AgentData";
            }

            bool result = true;
            if ("AgentData".Equals(messageType))
            {
                AgentData agent = new AgentData();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                    responsedata["str_response_string"] = "Bad request";
                    return;
                }

                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_SimulationService.UpdateAgent(regionhandle, agent);

            }
            else if ("AgentPosition".Equals(messageType))
            {
                AgentPosition agent = new AgentPosition();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_SimulationService.UpdateAgent(regionhandle, agent);

            }

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = result.ToString();
            //responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp); ??? instead
        }

        protected virtual void DoAgentGet(Hashtable request, Hashtable responsedata, UUID id, ulong regionHandle)
        {
            IAgentData agent = null;
            bool result = m_SimulationService.RetrieveAgent(regionHandle, id, out agent);
            OSDMap map = null;
            if (result)
            {
                if (agent != null) // just to make sure
                {
                    map = agent.Pack();
                    string strBuffer = "";
                    try
                    {
                        strBuffer = OSDParser.SerializeJsonString(map);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[AGENT HANDLER]: Exception thrown on serialization of DoAgentGet: {0}", e.Message);
                        responsedata["int_response_code"] = HttpStatusCode.InternalServerError;
                        // ignore. buffer will be empty, caller should check.
                    }

                    responsedata["content_type"] = "application/json";
                    responsedata["int_response_code"] = HttpStatusCode.OK;
                    responsedata["str_response_string"] = strBuffer;
                }
                else
                {
                    responsedata["int_response_code"] = HttpStatusCode.InternalServerError;
                    responsedata["str_response_string"] = "Internal error";
                }
            }
            else
            {
                responsedata["int_response_code"] = HttpStatusCode.NotFound;
                responsedata["str_response_string"] = "Not Found";
            }
        }

        protected virtual void DoAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, ulong regionHandle)
        {
            //m_log.Debug(" >>> DoDelete action:" + action + "; regionHandle:" + regionHandle);

            if (action.Equals("release"))
                m_SimulationService.ReleaseAgent(regionHandle, id, "");
            else
                m_SimulationService.CloseAgent(regionHandle, id);

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = "OpenSim agent " + id.ToString();

            m_log.Debug("[AGENT HANDLER]: Agent Deleted.");
        }
    }

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
            bool success = m_SimulationService.CreateAgent(regionhandle, aCircuit, /*!!!*/0, out reason);

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
