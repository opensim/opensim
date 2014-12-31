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
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using log4net;
using OMV = OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;
using OpenSim.Region.OptionalModules.Scripting; // for ExtendedPhysics

namespace OpenSim.Region.Physics.BulletSPlugin
{

    [Serializable]
public class BSPrim : BSPhysObject
{
    protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

    // _size is what the user passed. Scale is what we pass to the physics engine with the mesh.
    private OMV.Vector3 _size;  // the multiplier for each mesh dimension as passed by the user

    private bool _grabbed;
    private bool _isSelected;
    private bool _isVolumeDetect;

    private float _mass;    // the mass of this object
    private OMV.Vector3 _acceleration;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _floatOnWater;
    private OMV.Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    private int CrossingFailures { get; set; }

    // Keep a handle to the vehicle actor so it is easy to set parameters on same.
    public const string VehicleActorName = "BasicVehicle";

    // Parameters for the hover actor
    public const string HoverActorName = "BSPrim.HoverActor";
    // Parameters for the axis lock actor
    public const String LockedAxisActorName = "BSPrim.LockedAxis";
    // Parameters for the move to target actor
    public const string MoveToTargetActorName = "BSPrim.MoveToTargetActor";
    // Parameters for the setForce and setTorque actors
    public const string SetForceActorName = "BSPrim.SetForceActor";
    public const string SetTorqueActorName = "BSPrim.SetTorqueActor";

    public BSPrim(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
            : base(parent_scene, localID, primName, "BSPrim")
    {
        // m_log.DebugFormat("{0}: BSPrim creation of {1}, id={2}", LogHeader, primName, localID);
        _physicsActorType = (int)ActorTypes.Prim;
        RawPosition = pos;
        _size = size;
        Scale = size;   // prims are the size the user wants them to be (different for BSCharactes).
        RawOrientation = rotation;
        _buoyancy = 0f;
        RawVelocity = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;
        BaseShape = pbs;
        _isPhysical = pisPhysical;
        _isVolumeDetect = false;

        _mass = CalculateMass();

        DetailLog("{0},BSPrim.constructor,pbs={1}", LocalID, BSScene.PrimitiveBaseShapeToString(pbs));
        // DetailLog("{0},BSPrim.constructor,call", LocalID);
        // do the actual object creation at taint time
        PhysScene.TaintedObject(LocalID, "BSPrim.create", delegate()
        {
            // Make sure the object is being created with some sanity.
            ExtremeSanityCheck(true /* inTaintTime */);

            CreateGeomAndObject(true);

            CurrentCollisionFlags = PhysScene.PE.GetCollisionFlags(PhysBody);

            IsInitialized = true;
        });
    }

    // called when this prim is being destroyed and we should free all the resources
    public override void Destroy()
    {
        // m_log.DebugFormat("{0}: Destroy, id={1}", LogHeader, LocalID);
        IsInitialized = false;

        base.Destroy();

        // Undo any vehicle properties
        this.VehicleType = (int)Vehicle.TYPE_NONE;

        PhysScene.TaintedObject(LocalID, "BSPrim.Destroy", delegate()
        {
            DetailLog("{0},BSPrim.Destroy,taint,", LocalID);
            // If there are physical body and shape, release my use of same.
            PhysScene.Shapes.DereferenceBody(PhysBody, null);
            PhysBody.Clear();
            PhysShape.Dereference(PhysScene);
            PhysShape = new BSShapeNull();
        });
    }

    // No one uses this property.
    public override bool Stopped {
        get { return false; }
    }

    public override bool IsIncomplete {
        get {
            return ShapeRebuildScheduled;
        }
    }

    // 'true' if this object's shape is in need of a rebuild and a rebuild has been queued.
    // The prim is still available but its underlying shape will change soon.
    // This is protected by a 'lock(this)'.
    public bool ShapeRebuildScheduled { get; protected set; }

    public override OMV.Vector3 Size {
        get { return _size; }
        set {
            // We presume the scale and size are the same. If scale must be changed for
            //     the physical shape, that is done when the geometry is built.
            _size = value;
            Scale = _size;
            ForceBodyShapeRebuild(false);
        }
    }

    public override PrimitiveBaseShape Shape {
        set {
            BaseShape = value;
            DetailLog("{0},BSPrim.changeShape,pbs={1}", LocalID, BSScene.PrimitiveBaseShapeToString(BaseShape));
            PrimAssetState = PrimAssetCondition.Unknown;
            ForceBodyShapeRebuild(false);
        }
    }
    // Cause the body and shape of the prim to be rebuilt if necessary.
    // If there are no changes required, this is quick and does not make changes to the prim.
    // If rebuilding is necessary (like changing from static to physical), that will happen.
    // The 'ShapeRebuildScheduled' tells any checker that the body/shape may change shortly.
    // The return parameter is not used by anyone.
    public override bool ForceBodyShapeRebuild(bool inTaintTime)
    {
        if (inTaintTime)
        {
            // If called in taint time, do the operation immediately
            _mass = CalculateMass();   // changing the shape changes the mass
            CreateGeomAndObject(true);
        }
        else
        {
            lock (this)
            {
                // If a rebuild is not already in the queue
                if (!ShapeRebuildScheduled)
                {
                    // Remember that a rebuild is queued -- this is used to flag an incomplete object
                    ShapeRebuildScheduled = true;
                    PhysScene.TaintedObject(LocalID, "BSPrim.ForceBodyShapeRebuild", delegate()
                    {
                        _mass = CalculateMass();   // changing the shape changes the mass
                        CreateGeomAndObject(true);
                        ShapeRebuildScheduled = false;
                    });
                }
            }
        }
        return true;
    }
    public override bool Grabbed {
        set { _grabbed = value;
        }
    }
    public override bool Selected {
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setSelected", delegate()
                {
                    DetailLog("{0},BSPrim.selected,taint,selected={1}", LocalID, _isSelected);
                    SetObjectDynamic(false);
                });
            }
        }
    }
    public override bool IsSelected
    {
        get { return _isSelected; }
    }

    public override void CrossingFailure()
    {
        CrossingFailures++;
        if (CrossingFailures > BSParam.CrossingFailuresBeforeOutOfBounds)
        {
            base.RaiseOutOfBounds(RawPosition);
        }
        else if (CrossingFailures == BSParam.CrossingFailuresBeforeOutOfBounds)
        {
            m_log.WarnFormat("{0} Too many crossing failures for {1}", LogHeader, Name);
        }
        return;
    }

    // link me to the specified parent
    public override void link(PhysicsActor obj) {
    }

    // delink me from my linkset
    public override void delink() {
    }

    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public override void ZeroMotion(bool inTaintTime)
    {
        RawVelocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties in the physics engine
        PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
                PhysScene.PE.ClearAllForces(PhysBody);
        });
    }
    public override void ZeroAngularMotion(bool inTaintTime)
    {
        _rotationalVelocity = OMV.Vector3.Zero;
        // Zero some other properties in the physics engine
        PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.ZeroMotion", delegate()
        {
            // DetailLog("{0},BSPrim.ZeroAngularMotion,call,rotVel={1}", LocalID, _rotationalVelocity);
            if (PhysBody.HasPhysicalBody)
            {
                PhysScene.PE.SetInterpolationAngularVelocity(PhysBody, _rotationalVelocity);
                PhysScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
            }
        });
    }

    public override void LockAngularMotion(OMV.Vector3 axis)
    {
        DetailLog("{0},BSPrim.LockAngularMotion,call,axis={1}", LocalID, axis);

        ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR, 0f, 0f);
        if (axis.X != 1)
        {
            ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X, 0f, 0f);
        }
        if (axis.Y != 1)
        {
            ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y, 0f, 0f);
        }
        if (axis.Z != 1)
        {
            ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z, 0f, 0f);
        }

        InitializeAxisActor();

        return;
    }

    public override OMV.Vector3 Position {
        get {
            // don't do the GetObjectPosition for root elements because this function is called a zillion times.
            // RawPosition = ForcePosition;
            return RawPosition;
        }
        set {
            // If the position must be forced into the physics engine, use ForcePosition.
            // All positions are given in world positions.
            if (RawPosition == value)
            {
                DetailLog("{0},BSPrim.setPosition,call,positionNotChanging,pos={1},orient={2}", LocalID, RawPosition, RawOrientation);
                return;
            }
            RawPosition = value;
            PositionSanityCheck(false);

            PhysScene.TaintedObject(LocalID, "BSPrim.setPosition", delegate()
            {
                DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, RawPosition, RawOrientation);
                ForcePosition = RawPosition;
            });
        }
    }

    // NOTE: overloaded by BSPrimDisplaced to handle offset for center-of-gravity.
    public override OMV.Vector3 ForcePosition {
        get {
            RawPosition = PhysScene.PE.GetPosition(PhysBody);
            return RawPosition;
        }
        set {
            RawPosition = value;
            if (PhysBody.HasPhysicalBody)
            {
                PhysScene.PE.SetTranslation(PhysBody, RawPosition, RawOrientation);
                ActivateIfPhysical(false);
            }
        }
    }

    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain and being out of bounds.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck(bool inTaintTime)
    {
        bool ret = false;

        // We don't care where non-physical items are placed
        if (!IsPhysicallyActive)
            return ret;

        if (!PhysScene.TerrainManager.IsWithinKnownTerrain(RawPosition))
        {
            // The physical object is out of the known/simulated area.
            // Upper levels of code will handle the transition to other areas so, for
            //     the time, we just ignore the position.
            return ret;
        }

        float terrainHeight = PhysScene.TerrainManager.GetTerrainHeightAtXYZ(RawPosition);
        OMV.Vector3 upForce = OMV.Vector3.Zero;
        float approxSize = Math.Max(Size.X, Math.Max(Size.Y, Size.Z));
        if ((RawPosition.Z + approxSize / 2f) < terrainHeight)
        {
            DetailLog("{0},BSPrim.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, RawPosition, terrainHeight);
            float targetHeight = terrainHeight + (Size.Z / 2f);
            // If the object is below ground it just has to be moved up because pushing will
            //     not get it through the terrain
            RawPosition = new OMV.Vector3(RawPosition.X, RawPosition.Y, targetHeight);
            if (inTaintTime)
            {
                ForcePosition = RawPosition;
            }
            // If we are throwing the object around, zero its other forces
            ZeroMotion(inTaintTime);
            ret = true;
        }

        if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
        {
            float waterHeight = PhysScene.TerrainManager.GetWaterLevelAtXYZ(RawPosition);
            // TODO: a floating motor so object will bob in the water
            if (Math.Abs(RawPosition.Z - waterHeight) > 0.1f)
            {
                // Upforce proportional to the distance away from the water. Correct the error in 1 sec.
                upForce.Z = (waterHeight - RawPosition.Z) * 1f;

                // Apply upforce and overcome gravity.
                OMV.Vector3 correctionForce = upForce - PhysScene.DefaultGravity;
                DetailLog("{0},BSPrim.PositionSanityCheck,applyForce,pos={1},upForce={2},correctionForce={3}", LocalID, RawPosition, upForce, correctionForce);
                AddForce(correctionForce, false, inTaintTime);
                ret = true;
            }
        }

        return ret;
    }

    // Occasionally things will fly off and really get lost.
    // Find the wanderers and bring them back.
    // Return 'true' if some parameter need some sanity.
    private bool ExtremeSanityCheck(bool inTaintTime)
    {
        bool ret = false;

        int wayOverThere = -1000;
        int wayOutThere = 10000;
        // There have been instances of objects getting thrown way out of bounds and crashing
        //    the border crossing code.
        if (   RawPosition.X < wayOverThere || RawPosition.X > wayOutThere
            || RawPosition.Y < wayOverThere || RawPosition.X > wayOutThere
            || RawPosition.Z < wayOverThere || RawPosition.X > wayOutThere)
        {
            RawPosition = new OMV.Vector3(10, 10, 50);
            ZeroMotion(inTaintTime);
            ret = true;
        }
        if (RawVelocity.LengthSquared() > BSParam.MaxLinearVelocitySquared)
        {
            RawVelocity = Util.ClampV(RawVelocity, BSParam.MaxLinearVelocity);
            ret = true;
        }
        if (_rotationalVelocity.LengthSquared() > BSParam.MaxAngularVelocitySquared)
        {
            _rotationalVelocity = Util.ClampV(_rotationalVelocity, BSParam.MaxAngularVelocity);
            ret = true;
        }

        return ret;
    }

    // Return the effective mass of the object.
        // The definition of this call is to return the mass of the prim.
        // If the simulator cares about the mass of the linkset, it will sum it itself.
    public override float Mass
    {
        get { return _mass; }
    }
    // TotalMass returns the mass of the large object the prim may be in (overridden by linkset code)
    public virtual float TotalMass
    {
        get { return _mass; }
    }
    // used when we only want this prim's mass and not the linkset thing
    public override float RawMass {
        get { return _mass; }
    }
    // Set the physical mass to the passed mass.
    // Note that this does not change _mass!
    public override void UpdatePhysicalMassProperties(float physMass, bool inWorld)
    {
        if (PhysBody.HasPhysicalBody && PhysShape.HasPhysicalShape)
        {
            if (IsStatic)
            {
                PhysScene.PE.SetGravity(PhysBody, PhysScene.DefaultGravity);
                Inertia = OMV.Vector3.Zero;
                PhysScene.PE.SetMassProps(PhysBody, 0f, Inertia);
                PhysScene.PE.UpdateInertiaTensor(PhysBody);
            }
            else
            {
                if (inWorld)
                {
                    // Changing interesting properties doesn't change proxy and collision cache
                    //    information. The Bullet solution is to re-add the object to the world
                    //    after parameters are changed.
                    PhysScene.PE.RemoveObjectFromWorld(PhysScene.World, PhysBody);
                }

                // The computation of mass props requires gravity to be set on the object.
                Gravity = ComputeGravity(Buoyancy);
                PhysScene.PE.SetGravity(PhysBody, Gravity);

                // OMV.Vector3 currentScale = PhysScene.PE.GetLocalScaling(PhysShape.physShapeInfo);   // DEBUG DEBUG
                // DetailLog("{0},BSPrim.UpdateMassProperties,currentScale{1},shape={2}", LocalID, currentScale, PhysShape.physShapeInfo);   // DEBUG DEBUG

                Inertia = PhysScene.PE.CalculateLocalInertia(PhysShape.physShapeInfo, physMass);
                PhysScene.PE.SetMassProps(PhysBody, physMass, Inertia);
                PhysScene.PE.UpdateInertiaTensor(PhysBody);

                DetailLog("{0},BSPrim.UpdateMassProperties,mass={1},localInertia={2},grav={3},inWorld={4}",
                                            LocalID, physMass, Inertia, Gravity, inWorld);

                if (inWorld)
                {
                    AddObjectToPhysicalWorld();
                }
            }
        }
    }

    // Return what gravity should be set to this very moment
    public OMV.Vector3 ComputeGravity(float buoyancy)
    {
        OMV.Vector3 ret = PhysScene.DefaultGravity;

        if (!IsStatic)
        {
            ret *= (1f - buoyancy);
            ret *= GravModifier;
        }

        return ret;
    }

    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return RawPosition; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return RawPosition; }
    }

    public override OMV.Vector3 Force {
        get { return RawForce; }
        set {
            RawForce = value;
            EnableActor(RawForce != OMV.Vector3.Zero, SetForceActorName, delegate()
            {
                return new BSActorSetForce(PhysScene, this, SetForceActorName);
            });

            // Call update so actor Refresh() is called to start things off
            PhysScene.TaintedObject(LocalID, "BSPrim.setForce", delegate()
            {
                UpdatePhysicalParameters();
            });
        }
    }

    // Find and return a handle to the current vehicle actor.
    // Return 'null' if there is no vehicle actor.
    public BSDynamics GetVehicleActor(bool createIfNone)
    {
        BSDynamics ret = null;
        BSActor actor;
        if (PhysicalActors.TryGetActor(VehicleActorName, out actor))
        {
            ret = actor as BSDynamics;
        }
        else
        {
            if (createIfNone)
            {
                ret = new BSDynamics(PhysScene, this, VehicleActorName);
                PhysicalActors.Add(ret.ActorName, ret);
            }
        }
        return ret;
    }

    public override int VehicleType {
        get {
            int ret = (int)Vehicle.TYPE_NONE;
            BSDynamics vehicleActor = GetVehicleActor(false /* createIfNone */);
            if (vehicleActor != null)
                ret = (int)vehicleActor.Type;
            return ret;
        }
        set {
            Vehicle type = (Vehicle)value;

            PhysScene.TaintedObject(LocalID, "setVehicleType", delegate()
            {
                // Some vehicle scripts change vehicle type on the fly as an easy way to
                //    change all the parameters. Like a plane changing to CAR when on the
                //    ground. In this case, don't want to zero motion.
                // ZeroMotion(true /* inTaintTime */);
                if (type == Vehicle.TYPE_NONE)
                {
                    // Vehicle type is 'none' so get rid of any actor that may have been allocated.
                    BSDynamics vehicleActor = GetVehicleActor(false /* createIfNone */);
                    if (vehicleActor != null)
                    {
                        PhysicalActors.RemoveAndRelease(vehicleActor.ActorName);
                    }
                }
                else
                {
                    // Vehicle type is not 'none' so create an actor and set it running.
                    BSDynamics vehicleActor = GetVehicleActor(true /* createIfNone */);
                    if (vehicleActor != null)
                    {
                        vehicleActor.ProcessTypeChange(type);
                        ActivateIfPhysical(false);
                    }
                }
            });
        }
    }
    public override void VehicleFloatParam(int param, float value)
    {
        PhysScene.TaintedObject(LocalID, "BSPrim.VehicleFloatParam", delegate()
        {
            BSDynamics vehicleActor = GetVehicleActor(true /* createIfNone */);
            if (vehicleActor != null)
            {
                vehicleActor.ProcessFloatVehicleParam((Vehicle)param, value);
                ActivateIfPhysical(false);
            }
        });
    }
    public override void VehicleVectorParam(int param, OMV.Vector3 value)
    {
        PhysScene.TaintedObject(LocalID, "BSPrim.VehicleVectorParam", delegate()
        {
            BSDynamics vehicleActor = GetVehicleActor(true /* createIfNone */);
            if (vehicleActor != null)
            {
                vehicleActor.ProcessVectorVehicleParam((Vehicle)param, value);
                ActivateIfPhysical(false);
            }
        });
    }
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation)
    {
        PhysScene.TaintedObject(LocalID, "BSPrim.VehicleRotationParam", delegate()
        {
            BSDynamics vehicleActor = GetVehicleActor(true /* createIfNone */);
            if (vehicleActor != null)
            {
                vehicleActor.ProcessRotationVehicleParam((Vehicle)param, rotation);
                ActivateIfPhysical(false);
            }
        });
    }
    public override void VehicleFlags(int param, bool remove)
    {
        PhysScene.TaintedObject(LocalID, "BSPrim.VehicleFlags", delegate()
        {
            BSDynamics vehicleActor = GetVehicleActor(true /* createIfNone */);
            if (vehicleActor != null)
            {
                vehicleActor.ProcessVehicleFlags(param, remove);
            }
        });
    }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        if (_isVolumeDetect != newValue)
        {
            _isVolumeDetect = newValue;
            PhysScene.TaintedObject(LocalID, "BSPrim.SetVolumeDetect", delegate()
            {
                // DetailLog("{0},setVolumeDetect,taint,volDetect={1}", LocalID, _isVolumeDetect);
                SetObjectDynamic(true);
            });
        }
        return;
    }
    public override bool IsVolumeDetect
    {
        get { return _isVolumeDetect; }
    }
    public override void SetMaterial(int material)
    {
        base.SetMaterial(material);
        PhysScene.TaintedObject(LocalID, "BSPrim.SetMaterial", delegate()
        {
            UpdatePhysicalParameters();
        });
    }
    public override float Friction
    {
        get { return base.Friction; }
        set
        {
            if (base.Friction != value)
            {
                base.Friction = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setFriction", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    public override float Restitution
    {
        get { return base.Restitution; }
        set
        {
            if (base.Restitution != value)
            {
                base.Restitution = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setRestitution", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    // The simulator/viewer keep density as 100kg/m3.
    // Remember to use BSParam.DensityScaleFactor to create the physical density.
    public override float Density
    {
        get { return base.Density; }
        set
        {
            if (base.Density != value)
            {
                base.Density = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setDensity", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    public override float GravModifier
    {
        get { return base.GravModifier; }
        set
        {
            if (base.GravModifier != value)
            {
                base.GravModifier = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setGravityModifier", delegate()
                {
                    UpdatePhysicalParameters();
                });
            }
        }
    }
    public override OMV.Vector3 Velocity {
        get { return RawVelocity; }
        set {
            RawVelocity = value;
            PhysScene.TaintedObject(LocalID, "BSPrim.setVelocity", delegate()
            {
                // DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, RawVelocity);
                ForceVelocity = RawVelocity;
            });
        }
    }
    public override OMV.Vector3 ForceVelocity {
        get { return RawVelocity; }
        set {
            PhysScene.AssertInTaintTime("BSPrim.ForceVelocity");

            RawVelocity = Util.ClampV(value, BSParam.MaxLinearVelocity);
            if (PhysBody.HasPhysicalBody)
            {
                DetailLog("{0},BSPrim.ForceVelocity,taint,vel={1}", LocalID, RawVelocity);
                PhysScene.PE.SetLinearVelocity(PhysBody, RawVelocity);
                ActivateIfPhysical(false);
            }
        }
    }
    public override OMV.Vector3 Torque {
        get { return RawTorque; }
        set {
            RawTorque = value;
            EnableActor(RawTorque != OMV.Vector3.Zero, SetTorqueActorName, delegate()
            {
                return new BSActorSetTorque(PhysScene, this, SetTorqueActorName);
            });
            DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, RawTorque);

            // Call update so actor Refresh() is called to start things off
            PhysScene.TaintedObject(LocalID, "BSPrim.setTorque", delegate()
            {
                UpdatePhysicalParameters();
            });
        }
    }
    public override OMV.Vector3 Acceleration {
        get { return _acceleration; }
        set { _acceleration = value; }
    }

    public override OMV.Quaternion Orientation {
        get {
            return RawOrientation;
        }
        set {
            if (RawOrientation == value)
                return;
            RawOrientation = value;

            PhysScene.TaintedObject(LocalID, "BSPrim.setOrientation", delegate()
            {
                ForceOrientation = RawOrientation;
            });
        }
    }
    // Go directly to Bullet to get/set the value.
    public override OMV.Quaternion ForceOrientation
    {
        get
        {
            RawOrientation = PhysScene.PE.GetOrientation(PhysBody);
            return RawOrientation;
        }
        set
        {
            RawOrientation = value;
            if (PhysBody.HasPhysicalBody)
                PhysScene.PE.SetTranslation(PhysBody, RawPosition, RawOrientation);
        }
    }
    public override int PhysicsActorType {
        get { return _physicsActorType; }
        set { _physicsActorType = value; }
    }
    public override bool IsPhysical {
        get { return _isPhysical; }
        set {
            if (_isPhysical != value)
            {
                _isPhysical = value;
                PhysScene.TaintedObject(LocalID, "BSPrim.setIsPhysical", delegate()
                {
                    DetailLog("{0},setIsPhysical,taint,isPhys={1}", LocalID, _isPhysical);
                    SetObjectDynamic(true);
                    // whether phys-to-static or static-to-phys, the object is not moving.
                    ZeroMotion(true);

                });
            }
        }
    }

    // An object is static (does not move) if selected or not physical
    public override bool IsStatic
    {
        get { return _isSelected || !IsPhysical; }
    }

    // An object is solid if it's not phantom and if it's not doing VolumeDetect
    public override bool IsSolid
    {
        get { return !IsPhantom && !_isVolumeDetect; }
    }

    // The object is moving and is actively being dynamic in the physical world
    public override bool IsPhysicallyActive
    {
        get { return !_isSelected && IsPhysical; }
    }

    // Make gravity work if the object is physical and not selected
    // Called at taint-time!!
    private void SetObjectDynamic(bool forceRebuild)
    {
        // Recreate the physical object if necessary
        CreateGeomAndObject(forceRebuild);
    }

    // Convert the simulator's physical properties into settings on BulletSim objects.
    // There are four flags we're interested in:
    //     IsStatic: Object does not move, otherwise the object has mass and moves
    //     isSolid: other objects bounce off of this object
    //     isVolumeDetect: other objects pass through but can generate collisions
    //     collisionEvents: whether this object returns collision events
    // NOTE: overloaded by BSPrimLinkable to also update linkset physical parameters.
    public virtual void UpdatePhysicalParameters()
    {
        if (!PhysBody.HasPhysicalBody)
        {
            // This would only happen if updates are called for during initialization when the body is not set up yet.
            // DetailLog("{0},BSPrim.UpdatePhysicalParameters,taint,calledWithNoPhysBody", LocalID);
            return;
        }

        // Mangling all the physical properties requires the object not be in the physical world.
        // This is a NOOP if the object is not in the world (BulletSim and Bullet ignore objects not found).
        PhysScene.PE.RemoveObjectFromWorld(PhysScene.World, PhysBody);

        // Set up the object physicalness (does gravity and collisions move this object)
        MakeDynamic(IsStatic);

        // Update vehicle specific parameters (after MakeDynamic() so can change physical parameters)
        PhysicalActors.Refresh();

        // Arrange for collision events if the simulator wants them
        EnableCollisions(SubscribedEvents());

        // Make solid or not (do things bounce off or pass through this object).
        MakeSolid(IsSolid);

        AddObjectToPhysicalWorld();

        // Rebuild its shape
        PhysScene.PE.UpdateSingleAabb(PhysScene.World, PhysBody);

        DetailLog("{0},BSPrim.UpdatePhysicalParameters,taintExit,static={1},solid={2},mass={3},collide={4},cf={5:X},cType={6},body={7},shape={8}",
                                    LocalID, IsStatic, IsSolid, Mass, SubscribedEvents(),
                                    CurrentCollisionFlags, PhysBody.collisionType, PhysBody, PhysShape);
    }

    // "Making dynamic" means changing to and from static.
    // When static, gravity does not effect the object and it is fixed in space.
    // When dynamic, the object can fall and be pushed by others.
    // This is independent of its 'solidness' which controls what passes through
    //    this object and what interacts with it.
    protected virtual void MakeDynamic(bool makeStatic)
    {
        if (makeStatic)
        {
            // Become a Bullet 'static' object type
            CurrentCollisionFlags = PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_STATIC_OBJECT);
            // Stop all movement
            ZeroMotion(true);

            // Set various physical properties so other object interact properly
            PhysScene.PE.SetFriction(PhysBody, Friction);
            PhysScene.PE.SetRestitution(PhysBody, Restitution);
            PhysScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);

            // Mass is zero which disables a bunch of physics stuff in Bullet
            UpdatePhysicalMassProperties(0f, false);
            // Set collision detection parameters
            if (BSParam.CcdMotionThreshold > 0f)
            {
                PhysScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                PhysScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
            }

            // The activation state is 'disabled' so Bullet will not try to act on it.
            // PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_SIMULATION);
            // Start it out sleeping and physical actions could wake it up.
            PhysScene.PE.ForceActivationState(PhysBody, ActivationState.ISLAND_SLEEPING);

            // This collides like a static object
            PhysBody.collisionType = CollisionType.Static;
        }
        else
        {
            // Not a Bullet static object
            CurrentCollisionFlags = PhysScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.CF_STATIC_OBJECT);

            // Set various physical properties so other object interact properly
            PhysScene.PE.SetFriction(PhysBody, Friction);
            PhysScene.PE.SetRestitution(PhysBody, Restitution);
            // DetailLog("{0},BSPrim.MakeDynamic,frict={1},rest={2}", LocalID, Friction, Restitution);

            // per http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=3382
            // Since this can be called multiple times, only zero forces when becoming physical
            // PhysicsScene.PE.ClearAllForces(BSBody);

            // For good measure, make sure the transform is set through to the motion state
            ForcePosition = RawPosition;
            ForceVelocity = RawVelocity;
            ForceRotationalVelocity = _rotationalVelocity;

            // A dynamic object has mass
            UpdatePhysicalMassProperties(RawMass, false);

            // Set collision detection parameters
            if (BSParam.CcdMotionThreshold > 0f)
            {
                PhysScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
                PhysScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
            }

            // Various values for simulation limits
            PhysScene.PE.SetDamping(PhysBody, BSParam.LinearDamping, BSParam.AngularDamping);
            PhysScene.PE.SetDeactivationTime(PhysBody, BSParam.DeactivationTime);
            PhysScene.PE.SetSleepingThresholds(PhysBody, BSParam.LinearSleepingThreshold, BSParam.AngularSleepingThreshold);
            PhysScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);

            // This collides like an object.
            PhysBody.collisionType = CollisionType.Dynamic;

            // Force activation of the object so Bullet will act on it.
            // Must do the ForceActivationState2() to overcome the DISABLE_SIMULATION from static objects.
            PhysScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);
        }
    }

    // "Making solid" means that other object will not pass through this object.
    // To make transparent, we create a Bullet ghost object.
    // Note: This expects to be called from the UpdatePhysicalParameters() routine as
    //     the functions after this one set up the state of a possibly newly created collision body.
    private void MakeSolid(bool makeSolid)
    {
        CollisionObjectTypes bodyType = (CollisionObjectTypes)PhysScene.PE.GetBodyType(PhysBody);
        if (makeSolid)
        {
            // Verify the previous code created the correct shape for this type of thing.
            if ((bodyType & CollisionObjectTypes.CO_RIGID_BODY) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for solidity. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = PhysScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);
        }
        else
        {
            if ((bodyType & CollisionObjectTypes.CO_GHOST_OBJECT) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for non-solidness. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_NO_CONTACT_RESPONSE);

            // Change collision info from a static object to a ghosty collision object
            PhysBody.collisionType = CollisionType.VolumeDetect;
        }
    }

    // Turn on or off the flag controlling whether collision events are returned to the simulator.
    private void EnableCollisions(bool wantsCollisionEvents)
    {
        if (wantsCollisionEvents)
        {
            CurrentCollisionFlags = PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
        else
        {
            CurrentCollisionFlags = PhysScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
    }

    // Add me to the physical world.
    // Object MUST NOT already be in the world.
    // This routine exists because some assorted properties get mangled by adding to the world.
    internal void AddObjectToPhysicalWorld()
    {
        if (PhysBody.HasPhysicalBody)
        {
            PhysScene.PE.AddObjectToWorld(PhysScene.World, PhysBody);
        }
        else
        {
            m_log.ErrorFormat("{0} Attempt to add physical object without body. id={1}", LogHeader, LocalID);
            DetailLog("{0},BSPrim.AddObjectToPhysicalWorld,addObjectWithoutBody,cType={1}", LocalID, PhysBody.collisionType);
        }
    }

    // prims don't fly
    public override bool Flying {
        get { return _flying; }
        set {
            _flying = value;
        }
    }
    public override bool SetAlwaysRun {
        get { return _setAlwaysRun; }
        set { _setAlwaysRun = value; }
    }
    public override bool ThrottleUpdates {
        get { return _throttleUpdates; }
        set { _throttleUpdates = value; }
    }
    public bool IsPhantom {
        get {
            // SceneObjectPart removes phantom objects from the physics scene
            // so, although we could implement touching and such, we never
            // are invoked as a phantom object
            return false;
        }
    }
    public override bool FloatOnWater {
        set {
            _floatOnWater = value;
            PhysScene.TaintedObject(LocalID, "BSPrim.setFloatOnWater", delegate()
            {
                if (_floatOnWater)
                    CurrentCollisionFlags = PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
                else
                    CurrentCollisionFlags = PhysScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
            });
        }
    }
    public override OMV.Vector3 RotationalVelocity {
        get {
            return _rotationalVelocity;
        }
        set {
            _rotationalVelocity = value;
            Util.ClampV(_rotationalVelocity, BSParam.MaxAngularVelocity);
            // m_log.DebugFormat("{0}: RotationalVelocity={1}", LogHeader, _rotationalVelocity);
            PhysScene.TaintedObject(LocalID, "BSPrim.setRotationalVelocity", delegate()
            {
                ForceRotationalVelocity = _rotationalVelocity;
            });
        }
    }
    public override OMV.Vector3 ForceRotationalVelocity {
        get {
            return _rotationalVelocity;
        }
        set {
            _rotationalVelocity = Util.ClampV(value, BSParam.MaxAngularVelocity);
            if (PhysBody.HasPhysicalBody)
            {
                DetailLog("{0},BSPrim.ForceRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
                PhysScene.PE.SetAngularVelocity(PhysBody, _rotationalVelocity);
                // PhysicsScene.PE.SetInterpolationAngularVelocity(PhysBody, _rotationalVelocity);
                ActivateIfPhysical(false);
            }
        }
    }
    public override bool Kinematic {
        get { return _kinematic; }
        set { _kinematic = value;
            // m_log.DebugFormat("{0}: Kinematic={1}", LogHeader, _kinematic);
        }
    }
    public override float Buoyancy {
        get { return _buoyancy; }
        set {
            _buoyancy = value;
            PhysScene.TaintedObject(LocalID, "BSPrim.setBuoyancy", delegate()
            {
                ForceBuoyancy = _buoyancy;
            });
        }
    }
    public override float ForceBuoyancy {
        get { return _buoyancy; }
        set {
            _buoyancy = value;
            // DetailLog("{0},BSPrim.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
            // Force the recalculation of the various inertia,etc variables in the object
            UpdatePhysicalMassProperties(RawMass, true);
            DetailLog("{0},BSPrim.ForceBuoyancy,buoy={1},mass={2},grav={3}", LocalID, _buoyancy, RawMass, Gravity);
            ActivateIfPhysical(false);
        }
    }

    public override bool PIDActive 
    {
        get
        {
            return MoveToTargetActive;
        }

        set 
        {
            MoveToTargetActive = value;

            EnableActor(MoveToTargetActive, MoveToTargetActorName, delegate()
            {
                return new BSActorMoveToTarget(PhysScene, this, MoveToTargetActorName);
            });

            // Call update so actor Refresh() is called to start things off
            PhysScene.TaintedObject(LocalID, "BSPrim.PIDActive", delegate()
            {
                UpdatePhysicalParameters();
            });
        }
    }

    public override OMV.Vector3 PIDTarget
    {
        set
        {
            base.PIDTarget = value;
            BSActor actor;
            if (PhysicalActors.TryGetActor(MoveToTargetActorName, out actor))
            {
                // if the actor exists, tell it to refresh its values.
                actor.Refresh();
            }
            
        }
    }
    // Used for llSetHoverHeight and maybe vehicle height
    // Hover Height will override MoveTo target's Z
    public override bool PIDHoverActive {
        set {
            base.HoverActive = value;
            EnableActor(HoverActive, HoverActorName, delegate()
            {
                return new BSActorHover(PhysScene, this, HoverActorName);
            });

            // Call update so actor Refresh() is called to start things off
            PhysScene.TaintedObject(LocalID, "BSPrim.PIDHoverActive", delegate()
            {
                UpdatePhysicalParameters();
            });
        }
    }

    public override void AddForce(OMV.Vector3 force, bool pushforce) {
        // Per documentation, max force is limited.
        OMV.Vector3 addForce = Util.ClampV(force, BSParam.MaxAddForceMagnitude);

        // Since this force is being applied in only one step, make this a force per second.
        addForce /= PhysScene.LastTimeStep;
        AddForce(addForce, pushforce, false /* inTaintTime */);
    }

    // Applying a force just adds this to the total force on the object.
    // This added force will only last the next simulation tick.
    public override void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
        // for an object, doesn't matter if force is a pushforce or not
        if (IsPhysicallyActive)
        {
            if (force.IsFinite())
            {
                // DetailLog("{0},BSPrim.addForce,call,force={1}", LocalID, addForce);

                OMV.Vector3 addForce = force;
                PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.AddForce", delegate()
                {
                    // Bullet adds this central force to the total force for this tick.
                    // Deep down in Bullet:
                    //      linearVelocity += totalForce / mass * timeStep;
                    DetailLog("{0},BSPrim.addForce,taint,force={1}", LocalID, addForce);
                    if (PhysBody.HasPhysicalBody)
                    {
                        PhysScene.PE.ApplyCentralForce(PhysBody, addForce);
                        ActivateIfPhysical(false);
                    }
                });
            }
            else
            {
                m_log.WarnFormat("{0}: AddForce: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
                return;
            }
        }
    }

    public void AddForceImpulse(OMV.Vector3 impulse, bool pushforce, bool inTaintTime) {
        // for an object, doesn't matter if force is a pushforce or not
        if (!IsPhysicallyActive)
        {
            if (impulse.IsFinite())
            {
                OMV.Vector3 addImpulse = Util.ClampV(impulse, BSParam.MaxAddForceMagnitude);
                // DetailLog("{0},BSPrim.addForceImpulse,call,impulse={1}", LocalID, impulse);

                PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.AddImpulse", delegate()
                {
                    // Bullet adds this impulse immediately to the velocity
                    DetailLog("{0},BSPrim.addForceImpulse,taint,impulseforce={1}", LocalID, addImpulse);
                    if (PhysBody.HasPhysicalBody)
                    {
                        PhysScene.PE.ApplyCentralImpulse(PhysBody, addImpulse);
                        ActivateIfPhysical(false);
                    }
                });
            }
            else
            {
                m_log.WarnFormat("{0}: AddForceImpulse: Got a NaN impulse applied to a prim. LocalID={1}", LogHeader, LocalID);
                return;
            }
        }
    }

    // BSPhysObject.AddAngularForce()
    public override void AddAngularForce(OMV.Vector3 force, bool pushforce, bool inTaintTime)
    {
        if (force.IsFinite())
        {
            OMV.Vector3 angForce = force;
            PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.AddAngularForce", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    DetailLog("{0},BSPrim.AddAngularForce,taint,angForce={1}", LocalID, angForce);
                    PhysScene.PE.ApplyTorque(PhysBody, angForce);
                    ActivateIfPhysical(false);
                }
            });
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
            return;
        }
    }

    // A torque impulse.
    // ApplyTorqueImpulse adds torque directly to the angularVelocity.
    // AddAngularForce accumulates the force and applied it to the angular velocity all at once.
    // Computed as: angularVelocity += impulse * inertia;
    public void ApplyTorqueImpulse(OMV.Vector3 impulse, bool inTaintTime)
    {
        OMV.Vector3 applyImpulse = impulse;
        PhysScene.TaintedObject(inTaintTime, LocalID, "BSPrim.ApplyTorqueImpulse", delegate()
        {
            if (PhysBody.HasPhysicalBody)
            {
                PhysScene.PE.ApplyTorqueImpulse(PhysBody, applyImpulse);
                ActivateIfPhysical(false);
            }
        });
    }

    public override void SetMomentum(OMV.Vector3 momentum) {
        // DetailLog("{0},BSPrim.SetMomentum,call,mom={1}", LocalID, momentum);
    }
    #region Mass Calculation

    private float CalculateMass()
    {
        float volume = _size.X * _size.Y * _size.Z; // default
        float tmp;

        float returnMass = 0;
        float hollowAmount = (float)BaseShape.ProfileHollow * 2.0e-5f;
        float hollowVolume = hollowAmount * hollowAmount;

        switch (BaseShape.ProfileShape)
        {
            case ProfileShape.Square:
                // default box

                if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                    if (hollowAmount > 0.0)
                        {
                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                break;

                            case HollowShape.Circle:

                                hollowVolume *= 0.78539816339f;
                                break;

                            case HollowShape.Triangle:

                                hollowVolume *= (0.5f * .5f);
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                    //a tube

                    volume *= 0.78539816339e-2f * (float)(200 - BaseShape.PathScaleX);
                    tmp= 1.0f -2.0e-2f * (float)(200 - BaseShape.PathScaleY);
                    volume -= volume*tmp*tmp;

                    if (hollowAmount > 0.0)
                        {
                        hollowVolume *= hollowAmount;

                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Square:
                            case HollowShape.Same:
                                break;

                            case HollowShape.Circle:
                                hollowVolume *= 0.78539816339f;;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= 0.5f * 0.5f;
                                break;
                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                break;

            case ProfileShape.Circle:

                if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.78539816339f; // elipse base

                    if (hollowAmount > 0.0)
                        {
                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Circle:
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.5f * 2.5984480504799f;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= .5f * 1.27323954473516f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }

                else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - BaseShape.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - BaseShape.PathScaleY);
                    volume *= (1.0f - tmp * tmp);

                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        hollowVolume *= hollowAmount;

                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Circle:
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.5f * 2.5984480504799f;
                                break;

                            case HollowShape.Triangle:
                                hollowVolume *= .5f * 1.27323954473516f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                break;

            case ProfileShape.HalfCircle:
                if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                {
                volume *= 0.52359877559829887307710723054658f;
                }
                break;

            case ProfileShape.EquilateralTriangle:

                if (BaseShape.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.32475953f;

                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Triangle:
                                hollowVolume *= .25f;
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.499849f * 3.07920140172638f;
                                break;

                            case HollowShape.Circle:
                                // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                // Cyllinder hollow volume calculation

                                hollowVolume *= 0.1963495f * 3.07920140172638f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                else if (BaseShape.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.32475953f;
                    volume *= 0.01f * (float)(200 - BaseShape.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - BaseShape.PathScaleY);
                    volume *= (1.0f - tmp * tmp);

                    if (hollowAmount > 0.0)
                        {

                        hollowVolume *= hollowAmount;

                        switch (BaseShape.HollowShape)
                            {
                            case HollowShape.Same:
                            case HollowShape.Triangle:
                                hollowVolume *= .25f;
                                break;

                            case HollowShape.Square:
                                hollowVolume *= 0.499849f * 3.07920140172638f;
                                break;

                            case HollowShape.Circle:

                                hollowVolume *= 0.1963495f * 3.07920140172638f;
                                break;

                            default:
                                hollowVolume = 0;
                                break;
                            }
                        volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

            default:
                break;
            }



        float taperX1;
        float taperY1;
        float taperX;
        float taperY;
        float pathBegin;
        float pathEnd;
        float profileBegin;
        float profileEnd;

        if (BaseShape.PathCurve == (byte)Extrusion.Straight || BaseShape.PathCurve == (byte)Extrusion.Flexible)
            {
            taperX1 = BaseShape.PathScaleX * 0.01f;
            if (taperX1 > 1.0f)
                taperX1 = 2.0f - taperX1;
            taperX = 1.0f - taperX1;

            taperY1 = BaseShape.PathScaleY * 0.01f;
            if (taperY1 > 1.0f)
                taperY1 = 2.0f - taperY1;
            taperY = 1.0f - taperY1;
            }
        else
            {
            taperX = BaseShape.PathTaperX * 0.01f;
            if (taperX < 0.0f)
                taperX = -taperX;
            taperX1 = 1.0f - taperX;

            taperY = BaseShape.PathTaperY * 0.01f;
            if (taperY < 0.0f)
                taperY = -taperY;
            taperY1 = 1.0f - taperY;

            }


        volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

        pathBegin = (float)BaseShape.PathBegin * 2.0e-5f;
        pathEnd = 1.0f - (float)BaseShape.PathEnd * 2.0e-5f;
        volume *= (pathEnd - pathBegin);

        // this is crude aproximation
        profileBegin = (float)BaseShape.ProfileBegin * 2.0e-5f;
        profileEnd = 1.0f - (float)BaseShape.ProfileEnd * 2.0e-5f;
        volume *= (profileEnd - profileBegin);

        returnMass = Density * BSParam.DensityScaleFactor * volume;

        returnMass = Util.Clamp(returnMass, BSParam.MinimumObjectMass, BSParam.MaximumObjectMass);
        // DetailLog("{0},BSPrim.CalculateMass,den={1},vol={2},mass={3}", LocalID, Density, volume, returnMass);
        DetailLog("{0},BSPrim.CalculateMass,den={1},vol={2},mass={3},pathB={4},pathE={5},profB={6},profE={7},siz={8}",
                            LocalID, Density, volume, returnMass, pathBegin, pathEnd, profileBegin, profileEnd, _size);

        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // Called at taint-time!!!
    public void CreateGeomAndObject(bool forceRebuild)
    {
        // Create the correct physical representation for this type of object.
        // Updates base.PhysBody and base.PhysShape with the new information.
        // Ignore 'forceRebuild'. 'GetBodyAndShape' makes the right choices and changes of necessary.
        PhysScene.Shapes.GetBodyAndShape(false /*forceRebuild */, PhysScene.World, this, delegate(BulletBody pBody, BulletShape pShape)
        {
            // Called if the current prim body is about to be destroyed.
            // Remove all the physical dependencies on the old body.
            // (Maybe someday make the changing of BSShape an event to be subscribed to by BSLinkset, ...)
            // Note: this virtual function is overloaded by BSPrimLinkable to remove linkset constraints.
            RemoveDependencies();
        });

        // Make sure the properties are set on the new object
        UpdatePhysicalParameters();
        return;
    }

    // Called at taint-time
    protected virtual void RemoveDependencies()
    {
        PhysicalActors.RemoveDependencies();
    }

    #region Extension
    public override object Extension(string pFunct, params object[] pParams)
    {
        DetailLog("{0} BSPrim.Extension,op={1}", LocalID, pFunct);
        object ret = null;
        switch (pFunct)
        {
            case ExtendedPhysics.PhysFunctAxisLockLimits:
                ret = SetAxisLockLimitsExtension(pParams);
                break;
            default:
                ret = base.Extension(pFunct, pParams);
                break;
        }
        return ret;
    }

    private void InitializeAxisActor()
    {
        EnableActor(LockedAngularAxis != LockedAxisFree || LockedLinearAxis != LockedAxisFree,
                                    LockedAxisActorName, delegate()
        {
            return new BSActorLockAxis(PhysScene, this, LockedAxisActorName);
        });

        // Update parameters so the new actor's Refresh() action is called at the right time.
        PhysScene.TaintedObject(LocalID, "BSPrim.LockAxis", delegate()
        {
            UpdatePhysicalParameters();
        });
    }

    // Passed an array of an array of parameters, set the axis locking.
    // This expects an int (PHYS_AXIS_*) followed by none or two limit floats
    //    followed by another int and floats, etc.
    private object SetAxisLockLimitsExtension(object[] pParams)
    {
        DetailLog("{0} SetAxisLockLimitsExtension. parmlen={1}", LocalID, pParams.GetLength(0));
        object ret = null;
        try
        {
            if (pParams.GetLength(0) > 1)
            {
                int index = 2;
                while (index < pParams.GetLength(0))
                {
                    var funct = pParams[index];
                    DetailLog("{0} SetAxisLockLimitsExtension. op={1}, index={2}", LocalID, funct, index);
                    if (funct is Int32 || funct is Int64)
                    {
                        switch ((int)funct)
                        {
                            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_X, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_X, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Y, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Y, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Z, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Z, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_X, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Y, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Z, (float)pParams[index + 1], (float)pParams[index + 2]);
                                index += 3;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_X, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Y, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Z, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_X:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_X, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Y:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Y, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Z:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Z, 0f, 0f);
                                index += 1;
                                break;
                            case ExtendedPhysics.PHYS_AXIS_UNLOCK:
                                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK, 0f, 0f);
                                index += 1;
                                break;
                            default:
                                m_log.WarnFormat("{0} SetSxisLockLimitsExtension. Unknown op={1}", LogHeader, funct);
                                index += 1;
                                break;
                        }
                    }
                }
                InitializeAxisActor();
                ret = (object)index;
            }
        }
        catch (Exception e)
        {
            m_log.WarnFormat("{0} SetSxisLockLimitsExtension exception in object {1}: {2}", LogHeader, this.Name, e);
            ret = null;
        }
        return ret;    // not implemented yet
    }

        // Set the locking parameters.
        // If an axis is locked, the limits for the axis are set to zero,
        // If the axis is being constrained, the high and low value are passed and set.
        // When done here, LockedXXXAxis flags are set and LockedXXXAxixLow/High are set to the range.
    protected void ApplyAxisLimits(int funct, float low, float high)
    {
        DetailLog("{0} ApplyAxisLimits. op={1}, low={2}, high={3}", LocalID, funct, low, high);
        float linearMax = 23000f;
        float angularMax = (float)Math.PI;

        switch ((int)funct)
        {
            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR:
                this.LockedLinearAxis = new OMV.Vector3(LockedAxis, LockedAxis, LockedAxis);
                this.LockedLinearAxisLow = OMV.Vector3.Zero;
                this.LockedLinearAxisHigh = OMV.Vector3.Zero;
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_X:
                this.LockedLinearAxis.X = LockedAxis;
                this.LockedLinearAxisLow.X = 0f;
                this.LockedLinearAxisHigh.X = 0f;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_X:
                this.LockedLinearAxis.X = LockedAxis;
                this.LockedLinearAxisLow.X = Util.Clip(low, -linearMax, linearMax);
                this.LockedLinearAxisHigh.X = Util.Clip(high, -linearMax, linearMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Y:
                this.LockedLinearAxis.Y = LockedAxis;
                this.LockedLinearAxisLow.Y = 0f;
                this.LockedLinearAxisHigh.Y = 0f;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Y:
                this.LockedLinearAxis.Y = LockedAxis;
                this.LockedLinearAxisLow.Y = Util.Clip(low, -linearMax, linearMax);
                this.LockedLinearAxisHigh.Y = Util.Clip(high, -linearMax, linearMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_LINEAR_Z:
                this.LockedLinearAxis.Z = LockedAxis;
                this.LockedLinearAxisLow.Z = 0f;
                this.LockedLinearAxisHigh.Z = 0f;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_LINEAR_Z:
                this.LockedLinearAxis.Z = LockedAxis;
                this.LockedLinearAxisLow.Z = Util.Clip(low, -linearMax, linearMax);
                this.LockedLinearAxisHigh.Z = Util.Clip(high, -linearMax, linearMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR:
                this.LockedAngularAxis = new OMV.Vector3(LockedAxis, LockedAxis, LockedAxis);
                this.LockedAngularAxisLow = OMV.Vector3.Zero;
                this.LockedAngularAxisHigh = OMV.Vector3.Zero;
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_X:
                this.LockedAngularAxis.X = LockedAxis;
                this.LockedAngularAxisLow.X = 0;
                this.LockedAngularAxisHigh.X = 0;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_X:
                this.LockedAngularAxis.X = LockedAxis;
                this.LockedAngularAxisLow.X = Util.Clip(low, -angularMax, angularMax);
                this.LockedAngularAxisHigh.X = Util.Clip(high, -angularMax, angularMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Y:
                this.LockedAngularAxis.Y = LockedAxis;
                this.LockedAngularAxisLow.Y = 0;
                this.LockedAngularAxisHigh.Y = 0;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Y:
                this.LockedAngularAxis.Y = LockedAxis;
                this.LockedAngularAxisLow.Y = Util.Clip(low, -angularMax, angularMax);
                this.LockedAngularAxisHigh.Y = Util.Clip(high, -angularMax, angularMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_LOCK_ANGULAR_Z:
                this.LockedAngularAxis.Z = LockedAxis;
                this.LockedAngularAxisLow.Z = 0;
                this.LockedAngularAxisHigh.Z = 0;
                break;
            case ExtendedPhysics.PHYS_AXIS_LIMIT_ANGULAR_Z:
                this.LockedAngularAxis.Z = LockedAxis;
                this.LockedAngularAxisLow.Z = Util.Clip(low, -angularMax, angularMax);
                this.LockedAngularAxisHigh.Z = Util.Clip(high, -angularMax, angularMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR:
                this.LockedLinearAxis = LockedAxisFree;
                this.LockedLinearAxisLow = new OMV.Vector3(-linearMax, -linearMax, -linearMax);
                this.LockedLinearAxisHigh = new OMV.Vector3(linearMax, linearMax, linearMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_X:
                this.LockedLinearAxis.X = FreeAxis;
                this.LockedLinearAxisLow.X = -linearMax;
                this.LockedLinearAxisHigh.X = linearMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Y:
                this.LockedLinearAxis.Y = FreeAxis;
                this.LockedLinearAxisLow.Y = -linearMax;
                this.LockedLinearAxisHigh.Y = linearMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR_Z:
                this.LockedLinearAxis.Z = FreeAxis;
                this.LockedLinearAxisLow.Z = -linearMax;
                this.LockedLinearAxisHigh.Z = linearMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR:
                this.LockedAngularAxis = LockedAxisFree;
                this.LockedAngularAxisLow = new OMV.Vector3(-angularMax, -angularMax, -angularMax);
                this.LockedAngularAxisHigh = new OMV.Vector3(angularMax, angularMax, angularMax);
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_X:
                this.LockedAngularAxis.X = FreeAxis;
                this.LockedAngularAxisLow.X = -angularMax;
                this.LockedAngularAxisHigh.X = angularMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Y:
                this.LockedAngularAxis.Y = FreeAxis;
                this.LockedAngularAxisLow.Y = -angularMax;
                this.LockedAngularAxisHigh.Y = angularMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR_Z:
                this.LockedAngularAxis.Z = FreeAxis;
                this.LockedAngularAxisLow.Z = -angularMax;
                this.LockedAngularAxisHigh.Z = angularMax;
                break;
            case ExtendedPhysics.PHYS_AXIS_UNLOCK:
                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_LINEAR, 0f, 0f);
                ApplyAxisLimits(ExtendedPhysics.PHYS_AXIS_UNLOCK_ANGULAR, 0f, 0f);
                break;
            default:
                break;
        }
        return;
    }
    #endregion  // Extension

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    // NOTE: BSPrim.UpdateProperties is overloaded by BSPrimLinkable which modifies updates from root and children prims.
    // NOTE: BSPrim.UpdateProperties is overloaded by BSPrimDisplaced which handles mapping physical position to simulator position.
    public override void UpdateProperties(EntityProperties entprop)
    {
        // Let anyone (like the actors) modify the updated properties before they are pushed into the object and the simulator.
        TriggerPreUpdatePropertyAction(ref entprop);

        // DetailLog("{0},BSPrim.UpdateProperties,entry,entprop={1}", LocalID, entprop);   // DEBUG DEBUG

        // Assign directly to the local variables so the normal set actions do not happen
        RawPosition = entprop.Position;
        RawOrientation = entprop.Rotation;
        // DEBUG DEBUG DEBUG -- smooth velocity changes a bit. The simulator seems to be
        //    very sensitive to velocity changes.
        if (entprop.Velocity == OMV.Vector3.Zero || !entprop.Velocity.ApproxEquals(RawVelocity, BSParam.UpdateVelocityChangeThreshold))
            RawVelocity = entprop.Velocity;
        _acceleration = entprop.Acceleration;
        _rotationalVelocity = entprop.RotationalVelocity;

        // DetailLog("{0},BSPrim.UpdateProperties,afterAssign,entprop={1}", LocalID, entprop);   // DEBUG DEBUG

        // The sanity check can change the velocity and/or position.
        if (PositionSanityCheck(true /* inTaintTime */ ))
        {
            entprop.Position = RawPosition;
            entprop.Velocity = RawVelocity;
            entprop.RotationalVelocity = _rotationalVelocity;
            entprop.Acceleration = _acceleration;
        }

        OMV.Vector3 direction = OMV.Vector3.UnitX * RawOrientation;   // DEBUG DEBUG DEBUG
        DetailLog("{0},BSPrim.UpdateProperties,call,entProp={1},dir={2}", LocalID, entprop, direction);

        // remember the current and last set values
        LastEntityProperties = CurrentEntityProperties;
        CurrentEntityProperties = entprop;

        PhysScene.PostUpdate(this);
    }
}
}
