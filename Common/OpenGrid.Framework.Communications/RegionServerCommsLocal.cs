using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsLocal : RegionServerCommsManager
    {
        protected Dictionary<ulong, RegionInfo> regions = new Dictionary<ulong, RegionInfo>();
        protected Dictionary<ulong, RegionCommsHostBase> regionHosts = new Dictionary<ulong, RegionCommsHostBase>();

        public RegionServerCommsLocal()
        {
            userServer = new UserServer.UserCommsManagerLocal(); //Local User Server
            gridServer = new GridServer.GridCommsManagerLocal(); //Locl Grid Server
        }

        /// <summary>
        /// Main Register a region method with the CommsManager.
        /// Should do anything that is needed and also call the RegisterRegion method in the gridserver class
        /// to inform the grid server (in grid mode).
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            //Console.WriteLine("CommsManager - Region " + regionInfo.RegionHandle + " , " + regionInfo.RegionLocX + " , "+ regionInfo.RegionLocY +" is registering");
            if (!this.regions.ContainsKey((uint)regionInfo.RegionHandle))
            {
                //Console.WriteLine("CommsManager - Adding Region " + regionInfo.RegionHandle );
                this.regions.Add(regionInfo.RegionHandle, regionInfo);
                RegionCommsHostBase regionHost = new RegionCommsHostBase();
                this.regionHosts.Add(regionInfo.RegionHandle, regionHost);

                return regionHost;
            }

            //already in our list of regions so for now lets return null
            return null;
        }

        /// <summary>
        /// In the current class structure this shouldn't be here as it should only be in the gridserver class
        /// but having it there in sandbox mode makes things very difficult, so for now until something is sorted out 
        /// it will have to be here as well
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
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
        /// informs a neighbouring sim to expect a child agent
        /// I guess if we are going to stick with the current class structure then we need a intersim class
        /// but think we need to really rethink the class structure as currently it makes things very messy for sandbox mode 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
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
        /// Is a Sandbox mode method, used by the local Login server to inform a region of a connection user/session
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="loginData"></param>
        /// <returns></returns>
        public bool AddNewSession(ulong regionHandle, Login loginData)
        {
            //Console.WriteLine(" comms manager been told to expect new user");
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
