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
    /// Manages sleep states of physical objects to improve performance.
    /// Identifies objects that have settled and forces them into a sleeping state
    /// to reduce physics simulation overhead.
    /// </summary>
    public class SleepOptimizationManager
    {
        private float m_sleepTimeout;
        private float m_velocityThreshold;
        private Dictionary<uint, float> m_stationaryTimes = new Dictionary<uint, float>();

        /// <summary>
        /// Initializes a new instance of the SleepOptimizationManager.
        /// </summary>
        /// <param name="sleepTimeout">Time in seconds an object must be stationary before sleeping.</param>
        /// <param name="velocityThreshold">Velocity below which an object is considered stationary.</param>
        public SleepOptimizationManager(float sleepTimeout, float velocityThreshold)
        {
            m_sleepTimeout = sleepTimeout;
            m_velocityThreshold = velocityThreshold;
        }

        /// <summary>
        /// Checks objects and puts them to sleep if they meet the criteria.
        /// </summary>
        /// <param name="scene">The physics scene.</param>
        /// <param name="dt">The time step.</param>
        public void CheckObjects(BSScene scene, float dt)
        {
            float thresholdSq = m_velocityThreshold * m_velocityThreshold;

            lock (scene.PhysObjects)
            {
                foreach (var kvp in scene.PhysObjects)
                {
                    BSPhysObject prim = kvp.Value;
                    
                    // Skip if already static, selected, or explicitly disable deactivation
                    if (prim.IsStatic || prim.IsSelected || prim.DisableDeactivation) 
                    {
                        if (m_stationaryTimes.ContainsKey(prim.LocalID))
                            m_stationaryTimes.Remove(prim.LocalID);
                        continue;
                    }

                    // Check velocity
                    if (prim.RawVelocity.LengthSquared() < thresholdSq && 
                        prim.RawRotationalVelocity.LengthSquared() < thresholdSq)
                    {
                        // Object is effectively stationary
                        if (!m_stationaryTimes.ContainsKey(prim.LocalID))
                            m_stationaryTimes[prim.LocalID] = 0f;
                        
                        m_stationaryTimes[prim.LocalID] += dt;

                        // If stationary for long enough, force sleep
                        if (m_stationaryTimes[prim.LocalID] > m_sleepTimeout)
                        {
                            if (prim.PhysBody.HasPhysicalBody)
                            {
                                scene.PE.ForceActivationState(prim.PhysBody, ActivationState.ISLAND_SLEEPING);
                            }
                        }
                    }
                    else
                    {
                        // Object moved, reset timer
                        if (m_stationaryTimes.ContainsKey(prim.LocalID))
                            m_stationaryTimes.Remove(prim.LocalID);
                    }
                }
            }
        }
    }
}
