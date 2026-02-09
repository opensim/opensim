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
using System.Linq;
using System.Text;

using OpenSim.Region.PhysicsModules.SharedBase;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
    /// <summary>
    /// A physical actor that applies forces to an object to keep it hovering at a specified height.
    /// </summary>
    public class BSActorHover : BSActor
    {
        private BSVMotor m_hoverMotor;

        /// <summary>
        /// Initializes a new instance of the <see cref="BSActorHover"/> class.
        /// </summary>
        /// <param name="physicsScene">The physics scene this actor belongs to.</param>
        /// <param name="pObj">The physical object this actor controls.</param>
        /// <param name="actorName">The name of this actor instance.</param>
        public BSActorHover(BSScene physicsScene, BSPhysObject pObj, string actorName)
            : base(physicsScene, pObj, actorName)
        {
            m_hoverMotor = null;
            m_physicsScene.DetailLog("{0},BSActorHover,constructor", m_controllingPrim.LocalID);
        }

        /// <summary>
        /// Gets a value indicating whether this actor is currently active.
        /// </summary>
        public override bool isActive
        {
            get { return Enabled; }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public override void Dispose()
        {
            Enabled = false;
            DeactivateHover();
        }

        /// <summary>
        /// Refreshes the actor's state. Called when physical parameters (properties set in Bullet) need to be re-applied.
        /// This method is typically called at taint-time.
        /// </summary>
        public override void Refresh()
        {
            m_physicsScene.DetailLog("{0},BSActorHover,refresh", m_controllingPrim.LocalID);
    
            // If not active any more, turn me off
            if (!m_controllingPrim.HoverActive)
            {
                SetEnabled(false);
            }
    
            // If the object is physically active, add the hoverer prestep action
            if (isActive)
            {
                ActivateHover();
            }
            else
            {
                DeactivateHover();
            }
        }

        /// <summary>
        /// Called when the object's physical representation is being rebuilt.
        /// Allows the actor to remove any physical dependencies (constraints, etc.) before the body is destroyed.
        /// This is called at taint-time.
        /// </summary>
        public override void RemoveDependencies()
        {
            // Nothing to do for the hoverer since it is all software at pre-step action time.
        }

        /// <summary>
        /// Activates the hover mechanism.
        /// Creates the motor if it doesn't exist and subscribes to the pre-step event.
        /// </summary>
        private void ActivateHover()
        {
            if (m_hoverMotor == null)
            {
                // Turning the target on
                m_hoverMotor = new BSVMotor("BSActorHover",
                                            m_controllingPrim.HoverTau,               // timeScale
                                            BSMotor.Infinite,           // decay time scale
                                            1f                          // efficiency
                );
                m_hoverMotor.SetTarget(ComputeCurrentHoverHeight());
                m_hoverMotor.SetCurrent(m_controllingPrim.RawPosition.Z);
                m_hoverMotor.PhysicsScene = m_physicsScene; // DEBUG DEBUG so motor will output detail log messages.
    
                m_physicsScene.BeforeStep += Hoverer;
            }
            else
            {
                // Update parameters if already active
                m_hoverMotor.TimeScale = m_controllingPrim.HoverTau;
            }
        }

        /// <summary>
        /// Deactivates the hover mechanism.
        /// Unsubscribes from the pre-step event and releases the motor.
        /// </summary>
        private void DeactivateHover()
        {
            if (m_hoverMotor != null)
            {
                m_physicsScene.BeforeStep -= Hoverer;
                m_hoverMotor = null;
            }
        }

        /// <summary>
        /// The pre-step action called just before the simulation step.
        /// Updates the vertical position for hoverness by applying forces.
        /// </summary>
        /// <param name="timeStep">The time step for the current simulation frame.</param>
        private void Hoverer(float timeStep)
        {
            // Don't do hovering while the object is selected.
            if (!isActive)
                return;
    
            // Recompute target height based on terrain/water
            float targetHeight = ComputeCurrentHoverHeight();
            
            // Update motor state
            m_hoverMotor.SetCurrent(m_controllingPrim.RawPosition.Z);
            m_hoverMotor.SetTarget(targetHeight);
            
            // Calculate the error (distance to target)
            float error = targetHeight - m_controllingPrim.RawPosition.Z;
            
            // If we are close enough, just apply a force to counteract gravity and velocity
            // This prevents jittering when hovering at equilibrium
            if (Math.Abs(error) < 0.01f && Math.Abs(m_controllingPrim.RawVelocity.Z) < 0.1f)
            {
                 // Counteract gravity
                 OMV.Vector3 antiGravity = -m_controllingPrim.Gravity * m_controllingPrim.RawMass;
                 // Counteract vertical velocity (damping)
                 OMV.Vector3 damping = new OMV.Vector3(0f, 0f, -m_controllingPrim.RawVelocity.Z * m_controllingPrim.RawMass * 2.0f); // 2.0 is damping factor
                 
                 m_physicsScene.PE.ApplyCentralForce(m_controllingPrim.PhysBody, antiGravity + damping);
                 return;
            }

            // Calculate correction using the motor (PID-like behavior)
            float correctionAmount = m_hoverMotor.Step(timeStep);
            
            // Convert correction to force
            // Force = Mass * Acceleration
            // Acceleration = Correction / TimeStep^2  (Simplified)
            // Better approach: Force to reach target in TimeScale seconds.
            
            // Calculate desired velocity to reach target
            float desiredVelocity = (targetHeight - m_controllingPrim.RawPosition.Z) / m_controllingPrim.HoverTau;
            
            // Clamp desired velocity to reasonable limits to prevent rocket launches
            desiredVelocity = OMV.Utils.Clamp(desiredVelocity, -50f, 50f);
            
            // Calculate force needed to achieve desired velocity in one timestep
            // F = m * (v_target - v_current) / dt
            float moveForce = m_controllingPrim.RawMass * (desiredVelocity - m_controllingPrim.RawVelocity.Z) / timeStep;
            
            // Add force to counteract gravity so we don't sag
            float gravityForce = -m_controllingPrim.Gravity.Z * m_controllingPrim.RawMass;
            
            float totalForce = moveForce + gravityForce;

            // Apply the force
            m_physicsScene.PE.ApplyCentralForce(m_controllingPrim.PhysBody, new OMV.Vector3(0f, 0f, totalForce));
            
            m_physicsScene.DetailLog("{0},BSActorHover.Hoverer,move,targHt={1},err={2},force={3},mass={4}",
                            m_controllingPrim.LocalID, targetHeight, error, totalForce, m_controllingPrim.RawMass);
        }

        /// <summary>
        /// Computes the current target hover height based on the object's position and hover settings.
        /// This accounts for terrain height and water level if applicable.
        /// </summary>
        /// <returns>The target Z coordinate for hovering.</returns>
        private float ComputeCurrentHoverHeight()
        {
            float ret = m_controllingPrim.HoverHeight;
            float groundHeight = m_physicsScene.TerrainManager.GetTerrainHeightAtXYZ(m_controllingPrim.RawPosition);
    
            switch (m_controllingPrim.HoverType)
            {
                case PIDHoverType.Ground:
                    ret = groundHeight + m_controllingPrim.HoverHeight;
                    break;
                case PIDHoverType.GroundAndWater:
                    float waterHeight = m_physicsScene.TerrainManager.GetWaterLevelAtXYZ(m_controllingPrim.RawPosition);
                    if (groundHeight > waterHeight)
                    {
                        ret = groundHeight + m_controllingPrim.HoverHeight;
                    }
                    else
                    {
                        ret = waterHeight + m_controllingPrim.HoverHeight;
                    }
                    break;
            }
            return ret;
        }
    }
}
