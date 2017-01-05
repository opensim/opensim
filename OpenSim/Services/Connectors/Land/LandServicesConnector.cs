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

using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Services.Interfaces;
using OpenMetaverse;
using Nwc.XmlRpc;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Connectors
{
    public class LandServicesConnector : ILandService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected IGridService m_GridService = null;

        public LandServicesConnector()
        {
        }

        public LandServicesConnector(IGridService gridServices)
        {
            Initialise(gridServices);
        }

        public virtual void Initialise(IGridService gridServices)
        {
            m_GridService = gridServices;
        }

        public virtual LandData GetLandData(UUID scopeID, ulong regionHandle, uint x, uint y, out byte regionAccess)
        {
            LandData landData = null;

            IList paramList = new ArrayList();
            regionAccess = 42; // Default to adult. Better safe...

            try
            {
                uint xpos = 0, ypos = 0;
                Util.RegionHandleToWorldLoc(regionHandle, out xpos, out ypos);

                GridRegion info = m_GridService.GetRegionByPosition(scopeID, (int)xpos, (int)ypos);
                if (info != null) // just to be sure
                {
                    string targetHandlestr = info.RegionHandle.ToString();
                    if( ypos == 0 ) //HG proxy?
                    {
                        // this is real region handle on hg proxies hack
                        targetHandlestr = info.RegionSecret;
                    }

                    Hashtable hash = new Hashtable();
                    hash["region_handle"] = targetHandlestr;
                    hash["x"] = x.ToString();
                    hash["y"] = y.ToString();
                    paramList.Add(hash);

                    XmlRpcRequest request = new XmlRpcRequest("land_data", paramList);
                    XmlRpcResponse response = request.Send(info.ServerURI, 10000);
                    if (response.IsFault)
                    {
                        m_log.ErrorFormat("[LAND CONNECTOR]: remote call returned an error: {0}", response.FaultString);
                    }
                    else
                    {
                        hash = (Hashtable)response.Value;
                        try
                        {
                            landData = new LandData();
                            landData.AABBMax = Vector3.Parse((string)hash["AABBMax"]);
                            landData.AABBMin = Vector3.Parse((string)hash["AABBMin"]);
                            landData.Area = Convert.ToInt32(hash["Area"]);
                            landData.AuctionID = Convert.ToUInt32(hash["AuctionID"]);
                            landData.Description = (string)hash["Description"];
                            landData.Flags = Convert.ToUInt32(hash["Flags"]);
                            landData.GlobalID = new UUID((string)hash["GlobalID"]);
                            landData.Name = (string)hash["Name"];
                            landData.OwnerID = new UUID((string)hash["OwnerID"]);
                            landData.SalePrice = Convert.ToInt32(hash["SalePrice"]);
                            landData.SnapshotID = new UUID((string)hash["SnapshotID"]);
                            landData.UserLocation = Vector3.Parse((string)hash["UserLocation"]);
                            if (hash["RegionAccess"] != null)
                                regionAccess = (byte)Convert.ToInt32((string)hash["RegionAccess"]);
                            m_log.DebugFormat("[LAND CONNECTOR]: Got land data for parcel {0}", landData.Name);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[LAND CONNECTOR]: Got exception while parsing land-data: {0} {1}",
                                e.Message, e.StackTrace);
                        }
                    }
                }
                else
                    m_log.WarnFormat("[LAND CONNECTOR]: Couldn't find region with handle {0}", regionHandle);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[LAND CONNECTOR]: Couldn't contact region {0}: {1} {2}", regionHandle, e.Message, e.StackTrace);
            }

            return landData;
        }
    }
}
