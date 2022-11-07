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
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Net;

using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;


namespace OpenSim.Server.Handlers.Simulation
{
    //this is only for hg homeagent and gatekeeperagent
    public class AgentPostHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Proxy = false;

        public AgentPostHandler(string path) : base(path)
        {
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // m_log.DebugFormat("[SIMULATION]: Stream handler called");

            httpResponse.ContentType = "text/html"; //??
            httpResponse.KeepAlive = false;

            if(httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                httpResponse.RawBuffer = Utils.falseStrBytes;
            }

            if (httpRequest.ContentType != "application/json" && httpRequest.ContentType != "application/x-gzip")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotAcceptable;
                httpResponse.RawBuffer = Utils.falseStrBytes;
            }

            UUID agentID;
            UUID regionID;
            string action;

            if (!Utils.GetParams(httpRequest.UriPath, out agentID, out regionID, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", httpRequest.RawUrl);

                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                httpResponse.RawBuffer = Utils.falseStrBytes;
            }

            OSDMap args = Utils.DeserializeJSONOSMap(httpRequest);
            if (args == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.RawBuffer = Utils.falseStrBytes;
            }

            DoAgentPost(args, httpRequest.RemoteIPEndPoint.Address.ToString(), httpResponse, agentID);

            httpResponse.StatusCode = 200;
        }

        protected void DoAgentPost(OSDMap args, string remoteAddress, IOSHttpResponse response, UUID id)
        {
            OSD tmpOSD;
            EntityTransferContext ctx = new EntityTransferContext();
            if (args.TryGetValue("context", out tmpOSD) && tmpOSD is OSDMap)
                ctx.Unpack((OSDMap)tmpOSD);

            AgentDestinationData data = CreateAgentDestinationData();
            UnpackData(args, data, remoteAddress);

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
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            GridRegion source = null;

            if (args.TryGetValue("source_uuid", out tmpOSD))
            {
                source = new GridRegion();
                source.RegionID = UUID.Parse(tmpOSD.AsString());
                source.RegionLocX = Int32.Parse(args["source_x"].AsString());
                source.RegionLocY = Int32.Parse(args["source_y"].AsString());
                source.RegionName = args["source_name"].AsString();

                if (args.TryGetValue("source_server_uri", out tmpOSD))
                    source.RawServerURI = tmpOSD.AsString();
                else
                    source.RawServerURI = null;
            }

            bool result = CreateAgent(source, gatekeeper, destination, aCircuit, data.flags, data.fromLogin, ctx, out string reason);

            OSDMap resp = new OSDMap(3);
            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);
            // Let's also send out the IP address of the caller back to the caller (HG 1.5)
            resp["your_ip"] = remoteAddress;

            response.StatusCode = (int)HttpStatusCode.OK;
            response.RawBuffer = OSDParser.SerializeJsonToBytes(resp);
        }

        protected virtual AgentDestinationData CreateAgentDestinationData()
        {
            return new AgentDestinationData();
        }

        protected virtual void UnpackData(OSDMap args, AgentDestinationData data, string remoteAddress)
        {
            OSD tmpOSD;
            // retrieve the input arguments
            if (args.TryGetValue("destination_x", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out data.x);
            else
                m_log.WarnFormat("  -- request didn't have destination_x");

            if (args.TryGetValue("destination_y", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out data.y);
            else
                m_log.WarnFormat("  -- request didn't have destination_y");

            if (args.TryGetValue("destination_uuid", out tmpOSD) && tmpOSD != null)
                UUID.TryParse(tmpOSD.AsString(), out data.uuid);

            if (args.TryGetValue("destination_name", out tmpOSD) && tmpOSD != null)
                data.name = tmpOSD.ToString();

            if (args.TryGetValue("teleport_flags", out tmpOSD) && tmpOSD != null)
                data.flags = tmpOSD.AsUInteger();
        }

        protected virtual GridRegion ExtractGatekeeper(AgentDestinationData data)
        {
            return null;
        }

        // subclasses must override this
        protected virtual bool CreateAgent(GridRegion source, GridRegion gatekeeper, GridRegion destination,
            AgentCircuitData aCircuit, uint teleportFlags, bool fromLogin, EntityTransferContext ctx, out string reason)
        {
            reason = "Configuration issues, plz mantis";
            return false;
        }
    }

    public class AgentSimpleHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimulationService m_SimulationService;
        protected bool m_Proxy = false;

        public AgentSimpleHandler(ISimulationService service) : base("/agent")
        {
            m_SimulationService = service;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;
            httpResponse.ContentType = "application/json";
            if (m_SimulationService == null)
            {
                m_log.Debug("[AGENT HANDLER]: ProcessRequest called with null Simulation Service");
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                httpResponse.RawBuffer = Utils.falseStrBytes;
                return;
            }

            if (!Utils.GetParams(httpRequest.UriPath, out UUID agentID, out UUID regionID, out string action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", httpRequest.UriPath);

                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                httpResponse.RawBuffer = Utils.falseStrBytes;
                return;
            }

            switch(httpRequest.HttpMethod)
            {
                case "QUERYACCESS":
                {
                    if (agentID.IsZero() || regionID.IsZero())
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    OSDMap args = Utils.DeserializeJSONOSMap(httpRequest);
                    if (args == null)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    DoQueryAccess(args, httpResponse, agentID, regionID);
                    break;
                }
                case "PUT":
                {
                    OSDMap args = Utils.DeserializeJSONOSMap(httpRequest);
                    if (args == null)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }

                    DoAgentPut(args, httpResponse);
                    break;
                }
                case "POST":
                {
                    if (agentID.IsZero())
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    OSDMap args = Utils.DeserializeJSONOSMap(httpRequest);
                    if (args == null)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    DoAgentPost(args, httpRequest, httpResponse, agentID);
                    break;
                }
                case "DELETE":
                {
                    if (agentID.IsZero() || regionID.IsZero())
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    httpRequest.QueryAsDictionary.TryGetValue("auth", out string auth_token);

                    DoAgentDelete(httpRequest, httpResponse, agentID, action, regionID, auth_token);
                    break;
                }
                default:
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    httpResponse.RawBuffer = Utils.falseStrBytes;
                    return;
                }
            }
        }

        protected virtual void DoQueryAccess(OSDMap args, IOSHttpResponse httpResponse, UUID agentID, UUID regionID)
        {
            bool viaTeleport = true;
            OSD tmpOSD;
            if (args.TryGetValue("viaTeleport", out tmpOSD))
                viaTeleport = tmpOSD.AsBoolean();

            Vector3 position = Vector3.Zero;
            if (args.TryGetValue("position", out tmpOSD))
                position = Vector3.Parse(tmpOSD.AsString());

            string agentHomeURI = null;
            if (args.TryGetValue("agent_home_uri", out tmpOSD))
                agentHomeURI = tmpOSD.AsString();

            // Decode the legacy (string) version and extract the number
            float theirVersion = 0f;
            if (args.TryGetValue("my_version", out tmpOSD))
            {
                string theirVersionStr = tmpOSD.AsString();
                string[] parts = theirVersionStr.Split(new char[] { '/' });
                if (parts.Length > 1)
                    theirVersion = float.Parse(parts[1], Culture.FormatProvider);
            }

            EntityTransferContext ctx = new EntityTransferContext();
            if (args.TryGetValue("context", out tmpOSD) && tmpOSD is OSDMap)
                ctx.Unpack((OSDMap)tmpOSD);

            // Decode the new versioning data
            float minVersionRequired = 0f;
            float maxVersionRequired = 0f;
            float minVersionProvided = 0f;
            float maxVersionProvided = 0f;

            if (args.TryGetValue("simulation_service_supported_min", out tmpOSD))
                minVersionProvided = (float)tmpOSD.AsReal();
            if (args.TryGetValue("simulation_service_supported_max", out tmpOSD))
                maxVersionProvided = (float)tmpOSD.AsReal();

            if (args.TryGetValue("simulation_service_accepted_min", out tmpOSD))
                minVersionRequired = (float)tmpOSD.AsReal();
            if (args.TryGetValue("simulation_service_accepted_max", out tmpOSD))
                maxVersionRequired = (float)tmpOSD.AsReal();

            OSDMap resp = new OSDMap(3);

            float version = 0f;

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            

            float outboundVersion = 0f;
            float inboundVersion = 0f;

            if (minVersionProvided == 0f) // string version or older
            {
                // If there is no version in the packet at all we're looking at 0.6 or
                // even more ancient. Refuse it.
                if (theirVersion == 0f)
                {
                    resp["success"] = OSD.FromBoolean(false);
                    resp["reason"] = OSD.FromString("Your region is running a old version of opensim no longer supported. Consider updating it");
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp, true));
                    return;
                }

                version = theirVersion;

                if (version < VersionInfo.SimulationServiceVersionAcceptedMin ||
                    version > VersionInfo.SimulationServiceVersionAcceptedMax)
                {
                    resp["success"] = OSD.FromBoolean(false);
                    resp["reason"] = OSD.FromString(String.Format("Your region protocol version is {0} and we accept only {1} - {2}. No version overlap.", theirVersion, VersionInfo.SimulationServiceVersionAcceptedMin, VersionInfo.SimulationServiceVersionAcceptedMax));
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp, true));
                    return;
                }
            }
            else
            {
                // Test for no overlap
                if (minVersionProvided > VersionInfo.SimulationServiceVersionAcceptedMax ||
                    maxVersionProvided < VersionInfo.SimulationServiceVersionAcceptedMin)
                {
                    resp["success"] = OSD.FromBoolean(false);
                    resp["reason"] = OSD.FromString(String.Format("Your region provide protocol versions {0} - {1} and we accept only {2} - {3}. No version overlap.", minVersionProvided, maxVersionProvided, VersionInfo.SimulationServiceVersionAcceptedMin, VersionInfo.SimulationServiceVersionAcceptedMax));
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp, true));
                    return;
                }
                if (minVersionRequired > VersionInfo.SimulationServiceVersionSupportedMax ||
                    maxVersionRequired < VersionInfo.SimulationServiceVersionSupportedMin)
                {
                    resp["success"] = OSD.FromBoolean(false);
                    resp["reason"] = OSD.FromString(String.Format("You require region protocol versions {0} - {1} and we provide only {2} - {3}. No version overlap.", minVersionRequired, maxVersionRequired, VersionInfo.SimulationServiceVersionSupportedMin, VersionInfo.SimulationServiceVersionSupportedMax));
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp, true));
                    return;
                }

                // Determine versions to use
                // This is intentionally inverted. Inbound and Outbound refer to the direction of the transfer.
                // Therefore outbound means from the sender to the receier and inbound means from the receiver to the sender.
                // So outbound is what we will accept and inbound is what we will send. Confused yet?
                outboundVersion = Math.Min(maxVersionProvided, VersionInfo.SimulationServiceVersionAcceptedMax);
                inboundVersion = Math.Min(maxVersionRequired, VersionInfo.SimulationServiceVersionSupportedMax);
            }

            List<UUID> features = new List<UUID>();

            if (args.TryGetValue("features", out tmpOSD) && tmpOSD is OSDArray)
            {
                OSDArray array = (OSDArray)tmpOSD;

                foreach (OSD o in array)
                    features.Add(new UUID(o.AsString()));
            }

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            string reason;
            // We're sending the version numbers down to the local connector to do the varregion check.
            ctx.InboundVersion = inboundVersion;
            ctx.OutboundVersion = outboundVersion;
            if (minVersionProvided == 0f)
            {
                ctx.InboundVersion = version;
                ctx.OutboundVersion = version;
            }

            bool result = m_SimulationService.QueryAccess(destination, agentID, agentHomeURI, viaTeleport, position, features, ctx, out reason);
            m_log.DebugFormat("[AGENT HANDLER]: QueryAccess returned {0} ({1}). Version={2}, {3}/{4}",
                result, reason, version, inboundVersion, outboundVersion);

            resp["success"] = OSD.FromBoolean(result);
            resp["reason"] = OSD.FromString(reason);
            string legacyVersion = String.Format(Culture.FormatProvider, "SIMULATION/{0}", version);
            resp["version"] = OSD.FromString(legacyVersion);
            resp["negotiated_inbound_version"] = OSD.FromReal(inboundVersion);
            resp["negotiated_outbound_version"] = OSD.FromReal(outboundVersion);

            OSDArray featuresWanted = new OSDArray();
            foreach (UUID feature in features)
                featuresWanted.Add(OSD.FromString(feature.ToString()));

            resp["features"] = featuresWanted;

            httpResponse.KeepAlive = result;

            // We must preserve defaults here, otherwise a false "success" will not be put into the JSON map!
            httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp, true));

            // console.WriteLine("str_response_string [{0}]", responsedata["str_response_string"]);
        }

        protected void DoAgentDelete(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID, string action, UUID regionID, string auth_token)
        {
            if (string.IsNullOrEmpty(action))
                m_log.DebugFormat("[AGENT HANDLER]: >>> DELETE <<< RegionID: {0}; from: {1}; auth_code: {2}",
                    regionID, httpRequest.RemoteIPEndPoint.Address.ToString(), auth_token);
            else
                m_log.DebugFormat("[AGENT HANDLER]: Release {0} to RegionID: {1}", agentID, regionID);


            if (action.Equals("release"))
                m_SimulationService.ReleaseAgent(regionID, agentID, "");
            else
            {
                GridRegion destination = new GridRegion();
                destination.RegionID = regionID;
                Util.FireAndForget(
                    o => m_SimulationService.CloseAgent(destination, agentID, auth_token), null, "AgentHandler.DoAgentDelete");
            }

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.RawBuffer = Util.UTF8.GetBytes("OpenSim agent " + agentID.ToString());

            //m_log.DebugFormat("[AGENT HANDLER]: Agent {0} Released/Deleted from region {1}", id, regionID);
        }

        protected void DoAgentPost(OSDMap args, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID)
        {
            OSD tmpOSD;
            EntityTransferContext ctx = new EntityTransferContext();
            if (args.TryGetValue("context", out tmpOSD) && tmpOSD is OSDMap)
                ctx.Unpack((OSDMap)tmpOSD);

            AgentDestinationData data = CreateAgentDestinationData();
            UnpackData(args, data);

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
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                httpResponse.RawBuffer = Util.UTF8.GetBytes("false");
                return;
            }

            GridRegion source = null;

            if (args.TryGetValue("source_uuid", out tmpOSD))
            {
                source = new GridRegion();
                source.RegionID = UUID.Parse(tmpOSD.AsString());
                source.RegionLocX = Int32.Parse(args["source_x"].AsString());
                source.RegionLocY = Int32.Parse(args["source_y"].AsString());
                source.RegionName = args["source_name"].AsString();

                if (args.TryGetValue("source_server_uri", out tmpOSD))
                    source.RawServerURI = tmpOSD.AsString();
                else
                    source.RawServerURI = null;
            }

            OSDMap resp = new OSDMap(2);
            string reason = string.Empty;

            bool result = CreateAgent(source, gatekeeper, destination, aCircuit, data.flags, ctx, out reason);

            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);
            // Let's also send out the IP address of the caller back to the caller (HG 1.5)
            resp["your_ip"] = OSD.FromString(httpRequest.RemoteIPEndPoint.Address.ToString());

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp));
            httpResponse.KeepAlive = (data.flags & (uint)(TeleportFlags.ViaLogin | TeleportFlags.ViaHGLogin)) != (uint)TeleportFlags.ViaLogin;
        }

        protected virtual AgentDestinationData CreateAgentDestinationData()
        {
            return new AgentDestinationData();
        }

        protected virtual void UnpackData(OSDMap args, AgentDestinationData data)
        {
            OSD tmpOSD;
            // retrieve the input arguments
            if (args.TryGetValue("destination_x", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out data.x);
            else
                m_log.WarnFormat("  -- request didn't have destination_x");

            if (args.TryGetValue("destination_y", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out data.y);
            else
                m_log.WarnFormat("  -- request didn't have destination_y");

            if (args.TryGetValue("destination_uuid", out tmpOSD) && tmpOSD != null)
                UUID.TryParse(tmpOSD.AsString(), out data.uuid);

            if (args.TryGetValue("destination_name", out tmpOSD) && tmpOSD != null)
                data.name = tmpOSD.ToString();

            if (args.TryGetValue("teleport_flags", out tmpOSD) && tmpOSD != null)
                data.flags = tmpOSD.AsUInteger();
        }

        protected virtual GridRegion ExtractGatekeeper(AgentDestinationData data)
        {
            return null;
        }

        // subclasses can override this
        protected virtual bool CreateAgent(GridRegion source, GridRegion gatekeeper, GridRegion destination,
            AgentCircuitData aCircuit, uint teleportFlags, EntityTransferContext ctx, out string reason)
        {
            reason = string.Empty;
            bool ret = m_SimulationService.CreateAgent(source, destination, aCircuit, teleportFlags, ctx, out reason);
            //                m_log.DebugFormat("[AGENT HANDLER]: SYNC CreateAgent {0} {1}", ret.ToString(), reason);
            return ret;
        }

        protected void DoAgentPut(OSDMap args, IOSHttpResponse httpResponse)
        {
            // retrieve the input arguments
            OSD tmpOSD;
            EntityTransferContext ctx = new EntityTransferContext();
            int x = 0, y = 0;
            UUID uuid = UUID.Zero;
            string regionname = string.Empty;
            if (args.TryGetValue("destination_x", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out x);
            if (args.TryGetValue("destination_y", out tmpOSD) && tmpOSD != null)
                Int32.TryParse(tmpOSD.AsString(), out y);
            if (args.TryGetValue("destination_uuid", out tmpOSD) && tmpOSD != null)
                UUID.TryParse(tmpOSD.AsString(), out uuid);
            if (args.TryGetValue("destination_name", out tmpOSD) && tmpOSD != null)
                regionname = tmpOSD.ToString();
            if (args.TryGetValue("context", out tmpOSD) && tmpOSD is OSDMap)
                ctx.Unpack((OSDMap)tmpOSD);

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
                    agent.Unpack(args, m_SimulationService.GetScene(destination.RegionID), ctx);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    httpResponse.RawBuffer = Util.UTF8.GetBytes("false");
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
                    agent.Unpack(args, m_SimulationService.GetScene(destination.RegionID), ctx);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    httpResponse.RawBuffer = Util.UTF8.GetBytes("false");
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_SimulationService.UpdateAgent(destination, agent);
            }

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.RawBuffer = Util.UTF8.GetBytes(result.ToString());
            //responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp); ??? instead
        }

        // subclasses can override this
        protected virtual bool UpdateAgent(GridRegion destination, AgentData agent)
        {
            // The data and protocols are already defined so this is just a dummy to satisfy the interface
            // TODO: make this end-to-end
            return m_SimulationService.UpdateAgent(destination, agent, new EntityTransferContext());
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
