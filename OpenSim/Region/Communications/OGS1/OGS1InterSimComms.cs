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
 */

using System;
using System.Reflection;
using System.Runtime.Remoting;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Region.Communications.OGS1
{
    public delegate bool InformRegionChild(ulong regionHandle, AgentCircuitData agentData);

    public delegate bool ExpectArrival(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying);

    public delegate bool InformRegionPrimGroup(ulong regionHandle, UUID primID, Vector3 Positon, bool isPhysical);

    public delegate bool PrimGroupArrival(ulong regionHandle, UUID primID, string objData, int XMLMethod);

    public delegate bool RegionUp(RegionUpData region, ulong regionhandle);

    public delegate bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate childUpdate);

    public delegate bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID);

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

        private InformRegionChild handlerChildAgent = null; // OnChildAgent;
        private ExpectArrival handlerArrival = null; // OnArrival;
        private InformRegionPrimGroup handlerPrimGroupNear = null; // OnPrimGroupNear;
        private PrimGroupArrival handlerPrimGroupArrival = null; // OnPrimGroupArrival;
        private RegionUp handlerRegionUp = null; // OnRegionUp;
        private ChildAgentUpdate handlerChildAgentUpdate = null; // OnChildAgentUpdate;
        private TellRegionToCloseChildConnection handlerTellRegionToCloseChildConnection = null; // OnTellRegionToCloseChildConnection;


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
            handlerChildAgent = OnChildAgent;
            if (handlerChildAgent != null)
            {
                return handlerChildAgent(regionHandle, agentData);
            }
            return false;
        }

        public bool RegionUp(RegionUpData sregion, ulong regionhandle)
        {
            handlerRegionUp = OnRegionUp;
            if (handlerRegionUp != null)
            {
                return handlerRegionUp(sregion, regionhandle);
            }
            return false;
        }

        public bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentUpdate)
        {
            handlerChildAgentUpdate = OnChildAgentUpdate;
            if (handlerChildAgentUpdate != null)
            {
                return handlerChildAgentUpdate(regionHandle, cAgentUpdate);
            }
            return false;
        }

        public bool ExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying)
        {
            handlerArrival = OnArrival;
            if (handlerArrival != null)
            {
                return handlerArrival(regionHandle, agentID, position, isFlying);
            }
            return false;
        }

        public bool InformRegionPrim(ulong regionHandle, UUID primID, Vector3 position, bool isPhysical)
        {
            handlerPrimGroupNear = OnPrimGroupNear;
            if (handlerPrimGroupNear != null)
            {
                return handlerPrimGroupNear(regionHandle, primID, position, isPhysical);
            }
            return false;
        }

        public bool ExpectPrimCrossing(ulong regionHandle, UUID primID, string objData, int XMLMethod)
        {
            handlerPrimGroupArrival = OnPrimGroupArrival;
            if (handlerPrimGroupArrival != null)
            {
                return handlerPrimGroupArrival(regionHandle, primID, objData, XMLMethod);
            }
            return false;
        }

        public bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID)
        {
            handlerTellRegionToCloseChildConnection = OnTellRegionToCloseChildConnection;
            if (handlerTellRegionToCloseChildConnection != null)
            {
                return handlerTellRegionToCloseChildConnection(regionHandle, agentID);
            }
            return false;
        }
    }

    public class OGS1InterRegionRemoting : MarshalByRefObject
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        public bool RegionUp(RegionUpData region, ulong regionhandle)
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


        public bool ExpectAvatarCrossing(ulong regionHandle, Guid agentID, Vector3 position, bool isFlying)
        {
            try
            {
                return
                    InterRegionSingleton.Instance.ExpectAvatarCrossing(regionHandle, new UUID(agentID),
                                                                       position,
                                                                       isFlying);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool InformRegionPrim(ulong regionHandle, Guid SceneObjectGroupID, Vector3 position, bool isPhysical)
        {
            try
            {
                return
                    InterRegionSingleton.Instance.InformRegionPrim(regionHandle, new UUID(SceneObjectGroupID),
                                                                   position,
                                                                   isPhysical);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

        public bool InformRegionOfPrimCrossing(ulong regionHandle, Guid primID, string objData, int XMLMethod)
        {
            try
            {
                return InterRegionSingleton.Instance.ExpectPrimCrossing(regionHandle, new UUID(primID), objData, XMLMethod);
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
                return InterRegionSingleton.Instance.TellRegionToCloseChildConnection(regionHandle, new UUID(agentID));
            }
            catch (RemotingException)
            {
                m_log.Info("[INTERREGION]: Remoting Error: Unable to connect to remote region: " + regionHandle.ToString());
                return false;
            }
        }
    }
}
