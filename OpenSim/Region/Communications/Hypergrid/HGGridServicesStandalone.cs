/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Security.Authentication;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment.Scenes;

using OpenMetaverse;
using Nwc.XmlRpc;
using log4net;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGGridServicesStandalone : HGGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Encapsulate local backend services for manipulation of local regions
        /// </summary>
        protected LocalBackEndServices m_localBackend = new LocalBackEndServices();

        private Dictionary<ulong, int> m_deadRegionCache = new Dictionary<ulong, int>();

        public LocalBackEndServices LocalBackend
        {
            get { return m_localBackend; }
        }

        public override string gdebugRegionName
        {
            get { return m_localBackend.gdebugRegionName; }
            set { m_localBackend.gdebugRegionName = value; }
        }

        public override bool RegionLoginsEnabled
        {
            get { return m_localBackend.RegionLoginsEnabled; }
            set { m_localBackend.RegionLoginsEnabled = value; }
        }      


        public HGGridServicesStandalone(NetworkServersInfo servers_info, BaseHttpServer httpServe, AssetCache asscache, SceneManager sman)
            : base(servers_info, httpServe, asscache, sman)
        {
            //Respond to Grid Services requests
            httpServer.AddXmlRPCHandler("logoff_user", LogOffUser);
            httpServer.AddXmlRPCHandler("check", PingCheckReply);
            httpServer.AddXmlRPCHandler("land_data", LandData);

            StartRemoting();
        }

        #region IGridServices interface

        public override RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            if (!regionInfo.RegionID.Equals(UUID.Zero))
            {
                m_regionsOnInstance.Add(regionInfo);
                return m_localBackend.RegisterRegion(regionInfo);
            }
            else
                return base.RegisterRegion(regionInfo);

        }

        public override bool DeregisterRegion(RegionInfo regionInfo)
        {
            bool success = m_localBackend.DeregisterRegion(regionInfo);
            if (!success)
                success = base.DeregisterRegion(regionInfo);
            return success;
        }

        public override List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            List<SimpleRegionInfo> neighbours = m_localBackend.RequestNeighbours(x, y);
            List<SimpleRegionInfo> remotes = base.RequestNeighbours(x, y);
            neighbours.AddRange(remotes);
    
            return neighbours;
        }

        public override RegionInfo RequestNeighbourInfo(UUID Region_UUID)
        {
            RegionInfo info = m_localBackend.RequestNeighbourInfo(Region_UUID);
            if (info == null)
                info = base.RequestNeighbourInfo(Region_UUID);
            return info;
        }

        public override RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            RegionInfo info = m_localBackend.RequestNeighbourInfo(regionHandle);
            //m_log.Info("[HGrid] Request neighbor info, local backend returned " + info);
            if (info == null)
                info = base.RequestNeighbourInfo(regionHandle);
            return info;
        }

        public override RegionInfo RequestClosestRegion(string regionName)
        {
            RegionInfo info = m_localBackend.RequestClosestRegion(regionName);
            if (info == null)
                info = base.RequestClosestRegion(regionName);
            return info;
        }

        public override List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            //m_log.Info("[HGrid] Request map blocks " + minX + "-" + minY + "-" + maxX + "-" + maxY);
            List<MapBlockData> neighbours = m_localBackend.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            List<MapBlockData> remotes = base.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            neighbours.AddRange(remotes);

            return neighbours;
        }

        public override LandData RequestLandData(ulong regionHandle, uint x, uint y)
        {
            LandData land = m_localBackend.RequestLandData(regionHandle, x, y);
            if (land == null)
                land = base.RequestLandData(regionHandle, x, y);
            return land;
        }

        public override List<RegionInfo> RequestNamedRegions(string name, int maxNumber)
        {
            List<RegionInfo> infos = m_localBackend.RequestNamedRegions(name, maxNumber);
            List<RegionInfo> remotes = base.RequestNamedRegions(name, maxNumber);
            infos.AddRange(remotes);
            return infos;
        }

        #endregion 

        #region XML Request Handlers

        /// <summary>
        /// A ping / version check
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public virtual XmlRpcResponse PingCheckReply(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable respData = new Hashtable();
            respData["online"] = "true";

            m_localBackend.PingCheckReply(respData);

            response.Value = respData;

            return response;
        }


        // Grid Request Processing
        /// <summary>
        /// Ooops, our Agent must be dead if we're getting this request!
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LogOffUser(XmlRpcRequest request)
        {
            m_log.Debug("[HGrid]: LogOff User Called");

            Hashtable requestData = (Hashtable)request.Params[0];
            string message = (string)requestData["message"];
            UUID agentID = UUID.Zero;
            UUID RegionSecret = UUID.Zero;
            UUID.TryParse((string)requestData["agent_id"], out agentID);
            UUID.TryParse((string)requestData["region_secret"], out RegionSecret);

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            m_localBackend.TriggerLogOffUser(regionHandle, agentID, RegionSecret, message);

            return new XmlRpcResponse();
        }

        /// <summary>
        /// Someone asked us about parcel-information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LandData(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            ulong regionHandle = Convert.ToUInt64(requestData["region_handle"]);
            uint x = Convert.ToUInt32(requestData["x"]);
            uint y = Convert.ToUInt32(requestData["y"]);
            m_log.DebugFormat("[HGrid]: Got XML reqeuest for land data at {0}, {1} in region {2}", x, y, regionHandle);

            LandData landData = m_localBackend.RequestLandData(regionHandle, x, y);
            Hashtable hash = new Hashtable();
            if (landData != null)
            {
                // for now, only push out the data we need for answering a ParcelInfoReqeust
                hash["AABBMax"] = landData.AABBMax.ToString();
                hash["AABBMin"] = landData.AABBMin.ToString();
                hash["Area"] = landData.Area.ToString();
                hash["AuctionID"] = landData.AuctionID.ToString();
                hash["Description"] = landData.Description;
                hash["Flags"] = landData.Flags.ToString();
                hash["GlobalID"] = landData.GlobalID.ToString();
                hash["Name"] = landData.Name;
                hash["OwnerID"] = landData.OwnerID.ToString();
                hash["SalePrice"] = landData.SalePrice.ToString();
                hash["SnapshotID"] = landData.SnapshotID.ToString();
                hash["UserLocation"] = landData.UserLocation.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        #endregion

        #region Remoting

        /// <summary>
        /// Start listening for .net remoting calls from other regions.
        /// </summary>
        private void StartRemoting()
        {
            m_log.Info("[HGrid]: Start remoting...");
            TcpChannel ch;
            try
            {
                ch = new TcpChannel((int)NetworkServersInfo.RemotingListenerPort);
                ChannelServices.RegisterChannel(ch, false); // Disabled security as Mono doesn't support this.
            }
            catch (Exception ex)
            {
                m_log.Error("[HGrid]: Exception while attempting to listen on TCP port " + (int)NetworkServersInfo.RemotingListenerPort + ".");
                throw (ex);
            }

            WellKnownServiceTypeEntry wellType =
                new WellKnownServiceTypeEntry(typeof(OGS1InterRegionRemoting), "InterRegions",
                                              WellKnownObjectMode.Singleton);
            RemotingConfiguration.RegisterWellKnownServiceType(wellType);
            InterRegionSingleton.Instance.OnArrival += TriggerExpectAvatarCrossing;
            InterRegionSingleton.Instance.OnChildAgent += IncomingChildAgent;
            InterRegionSingleton.Instance.OnPrimGroupArrival += IncomingPrim;
            InterRegionSingleton.Instance.OnPrimGroupNear += TriggerExpectPrimCrossing;
            InterRegionSingleton.Instance.OnRegionUp += TriggerRegionUp;
            InterRegionSingleton.Instance.OnChildAgentUpdate += TriggerChildAgentUpdate;
            InterRegionSingleton.Instance.OnTellRegionToCloseChildConnection += TriggerTellRegionToCloseChildConnection;
        }


        #endregion

        #region IInterRegionCommunications interface (Methods called by regions in this instance)

        public override bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            int failures = 0;
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionHandle))
                {
                    failures = m_deadRegionCache[regionHandle];
                }
            }
            if (failures <= 3)
            {
                RegionInfo regInfo = null;
                try
                {
                    if (m_localBackend.ChildAgentUpdate(regionHandle, cAgentData))
                    {
                        return true;
                    }

                    regInfo = RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                            typeof(OGS1InterRegionRemoting),
                            "tcp://" + regInfo.RemotingAddress +
                            ":" + regInfo.RemotingPort +
                            "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.ChildAgentUpdate(regionHandle, cAgentData);
                        }
                        else
                        {
                            m_log.Warn("[HGrid]: remoting object not found");
                        }
                        remObject = null;

                        return retValue;
                    }
                    NoteDeadRegion(regionHandle);

                    return false;
                }
                catch (RemotingException e)
                {
                    NoteDeadRegion(regionHandle);

                    m_log.WarnFormat(
                        "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                    return false;
                }
                catch (SocketException e)
                {
                    NoteDeadRegion(regionHandle);

                    m_log.WarnFormat(
                        "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                    return false;
                }
                catch (InvalidCredentialException e)
                {
                    NoteDeadRegion(regionHandle);

                    m_log.WarnFormat(
                        "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                    return false;
                }
                catch (AuthenticationException e)
                {
                    NoteDeadRegion(regionHandle);

                    m_log.WarnFormat(
                        "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                    return false;
                }
                catch (Exception e)
                {
                    NoteDeadRegion(regionHandle);

                    m_log.WarnFormat("[HGrid]: Unable to connect to adjacent region: {0} {1},{2}",
                                     regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                    return false;
                }
            }
            else
            {
                //m_log.Info("[INTERREGION]: Skipped Sending Child Update to a region because it failed too many times:" + regionHandle.ToString());
                return false;
            }
        }

        /// <summary>
        /// Inform a region that a child agent will be on the way from a client.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {

            if (m_localBackend.InformRegionOfChildAgent(regionHandle, agentData))
            {
                return true;
            }
            return base.InformRegionOfChildAgent(regionHandle, agentData);
        }

        // UGLY!
        public override bool RegionUp(SerializableRegionInfo region, ulong regionhandle)
        {
            if (m_localBackend.RegionUp(region, regionhandle))
                return true;
            return base.RegionUp(region, regionhandle);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformRegionOfPrimCrossing(ulong regionHandle, UUID primID, string objData, int XMLMethod)
        {
            int failures = 0;
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionHandle))
                {
                    failures = m_deadRegionCache[regionHandle];
                }
            }
            if (failures <= 1)
            {
                RegionInfo regInfo = null;
                try
                {
                    if (m_localBackend.InformRegionOfPrimCrossing(regionHandle, primID, objData, XMLMethod))
                    {
                        return true;
                    }

                    regInfo = RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;

                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                            typeof(OGS1InterRegionRemoting),
                            "tcp://" + regInfo.RemotingAddress +
                            ":" + regInfo.RemotingPort +
                            "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.InformRegionOfPrimCrossing(regionHandle, primID.Guid, objData, XMLMethod);
                        }
                        else
                        {
                            m_log.Warn("[HGrid]: Remoting object not found");
                        }
                        remObject = null;

                        return retValue;
                    }
                    NoteDeadRegion(regionHandle);
                    return false;
                }
                catch (RemotingException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (SocketException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (InvalidCredentialException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[HGrid]: Invalid Credential Exception: Invalid Credentials : " + regionHandle);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (AuthenticationException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[HGrid]: Authentication exception: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (Exception e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[HGrid]: Unknown exception: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[HGrid]: {0}", e);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public override bool ExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying)
        {

            RegionInfo[] regions = m_regionsOnInstance.ToArray();
            bool banned = false;
            bool localregion = false;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i] != null)
                {
                    if (regions[i].RegionHandle == regionHandle)
                    {
                        localregion = true;
                        if (regions[i].EstateSettings.IsBanned(agentID))
                        {
                            banned = true;
                            break;
                        }
                    }
                }
            }

            if (banned)
                return false;
            if (localregion)
                return m_localBackend.ExpectAvatarCrossing(regionHandle, agentID, position, isFlying);

            return base.ExpectAvatarCrossing(regionHandle, agentID, position, isFlying);

        }

        public override bool ExpectPrimCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isPhysical)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.TriggerExpectPrimCrossing(regionHandle, agentID, position, isPhysical))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                        typeof(OGS1InterRegionRemoting),
                        "tcp://" + regInfo.RemotingAddress +
                        ":" + regInfo.RemotingPort +
                        "/InterRegions");

                    if (remObject != null)
                    {
                        retValue =
                            remObject.ExpectAvatarCrossing(regionHandle, agentID.Guid, new sLLVector3(position),
                                                           isPhysical);
                    }
                    else
                    {
                        m_log.Warn("[HGrid]: Remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting
                // to inform it.
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (SocketException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Invalid Credential Exception: Invalid Credentials : " + regionHandle);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Authentication exception: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Unknown exception: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[HGrid]: {0}", e);
                return false;
            }
        }

        public override bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.TriggerTellRegionToCloseChildConnection(regionHandle, agentID))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    // bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                        typeof(OGS1InterRegionRemoting),
                        "tcp://" + regInfo.RemotingAddress +
                        ":" + regInfo.RemotingPort +
                        "/InterRegions");

                    if (remObject != null)
                    {
                        // retValue =
                        remObject.TellRegionToCloseChildConnection(regionHandle, agentID.Guid);
                    }
                    else
                    {
                        m_log.Warn("[HGrid]: Remoting object not found");
                    }
                    remObject = null;

                    return true;
                }
                //TODO need to see if we know about where this region is and use .net remoting
                // to inform it.
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region to tell it to close child agents: " + regInfo.RegionName +
                           " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                //m_log.Debug(e.ToString());
                return false;
            }
            catch (SocketException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGridS]: Socket Error: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                           " - Is this neighbor up?");
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Invalid Credentials: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: Authentication exception: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (WebException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[HGrid]: WebException exception: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                NoteDeadRegion(regionHandle);
                // This line errors with a Null Reference Exception..    Why?  @.@
                //m_log.Warn("Unknown exception: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                // ":" + regInfo.RemotingPort +
                //"/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - This is likely caused by an incompatibility in the protocol between this sim and that one");
                m_log.DebugFormat("[HGrid]: {0}", e);
                return false;
            }
        }

        public override bool AcknowledgeAgentCrossed(ulong regionHandle, UUID agentId)
        {
            return m_localBackend.AcknowledgeAgentCrossed(regionHandle, agentId);
        }

        public override bool AcknowledgePrimCrossed(ulong regionHandle, UUID primId)
        {
            return m_localBackend.AcknowledgePrimCrossed(regionHandle, primId);
        }

        #endregion

        #region Methods triggered by calls from external instances

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool IncomingChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            HGIncomingChildAgent(regionHandle, agentData);
     
            m_log.Info("[HGrid]: " + gdebugRegionName + ": Incoming HGrid Agent " + agentData.firstname + " " + agentData.lastname);

            return m_localBackend.IncomingChildAgent(regionHandle, agentData);
        }

        public bool TriggerRegionUp(RegionUpData regionData, ulong regionhandle)
        {
            m_log.Info(
               "[HGrid]: " +
               m_localBackend._gdebugRegionName + "Incoming HGrid RegionUpReport:  " + "(" + regionData.X +
               "," + regionData.Y + "). Giving this region a fresh set of 'dead' tries");

            RegionInfo nRegionInfo = new RegionInfo();
            nRegionInfo.SetEndPoint("127.0.0.1", regionData.PORT);
            nRegionInfo.ExternalHostName = regionData.IPADDR;
            nRegionInfo.RegionLocX = regionData.X;
            nRegionInfo.RegionLocY = regionData.Y;

            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(nRegionInfo.RegionHandle))
                {
                    m_deadRegionCache.Remove(nRegionInfo.RegionHandle);
                }
            }

            return m_localBackend.TriggerRegionUp(nRegionInfo, regionhandle);
        }

        public bool TriggerChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            //m_log.Info("[INTER]: Incoming HGrid Child Agent Data Update");

            return m_localBackend.TriggerChildAgentUpdate(regionHandle, cAgentData);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool IncomingPrim(ulong regionHandle, UUID primID, string objData, int XMLMethod)
        {
            m_localBackend.TriggerExpectPrim(regionHandle, primID, objData, XMLMethod);

            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool TriggerExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying)
        {
            return m_localBackend.TriggerExpectAvatarCrossing(regionHandle, agentID, position, isFlying);
        }

        public bool TriggerExpectPrimCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isPhysical)
        {
            return m_localBackend.TriggerExpectPrimCrossing(regionHandle, agentID, position, isPhysical);
        }

        public bool TriggerTellRegionToCloseChildConnection(ulong regionHandle, UUID agentID)
        {
            return m_localBackend.TriggerTellRegionToCloseChildConnection(regionHandle, agentID);
        }

        int timeOut = 10; //10 seconds
        /// <summary>
        /// Check that a region is available for TCP comms.  This is necessary for .NET remoting between regions.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="retry"></param>
        /// <returns></returns>
        public bool CheckRegion(string address, uint port, bool retry)
        {
            bool available = false;
            bool timed_out = true;

            IPAddress ia;
            IPAddress.TryParse(address, out ia);
            IPEndPoint m_EndPoint = new IPEndPoint(ia, (int)port);

            AsyncCallback callback = delegate(IAsyncResult iar)
            {
                Socket s = (Socket)iar.AsyncState;
                try
                {
                    s.EndConnect(iar);
                    available = true;
                    timed_out = false;
                }
                catch (Exception e)
                {
                    m_log.DebugFormat(
                        "[HGrid]: Callback EndConnect exception: {0}:{1}", e.Message, e.StackTrace);
                }

                s.Close();
            };

            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult ar = socket.BeginConnect(m_EndPoint, callback, socket);
                ar.AsyncWaitHandle.WaitOne(timeOut * 1000, false);
            }
            catch (Exception e)
            {
                m_log.DebugFormat(
                    "[HGrid]: CheckRegion Socket Setup exception: {0}:{1}", e.Message, e.StackTrace);

                return false;
            }

            if (timed_out)
            {
                m_log.DebugFormat(
                    "[HGrid]: socket [{0}] timed out ({1}) waiting to obtain a connection.",
                    m_EndPoint, timeOut * 1000);

                if (retry)
                {
                    return CheckRegion(address, port, false);
                }
            }

            return available;
        }

        public override bool CheckRegion(string address, uint port)
        {
            return CheckRegion(address, port, true);
        }

        public void NoteDeadRegion(ulong regionhandle)
        {
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionhandle))
                {
                    m_deadRegionCache[regionhandle] = m_deadRegionCache[regionhandle] + 1;
                }
                else
                {
                    m_deadRegionCache.Add(regionhandle, 1);
                }
            }
            
        }

        #endregion


    }
}
