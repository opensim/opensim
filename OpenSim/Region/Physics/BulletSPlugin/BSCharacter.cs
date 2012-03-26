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
    private String _avName;
    private bool _stopped;
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

    private int _subscribedEventsMs = 0;
    private int _lastCollisionTime = 0;

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
        _scale = new Vector3(1f, 1f, 1f);
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
        _scene.TaintedObject(delegate()
        {
            BulletSimAPI.CreateObject(parent_scene.WorldID, shapeData);
        });
            
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public void Destroy()
    {
        _scene.TaintedObject(delegate()
        {
            BulletSimAPI.DestroyObject(_scene.WorldID, _localID);
        });
    }

    public override void RequestPhysicsterseUpdate()
    {
        base.RequestPhysicsterseUpdate();
    }

    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override Vector3 Size { 
        get { return _size; } 
        set { _size = value;
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
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
            });
        } 
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
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectForce(_scene.WorldID, _localID, _force);
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
            _scene.TaintedObject(delegate()
            {
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
            _scene.TaintedObject(delegate()
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
            _flying = value;
            // simulate flying by changing the effect of gravity
            this.Buoyancy = ComputeBuoyancyFromFlying(_flying);
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
            _scene.TaintedObject(delegate()
            {
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
            _scene.TaintedObject(delegate()
            {
                BulletSimAPI.SetObjectForce(_scene.WorldID, _localID, _force);
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
    public override void SubscribeEvents(int ms) {
        _subscribedEventsMs = ms;
        _lastCollisionTime = Util.EnvironmentTickCount() - _subscribedEventsMs; // make first collision happen
    }
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
    }
    public override bool SubscribedEvents() {
        return (_subscribedEventsMs > 0);
    }

    // set _avatarVolume and _mass based on capsule size, _density and _scale
    private void ComputeAvatarVolumeAndMass()
    {
        _avatarVolume = (float)(
                        Math.PI
                        * _scene.Params.avatarCapsuleRadius * _scale.X
                        * _scene.Params.avatarCapsuleRadius * _scale.Y
                        * _scene.Params.avatarCapsuleHeight * _scale.Z);
        _mass = _density * _avatarVolume;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public void UpdateProperties(EntityProperties entprop)
    {
        bool changed = false;
        // we assign to the local variables so the normal set action does not happen
        if (_position != entprop.Position)
        {
            _position = entprop.Position;
            changed = true;
        }
        if (_orientation != entprop.Rotation)
        {
            _orientation = entprop.Rotation;
            changed = true;
        }
        if (_velocity != entprop.Velocity)
        {
            _velocity = entprop.Velocity;
            changed = true;
        }
        if (_acceleration != entprop.Acceleration)
        {
            _acceleration = entprop.Acceleration;
            changed = true;
        }
        if (_rotationalVelocity != entprop.RotationalVelocity)
        {
            _rotationalVelocity = entprop.RotationalVelocity;
            changed = true;
        }
        if (changed)
        {
            // m_log.DebugFormat("{0}: UpdateProperties: id={1}, c={2}, pos={3}, rot={4}", LogHeader, LocalID, changed, _position, _orientation);
            // Avatar movement is not done by generating this event. There is a system that
            //  checks for avatar updates each heartbeat loop.
            // base.RequestPhysicsterseUpdate();
        }
    }

    public void Collide(uint collidingWith, ActorTypes type, Vector3 contactPoint, Vector3 contactNormal, float pentrationDepth)
    {
        // m_log.DebugFormat("{0}: Collide: ms={1}, id={2}, with={3}", LogHeader, _subscribedEventsMs, LocalID, collidingWith);

        // The following makes IsColliding() and IsCollidingGround() work
        _collidingStep = _scene.SimulationStep;
        if (collidingWith == BSScene.TERRAIN_ID || collidingWith == BSScene.GROUNDPLANE_ID)
        {
            _collidingGroundStep = _scene.SimulationStep;
        }

        // throttle collisions to the rate specified in the subscription
        if (_subscribedEventsMs == 0) return;   // don't want collisions
        int nowTime = _scene.SimulationNowTime;
        if (nowTime < (_lastCollisionTime + _subscribedEventsMs)) return;
        _lastCollisionTime = nowTime;

        Dictionary<uint, ContactPoint> contactPoints = new Dictionary<uint, ContactPoint>();
        contactPoints.Add(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
        CollisionEventUpdate args = new CollisionEventUpdate(contactPoints);
        base.SendCollisionUpdate(args);
    }

}
}
