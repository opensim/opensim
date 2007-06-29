/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.Communications.Local
{

    public class LocalBackEndServices : IGridServices, IInterRegionCommunications
    {
        protected Dictionary<ulong, RegionInfo> regions = new Dictionary<ulong, RegionInfo>();
        protected Dictionary<ulong, RegionCommsListener> regionHosts = new Dictionary<ulong, RegionCommsListener>();

        public LocalBackEndServices()
        {

        }

        /// <summary>
        /// Register a region method with the BackEnd Services.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo, GridInfo gridInfo)
        {
            //Console.WriteLine("CommsManager - Region " + regionInfo.RegionHandle + " , " + regionInfo.RegionLocX + " , "+ regionInfo.RegionLocY +" is registering");
            if (!this.regions.ContainsKey((uint)regionInfo.RegionHandle))
            {
                //Console.WriteLine("CommsManager - Adding Region " + regionInfo.RegionHandle );
                this.regions.Add(regionInfo.RegionHandle, regionInfo);
                RegionCommsListener regionHost = new RegionCommsListener();
                this.regionHosts.Add(regionInfo.RegionHandle, regionHost);

                return regionHost;
            }

            //already in our list of regions so for now lets return null
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            // Console.WriteLine("Finding Neighbours to " + regionInfo.RegionHandle);
            List<RegionInfo> neighbours = new List<RegionInfo>();

            foreach (RegionInfo reg in this.regions.Values)
            {
                // Console.WriteLine("CommsManager- RequestNeighbours() checking region " + reg.RegionLocX + " , "+ reg.RegionLocY);
                if (reg.RegionHandle != regionInfo.RegionHandle)
                {
                    //Console.WriteLine("CommsManager- RequestNeighbours() - found a different region in list, checking location");
                    if ((reg.RegionLocX > (regionInfo.RegionLocX - 2)) && (reg.RegionLocX < (regionInfo.RegionLocX + 2)))
                    {
                        if ((reg.RegionLocY > (regionInfo.RegionLocY - 2)) && (reg.RegionLocY < (regionInfo.RegionLocY + 2)))
                        {
                            neighbours.Add(reg);
                        }
                    }
                }
            }
            return neighbours;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            if (this.regions.ContainsKey(regionHandle))
            {
                return this.regions[regionHandle];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            foreach(RegionInfo regInfo in this.regions.Values)
            {
                if (((regInfo.RegionLocX >= minX) && (regInfo.RegionLocX <= maxX)) && ((regInfo.RegionLocY >= minY) && (regInfo.RegionLocY <= maxY)))
                {
                    MapBlockData map = new MapBlockData();
                    map.Name = regInfo.RegionName;
                    map.X = (ushort)regInfo.RegionLocX;
                    map.Y = (ushort)regInfo.RegionLocY;
                    map.WaterHeight =(byte) regInfo.estateSettings.waterHeight;
                    map.MapImageId = regInfo.estateSettings.terrainImageID; //new LLUUID("00000000-0000-0000-9999-000000000007");
                    map.Agents = 1;
                    map.RegionFlags = 72458694;
                    map.Access = 13;
                    mapBlocks.Add(map);
                }
            }
            return mapBlocks;
        }

        /// <summary>
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            //Console.WriteLine("CommsManager- Trying to Inform a region to expect child agent");
            if (this.regionHosts.ContainsKey(regionHandle))
            {
                // Console.WriteLine("CommsManager- Informing a region to expect child agent");
                this.regionHosts[regionHandle].TriggerExpectUser(regionHandle, agentData);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool ExpectAvatarCrossing(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
        {
            if (this.regionHosts.ContainsKey(regionHandle))
            {
                // Console.WriteLine("CommsManager- Informing a region to expect avatar crossing");
                this.regionHosts[regionHandle].TriggerExpectAvatarCrossing(regionHandle, agentID, position);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Is a Sandbox mode method, used by the local Login server to inform a region of a connection user/session
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="loginData"></param>
        /// <returns></returns>
        public bool AddNewSession(ulong regionHandle, Login loginData)
        {
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = loginData.Agent;
            agent.firstname = loginData.First;
            agent.lastname = loginData.Last;
            agent.SessionID = loginData.Session;
            agent.SecureSessionID = loginData.SecureSession;
            agent.circuitcode = loginData.CircuitCode;
            agent.BaseFolder = loginData.BaseFolder;
            agent.InventoryFolder = loginData.InventoryFolder;
            agent.startpos = new LLVector3(128, 128, 70);
            agent.CapsPath = loginData.CapsPath;

            if (this.regionHosts.ContainsKey(regionHandle))
            {
                this.regionHosts[regionHandle].TriggerExpectUser(regionHandle, agent);
                return true;
            }

            // region not found
            return false;
        }
    }
}

