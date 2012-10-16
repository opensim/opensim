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

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ISoundModule
    {
        /// <summary>
        /// Maximum distance between a sound source and a recipient.
        /// </summary>
        float MaxDistance { get; }

        /// <summary>
        /// Play a sound from an object.
        /// </summary>
        /// <param name="soundID">Sound asset ID</param>
        /// <param name="ownerID">Sound source owner</param>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="gain">Sound volume</param>
        /// <param name="position">Sound source position</param>
        /// <param name="flags">Sound flags</param>
        /// <param name="radius">
        /// Radius used to affect gain over distance.
        /// </param>
        void PlayAttachedSound(UUID soundID, UUID ownerID, UUID objectID,
                double gain, Vector3 position, byte flags, float radius);

        /// <summary>
        /// Trigger a sound in the scene.
        /// </summary>
        /// <param name="soundId">Sound asset ID</param>
        /// <param name="ownerID">Sound source owner</param>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="parentID">Sound source parent.</param>
        /// <param name="gain">Sound volume</param>
        /// <param name="position">Sound source position</param>
        /// <param name="handle"></param>
        /// <param name="radius">
        /// Radius used to affect gain over distance.
        /// </param>
        void TriggerSound(
                UUID soundId, UUID ownerID, UUID objectID, UUID parentID,
                double gain, Vector3 position, UInt64 handle, float radius);

        /// <summary>
        /// Stop sounds eminating from an object.
        /// </summary>
        /// <param name="objectID">Sound source ID</param>
        void StopSound(UUID objectID);

        /// <summary>
        /// Preload sound to viewers within range.
        /// </summary>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="soundID">Sound asset ID</param>
        /// <param name="radius">
        /// Radius used to determine which viewers should preload the sound.
        /// </param>
        void PreloadSound(UUID objectID, UUID soundID, float radius);

        /// <summary>
        /// Loop specified sound at specified volume with specified radius,
        /// optionally declaring object as new sync master.
        /// </summary>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="soundID">Sound asset ID</param>
        /// <param name="gain">Sound volume</param>
        /// <param name="radius">Sound radius</param>
        /// <param name="isMaster">Set object to sync master if true</param>
        void LoopSound(UUID objectID, UUID soundID, double gain,
                double radius, bool isMaster);

        /// <summary>
        /// Trigger or play an attached sound in this part's inventory.
        /// </summary>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="sound">Sound asset ID</param>
        /// <param name="volume">Sound volume</param>
        /// <param name="triggered">Triggered or not.</param>
        /// <param name="flags"></param>
        /// <param name="radius">Sound radius</param>
        /// <param name="useMaster">Play using sound master</param>
        /// <param name="isMaster">Play as sound master</param>
        void SendSound(UUID objectID, UUID sound, double volume,
                bool triggered, byte flags, float radius, bool useMaster,
                bool isMaster);

        /// <summary>
        /// Trigger a sound to be played to all agents within an axis-aligned
        /// bounding box.
        /// </summary>
        /// <param name="objectID">Sound source ID</param>
        /// <param name="sound">Sound asset ID</param>
        /// <param name="volume">Sound volume</param>
        /// <param name="min">AABB bottom south-west corner</param>
        /// <param name="max">AABB top north-east corner</param>
        void TriggerSoundLimited(UUID objectID, UUID sound, double volume,
                Vector3 min, Vector3 max);
    }
}