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

using System.Collections.Generic;
using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class RegionCommsListener : IRegionCommsListener
    {
        public event ExpectUserDelegate OnExpectUser;
        public event ExpectPrimDelegate OnExpectPrim;
        public event GenericCall2 OnExpectChildAgent;
        public event AgentCrossing OnAvatarCrossingIntoRegion;
        public event PrimCrossing OnPrimCrossingIntoRegion;
        public event UpdateNeighbours OnNeighboursUpdate;
        public event AcknowledgeAgentCross OnAcknowledgeAgentCrossed;
        public event AcknowledgePrimCross OnAcknowledgePrimCrossed;
        public event CloseAgentConnection OnCloseAgentConnection;
        public event RegionUp OnRegionUp;
        public event ChildAgentUpdate OnChildAgentUpdate;

        private ExpectUserDelegate handler001 = null; // OnExpectUser
        private ExpectPrimDelegate handler002 = null; // OnExpectPrim;
        private GenericCall2 handler003 = null; // OnExpectChildAgent;
        private AgentCrossing handler004 = null; // OnAvatarCrossingIntoRegion;
        private PrimCrossing handler005 = null; // OnPrimCrossingIntoRegion;
        private UpdateNeighbours handler006 = null; // OnNeighboursUpdate;
        private AcknowledgeAgentCross handler007 = null; // OnAcknowledgeAgentCrossed;
        private AcknowledgePrimCross handler008 = null; // OnAcknowledgePrimCrossed;
        private CloseAgentConnection handler009 = null; // OnCloseAgentConnection;
        private RegionUp handlerRegionUp = null; // OnRegionUp;
        private ChildAgentUpdate handlerChildAgentUpdate = null; // OnChildAgentUpdate;

        public string debugRegionName = String.Empty;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public virtual bool TriggerExpectUser(ulong regionHandle, AgentCircuitData agent)
        {
            handler001 = OnExpectUser;
            if (handler001 != null)
            {
                handler001(regionHandle, agent);
                return true;
            }

            return false;
        }


        public virtual bool TriggerExpectPrim(ulong regionHandle, LLUUID primID, string objData)
        {
            handler002 = OnExpectPrim;
            if (handler002 != null)
            {
                handler002(regionHandle, primID, objData);
                return true;
            }
            return false;
        }

        public virtual bool TriggerRegionUp(RegionInfo region)
        {
            handlerRegionUp = OnRegionUp;
            if (handlerRegionUp != null)
            {
                handlerRegionUp(region);
                return true;
            }
            return false;
        }

        public virtual bool TriggerChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            handlerChildAgentUpdate = OnChildAgentUpdate;
            if (handlerChildAgentUpdate != null)
            {
                handlerChildAgentUpdate(regionHandle, cAgentData);
                return true;
            }
            return false;
        }

        public virtual bool TriggerExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position,
                                                        bool isFlying)
        {
            handler004 = OnAvatarCrossingIntoRegion;
            if (handler004 != null)
            {
                handler004(regionHandle, agentID, position, isFlying);
                return true;
            }
            return false;
        }

        public virtual bool TriggerExpectPrimCrossing(ulong regionHandle, LLUUID primID, LLVector3 position,
                                                      bool isPhysical)
        {
            handler005 = OnPrimCrossingIntoRegion;
            if (handler005 != null)
            {
                handler005(regionHandle, primID, position, isPhysical);
                return true;
            }
            return false;
        }

        public virtual bool TriggerAcknowledgeAgentCrossed(ulong regionHandle, LLUUID agentID)
        {
            handler007 = OnAcknowledgeAgentCrossed;
            if (handler007 != null)
            {
                handler007(regionHandle, agentID);
                return true;
            }
            return false;
        }

        public virtual bool TriggerAcknowledgePrimCrossed(ulong regionHandle, LLUUID primID)
        {
            handler008 = OnAcknowledgePrimCrossed;
            if (handler008 != null)
            {
                handler008(regionHandle, primID);
                return true;
            }
            return false;
        }

        public virtual bool TriggerCloseAgentConnection(ulong regionHandle, LLUUID agentID)
        {
            handler009 = OnCloseAgentConnection;
            if (handler009 != null)
            {
                handler009(regionHandle, agentID);
                return true;

            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>TODO: Doesnt take any args??</remarks>
        /// <returns></returns>
        public virtual bool TriggerExpectChildAgent()
        {
            handler003 = OnExpectChildAgent;
            if (handler003 != null)
            {
                handler003();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Added to avoid a unused compiler warning on OnNeighboursUpdate, TODO: Check me</remarks>
        /// <param name="neighbours"></param>
        /// <returns></returns>
        public virtual bool TriggerOnNeighboursUpdate(List<RegionInfo> neighbours)
        {
            handler006 = OnNeighboursUpdate;
            if (handler006 != null)
            {
                handler006(neighbours);
                return true;
            }

            return false;
        }

        public bool TriggerTellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {
            handler009 = OnCloseAgentConnection;
            if (handler009 != null)
                return handler009(regionHandle, agentID);

            return false;
        }
    }
}