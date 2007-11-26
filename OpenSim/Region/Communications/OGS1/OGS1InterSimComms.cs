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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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

    public delegate bool RegionUP (RegionInfo region);

    public sealed class InterRegionSingleton
    {
        private static readonly InterRegionSingleton instance = new InterRegionSingleton();

        public event InformRegionChild OnChildAgent;
        public event ExpectArrival OnArrival;
        public event InformRegionPrimGroup OnPrimGroupNear;
        public event PrimGroupArrival OnPrimGroupArrival;
        public event RegionUp OnRegionUp;


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

        public bool RegionUp(RegionInfo region)
        {
            if (OnRegionUp != null)
            {
                return OnRegionUp(region);
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
    }

    public class OGS1InterRegionRemoting : MarshalByRefObject
    {
        public OGS1InterRegionRemoting()
        {
        }

        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            try
            {
                return InterRegionSingleton.Instance.InformRegionOfChildAgent(regionHandle, agentData);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }
        public bool RegionUp(RegionInfo region)
        {
            try
            {
                return InterRegionSingleton.Instance.RegionUp(region);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }
        public bool ExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            try
            {
                return InterRegionSingleton.Instance.ExpectAvatarCrossing(regionHandle, agentID, position, isFlying);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }
        public bool InformRegionPrim(ulong regionHandle, LLUUID SceneObjectGroupID, LLVector3 position, bool isPhysical)
        {
            try
            {
                return InterRegionSingleton.Instance.InformRegionPrim(regionHandle, SceneObjectGroupID, position, isPhysical);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
            
        }
        public bool InformRegionOfPrimCrossing(ulong regionHandle,LLUUID primID, string objData)
        {
            try
            {
                return InterRegionSingleton.Instance.ExpectPrimCrossing(regionHandle, primID, objData);
            }
            catch (RemotingException e)
            {
                Console.WriteLine("Remoting Error: Unable to connect to remote region.\n" + e.ToString());
                return false;
            }
        }

    }
}