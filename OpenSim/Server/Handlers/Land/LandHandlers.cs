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
using Nwc.XmlRpc;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Land
{
    public class LandHandlers
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ILandService m_LocalService;

        public LandHandlers(ILandService service)
        {
            m_LocalService = service;
        }

        public XmlRpcResponse GetLandData(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            ulong regionHandle = Convert.ToUInt64(requestData["region_handle"]);
            uint x = Convert.ToUInt32(requestData["x"]);
            uint y = Convert.ToUInt32(requestData["y"]);
//            m_log.DebugFormat("[LAND HANDLER]: Got request for land data at {0}, {1} for region {2}", x, y, regionHandle);

            byte regionAccess;
            LandData landData = m_LocalService.GetLandData(UUID.Zero, regionHandle, x, y, out regionAccess);
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
                hash["RegionAccess"] = regionAccess.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }
    }

}
