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
        IScene GetScene(ulong regionHandle);
        ISimulationService GetInnerService();

        #region Agents

        bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason);

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

        bool RetrieveAgent(GridRegion destination, UUID id, out IAgentData agent);

        bool QueryAccess(GridRegion destination, UUID id, Vector3 position, out string reason);

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
        /// Close child agent.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CloseChildAgent(GridRegion destination, UUID id);

        /// <summary>
        /// Close agent.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        bool CloseAgent(GridRegion destination, UUID id);

        #endregion Agents

        #region Objects

        /// <summary>
        /// Create an object in the destination region. This message is used primarily for prim crossing.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="sog"></param>
        /// <param name="isLocalCall"></param>
        /// <returns></returns>
        bool CreateObject(GridRegion destination, ISceneObject sog, bool isLocalCall);

        /// <summary>
        /// Create an object from the user's inventory in the destination region. 
        /// This message is used primarily by clients.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="userID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        bool CreateObject(GridRegion destination, UUID userID, UUID itemID);

        #endregion Objects

    }
}
