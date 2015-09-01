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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Land;

namespace OpenSim.Region.RegionCombinerModule
{
    public class RegionCombinerLargeLandChannel : ILandChannel
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RegionData RegData;
        private ILandChannel RootRegionLandChannel;
        private readonly List<RegionData> RegionConnections;
        
        #region ILandChannel Members

        public RegionCombinerLargeLandChannel(RegionData regData, ILandChannel rootRegionLandChannel,
                                              List<RegionData> regionConnections)
        {
            RegData = regData;
            RootRegionLandChannel = rootRegionLandChannel;
            RegionConnections = regionConnections;
        }

        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            //m_log.DebugFormat("[LANDPARCELNEARPOINT]: {0}>", position);
            return RootRegionLandChannel.ParcelsNearPoint(position - RegData.Offset);
        }

        public List<ILandObject> AllParcels()
        {
            return RootRegionLandChannel.AllParcels();
        }
        
        public void Clear(bool setupDefaultParcel)
        {
            RootRegionLandChannel.Clear(setupDefaultParcel);
        }

        public ILandObject GetLandObject(Vector3 position)
        {
            return GetLandObject(position.X, position.Y);
        }

        public ILandObject GetLandObject(int x, int y)
        {
            return GetLandObject((float)x, (float)y);

//            m_log.DebugFormat("[BIGLANDTESTINT]: <{0},{1}>", x, y);
//
//            if (x > 0 && x <= (int)Constants.RegionSize && y > 0 && y <= (int)Constants.RegionSize)
//            {
//                return RootRegionLandChannel.GetLandObject(x, y);
//            }
//            else
//            {
//                int offsetX = (x / (int)Constants.RegionSize);
//                int offsetY = (y / (int)Constants.RegionSize);
//                offsetX *= (int)Constants.RegionSize;
//                offsetY *= (int)Constants.RegionSize;
//
//                foreach (RegionData regionData in RegionConnections)
//                {
//                    if (regionData.Offset.X == offsetX && regionData.Offset.Y == offsetY)
//                    {
//                        m_log.DebugFormat(
//                            "[REGION COMBINER LARGE LAND CHANNEL]: Found region {0} at offset {1},{2}", 
//                            regionData.RegionScene.Name, offsetX, offsetY);
//
//                        return regionData.RegionScene.LandChannel.GetLandObject(x - offsetX, y - offsetY);
//                    }
//                }
//                //ILandObject obj = new LandObject(UUID.Zero, false, RegData.RegionScene);
//                //obj.LandData.Name = "NO LAND";
//                //return obj;
//            }
//
//            m_log.DebugFormat("[REGION COMBINER LARGE LAND CHANNEL]: No region found at {0},{1}, returning null", x, y);
//
//            return null;
        }

        public ILandObject GetLandObject(int localID)
        {
            // XXX: Possibly should be looking in every land channel, not just the root.
            return RootRegionLandChannel.GetLandObject(localID);
        }

        public ILandObject GetLandObject(float x, float y)
        {
//            m_log.DebugFormat("[BIGLANDTESTFLOAT]: <{0},{1}>", x, y);
            
            if (x > 0 && x <= (int)Constants.RegionSize && y > 0 && y <= (int)Constants.RegionSize)
            {
                return RootRegionLandChannel.GetLandObject(x, y);
            }
            else
            {
                int offsetX = (int)(x/(int) Constants.RegionSize);
                int offsetY = (int)(y/(int) Constants.RegionSize);
                offsetX *= (int) Constants.RegionSize;
                offsetY *= (int) Constants.RegionSize;

                foreach (RegionData regionData in RegionConnections)
                {
                    if (regionData.Offset.X == offsetX && regionData.Offset.Y == offsetY)
                    {
//                        m_log.DebugFormat(
//                            "[REGION COMBINER LARGE LAND CHANNEL]: Found region {0} at offset {1},{2}", 
//                            regionData.RegionScene.Name, offsetX, offsetY);

                        return regionData.RegionScene.LandChannel.GetLandObject(x - offsetX, y - offsetY);
                    }
                }

//                ILandObject obj = new LandObject(UUID.Zero, false, RegData.RegionScene);
//                obj.LandData.Name = "NO LAND";
//                return obj;
            }

//            m_log.DebugFormat("[REGION COMBINER LARGE LAND CHANNEL]: No region found at {0},{1}, returning null", x, y);

            return null;
        }

        public bool IsForcefulBansAllowed()
        {
            return RootRegionLandChannel.IsForcefulBansAllowed();
        }

        public void UpdateLandObject(int localID, LandData data)
        {
            RootRegionLandChannel.UpdateLandObject(localID, data);
        }

        public void Join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            RootRegionLandChannel.Join(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        public void Subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            RootRegionLandChannel.Subdivide(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            RootRegionLandChannel.ReturnObjectsInParcel(localID, returnType, agentIDs, taskIDs, remoteClient);
        }

        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            RootRegionLandChannel.setParcelObjectMaxOverride(overrideDel);
        }

        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            RootRegionLandChannel.setSimulatorObjectMaxOverride(overrideDel);
        }

        public void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            RootRegionLandChannel.SetParcelOtherCleanTime(remoteClient, localID, otherCleanTime);
        }

        public void sendClientInitialLandInfo(IClientAPI remoteClient) { }
        #endregion
    }
}