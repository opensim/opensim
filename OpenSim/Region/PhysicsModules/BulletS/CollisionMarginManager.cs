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
    public class CollisionMarginManager
    {
        private BSScene m_scene;
        private const float DEFAULT_MARGIN = 0.04f;
        private const float MIN_MARGIN = 0.001f;
        private const float MAX_MARGIN = 0.2f;

        public CollisionMarginManager(BSScene scene)
        {
            m_scene = scene;
        }

        public float CalculateOptimalMargin(BSPhysObject prim)
        {
            Vector3 size = prim.Size; // This is the size passed by user (scale)
            // If it's a mesh/hull, the margin might need to be smaller relative to the object size
            
            // For very thin objects, the margin should be smaller than the thickness
            float minDimension = Math.Min(size.X, Math.Min(size.Y, size.Z));
            
            // Calculate margin as percentage of smallest dimension
            // 2% seems like a reasonable starting point for dynamic adjustment
            // but we clamp it to be safe.
            float optimalMargin = minDimension * 0.02f;
            
            // Ensure we don't go below a safe minimum or above a reasonable maximum
            return Utils.Clamp(optimalMargin, MIN_MARGIN, MAX_MARGIN);
        }

        public void UpdateCollisionMargins()
        {
            // Iterate over all physical objects in the scene
            // We lock to ensure thread safety while iterating
            lock (m_scene.PhysObjects)
            {
                foreach (var kvp in m_scene.PhysObjects)
                {
                    BSPhysObject prim = kvp.Value;
                    if (prim.PhysShape.HasPhysicalShape)
                    {
                        float optimalMargin = CalculateOptimalMargin(prim);
                        // Apply the margin using the physics engine interface
                        m_scene.PE.SetMargin(prim.PhysShape.physShapeInfo, optimalMargin);
                    }
                }
            }
        }

        // Update margin for a specific object
        public void UpdateCollisionMargin(BSPhysObject prim)
        {
            if (prim.PhysShape.HasPhysicalShape)
            {
                float optimalMargin = CalculateOptimalMargin(prim);
                m_scene.PE.SetMargin(prim.PhysShape.physShapeInfo, optimalMargin);
            }
        }
    }
}
