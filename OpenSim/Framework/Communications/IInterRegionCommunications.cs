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
using OpenMetaverse;

namespace OpenSim.Framework.Communications
{
    public interface IInterRegionCommunications
    {
        string rdebugRegionName { get; set; }

        bool CheckRegion(string address, uint port);
        bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData);
        bool InformRegionOfPrimCrossing(ulong regionHandle, UUID primID, string objData, int XMLMethod);
        bool RegionUp(SerializableRegionInfo region, ulong regionhandle);
        bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData);

        bool ExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying);
        bool ExpectPrimCrossing(ulong regionHandle, UUID primID, Vector3 position, bool isFlying);

        bool AcknowledgeAgentCrossed(ulong regionHandle, UUID agentId);
        bool AcknowledgePrimCrossed(ulong regionHandle, UUID primID);

        bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID);

        /// <summary>
        /// Try to inform friends in the given region about online status of agent.
        /// </summary>
        /// <param name="agentId">
        /// The <see cref="UUID"/> of the agent.
        /// </param>
        /// <param name="destRegionHandle">
        /// The regionHandle of the region.
        /// </param>
        /// <param name="friends">
        /// A List of <see cref="UUID"/>s of friends to inform in the given region.
        /// </param>
        /// <param name="online">
        /// Is the agent online or offline
        /// </param>
        /// <returns>
        /// A list of friends that couldn't be reached on this region.
        /// </returns>
        List<UUID> InformFriendsInOtherRegion(UUID agentId, ulong destRegionHandle, List<UUID> friends, bool online);

        /// <summary>
        /// Send TerminateFriend of exFriendID to agent agentID in region regionHandle.
        /// </summary>
        /// <param name="regionHandle">
        /// The handle of the region agentID is in (hopefully).
        /// </param>
        /// <param name="agentID">
        /// The agent to send the packet to.
        /// </param>
        /// <param name="exFriendID">
        /// The ex-friends ID.
        /// </param>
        /// <returns>
        /// Whether the packet could be sent. False if the agent couldn't be found in the region.
        /// </returns>
        bool TriggerTerminateFriend(ulong regionHandle, UUID agentID, UUID exFriendID);
    }
}
