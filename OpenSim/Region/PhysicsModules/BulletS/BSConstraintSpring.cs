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

namespace OpenSim.Region.PhysicsModule.BulletS
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

        PhysicsScene.DetailLog("{0},BSConstraintSpring,create,wID={1}, rID={2}, rBody={3}, cID={4}, cBody={5}",
                            obj1.ID, world.worldID, obj1.ID, obj1.AddrString, obj2.ID, obj2.AddrString);
        PhysicsScene.DetailLog("{0},BSConstraintSpring,create,  f1Loc={1},f1Rot={2},f2Loc={3},f2Rot={4},usefA={5},disCol={6}",
                    m_body1.ID, frame1Loc, frame1Rot, frame2Loc, frame2Rot, useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies);
    }

    public bool SetAxisEnable(int pIndex, bool pAxisEnable)
    {
        PhysicsScene.DetailLog("{0},BSConstraintSpring.SetEnable,obj1ID={1},obj2ID={2},indx={3},enable={4}",
                                        m_body1.ID, m_body1.ID, m_body2.ID, pIndex, pAxisEnable);
        PhysicsScene.PE.SpringEnable(m_constraint, pIndex, BSParam.NumericBool(pAxisEnable));
        return true;
    }

    public bool SetStiffness(int pIndex, float pStiffness)
    {
        PhysicsScene.DetailLog("{0},BSConstraintSpring.SetStiffness,obj1ID={1},obj2ID={2},indx={3},stiff={4}",
                                        m_body1.ID, m_body1.ID, m_body2.ID, pIndex, pStiffness);
        PhysicsScene.PE.SpringSetStiffness(m_constraint, pIndex, pStiffness);
        return true;
    }

    public bool SetDamping(int pIndex, float pDamping)
    {
        PhysicsScene.DetailLog("{0},BSConstraintSpring.SetDamping,obj1ID={1},obj2ID={2},indx={3},damp={4}",
                                        m_body1.ID, m_body1.ID, m_body2.ID, pIndex, pDamping);
        PhysicsScene.PE.SpringSetDamping(m_constraint, pIndex, pDamping);
        return true;
    }

    public bool SetEquilibriumPoint(int pIndex, float pEqPoint)
    {
        PhysicsScene.DetailLog("{0},BSConstraintSpring.SetEquilibriumPoint,obj1ID={1},obj2ID={2},indx={3},eqPoint={4}",
                                        m_body1.ID, m_body1.ID, m_body2.ID, pIndex, pEqPoint);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, pIndex, pEqPoint);
        return true;
    }

    public bool SetEquilibriumPoint(Vector3 linearEq, Vector3 angularEq)
    {
        PhysicsScene.DetailLog("{0},BSConstraintSpring.SetEquilibriumPoint,obj1ID={1},obj2ID={2},linearEq={3},angularEq={4}",
                                        m_body1.ID, m_body1.ID, m_body2.ID, linearEq, angularEq);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 0, linearEq.X);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 1, linearEq.Y);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 2, linearEq.Z);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 3, angularEq.X);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 4, angularEq.Y);
        PhysicsScene.PE.SpringSetEquilibriumPoint(m_constraint, 5, angularEq.Z);
        return true;
    }

}

}