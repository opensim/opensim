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

using OMV = OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSActorLockAxis : BSActor
{
    bool TryExperimentalLockAxisCode = false;
    BSConstraint LockAxisConstraint = null;

    public BSActorLockAxis(BSScene physicsScene, BSPhysObject pObj, string actorName)
        : base(physicsScene, pObj,actorName)
    {
        LockAxisConstraint = null;
    }

    // BSActor.isActive
    public override bool isActive
    {
        get { return Enabled && Prim.IsPhysicallyActive; }
    }

    // Release any connections and resources used by the actor.
    // BSActor.Release()
    public override void Release()
    {
        RemoveAxisLockConstraint();
    }

    // Called when physical parameters (properties set in Bullet) need to be re-applied.
    // Called at taint-time.
    // BSActor.Refresh()
    public override void Refresh()
    {
        // If all the axis are free, we don't need to exist
        if (Prim.LockedAxis == Prim.LockedAxisFree)
        {
            Prim.PhysicalActors.RemoveAndRelease(ActorName);
            return;
        }
        // If the object is physically active, add the axis locking constraint
        if (Enabled
                && Prim.IsPhysicallyActive
                && TryExperimentalLockAxisCode
                && Prim.LockedAxis != Prim.LockedAxisFree)
        {
            if (LockAxisConstraint != null)
                AddAxisLockConstraint();
        }
        else
        {
            RemoveAxisLockConstraint();
        }
    }

    // The object's physical representation is being rebuilt so pick up any physical dependencies (constraints, ...).
    //     Register a prestep action to restore physical requirements before the next simulation step.
    // Called at taint-time.
    // BSActor.RemoveBodyDependencies()
    public override void RemoveBodyDependencies()
    {
        if (LockAxisConstraint != null)
        {
            // If a constraint is set up, remove it from the physical scene
            RemoveAxisLockConstraint();
            // Schedule a call before the next simulation step to restore the constraint.
            PhysicsScene.PostTaintObject(Prim.LockedAxisActorName, Prim.LocalID, delegate()
            {
                Refresh();
            });
        }
    }

    private void AddAxisLockConstraint()
    {
        // Lock that axis by creating a 6DOF constraint that has one end in the world and
        //    the other in the object.
        // http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?p=20817
        // http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?p=26380

        // Remove any existing axis constraint (just to be sure)
        RemoveAxisLockConstraint();

        BSConstraint6Dof axisConstrainer = new BSConstraint6Dof(PhysicsScene.World, Prim.PhysBody, 
                            OMV.Vector3.Zero, OMV.Quaternion.Inverse(Prim.RawOrientation),
                            true /* useLinearReferenceFrameB */, true /* disableCollisionsBetweenLinkedBodies */);
        LockAxisConstraint = axisConstrainer;
        PhysicsScene.Constraints.AddConstraint(LockAxisConstraint);

        // The constraint is tied to the world and oriented to the prim.

        // Free to move linearly
        OMV.Vector3 linearLow = OMV.Vector3.Zero;
        OMV.Vector3 linearHigh = PhysicsScene.TerrainManager.DefaultRegionSize;
        axisConstrainer.SetLinearLimits(linearLow, linearHigh);

        // Angular with some axis locked
        float f2PI = (float)Math.PI * 2f;
        OMV.Vector3 angularLow = new OMV.Vector3(-f2PI, -f2PI, -f2PI);
        OMV.Vector3 angularHigh = new OMV.Vector3(f2PI, f2PI, f2PI);
        if (Prim.LockedAxis.X != 1f)
        {
            angularLow.X = 0f;
            angularHigh.X = 0f;
        }
        if (Prim.LockedAxis.Y != 1f)
        {
            angularLow.Y = 0f;
            angularHigh.Y = 0f;
        }
        if (Prim.LockedAxis.Z != 1f)
        {
            angularLow.Z = 0f;
            angularHigh.Z = 0f;
        }
        axisConstrainer.SetAngularLimits(angularLow, angularHigh);

        PhysicsScene.DetailLog("{0},BSPrim.LockAngularMotion,create,linLow={1},linHi={2},angLow={3},angHi={4}",
                                    Prim.LocalID, linearLow, linearHigh, angularLow, angularHigh);

        // Constants from one of the posts mentioned above and used in Bullet's ConstraintDemo.
        axisConstrainer.TranslationalLimitMotor(true /* enable */, 5.0f, 0.1f);

        axisConstrainer.RecomputeConstraintVariables(Prim.RawMass);
    }

    private void RemoveAxisLockConstraint()
    {
        if (LockAxisConstraint != null)
        {
            PhysicsScene.Constraints.RemoveAndDestroyConstraint(LockAxisConstraint);
            LockAxisConstraint = null;
            PhysicsScene.DetailLog("{0},BSPrim.CleanUpLockAxisPhysicals,destroyingConstraint", Prim.LocalID);
        }
    }
}
}
