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

        //private GridRegion m_Region;

        public SimulationServiceConnector()
        {
        }

        public SimulationServiceConnector(IConfigSource config)
        {
            //m_Region = region;
        }

        public IScene GetScene(ulong regionHandle)
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

        /// <summary>
        /// 
        /// </summary>
        public bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: CreateAgent start");
            
            reason = String.Empty;
            if (destination == null)
            {
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Given destination is null");
                return false;
            }

            string uri = destination.ServerURI + AgentPath() + aCircuit.AgentID + "/";
            
            try
            {
                OSDMap args = aCircuit.PackAgentCircuitData();

                args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
                args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
                args["destination_name"] = OSD.FromString(destination.RegionName);
                args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
                args["teleport_flags"] = OSD.FromString(flags.ToString());

                OSDMap result = WebUtil.PostToService(uri,args);
                if (result["Success"].AsBoolean())
                    return true;
                
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
        public bool UpdateAgent(GridRegion destination, AgentData data)
        {
            return UpdateAgent(destination, (IAgentData)data);
        }

        /// <summary>
        /// Send updated position information about an agent in this region to a neighbor
        /// This operation may be called very frequently if an avatar is moving about in
        /// the region.
        /// </summary>
        public bool UpdateAgent(GridRegion destination, AgentPosition data)
        {
            // we need a better throttle for these
            // return false;
            
            return UpdateAgent(destination, (IAgentData)data);
        }

        /// <summary>
        /// This is the worker function to send AgentData to a neighbor region
        /// </summary>
        private bool UpdateAgent(GridRegion destination, IAgentData cAgentData)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: UpdateAgent start");

            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + cAgentData.AgentID + "/";

            try
            {
                OSDMap args = cAgentData.Pack();

                args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
                args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
                args["destination_name"] = OSD.FromString(destination.RegionName);
                args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());

                OSDMap result = WebUtil.PutToService(uri,args);
                return result["Success"].AsBoolean();
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: UpdateAgent failed with exception: " + e.ToString());
            }

            return false;
        }

        /// <summary>
        /// Not sure what sequence causes this function to be invoked. The only calling
        /// path is through the GET method 
        /// </summary>
        public bool RetrieveAgent(GridRegion destination, UUID id, out IAgentData agent)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: RetrieveAgent start");

            agent = null;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/";

            try
            {
                OSDMap result = WebUtil.GetFromService(uri);
                if (result["Success"].AsBoolean())
                {
                    // OSDMap args = Util.GetOSDMap(result["_RawResult"].AsString());
                    OSDMap args = (OSDMap)result["_Result"];
                    if (args != null)
                    {
                        agent = new CompleteAgentData();
                        agent.Unpack(args);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: UpdateAgent failed with exception: " + e.ToString());
            }

            return false;
        }

        /// <summary>
        /// </summary>
        public bool QueryAccess(GridRegion destination, UUID id)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: QueryAccess start");

            IPEndPoint ext = destination.ExternalEndPoint;
            if (ext == null) return false;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/";

            try
            {
                OSDMap result = WebUtil.ServiceOSDRequest(uri,null,"QUERYACCESS",10000);
                return result["Success"].AsBoolean();
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] QueryAcess failed with exception; {0}",e.ToString());
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
                OSDMap result = WebUtil.ServiceOSDRequest(uri,null,"DELETE",10000);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] ReleaseAgent failed with exception; {0}",e.ToString());
            }
            
            return true;
        }

        private bool CloseAgent(GridRegion destination, UUID id, bool ChildOnly)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: CloseAgent start");

            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/";

            try
            {
                OSDMap result = WebUtil.ServiceOSDRequest(uri,null,"DELETE",10000);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] CloseAgent failed with exception; {0}",e.ToString());
            }

            return true;
        }

        public bool CloseChildAgent(GridRegion destination, UUID id)
        {
            return CloseAgent(destination, id, true);
        }

        public bool CloseAgent(GridRegion destination, UUID id)
        {
            return CloseAgent(destination, id, false);
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
        public bool CreateObject(GridRegion destination, ISceneObject sog, bool isLocalCall)
        {
            // m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: CreateObject start");

            string uri = destination.ServerURI + ObjectPath() + sog.UUID + "/";

            try
            {
                OSDMap args = new OSDMap(2);

                args["sog"] = OSD.FromString(sog.ToXml2());
                args["extra"] = OSD.FromString(sog.ExtraToXmlString());
                args["modified"] = OSD.FromBoolean(sog.HasGroupChanged);

                string state = sog.GetStateSnapshot();
                if (state.Length > 0)
                    args["state"] = OSD.FromString(state);

                // Add the input general arguments
                args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
                args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
                args["destination_name"] = OSD.FromString(destination.RegionName);
                args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());

                OSDMap result = WebUtil.PostToService(uri,args);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR] CreateObject failed with exception; {0}",e.ToString());
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
