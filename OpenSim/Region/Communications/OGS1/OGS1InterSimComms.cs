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
*     * Neither the name of the OpenSim Project nor the
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
* 
*/

using System;
using System.Runtime.Remoting;
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Region.Communications.OGS1
{
    public delegate bool InformRegionChild(ulong regionHandle, AgentCircuitData agentData);

    public delegate bool ExpectArrival(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying);

    public delegate bool InformRegionPrimGroup(ulong regionHandle, LLUUID primID, LLVector3 Positon, bool isPhysical);

    public delegate bool PrimGroupArrival(ulong regionHandle, LLUUID primID, string objData);

    public delegate bool RegionUp(SearializableRegionInfo region, ulong regionhandle);

    public delegate bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate childUpdate);

    public delegate bool TellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID);

    public sealed class InterRegionSingleton
    {
        private static readonly InterRegionSingleton instance = new InterRegionSingleton();

        public event InformRegionChild OnChildAgent;
        public event ExpectArrival OnArrival;
        public event InformRegionPrimGroup OnPrimGroupNear;
        public event PrimGroupArrival OnPrimGroupArrival;
        public event RegionUp OnRegionUp;
        public event ChildAgentUpdate OnChildAgentUpdate;
        public event TellRegionToCloseChildConnection OnTellRegionToCloseChildConnection;



        static InterRegionSingleton()
        {
        }

        private InterRegionSingleton()
        {
        }

        public static InterRegionSingleton Instance
        {
            get { return instance; }
        }

        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            if (OnChildAgent != null)
            {
                return OnChildAgent(regionHandle, agentData);
            }
            return false;
        }

        public bool RegionUp(SearializableRegionInfo sregion, ulong regionhandle)
        {
            if (OnRegionUp != null)
            {
                return OnRegionUp(sregion, regionhandle);
            }
            return false;
        }

        public bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentUpdate)
        {
            if (OnChildAgentUpdate != null)
            {
                return OnChildAgentUpdate(regionHandle, cAgentUpdate);
            }
            return false;
        }

        public bool ExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (OnArrival != null)
            {
                return OnArrival(regionHandle, agentID, position, isFlying);
            }
            return false;
        }

        public bool InformRegionPrim(ulong regionHandle, LLUUID primID, LLVector3 position, bool isPhysical)
        {
            if (OnPrimGroupNear != null)
            {
                return OnPrimGroupNear(regionHandle, primID, position, isPhysical);
            }
            return false;
        }

        public bool ExpectPrimCrossing(ulong regionHandle, LLUUID primID, string objData)
        {
            if (OnPrimGroupArrival != null)
            {
                return OnPrimGroupArrival(regionHandle, primID, objData);
            }
            return false;
        }

        public bool TellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {
            if (OnTellRegionToCloseChildConnection != null)
            {

                return OnTellRegionToCloseChildConnection(regionHandle, agentID);
            }
            return false;
        }
    }

    public class OGS1InterRegionRemoting : MarshalByRefObject
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public OGS1InterRegionRemoting()
        {
        }

        public bool InformRegionOfChildAgent(ulong regionHandle, sAgentCircuitData agentData)
        {
            try
            {
                return
                    InterRegionSingleton.Instance.InformRegionOfChildAgent(regionHandle, new AgentCircuitData(agentData));
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool RegionUp(SearializableRegionInfo region, ulong regionhandle)
        {
            try
            {
                return InterRegionSingleton.Instance.RegionUp(region, regionhandle);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            try
            {
                return InterRegionSingleton.Instance.ChildAgentUpdate(regionHandle, cAgentData);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to send Child agent update to remote region.\n" + e.ToString());
                return false;
            }
        }


        public bool ExpectAvatarCrossing(ulong regionHandle, Guid agentID, sLLVector3 position, bool isFlying)
        {
            try
            {
                return
                    InterRegionSingleton.Instance.ExpectAvatarCrossing(regionHandle, new LLUUID(agentID),
                                                                       new LLVector3(position.x, position.y, position.z),
                                                                       isFlying);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool InformRegionPrim(ulong regionHandle, Guid SceneObjectGroupID, sLLVector3 position, bool isPhysical)
        {
            try
            {
                return
                    InterRegionSingleton.Instance.InformRegionPrim(regionHandle, new LLUUID(SceneObjectGroupID),
                                                                   new LLVector3(position.x, position.y, position.z),
                                                                   isPhysical);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool InformRegionOfPrimCrossing(ulong regionHandle, Guid primID, string objData)
        {
            try
            {
                return InterRegionSingleton.Instance.ExpectPrimCrossing(regionHandle, new LLUUID(primID), objData);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool TellRegionToCloseChildConnection(ulong regionHandle, Guid agentID)
        {
            try
            {
                return InterRegionSingleton.Instance.TellRegionToCloseChildConnection(regionHandle, new LLUUID(agentID));
            }
            catch (RemotingException)
            {
                m_log.Info("[INTERREGION]: Remoting Error: Unable to connect to remote region: " + regionHandle.ToString());
                return false;
            }
        }
    }
}
