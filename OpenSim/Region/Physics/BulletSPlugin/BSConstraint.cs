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

public abstract class BSConstraint : IDisposable
{
    protected BulletSim m_world;
    protected BulletBody m_body1;
    protected BulletBody m_body2;
    protected BulletConstraint m_constraint;
    protected bool m_enabled = false;

    public BSConstraint()
    {
    }

    public virtual void Dispose()
    {
        if (m_enabled)
        {
            // BulletSimAPI.RemoveConstraint(m_world.ID, m_body1.ID, m_body2.ID);
            bool success = BulletSimAPI.DestroyConstraint2(m_world.Ptr, m_constraint.Ptr);
            m_world.scene.DetailLog("{0},BSConstraint.Dispose,taint,body1={1},body2={2},success={3}", BSScene.DetailLogZero, m_body1.ID, m_body2.ID, success);
            m_constraint.Ptr = System.IntPtr.Zero;
            m_enabled = false;
        }
    }

    public BulletBody Body1 { get { return m_body1; } }
    public BulletBody Body2 { get { return m_body2; } }

    public virtual bool SetLinearLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
            ret = BulletSimAPI.SetLinearLimits2(m_constraint.Ptr, low, high);
        return ret;
    }

    public virtual bool SetAngularLimits(Vector3 low, Vector3 high)
    {
        bool ret = false;
        if (m_enabled)
            ret = BulletSimAPI.SetAngularLimits2(m_constraint.Ptr, low, high);
        return ret;
    }

    public virtual bool CalculateTransforms()
    {
        bool ret = false;
        if (m_enabled)
        {
            // Recompute the internal transforms
            BulletSimAPI.CalculateTransforms2(m_constraint.Ptr);
            ret = true;
        }
        return ret;
    }

    // Reset this constraint making sure it has all its internal structures
    //    recomputed and is enabled and ready to go.
    public virtual bool RecomputeConstraintVariables(float mass)
    {
        bool ret = false;
        if (m_enabled)
        {
            ret = CalculateTransforms();
            if (ret)
            {
                // m_world.scene.PhysicsLogging.Write("{0},BSConstraint.RecomputeConstraintVariables,taint,enabling,A={1},B={2}",
                //                 BSScene.DetailLogZero, Body1.ID, Body2.ID);
                BulletSimAPI.SetConstraintEnable2(m_constraint.Ptr, m_world.scene.NumericBool(true));
            }
            else
            {
                m_world.scene.Logger.ErrorFormat("[BULLETSIM CONSTRAINT] CalculateTransforms failed. A={0}, B={1}", Body1.ID, Body2.ID);
            }
        }
        return ret;
    }
}
}
