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
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{

public sealed class BSConstraintSpring : BSConstraint6Dof
{
    public override ConstraintType Type { get { return ConstraintType.D6_SPRING_CONSTRAINT_TYPE; } }

    public BSConstraintSpring(BulletWorld world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1Loc, Quaternion frame1Rot,
                    Vector3 frame2Loc, Quaternion frame2Rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
        :base(world, obj1, obj2)
    {
        m_constraint = PhysicsScene.PE.Create6DofSpringConstraint(world, obj1, obj2,
                                frame1Loc, frame1Rot, frame2Loc, frame2Rot,
                                useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies);
        m_enabled = true;

        world.physicsScene.DetailLog("{0},BSConstraintSpring,createFrame,wID={1}, rID={2}, rBody={3}, cID={4}, cBody={5}",
                            BSScene.DetailLogZero, world.worldID,
                            obj1.ID, obj1.AddrString, obj2.ID, obj2.AddrString);
    }

    public bool SetEnable(int index, bool axisEnable)
    {
        bool ret = false;

        return ret;
    }

    public bool SetStiffness(int index, float stiffness)
    {
        bool ret = false;

        return ret;
    }

    public bool SetDamping(int index, float damping)
    {
        bool ret = false;

        return ret;
    }

    public bool SetEquilibriumPoint(int index, float eqPoint)
    {
        bool ret = false;

        return ret;
    }

}

}