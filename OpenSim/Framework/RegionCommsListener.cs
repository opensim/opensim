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

        private ExpectUserDelegate handlerExpectUser = null; // OnExpectUser
        private ExpectPrimDelegate handlerExpectPrim = null; // OnExpectPrim;
        private GenericCall2 handlerExpectChildAgent = null; // OnExpectChildAgent;
        private AgentCrossing handlerAvatarCrossingIntoRegion = null; // OnAvatarCrossingIntoRegion;
        private PrimCrossing handlerPrimCrossingIntoRegion = null; // OnPrimCrossingIntoRegion;
        private UpdateNeighbours handlerNeighboursUpdate = null; // OnNeighboursUpdate;
        private AcknowledgeAgentCross handlerAcknowledgeAgentCrossed = null; // OnAcknowledgeAgentCrossed;
        private AcknowledgePrimCross handlerAcknowledgePrimCrossed = null; // OnAcknowledgePrimCrossed;
        private CloseAgentConnection handlerCloseAgentConnection = null; // OnCloseAgentConnection;
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
            handlerExpectUser = OnExpectUser;
            if (handlerExpectUser != null)
            {
                handlerExpectUser(regionHandle, agent);
                return true;
            }

            return false;
        }


        public virtual bool TriggerExpectPrim(ulong regionHandle, LLUUID primID, string objData)
        {
            handlerExpectPrim = OnExpectPrim;
            if (handlerExpectPrim != null)
            {
                handlerExpectPrim(regionHandle, primID, objData);
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
            handlerAvatarCrossingIntoRegion = OnAvatarCrossingIntoRegion;
            if (handlerAvatarCrossingIntoRegion != null)
            {
                handlerAvatarCrossingIntoRegion(regionHandle, agentID, position, isFlying);
                return true;
            }
            return false;
        }

        public virtual bool TriggerExpectPrimCrossing(ulong regionHandle, LLUUID primID, LLVector3 position,
                                                      bool isPhysical)
        {
            handlerPrimCrossingIntoRegion = OnPrimCrossingIntoRegion;
            if (handlerPrimCrossingIntoRegion != null)
            {
                handlerPrimCrossingIntoRegion(regionHandle, primID, position, isPhysical);
                return true;
            }
            return false;
        }

        public virtual bool TriggerAcknowledgeAgentCrossed(ulong regionHandle, LLUUID agentID)
        {
            handlerAcknowledgeAgentCrossed = OnAcknowledgeAgentCrossed;
            if (handlerAcknowledgeAgentCrossed != null)
            {
                handlerAcknowledgeAgentCrossed(regionHandle, agentID);
                return true;
            }
            return false;
        }

        public virtual bool TriggerAcknowledgePrimCrossed(ulong regionHandle, LLUUID primID)
        {
            handlerAcknowledgePrimCrossed = OnAcknowledgePrimCrossed;
            if (handlerAcknowledgePrimCrossed != null)
            {
                handlerAcknowledgePrimCrossed(regionHandle, primID);
                return true;
            }
            return false;
        }

        public virtual bool TriggerCloseAgentConnection(ulong regionHandle, LLUUID agentID)
        {
            handlerCloseAgentConnection = OnCloseAgentConnection;
            if (handlerCloseAgentConnection != null)
            {
                handlerCloseAgentConnection(regionHandle, agentID);
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
            handlerExpectChildAgent = OnExpectChildAgent;
            if (handlerExpectChildAgent != null)
            {
                handlerExpectChildAgent();
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
            handlerNeighboursUpdate = OnNeighboursUpdate;
            if (handlerNeighboursUpdate != null)
            {
                handlerNeighboursUpdate(neighbours);
                return true;
            }

            return false;
        }

        public bool TriggerTellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {
            handlerCloseAgentConnection = OnCloseAgentConnection;
            if (handlerCloseAgentConnection != null)
                return handlerCloseAgentConnection(regionHandle, agentID);

            return false;
        }
    }
}
