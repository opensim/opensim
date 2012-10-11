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
public class BSCharacter : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS CHAR]";

    // private bool _stopped;
    private OMV.Vector3 _size;
    private OMV.Vector3 _scale;
    private PrimitiveBaseShape _pbs;
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

    private OMV.Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSCharacter(uint localID, String avName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 size, bool isFlying)
    {
        base.BaseInitialize(parent_scene, localID, avName, "BSCharacter");
        _physicsActorType = (int)ActorTypes.Agent;
        _position = pos;
        _size = size;
        _flying = isFlying;
        _orientation = OMV.Quaternion.Identity;
        _velocity = OMV.Vector3.Zero;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);

        // The dimensions of the avatar capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        ComputeAvatarScale(_size);
        _avatarDensity = PhysicsScene.Params.avatarDensity;
        // set _avatarVolume and _mass based on capsule size, _density and _scale
        ComputeAvatarVolumeAndMass();

        ShapeData shapeData = new ShapeData();
        shapeData.ID = LocalID;
        shapeData.Type = ShapeData.PhysicsShapeType.SHAPE_AVATAR;
        shapeData.Position = _position;
        shapeData.Rotation = _orientation;
        shapeData.Velocity = _velocity;
        shapeData.Scale = _scale;
        shapeData.Mass = _mass;
        shapeData.Buoyancy = _buoyancy;
        shapeData.Static = ShapeData.numericFalse;
        shapeData.Friction = PhysicsScene.Params.avatarFriction;
        shapeData.Restitution = PhysicsScene.Params.avatarRestitution;

        // do actual create at taint time
        PhysicsScene.TaintedObject("BSCharacter.create", delegate()
        {
            DetailLog("{0},BSCharacter.create,taint", LocalID);
            BulletSimAPI.CreateObject(PhysicsScene.WorldID, shapeData);

            // Set the buoyancy for flying. This will be refactored when all the settings happen in C#.
            // If not set at creation, the avatar will stop flying when created after crossing a region boundry.
            BulletSimAPI.SetObjectBuoyancy(PhysicsScene.WorldID, LocalID, _buoyancy);

            BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(PhysicsScene.World.ptr, LocalID));

            // This works here because CreateObject has already put the character into the physical world.
            BulletSimAPI.SetCollisionFilterMask2(BSBody.ptr,
                            (uint)CollisionFilterGroups.AvatarFilter, (uint)CollisionFilterGroups.AvatarMask);
        });
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public override void Destroy()
    {
        DetailLog("{0},BSCharacter.Destroy", LocalID);
        PhysicsScene.TaintedObject("BSCharacter.destroy", delegate()
        {
            BulletSimAPI.DestroyObject(PhysicsScene.WorldID, LocalID);
        });
    }

    public override void RequestPhysicsterseUpdate()
    {
        base.RequestPhysicsterseUpdate();
    }
    // No one calls this method so I don't know what it could possibly mean
    public override bool Stopped {
        get { return false; }
    }
    public override OMV.Vector3 Size {
        get
        {
            // Avatar capsule size is kept in the scale parameter.
            return new OMV.Vector3(_scale.X * 2, _scale.Y * 2, _scale.Z);
        }

        set {
            // When an avatar's size is set, only the height is changed
            //    and that really only depends on the radius.
            _size = value;
            ComputeAvatarScale(_size);

            // TODO: something has to be done with the avatar's vertical position

            ComputeAvatarVolumeAndMass();

            PhysicsScene.TaintedObject("BSCharacter.setSize", delegate()
            {
                BulletSimAPI.SetObjectScaleMass(PhysicsScene.WorldID, LocalID, _scale, _mass, true);
            });

        }
    }
    public override PrimitiveBaseShape Shape {
        set { _pbs = value;
        }
    }
    public override bool Grabbed {
        set { _grabbed = value;
        }
    }
    public override bool Selected {
        set { _selected = value;
        }
    }
    public override void CrossingFailure() { return; }
    public override void link(PhysicsActor obj) { return; }
    public override void delink() { return; }

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
        BulletSimAPI.SetLinearVelocity2(BSBody.ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetAngularVelocity2(BSBody.ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetInterpolationVelocity2(BSBody.ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);
        BulletSimAPI.ClearForces2(BSBody.ptr);
    }

    public override void LockAngularMotion(OMV.Vector3 axis) { return; }

    public override OMV.Vector3 Position {
        get {
            // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, _localID);
            return _position;
        }
        set {
            _position = value;
            PositionSanityCheck();

            PhysicsScene.TaintedObject("BSCharacter.setPosition", delegate()
            {
                DetailLog("{0},BSCharacter.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetObjectTranslation(PhysicsScene.WorldID, LocalID, _position, _orientation);
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
    //    pushed back to the physics engine. This routine would be used by anyone
    //    who is not already pushing the value.
    private bool PositionSanityCheck2(bool inTaintTime)
    {
        bool ret = false;
        if (PositionSanityCheck())
        {
            // The new position value must be pushed into the physics engine but we can't
            //    just assign to "Position" because of potential call loops.
            BSScene.TaintCallback sanityOperation = delegate()
            {
                DetailLog("{0},BSCharacter.PositionSanityCheck,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetObjectTranslation(PhysicsScene.WorldID, LocalID, _position, _orientation);
            };
            if (inTaintTime)
                sanityOperation();
            else
                PhysicsScene.TaintedObject("BSCharacter.PositionSanityCheck", sanityOperation);
            ret = true;
        }
        return ret;
    }

    public override float Mass {
        get {
            return _mass;
        }
    }

    // used when we only want this prim's mass and not the linkset thing
    public override float MassRaw { get {return _mass; } }

    public override OMV.Vector3 Force {
        get { return _force; }
        set {
            _force = value;
            // m_log.DebugFormat("{0}: Force = {1}", LogHeader, _force);
            PhysicsScene.TaintedObject("BSCharacter.SetForce", delegate()
            {
                DetailLog("{0},BSCharacter.setForce,taint,force={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce(PhysicsScene.WorldID, LocalID, _force);
            });
        }
    }

    public override int VehicleType {
        get { return 0; }
        set { return; }
    }
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
                BulletSimAPI.SetObjectVelocity(PhysicsScene.WorldID, LocalID, _velocity);
            });
        }
    }
    public override OMV.Vector3 ForceVelocity {
        get { return _velocity; }
        set {
            _velocity = value;
            BulletSimAPI.SetObjectVelocity(PhysicsScene.WorldID, LocalID, _velocity);
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
    public override OMV.Quaternion Orientation {
        get { return _orientation; }
        set {
            _orientation = value;
            // m_log.DebugFormat("{0}: set orientation to {1}", LogHeader, _orientation);
            PhysicsScene.TaintedObject("BSCharacter.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, _localID);
                BulletSimAPI.SetObjectTranslation(PhysicsScene.WorldID, LocalID, _position, _orientation);
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
        set { _physicsActorType = value;
        }
    }
    public override bool IsPhysical {
        get { return _isPhysical; }
        set { _isPhysical = value;
        }
    }
    public override bool Flying {
        get { return _flying; }
        set {
            _flying = value;
            // simulate flying by changing the effect of gravity
            this.Buoyancy = ComputeBuoyancyFromFlying(_flying);
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
                if (_floatOnWater)
                    CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
                else
                    CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.ptr, CollisionFlags.BS_FLOATS_ON_WATER);
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
                BulletSimAPI.SetObjectBuoyancy(PhysicsScene.WorldID, LocalID, _buoyancy);
            });
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
                BulletSimAPI.SetObjectForce2(BSBody.ptr, _force);
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
        _scale.X = PhysicsScene.Params.avatarCapsuleRadius;
        _scale.Y = PhysicsScene.Params.avatarCapsuleRadius;

        // The 1.15 came from ODE but it seems to cause the avatar to float off the ground
        // _scale.Z = (_size.Z * 1.15f) - (_scale.X + _scale.Y);
        _scale.Z = (_size.Z) - (_scale.X + _scale.Y);
    }

    // set _avatarVolume and _mass based on capsule size, _density and _scale
    private void ComputeAvatarVolumeAndMass()
    {
        _avatarVolume = (float)(
                        Math.PI
                        * _scale.X
                        * _scale.Y  // the area of capsule cylinder
                        * _scale.Z  // times height of capsule cylinder
                      + 1.33333333f
                        * Math.PI
                        * _scale.X
                        * Math.Min(_scale.X, _scale.Y)
                        * _scale.Y  // plus the volume of the capsule end caps
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
        PositionSanityCheck2(true);

        // Avatars don't report their changes the usual way. Changes are checked for in the heartbeat loop.
        // base.RequestPhysicsterseUpdate();

        DetailLog("{0},BSCharacter.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);
    }
}
}
