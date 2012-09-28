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

public class BS6DofConstraint : BSConstraint
{
    private static string LogHeader = "[BULLETSIM 6DOF CONSTRAINT]";

    public override ConstraintType Type { get { return ConstraintType.D6_CONSTRAINT_TYPE; } }

    // Create a btGeneric6DofConstraint
    public BS6DofConstraint(BulletSim world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1, Quaternion frame1rot,
                    Vector3 frame2, Quaternion frame2rot,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
    {
        m_world = world;
        m_body1 = obj1;
        m_body2 = obj2;
        m_constraint = new BulletConstraint(
                            BulletSimAPI.Create6DofConstraint2(m_world.ptr, m_body1.ptr, m_body2.ptr,
                                frame1, frame1rot,
                                frame2, frame2rot,
                                useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
        m_enabled = true;
        world.physicsScene.DetailLog("{0},BS6DofConstraint,createFrame,wID={1}, rID={2}, rBody={3}, cID={4}, cBody={5}",
                            BSScene.DetailLogZero, world.worldID,
                            obj1.ID, obj1.ptr.ToString("X"), obj2.ID, obj2.ptr.ToString("X"));
    }

    public BS6DofConstraint(BulletSim world, BulletBody obj1, BulletBody obj2,
                    Vector3 joinPoint,
                    bool useLinearReferenceFrameA, bool disableCollisionsBetweenLinkedBodies)
    {
        m_world = world;
        m_body1 = obj1;
        m_body2 = obj2;
        if (obj1.ptr == IntPtr.Zero || obj2.ptr == IntPtr.Zero)
        {
            world.physicsScene.DetailLog("{0},BS6DOFConstraint,badBodyPtr,wID={1}, rID={2}, rBody={3}, cID={4}, cBody={5}",
                            BSScene.DetailLogZero, world.worldID,
                            obj1.ID, obj1.ptr.ToString("X"), obj2.ID, obj2.ptr.ToString("X"));
            world.physicsScene.Logger.ErrorFormat("{0} Attempt to build 6DOF constraint with missing bodies: wID={1}, rID={2}, rBody={3}, cID={4}, cBody={5}",
                            "[BULLETSIM 6DOF CONSTRAINT]", world.worldID,
                            obj1.ID, obj1.ptr.ToString("X"), obj2.ID, obj2.ptr.ToString("X"));
            m_enabled = false;
        }
        else
        {
            m_constraint = new BulletConstraint(
                                BulletSimAPI.Create6DofConstraintToPoint2(m_world.ptr, m_body1.ptr, m_body2.ptr,
                                    joinPoint,
                                    useLinearReferenceFrameA, disableCollisionsBetweenLinkedBodies));
            world.physicsScene.DetailLog("{0},BS6DofConstraint,createMidPoint,wID={1}, csrt={2}, rID={3}, rBody={4}, cID={5}, cBody={6}",
                                BSScene.DetailLogZero, world.worldID, m_constraint.ptr.ToString("X"),
                                obj1.ID, obj1.ptr.ToString("X"), obj2.ID, obj2.ptr.ToString("X"));
            if (m_constraint.ptr == IntPtr.Zero)
            {
                world.physicsScene.Logger.ErrorFormat("{0} Failed creation of 6Dof constraint. rootID={1}, childID={2}",
                                LogHeader, obj1.ID, obj2.ID);
                m_enabled = false;
            }
            else
            {
                m_enabled = true;
            }
        }
    }

    public bool SetFrames(Vector3 frameA, Quaternion frameArot, Vector3 frameB, Quaternion frameBrot)
    {
        bool ret = false;
        if (m_enabled)
        {
            BulletSimAPI.SetFrames2(m_constraint.ptr, frameA, frameArot, frameB, frameBrot);
            ret = true;
        }
        return ret;
    }

    public bool SetCFMAndERP(float cfm, float erp)
    {
        bool ret = false;
        if (m_enabled)
        {
            BulletSimAPI.SetConstraintParam2(m_constraint.ptr, ConstraintParams.BT_CONSTRAINT_STOP_CFM, cfm, ConstraintParamAxis.AXIS_ALL);
            BulletSimAPI.SetConstraintParam2(m_constraint.ptr, ConstraintParams.BT_CONSTRAINT_STOP_ERP, erp, ConstraintParamAxis.AXIS_ALL);
            BulletSimAPI.SetConstraintParam2(m_constraint.ptr, ConstraintParams.BT_CONSTRAINT_CFM, cfm, ConstraintParamAxis.AXIS_ALL);
            ret = true;
        }
        return ret;
    }

    public bool UseFrameOffset(bool useOffset)
    {
        bool ret = false;
        float onOff = useOffset ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse;
        if (m_enabled)
            ret = BulletSimAPI.UseFrameOffset2(m_constraint.ptr, onOff);
        return ret;
    }

    public bool TranslationalLimitMotor(bool enable, float targetVelocity, float maxMotorForce)
    {
        bool ret = false;
        float onOff = enable ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse;
        if (m_enabled)
            ret = BulletSimAPI.TranslationalLimitMotor2(m_constraint.ptr, onOff, targetVelocity, maxMotorForce);
        return ret;
    }

    public bool SetBreakingImpulseThreshold(float threshold)
    {
        bool ret = false;
        if (m_enabled)
            ret = BulletSimAPI.SetBreakingImpulseThreshold2(m_constraint.ptr, threshold);
        return ret;
    }
}
}
