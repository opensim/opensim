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
using System.Reflection;
using log4net;
using OMV = OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public sealed class BSCharacter : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS CHAR]";

    // private bool _stopped;
    private OMV.Vector3 _size;
    private bool _grabbed;
    private bool _selected;
    private OMV.Vector3 _position;
    private float _mass;
    private float _avatarDensity;
    private float _avatarVolume;
    private OMV.Vector3 _force;
    private OMV.Vector3 _velocity;
    private OMV.Vector3 _torque;
    private float _collisionScore;
    private OMV.Vector3 _acceleration;
    private OMV.Quaternion _orientation;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _isColliding;
    private bool _collidingObj;
    private bool _floatOnWater;
    private OMV.Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    // The friction and velocity of the avatar is modified depending on whether walking or not.
    private OMV.Vector3 _appliedVelocity;   // the last velocity applied to the avatar
    private float _currentFriction;         // the friction currently being used (changed by setVelocity).

    private OMV.Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSCharacter(uint localID, String avName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size, bool isFlying)
            : base(parent_scene, localID, avName, "BSCharacter")
    {
        _physicsActorType = (int)ActorTypes.Agent;
        _position = pos;

        // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
        //     replace with the default values.
        _size = size;
        if (_size.X == 0f) _size.X = PhysicsScene.Params.avatarCapsuleDepth;
        if (_size.Y == 0f) _size.Y = PhysicsScene.Params.avatarCapsuleWidth;

        _flying = isFlying;
        _orientation = OMV.Quaternion.Identity;
        _velocity = OMV.Vector3.Zero;
        _appliedVelocity = OMV.Vector3.Zero;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);
        _currentFriction = PhysicsScene.Params.avatarStandingFriction;
        _avatarDensity = PhysicsScene.Params.avatarDensity;

        // The dimensions of the avatar capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        ComputeAvatarScale(_size);
        // set _avatarVolume and _mass based on capsule size, _density and Scale
        ComputeAvatarVolumeAndMass();
        DetailLog("{0},BSCharacter.create,call,size={1},scale={2},density={3},volume={4},mass={5}",
                            LocalID, _size, Scale, _avatarDensity, _avatarVolume, RawMass);

        // do actual creation in taint time
        PhysicsScene.TaintedObject("BSCharacter.create", delegate()
        {
            DetailLog("{0},BSCharacter.create,taint", LocalID);
            // New body and shape into PhysBody and PhysShape
            PhysicsScene.Shapes.GetBodyAndShape(true, PhysicsScene.World, this);

            SetPhysicalProperties();
        });
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public override void Destroy()
    {
        DetailLog("{0},BSCharacter.Destroy", LocalID);
        PhysicsScene.TaintedObject("BSCharacter.destroy", delegate()
        {
            PhysicsScene.Shapes.DereferenceBody(PhysBody, true, null);
            PhysBody.Clear();
            PhysicsScene.Shapes.DereferenceShape(PhysShape, true, null);
            PhysShape.Clear();
        });
    }

    private void SetPhysicalProperties()
    {
        BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.ptr, PhysBody.ptr);

        ZeroMotion(true);
        ForcePosition = _position;
        // Set the velocity and compute the proper friction
        ForceVelocity = _velocity;

        // This will enable or disable the flying buoyancy of the avatar.
        // Needs to be reset especially when an avatar is recreated after crossing a region boundry.
        Flying = _flying;

        BulletSimAPI.SetRestitution2(PhysBody.ptr, PhysicsScene.Params.avatarRestitution);
        BulletSimAPI.SetMargin2(PhysShape.ptr, PhysicsScene.Params.collisionMargin);
        BulletSimAPI.SetLocalScaling2(PhysShape.ptr, Scale);
        BulletSimAPI.SetContactProcessingThreshold2(PhysBody.ptr, PhysicsScene.Params.contactProcessingThreshold);
        if (PhysicsScene.Params.ccdMotionThreshold > 0f)
        {
            BulletSimAPI.SetCcdMotionThreshold2(PhysBody.ptr, PhysicsScene.Params.ccdMotionThreshold);
            BulletSimAPI.SetCcdSweptSphereRadius2(PhysBody.ptr, PhysicsScene.Params.ccdSweptSphereRadius);
        }

        UpdatePhysicalMassProperties(RawMass);

        // Make so capsule does not fall over
        BulletSimAPI.SetAngularFactorV2(PhysBody.ptr, OMV.Vector3.Zero);

        BulletSimAPI.AddToCollisionFlags2(PhysBody.ptr, CollisionFlags.CF_CHARACTER_OBJECT);

        BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.ptr, PhysBody.ptr);

        // BulletSimAPI.ForceActivationState2(BSBody.ptr, ActivationState.ACTIVE_TAG);
        BulletSimAPI.ForceActivationState2(PhysBody.ptr, ActivationState.DISABLE_DEACTIVATION);
        BulletSimAPI.UpdateSingleAabb2(PhysicsScene.World.ptr, PhysBody.ptr);

        // Do this after the object has been added to the world
        PhysBody.collisionType = CollisionType.Avatar;
        PhysBody.ApplyCollisionMask();
    }

    public override void RequestPhysicsterseUpdate()
    {
        base.RequestPhysicsterseUpdate();
    }
    // No one calls this method so I don't know what it could possibly mean
    public override bool Stopped { get { return false; } }

    public override OMV.Vector3 Size {
        get
        {
            // Avatar capsule size is kept in the scale parameter.
            return _size;
        }

        set {
            // When an avatar's size is set, only the height is changed.
            _size = value;
            // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
            //     replace with the default values.
            if (_size.X == 0f) _size.X = PhysicsScene.Params.avatarCapsuleDepth;
            if (_size.Y == 0f) _size.Y = PhysicsScene.Params.avatarCapsuleWidth;

            ComputeAvatarScale(_size);
            ComputeAvatarVolumeAndMass();
            DetailLog("{0},BSCharacter.setSize,call,size={1},scale={2},density={3},volume={4},mass={5}",
                            LocalID, _size, Scale, _avatarDensity, _avatarVolume, RawMass);

            PhysicsScene.TaintedObject("BSCharacter.setSize", delegate()
            {
                if (PhysBody.HasPhysicalBody && PhysShape.HasPhysicalShape)
                {
                    BulletSimAPI.SetLocalScaling2(PhysShape.ptr, Scale);
                    UpdatePhysicalMassProperties(RawMass);
                    // Make sure this change appears as a property update event
                    BulletSimAPI.PushUpdate2(PhysBody.ptr);
                }
            });

        }
    }

    public override PrimitiveBaseShape Shape
    {
        set { BaseShape = value; }
    }
    // I want the physics engine to make an avatar capsule
    public override BSPhysicsShapeType PreferredPhysicalShape
    {
        get {return BSPhysicsShapeType.SHAPE_CAPSULE; }
    }

    public override bool Grabbed {
        set { _grabbed = value; }
    }
    public override bool Selected {
        set { _selected = value; }
    }
    public override void CrossingFailure() { return; }
    public override void link(PhysicsActor obj) { return; }
    public override void delink() { return; }

    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public override void ZeroMotion(bool inTaintTime)
    {
        _velocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties directly into the physics engine
        PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
                BulletSimAPI.ClearAllForces2(PhysBody.ptr);
        });
    }
    public override void ZeroAngularMotion(bool inTaintTime)
    {
        _rotationalVelocity = OMV.Vector3.Zero;

        PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
            {
                BulletSimAPI.SetInterpolationAngularVelocity2(PhysBody.ptr, OMV.Vector3.Zero);
                BulletSimAPI.SetAngularVelocity2(PhysBody.ptr, OMV.Vector3.Zero);
                // The next also get rid of applied linear force but the linear velocity is untouched.
                BulletSimAPI.ClearForces2(PhysBody.ptr);
            }
        });
    }


    public override void LockAngularMotion(OMV.Vector3 axis) { return; }

    public override OMV.Vector3 RawPosition
    {
        get { return _position; }
        set { _position = value; }
    }
    public override OMV.Vector3 Position {
        get {
            // Don't refetch the position because this function is called a zillion times
            // _position = BulletSimAPI.GetObjectPosition2(Scene.World.ptr, LocalID);
            return _position;
        }
        set {
            _position = value;
            PositionSanityCheck();

            PhysicsScene.TaintedObject("BSCharacter.setPosition", delegate()
            {
                DetailLog("{0},BSCharacter.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                if (PhysBody.HasPhysicalBody)
                    BulletSimAPI.SetTranslation2(PhysBody.ptr, _position, _orientation);
            });
        }
    }
    public override OMV.Vector3 ForcePosition {
        get {
            _position = BulletSimAPI.GetPosition2(PhysBody.ptr);
            return _position;
        }
        set {
            _position = value;
            PositionSanityCheck();
            BulletSimAPI.SetTranslation2(PhysBody.ptr, _position, _orientation);
        }
    }


    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain or on water.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck()
    {
        bool ret = false;

        // TODO: check for out of bounds
        if (!PhysicsScene.TerrainManager.IsWithinKnownTerrain(_position))
        {
            // The character is out of the known/simulated area.
            // Upper levels of code will handle the transition to other areas so, for
            //     the time, we just ignore the position.
            return ret;
        }

        // If below the ground, move the avatar up
        float terrainHeight = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(_position);
        if (Position.Z < terrainHeight)
        {
            DetailLog("{0},BSCharacter.PositionAdjustUnderGround,call,pos={1},terrain={2}", LocalID, _position, terrainHeight);
            _position.Z = terrainHeight + 2.0f;
            ret = true;
        }
        if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
        {
            float waterHeight = PhysicsScene.TerrainManager.GetWaterLevelAtXYZ(_position);
            if (Position.Z < waterHeight)
            {
                _position.Z = waterHeight;
                ret = true;
            }
        }

        return ret;
    }

    // A version of the sanity check that also makes sure a new position value is
    //    pushed back to the physics engine. This routine would be used by anyone
    //    who is not already pushing the value.
    private bool PositionSanityCheck(bool inTaintTime)
    {
        bool ret = false;
        if (PositionSanityCheck())
        {
            // The new position value must be pushed into the physics engine but we can't
            //    just assign to "Position" because of potential call loops.
            PhysicsScene.TaintedObject(inTaintTime, "BSCharacter.PositionSanityCheck", delegate()
            {
                DetailLog("{0},BSCharacter.PositionSanityCheck,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                if (PhysBody.HasPhysicalBody)
                    BulletSimAPI.SetTranslation2(PhysBody.ptr, _position, _orientation);
            });
            ret = true;
        }
        return ret;
    }

    public override float Mass { get { return _mass; } }

    // used when we only want this prim's mass and not the linkset thing
    public override float RawMass { 
        get {return _mass; }
    }
    public override void UpdatePhysicalMassProperties(float physMass)
    {
        OMV.Vector3 localInertia = BulletSimAPI.CalculateLocalInertia2(PhysShape.ptr, physMass);
        BulletSimAPI.SetMassProps2(PhysBody.ptr, physMass, localInertia);
    }

    public override OMV.Vector3 Force {
        get { return _force; }
        set {
            _force = value;
            // m_log.DebugFormat("{0}: Force = {1}", LogHeader, _force);
            PhysicsScene.TaintedObject("BSCharacter.SetForce", delegate()
            {
                DetailLog("{0},BSCharacter.setForce,taint,force={1}", LocalID, _force);
                if (PhysBody.HasPhysicalBody)
                    BulletSimAPI.SetObjectForce2(PhysBody.ptr, _force);
            });
        }
    }

    // Avatars don't do vehicles
    public override int VehicleType { get { return (int)Vehicle.TYPE_NONE; } set { return; } }
    public override void VehicleFloatParam(int param, float value) { }
    public override void VehicleVectorParam(int param, OMV.Vector3 value) {}
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation) { }
    public override void VehicleFlags(int param, bool remove) { }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) { return; }

    public override OMV.Vector3 GeometricCenter { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 CenterOfMass { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 Velocity {
        get { return _velocity; }
        set {
            _velocity = value;
            // m_log.DebugFormat("{0}: set velocity = {1}", LogHeader, _velocity);
            PhysicsScene.TaintedObject("BSCharacter.setVelocity", delegate()
            {
                DetailLog("{0},BSCharacter.setVelocity,taint,vel={1}", LocalID, _velocity);
                ForceVelocity = _velocity;
            });
        }
    }
    public override OMV.Vector3 ForceVelocity {
        get { return _velocity; }
        set {
            // Depending on whether the avatar is moving or not, change the friction
            //    to keep the avatar from slipping around
            if (_velocity.Length() == 0)
            {
                if (_currentFriction != PhysicsScene.Params.avatarStandingFriction)
                {
                    _currentFriction = PhysicsScene.Params.avatarStandingFriction;
                    if (PhysBody.HasPhysicalBody)
                        BulletSimAPI.SetFriction2(PhysBody.ptr, _currentFriction);
                }
            }
            else
            {
                if (_currentFriction != PhysicsScene.Params.avatarFriction)
                {
                    _currentFriction = PhysicsScene.Params.avatarFriction;
                    if (PhysBody.HasPhysicalBody)
                        BulletSimAPI.SetFriction2(PhysBody.ptr, _currentFriction);
                }
            }
            _velocity = value;
            // Remember the set velocity so we can suppress the reduction by friction, ...
            _appliedVelocity = value;

            BulletSimAPI.SetLinearVelocity2(PhysBody.ptr, _velocity);
            BulletSimAPI.Activate2(PhysBody.ptr, true);
        }
    }
    public override OMV.Vector3 Torque {
        get { return _torque; }
        set { _torque = value;
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
    public override OMV.Quaternion RawOrientation
    {
        get { return _orientation; }
        set { _orientation = value; }
    }
    public override OMV.Quaternion Orientation {
        get { return _orientation; }
        set {
            _orientation = value;
            // m_log.DebugFormat("{0}: set orientation to {1}", LogHeader, _orientation);
            PhysicsScene.TaintedObject("BSCharacter.setOrientation", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    // _position = BulletSimAPI.GetPosition2(BSBody.ptr);
                    BulletSimAPI.SetTranslation2(PhysBody.ptr, _position, _orientation);
                }
            });
        }
    }
    // Go directly to Bullet to get/set the value.
    public override OMV.Quaternion ForceOrientation
    {
        get
        {
            _orientation = BulletSimAPI.GetOrientation2(PhysBody.ptr);
            return _orientation;
        }
        set
        {
            _orientation = value;
            BulletSimAPI.SetTranslation2(PhysBody.ptr, _position, _orientation);
        }
    }
    public override int PhysicsActorType {
        get { return _physicsActorType; }
        set { _physicsActorType = value;
        }
    }
    public override bool IsPhysical {
        get { return _isPhysical; }
        set { _isPhysical = value;
        }
    }
    public override bool IsSolid {
        get { return true; }
    }
    public override bool IsStatic {
        get { return false; }
    }
    public override bool Flying {
        get { return _flying; }
        set {
            _flying = value;
            // simulate flying by changing the effect of gravity
            Buoyancy = ComputeBuoyancyFromFlying(_flying);
        }
    }
    // Flying is implimented by changing the avatar's buoyancy.
    // Would this be done better with a vehicle type?
    private float ComputeBuoyancyFromFlying(bool ifFlying) {
        return ifFlying ? 1f : 0f;
    }
    public override bool
        SetAlwaysRun {
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
        set { CollidingGround = value; }
    }
    public override bool CollidingObj {
        get { return _collidingObj; }
        set { _collidingObj = value; }
    }
    public override bool FloatOnWater {
        set {
            _floatOnWater = value;
            PhysicsScene.TaintedObject("BSCharacter.setFloatOnWater", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    if (_floatOnWater)
                        CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(PhysBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
                    else
                        CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(PhysBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
                }
            });
        }
    }
    public override OMV.Vector3 RotationalVelocity {
        get { return _rotationalVelocity; }
        set { _rotationalVelocity = value; }
    }
    public override OMV.Vector3 ForceRotationalVelocity {
        get { return _rotationalVelocity; }
        set { _rotationalVelocity = value; }
    }
    public override bool Kinematic {
        get { return _kinematic; }
        set { _kinematic = value; }
    }
    // neg=fall quickly, 0=1g, 1=0g, pos=float up
    public override float Buoyancy {
        get { return _buoyancy; }
        set { _buoyancy = value;
            PhysicsScene.TaintedObject("BSCharacter.setBuoyancy", delegate()
            {
                DetailLog("{0},BSCharacter.setBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                ForceBuoyancy = _buoyancy;
            });
        }
    }
    public override float ForceBuoyancy {
        get { return _buoyancy; }
        set { _buoyancy = value;
            DetailLog("{0},BSCharacter.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
            // Buoyancy is faked by changing the gravity applied to the object
            float grav = PhysicsScene.Params.gravity * (1f - _buoyancy);
            if (PhysBody.HasPhysicalBody)
                BulletSimAPI.SetGravity2(PhysBody.ptr, new OMV.Vector3(0f, 0f, grav));
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

    public override void AddForce(OMV.Vector3 force, bool pushforce) {
        if (force.IsFinite())
        {
            _force.X += force.X;
            _force.Y += force.Y;
            _force.Z += force.Z;
            // m_log.DebugFormat("{0}: AddForce. adding={1}, newForce={2}", LogHeader, force, _force);
            PhysicsScene.TaintedObject("BSCharacter.AddForce", delegate()
            {
                DetailLog("{0},BSCharacter.setAddForce,taint,addedForce={1}", LocalID, _force);
                if (PhysBody.HasPhysicalBody)
                    BulletSimAPI.SetObjectForce2(PhysBody.ptr, _force);
            });
        }
        else
        {
            m_log.ErrorFormat("{0}: Got a NaN force applied to a Character", LogHeader);
        }
        //m_lastUpdateSent = false;
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) {
    }
    public override void SetMomentum(OMV.Vector3 momentum) {
    }

    private void ComputeAvatarScale(OMV.Vector3 size)
    {
        OMV.Vector3 newScale = size;
        // newScale.X = PhysicsScene.Params.avatarCapsuleWidth;
        // newScale.Y = PhysicsScene.Params.avatarCapsuleDepth;

        // From the total height, remove the capsule half spheres that are at each end
        // The 1.15f came from ODE. Not sure what this factors in.
        // newScale.Z = (size.Z * 1.15f) - (newScale.X + newScale.Y);

        // The total scale height is the central cylindar plus the caps on the two ends.
        newScale.Z = size.Z + (Math.Min(size.X, size.Y) * 2f);

        // Convert diameters to radii and height to half height -- the way Bullet expects it.
        Scale = newScale / 2f;
    }

    // set _avatarVolume and _mass based on capsule size, _density and Scale
    private void ComputeAvatarVolumeAndMass()
    {
        _avatarVolume = (float)(
                        Math.PI
                        * Scale.X
                        * Scale.Y    // the area of capsule cylinder
                        * Scale.Z    // times height of capsule cylinder
                      + 1.33333333f
                        * Math.PI
                        * Scale.X
                        * Math.Min(Scale.X, Scale.Y)
                        * Scale.Y    // plus the volume of the capsule end caps
                        );
        _mass = _avatarDensity * _avatarVolume;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public override void UpdateProperties(EntityProperties entprop)
    {
        _position = entprop.Position;
        _orientation = entprop.Rotation;
        _velocity = entprop.Velocity;
        _acceleration = entprop.Acceleration;
        _rotationalVelocity = entprop.RotationalVelocity;
        // Do some sanity checking for the avatar. Make sure it's above ground and inbounds.
        PositionSanityCheck(true);

        // remember the current and last set values
        LastEntityProperties = CurrentEntityProperties;
        CurrentEntityProperties = entprop;

        if (entprop.Velocity != LastEntityProperties.Velocity)
        {
            // Changes in the velocity are suppressed in avatars.
            // That's just the way they are defined.
            OMV.Vector3 avVel = new OMV.Vector3(_appliedVelocity.X, _appliedVelocity.Y, entprop.Velocity.Z);
            _velocity = avVel;
            BulletSimAPI.SetLinearVelocity2(PhysBody.ptr, avVel);
        }

        // Tell the linkset about value changes
        Linkset.UpdateProperties(this, true);

        // Avatars don't report their changes the usual way. Changes are checked for in the heartbeat loop.
        // base.RequestPhysicsterseUpdate();

        DetailLog("{0},BSCharacter.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);
    }
}
}
