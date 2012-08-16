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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
public class BSCharacter : PhysicsActor
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS CHAR]";

    private BSScene _scene;
    public BSScene Scene { get { return _scene; } }
    private String _avName;
    // private bool _stopped;
    private Vector3 _size;
    private Vector3 _scale;
    private PrimitiveBaseShape _pbs;
    private uint _localID = 0;
    private bool _grabbed;
    private bool _selected;
    private Vector3 _position;
    private float _mass;
    public float _density;
    public float _avatarVolume;
    private Vector3 _force;
    private Vector3 _velocity;
    private Vector3 _torque;
    private float _collisionScore;
    private Vector3 _acceleration;
    private Quaternion _orientation;
    private int _physicsActorType;
    private bool _isPhysical;
    private bool _flying;
    private bool _setAlwaysRun;
    private bool _throttleUpdates;
    private bool _isColliding;
    private long _collidingStep;
    private bool _collidingGround;
    private long _collidingGroundStep;
    private bool _collidingObj;
    private bool _floatOnWater;
    private Vector3 _rotationalVelocity;
    private bool _kinematic;
    private float _buoyancy;

    private BulletBody m_body;
    public BulletBody Body { 
        get { return m_body; }
        set { m_body = value; }
    }

    private int _subscribedEventsMs = 0;
    private int _nextCollisionOkTime = 0;

    private Vector3 _PIDTarget;
    private bool _usePID;
    private float _PIDTau;
    private bool _useHoverPID;
    private float _PIDHoverHeight;
    private PIDHoverType _PIDHoverType;
    private float _PIDHoverTao;

    public BSCharacter(uint localID, String avName, BSScene parent_scene, Vector3 pos, Vector3 size, bool isFlying)
    {
        _localID = localID;
        _avName = avName;
        _scene = parent_scene;
        _position = pos;
        _size = size;
        _flying = isFlying;
        _orientation = Quaternion.Identity;
        _velocity = Vector3.Zero;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);
        // The dimensions of the avatar capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        _scale = new Vector3(_scene.Params.avatarCapsuleRadius, _scene.Params.avatarCapsuleRadius, size.Z);
        _density = _scene.Params.avatarDensity;
        ComputeAvatarVolumeAndMass();   // set _avatarVolume and _mass based on capsule size, _density and _scale

        ShapeData shapeData = new ShapeData();
        shapeData.ID = _localID;
        shapeData.Type = ShapeData.PhysicsShapeType.SHAPE_AVATAR;
        shapeData.Position = _position;
        shapeData.Rotation = _orientation;
        shapeData.Velocity = _velocity;
        shapeData.Scale = _scale;
        shapeData.Mass = _mass;
        shapeData.Buoyancy = _buoyancy;
        shapeData.Static = ShapeData.numericFalse;
        shapeData.Friction = _scene.Params.avatarFriction;
        shapeData.Restitution = _scene.Params.avatarRestitution;

        // do actual create at taint time
        _scene.TaintedObject("BSCharacter.create", delegate()
        {
            BulletSimAPI.CreateObject(parent_scene.WorldID, shapeData);

            m_body = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(_scene.World.Ptr, LocalID));
            // avatars get all collisions no matter what
            BulletSimAPI.AddToCollisionFlags2(Body.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        });
            
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public void Destroy()
    {
        // DetailLog("{0},BSCharacter.Destroy", LocalID);
        _scene.TaintedObject("BSCharacter.destroy", delegate()
        {
            BulletSimAPI.DestroyObject(_scene.WorldID, _localID);
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
    public override Vector3 Size {
        get
        {
            // Avatar capsule size is kept in the scale parameter.
            return new Vector3(_scale.X * 2, _scale.Y * 2, _scale.Z);
        }

        set { 
            // When an avatar's size is set, only the height is changed
            //    and that really only depends on the radius.
            _size = value;
            _scale.Z = (_size.Z * 1.15f) - (_scale.X + _scale.Y);

            // TODO: something has to be done with the avatar's vertical position

            ComputeAvatarVolumeAndMass();

            _scene.TaintedObject("BSCharacter.setSize", delegate()
            {
                BulletSimAPI.SetObjectScaleMass(_scene.WorldID, LocalID, _scale, _mass, true);
            });

        } 
    }
    public override PrimitiveBaseShape Shape { 
        set { _pbs = value; 
        } 
    }
    public override uint LocalID { 
        set { _localID = value; 
        }
        get { return _localID; }
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
    public override void LockAngularMotion(Vector3 axis) { return; }

    public override Vector3 Position { 
        get {
            // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
            return _position; 
        } 
        set {
            _position = value;
            PositionSanityCheck();

            _scene.TaintedObject("BSCharacter.setPosition", delegate()
            {
                DetailLog("{0},BSCharacter.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
            });
        } 
    }

    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain and being out of bounds.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck()
    {
        bool ret = false;
        
        // If below the ground, move the avatar up
        float terrainHeight = Scene.GetTerrainHeightAtXYZ(_position);
        if (_position.Z < terrainHeight)
        {
            DetailLog("{0},BSCharacter.PositionAdjustUnderGround,call,pos={1},orient={2}", LocalID, _position, _orientation);
            _position.Z = terrainHeight + 2.0f;
            ret = true;
        }

        // TODO: check for out of bounds

        return ret;
    }

    public override float Mass { 
        get { 
            return _mass; 
        } 
    }
    public override Vector3 Force { 
        get { return _force; } 
        set {
            _force = value;
            // m_log.DebugFormat("{0}: Force = {1}", LogHeader, _force);
            Scene.TaintedObject("BSCharacter.SetForce", delegate()
            {
                DetailLog("{0},BSCharacter.setForce,taint,force={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce(Scene.WorldID, LocalID, _force);
            });
        } 
    }

    public override int VehicleType { 
        get { return 0; } 
        set { return; } 
    }
    public override void VehicleFloatParam(int param, float value) { }
    public override void VehicleVectorParam(int param, Vector3 value) {}
    public override void VehicleRotationParam(int param, Quaternion rotation) { }
    public override void VehicleFlags(int param, bool remove) { }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) { return; }

    public override Vector3 GeometricCenter { get { return Vector3.Zero; } }
    public override Vector3 CenterOfMass { get { return Vector3.Zero; } }
    public override Vector3 Velocity { 
        get { return _velocity; } 
        set {
            _velocity = value;
            // m_log.DebugFormat("{0}: set velocity = {1}", LogHeader, _velocity);
            _scene.TaintedObject("BSCharacter.setVelocity", delegate()
            {
                DetailLog("{0},BSCharacter.setVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetObjectVelocity(_scene.WorldID, _localID, _velocity);
            });
        } 
    }
    public override Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
        } 
    }
    public override float CollisionScore { 
        get { return _collisionScore; } 
        set { _collisionScore = value; 
        } 
    }
    public override Vector3 Acceleration { 
        get { return _acceleration; }
        set { _acceleration = value; }
    }
    public override Quaternion Orientation { 
        get { return _orientation; } 
        set {
            _orientation = value;
            // m_log.DebugFormat("{0}: set orientation to {1}", LogHeader, _orientation);
            _scene.TaintedObject("BSCharacter.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
            });
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
            if (_flying != value)
            {
                _flying = value;
                // simulate flying by changing the effect of gravity
                this.Buoyancy = ComputeBuoyancyFromFlying(_flying);
            }
        } 
    }
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
        get { return (_collidingStep == _scene.SimulationStep); } 
        set { _isColliding = value; } 
    }
    public override bool CollidingGround {
        get { return (_collidingGroundStep == _scene.SimulationStep); } 
        set { _collidingGround = value; } 
    }
    public override bool CollidingObj { 
        get { return _collidingObj; } 
        set { _collidingObj = value; } 
    }
    public override bool FloatOnWater { 
        set { _floatOnWater = value; } 
    }
    public override Vector3 RotationalVelocity { 
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
            _scene.TaintedObject("BSCharacter.setBuoyancy", delegate()
            {
                DetailLog("{0},BSCharacter.setBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                BulletSimAPI.SetObjectBuoyancy(_scene.WorldID, LocalID, _buoyancy);
            });
        } 
    }

    // Used for MoveTo
    public override Vector3 PIDTarget { 
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
    public override Quaternion APIDTarget { set { return; } }
    public override bool APIDActive { set { return; } }
    public override float APIDStrength { set { return; } }
    public override float APIDDamping { set { return; } }

    public override void AddForce(Vector3 force, bool pushforce) { 
        if (force.IsFinite())
        {
            _force.X += force.X;
            _force.Y += force.Y;
            _force.Z += force.Z;
            // m_log.DebugFormat("{0}: AddForce. adding={1}, newForce={2}", LogHeader, force, _force);
            _scene.TaintedObject("BSCharacter.AddForce", delegate()
            {
                DetailLog("{0},BSCharacter.setAddForce,taint,addedForce={1}", LocalID, _force);
                BulletSimAPI.AddObjectForce2(Body.Ptr, _force);
            });
        }
        else
        {
            m_log.ErrorFormat("{0}: Got a NaN force applied to a Character", LogHeader);
        }
        //m_lastUpdateSent = false;
    }

    public override void AddAngularForce(Vector3 force, bool pushforce) { 
    }
    public override void SetMomentum(Vector3 momentum) { 
    }

    // Turn on collision events at a rate no faster than one every the given milliseconds
    public override void SubscribeEvents(int ms) {
        _subscribedEventsMs = ms;
        if (ms > 0)
        {
            // make sure first collision happens
            _nextCollisionOkTime = Util.EnvironmentTickCount() - _subscribedEventsMs;

            Scene.TaintedObject("BSCharacter.SubscribeEvents", delegate()
            {
                BulletSimAPI.AddToCollisionFlags2(Body.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            });
        }
    }
    // Stop collision events
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
        // Avatars get all their collision events
        // Scene.TaintedObject("BSCharacter.UnSubscribeEvents", delegate()
        // {
        //     BulletSimAPI.RemoveFromCollisionFlags2(Body.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        // });
    }
    // Return 'true' if someone has subscribed to events
    public override bool SubscribedEvents() {
        return (_subscribedEventsMs > 0);
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
        _mass = _density * _avatarVolume;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public void UpdateProperties(EntityProperties entprop)
    {
        _position = entprop.Position;
        _orientation = entprop.Rotation;
        _velocity = entprop.Velocity;
        _acceleration = entprop.Acceleration;
        _rotationalVelocity = entprop.RotationalVelocity;
        // Avatars don't report their changes the usual way. Changes are checked for in the heartbeat loop.
        // base.RequestPhysicsterseUpdate();

        /*
        DetailLog("{0},BSCharacter.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                LocalID, entprop.Position, entprop.Rotation, entprop.Velocity, 
                entprop.Acceleration, entprop.RotationalVelocity);
         */
    }

    // Called by the scene when a collision with this object is reported
    // The collision, if it should be reported to the character, is placed in a collection
    //   that will later be sent to the simulator when SendCollisions() is called.
    CollisionEventUpdate collisionCollection = null;
    public void Collide(uint collidingWith, ActorTypes type, Vector3 contactPoint, Vector3 contactNormal, float pentrationDepth)
    {
        // m_log.DebugFormat("{0}: Collide: ms={1}, id={2}, with={3}", LogHeader, _subscribedEventsMs, LocalID, collidingWith);

        // The following makes IsColliding() and IsCollidingGround() work
        _collidingStep = _scene.SimulationStep;
        if (collidingWith == BSScene.TERRAIN_ID || collidingWith == BSScene.GROUNDPLANE_ID)
        {
            _collidingGroundStep = _scene.SimulationStep;
        }
        // DetailLog("{0},BSCharacter.Collison,call,with={1}", LocalID, collidingWith);

        // throttle collisions to the rate specified in the subscription
        if (_subscribedEventsMs != 0) {
            int nowTime = _scene.SimulationNowTime;
            if (nowTime >= _nextCollisionOkTime) {
                _nextCollisionOkTime = nowTime + _subscribedEventsMs;

                if (collisionCollection == null)
                    collisionCollection = new CollisionEventUpdate();
                collisionCollection.AddCollider(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
            }
        }
    }

    public void SendCollisions()
    {
        /*
        if (collisionCollection != null && collisionCollection.Count > 0)
        {
            base.SendCollisionUpdate(collisionCollection);
            collisionCollection = null;
        }
         */
        // Kludge to make a collision call even if there are no collisions.
        // This causes the avatar animation to get updated.
        if (collisionCollection == null)
            collisionCollection = new CollisionEventUpdate();
        base.SendCollisionUpdate(collisionCollection);
        // If there were any collisions in the collection, make sure we don't use the
        //    same instance next time.
        if (collisionCollection.Count > 0)
            collisionCollection = null;
        // End kludge
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        Scene.PhysicsLogging.Write(msg, args);
    }
}
}
