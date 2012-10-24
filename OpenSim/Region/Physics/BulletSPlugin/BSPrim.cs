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

// Uncomment this it enable code to do all shape an body memory management
//    in the C# code.
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using log4net;
using OMV = OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Physics.ConvexDecompositionDotNet;

namespace OpenSim.Region.Physics.BulletSPlugin
{

    [Serializable]
public sealed class BSPrim : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

    // _size is what the user passed. Scale is what we pass to the physics engine with the mesh.
    // Often Scale is unity because the meshmerizer will apply _size when creating the mesh.
    private OMV.Vector3 _size;  // the multiplier for each mesh dimension as passed by the user
    // private OMV.Vector3 _scale; // the multiplier for each mesh dimension for the mesh as created by the meshmerizer

    private bool _grabbed;
    private bool _isSelected;
    private bool _isVolumeDetect;
    private OMV.Vector3 _position;
    private float _mass;    // the mass of this object
    private float _density;
    private OMV.Vector3 _force;
    private OMV.Vector3 _velocity;
    private OMV.Vector3 _torque;
    private float _collisionScore;
    private OMV.Vector3 _acceleration;
    private OMV.Quaternion _orientation;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private float _friction;
    private float _restitution;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _isColliding;
    private bool _collidingGround;
    private bool _collidingObj;
    private bool _floatOnWater;
    private OMV.Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    private BSDynamics _vehicle;

    private OMV.Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSPrim(uint localID, String primName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size,
                       OMV.Quaternion rotation, PrimitiveBaseShape pbs, bool pisPhysical)
    {
        // m_log.DebugFormat("{0}: BSPrim creation of {1}, id={2}", LogHeader, primName, localID);
        base.BaseInitialize(parent_scene, localID, primName, "BSPrim");
        _physicsActorType = (int)ActorTypes.Prim;
        _position = pos;
        _size = size;
        Scale = new OMV.Vector3(1f, 1f, 1f);   // the scale will be set by CreateGeom depending on object type
        _orientation = rotation;
        _buoyancy = 1f;
        _velocity = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;
        BaseShape = pbs;
        _isPhysical = pisPhysical;
        _isVolumeDetect = false;
        _friction = PhysicsScene.Params.defaultFriction;  // TODO: compute based on object material
        _density = PhysicsScene.Params.defaultDensity;    // TODO: compute based on object material
        _restitution = PhysicsScene.Params.defaultRestitution;
        _vehicle = new BSDynamics(PhysicsScene, this);            // add vehicleness
        _mass = CalculateMass();

        // No body or shape yet
        BSBody = new BulletBody(LocalID, IntPtr.Zero);
        BSShape = new BulletShape(IntPtr.Zero);

        DetailLog("{0},BSPrim.constructor,call", LocalID);
        // do the actual object creation at taint time
        PhysicsScene.TaintedObject("BSPrim.create", delegate()
        {
            CreateGeomAndObject(true);

            CurrentCollisionFlags = BulletSimAPI.GetCollisionFlags2(BSBody.ptr);
        });
    }

    // called when this prim is being destroyed and we should free all the resources
    public override void Destroy()
    {
        // m_log.DebugFormat("{0}: Destroy, id={1}", LogHeader, LocalID);

        // Undo any links between me and any other object
        BSPhysObject parentBefore = Linkset.LinksetRoot;
        int childrenBefore = Linkset.NumberOfChildren;

        Linkset = Linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.Destroy,call,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}",
            LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);

        // Undo any vehicle properties
        this.VehicleType = (int)Vehicle.TYPE_NONE;

        PhysicsScene.TaintedObject("BSPrim.destroy", delegate()
        {
            DetailLog("{0},BSPrim.Destroy,taint,", LocalID);
            // If there are physical body and shape, release my use of same.
            PhysicsScene.Shapes.DereferenceBody(BSBody, true, null);
            PhysicsScene.Shapes.DereferenceShape(BSShape, true, null);
        });
    }

    // No one uses this property.
    public override bool Stopped {
        get { return false; }
    }
    public override OMV.Vector3 Size {
        get { return _size; }
        set {
            _size = value;
            ForceBodyShapeRebuild(false);
        }
    }
    // Scale is what we set in the physics engine. It is different than 'size' in that
    //     'size' can be encorporated into the mesh. In that case, the scale is <1,1,1>.
    public override OMV.Vector3 Scale { get; set; }

    public override PrimitiveBaseShape Shape {
        set {
            BaseShape = value;
            ForceBodyShapeRebuild(false);
        }
    }
    public override bool ForceBodyShapeRebuild(bool inTaintTime)
    {
        LastAssetBuildFailed = false;
        BSScene.TaintCallback rebuildOperation = delegate()
        {
            _mass = CalculateMass();   // changing the shape changes the mass
            CreateGeomAndObject(true);
        };
        if (inTaintTime)
            rebuildOperation();
        else
            PhysicsScene.TaintedObject("BSPrim.ForceBodyShapeRebuild", rebuildOperation);
        return true;
    }
    public override bool Grabbed {
        set { _grabbed = value;
        }
    }
    public override bool Selected {
        set {
            _isSelected = value;
            PhysicsScene.TaintedObject("BSPrim.setSelected", delegate()
            {
                DetailLog("{0},BSPrim.selected,taint,selected={1}", LocalID, _isSelected);
                SetObjectDynamic(false);
            });
        }
    }
    public override void CrossingFailure() { return; }

    // link me to the specified parent
    public override void link(PhysicsActor obj) {
        BSPrim parent = obj as BSPrim;
        if (parent != null)
        {
            BSPhysObject parentBefore = Linkset.LinksetRoot;
            int childrenBefore = Linkset.NumberOfChildren;

            Linkset = parent.Linkset.AddMeToLinkset(this);

            DetailLog("{0},BSPrim.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}",
                LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        }
        return;
    }

    // delink me from my linkset
    public override void delink() {
        // TODO: decide if this parent checking needs to happen at taint time
        // Race condition here: if link() and delink() in same simulation tick, the delink will not happen

        BSPhysObject parentBefore = Linkset.LinksetRoot;
        int childrenBefore = Linkset.NumberOfChildren;

        Linkset = Linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ",
            LocalID, parentBefore.LocalID, childrenBefore, Linkset.LinksetRoot.LocalID, Linkset.NumberOfChildren);
        return;
    }

    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public override void ZeroMotion()
    {
        _velocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties directly into the physics engine
        BulletSimAPI.ClearForces2(BSBody.ptr);
    }

    public override void LockAngularMotion(OMV.Vector3 axis)
    {
        DetailLog("{0},BSPrim.LockAngularMotion,call,axis={1}", LocalID, axis);
        return;
    }

    public override OMV.Vector3 Position {
        get {
            if (!Linkset.IsRoot(this))
                // child prims move around based on their parent. Need to get the latest location
                _position = BulletSimAPI.GetPosition2(BSBody.ptr);

            // don't do the GetObjectPosition for root elements because this function is called a zillion times
            // _position = BulletSimAPI.GetObjectPosition2(PhysicsScene.World.ptr, BSBody.ptr);
            return _position;
        }
        set {
            // If you must push the position into the physics engine, use ForcePosition.
            if (_position == value)
            {
                return;
            }
            _position = value;
            // TODO: what does it mean to set the position of a child prim?? Rebuild the constraint?
            PositionSanityCheck();
            PhysicsScene.TaintedObject("BSPrim.setPosition", delegate()
            {
                // DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.ptr, _position, _orientation);
            });
        }
    }
    public override OMV.Vector3 ForcePosition {
        get {
            _position = BulletSimAPI.GetPosition2(BSBody.ptr);
            return _position;
        }
        set {
            _position = value;
            PositionSanityCheck();
            BulletSimAPI.SetTranslation2(BSBody.ptr, _position, _orientation);
        }
    }

    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain and being out of bounds.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck()
    {
        bool ret = false;

        // If totally below the ground, move the prim up
        // TODO: figure out the right solution for this... only for dynamic objects?
        /*
        float terrainHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(_position);
        if (Position.Z < terrainHeight)
        {
            DetailLog("{0},BSPrim.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, _position, terrainHeight);
            _position.Z = terrainHeight + 2.0f;
            ret = true;
        }
         */
        if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
        {
            float waterHeight = PhysicsScene.GetWaterLevelAtXYZ(_position);
            if (Position.Z < waterHeight)
            {
                _position.Z = waterHeight;
                ret = true;
            }
        }

        // TODO: check for out of bounds
        return ret;
    }

    // A version of the sanity check that also makes sure a new position value is
    //    pushed to the physics engine. This routine would be used by anyone
    //    who is not already pushing the value.
    private bool PositionSanityCheck(bool inTaintTime)
    {
        bool ret = false;
        if (PositionSanityCheck())
        {
            // The new position value must be pushed into the physics engine but we can't
            //    just assign to "Position" because of potential call loops.
            BSScene.TaintCallback sanityOperation = delegate()
            {
                DetailLog("{0},BSPrim.PositionSanityCheck,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                ForcePosition = _position;
            };
            if (inTaintTime)
                sanityOperation();
            else
                PhysicsScene.TaintedObject("BSPrim.PositionSanityCheck", sanityOperation);

            ret = true;
        }
        return ret;
    }

    // Return the effective mass of the object.
    // If there are multiple items in the linkset, add them together for the root
    public override float Mass
    {
        get
        {
            // return Linkset.LinksetMass;
            return _mass;
        }
    }

    // used when we only want this prim's mass and not the linkset thing
    public override float MassRaw { get { return _mass; }  }

    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return Linkset.CenterOfMass; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return Linkset.GeometricCenter; }
    }

    public override OMV.Vector3 Force {
        get { return _force; }
        set {
            _force = value;
            PhysicsScene.TaintedObject("BSPrim.setForce", delegate()
            {
                // DetailLog("{0},BSPrim.setForce,taint,force={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce2(BSBody.ptr, _force);
            });
        }
    }

    public override int VehicleType {
        get {
            return (int)_vehicle.Type;   // if we are a vehicle, return that type
        }
        set {
            Vehicle type = (Vehicle)value;

            // Tell the scene about the vehicle so it will get processing each frame.
            PhysicsScene.VehicleInSceneTypeChanged(this, type);

            PhysicsScene.TaintedObject("setVehicleType", delegate()
            {
                // Done at taint time so we're sure the physics engine is not using the variables
                // Vehicle code changes the parameters for this vehicle type.
                _vehicle.ProcessTypeChange(type);
            });
        }
    }
    public override void VehicleFloatParam(int param, float value)
    {
        PhysicsScene.TaintedObject("BSPrim.VehicleFloatParam", delegate()
        {
            _vehicle.ProcessFloatVehicleParam((Vehicle)param, value);
        });
    }
    public override void VehicleVectorParam(int param, OMV.Vector3 value)
    {
        PhysicsScene.TaintedObject("BSPrim.VehicleVectorParam", delegate()
        {
            _vehicle.ProcessVectorVehicleParam((Vehicle)param, value);
        });
    }
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation)
    {
        PhysicsScene.TaintedObject("BSPrim.VehicleRotationParam", delegate()
        {
            _vehicle.ProcessRotationVehicleParam((Vehicle)param, rotation);
        });
    }
    public override void VehicleFlags(int param, bool remove)
    {
        PhysicsScene.TaintedObject("BSPrim.VehicleFlags", delegate()
        {
            _vehicle.ProcessVehicleFlags(param, remove);
        });
    }

    // Called each simulation step to advance vehicle characteristics.
    // Called from Scene when doing simulation step so we're in taint processing time.
    public override void StepVehicle(float timeStep)
    {
        if (IsPhysical)
        {
            _vehicle.Step(timeStep);
        }
    }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        if (_isVolumeDetect != newValue)
        {
            _isVolumeDetect = newValue;
            PhysicsScene.TaintedObject("BSPrim.SetVolumeDetect", delegate()
            {
                // DetailLog("{0},setVolumeDetect,taint,volDetect={1}", LocalID, _isVolumeDetect);
                SetObjectDynamic(true);
            });
        }
        return;
    }
    public override OMV.Vector3 Velocity {
        get { return _velocity; }
        set {
            _velocity = value;
            PhysicsScene.TaintedObject("BSPrim.setVelocity", delegate()
            {
                // DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetLinearVelocity2(BSBody.ptr, _velocity);
            });
        }
    }
    public override OMV.Vector3 ForceVelocity {
        get { return _velocity; }
        set {
            _velocity = value;
            BulletSimAPI.SetLinearVelocity2(BSBody.ptr, _velocity);
        }
    }
    public override OMV.Vector3 Torque {
        get { return _torque; }
        set { _torque = value;
            // DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, _torque);
        }
    }
    public override float CollisionScore {
        get { return _collisionScore; }
        set { _collisionScore = value;
        }
    }
    public override OMV.Vector3 Acceleration {
        get { return _acceleration; }
        set { _acceleration = value; }
    }
    public override OMV.Quaternion Orientation {
        get {
            if (!Linkset.IsRoot(this))
            {
                // Children move around because tied to parent. Get a fresh value.
                _orientation = BulletSimAPI.GetOrientation2(BSBody.ptr);
            }
            return _orientation;
        }
        set {
            if (_orientation == value)
                return;
            _orientation = value;
            // TODO: what does it mean if a child in a linkset changes its orientation? Rebuild the constraint?
            PhysicsScene.TaintedObject("BSPrim.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition2(PhysicsScene.World.ptr, BSBody.ptr);
                // DetailLog("{0},BSPrim.setOrientation,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.ptr, _position, _orientation);
            });
        }
    }
    // Go directly to Bullet to get/set the value.
    public override OMV.Quaternion ForceOrientation
    {
        get
        {
            _orientation = BulletSimAPI.GetOrientation2(BSBody.ptr);
            return _orientation;
        }
        set
        {
            _orientation = value;
            BulletSimAPI.SetTranslation2(BSBody.ptr, _position, _orientation);
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
                PhysicsScene.TaintedObject("BSPrim.setIsPhysical", delegate()
                {
                    // DetailLog("{0},setIsPhysical,taint,isPhys={1}", LocalID, _isPhysical);
                    SetObjectDynamic(true);
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
    private void UpdatePhysicalParameters()
    {
        // DetailLog("{0},BSPrim.UpdatePhysicalParameters,entry,body={1},shape={2}", LocalID, BSBody, BSShape);

        // Mangling all the physical properties requires the object not be in the physical world.
        // This is a NOOP if the object is not in the world (BulletSim and Bullet ignore objects not found).
        BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, BSBody.ptr);

        // Set up the object physicalness (does gravity and collisions move this object)
        MakeDynamic(IsStatic);

        // Update vehicle specific parameters (after MakeDynamic() so can change physical parameters)
        _vehicle.Refresh();

        // Arrange for collision events if the simulator wants them
        EnableCollisions(SubscribedEvents());

        // Make solid or not (do things bounce off or pass through this object).
        MakeSolid(IsSolid);

        BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, BSBody.ptr);

        // Rebuild its shape
        BulletSimAPI.UpdateSingleAabb2(PhysicsScene.World.ptr, BSBody.ptr);

        // Collision filter can be set only when the object is in the world
        if (BSBody.collisionFilter != 0 || BSBody.collisionMask != 0)
        {
            BulletSimAPI.SetCollisionFilterMask2(BSBody.ptr, (uint)BSBody.collisionFilter, (uint)BSBody.collisionMask);
        }

        // Recompute any linkset parameters.
        // When going from non-physical to physical, this re-enables the constraints that
        //     had been automatically disabled when the mass was set to zero.
        Linkset.Refresh(this, true);

        DetailLog("{0},BSPrim.UpdatePhysicalParameters,exit,static={1},solid={2},mass={3},collide={4},cf={5:X},body={6},shape={7}",
                        LocalID, IsStatic, IsSolid, _mass, SubscribedEvents(), CurrentCollisionFlags, BSBody, BSShape);
    }

    // "Making dynamic" means changing to and from static.
    // When static, gravity does not effect the object and it is fixed in space.
    // When dynamic, the object can fall and be pushed by others.
    // This is independent of its 'solidness' which controls what passes through
    //    this object and what interacts with it.
    private void MakeDynamic(bool makeStatic)
    {
        if (makeStatic)
        {
            // Become a Bullet 'static' object type
            CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.ptr, CollisionFlags.CF_STATIC_OBJECT);
            // Stop all movement
            BulletSimAPI.ClearAllForces2(BSBody.ptr);
            // Center of mass is at the center of the object
            BulletSimAPI.SetCenterOfMassByPosRot2(Linkset.LinksetRoot.BSBody.ptr, _position, _orientation);
            // Mass is zero which disables a bunch of physics stuff in Bullet
            BulletSimAPI.SetMassProps2(BSBody.ptr, 0f, OMV.Vector3.Zero);
            // There is no inertia in a static object
            BulletSimAPI.UpdateInertiaTensor2(BSBody.ptr);
            // Set collision detection parameters
            if (PhysicsScene.Params.ccdMotionThreshold > 0f)
            {
                BulletSimAPI.SetCcdMotionThreshold2(BSBody.ptr, PhysicsScene.Params.ccdMotionThreshold);
                BulletSimAPI.SetCcdSweepSphereRadius2(BSBody.ptr, PhysicsScene.Params.ccdSweptSphereRadius);
            }
            // There can be special things needed for implementing linksets
            Linkset.MakeStatic(this);
            // The activation state is 'disabled' so Bullet will not try to act on it.
            BulletSimAPI.ForceActivationState2(BSBody.ptr, ActivationState.DISABLE_SIMULATION);
            // Start it out sleeping and physical actions could wake it up.
            // BulletSimAPI.ForceActivationState2(BSBody.ptr, ActivationState.ISLAND_SLEEPING);

            BSBody.collisionFilter = CollisionFilterGroups.StaticObjectFilter;
            BSBody.collisionMask = CollisionFilterGroups.StaticObjectMask;
        }
        else
        {
            // Not a Bullet static object
            CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.ptr, CollisionFlags.CF_STATIC_OBJECT);

            // Set various physical properties so internal dynamic properties will get computed correctly as they are set
            BulletSimAPI.SetFriction2(BSBody.ptr, PhysicsScene.Params.defaultFriction);
            BulletSimAPI.SetRestitution2(BSBody.ptr, PhysicsScene.Params.defaultRestitution);

            // per http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=3382
            BulletSimAPI.ClearAllForces2(BSBody.ptr);

            // For good measure, make sure the transform is set through to the motion state
            BulletSimAPI.SetTranslation2(BSBody.ptr, _position, _orientation);

            // A dynamic object has mass
            IntPtr collisionShapePtr = BulletSimAPI.GetCollisionShape2(BSBody.ptr);
            OMV.Vector3 inertia = BulletSimAPI.CalculateLocalInertia2(collisionShapePtr, Mass);
            BulletSimAPI.SetMassProps2(BSBody.ptr, _mass, inertia);
            BulletSimAPI.UpdateInertiaTensor2(BSBody.ptr);

            // Set collision detection parameters
            if (PhysicsScene.Params.ccdMotionThreshold > 0f)
            {
                BulletSimAPI.SetCcdMotionThreshold2(BSBody.ptr, PhysicsScene.Params.ccdMotionThreshold);
                BulletSimAPI.SetCcdSweepSphereRadius2(BSBody.ptr, PhysicsScene.Params.ccdSweptSphereRadius);
            }

            // Various values for simulation limits
            BulletSimAPI.SetDamping2(BSBody.ptr, PhysicsScene.Params.linearDamping, PhysicsScene.Params.angularDamping);
            BulletSimAPI.SetDeactivationTime2(BSBody.ptr, PhysicsScene.Params.deactivationTime);
            BulletSimAPI.SetSleepingThresholds2(BSBody.ptr, PhysicsScene.Params.linearSleepingThreshold, PhysicsScene.Params.angularSleepingThreshold);
            BulletSimAPI.SetContactProcessingThreshold2(BSBody.ptr, PhysicsScene.Params.contactProcessingThreshold);

            // There might be special things needed for implementing linksets.
            Linkset.MakeDynamic(this);

            // Force activation of the object so Bullet will act on it.
            // Must do the ForceActivationState2() to overcome the DISABLE_SIMULATION from static objects.
            BulletSimAPI.ForceActivationState2(BSBody.ptr, ActivationState.ACTIVE_TAG);
            // BulletSimAPI.Activate2(BSBody.ptr, true);

            BSBody.collisionFilter = CollisionFilterGroups.ObjectFilter;
            BSBody.collisionMask = CollisionFilterGroups.ObjectMask;
        }
    }

    // "Making solid" means that other object will not pass through this object.
    // To make transparent, we create a Bullet ghost object.
    // Note: This expects to be called from the UpdatePhysicalParameters() routine as
    //     the functions after this one set up the state of a possibly newly created collision body.
    private void MakeSolid(bool makeSolid)
    {
        CollisionObjectTypes bodyType = (CollisionObjectTypes)BulletSimAPI.GetBodyType2(BSBody.ptr);
        if (makeSolid)
        {
            // Verify the previous code created the correct shape for this type of thing.
            if ((bodyType & CollisionObjectTypes.CO_RIGID_BODY) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for solidity. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
        }
        else
        {
            if ((bodyType & CollisionObjectTypes.CO_GHOST_OBJECT) == 0)
            {
                m_log.ErrorFormat("{0} MakeSolid: physical body of wrong type for non-solidness. id={1}, type={2}", LogHeader, LocalID, bodyType);
            }
            CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.ptr, CollisionFlags.CF_NO_CONTACT_RESPONSE);
            BSBody.collisionFilter = CollisionFilterGroups.VolumeDetectFilter;
            BSBody.collisionMask = CollisionFilterGroups.VolumeDetectMask;
        }
    }

    // Turn on or off the flag controlling whether collision events are returned to the simulator.
    private void EnableCollisions(bool wantsCollisionEvents)
    {
        if (wantsCollisionEvents)
        {
            CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
        else
        {
            CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
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
    public override bool IsColliding {
        get { return (CollidingStep == PhysicsScene.SimulationStep); }
        set { _isColliding = value; }
    }
    public override bool CollidingGround {
        get { return (CollidingGroundStep == PhysicsScene.SimulationStep); }
        set { _collidingGround = value; }
    }
    public override bool CollidingObj {
        get { return _collidingObj; }
        set { _collidingObj = value; }
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
            PhysicsScene.TaintedObject("BSPrim.setFloatOnWater", delegate()
            {
                if (_floatOnWater)
                    CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
                else
                    CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
            });
        }
    }
    public override OMV.Vector3 RotationalVelocity {
        get {
            /*
            OMV.Vector3 pv = OMV.Vector3.Zero;
            // if close to zero, report zero
            // This is copied from ODE but I'm not sure why it returns zero but doesn't
            //    zero the property in the physics engine.
            if (_rotationalVelocity.ApproxEquals(pv, 0.2f))
                return pv;
             */

            return _rotationalVelocity;
        }
        set {
            _rotationalVelocity = value;
            // m_log.DebugFormat("{0}: RotationalVelocity={1}", LogHeader, _rotationalVelocity);
            PhysicsScene.TaintedObject("BSPrim.setRotationalVelocity", delegate()
            {
                // DetailLog("{0},BSPrim.SetRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
                BulletSimAPI.SetAngularVelocity2(BSBody.ptr, _rotationalVelocity);
            });
        }
    }
    public override OMV.Vector3 ForceRotationalVelocity {
        get {
            return _rotationalVelocity;
        }
        set {
            _rotationalVelocity = value;
            BulletSimAPI.SetAngularVelocity2(BSBody.ptr, _rotationalVelocity);
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
            PhysicsScene.TaintedObject("BSPrim.setBuoyancy", delegate()
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
            // Buoyancy is faked by changing the gravity applied to the object
            float grav = PhysicsScene.Params.gravity * (1f - _buoyancy);
            BulletSimAPI.SetGravity2(BSBody.ptr, new OMV.Vector3(0f, 0f, grav));
        }
    }

    // Used for MoveTo
    public override OMV.Vector3 PIDTarget {
        set { _PIDTarget = value; }
    }
    public override bool PIDActive {
        set { _usePID = value; }
    }
    public override float PIDTau {
        set { _PIDTau = value; }
    }

    // Used for llSetHoverHeight and maybe vehicle height
    // Hover Height will override MoveTo target's Z
    public override bool PIDHoverActive {
        set { _useHoverPID = value; }
    }
    public override float PIDHoverHeight {
        set { _PIDHoverHeight = value; }
    }
    public override PIDHoverType PIDHoverType {
        set { _PIDHoverType = value; }
    }
    public override float PIDHoverTau {
        set { _PIDHoverTao = value; }
    }

    // For RotLookAt
    public override OMV.Quaternion APIDTarget { set { return; } }
    public override bool APIDActive { set { return; } }
    public override float APIDStrength { set { return; } }
    public override float APIDDamping { set { return; } }

    private List<OMV.Vector3> m_accumulatedForces = new List<OMV.Vector3>();
    public override void AddForce(OMV.Vector3 force, bool pushforce) {
        AddForce(force, pushforce, false);
    }
    public void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
        // for an object, doesn't matter if force is a pushforce or not
        if (force.IsFinite())
        {
            // _force += force;
            lock (m_accumulatedForces)
                m_accumulatedForces.Add(new OMV.Vector3(force));
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
            return;
        }
        BSScene.TaintCallback addForceOperation = delegate()
        {
            OMV.Vector3 fSum = OMV.Vector3.Zero;
            lock (m_accumulatedForces)
            {
                // Sum the accumulated additional forces for one big force to apply once.
                foreach (OMV.Vector3 v in m_accumulatedForces)
                {
                    fSum += v;
                }
                m_accumulatedForces.Clear();
            }
            // DetailLog("{0},BSPrim.AddObjectForce,taint,force={1}", LocalID, fSum);
            // For unknown reasons, "ApplyCentralForce" adds this force to the total force on the object.
            BulletSimAPI.ApplyCentralForce2(BSBody.ptr, fSum);
        };
        if (inTaintTime)
            addForceOperation();
        else
            PhysicsScene.TaintedObject("BSPrim.AddForce", addForceOperation);
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) {
        // DetailLog("{0},BSPrim.AddAngularForce,call,angForce={1},push={2}", LocalID, force, pushforce);
        // m_log.DebugFormat("{0}: AddAngularForce. f={1}, push={2}", LogHeader, force, pushforce);
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

        returnMass = _density * volume;

        /*
         * This change means each object keeps its own mass and the Mass property
         * will return the sum if we're part of a linkset.
        if (IsRootOfLinkset)
        {
            foreach (BSPrim prim in _childrenPrims)
            {
                returnMass += prim.CalculateMass();
            }
        }
         */

        if (returnMass <= 0)
            returnMass = 0.0001f;

        if (returnMass > PhysicsScene.MaximumObjectMass)
            returnMass = PhysicsScene.MaximumObjectMass;

        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

    // Copy prim's info into the BulletSim shape description structure
    public void FillShapeInfo(out ShapeData shape)
    {
        shape.ID = LocalID;
        shape.Type = ShapeData.PhysicsShapeType.SHAPE_UNKNOWN;
        shape.Position = _position;
        shape.Rotation = _orientation;
        shape.Velocity = _velocity;
        shape.Size = _size;
        shape.Scale = Scale;
        shape.Mass = _isPhysical ? _mass : 0f;
        shape.Buoyancy = _buoyancy;
        shape.HullKey = 0;
        shape.MeshKey = 0;
        shape.Friction = _friction;
        shape.Restitution = _restitution;
        shape.Collidable = (!IsPhantom) ? ShapeData.numericTrue : ShapeData.numericFalse;
        shape.Static = _isPhysical ? ShapeData.numericFalse : ShapeData.numericTrue;
        shape.Solid = IsSolid ? ShapeData.numericFalse : ShapeData.numericTrue;
    }
    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // Called at taint-time!!!
    private void CreateGeomAndObject(bool forceRebuild)
    {
        ShapeData shapeData;
        FillShapeInfo(out shapeData);

        // If this prim is part of a linkset, we must remove and restore the physical
        //    links if the body is rebuilt.
        bool needToRestoreLinkset = false;

        // Create the correct physical representation for this type of object.
        // Updates BSBody and BSShape with the new information.
        // Ignore 'forceRebuild'. This routine makes the right choices and changes of necessary.
        // Returns 'true' if either the body or the shape was changed.
        PhysicsScene.Shapes.GetBodyAndShape(false, PhysicsScene.World, this, shapeData, BaseShape,
                        null, delegate(BulletBody dBody)
        {
            // Called if the current prim body is about to be destroyed.
            // Remove all the physical dependencies on the old body.
            // (Maybe someday make the changing of BSShape an event handled by BSLinkset.)
            needToRestoreLinkset = Linkset.RemoveBodyDependencies(this);
        });

        if (needToRestoreLinkset)
        {
            // If physical body dependencies were removed, restore them
            Linkset.RestoreBodyDependencies(this);
        }

        // Make sure the properties are set on the new object
        UpdatePhysicalParameters();
        return;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    // TODO: do we really need to check for changed? Maybe just copy values and call RequestPhysicsterseUpdate()
    enum UpdatedProperties {
        Position      = 1 << 0,
        Rotation      = 1 << 1,
        Velocity      = 1 << 2,
        Acceleration  = 1 << 3,
        RotationalVel = 1 << 4
    }

    const float ROTATION_TOLERANCE = 0.01f;
    const float VELOCITY_TOLERANCE = 0.001f;
    const float POSITION_TOLERANCE = 0.05f;
    const float ACCELERATION_TOLERANCE = 0.01f;
    const float ROTATIONAL_VELOCITY_TOLERANCE = 0.01f;

    public override void UpdateProperties(EntityProperties entprop)
    {
        /*
        UpdatedProperties changed = 0;
        // assign to the local variables so the normal set action does not happen
        // if (_position != entprop.Position)
        if (!_position.ApproxEquals(entprop.Position, POSITION_TOLERANCE))
        {
            _position = entprop.Position;
            changed |= UpdatedProperties.Position;
        }
        // if (_orientation != entprop.Rotation)
        if (!_orientation.ApproxEquals(entprop.Rotation, ROTATION_TOLERANCE))
        {
            _orientation = entprop.Rotation;
            changed |= UpdatedProperties.Rotation;
        }
        // if (_velocity != entprop.Velocity)
        if (!_velocity.ApproxEquals(entprop.Velocity, VELOCITY_TOLERANCE))
        {
            _velocity = entprop.Velocity;
            changed |= UpdatedProperties.Velocity;
        }
        // if (_acceleration != entprop.Acceleration)
        if (!_acceleration.ApproxEquals(entprop.Acceleration, ACCELERATION_TOLERANCE))
        {
            _acceleration = entprop.Acceleration;
            changed |= UpdatedProperties.Acceleration;
        }
        // if (_rotationalVelocity != entprop.RotationalVelocity)
        if (!_rotationalVelocity.ApproxEquals(entprop.RotationalVelocity, ROTATIONAL_VELOCITY_TOLERANCE))
        {
            _rotationalVelocity = entprop.RotationalVelocity;
            changed |= UpdatedProperties.RotationalVel;
        }
        if (changed != 0)
        {
            // Only update the position of single objects and linkset roots
            if (this._parentPrim == null)
            {
                base.RequestPhysicsterseUpdate();
            }
        }
        */

        // Don't check for damping here -- it's done in BulletSim and SceneObjectPart.

        // Updates only for individual prims and for the root object of a linkset.
        if (Linkset.IsRoot(this))
        {
            // Assign to the local variables so the normal set action does not happen
            _position = entprop.Position;
            _orientation = entprop.Rotation;
            _velocity = entprop.Velocity;
            _acceleration = entprop.Acceleration;
            _rotationalVelocity = entprop.RotationalVelocity;

            // remember the current and last set values
            LastEntityProperties = CurrentEntityProperties;
            CurrentEntityProperties = entprop;

            PositionSanityCheck(true);

            DetailLog("{0},BSPrim.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);

            // BulletSimAPI.DumpRigidBody2(PhysicsScene.World.ptr, BSBody.ptr);   // DEBUG DEBUG DEBUG

            base.RequestPhysicsterseUpdate();
        }
            /*
        else
        {
            // For debugging, report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity,
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */

        // The linkset implimentation might want to know about this.
        Linkset.UpdateProperties(this);
    }
}
}
