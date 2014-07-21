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
using OpenSim.Framework;
using OpenMetaverse;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Interfaces
{
    public interface ISimulationService
    {
        /// <summary>
        /// Retrieve the scene with the given region ID.
        /// </summary>
        /// <param name='regionId'>
        /// Region identifier.
        /// </param>
        /// <returns>
        /// The scene.
        /// </returns>
        IScene GetScene(UUID regionId);

        ISimulationService GetInnerService();

        #region Agents

        /// <summary>
        /// Ask the simulator hosting the destination to create an agent on that region.
        /// </summary>
        /// <param name="source">The region that the user is coming from. Will be null if the user
        /// logged-in directly, or arrived from a simulator that doesn't send this parameter.</param>
        /// <param name="destination"></param>
        /// <param name="aCircuit"></param>
        /// <param name="flags"></param>
        /// <param name="reason">Reason message in the event of a failure.</param>        
        bool CreateAgent(GridRegion source, GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason);

        /// <summary>
        /// Full child agent update.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool UpdateAgent(GridRegion destination, AgentData data);

        /// <summary>
        /// Short child agent update, mostly for position.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool UpdateAgent(GridRegion destination, AgentPosition data);

        /// <summary>
        /// Returns whether a propspective user is allowed to visit the region.
        /// </summary>
        /// <param name="destination">Desired destination</param>
        /// <param name="agentID">The visitor's User ID</param>
        /// <param name="agentHomeURI">The visitor's Home URI. Will be missing (null) in older OpenSims.</param>
        /// <param name="viaTeleport">True: via teleport; False: via cross (walking)</param>
        /// <param name="position">Position in the region</param>
        /// <param name="sversion">
        /// Version that the requesting simulator is runing.  If null then no version check is carried out.
        /// </param>
        /// <param name="version">Version that the target simulator is running</param>
        /// <param name="reason">[out] Optional error message</param>
        /// <returns>True: ok; False: not allowed</returns>
        bool QueryAccess(GridRegion destination, UUID agentID, string agentHomeURI, bool viaTeleport, Vector3 position, string sversion, out string version, out string reason);

        /// <summary>
        /// Message from receiving region to departing region, telling it got contacted by the client.
        /// When sent over REST, it invokes the opaque uri.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        bool ReleaseAgent(UUID originRegion, UUID id, string uri);

        /// <summary>
        /// Close agent.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CloseAgent(GridRegion destination, UUID id, string auth_token);

        #endregion Agents

        #region Objects

        /// <summary>
        /// Create an object in the destination region. This message is used primarily for prim crossing.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="sog"></param>
        /// <param name="isLocalCall"></param>
        /// <returns></returns>
        bool CreateObject(GridRegion destination, Vector3 newPosition, ISceneObject sog, bool isLocalCall);

        #endregion Objects

    }
}
