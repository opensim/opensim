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
using System.IO.Compression;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
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

        public AgentHandler() { }

        public AgentHandler(ISimulationService sim)
        {
            m_SimulationService = sim;
        }

        public Hashtable Handler(Hashtable request)
        {
//            m_log.Debug("[CONNECTION DEBUGGING]: AgentHandler Called");
//
//            m_log.Debug("---------------------------");
//            m_log.Debug(" >> uri=" + request["uri"]);
//            m_log.Debug(" >> content-type=" + request["content-type"]);
//            m_log.Debug(" >> http-method=" + request["http-method"]);
//            m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";
            responsedata["keepalive"] = false;


            UUID agentID;
            UUID regionID;
            string action;
            if (!Utils.GetParams((string)request["uri"], out agentID, out regionID, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("GET"))
            {
                DoAgentGet(request, responsedata, agentID, regionID);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoAgentDelete(request, responsedata, agentID, action, regionID);
                return responsedata;
            }
            else if (method.Equals("DELETECHILD"))
            {
                DoChildAgentDelete(request, responsedata, agentID, action, regionID);
                return responsedata;
            }
            else if (method.Equals("QUERYACCESS"))
            {
                DoQueryAccess(request, responsedata, agentID, regionID);
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

        protected virtual void DoQueryAccess(Hashtable request, Hashtable responsedata, UUID id, UUID regionID)
        {
            if (m_SimulationService == null)
            {
                m_log.Debug("[AGENT HANDLER]: Agent QUERY called. Harmless but useless.");
                responsedata["content_type"] = "application/json";
                responsedata["int_response_code"] = HttpStatusCode.NotImplemented;
                responsedata["str_response_string"] = string.Empty;

                return;
            }

            // m_log.DebugFormat("[AGENT HANDLER]: Received QUERYACCESS with {0}", (string)request["body"]);
            OSDMap args = Utils.GetOSDMap((string)request["body"]);

            Vector3 position = Vector3.Zero;
            if (args.ContainsKey("position"))
                position = Vector3.Parse(args["position"].AsString());

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            string reason;
            string version;
            bool result = m_SimulationService.QueryAccess(destination, id, position, out version, out reason);

            responsedata["int_response_code"] = HttpStatusCode.OK;

            OSDMap resp = new OSDMap(3);

            resp["success"] = OSD.FromBoolean(result);
            resp["reason"] = OSD.FromString(reason);
            resp["version"] = OSD.FromString(version);

            // We must preserve defaults here, otherwise a false "success" will not be put into the JSON map!
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp, true);

//            Console.WriteLine("str_response_string [{0}]", responsedata["str_response_string"]);
        }

        protected virtual void DoAgentGet(Hashtable request, Hashtable responsedata, UUID id, UUID regionID)
        {
            if (m_SimulationService == null)
            {
                m_log.Debug("[AGENT HANDLER]: Agent GET called. Harmless but useless.");
                responsedata["content_type"] = "application/json";
                responsedata["int_response_code"] = HttpStatusCode.NotImplemented;
                responsedata["str_response_string"] = string.Empty;

                return;
            }

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            IAgentData agent = null;
            bool result = m_SimulationService.RetrieveAgent(destination, id, out agent);
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

        protected void DoChildAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, UUID regionID)
        {
            m_log.Debug(" >>> DoChildAgentDelete action:" + action + "; RegionID:" + regionID);

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            if (action.Equals("release"))
                ReleaseAgent(regionID, id);
            else
                m_SimulationService.CloseChildAgent(destination, id);

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = "OpenSim agent " + id.ToString();

            m_log.Debug("[AGENT HANDLER]: Child Agent Released/Deleted.");
        }

        protected void DoAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, UUID regionID)
        {
            m_log.Debug(" >>> DoDelete action:" + action + "; RegionID:" + regionID);

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            if (action.Equals("release"))
                ReleaseAgent(regionID, id);
            else
                Util.FireAndForget(delegate { m_SimulationService.CloseAgent(destination, id); });

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = "OpenSim agent " + id.ToString();

            m_log.DebugFormat("[AGENT HANDLER]: Agent {0} Released/Deleted from region {1}", id, regionID);
        }

        protected virtual void ReleaseAgent(UUID regionID, UUID id)
        {
            m_SimulationService.ReleaseAgent(regionID, id, "");
        }
    }

    public class AgentPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimulationService m_SimulationService;
        protected bool m_Proxy = false;

        public AgentPostHandler(ISimulationService service) :
                base("POST", "/agent")
        {
            m_SimulationService = service;
        }

        public AgentPostHandler(string path) :
                base("POST", path)
        {
            m_SimulationService = null;
        }

        public override byte[] Handle(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat("[SIMULATION]: Stream handler called");

            Hashtable keysvals = new Hashtable();
            Hashtable headervals = new Hashtable();

            string[] querystringkeys = httpRequest.QueryString.AllKeys;
            string[] rHeaders = httpRequest.Headers.AllKeys;

            keysvals.Add("uri", httpRequest.RawUrl);
            keysvals.Add("content-type", httpRequest.ContentType);
            keysvals.Add("http-method", httpRequest.HttpMethod);

            foreach (string queryname in querystringkeys)
                keysvals.Add(queryname, httpRequest.QueryString[queryname]);

            foreach (string headername in rHeaders)
                headervals[headername] = httpRequest.Headers[headername];

            keysvals.Add("headers", headervals);
            keysvals.Add("querystringkeys", querystringkeys);

            httpResponse.StatusCode = 200;
            httpResponse.ContentType = "text/html";
            httpResponse.KeepAlive = false;
            Encoding encoding = Encoding.UTF8;

            Stream inputStream = null;
            if (httpRequest.ContentType == "application/x-gzip")
                inputStream = new GZipStream(request, CompressionMode.Decompress);
            else if (httpRequest.ContentType == "application/json")
                inputStream = request;
            else // no go
            {
                httpResponse.StatusCode = 406;
                return encoding.GetBytes("false");
            }

            StreamReader reader = new StreamReader(inputStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            keysvals.Add("body", requestBody);

            Hashtable responsedata = new Hashtable();

            UUID agentID;
            UUID regionID;
            string action;

            if (!Utils.GetParams((string)keysvals["uri"], out agentID, out regionID, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", keysvals["uri"]);

                httpResponse.StatusCode = 404;

                return encoding.GetBytes("false");
            }

            DoAgentPost(keysvals, responsedata, agentID);

            httpResponse.StatusCode = (int)responsedata["int_response_code"];
            return encoding.GetBytes((string)responsedata["str_response_string"]);
        }

        protected void DoAgentPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            AgentDestinationData data = CreateAgentDestinationData();
            UnpackData(args, data, request);

            GridRegion destination = new GridRegion();
            destination.RegionID = data.uuid;
            destination.RegionLocX = data.x;
            destination.RegionLocY = data.y;
            destination.RegionName = data.name;

            GridRegion gatekeeper = ExtractGatekeeper(data);

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

            // This is the meaning of POST agent
            //m_regionClient.AdjustUserInformation(aCircuit);
            //bool result = m_SimulationService.CreateAgent(destination, aCircuit, teleportFlags, out reason);
            bool result = CreateAgent(gatekeeper, destination, aCircuit, data.flags, data.fromLogin, out reason);

            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);
            // Let's also send out the IP address of the caller back to the caller (HG 1.5)
            resp["your_ip"] = OSD.FromString(GetCallerIP(request));

            // TODO: add reason if not String.Empty?
            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        protected virtual AgentDestinationData CreateAgentDestinationData()
        {
            return new AgentDestinationData();
        }

        protected virtual void UnpackData(OSDMap args, AgentDestinationData data, Hashtable request)
        {
            // retrieve the input arguments
            if (args.ContainsKey("destination_x") && args["destination_x"] != null)
                Int32.TryParse(args["destination_x"].AsString(), out data.x);
            else
                m_log.WarnFormat("  -- request didn't have destination_x");
            if (args.ContainsKey("destination_y") && args["destination_y"] != null)
                Int32.TryParse(args["destination_y"].AsString(), out data.y);
            else
                m_log.WarnFormat("  -- request didn't have destination_y");
            if (args.ContainsKey("destination_uuid") && args["destination_uuid"] != null)
                UUID.TryParse(args["destination_uuid"].AsString(), out data.uuid);
            if (args.ContainsKey("destination_name") && args["destination_name"] != null)
                data.name = args["destination_name"].ToString();
            if (args.ContainsKey("teleport_flags") && args["teleport_flags"] != null)
                data.flags = args["teleport_flags"].AsUInteger();
        }

        protected virtual GridRegion ExtractGatekeeper(AgentDestinationData data)
        {
            return null;
        }

        protected string GetCallerIP(Hashtable request)
        {
            if (!m_Proxy)
                return Util.GetCallerIP(request);

            // We're behind a proxy
            Hashtable headers = (Hashtable)request["headers"];

            //// DEBUG
            //foreach (object o in headers.Keys)
            //    m_log.DebugFormat("XXX {0} = {1}", o.ToString(), (headers[o] == null? "null" : headers[o].ToString()));

            string xff = "X-Forwarded-For";
            if (headers.ContainsKey(xff.ToLower()))
                xff = xff.ToLower();

            if (!headers.ContainsKey(xff) || headers[xff] == null)
            {
                m_log.WarnFormat("[AGENT HANDLER]: No XFF header");
                return Util.GetCallerIP(request);
            }

            m_log.DebugFormat("[AGENT HANDLER]: XFF is {0}", headers[xff]);

            IPEndPoint ep = Util.GetClientIPFromXFF((string)headers[xff]);
            if (ep != null)
                return ep.Address.ToString();

            // Oops
            return Util.GetCallerIP(request);
        }

        // subclasses can override this
        protected virtual bool CreateAgent(GridRegion gatekeeper, GridRegion destination, AgentCircuitData aCircuit, uint teleportFlags, bool fromLogin, out string reason)
        {
            reason = String.Empty;
            
            Util.FireAndForget(x =>
            {
                string r;
                m_SimulationService.CreateAgent(destination, aCircuit, teleportFlags, out r);
            });

            return true;
        }
    }

    public class AgentPutHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimulationService m_SimulationService;
        protected bool m_Proxy = false;

        public AgentPutHandler(ISimulationService service) :
                base("PUT", "/agent")
        {
            m_SimulationService = service;
        }

        public AgentPutHandler(string path) :
                base("PUT", path)
        {
            m_SimulationService = null;
        }

        public override byte[] Handle(string path, Stream request,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat("[SIMULATION]: Stream handler called");

            Hashtable keysvals = new Hashtable();
            Hashtable headervals = new Hashtable();

            string[] querystringkeys = httpRequest.QueryString.AllKeys;
            string[] rHeaders = httpRequest.Headers.AllKeys;

            keysvals.Add("uri", httpRequest.RawUrl);
            keysvals.Add("content-type", httpRequest.ContentType);
            keysvals.Add("http-method", httpRequest.HttpMethod);

            foreach (string queryname in querystringkeys)
                keysvals.Add(queryname, httpRequest.QueryString[queryname]);

            foreach (string headername in rHeaders)
                headervals[headername] = httpRequest.Headers[headername];

            keysvals.Add("headers", headervals);
            keysvals.Add("querystringkeys", querystringkeys);

            Stream inputStream;
            if (httpRequest.ContentType == "application/x-gzip")
                inputStream = new GZipStream(request, CompressionMode.Decompress);
            else
                inputStream = request;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(inputStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            keysvals.Add("body", requestBody);

            httpResponse.StatusCode = 200;
            httpResponse.ContentType = "text/html";
            httpResponse.KeepAlive = false;

            Hashtable responsedata = new Hashtable();

            UUID agentID;
            UUID regionID;
            string action;

            if (!Utils.GetParams((string)keysvals["uri"], out agentID, out regionID, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", keysvals["uri"]);

                httpResponse.StatusCode = 404;

                return encoding.GetBytes("false");
            }

            DoAgentPut(keysvals, responsedata);

            httpResponse.StatusCode = (int)responsedata["int_response_code"];
            return encoding.GetBytes((string)responsedata["str_response_string"]);
        }

        protected void DoAgentPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            // retrieve the input arguments
            int x = 0, y = 0;
            UUID uuid = UUID.Zero;
            string regionname = string.Empty;
            if (args.ContainsKey("destination_x") && args["destination_x"] != null)
                Int32.TryParse(args["destination_x"].AsString(), out x);
            if (args.ContainsKey("destination_y") && args["destination_y"] != null)
                Int32.TryParse(args["destination_y"].AsString(), out y);
            if (args.ContainsKey("destination_uuid") && args["destination_uuid"] != null)
                UUID.TryParse(args["destination_uuid"].AsString(), out uuid);
            if (args.ContainsKey("destination_name") && args["destination_name"] != null)
                regionname = args["destination_name"].ToString();

            GridRegion destination = new GridRegion();
            destination.RegionID = uuid;
            destination.RegionLocX = x;
            destination.RegionLocY = y;
            destination.RegionName = regionname;

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
                    agent.Unpack(args, m_SimulationService.GetScene(destination.RegionID));
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
                result = UpdateAgent(destination, agent);
            }
            else if ("AgentPosition".Equals(messageType))
            {
                AgentPosition agent = new AgentPosition();
                try
                {
                    agent.Unpack(args, m_SimulationService.GetScene(destination.RegionID));
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_SimulationService.UpdateAgent(destination, agent);

            }

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = result.ToString();
            //responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp); ??? instead
        }

        // subclasses can override this
        protected virtual bool UpdateAgent(GridRegion destination, AgentData agent)
        {
            return m_SimulationService.UpdateAgent(destination, agent);
        }
    }

    public class AgentDestinationData
    {
        public int x;
        public int y;
        public string name;
        public UUID uuid;
        public uint flags;
        public bool fromLogin;
    }
}
