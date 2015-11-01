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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Collections;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;

namespace OpenSim.Services.Connectors.Simulation
{
    public class SimulationServiceConnector : ISimulationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // we use this dictionary to track the pending updateagent requests, maps URI --> position update
        private Dictionary<string,AgentPosition> m_updateAgentQueue = new Dictionary<string,AgentPosition>();
        
        //private GridRegion m_Region;

        public SimulationServiceConnector()
        {
        }

        public SimulationServiceConnector(IConfigSource config)
        {
            //m_Region = region;
        }

        public IScene GetScene(UUID regionId)
        {
            return null;
        }

        public ISimulationService GetInnerService()
        {
            return null;
        }

        #region Agents

        protected virtual string AgentPath()
        {
            return "agent/";
        }

        protected virtual void PackData(OSDMap args, GridRegion source, AgentCircuitData aCircuit, GridRegion destination, uint flags)
        {
            if (source != null)
            {
                args["source_x"] = OSD.FromString(source.RegionLocX.ToString());
                args["source_y"] = OSD.FromString(source.RegionLocY.ToString());
                args["source_name"] = OSD.FromString(source.RegionName);
                args["source_uuid"] = OSD.FromString(source.RegionID.ToString());
                if (!String.IsNullOrEmpty(source.RawServerURI))
                    args["source_server_uri"] = OSD.FromString(source.RawServerURI);
            }

            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
            args["teleport_flags"] = OSD.FromString(flags.ToString());
        }

        public bool CreateAgent(GridRegion source, GridRegion destination, AgentCircuitData aCircuit, uint flags, EntityTransferContext ctx, out string reason)
        {
            string tmp = String.Empty;
            return CreateAgent(source, destination, aCircuit, flags, ctx, out tmp, out reason);
        }

        public bool CreateAgent(GridRegion source, GridRegion destination, AgentCircuitData aCircuit, uint flags, EntityTransferContext ctx, out string myipaddress, out string reason)
        {
            m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: Creating agent at {0}", destination.ServerURI);
            reason = String.Empty;
            myipaddress = String.Empty;

            if (destination == null)
            {
                reason = "Destination not found";
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Given destination is null");
                return false;
            }

            string uri = destination.ServerURI + AgentPath() + aCircuit.AgentID + "/";
            
            try
            {
                OSDMap args = aCircuit.PackAgentCircuitData(ctx);
                args["context"] = ctx.Pack();
                PackData(args, source, aCircuit, destination, flags);

                OSDMap result = WebUtil.PostToServiceCompressed(uri, args, 30000);
                bool success = result["success"].AsBoolean();
                if (success && result.ContainsKey("_Result"))
                {
                    OSDMap data = (OSDMap)result["_Result"];

                    reason = data["reason"].AsString();
                    success = data["success"].AsBoolean();
                    myipaddress = data["your_ip"].AsString();
                    return success;
                }
              
                // Try the old version, uncompressed
                result = WebUtil.PostToService(uri, args, 30000, false);

                if (result["Success"].AsBoolean())
                {
                    if (result.ContainsKey("_Result"))
                    {
                        OSDMap data = (OSDMap)result["_Result"];

                        reason = data["reason"].AsString();
                        success = data["success"].AsBoolean();
                        myipaddress = data["your_ip"].AsString();
                        m_log.WarnFormat(
                            "[REMOTE SIMULATION CONNECTOR]: Remote simulator {0} did not accept compressed transfer, suggest updating it.", destination.RegionName);
                        return success;
                    }
                }
                
                m_log.WarnFormat(
                    "[REMOTE SIMULATION CONNECTOR]: Failed to create agent {0} {1} at remote simulator {2}", 
                    aCircuit.firstname, aCircuit.lastname, destination.RegionName);                       
                reason = result["Message"] != null ? result["Message"].AsString() : "error";
                return false;
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: CreateAgent failed with exception: " + e.ToString());
                reason = e.Message;
            }

            return false;
        }

        /// <summary>
        /// Send complete data about an agent in this region to a neighbor
        /// </summary>
        public bool UpdateAgent(GridRegion destination, AgentData data, EntityTransferContext ctx)
        {
            return UpdateAgent(destination, (IAgentData)data, ctx, 200000); // yes, 200 seconds
        }

        private ExpiringCache<string, bool> _failedSims = new ExpiringCache<string, bool>();
        /// <summary>
        /// Send updated position information about an agent in this region to a neighbor
        /// This operation may be called very frequently if an avatar is moving about in
        /// the region.
        /// </summary>
        public bool UpdateAgent(GridRegion destination, AgentPosition data)
        {
            bool v = true;
            if (_failedSims.TryGetValue(destination.ServerURI, out v))
                return false;

            // The basic idea of this code is that the first thread that needs to
            // send an update for a specific avatar becomes the worker for any subsequent
            // requests until there are no more outstanding requests. Further, only send the most
            // recent update; this *should* never be needed but some requests get
            // slowed down and once that happens the problem with service end point
            // limits kicks in and nothing proceeds
            string uri = destination.ServerURI + AgentPath() + data.AgentID + "/";
            lock (m_updateAgentQueue)
            {
                if (m_updateAgentQueue.ContainsKey(uri))
                {
                    // Another thread is already handling 
                    // updates for this simulator, just update 
                    // the position and return, overwrites are
                    // not a problem since we only care about the
                    // last update anyway
                    m_updateAgentQueue[uri] = data;
                    return true;
                }

                // Otherwise update the reference and start processing
                m_updateAgentQueue[uri] = data;
            }

            AgentPosition pos = null;
            bool success = true;
            while (success)
            {
                lock (m_updateAgentQueue)
                {
                    // save the position
                    AgentPosition lastpos = pos;

                    pos = m_updateAgentQueue[uri];

                    // this is true if no one put a new
                    // update in the map since the last
                    // one we processed, if thats the
                    // case then we are done
                    if (pos == lastpos)
                    {
                        m_updateAgentQueue.Remove(uri);
                        return true;
                    }
                }

                EntityTransferContext ctx = new EntityTransferContext(); // Dummy, not needed for position
                success = UpdateAgent(destination, (IAgentData)pos, ctx, 10000);
            }
            // we get here iff success == false
            // blacklist sim for 2 minutes
            lock (m_updateAgentQueue)
            {
                _failedSims.AddOrUpdate(destination.ServerURI, true, 120);
                m_updateAgentQueue.Remove(uri);
            }
            return false;
        }

        /// <summary>
        /// This is the worker function to send AgentData to a neighbor region
        /// </summary>
        private bool UpdateAgent(GridRegion destination, IAgentData cAgentData, EntityTransferContext ctx, int timeout)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: UpdateAgent in {0}", destination.ServerURI);

            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + cAgentData.AgentID + "/";

            try
            {
                OSDMap args = cAgentData.Pack(ctx);

                args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
                args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
                args["destination_name"] = OSD.FromString(destination.RegionName);
                args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
                args["context"] = ctx.Pack();

                OSDMap result = WebUtil.PutToServiceCompressed(uri, args, timeout);
                if (result["Success"].AsBoolean())
                    return true;

                result = WebUtil.PutToService(uri, args, timeout);

                return result["Success"].AsBoolean();
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: UpdateAgent failed with exception: " + e.ToString());
            }

            return false;
        }


        public bool QueryAccess(GridRegion destination, UUID agentID, string agentHomeURI, bool viaTeleport, Vector3 position, List<UUID> featuresAvailable, EntityTransferContext ctx, out string reason)
        {
            reason = "Failed to contact destination";

            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: QueryAccess start, position={0}", position);

            IPEndPoint ext = destination.ExternalEndPoint;
            if (ext == null) return false;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + agentID + "/" + destination.RegionID.ToString() + "/";

            OSDMap request = new OSDMap();
            request.Add("viaTeleport", OSD.FromBoolean(viaTeleport));
            request.Add("position", OSD.FromString(position.ToString()));
            // To those who still understad this field, we're telling them 
            // the lowest version just to be safe
            request.Add("my_version", OSD.FromString(String.Format("SIMULATION/{0}", VersionInfo.SimulationServiceVersionSupportedMin)));
            // New simulation service negotiation
            request.Add("simulation_service_supported_min", OSD.FromReal(VersionInfo.SimulationServiceVersionSupportedMin));
            request.Add("simulation_service_supported_max", OSD.FromReal(VersionInfo.SimulationServiceVersionSupportedMax));
            request.Add("simulation_service_accepted_min", OSD.FromReal(VersionInfo.SimulationServiceVersionAcceptedMin));
            request.Add("simulation_service_accepted_max", OSD.FromReal(VersionInfo.SimulationServiceVersionAcceptedMax));

            request.Add("context", ctx.Pack());

            OSDArray features = new OSDArray();
            foreach (UUID feature in featuresAvailable)
                features.Add(OSD.FromString(feature.ToString()));

            request.Add("features", features);

            if (agentHomeURI != null)
                request.Add("agent_home_uri", OSD.FromString(agentHomeURI));

            try
            {
                OSDMap result = WebUtil.ServiceOSDRequest(uri, request, "QUERYACCESS", 30000, false, false);
                bool success = result["success"].AsBoolean();
                if (result.ContainsKey("_Result"))
                {
                    OSDMap data = (OSDMap)result["_Result"];

                    // FIXME: If there is a _Result map then it's the success key here that indicates the true success
                    // or failure, not the sibling result node.
                    success = data["success"];

                    reason = data["reason"].AsString();
                    // We will need to plumb this and start sing the outbound version as well
                    // TODO: lay the pipe for version plumbing
                    if (data.ContainsKey("negotiated_inbound_version") && data["negotiated_inbound_version"] != null)
                    {
                        ctx.InboundVersion = (float)data["negotiated_inbound_version"].AsReal();
                        ctx.OutboundVersion = (float)data["negotiated_outbound_version"].AsReal();
                    }
                    else if (data["version"] != null && data["version"].AsString() != string.Empty)
                    {
                        string versionString = data["version"].AsString();
                        String[] parts = versionString.Split(new char[] {'/'});
                        if (parts.Length > 1)
                        {
                            ctx.InboundVersion = float.Parse(parts[1]);
                            ctx.OutboundVersion = float.Parse(parts[1]);
                        }
                    }

                    m_log.DebugFormat(
                        "[REMOTE SIMULATION CONNECTOR]: QueryAccess to {0} returned {1}, reason {2}, version {3}/{4}",
                        uri, success, reason, ctx.InboundVersion, ctx.OutboundVersion);
                }

                if (!success || ctx.InboundVersion == 0f || ctx.OutboundVersion == 0f)
                {
                    // If we don't check this then OpenSimulator 0.7.3.1 and some period before will never see the
                    // actual failure message
                    if (!result.ContainsKey("_Result"))
                    {
                        if (result.ContainsKey("Message"))
                        {
                            string message = result["Message"].AsString();
                            if (message == "Service request failed: [MethodNotAllowed] MethodNotAllowed") // Old style region
                            {
                                m_log.Info("[REMOTE SIMULATION CONNECTOR]: The above web util error was caused by a TP to a sim that doesn't support QUERYACCESS and can be ignored");
                                return true;
                            }
    
                            reason = result["Message"];
                        }
                        else
                        {
                            reason = "Communications failure";
                        }
                    }

                    return false;
                }

                featuresAvailable.Clear();

                if (result.ContainsKey("features"))
                {
                    OSDArray array = (OSDArray)result["features"];

                    foreach (OSD o in array)
                        featuresAvailable.Add(new UUID(o.AsString()));
                }

                // Version stuff
                if (ctx.OutboundVersion < 0.4)
                    ctx.WearablesCount = AvatarWearable.LEGACY_VERSION_MAX_WEARABLES;

                return success;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] QueryAcesss failed with exception; {0}",e.ToString());
            }
            
            return false;
        }

        /// <summary>
        /// </summary>
        public bool ReleaseAgent(UUID origin, UUID id, string uri)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: ReleaseAgent start");

            try
            {
                WebUtil.ServiceOSDRequest(uri, null, "DELETE", 10000, false, false);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] ReleaseAgent failed with exception; {0}",e.ToString());
            }
            
            return true;
        }

        /// <summary>
        /// </summary>
        public bool CloseAgent(GridRegion destination, UUID id, string auth_code)
        {
            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/?auth=" + auth_code;
            m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: CloseAgent {0}", uri);

            try
            {
                WebUtil.ServiceOSDRequest(uri, null, "DELETE", 10000, false, false);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] CloseAgent failed with exception; {0}",e.ToString());
            }

            return true;
        }

        #endregion Agents

        #region Objects

        protected virtual string ObjectPath()
        {
            return "object/";
        }

        /// <summary>
        ///
        /// </summary>
        public bool CreateObject(GridRegion destination, Vector3 newPosition, ISceneObject sog, bool isLocalCall)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: CreateObject start");

            string uri = destination.ServerURI + ObjectPath() + sog.UUID + "/";

            try
            {
                OSDMap args = new OSDMap(2);

                args["sog"] = OSD.FromString(sog.ToXml2());
                args["extra"] = OSD.FromString(sog.ExtraToXmlString());
                args["modified"] = OSD.FromBoolean(sog.HasGroupChanged);
                args["new_position"] = newPosition.ToString();

                string state = sog.GetStateSnapshot();
                if (state.Length > 0)
                    args["state"] = OSD.FromString(state);

                // Add the input general arguments
                args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
                args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
                args["destination_name"] = OSD.FromString(destination.RegionName);
                args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());

                OSDMap result = WebUtil.PostToService(uri, args, 40000, false);

                if (result == null)
                    return false;
                bool success = result["success"].AsBoolean();
                if (!success)
                    return false;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] CreateObject failed with exception; {0}",e.ToString());
                return false;
            }

            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public bool CreateObject(GridRegion destination, UUID userID, UUID itemID)
        {
            // TODO, not that urgent
            return false;
        }

        #endregion Objects
    }
}
