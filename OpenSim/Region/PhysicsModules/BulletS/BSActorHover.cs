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
public class BSActorHover : BSActor
{
    private BSFMotor m_hoverMotor;

    public BSActorHover(BSScene physicsScene, BSPhysObject pObj, string actorName)
        : base(physicsScene, pObj, actorName)
    {
        m_hoverMotor = null;
        m_physicsScene.DetailLog("{0},BSActorHover,constructor", m_controllingPrim.LocalID);
    }

    // BSActor.isActive
    public override bool isActive
    {
        get { return Enabled; }
    }

    // Release any connections and resources used by the actor.
    // BSActor.Dispose()
    public override void Dispose()
    {
        Enabled = false;
        DeactivateHover();
    }

    // Called when physical parameters (properties set in Bullet) need to be re-applied.
    // Called at taint-time.
    // BSActor.Refresh()
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

    // The object's physical representation is being rebuilt so pick up any physical dependencies (constraints, ...).
    //     Register a prestep action to restore physical requirements before the next simulation step.
    // Called at taint-time.
    // BSActor.RemoveDependencies()
    public override void RemoveDependencies()
    {
        // Nothing to do for the hoverer since it is all software at pre-step action time.
    }

    // If a hover motor has not been created, create one and start the hovering.
    private void ActivateHover()
    {
        if (m_hoverMotor == null)
        {
            // Turning the target on
            m_hoverMotor = new BSFMotor("BSActorHover",
                                        m_controllingPrim.HoverTau,               // timeScale
                                        BSMotor.Infinite,           // decay time scale
                                        1f                          // efficiency
            );
            m_hoverMotor.SetTarget(ComputeCurrentHoverHeight());
            m_hoverMotor.SetCurrent(m_controllingPrim.RawPosition.Z);
            m_hoverMotor.PhysicsScene = m_physicsScene; // DEBUG DEBUG so motor will output detail log messages.

            m_physicsScene.BeforeStep += Hoverer;
        }
    }

    private void DeactivateHover()
    {
        if (m_hoverMotor != null)
        {
            m_physicsScene.BeforeStep -= Hoverer;
            m_hoverMotor = null;
        }
    }

    // Called just before the simulation step. Update the vertical position for hoverness.
    private void Hoverer(float timeStep)
    {
        // Don't do hovering while the object is selected.
        if (!isActive)
            return;

        m_hoverMotor.SetCurrent(m_controllingPrim.RawPosition.Z);
        m_hoverMotor.SetTarget(ComputeCurrentHoverHeight());
        float targetHeight = m_hoverMotor.Step(timeStep);

        // 'targetHeight' is where we'd like the Z of the prim to be at this moment.
        // Compute the amount of force to push us there.
        float moveForce = (targetHeight - m_controllingPrim.RawPosition.Z) * m_controllingPrim.RawMass;
        // Undo anything the object thinks it's doing at the moment
        moveForce = -m_controllingPrim.RawVelocity.Z * m_controllingPrim.Mass;

        m_physicsScene.PE.ApplyCentralImpulse(m_controllingPrim.PhysBody, new OMV.Vector3(0f, 0f, moveForce));
        m_physicsScene.DetailLog("{0},BSPrim.Hover,move,targHt={1},moveForce={2},mass={3}",
                        m_controllingPrim.LocalID, targetHeight, moveForce, m_controllingPrim.RawMass);
    }

    // Based on current position, determine what we should be hovering at now.
    // Must recompute often. What if we walked offa cliff>
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
