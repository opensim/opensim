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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// An agent in the scene.
    /// </summary>
    /// <remarks>
    /// Interface is a work in progress.  Please feel free to add other required properties and methods.
    /// </remarks>
    public interface ISceneAgent : ISceneEntity
    {
        /// <value>
        /// The client controlling this presence
        /// </value>
        IClientAPI ControllingClient { get; }

        /// <summary>
        /// What type of presence is this?  User, NPC, etc.
        /// </summary>
        PresenceType PresenceType { get; }

        /// <summary>
        /// If true, then the agent has no avatar in the scene.
        /// The agent exists to relay data from a region that neighbours the current position of the user's avatar.
        /// Occasionally data is relayed, such as which a user clicks an item in a neighbouring region.
        /// </summary>
        bool IsChildAgent { get; }

        bool IsInTransit { get; }
        bool IsNPC { get;}

        bool Invulnerable { get; set; }
        /// <summary>
        /// Avatar appearance data.
        /// </summary>
        /// <remarks>
        // Because appearance setting is in a module, we actually need
        // to give it access to our appearance directly, otherwise we
        // get a synchronization issue.
        /// </remarks>
        AvatarAppearance Appearance { get; set; }

        /// <summary>
        /// Send initial scene data to the client controlling this agent
        /// </summary>
        /// <remarks>
        /// This includes scene object data and the appearance data of other avatars.
        /// </remarks>
        void SendInitialDataToMe();

         /// <summary>
        /// Direction in which the scene presence is looking.
        /// </summary>
        /// <remarks>Will be Vector3.Zero for a child agent.</remarks>
        Vector3 Lookat { get; }
    }
}
