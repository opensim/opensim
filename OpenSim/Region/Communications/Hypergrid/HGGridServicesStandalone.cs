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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Security.Authentication;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGGridServicesStandalone : HGGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Encapsulate local backend services for manipulation of local regions
        /// </summary>
        protected LocalBackEndServices m_localBackend = new LocalBackEndServices();

        //private Dictionary<ulong, int> m_deadRegionCache = new Dictionary<ulong, int>();

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


        public HGGridServicesStandalone(NetworkServersInfo servers_info, BaseHttpServer httpServe, IAssetCache asscache, SceneManager sman)
            : base(servers_info, httpServe, asscache, sman)
        {
            //Respond to Grid Services requests
            httpServer.AddXmlRPCHandler("logoff_user", LogOffUser);
            httpServer.AddXmlRPCHandler("check", PingCheckReply);
            httpServer.AddXmlRPCHandler("land_data", LandData);

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
            //List<SimpleRegionInfo> remotes = base.RequestNeighbours(x, y);
            //neighbours.AddRange(remotes);
    
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
        public virtual XmlRpcResponse PingCheckReply(XmlRpcRequest request, IPEndPoint remoteClient)
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
        public XmlRpcResponse LogOffUser(XmlRpcRequest request, IPEndPoint remoteClient)
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
        public XmlRpcResponse LandData(XmlRpcRequest request, IPEndPoint remoteClient)
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

    }
}
