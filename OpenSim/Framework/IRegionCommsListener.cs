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

using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public delegate void ExpectUserDelegate(AgentCircuitData agent);

    public delegate bool ExpectPrimDelegate(UUID primID, string objData, int XMLMethod);

    public delegate void UpdateNeighbours(List<RegionInfo> neighbours);

    public delegate void AgentCrossing(UUID agentID, Vector3 position, bool isFlying);

    public delegate void PrimCrossing(UUID primID, Vector3 position, bool isPhysical);

    public delegate void AcknowledgeAgentCross(UUID agentID);

    public delegate void AcknowledgePrimCross(UUID PrimID);

    public delegate bool CloseAgentConnection(UUID agentID);

    public delegate bool ChildAgentUpdate(ChildAgentDataUpdate cAgentData);

    public delegate void LogOffUser(UUID agentID, UUID regionSecret, string message);

    public delegate LandData GetLandData(uint x, uint y);

    public interface IRegionCommsListener
    {
        event ExpectUserDelegate OnExpectUser;
        event ExpectPrimDelegate OnExpectPrim;
        event GenericCall2 OnExpectChildAgent;
        event AgentCrossing OnAvatarCrossingIntoRegion;
        event PrimCrossing OnPrimCrossingIntoRegion;
        event AcknowledgeAgentCross OnAcknowledgeAgentCrossed;
        event AcknowledgePrimCross OnAcknowledgePrimCrossed;
        event UpdateNeighbours OnNeighboursUpdate;
        event CloseAgentConnection OnCloseAgentConnection;
        event ChildAgentUpdate OnChildAgentUpdate;
        event LogOffUser OnLogOffUser;
        event GetLandData OnGetLandData;
    }
}
