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

using System;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate ScenePresence CrossAgentToNewRegionDelegate(ScenePresence agent, Vector3 pos, GridRegion neighbourRegion, bool isFlying, string version);

    public interface IEntityTransferModule
    {
        /// <summary>
        /// Teleport an agent within the same or to a different region.
        /// </summary>
        /// <param name='agent'></param>
        /// <param name='regionHandle'>
        /// The handle of the destination region.  If it's the same as the region currently
        /// occupied by the agent then the teleport will be within that region.
        /// </param>
        /// <param name='agent'></param>
        /// <param name='regionHandle'></param>
        /// <param name='position'></param>
        /// <param name='lookAt'></param>
        /// <param name='teleportFlags'></param>
        void Teleport(ScenePresence agent, ulong regionHandle, Vector3 position, Vector3 lookAt, uint teleportFlags);

        /// <summary>
        /// Teleports the agent for the given client to their home destination.
        /// </summary>
        /// <param name='id'></param>
        /// <param name='client'></param>
        bool TeleportHome(UUID id, IClientAPI client);

        /// <summary>
        /// Teleport an agent directly to a given region without checking whether the region should be substituted.
        /// </summary>
        /// <remarks>
        /// Please use Teleport() instead unless you know exactly what you're doing.
        /// Do not use for same region teleports.
        /// </remarks>
        /// <param name='sp'></param>
        /// <param name='reg'></param>
        /// <param name='finalDestination'>/param>
        /// <param name='position'></param>
        /// <param name='lookAt'></param>
        /// <param name='teleportFlags'></param>
        void DoTeleport(ScenePresence sp, GridRegion reg, GridRegion finalDestination,
            Vector3 position, Vector3 lookAt, uint teleportFlags);

        /// <summary>
        /// Show whether the given agent is being teleported.
        /// </summary>
        /// <param name='id'>The agent ID</para></param>
        /// <returns>true if the agent is in the process of being teleported, false otherwise.</returns>
        bool IsInTransit(UUID id);        

        bool Cross(ScenePresence agent, bool isFlying);

        void AgentArrivedAtDestination(UUID agent);

        void EnableChildAgents(ScenePresence agent);

        void EnableChildAgent(ScenePresence agent, GridRegion region);

        GridRegion GetDestination(Scene scene, UUID agentID, Vector3 pos, out string version,
                                        out Vector3 newpos, out string reason);

        void Cross(SceneObjectGroup sog, Vector3 position, bool silent);

        ScenePresence CrossAgentToNewRegionAsync(ScenePresence agent, Vector3 pos, GridRegion neighbourRegion, bool isFlying, string version);

        bool HandleIncomingSceneObject(SceneObjectGroup so, Vector3 newPosition);
    }

    public interface IUserAgentVerificationModule
    {
        bool VerifyClient(AgentCircuitData aCircuit, string token);
    }
}
