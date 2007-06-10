using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Types;

using libsecondlife;

namespace OpenGrid.Framework.Communications
{
    public class SandBoxManager
    {
        protected Dictionary<ulong, RegionInfo> regions = new Dictionary<ulong, RegionInfo>();
        protected Dictionary<ulong, RegionCommsHostBase> regionHosts = new Dictionary<ulong, RegionCommsHostBase>();

        public SandBoxManager()
        {

        }

        /// <summary>
        /// Register a region method with the SandBoxManager.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
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
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public  List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
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
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public  bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
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
