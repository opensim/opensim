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

using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public class BSActorAvatarMove : BSActor
{
    BSVMotor m_velocityMotor;

    // Set to true if we think we're going up stairs.
    //    This state is remembered because collisions will turn on and off as we go up stairs.
    int m_walkingUpStairs;
    // The amount the step up is applying. Used to smooth stair walking.
    float m_lastStepUp;

    // There are times the velocity is set but we don't want to inforce stationary until the
    //    real velocity drops.
    bool m_waitingForLowVelocityForStationary = false;

    public BSActorAvatarMove(BSScene physicsScene, BSPhysObject pObj, string actorName)
        : base(physicsScene, pObj, actorName)
    {
        m_velocityMotor = null;
        m_walkingUpStairs = 0;
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
        base.SetEnabled(false);
        DeactivateAvatarMove();
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
    // BSActor.RemoveDependencies()
    public override void RemoveDependencies()
    {
        // Nothing to do for the hoverer since it is all software at pre-step action time.
    }

    // Usually called when target velocity changes to set the current velocity and the target
    //     into the movement motor.
    public void SetVelocityAndTarget(OMV.Vector3 vel, OMV.Vector3 targ, bool inTaintTime)
    {
        m_physicsScene.TaintedObject(inTaintTime, m_controllingPrim.LocalID, "BSActorAvatarMove.setVelocityAndTarget", delegate()
        {
            if (m_velocityMotor != null)
            {
                m_velocityMotor.Reset();
                m_velocityMotor.SetTarget(targ);
                m_velocityMotor.SetCurrent(vel);
                m_velocityMotor.Enabled = true;
                m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,SetVelocityAndTarget,vel={1}, targ={2}",
                            m_controllingPrim.LocalID, vel, targ);
                m_waitingForLowVelocityForStationary = false;
            }
        });
    }

    public void SuppressStationayCheckUntilLowVelocity()
    {
        m_waitingForLowVelocityForStationary = true;
    }

    // If a movement motor has not been created, create one and start the movement
    private void ActivateAvatarMove()
    {
        if (m_velocityMotor == null)
        {
            // Infinite decay and timescale values so motor only changes current to target values.
            m_velocityMotor = new BSVMotor("BSCharacter.Velocity",
                                                0.2f,                       // time scale
                                                BSMotor.Infinite,           // decay time scale
                                                1f                          // efficiency
            );
            m_velocityMotor.ErrorZeroThreshold = BSParam.AvatarStopZeroThreshold;
            // m_velocityMotor.PhysicsScene = m_controllingPrim.PhysScene; // DEBUG DEBUG so motor will output detail log messages.
            SetVelocityAndTarget(m_controllingPrim.RawVelocity, m_controllingPrim.TargetVelocity, true /* inTaintTime */);

            m_physicsScene.BeforeStep += Mover;
            m_controllingPrim.OnPreUpdateProperty += Process_OnPreUpdateProperty;

            m_walkingUpStairs = 0;
            m_waitingForLowVelocityForStationary = false;
        }
    }

    private void DeactivateAvatarMove()
    {
        if (m_velocityMotor != null)
        {
            m_controllingPrim.OnPreUpdateProperty -= Process_OnPreUpdateProperty;
            m_physicsScene.BeforeStep -= Mover;
            m_velocityMotor = null;
        }
    }

    // Called just before the simulation step.
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
                // if colliding with something stationary and we're not doing volume detect .
                if (!m_controllingPrim.ColliderIsMoving && !m_controllingPrim.ColliderIsVolumeDetect)
                {
                    if (m_waitingForLowVelocityForStationary)
                    {
                        // if waiting for velocity to drop and it has finally dropped, we can be stationary
                        if (m_controllingPrim.RawVelocity.LengthSquared() < BSParam.AvatarStopZeroThresholdSquared)
                        {
                            m_waitingForLowVelocityForStationary = false;
                        }
                    }
                    if (!m_waitingForLowVelocityForStationary)
                    {
                        m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,collidingWithStationary,zeroingMotion", m_controllingPrim.LocalID);
                        m_controllingPrim.IsStationary = true;
                        m_controllingPrim.ZeroMotion(true /* inTaintTime */);
                    }
                    else
                    {
                        m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,waitingForLowVel,rawvel={1}",
                                    m_controllingPrim.LocalID, m_controllingPrim.RawVelocity.Length());
                    }
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
                    // Flying and not colliding and velocity nearly zero.
                    m_controllingPrim.ZeroMotion(true /* inTaintTime */);
                }
                else
                {
                    //We are falling but are not touching any keys make sure not falling too fast
                    if (m_controllingPrim.RawVelocity.Z < BSParam.AvatarTerminalVelocity)
                    {

                        OMV.Vector3 slowingForce = new OMV.Vector3(0f, 0f, BSParam.AvatarTerminalVelocity - m_controllingPrim.RawVelocity.Z) * m_controllingPrim.Mass;
                        m_physicsScene.PE.ApplyCentralImpulse(m_controllingPrim.PhysBody, slowingForce);
                    }

                }
            }

            m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,taint,stopping,target={1},colliding={2},isStationary={3}",
                            m_controllingPrim.LocalID, m_velocityMotor.TargetValue, m_controllingPrim.IsColliding,m_controllingPrim.IsStationary);
        }
        else
        {
            // Supposed to be moving.
            OMV.Vector3 stepVelocity = m_velocityMotor.CurrentValue;

            if (m_controllingPrim.Friction != BSParam.AvatarFriction)
            {
                // Probably starting to walk. Set friction to moving friction.
                m_controllingPrim.Friction = BSParam.AvatarFriction;
                m_physicsScene.PE.SetFriction(m_controllingPrim.PhysBody, m_controllingPrim.Friction);
            }

            // 'm_velocityMotor is used for walking, flying, and jumping and will thus have the correct values
            //    for Z. But in come cases it must be over-ridden. Like when falling or jumping.

            float realVelocityZ = m_controllingPrim.RawVelocity.Z;

            // If not flying and falling, we over-ride the stepping motor so we can fall to the ground
            if (!m_controllingPrim.Flying && realVelocityZ < 0)
            {
                // Can't fall faster than this
                if (realVelocityZ < BSParam.AvatarTerminalVelocity)
                {
                    realVelocityZ = BSParam.AvatarTerminalVelocity;
                }

                stepVelocity.Z = realVelocityZ;
            }
            // m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,DEBUG,motorCurrent={1},realZ={2},flying={3},collid={4},jFrames={5}",
            //     m_controllingPrim.LocalID, m_velocityMotor.CurrentValue, realVelocityZ, m_controllingPrim.Flying, m_controllingPrim.IsColliding, m_jumpFrames);

            //Alicia: Maintain minimum height when flying.
            // SL has a flying effect that keeps the avatar flying above the ground by some margin
            if (m_controllingPrim.Flying)
            {
                float hover_height = m_physicsScene.TerrainManager.GetTerrainHeightAtXYZ(m_controllingPrim.RawPosition)
                                                        + BSParam.AvatarFlyingGroundMargin;

                if( m_controllingPrim.Position.Z < hover_height)
                {
                    m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,addingUpforceForGroundMargin,height={1},hoverHeight={2}",
                                m_controllingPrim.LocalID, m_controllingPrim.Position.Z, hover_height);
                    stepVelocity.Z += BSParam.AvatarFlyingGroundUpForce;
                }
            }

            // 'stepVelocity' is now the speed we'd like the avatar to move in. Turn that into an instantanous force.
            OMV.Vector3 moveForce = (stepVelocity - m_controllingPrim.RawVelocity) * m_controllingPrim.Mass;

            // Add special movement force to allow avatars to walk up stepped surfaces.
            moveForce += WalkUpStairs();

            m_physicsScene.DetailLog("{0},BSCharacter.MoveMotor,move,stepVel={1},vel={2},mass={3},moveForce={4}",
                            m_controllingPrim.LocalID, stepVelocity, m_controllingPrim.RawVelocity, m_controllingPrim.Mass, moveForce);
            m_physicsScene.PE.ApplyCentralImpulse(m_controllingPrim.PhysBody, moveForce);
        }
    }

    // Called just as the property update is received from the physics engine.
    // Do any mode necessary for avatar movement.
    private void Process_OnPreUpdateProperty(ref EntityProperties entprop)
    {
        // Don't change position if standing on a stationary object.
        if (m_controllingPrim.IsStationary)
        {
            entprop.Position = m_controllingPrim.RawPosition;
            entprop.Velocity = OMV.Vector3.Zero;
            m_physicsScene.PE.SetTranslation(m_controllingPrim.PhysBody, entprop.Position, entprop.Rotation);
        }

    }

    // Decide if the character is colliding with a low object and compute a force to pop the
    //    avatar up so it can walk up and over the low objects.
    private OMV.Vector3 WalkUpStairs()
    {
        OMV.Vector3 ret = OMV.Vector3.Zero;

        m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs,IsColliding={1},flying={2},targSpeed={3},collisions={4},avHeight={5}",
                        m_controllingPrim.LocalID, m_controllingPrim.IsColliding, m_controllingPrim.Flying,
                        m_controllingPrim.TargetVelocitySpeed, m_controllingPrim.CollisionsLastTick.Count, m_controllingPrim.Size.Z);

        // Check for stairs climbing if colliding, not flying and moving forward
        if ( m_controllingPrim.IsColliding
                    && !m_controllingPrim.Flying
                    && m_controllingPrim.TargetVelocitySpeed > 0.1f )
        {
            // The range near the character's feet where we will consider stairs
            // float nearFeetHeightMin = m_controllingPrim.RawPosition.Z - (m_controllingPrim.Size.Z / 2f) + 0.05f;
            // Note: there is a problem with the computation of the capsule height. Thus RawPosition is off
            //    from the height. Revisit size and this computation when height is scaled properly.
            float nearFeetHeightMin = m_controllingPrim.RawPosition.Z - (m_controllingPrim.Size.Z / 2f) - BSParam.AvatarStepGroundFudge;
            float nearFeetHeightMax = nearFeetHeightMin + BSParam.AvatarStepHeight;

            // Look for a collision point that is near the character's feet and is oriented the same as the charactor is.
            // Find the highest 'good' collision.
            OMV.Vector3 highestTouchPosition = OMV.Vector3.Zero;
            foreach (KeyValuePair<uint, ContactPoint> kvp in m_controllingPrim.CollisionsLastTick.m_objCollisionList)
            {
                // Don't care about collisions with the terrain
                if (kvp.Key > m_physicsScene.TerrainManager.HighestTerrainID)
                {
                    BSPhysObject collisionObject;
                    if (m_physicsScene.PhysObjects.TryGetValue(kvp.Key, out collisionObject))
                    {
                        if (!collisionObject.IsVolumeDetect)
                        {
                            OMV.Vector3 touchPosition = kvp.Value.Position;
                            m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs,min={1},max={2},touch={3}",
                                            m_controllingPrim.LocalID, nearFeetHeightMin, nearFeetHeightMax, touchPosition);
                            if (touchPosition.Z >= nearFeetHeightMin && touchPosition.Z <= nearFeetHeightMax)
                            {
                                // This contact is within the 'near the feet' range.
                                // The step is presumed to be more or less vertical. Thus the Z component should
                                //    be nearly horizontal.
                                OMV.Vector3 directionFacing = OMV.Vector3.UnitX * m_controllingPrim.RawOrientation;
                                OMV.Vector3 touchNormal = OMV.Vector3.Normalize(kvp.Value.SurfaceNormal);
                                const float PIOver2 = 1.571f; // Used to make unit vector axis into approx radian angles
                                // m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs,avNormal={1},colNormal={2},diff={3}",
                                //             m_controllingPrim.LocalID, directionFacing, touchNormal,
                                //             Math.Abs(OMV.Vector3.Distance(directionFacing, touchNormal)) );
                                if ((Math.Abs(directionFacing.Z) * PIOver2) < BSParam.AvatarStepAngle
                                    && (Math.Abs(touchNormal.Z) * PIOver2) < BSParam.AvatarStepAngle)
                                {
                                    // The normal should be our contact point to the object so it is pointing away
                                    //    thus the difference between our facing orientation and the normal should be small.
                                    float diff = Math.Abs(OMV.Vector3.Distance(directionFacing, touchNormal));
                                    if (diff < BSParam.AvatarStepApproachFactor)
                                    {
                                        if (highestTouchPosition.Z < touchPosition.Z)
                                            highestTouchPosition = touchPosition;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            m_walkingUpStairs = 0;
            // If there is a good step sensing, move the avatar over the step.
            if (highestTouchPosition != OMV.Vector3.Zero)
            {
                // Remember that we are going up stairs. This is needed because collisions
                //    will stop when we move up so this smoothes out that effect.
                m_walkingUpStairs = BSParam.AvatarStepSmoothingSteps;

                m_lastStepUp = highestTouchPosition.Z - nearFeetHeightMin;
                ret = ComputeStairCorrection(m_lastStepUp);
                m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs,touchPos={1},nearFeetMin={2},ret={3}",
                        m_controllingPrim.LocalID, highestTouchPosition, nearFeetHeightMin, ret);
            }
        }
        else
        {
            // If we used to be going up stairs but are not now, smooth the case where collision goes away while
            //    we are bouncing up the stairs.
            if (m_walkingUpStairs > 0)
            {
                m_walkingUpStairs--;
                ret = ComputeStairCorrection(m_lastStepUp);
            }
        }

        return ret;
    }

    private OMV.Vector3 ComputeStairCorrection(float stepUp)
    {
        OMV.Vector3 ret = OMV.Vector3.Zero;
        OMV.Vector3 displacement = OMV.Vector3.Zero;

        if (stepUp > 0f)
        {
            // Found the stairs contact point. Push up a little to raise the character.
            if (BSParam.AvatarStepForceFactor > 0f)
            {
                float upForce = stepUp * m_controllingPrim.Mass * BSParam.AvatarStepForceFactor;
                ret = new OMV.Vector3(0f, 0f, upForce);
            }

            // Also move the avatar up for the new height
            if (BSParam.AvatarStepUpCorrectionFactor > 0f)
            {
                // Move the avatar up related to the height of the collision
                displacement = new OMV.Vector3(0f, 0f, stepUp * BSParam.AvatarStepUpCorrectionFactor);
                m_controllingPrim.ForcePosition = m_controllingPrim.RawPosition + displacement;
            }
            else
            {
                if (BSParam.AvatarStepUpCorrectionFactor < 0f)
                {
                    // Move the avatar up about the specified step height
                    displacement = new OMV.Vector3(0f, 0f, BSParam.AvatarStepHeight);
                    m_controllingPrim.ForcePosition = m_controllingPrim.RawPosition + displacement;
                }
            }
            m_physicsScene.DetailLog("{0},BSCharacter.WalkUpStairs.ComputeStairCorrection,stepUp={1},isp={2},force={3}",
                                        m_controllingPrim.LocalID, stepUp, displacement, ret);

        }
        return ret;
    }
}
}


