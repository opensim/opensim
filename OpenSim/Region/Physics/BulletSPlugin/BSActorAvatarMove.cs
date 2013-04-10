/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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

using OpenSim.Region.Physics.Manager;

using OMV = OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSActorAvatarMove : BSActor
{
    BSVMotor m_velocityMotor;

    public BSActorAvatarMove(BSScene physicsScene, BSPhysObject pObj, string actorName)
        : base(physicsScene, pObj, actorName)
    {
        m_velocityMotor = null;
        m_physicsScene.DetailLog("{0},BSActorAvatarMove,constructor", m_controllingPrim.LocalID);
    }

    // BSActor.isActive
    public override bool isActive
    {
        get { return Enabled && m_controllingPrim.IsPhysicallyActive; }
    }

    // Release any connections and resources used by the actor.
    // BSActor.Dispose()
    public override void Dispose()
    {
        Enabled = false;
    }

    // Called when physical parameters (properties set in Bullet) need to be re-applied.
    // Called at taint-time.
    // BSActor.Refresh()
    public override void Refresh()
    {
        m_physicsScene.DetailLog("{0},BSActorAvatarMove,refresh", m_controllingPrim.LocalID);

        // If the object is physically active, add the hoverer prestep action
        if (isActive)
        {
            ActivateAvatarMove();
        }
        else
        {
            DeactivateAvatarMove();
        }
    }

    // The object's physical representation is being rebuilt so pick up any physical dependencies (constraints, ...).
    //     Register a prestep action to restore physical requirements before the next simulation step.
    // Called at taint-time.
    // BSActor.RemoveBodyDependencies()
    public override void RemoveBodyDependencies()
    {
        // Nothing to do for the hoverer since it is all software at pre-step action time.
    }

    // Usually called when target velocity changes to set the current velocity and the target
    //     into the movement motor.
    public void SetVelocityAndTarget(OMV.Vector3 vel, OMV.Vector3 targ, bool inTaintTime)
    {
        m_physicsScene.TaintedObject(inTaintTime, "BSActorAvatarMove.setVelocityAndTarget", delegate()
        {
            if (m_velocityMotor != null)
            {
                m_velocityMotor.Reset();
                m_velocityMotor.SetTarget(targ);
                m_velocityMotor.SetCurrent(vel);
                m_velocityMotor.Enabled = true;
            }
        });
    }

    // If a hover motor has not been created, create one and start the hovering.
    private void ActivateAvatarMove()
    {
        if (m_velocityMotor == null)
        {
            // Infinite decay and timescale values so motor only changes current to target values.
            m_velocityMotor = new BSVMotor("BSCharacter.Velocity", 
                                                0.2f,                       // time scale
                                                BSMotor.Infinite,           // decay time scale
                                                BSMotor.InfiniteVector,     // friction timescale
                                                1f                          // efficiency
            );
            // _velocityMotor.PhysicsScene = PhysicsScene; // DEBUG DEBUG so motor will output detail log messages.
            SetVelocityAndTarget(m_controllingPrim.RawVelocity, m_controllingPrim.TargetVelocity, true /* inTaintTime */);

            m_physicsScene.BeforeStep += Mover;
        }
    }

    private void DeactivateAvatarMove()
    {
        if (m_velocityMotor != null)
        {
            m_physicsScene.BeforeStep -= Mover;
            m_velocityMotor = null;
        }
    }

    // Called just before the simulation step. Update the vertical position for hoverness.
    private void Mover(float timeStep)
    {
        // Don't do movement while the object is selected.
        if (!isActive)
            return;

        // TODO: Decide if the step parameters should be changed depending on the avatar's
        //     state (flying, colliding, ...). There is code in ODE to do this.

        // COMMENTARY: when the user is making the avatar walk, except for falling, the velocity
        //   specified for the avatar is the one that should be used. For falling, if the avatar
        //   is not flying and is not colliding then it is presumed to be falling and the Z
        //   component is not fooled with (thus allowing gravity to do its thing).
        // When the avatar is standing, though, the user has specified a velocity of zero and
        //   the avatar should be standing. But if the avatar is pushed by something in the world
        //   (raising elevator platform, moving vehicle, ...) the avatar should be allowed to
        //   move. Thus, the velocity cannot be forced to zero. The problem is that small velocity
        //   errors can creap in and the avatar will slowly float off in some direction.
        // So, the problem is that, when an avatar is standing, we cannot tell creaping error
        //   from real pushing.
        // The code below uses whether the collider is static or moving to decide whether to zero motion.

        m_velocityMotor.Step(timeStep);
        m_controllingPrim.IsStationary = false;

        // If we're not supposed to be moving, make sure things are zero.
        if (m_velocityMotor.ErrorIsZero() && m_velocityMotor.TargetValue == OMV.Vector3.Zero)
        {
            // The avatar shouldn't be moving
            m_velocityMotor.Zero();

            if (m_controllingPrim.IsColliding)
            {
                // If we are colliding with a stationary object, presume we're standing and don't move around
                if (!m_controllingPrim.ColliderIsMoving)
                {
                    m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,collidingWithStationary,zeroingMotion", m_controllingPrim.LocalID);
                    m_controllingPrim.IsStationary = true;
                    m_controllingPrim.ZeroMotion(true /* inTaintTime */);
                }

                // Standing has more friction on the ground
                if (m_controllingPrim.Friction != BSParam.AvatarStandingFriction)
                {
                    m_controllingPrim.Friction = BSParam.AvatarStandingFriction;
                    m_physicsScene.PE.SetFriction(m_controllingPrim.PhysBody, m_controllingPrim.Friction);
                }
            }
            else
            {
                if (m_controllingPrim.Flying)
                {
                    // Flying and not collising and velocity nearly zero.
                    m_controllingPrim.ZeroMotion(true /* inTaintTime */);
                }
            }

            m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,taint,stopping,target={1},colliding={2}",
                            m_controllingPrim.LocalID, m_velocityMotor.TargetValue, m_controllingPrim.IsColliding);
        }
        else
        {
            // Supposed to be moving.
            OMV.Vector3 stepVelocity = m_velocityMotor.CurrentValue;

            if (m_controllingPrim.Friction != BSParam.AvatarFriction)
            {
                // Probably starting up walking. Set friction to moving friction.
                m_controllingPrim.Friction = BSParam.AvatarFriction;
                m_physicsScene.PE.SetFriction(m_controllingPrim.PhysBody, m_controllingPrim.Friction);
            }

            // If falling, we keep the world's downward vector no matter what the other axis specify.
            // The check for RawVelocity.Z < 0 makes jumping work (temporary upward force).
            if (!m_controllingPrim.Flying && !m_controllingPrim.IsColliding)
            {
                if (m_controllingPrim.RawVelocity.Z < 0)
                    stepVelocity.Z = m_controllingPrim.RawVelocity.Z;
                // DetailLog("{0},BSCharacter.MoveMotor,taint,overrideStepZWithWorldZ,stepVel={1}", LocalID, stepVelocity);
            }

            // 'stepVelocity' is now the speed we'd like the avatar to move in. Turn that into an instantanous force.
            OMV.Vector3 moveForce = (stepVelocity - m_controllingPrim.RawVelocity) * m_controllingPrim.Mass;

            // Should we check for move force being small and forcing velocity to zero?

            // Add special movement force to allow avatars to walk up stepped surfaces.
            moveForce += WalkUpStairs();

            m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,move,stepVel={1},vel={2},mass={3},moveForce={4}",
                            m_controllingPrim.LocalID, stepVelocity, m_controllingPrim.RawVelocity, m_controllingPrim.Mass, moveForce);
            m_physicsScene.PE.ApplyCentralImpulse(m_controllingPrim.PhysBody, moveForce);
        }
    }

    // Decide if the character is colliding with a low object and compute a force to pop the
    //    avatar up so it can walk up and over the low objects.
    private OMV.Vector3 WalkUpStairs()
    {
        OMV.Vector3 ret = OMV.Vector3.Zero;

        // This test is done if moving forward, not flying and is colliding with something.
        // DetailLog("{0},BSCharacter.WalkUpStairs,IsColliding={1},flying={2},targSpeed={3},collisions={4}",
        //                 LocalID, IsColliding, Flying, TargetSpeed, CollisionsLastTick.Count);
        if (m_controllingPrim.IsColliding && !m_controllingPrim.Flying && m_controllingPrim.TargetVelocitySpeed > 0.1f /* && ForwardSpeed < 0.1f */)
        {
            // The range near the character's feet where we will consider stairs
            float nearFeetHeightMin = m_controllingPrim.RawPosition.Z - (m_controllingPrim.Size.Z / 2f) + 0.05f;
            float nearFeetHeightMax = nearFeetHeightMin + BSParam.AvatarStepHeight;

            // Look for a collision point that is near the character's feet and is oriented the same as the charactor is
            foreach (KeyValuePair<uint, ContactPoint> kvp in m_controllingPrim.CollisionsLastTick.m_objCollisionList)
            {
                // Don't care about collisions with the terrain
                if (kvp.Key > m_physicsScene.TerrainManager.HighestTerrainID)
                {
                    OMV.Vector3 touchPosition = kvp.Value.Position;
                    // DetailLog("{0},BSCharacter.WalkUpStairs,min={1},max={2},touch={3}",
                    //                 LocalID, nearFeetHeightMin, nearFeetHeightMax, touchPosition);
                    if (touchPosition.Z >= nearFeetHeightMin && touchPosition.Z <= nearFeetHeightMax)
                    {
                        // This contact is within the 'near the feet' range.
                        // The normal should be our contact point to the object so it is pointing away
                        //    thus the difference between our facing orientation and the normal should be small.
                        OMV.Vector3 directionFacing = OMV.Vector3.UnitX * m_controllingPrim.RawOrientation;
                        OMV.Vector3 touchNormal = OMV.Vector3.Normalize(kvp.Value.SurfaceNormal);
                        float diff = Math.Abs(OMV.Vector3.Distance(directionFacing, touchNormal));
                        if (diff < BSParam.AvatarStepApproachFactor)
                        {
                            // Found the stairs contact point. Push up a little to raise the character.
                            float upForce = (touchPosition.Z - nearFeetHeightMin) * m_controllingPrim.Mass * BSParam.AvatarStepForceFactor;
                            ret = new OMV.Vector3(0f, 0f, upForce);

                            // Also move the avatar up for the new height
                            OMV.Vector3 displacement = new OMV.Vector3(0f, 0f, BSParam.AvatarStepHeight / 2f);
                            m_controllingPrim.ForcePosition = m_controllingPrim.RawPosition + displacement;
                        }
                        m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs,touchPos={1},nearFeetMin={2},faceDir={3},norm={4},diff={5},ret={6}",
                                m_controllingPrim.LocalID, touchPosition, nearFeetHeightMin, directionFacing, touchNormal, diff, ret);
                    }
                }
            }
        }

        return ret;
    }

}
}


