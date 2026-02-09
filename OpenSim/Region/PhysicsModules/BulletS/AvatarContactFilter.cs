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
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
    /// <summary>
    /// Filters and debounces contact events for avatars to prevent jittery movement
    /// and rapid animation switching (standing/falling).
    /// </summary>
    public class AvatarContactFilter
    {
        /// <summary>
        /// The minimum time in seconds between contact updates to be considered a new/distinct contact event
        /// for the purpose of debouncing jitter.
        /// </summary>
        private const float DEBOUNCE_TIME = 0.1f; // 100ms

        /// <summary>
        /// Stores the last contact information for each avatar (by LocalID).
        /// </summary>
        private Dictionary<uint, ContactInfo> _lastContacts = new Dictionary<uint, ContactInfo>();

        private struct ContactInfo
        {
            public Vector3 Point;
            public float Time;
            public ContactInfo(Vector3 p, float t) { Point = p; Time = t; }
        }

        /// <summary>
        /// Determines if a contact should be processed based on time and distance thresholds.
        /// </summary>
        /// <param name="avatarId">The LocalID of the avatar.</param>
        /// <param name="contactPoint">The world position of the contact.</param>
        /// <param name="currentTime">The current simulation time (in seconds).</param>
        /// <returns>True if the contact is significant/new enough to process; False if it should be filtered out.</returns>
        public bool ShouldProcessContact(uint avatarId, Vector3 contactPoint, float currentTime)
        {
            if (!_lastContacts.TryGetValue(avatarId, out ContactInfo lastContact))
            {
                _lastContacts[avatarId] = new ContactInfo(contactPoint, currentTime);
                return true;
            }

            float timeDiff = currentTime - lastContact.Time;
            float distanceDiff = Vector3.Distance(contactPoint, lastContact.Point);

            // If enough time has passed or the contact point has moved significantly, update and process.
            // Distance threshold of 0.1m allows for small shifts without triggering debouncing resets,
            // but large movements (walking off a ledge) are caught immediately.
            if (timeDiff > DEBOUNCE_TIME || distanceDiff > 0.1f)
            {
                _lastContacts[avatarId] = new ContactInfo(contactPoint, currentTime);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Clears the contact history for a specific avatar. 
        /// Useful when an avatar teleports or is reset.
        /// </summary>
        /// <param name="avatarId">The LocalID of the avatar.</param>
        public void Clear(uint avatarId)
        {
            if (_lastContacts.ContainsKey(avatarId))
                _lastContacts.Remove(avatarId);
        }
    }
}
