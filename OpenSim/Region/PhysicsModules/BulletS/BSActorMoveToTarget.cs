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

using OpenSim.Region.PhysicsModules.SharedBase;

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public class BSActorMoveToTarget : BSActor
{
    private BSVMotor m_targetMotor;

    public BSActorMoveToTarget(BSScene physicsScene, BSPhysObject pObj, string actorName)
        : base(physicsScene, pObj, actorName)
    {
        m_targetMotor = null;
        m_physicsScene.DetailLog("{0},BSActorMoveToTarget,constructor", m_controllingPrim.LocalID);
    }

    // BSActor.isActive
    public override bool isActive
    {
        // MoveToTarget only works on physical prims
        get { return Enabled && m_controllingPrim.IsPhysicallyActive; }
    }

    // Release any connections and resources used by the actor.
    // BSActor.Dispose()
    public override void Dispose()
    {
        Enabled = false;
        DeactivateMoveToTarget();
    }

    // Called when physical parameters (properties set in Bullet) need to be re-applied.
    // Called at taint-time.
    // BSActor.Refresh()
    public override void Refresh()
    {
        m_physicsScene.DetailLog("{0},BSActorMoveToTarget,refresh,enabled={1},active={2},target={3},tau={4}",
            m_controllingPrim.LocalID, Enabled, m_controllingPrim.MoveToTargetActive,
            m_controllingPrim.MoveToTargetTarget, m_controllingPrim.MoveToTargetTau );

        // If not active any more...
        if (!m_controllingPrim.MoveToTargetActive)
        {
            Enabled = false;
        }

        if (isActive)
        {
            ActivateMoveToTarget();
        }
        else
        {
            DeactivateMoveToTarget();
        }
    }

    // The object's physical representation is being rebuilt so pick up any physical dependencies (constraints, ...).
    //     Register a prestep action to restore physical requirements before the next simulation step.
    // Called at taint-time.
    // BSActor.RemoveDependencies()
    public override void RemoveDependencies()
    {
        // Nothing to do for the moveToTarget since it is all software at pre-step action time.
    }

    // If a hover motor has not been created, create one and start the hovering.
    private void ActivateMoveToTarget()
    {
        if (m_targetMotor == null)
        {
            // We're taking over after this.
            m_controllingPrim.ZeroMotion(true);

            /* Someday use the PID controller
            m_targetMotor = new BSPIDVMotor("BSActorMoveToTarget-" + m_controllingPrim.LocalID.ToString());
            m_targetMotor.TimeScale = m_controllingPrim.MoveToTargetTau;
            m_targetMotor.Efficiency = 1f;
             */
            m_targetMotor = new BSVMotor("BSActorMoveToTarget-" + m_controllingPrim.LocalID.ToString(),
                                        m_controllingPrim.MoveToTargetTau,  // timeScale
                                        BSMotor.Infinite,                   // decay time scale
                                        1f                                  // efficiency
            );
            m_targetMotor.PhysicsScene = m_physicsScene; // DEBUG DEBUG so motor will output detail log messages.
            m_targetMotor.SetTarget(m_controllingPrim.MoveToTargetTarget);
            m_targetMotor.SetCurrent(m_controllingPrim.RawPosition);

            // m_physicsScene.BeforeStep += Mover;
            m_physicsScene.BeforeStep += Mover2;
        }
        else
        {
            // If already allocated, make sure the target and other paramters are current
            m_targetMotor.SetTarget(m_controllingPrim.MoveToTargetTarget);
            m_targetMotor.SetCurrent(m_controllingPrim.RawPosition);
        }
    }

    private void DeactivateMoveToTarget()
    {
        if (m_targetMotor != null)
        {
            // m_physicsScene.BeforeStep -= Mover;
            m_physicsScene.BeforeStep -= Mover2;
            m_targetMotor = null;
        }
    }

    // Origional mover that set the objects position to move to the target.
    // The problem was that gravity would keep trying to push the object down so
    //    the overall downward velocity would increase to infinity.
    // Called just before the simulation step.
    private void Mover(float timeStep)
    {
        // Don't do hovering while the object is selected.
        if (!isActive)
            return;

        OMV.Vector3 origPosition = m_controllingPrim.RawPosition;     // DEBUG DEBUG (for printout below)

        // 'movePosition' is where we'd like the prim to be at this moment.
        OMV.Vector3 movePosition = m_controllingPrim.RawPosition + m_targetMotor.Step(timeStep);

        // If we are very close to our target, turn off the movement motor.
        if (m_targetMotor.ErrorIsZero())
        {
            m_physicsScene.DetailLog("{0},BSActorMoveToTarget.Mover,zeroMovement,movePos={1},pos={2},mass={3}",
                                    m_controllingPrim.LocalID, movePosition, m_controllingPrim.RawPosition, m_controllingPrim.Mass);
            m_controllingPrim.ForcePosition = m_targetMotor.TargetValue;
            m_controllingPrim.ForceVelocity = OMV.Vector3.Zero;
            // Setting the position does not cause the physics engine to generate a property update. Force it.
            m_physicsScene.PE.PushUpdate(m_controllingPrim.PhysBody);
        }
        else
        {
            m_controllingPrim.ForcePosition = movePosition;
            // Setting the position does not cause the physics engine to generate a property update. Force it.
            m_physicsScene.PE.PushUpdate(m_controllingPrim.PhysBody);
        }
        m_physicsScene.DetailLog("{0},BSActorMoveToTarget.Mover,move,fromPos={1},movePos={2}",
                                        m_controllingPrim.LocalID, origPosition, movePosition);
    }

    // Version of mover that applies forces to move the physical object to the target.
    // Also overcomes gravity so the object doesn't just drop to the ground.
    // Called just before the simulation step.
    private void Mover2(float timeStep)
    {
        // Don't do hovering while the object is selected.
        if (!isActive)
            return;

        OMV.Vector3 origPosition = m_controllingPrim.RawPosition;     // DEBUG DEBUG (for printout below)
        OMV.Vector3 addedForce = OMV.Vector3.Zero;

        // CorrectionVector is the movement vector required this step
        OMV.Vector3 correctionVector = m_targetMotor.Step(timeStep, m_controllingPrim.RawPosition);

        // If we are very close to our target, turn off the movement motor.
        if (m_targetMotor.ErrorIsZero())
        {
            m_physicsScene.DetailLog("{0},BSActorMoveToTarget.Mover3,zeroMovement,pos={1},mass={2}",
                                    m_controllingPrim.LocalID, m_controllingPrim.RawPosition, m_controllingPrim.Mass);
            m_controllingPrim.ForcePosition = m_targetMotor.TargetValue;
            m_controllingPrim.ForceVelocity = OMV.Vector3.Zero;
            // Setting the position does not cause the physics engine to generate a property update. Force it.
            m_physicsScene.PE.PushUpdate(m_controllingPrim.PhysBody);
        }
        else
        {
            // First force to move us there -- the motor return a timestep scaled value.
            addedForce = correctionVector / timeStep;
            // Remove the existing velocity (only the moveToTarget force counts)
            addedForce -= m_controllingPrim.RawVelocity;
            // Overcome gravity.
            addedForce -= m_controllingPrim.Gravity;

            // Add enough force to overcome the mass of the object
            addedForce *= m_controllingPrim.Mass;

            m_controllingPrim.AddForce(true /* inTaintTime */, addedForce);
        }
        m_physicsScene.DetailLog("{0},BSActorMoveToTarget.Mover3,move,fromPos={1},addedForce={2}",
                                        m_controllingPrim.LocalID, origPosition, addedForce);
    }
}
}
