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

using OMV = OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSLinksetConstraints : BSLinkset
{
    // private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINTS]";

    public class BSLinkInfoConstraint : BSLinkInfo
    {
        public ConstraintType constraintType;
        public BSConstraint constraint;
        public OMV.Vector3 linearLimitLow;
        public OMV.Vector3 linearLimitHigh;
        public OMV.Vector3 angularLimitLow;
        public OMV.Vector3 angularLimitHigh;
        public bool useFrameOffset;
        public bool enableTransMotor;
        public float transMotorMaxVel;
        public float transMotorMaxForce;
        public float cfm;
        public float erp;
        public float solverIterations;
        //
        public OMV.Vector3 frameInAloc;
        public OMV.Quaternion frameInArot;
        public OMV.Vector3 frameInBloc;
        public OMV.Quaternion frameInBrot;
        public bool useLinearReferenceFrameA;
        // Spring
        public bool[] springAxisEnable;
        public float[] springDamping;
        public float[] springStiffness;
        public OMV.Vector3 springLinearEquilibriumPoint;
        public OMV.Vector3 springAngularEquilibriumPoint;

        public BSLinkInfoConstraint(BSPrimLinkable pMember)
            : base(pMember)
        {
            constraint = null;
            ResetLink();
            member.PhysScene.DetailLog("{0},BSLinkInfoConstraint.creation", member.LocalID);
        }

        // Set all the parameters for this constraint to a fixed, non-movable constraint.
        public override void ResetLink()
        {
            // constraintType = ConstraintType.D6_CONSTRAINT_TYPE;
            constraintType = ConstraintType.BS_FIXED_CONSTRAINT_TYPE;
            linearLimitLow = OMV.Vector3.Zero;
            linearLimitHigh = OMV.Vector3.Zero;
            angularLimitLow = OMV.Vector3.Zero;
            angularLimitHigh = OMV.Vector3.Zero;
            useFrameOffset = BSParam.LinkConstraintUseFrameOffset;
            enableTransMotor = BSParam.LinkConstraintEnableTransMotor;
            transMotorMaxVel = BSParam.LinkConstraintTransMotorMaxVel;
            transMotorMaxForce = BSParam.LinkConstraintTransMotorMaxForce;
            cfm = BSParam.LinkConstraintCFM;
            erp = BSParam.LinkConstraintERP;
            solverIterations = BSParam.LinkConstraintSolverIterations;
            frameInAloc = OMV.Vector3.Zero;
            frameInArot = OMV.Quaternion.Identity;
            frameInBloc = OMV.Vector3.Zero;
            frameInBrot = OMV.Quaternion.Identity;
            useLinearReferenceFrameA = true;
            springAxisEnable = new bool[6];
            springDamping = new float[6];
            springStiffness = new float[6];
            for (int ii = 0; ii < springAxisEnable.Length; ii++)
            {
                springAxisEnable[ii] = false;
                springDamping[ii] = BSAPITemplate.SPRING_NOT_SPECIFIED;
                springStiffness[ii] = BSAPITemplate.SPRING_NOT_SPECIFIED;
            }
            springLinearEquilibriumPoint = OMV.Vector3.Zero;
            springAngularEquilibriumPoint = OMV.Vector3.Zero;
            member.PhysScene.DetailLog("{0},BSLinkInfoConstraint.ResetLink", member.LocalID);
        }

        // Given a constraint, apply the current constraint parameters to same.
        public override void SetLinkParameters(BSConstraint constrain)
        {
            member.PhysScene.DetailLog("{0},BSLinkInfoConstraint.SetLinkParameters,type={1}", member.LocalID, constraintType);
            switch (constraintType)
            {
                case ConstraintType.BS_FIXED_CONSTRAINT_TYPE:
                case ConstraintType.D6_CONSTRAINT_TYPE:
                    BSConstraint6Dof constrain6dof = constrain as BSConstraint6Dof;
                    if (constrain6dof != null)
                    {
                        // NOTE: D6_SPRING_CONSTRAINT_TYPE should be updated if you change any of this code.
                        // zero linear and angular limits makes the objects unable to move in relation to each other
                        constrain6dof.SetLinearLimits(linearLimitLow, linearLimitHigh);
                        constrain6dof.SetAngularLimits(angularLimitLow, angularLimitHigh);

                        // tweek the constraint to increase stability
                        constrain6dof.UseFrameOffset(useFrameOffset);
                        constrain6dof.TranslationalLimitMotor(enableTransMotor, transMotorMaxVel, transMotorMaxForce);
                        constrain6dof.SetCFMAndERP(cfm, erp);
                        if (solverIterations != 0f)
                        {
                            constrain6dof.SetSolverIterations(solverIterations);
                        }
                    }
                    break;
                case ConstraintType.D6_SPRING_CONSTRAINT_TYPE:
                    BSConstraintSpring constrainSpring = constrain as BSConstraintSpring;
                    if (constrainSpring != null)
                    {
                        // zero linear and angular limits makes the objects unable to move in relation to each other
                        constrainSpring.SetLinearLimits(linearLimitLow, linearLimitHigh);
                        constrainSpring.SetAngularLimits(angularLimitLow, angularLimitHigh);

                        // tweek the constraint to increase stability
                        constrainSpring.UseFrameOffset(useFrameOffset);
                        constrainSpring.TranslationalLimitMotor(enableTransMotor, transMotorMaxVel, transMotorMaxForce);
                        constrainSpring.SetCFMAndERP(cfm, erp);
                        if (solverIterations != 0f)
                        {
                            constrainSpring.SetSolverIterations(solverIterations);
                        }
                        for (int ii = 0; ii < springAxisEnable.Length; ii++)
                        {
                            constrainSpring.SetAxisEnable(ii, springAxisEnable[ii]);
                            if (springDamping[ii] != BSAPITemplate.SPRING_NOT_SPECIFIED)
                                constrainSpring.SetDamping(ii, springDamping[ii]);
                            if (springStiffness[ii] != BSAPITemplate.SPRING_NOT_SPECIFIED)
                                constrainSpring.SetStiffness(ii, springStiffness[ii]);
                        }
                        constrainSpring.CalculateTransforms();

                        if (springLinearEquilibriumPoint != OMV.Vector3.Zero)
                            constrainSpring.SetEquilibriumPoint(springLinearEquilibriumPoint, springAngularEquilibriumPoint);
                        else
                            constrainSpring.SetEquilibriumPoint(BSAPITemplate.SPRING_NOT_SPECIFIED, BSAPITemplate.SPRING_NOT_SPECIFIED);
                    }
                    break;
                default:
                    break;
            }
        }

        // Return 'true' if the property updates from the physics engine should be reported
        //    to the simulator.
        // If the constraint is fixed, we don't need to report as the simulator and viewer will
        //    report the right things.
        public override bool ShouldUpdateChildProperties()
        {
            bool ret = true;
            if (constraintType == ConstraintType.BS_FIXED_CONSTRAINT_TYPE)
                ret = false;

            return ret;
        }
    }

    public BSLinksetConstraints(BSScene scene, BSPrimLinkable parent) : base(scene, parent)
    {
        LinksetImpl = LinksetImplementation.Constraint;
    }

    private static string LogHeader = "[BULLETSIM LINKSET CONSTRAINT]";

    // When physical properties are changed the linkset needs to recalculate
    //   its internal properties.
    // This is queued in the 'post taint' queue so the
    //   refresh will happen once after all the other taints are applied.
    public override void Refresh(BSPrimLinkable requestor)
    {
        ScheduleRebuild(requestor);
        base.Refresh(requestor);

    }

    private void ScheduleRebuild(BSPrimLinkable requestor)
    {
        DetailLog("{0},BSLinksetConstraint.ScheduleRebuild,,rebuilding={1},hasChildren={2},actuallyScheduling={3}",
                            requestor.LocalID, Rebuilding, HasAnyChildren, (!Rebuilding && HasAnyChildren));

        // When rebuilding, it is possible to set properties that would normally require a rebuild.
        //    If already rebuilding, don't request another rebuild.
        //    If a linkset with just a root prim (simple non-linked prim) don't bother rebuilding.
        lock (this)
        {
            if (!RebuildScheduled)
            {
                if (!Rebuilding && HasAnyChildren)
                {
                    RebuildScheduled = true;
                    // Queue to happen after all the other taint processing
                    m_physicsScene.PostTaintObject("BSLinksetContraints.Refresh", requestor.LocalID, delegate()
                    {
                        if (HasAnyChildren)
                        {
                            // Constraints that have not been changed are not rebuild but make sure
                            //     the constraint of the requestor is rebuilt.
                            PhysicallyUnlinkAChildFromRoot(LinksetRoot, requestor);
                            // Rebuild the linkset and all its constraints.
                            RecomputeLinksetConstraints();
                        }
                        RebuildScheduled = false;
                    });
                }
            }
        }
    }

    // The object is going dynamic (physical). Do any setup necessary
    //     for a dynamic linkset.
    // Only the state of the passed object can be modified. The rest of the linkset
    //     has not yet been fully constructed.
    // Return 'true' if any properties updated on the passed object.
    // Called at taint-time!
    public override bool MakeDynamic(BSPrimLinkable child)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetConstraints.MakeDynamic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        if (IsRoot(child))
        {
            // The root is going dynamic. Rebuild the linkset so parts and mass get computed properly.
            Refresh(LinksetRoot);
        }
        return ret;
    }

    // The object is going static (non-physical). Do any setup necessary for a static linkset.
    // Return 'true' if any properties updated on the passed object.
    // This doesn't normally happen -- OpenSim removes the objects from the physical
    //     world if it is a static linkset.
    // Called at taint-time!
    public override bool MakeStatic(BSPrimLinkable child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetConstraint.MakeStatic,call,IsRoot={1}", child.LocalID, IsRoot(child));
        child.ClearDisplacement();
        if (IsRoot(child))
        {
            // Schedule a rebuild to verify that the root shape is set to the real shape.
            Refresh(LinksetRoot);
        }
        return ret;
    }

    // Called at taint-time!!
    public override void UpdateProperties(UpdatedProperties whichUpdated, BSPrimLinkable pObj)
    {
        // Nothing to do for constraints on property updates
    }

    // Routine called when rebuilding the body of some member of the linkset.
    // Destroy all the constraints have have been made to root and set
    //     up to rebuild the constraints before the next simulation step.
    // Returns 'true' of something was actually removed and would need restoring
    // Called at taint-time!!
    public override bool RemoveDependencies(BSPrimLinkable child)
    {
        bool ret = false;

        DetailLog("{0},BSLinksetConstraint.RemoveDependencies,removeChildrenForRoot,rID={1},rBody={2}",
                                    child.LocalID, LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString);

        lock (m_linksetActivityLock)
        {
            // Just undo all the constraints for this linkset. Rebuild at the end of the step.
            ret = PhysicallyUnlinkAllChildrenFromRoot(LinksetRoot);
            // Cause the constraints, et al to be rebuilt before the next simulation step.
            Refresh(LinksetRoot);
        }
        return ret;
    }

    // ================================================================

    // Add a new child to the linkset.
    // Called while LinkActivity is locked.
    protected override void AddChildToLinkset(BSPrimLinkable child)
    {
        if (!HasChild(child))
        {
            m_children.Add(child, new BSLinkInfoConstraint(child));

            DetailLog("{0},BSLinksetConstraints.AddChildToLinkset,call,child={1}", LinksetRoot.LocalID, child.LocalID);

            // Cause constraints and assorted properties to be recomputed before the next simulation step.
            Refresh(LinksetRoot);
        }
        return;
    }

    // Remove the specified child from the linkset.
    // Safe to call even if the child is not really in my linkset.
    protected override void RemoveChildFromLinkset(BSPrimLinkable child, bool inTaintTime)
    {
        if (m_children.Remove(child))
        {
            BSPrimLinkable rootx = LinksetRoot; // capture the root and body as of now
            BSPrimLinkable childx = child;

            DetailLog("{0},BSLinksetConstraints.RemoveChildFromLinkset,call,rID={1},rBody={2},cID={3},cBody={4}",
                            childx.LocalID,
                            rootx.LocalID, rootx.PhysBody.AddrString,
                            childx.LocalID, childx.PhysBody.AddrString);

            m_physicsScene.TaintedObject(inTaintTime, childx.LocalID, "BSLinksetConstraints.RemoveChildFromLinkset", delegate()
            {
                PhysicallyUnlinkAChildFromRoot(rootx, childx);
            });
            // See that the linkset parameters are recomputed at the end of the taint time.
            Refresh(LinksetRoot);
        }
        else
        {
            // Non-fatal occurance.
            // PhysicsScene.Logger.ErrorFormat("{0}: Asked to remove child from linkset that was not in linkset", LogHeader);
        }
        return;
    }

    // Create a constraint between me (root of linkset) and the passed prim (the child).
    // Called at taint time!
    private void PhysicallyLinkAChildToRoot(BSPrimLinkable rootPrim, BSPrimLinkable childPrim)
    {
        // Don't build the constraint when asked. Put it off until just before the simulation step.
        Refresh(rootPrim);
    }

    // Create a static constraint between the two passed objects
    private BSConstraint BuildConstraint(BSPrimLinkable rootPrim, BSLinkInfo li)
    {
        BSLinkInfoConstraint linkInfo = li as BSLinkInfoConstraint;
        if (linkInfo == null)
            return null;

        // Zero motion for children so they don't interpolate
        li.member.ZeroMotion(true);

        BSConstraint constrain = null;

        switch (linkInfo.constraintType)
        {
            case ConstraintType.BS_FIXED_CONSTRAINT_TYPE:
            case ConstraintType.D6_CONSTRAINT_TYPE:
                // Relative position normalized to the root prim
                // Essentually a vector pointing from center of rootPrim to center of li.member
                OMV.Vector3 childRelativePosition = linkInfo.member.Position - rootPrim.Position;

                // real world coordinate of midpoint between the two objects
                OMV.Vector3 midPoint = rootPrim.Position + (childRelativePosition / 2);

                DetailLog("{0},BSLinksetConstraint.BuildConstraint,6Dof,rBody={1},cBody={2},rLoc={3},cLoc={4},midLoc={5}",
                                                rootPrim.LocalID, rootPrim.PhysBody, linkInfo.member.PhysBody,
                                                rootPrim.Position, linkInfo.member.Position, midPoint);

                // create a constraint that allows no freedom of movement between the two objects
                // http://bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=4818

                constrain = new BSConstraint6Dof(
                                    m_physicsScene.World, rootPrim.PhysBody, linkInfo.member.PhysBody, midPoint, true, true );

                /* NOTE: below is an attempt to build constraint with full frame computation, etc.
                 *     Using the midpoint is easier since it lets the Bullet code manipulate the transforms
                 *     of the objects.
                 * Code left for future programmers.
                // ==================================================================================
                // relative position normalized to the root prim
                OMV.Quaternion invThisOrientation = OMV.Quaternion.Inverse(rootPrim.Orientation);
                OMV.Vector3 childRelativePosition = (liConstraint.member.Position - rootPrim.Position) * invThisOrientation;

                // relative rotation of the child to the parent
                OMV.Quaternion childRelativeRotation = invThisOrientation * liConstraint.member.Orientation;
                OMV.Quaternion inverseChildRelativeRotation = OMV.Quaternion.Inverse(childRelativeRotation);

                DetailLog("{0},BSLinksetConstraint.PhysicallyLinkAChildToRoot,taint,root={1},child={2}", rootPrim.LocalID, rootPrim.LocalID, liConstraint.member.LocalID);
                constrain = new BS6DofConstraint(
                                PhysicsScene.World, rootPrim.Body, liConstraint.member.Body,
                                OMV.Vector3.Zero,
                                OMV.Quaternion.Inverse(rootPrim.Orientation),
                                OMV.Vector3.Zero,
                                OMV.Quaternion.Inverse(liConstraint.member.Orientation),
                                true,
                                true
                                );
                // ==================================================================================
                */

                break;
            case ConstraintType.D6_SPRING_CONSTRAINT_TYPE:
                constrain = new BSConstraintSpring(m_physicsScene.World, rootPrim.PhysBody, linkInfo.member.PhysBody,
                                linkInfo.frameInAloc, linkInfo.frameInArot, linkInfo.frameInBloc, linkInfo.frameInBrot,
                                linkInfo.useLinearReferenceFrameA,
                                true /*disableCollisionsBetweenLinkedBodies*/);
                DetailLog("{0},BSLinksetConstraint.BuildConstraint,spring,root={1},rBody={2},child={3},cBody={4},rLoc={5},cLoc={6}",
                                                rootPrim.LocalID,
                                                rootPrim.LocalID, rootPrim.PhysBody.AddrString,
                                                linkInfo.member.LocalID, linkInfo.member.PhysBody.AddrString,
                                                rootPrim.Position, linkInfo.member.Position);

                break;
            default:
                break;
        }

        linkInfo.SetLinkParameters(constrain);

        m_physicsScene.Constraints.AddConstraint(constrain);

        return constrain;
    }

    // Remove linkage between the linkset root and a particular child
    // The root and child bodies are passed in because we need to remove the constraint between
    //      the bodies that were present at unlink time.
    // Called at taint time!
    private bool PhysicallyUnlinkAChildFromRoot(BSPrimLinkable rootPrim, BSPrimLinkable childPrim)
    {
        bool ret = false;
        DetailLog("{0},BSLinksetConstraint.PhysicallyUnlinkAChildFromRoot,taint,root={1},rBody={2},child={3},cBody={4}",
                            rootPrim.LocalID,
                            rootPrim.LocalID, rootPrim.PhysBody.AddrString,
                            childPrim.LocalID, childPrim.PhysBody.AddrString);

        // If asked to unlink root from root, just remove all the constraints
        if (rootPrim == childPrim || childPrim == LinksetRoot)
        {
            PhysicallyUnlinkAllChildrenFromRoot(LinksetRoot);
            ret = true;
        }
        else
        {
            // Find the constraint for this link and get rid of it from the overall collection and from my list
            if (m_physicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody, childPrim.PhysBody))
            {
                // Make the child refresh its location
                m_physicsScene.PE.PushUpdate(childPrim.PhysBody);
                ret = true;
            }
        }

        return ret;
    }

    // Remove linkage between myself and any possible children I might have.
    // Returns 'true' of any constraints were destroyed.
    // Called at taint time!
    private bool PhysicallyUnlinkAllChildrenFromRoot(BSPrimLinkable rootPrim)
    {
        DetailLog("{0},BSLinksetConstraint.PhysicallyUnlinkAllChildren,taint", rootPrim.LocalID);

        return m_physicsScene.Constraints.RemoveAndDestroyConstraint(rootPrim.PhysBody);
    }

    // Call each of the constraints that make up this linkset and recompute the
    //    various transforms and variables. Create constraints of not created yet.
    // Called before the simulation step to make sure the constraint based linkset
    //    is all initialized.
    // Called at taint time!!
    private void RecomputeLinksetConstraints()
    {
        float linksetMass = LinksetMass;
        LinksetRoot.UpdatePhysicalMassProperties(linksetMass, true);

        DetailLog("{0},BSLinksetConstraint.RecomputeLinksetConstraints,set,rBody={1},linksetMass={2}",
                            LinksetRoot.LocalID, LinksetRoot.PhysBody.AddrString, linksetMass);

        try
        {
            Rebuilding = true;

            // There is no reason to build all this physical stuff for a non-physical linkset.
            if (!LinksetRoot.IsPhysicallyActive || !HasAnyChildren)
            {
                DetailLog("{0},BSLinksetConstraint.RecomputeLinksetCompound,notPhysicalOrNoChildren", LinksetRoot.LocalID);
                return; // Note the 'finally' clause at the botton which will get executed.
            }

            ForEachLinkInfo((li) =>
            {
                // A child in the linkset physically shows the mass of the whole linkset.
                // This allows Bullet to apply enough force on the child to move the whole linkset.
                // (Also do the mass stuff before recomputing the constraint so mass is not zero.)
                li.member.UpdatePhysicalMassProperties(linksetMass, true);

                BSConstraint constrain;
                if (!m_physicsScene.Constraints.TryGetConstraint(LinksetRoot.PhysBody, li.member.PhysBody, out constrain))
                {
                    // If constraint doesn't exist yet, create it.
                    constrain = BuildConstraint(LinksetRoot, li);
                }
                li.SetLinkParameters(constrain);
                constrain.RecomputeConstraintVariables(linksetMass);

                // PhysicsScene.PE.DumpConstraint(PhysicsScene.World, constrain.Constraint);    // DEBUG DEBUG
                return false;   // 'false' says to keep processing other members
            });
        }
        finally
        {
            Rebuilding = false;
        }
    }

    #region Extension
    public override object Extension(string pFunct, params object[] pParams)
    {
        object ret = null;
        switch (pFunct)
        {
            // pParams = [ BSPhysObject root, BSPhysObject child, integer linkType ]
            case ExtendedPhysics.PhysFunctChangeLinkType:
                if (pParams.Length > 2)
                {
                    int requestedType = (int)pParams[2];
                    DetailLog("{0},BSLinksetConstraint.ChangeLinkType,requestedType={1}", LinksetRoot.LocalID, requestedType);
                    if (requestedType == (int)ConstraintType.BS_FIXED_CONSTRAINT_TYPE
                        || requestedType == (int)ConstraintType.D6_CONSTRAINT_TYPE
                        || requestedType == (int)ConstraintType.D6_SPRING_CONSTRAINT_TYPE
                        || requestedType == (int)ConstraintType.HINGE_CONSTRAINT_TYPE
                        || requestedType == (int)ConstraintType.CONETWIST_CONSTRAINT_TYPE
                        || requestedType == (int)ConstraintType.SLIDER_CONSTRAINT_TYPE)
                    {
                        BSPrimLinkable child = pParams[1] as BSPrimLinkable;
                        if (child != null)
                        {
                            DetailLog("{0},BSLinksetConstraint.ChangeLinkType,rootID={1},childID={2},type={3}",
                                                    LinksetRoot.LocalID, LinksetRoot.LocalID, child.LocalID, requestedType);
                            m_physicsScene.TaintedObject(child.LocalID, "BSLinksetConstraint.PhysFunctChangeLinkType", delegate()
                            {
                                // Pick up all the constraints currently created.
                                RemoveDependencies(child);

                                BSLinkInfo linkInfo = null;
                                if (TryGetLinkInfo(child, out linkInfo))
                                {
                                    BSLinkInfoConstraint linkInfoC = linkInfo as BSLinkInfoConstraint;
                                    if (linkInfoC != null)
                                    {
                                        linkInfoC.constraintType = (ConstraintType)requestedType;
                                        ret = (object)true;
                                        DetailLog("{0},BSLinksetConstraint.ChangeLinkType,link={1},type={2}",
                                                    linkInfo.member.LocalID, linkInfo.member.LocalID, linkInfoC.constraintType);
                                    }
                                    else
                                    {
                                        DetailLog("{0},BSLinksetConstraint.ChangeLinkType,linkInfoNotConstraint,childID={1}", LinksetRoot.LocalID, child.LocalID);
                                    }
                                }
                                else
                                {
                                    DetailLog("{0},BSLinksetConstraint.ChangeLinkType,noLinkInfoForChild,childID={1}", LinksetRoot.LocalID, child.LocalID);
                                }
                                // Cause the whole linkset to be rebuilt in post-taint time.
                                Refresh(child);
                            });
                        }
                        else
                        {
                            DetailLog("{0},BSLinksetConstraint.SetLinkType,childNotBSPrimLinkable", LinksetRoot.LocalID);
                        }
                    }
                    else
                    {
                        DetailLog("{0},BSLinksetConstraint.SetLinkType,illegalRequestedType,reqested={1},spring={2}",
                                        LinksetRoot.LocalID, requestedType, ((int)ConstraintType.D6_SPRING_CONSTRAINT_TYPE));
                    }
                }
                break;
            // pParams = [ BSPhysObject root, BSPhysObject child ]
            case ExtendedPhysics.PhysFunctGetLinkType:
                if (pParams.Length > 0)
                {
                    BSPrimLinkable child = pParams[1] as BSPrimLinkable;
                    if (child != null)
                    {
                        BSLinkInfo linkInfo = null;
                        if (TryGetLinkInfo(child, out linkInfo))
                        {
                            BSLinkInfoConstraint linkInfoC = linkInfo as BSLinkInfoConstraint;
                            if (linkInfoC != null)
                            {
                                ret = (object)(int)linkInfoC.constraintType;
                                DetailLog("{0},BSLinksetConstraint.GetLinkType,link={1},type={2}",
                                            linkInfo.member.LocalID, linkInfo.member.LocalID, linkInfoC.constraintType);

                            }
                        }
                    }
                }
                break;
            // pParams = [ BSPhysObject root, BSPhysObject child, int op, object opParams, int op, object opParams, ... ]
            case ExtendedPhysics.PhysFunctChangeLinkParams:
                // There should be two parameters: the childActor and a list of parameters to set
                if (pParams.Length > 2)
                {
                    BSPrimLinkable child = pParams[1] as BSPrimLinkable;
                    BSLinkInfo baseLinkInfo = null;
                    if (TryGetLinkInfo(child, out baseLinkInfo))
                    {
                        BSLinkInfoConstraint linkInfo = baseLinkInfo as BSLinkInfoConstraint;
                        if (linkInfo != null)
                        {
                            int valueInt;
                            float valueFloat;
                            bool valueBool;
                            OMV.Vector3 valueVector;
                            OMV.Vector3 valueVector2;
                            OMV.Quaternion valueQuaternion;
                            int axisLow, axisHigh;

                            int opIndex = 2;
                            while (opIndex < pParams.Length)
                            {
                                int thisOp = 0;
                                string errMsg = "";
                                try
                                {
                                    thisOp = (int)pParams[opIndex];
                                    DetailLog("{0},BSLinksetConstraint.ChangeLinkParams2,op={1},val={2}",
                                                        linkInfo.member.LocalID, thisOp, pParams[opIndex + 1]);
                                    switch (thisOp)
                                    {
                                        case ExtendedPhysics.PHYS_PARAM_LINK_TYPE:
                                            valueInt = (int)pParams[opIndex + 1];
                                            ConstraintType valueType = (ConstraintType)valueInt;
                                            if (valueType == ConstraintType.BS_FIXED_CONSTRAINT_TYPE
                                                || valueType == ConstraintType.D6_CONSTRAINT_TYPE
                                                || valueType == ConstraintType.D6_SPRING_CONSTRAINT_TYPE
                                                || valueType == ConstraintType.HINGE_CONSTRAINT_TYPE
                                                || valueType == ConstraintType.CONETWIST_CONSTRAINT_TYPE
                                                || valueType == ConstraintType.SLIDER_CONSTRAINT_TYPE)
                                            {
                                                linkInfo.constraintType = valueType;
                                            }
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_FRAMEINA_LOC:
                                            errMsg = "PHYS_PARAM_FRAMEINA_LOC takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.frameInAloc = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_FRAMEINA_ROT:
                                            errMsg = "PHYS_PARAM_FRAMEINA_ROT takes one parameter of type rotation";
                                            valueQuaternion = (OMV.Quaternion)pParams[opIndex + 1];
                                            linkInfo.frameInArot = valueQuaternion;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_FRAMEINB_LOC:
                                            errMsg = "PHYS_PARAM_FRAMEINB_LOC takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.frameInBloc = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_FRAMEINB_ROT:
                                            errMsg = "PHYS_PARAM_FRAMEINB_ROT takes one parameter of type rotation";
                                            valueQuaternion = (OMV.Quaternion)pParams[opIndex + 1];
                                            linkInfo.frameInBrot = valueQuaternion;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_LINEAR_LIMIT_LOW:
                                            errMsg = "PHYS_PARAM_LINEAR_LIMIT_LOW takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.linearLimitLow = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_LINEAR_LIMIT_HIGH:
                                            errMsg = "PHYS_PARAM_LINEAR_LIMIT_HIGH takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.linearLimitHigh = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_ANGULAR_LIMIT_LOW:
                                            errMsg = "PHYS_PARAM_ANGULAR_LIMIT_LOW takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.angularLimitLow = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_ANGULAR_LIMIT_HIGH:
                                            errMsg = "PHYS_PARAM_ANGULAR_LIMIT_HIGH takes one parameter of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            linkInfo.angularLimitHigh = valueVector;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_USE_FRAME_OFFSET:
                                            errMsg = "PHYS_PARAM_USE_FRAME_OFFSET takes one parameter of type integer (bool)";
                                            valueBool = ((int)pParams[opIndex + 1]) != 0;
                                            linkInfo.useFrameOffset = valueBool;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_ENABLE_TRANSMOTOR:
                                            errMsg = "PHYS_PARAM_ENABLE_TRANSMOTOR takes one parameter of type integer (bool)";
                                            valueBool = ((int)pParams[opIndex + 1]) != 0;
                                            linkInfo.enableTransMotor = valueBool;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_TRANSMOTOR_MAXVEL:
                                            errMsg = "PHYS_PARAM_TRANSMOTOR_MAXVEL takes one parameter of type float";
                                            valueFloat = (float)pParams[opIndex + 1];
                                            linkInfo.transMotorMaxVel = valueFloat;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_TRANSMOTOR_MAXFORCE:
                                            errMsg = "PHYS_PARAM_TRANSMOTOR_MAXFORCE takes one parameter of type float";
                                            valueFloat = (float)pParams[opIndex + 1];
                                            linkInfo.transMotorMaxForce = valueFloat;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_CFM:
                                            errMsg = "PHYS_PARAM_CFM takes one parameter of type float";
                                            valueFloat = (float)pParams[opIndex + 1];
                                            linkInfo.cfm = valueFloat;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_ERP:
                                            errMsg = "PHYS_PARAM_ERP takes one parameter of type float";
                                            valueFloat = (float)pParams[opIndex + 1];
                                            linkInfo.erp = valueFloat;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_SOLVER_ITERATIONS:
                                            errMsg = "PHYS_PARAM_SOLVER_ITERATIONS takes one parameter of type float";
                                            valueFloat = (float)pParams[opIndex + 1];
                                            linkInfo.solverIterations = valueFloat;
                                            opIndex += 2;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_SPRING_AXIS_ENABLE:
                                            errMsg = "PHYS_PARAM_SPRING_AXIS_ENABLE takes two parameters of types integer and integer (bool)";
                                            valueInt = (int)pParams[opIndex + 1];
                                            valueBool = ((int)pParams[opIndex + 2]) != 0;
                                            GetAxisRange(valueInt, out axisLow, out axisHigh);
                                            for (int ii = axisLow; ii <= axisHigh; ii++)
                                                linkInfo.springAxisEnable[ii] = valueBool;
                                            opIndex += 3;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_SPRING_DAMPING:
                                            errMsg = "PHYS_PARAM_SPRING_DAMPING takes two parameters of types integer and float";
                                            valueInt = (int)pParams[opIndex + 1];
                                            valueFloat = (float)pParams[opIndex + 2];
                                            GetAxisRange(valueInt, out axisLow, out axisHigh);
                                            for (int ii = axisLow; ii <= axisHigh; ii++)
                                                linkInfo.springDamping[ii] = valueFloat;
                                            opIndex += 3;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_SPRING_STIFFNESS:
                                            errMsg = "PHYS_PARAM_SPRING_STIFFNESS takes two parameters of types integer and float";
                                            valueInt = (int)pParams[opIndex + 1];
                                            valueFloat = (float)pParams[opIndex + 2];
                                            GetAxisRange(valueInt, out axisLow, out axisHigh);
                                            for (int ii = axisLow; ii <= axisHigh; ii++)
                                                linkInfo.springStiffness[ii] = valueFloat;
                                            opIndex += 3;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_SPRING_EQUILIBRIUM_POINT:
                                            errMsg = "PHYS_PARAM_SPRING_EQUILIBRIUM_POINT takes two parameters of type vector";
                                            valueVector = (OMV.Vector3)pParams[opIndex + 1];
                                            valueVector2 = (OMV.Vector3)pParams[opIndex + 2];
                                            linkInfo.springLinearEquilibriumPoint = valueVector;
                                            linkInfo.springAngularEquilibriumPoint = valueVector2;
                                            opIndex += 3;
                                            break;
                                        case ExtendedPhysics.PHYS_PARAM_USE_LINEAR_FRAMEA:
                                            errMsg = "PHYS_PARAM_USE_LINEAR_FRAMEA takes one parameter of type integer (bool)";
                                            valueBool = ((int)pParams[opIndex + 1]) != 0;
                                            linkInfo.useLinearReferenceFrameA = valueBool;
                                            opIndex += 2;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                catch (InvalidCastException e)
                                {
                                    m_physicsScene.Logger.WarnFormat("{0} value of wrong type in physSetLinksetParams: {1}, err={2}",
                                                        LogHeader, errMsg, e);
                                }
                                catch (Exception e)
                                {
                                    m_physicsScene.Logger.WarnFormat("{0} bad parameters in physSetLinksetParams: {1}", LogHeader, e);
                                }
                            }
                        }
                        // Something changed so a rebuild is in order
                        Refresh(child);
                    }
                }
            break;
            default:
                ret = base.Extension(pFunct, pParams);
                break;
        }
        return ret;
    }

    // Bullet constraints keep some limit parameters for each linear and angular axis.
    // Setting same is easier if there is an easy way to see all or types.
    // This routine returns the array limits for the set of axis.
    private void GetAxisRange(int rangeSpec, out int low, out int high)
    {
        switch (rangeSpec)
        {
            case ExtendedPhysics.PHYS_AXIS_LINEAR_ALL:
                low = 0;
                high = 2;
                break;
            case ExtendedPhysics.PHYS_AXIS_ANGULAR_ALL:
                low = 3;
                high = 5;
                break;
            case ExtendedPhysics.PHYS_AXIS_ALL:
                low = 0;
                high = 5;
                break;
            default:
                low = high = rangeSpec;
                break;
        }
        return;
    }
    #endregion // Extension

}
}
