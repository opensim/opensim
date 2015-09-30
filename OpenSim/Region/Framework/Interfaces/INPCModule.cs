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

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    // option flags for NPCs
    public enum NPCOptionsFlags : int
    {
        None                    = 0x00, // no flags (max restriction)
        AllowNotOwned           = 0x01, // allow NPCs to be created not Owned
        AllowSenseAsAvatar      = 0x02, // allow NPCs to set to be sensed as Avatars
        AllowCloneOtherAvatars  = 0x04, // allow NPCs to created cloning a avatar in region
        NoNPCGroup              = 0x08  // NPCs will have no group title, otherwise will have "- NPC -"
    }

    /// <summary>
    /// Temporary interface. More methods to come at some point to make NPCs
    /// more object oriented rather than controlling purely through module
    /// level interface calls (e.g. sit/stand).
    /// </summary>
    public interface INPC
    {
        /// <summary>
        /// Should this NPC be sensed by LSL sensors as an 'agent'
        /// (interpreted here to mean a normal user) rather than an OpenSim
        /// specific NPC extension?
        /// </summary>
        bool SenseAsAgent { get; }
    }

    public interface INPCModule
    {
        /// <summary>
        /// Create an NPC
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <param name="position"></param>
        /// <param name="senseAsAgent">
        /// Make the NPC show up as an agent on LSL sensors. The default is
        /// that they show up as the NPC type instead, but this is currently
        /// an OpenSim-only extension.
        /// </param>
        /// <param name="scene"></param>
        /// <param name="appearance">
        /// The avatar appearance to use for the new NPC.
        /// </param>
        /// <returns>
        /// The UUID of the ScenePresence created. UUID.Zero if there was a
        /// failure.
        /// </returns>
        UUID CreateNPC(string firstname, string lastname, Vector3 position,
                UUID owner, bool senseAsAgent, Scene scene,
                AvatarAppearance appearance);

        /// <summary>
        /// Create an NPC with a user-supplied agentID
        /// </summary>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <param name="position"></param>
        /// <param name="agentID"></param>
        /// The desired agent ID
        /// <param name="owner"></param>
        /// <param name="senseAsAgent">
        /// Make the NPC show up as an agent on LSL sensors. The default is
        /// that they show up as the NPC type instead, but this is currently
        /// an OpenSim-only extension.
        /// </param>
        /// <param name="scene"></param>
        /// <param name="appearance">
        /// The avatar appearance to use for the new NPC.
        /// </param>
        /// <returns>
        /// The UUID of the ScenePresence created. UUID.Zero if there was a
        /// failure.
        /// </returns>
        UUID CreateNPC(string firstname, string lastname,
                Vector3 position, UUID agentID, UUID owner, bool senseAsAgent, Scene scene,
                AvatarAppearance appearance);

        /// <summary>
        /// Check if the agent is an NPC.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="scene"></param>
        /// <returns>
        /// True if the agent is an NPC in the given scene. False otherwise.
        /// </returns>
        bool IsNPC(UUID agentID, Scene scene);

        /// <summary>
        /// Get the NPC.
        /// </summary>
        /// <remarks>
        /// This is not currently complete - manipulation of NPCs still occurs
        /// through the region interface.
        /// </remarks>
        /// <param name="agentID"></param>
        /// <param name="scene"></param>
        /// <returns>The NPC. null if it does not exist.</returns>
        INPC GetNPC(UUID agentID, Scene scene);

        /// <summary>
        /// Check if the caller has permission to manipulate the given NPC.
        /// </summary>
        /// <remarks>
        /// A caller has permission if
        ///   * An NPC exists with the given npcID.
        ///   * The caller UUID given is UUID.Zero.
        ///   * The avatar is unowned (owner is UUID.Zero).
        ///   * The avatar is owned and the owner and callerID match.
        ///   * The avatar is owned and the callerID matches its agentID.
        /// </remarks>
        /// <param name="av"></param>
        /// <param name="callerID"></param>
        /// <returns>true if they do, false if they don't.</returns>
        /// <param name="npcID"></param>
        /// <param name="callerID"></param>
        /// <returns>
        /// true if they do, false if they don't or if there's no NPC with the
        /// given ID.
        /// </returns>
        bool CheckPermissions(UUID npcID, UUID callerID);

        /// <summary>
        /// Set the appearance for an NPC.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="appearance"></param>
        /// <param name="scene"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool SetNPCAppearance(UUID agentID, AvatarAppearance appearance,
                Scene scene);

        /// <summary>
        /// Move an NPC to a target over time.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <param name="pos"></param>
        /// <param name="noFly">
        /// If true, then the avatar will attempt to walk to the location even
        /// if it's up in the air. This is to allow walking on prims.
        /// </param>
        /// <param name="landAtTarget">
        /// If true and the avatar is flying when it reaches the target, land.
        /// </param> name="running">
        /// If true, NPC moves with running speed.
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool MoveToTarget(UUID agentID, Scene scene, Vector3 pos, bool noFly,
                bool landAtTarget, bool running);

        /// <summary>
        /// Stop the NPC's current movement.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool StopMoveToTarget(UUID agentID, Scene scene);

        /// <summary>
        /// Get the NPC to say something.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <param name="text"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool Say(UUID agentID, Scene scene, string text);

        /// <summary>
        /// Get the NPC to say something.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <param name="text"></param>
        /// <param name="channel"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool Say(UUID agentID, Scene scene, string text, int channel);

        /// <summary>
        /// Get the NPC to shout something.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <param name="text"></param>
        /// <param name="channel"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool Shout(UUID agentID, Scene scene, string text, int channel);

        /// <summary>
        /// Get the NPC to whisper something.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <param name="text"></param>
        /// <param name="channel"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool Whisper(UUID agentID, Scene scene, string text, int channel);

        /// <summary>
        /// Sit the NPC.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="partID"></param>
        /// <param name="scene"></param>
        /// <returns>true if the sit succeeded, false if not</returns>
        bool Sit(UUID agentID, UUID partID, Scene scene);

        /// <summary>
        /// Stand a sitting NPC.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="scene"></param>
        /// <returns>true if the stand succeeded, false if not</returns>
        bool Stand(UUID agentID, Scene scene);

        /// <summary>
        /// Get the NPC to touch an object.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="partID"></param>
        /// <returns>
        /// true if the touch is actually attempted, false if not.
        /// </returns>
        bool Touch(UUID agentID, UUID partID);

        /// <summary>
        /// Delete an NPC.
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <param name="scene"></param>
        /// <returns>
        /// True if the operation succeeded, false if there was no such agent
        /// or the agent was not an NPC.
        /// </returns>
        bool DeleteNPC(UUID agentID, Scene scene);

        /// <summary>
        /// Get the owner of a NPC
        /// </summary>
        /// <param name="agentID">The UUID of the NPC</param>
        /// <returns>
        /// UUID of owner if the NPC exists, UUID.Zero  if there was no such
        /// agent, the agent is unowned  or the agent was not an NPC.
        /// </returns>
        UUID GetOwner(UUID agentID);
 
        NPCOptionsFlags NPCOptionFlags {get;}
    }
}
