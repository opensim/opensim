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

public class BSConstraint : IDisposable
{
    private BulletSim m_world;
    private BulletBody m_body1;
    private BulletBody m_body2;
    private BulletConstraint m_constraint;
    private bool m_enabled = false;

    public BSConstraint(BulletSim world, BulletBody obj1, BulletBody obj2,
                    Vector3 frame1, Quaternion frame1rot,
                    Vector3 frame2, Quaternion frame2rot
        )
    {
        m_world = world;
        m_body1 = obj1;
        m_body2 = obj2;
        m_constraint = new BulletConstraint(BulletSimAPI.CreateConstraint2(m_world.Ptr, m_body1.Ptr, m_body2.Ptr,
                            frame1, frame1rot,
                            frame2, frame2rot,
                            true /*useLinearReferenceFrameA*/, true /*disableCollisionsBetweenLinkedBodies*/));
        m_enabled = true;
    }

    public void Dispose()
    {
        if (m_enabled)
        {
            // BulletSimAPI.RemoveConstraint(m_world.ID, m_body1.ID, m_body2.ID);
            BulletSimAPI.DestroyConstraint2(m_world.Ptr, m_constraint.Ptr);
            m_enabled = false;
        }
    }

    public BulletBody Body1 { get { return m_body1; } }
    public BulletBody Body2 { get { return m_body2; } }

    public bool SetLinearLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
            ret = BulletSimAPI.SetLinearLimits2(m_constraint.Ptr, low, high);
        return ret;
    }

    public bool SetAngularLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
            ret = BulletSimAPI.SetAngularLimits2(m_constraint.Ptr, low, high);
        return ret;
    }

    public bool SetCFMAndERP(float cfm, float erp)
    {
        bool ret = true;
        BulletSimAPI.SetConstraintParam2(m_constraint.Ptr, ConstraintParams.BT_CONSTRAINT_STOP_CFM, cfm, ConstraintParamAxis.AXIS_ALL);
        BulletSimAPI.SetConstraintParam2(m_constraint.Ptr, ConstraintParams.BT_CONSTRAINT_STOP_ERP, erp, ConstraintParamAxis.AXIS_ALL);
        BulletSimAPI.SetConstraintParam2(m_constraint.Ptr, ConstraintParams.BT_CONSTRAINT_CFM, cfm, ConstraintParamAxis.AXIS_ALL);
        return ret;
    }

    public bool UseFrameOffset(bool useOffset)
    {
        bool ret = false;
        float onOff = useOffset ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse;
        if (m_enabled)
            ret = BulletSimAPI.UseFrameOffset2(m_constraint.Ptr, onOff);
        return ret;
    }

    public bool TranslationalLimitMotor(bool enable, float targetVelocity, float maxMotorForce)
    {
        bool ret = false;
        float onOff = enable ? ConfigurationParameters.numericTrue : ConfigurationParameters.numericFalse;
        if (m_enabled)
            ret = BulletSimAPI.TranslationalLimitMotor2(m_constraint.Ptr, onOff, targetVelocity, maxMotorForce);
        return ret;
    }

    public bool CalculateTransforms()
    {
        bool ret = false;
        if (m_enabled)
        {
            BulletSimAPI.CalculateTransforms2(m_constraint.Ptr);
            ret = true;
        }
        return ret;
    }
}
}
