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

namespace OpenSim.Region.Physics.BulletSPlugin
{
    [Serializable]
public sealed class BSPrim : PhysicsActor
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

    private void DebugLog(string mm, params Object[] xx) { if (_scene.ShouldDebugLog) m_log.DebugFormat(mm, xx); }

    private IMesh _mesh;
    private PrimitiveBaseShape _pbs;
    private ShapeData.PhysicsShapeType _shapeType;
    private ulong _meshKey;
    private ulong _hullKey;
    private List<ConvexResult> _hulls;

    private BSScene _scene;
    public BSScene Scene { get { return _scene; } }
    private String _avName;
    private uint _localID = 0;

    // _size is what the user passed. _scale is what we pass to the physics engine with the mesh.
    // Often _scale is unity because the meshmerizer will apply _size when creating the mesh.
    private OMV.Vector3 _size;  // the multiplier for each mesh dimension as passed by the user
    private OMV.Vector3 _scale; // the multiplier for each mesh dimension for the mesh as created by the meshmerizer

    private bool _stopped;
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

    // Membership in a linkset is controlled by this class.
    private BSLinkset _linkset;
    public BSLinkset Linkset
    {
        get { return _linkset; }
        set { _linkset = value; }
    }

    private int _subscribedEventsMs = 0;
    private int _nextCollisionOkTime = 0;
    long _collidingStep;
    long _collidingGroundStep;

    private BulletBody m_body;
    public BulletBody Body { 
        get { return m_body; }
        set { m_body = value; }
    }

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
        _localID = localID;
        _avName = primName;
        _scene = parent_scene;
        _position = pos;
        _size = size;
        _scale = new OMV.Vector3(1f, 1f, 1f);   // the scale will be set by CreateGeom depending on object type
        _orientation = rotation;
        _buoyancy = 1f;
        _velocity = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;
        _hullKey = 0;
        _meshKey = 0;
        _pbs = pbs;
        _isPhysical = pisPhysical;
        _isVolumeDetect = false;
        _subscribedEventsMs = 0;
        _friction = _scene.Params.defaultFriction;  // TODO: compute based on object material
        _density = _scene.Params.defaultDensity;    // TODO: compute based on object material
        _restitution = _scene.Params.defaultRestitution;
        _linkset = new BSLinkset(_scene, this);     // a linkset of one
        _vehicle = new BSDynamics(this);            // add vehicleness
        _mass = CalculateMass();
        // do the actual object creation at taint time
        DetailLog("{0},BSPrim.constructor,call", LocalID);
        _scene.TaintedObject("BSPrim.create", delegate()
        {
            RecreateGeomAndObject();

            // Get the pointer to the physical body for this object.
            // At the moment, we're still letting BulletSim manage the creation and destruction
            //    of the object. Someday we'll move that into the C# code.
            m_body = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(_scene.World.Ptr, LocalID));
        });
    }

    // called when this prim is being destroyed and we should free all the resources
    public void Destroy()
    {
        // m_log.DebugFormat("{0}: Destroy, id={1}", LogHeader, LocalID);

        // Undo any links between me and any other object
        BSPrim parentBefore = _linkset.LinksetRoot;
        int childrenBefore = _linkset.NumberOfChildren;

        _linkset = _linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.Destroy,call,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}",
            LocalID, parentBefore.LocalID, childrenBefore, _linkset.LinksetRoot.LocalID, _linkset.NumberOfChildren);

        // Undo any vehicle properties
        this.VehicleType = (int)Vehicle.TYPE_NONE;

        _scene.TaintedObject("BSPrim.destroy", delegate()
        {
            DetailLog("{0},BSPrim.Destroy,taint,", LocalID);
            // everything in the C# world will get garbage collected. Tell the C++ world to free stuff.
            BulletSimAPI.DestroyObject(_scene.WorldID, LocalID);
        });
    }
    
    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override OMV.Vector3 Size { 
        get { return _size; } 
        set {
            _size = value;
            _scene.TaintedObject("BSPrim.setSize", delegate()
            {
                _mass = CalculateMass();   // changing size changes the mass
                BulletSimAPI.SetObjectScaleMass(_scene.WorldID, _localID, _scale, (IsPhysical ? _mass : 0f), IsPhysical);
                // DetailLog("{0}: BSPrim.setSize: size={1}, mass={2}, physical={3}", LocalID, _size, _mass, IsPhysical);
                RecreateGeomAndObject();
            });
        } 
    }
    public override PrimitiveBaseShape Shape { 
        set {
            _pbs = value;
            _scene.TaintedObject("BSPrim.setShape", delegate()
            {
                _mass = CalculateMass();   // changing the shape changes the mass
                RecreateGeomAndObject();
            });
        } 
    }
    public override uint LocalID { 
        set { _localID = value; }
        get { return _localID; }
    }
    public override bool Grabbed { 
        set { _grabbed = value; 
        } 
    }
    public override bool Selected { 
        set {
            _isSelected = value;
            _scene.TaintedObject("BSPrim.setSelected", delegate()
            {
                SetObjectDynamic();
            });
        } 
    }
    public override void CrossingFailure() { return; }

    // link me to the specified parent
    public override void link(PhysicsActor obj) {
        BSPrim parent = obj as BSPrim;
        if (parent != null)
        {
            DebugLog("{0}: link {1}/{2} to {3}", LogHeader, _avName, _localID, parent.LocalID);
            BSPrim parentBefore = _linkset.LinksetRoot;
            int childrenBefore = _linkset.NumberOfChildren;

            _linkset = parent.Linkset.AddMeToLinkset(this);

            DetailLog("{0},BSPrim.link,call,parentBefore={1}, childrenBefore=={2}, parentAfter={3}, childrenAfter={4}", 
                LocalID, parentBefore.LocalID, childrenBefore, _linkset.LinksetRoot.LocalID, _linkset.NumberOfChildren);
        }
        return; 
    }

    // delink me from my linkset
    public override void delink() {
        // TODO: decide if this parent checking needs to happen at taint time
        // Race condition here: if link() and delink() in same simulation tick, the delink will not happen
        DebugLog("{0}: delink {1}/{2}. Parent={3}", LogHeader, _avName, _localID, 
                            _linkset.LinksetRoot._avName+"/"+_linkset.LinksetRoot.LocalID.ToString());

        BSPrim parentBefore = _linkset.LinksetRoot;
        int childrenBefore = _linkset.NumberOfChildren;
        
        _linkset = _linkset.RemoveMeFromLinkset(this);

        DetailLog("{0},BSPrim.delink,parentBefore={1},childrenBefore={2},parentAfter={3},childrenAfter={4}, ", 
            LocalID, parentBefore.LocalID, childrenBefore, _linkset.LinksetRoot.LocalID, _linkset.NumberOfChildren);
        return; 
    }

    // Set motion values to zero.
    // Do it to the properties so the values get set in the physics engine.
    // Push the setting of the values to the viewer.
    // Called at taint time!
    public void ZeroMotion()
    {
        _velocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties directly into the physics engine
        BulletSimAPI.SetVelocity2(Body.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetAngularVelocity2(Body.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetInterpolation2(Body.Ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);
        BulletSimAPI.ClearForces2(Body.Ptr);
    }

    public override void LockAngularMotion(OMV.Vector3 axis)
    { 
        // DetailLog("{0},BSPrim.LockAngularMotion,call,axis={1}", LocalID, axis);
        return;
    }

    public override OMV.Vector3 Position { 
        get { 
            if (!_linkset.IsRoot(this))
                // child prims move around based on their parent. Need to get the latest location
                _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);

            // don't do the GetObjectPosition for root elements because this function is called a zillion times
            // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
            return _position; 
        } 
        set {
            _position = value;
            // TODO: what does it mean to set the position of a child prim?? Rebuild the constraint?
            _scene.TaintedObject("BSPrim.setPosition", delegate()
            {
                // DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetObjectTranslation(_scene.WorldID, _localID, _position, _orientation);
            });
        } 
    }

    // Return the effective mass of the object.
    // If there are multiple items in the linkset, add them together for the root
    public override float Mass
    { 
        get
        {
            return _linkset.LinksetMass;
        }
    }

    // used when we only want this prim's mass and not the linkset thing
    public float MassRaw { get { return _mass; } }

    // Is this used?
    public override OMV.Vector3 CenterOfMass
    {
        get { return _linkset.CenterOfMass; }
    }

    // Is this used?
    public override OMV.Vector3 GeometricCenter
    {
        get { return _linkset.GeometricCenter; }
    }

    public override OMV.Vector3 Force { 
        get { return _force; } 
        set {
            _force = value;
            _scene.TaintedObject("BSPrim.setForce", delegate()
            {
                // DetailLog("{0},BSPrim.setForce,taint,force={1}", LocalID, _force);
                // BulletSimAPI.SetObjectForce(_scene.WorldID, _localID, _force);
                BulletSimAPI.SetObjectForce2(Body.Ptr, _force);
            });
        } 
    }

    public override int VehicleType { 
        get {
            return (int)_vehicle.Type;   // if we are a vehicle, return that type
        } 
        set {
            Vehicle type = (Vehicle)value;
            BSPrim vehiclePrim = this;
            _scene.TaintedObject("setVehicleType", delegate()
            {
                // Done at taint time so we're sure the physics engine is not using the variables
                // Vehicle code changes the parameters for this vehicle type.
                _vehicle.ProcessTypeChange(type);
                // Tell the scene about the vehicle so it will get processing each frame.
                _scene.VehicleInSceneTypeChanged(this, type);
            });
        } 
    }
    public override void VehicleFloatParam(int param, float value) 
    {
        _scene.TaintedObject("BSPrim.VehicleFloatParam", delegate()
        {
            _vehicle.ProcessFloatVehicleParam((Vehicle)param, value, _scene.LastSimulatedTimestep);
        });
    }
    public override void VehicleVectorParam(int param, OMV.Vector3 value) 
    {
        _scene.TaintedObject("BSPrim.VehicleVectorParam", delegate()
        {
            _vehicle.ProcessVectorVehicleParam((Vehicle)param, value, _scene.LastSimulatedTimestep);
        });
    }
    public override void VehicleRotationParam(int param, OMV.Quaternion rotation) 
    {
        _scene.TaintedObject("BSPrim.VehicleRotationParam", delegate()
        {
            _vehicle.ProcessRotationVehicleParam((Vehicle)param, rotation);
        });
    }
    public override void VehicleFlags(int param, bool remove) 
    {
        _scene.TaintedObject("BSPrim.VehicleFlags", delegate()
        {
            _vehicle.ProcessVehicleFlags(param, remove);
        });
    }

    // Called each simulation step to advance vehicle characteristics.
    // Called from Scene when doing simulation step so we're in taint processing time.
    public void StepVehicle(float timeStep)
    {
        if (IsPhysical)
            _vehicle.Step(timeStep);
    }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        _isVolumeDetect = newValue;
        _scene.TaintedObject("BSPrim.SetVolumeDetect", delegate()
        {
            SetObjectDynamic();
        });
        return; 
    }

    public override OMV.Vector3 Velocity { 
        get { return _velocity; } 
        set {
            _velocity = value;
            _scene.TaintedObject("BSPrim.setVelocity", delegate()
            {
                // DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetObjectVelocity(_scene.WorldID, LocalID, _velocity);
            });
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
            if (!_linkset.IsRoot(this))
            {
                // Children move around because tied to parent. Get a fresh value.
                _orientation = BulletSimAPI.GetObjectOrientation(_scene.WorldID, LocalID);
            }
            return _orientation;
        } 
        set {
            _orientation = value;
            // TODO: what does it mean if a child in a linkset changes its orientation? Rebuild the constraint?
            _scene.TaintedObject("BSPrim.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(_scene.WorldID, _localID);
                // DetailLog("{0},BSPrim.setOrientation,taint,pos={1},orient={2}", LocalID, _position, _orientation);
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
        set {
            _isPhysical = value;
            _scene.TaintedObject("BSPrim.setIsPhysical", delegate()
            {
                SetObjectDynamic();
            });
        } 
    }

    // An object is static (does not move) if selected or not physical
    private bool IsStatic
    {
        get { return _isSelected || !IsPhysical; }
    }

    // An object is solid if it's not phantom and if it's not doing VolumeDetect
    private bool IsSolid
    {
        get { return !IsPhantom && !_isVolumeDetect; }
    }

    // Make gravity work if the object is physical and not selected
    // No locking here because only called when it is safe
    private void SetObjectDynamic()
    {
        // RA: remove this for the moment.
        // The problem is that dynamic objects are hulls so if we are becoming physical
        //    the shape has to be checked and possibly built.
        //    Maybe a VerifyCorrectPhysicalShape() routine?
        // RecreateGeomAndObject();

        // Bullet wants static objects to have a mass of zero
        float mass = IsStatic ? 0f : _mass;

        BulletSimAPI.SetObjectProperties(_scene.WorldID, LocalID, IsStatic, IsSolid, SubscribedEvents(), mass);

        // recompute any linkset parameters
        _linkset.Refresh(this);

        CollisionFlags cf = BulletSimAPI.GetCollisionFlags2(Body.Ptr);
        // DetailLog("{0},BSPrim.SetObjectDynamic,taint,static={1},solid={2},mass={3}, cf={4}", LocalID, IsStatic, IsSolid, mass, cf);
    }

    // prims don't fly
    public override bool Flying { 
        get { return _flying; } 
        set { _flying = value; } 
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
    public bool IsPhantom {
        get {
            // SceneObjectPart removes phantom objects from the physics scene
            // so, although we could implement touching and such, we never
            // are invoked as a phantom object
            return false;
        }
    }
    public override bool FloatOnWater { 
        set { _floatOnWater = value; } 
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
            _scene.TaintedObject("BSPrim.setRotationalVelocity", delegate()
            {
                // DetailLog("{0},BSPrim.SetRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
                BulletSimAPI.SetObjectAngularVelocity(_scene.WorldID, LocalID, _rotationalVelocity);
            });
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
            _scene.TaintedObject("BSPrim.setBuoyancy", delegate()
            {
                // DetailLog("{0},BSPrim.SetBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                BulletSimAPI.SetObjectBuoyancy(_scene.WorldID, _localID, _buoyancy);
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

    private List<OMV.Vector3> m_accumulatedForces = new List<OMV.Vector3>();
    public override void AddForce(OMV.Vector3 force, bool pushforce) {
        // for an object, doesn't matter if force is a pushforce or not
        if (force.IsFinite())
        {
            // _force += force;
            lock (m_accumulatedForces)
                m_accumulatedForces.Add(new OMV.Vector3(force));
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a Character", LogHeader);
            return;
        }
        _scene.TaintedObject("BSPrim.AddForce", delegate()
        {
            OMV.Vector3 fSum = OMV.Vector3.Zero;
            lock (m_accumulatedForces)
            {
                foreach (OMV.Vector3 v in m_accumulatedForces)
                {
                    fSum += v;
                }
                m_accumulatedForces.Clear();
            }
            // DetailLog("{0},BSPrim.AddObjectForce,taint,force={1}", LocalID, _force);
            BulletSimAPI.AddObjectForce2(Body.Ptr, fSum);
        });
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) { 
        // DetailLog("{0},BSPrim.AddAngularForce,call,angForce={1},push={2}", LocalID, force, pushforce);
        // m_log.DebugFormat("{0}: AddAngularForce. f={1}, push={2}", LogHeader, force, pushforce);
    }
    public override void SetMomentum(OMV.Vector3 momentum) { 
        // DetailLog("{0},BSPrim.SetMomentum,call,mom={1}", LocalID, momentum);
    }
    public override void SubscribeEvents(int ms) { 
        _subscribedEventsMs = ms;
        if (ms > 0)
        {
            // make sure first collision happens
            _nextCollisionOkTime = Util.EnvironmentTickCount() - _subscribedEventsMs;

            Scene.TaintedObject("BSPrim.SubscribeEvents", delegate()
            {
                BulletSimAPI.AddToCollisionFlags2(Body.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            });
        }
    }
    public override void UnSubscribeEvents() { 
        _subscribedEventsMs = 0;
        Scene.TaintedObject("BSPrim.UnSubscribeEvents", delegate()
        {
            BulletSimAPI.RemoveFromCollisionFlags2(Body.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        });
    }
    public override bool SubscribedEvents() { 
        return (_subscribedEventsMs > 0);
    }

    #region Mass Calculation

    private float CalculateMass()
    {
        float volume = _size.X * _size.Y * _size.Z; // default
        float tmp;

        float returnMass = 0;
        float hollowAmount = (float)_pbs.ProfileHollow * 2.0e-5f;
        float hollowVolume = hollowAmount * hollowAmount; 
        
        switch (_pbs.ProfileShape)
        {
            case ProfileShape.Square:
                // default box

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    if (hollowAmount > 0.0)
                        {
                        switch (_pbs.HollowShape)
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

                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    //a tube 

                    volume *= 0.78539816339e-2f * (float)(200 - _pbs.PathScaleX);
                    tmp= 1.0f -2.0e-2f * (float)(200 - _pbs.PathScaleY);
                    volume -= volume*tmp*tmp;
                    
                    if (hollowAmount > 0.0)
                        {
                        hollowVolume *= hollowAmount;
                        
                        switch (_pbs.HollowShape)
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

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.78539816339f; // elipse base

                    if (hollowAmount > 0.0)
                        {
                        switch (_pbs.HollowShape)
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

                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - _pbs.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                    volume *= (1.0f - tmp * tmp);
                    
                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        hollowVolume *= hollowAmount;

                        switch (_pbs.HollowShape)
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
                if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                volume *= 0.52359877559829887307710723054658f;
                }
                break;

            case ProfileShape.EquilateralTriangle:

                if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                    volume *= 0.32475953f;

                    if (hollowAmount > 0.0)
                        {

                        // calculate the hollow volume by it's shape compared to the prim shape
                        switch (_pbs.HollowShape)
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
                else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                    volume *= 0.32475953f;
                    volume *= 0.01f * (float)(200 - _pbs.PathScaleX);
                    tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                    volume *= (1.0f - tmp * tmp);

                    if (hollowAmount > 0.0)
                        {

                        hollowVolume *= hollowAmount;

                        switch (_pbs.HollowShape)
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

        if (_pbs.PathCurve == (byte)Extrusion.Straight || _pbs.PathCurve == (byte)Extrusion.Flexible)
            {
            taperX1 = _pbs.PathScaleX * 0.01f;
            if (taperX1 > 1.0f)
                taperX1 = 2.0f - taperX1;
            taperX = 1.0f - taperX1;

            taperY1 = _pbs.PathScaleY * 0.01f;
            if (taperY1 > 1.0f)
                taperY1 = 2.0f - taperY1;
            taperY = 1.0f - taperY1;
            }
        else
            {
            taperX = _pbs.PathTaperX * 0.01f;
            if (taperX < 0.0f)
                taperX = -taperX;
            taperX1 = 1.0f - taperX;

            taperY = _pbs.PathTaperY * 0.01f;
            if (taperY < 0.0f)
                taperY = -taperY;
            taperY1 = 1.0f - taperY;

            }


        volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

        pathBegin = (float)_pbs.PathBegin * 2.0e-5f;
        pathEnd = 1.0f - (float)_pbs.PathEnd * 2.0e-5f;
        volume *= (pathEnd - pathBegin);

        // this is crude aproximation
        profileBegin = (float)_pbs.ProfileBegin * 2.0e-5f;
        profileEnd = 1.0f - (float)_pbs.ProfileEnd * 2.0e-5f;
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

        if (returnMass > _scene.MaximumObjectMass)
            returnMass = _scene.MaximumObjectMass;

        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

    // Create the geometry information in Bullet for later use
    // The objects needs a hull if it's physical otherwise a mesh is enough
    // No locking here because this is done when we know physics is not simulating
    // if 'forceRebuild' is true, the geometry is rebuilt. Otherwise a previously built version is used
    // Returns 'true' if the geometry was rebuilt
    private bool CreateGeom(bool forceRebuild)
    {
        // the mesher thought this was too simple to mesh. Use a native Bullet collision shape.
        bool ret = false;
        if (!_scene.NeedsMeshing(_pbs))
        {
            if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
            {
                // if (_size.X == _size.Y && _size.Y == _size.Z && _size.X == _size.Z)
                // {
                    // m_log.DebugFormat("{0}: CreateGeom: Defaulting to sphere of size {1}", LogHeader, _size);
                    if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_SPHERE))
                    {
                        // DetailLog("{0},BSPrim.CreateGeom,sphere (force={1}", LocalID, forceRebuild);
                        _shapeType = ShapeData.PhysicsShapeType.SHAPE_SPHERE;
                        // Bullet native objects are scaled by the Bullet engine so pass the size in
                        _scale = _size;
                        // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                        ret = true;
                    }
                // }
            }
            else
            {
                // m_log.DebugFormat("{0}: CreateGeom: Defaulting to box. lid={1}, type={2}, size={3}", LogHeader, LocalID, _shapeType, _size);
                if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_BOX))
                {
                    // DetailLog("{0},BSPrim.CreateGeom,box (force={1})", LocalID, forceRebuild);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_BOX;
                    _scale = _size;
                    // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                    ret = true;
                }
            }
        }
        else
        {
            if (IsPhysical)
            {
                if (forceRebuild || _hullKey == 0)
                {
                    // physical objects require a hull for interaction.
                    // This will create the mesh if it doesn't already exist
                    CreateGeomHull();
                    ret = true;
                }
            }
            else
            {
                if (forceRebuild || _meshKey == 0)
                {
                    // Static (non-physical) objects only need a mesh for bumping into
                    CreateGeomMesh();
                    ret = true;
                }
            }
        }
        return ret;
    }

    // No locking here because this is done when we know physics is not simulating
    private void CreateGeomMesh()
    {
        float lod = _pbs.SculptEntry ? _scene.SculptLOD : _scene.MeshLOD;
        ulong newMeshKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomMesh: lID={1}, oldKey={2}, newKey={3}", LogHeader, _localID, _meshKey, newMeshKey);

        // if this new shape is the same as last time, don't recreate the mesh
        if (_meshKey == newMeshKey) return;

        // DetailLog("{0},BSPrim.CreateGeomMesh,create,key={1}", LocalID, newMeshKey);
        // Since we're recreating new, get rid of any previously generated shape
        if (_meshKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old mesh. lID={1}, Key={2}", LogHeader, _localID, _meshKey);
            // DetailLog("{0},BSPrim.CreateGeomMesh,deleteOld,key={1}", LocalID, _meshKey);
            BulletSimAPI.DestroyMesh(_scene.WorldID, _meshKey);
            _mesh = null;
            _meshKey = 0;
        }

        _meshKey = newMeshKey;
        // always pass false for physicalness as this creates some sort of bounding box which we don't need
        _mesh = _scene.mesher.CreateMesh(_avName, _pbs, _size, lod, false);

        int[] indices = _mesh.getIndexListAsInt();
        List<OMV.Vector3> vertices = _mesh.getVertexList();

        float[] verticesAsFloats = new float[vertices.Count * 3];
        int vi = 0;
        foreach (OMV.Vector3 vv in vertices)
        {
            verticesAsFloats[vi++] = vv.X;
            verticesAsFloats[vi++] = vv.Y;
            verticesAsFloats[vi++] = vv.Z;
        }

        // m_log.DebugFormat("{0}: CreateGeomMesh: calling CreateMesh. lid={1}, key={2}, indices={3}, vertices={4}", 
        //                  LogHeader, _localID, _meshKey, indices.Length, vertices.Count);
        BulletSimAPI.CreateMesh(_scene.WorldID, _meshKey, indices.GetLength(0), indices, 
                                                        vertices.Count, verticesAsFloats);

        _shapeType = ShapeData.PhysicsShapeType.SHAPE_MESH;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        // DetailLog("{0},BSPrim.CreateGeomMesh,done", LocalID);
        return;
    }

    // No locking here because this is done when we know physics is not simulating
    private void CreateGeomHull()
    {
        float lod = _pbs.SculptEntry ? _scene.SculptLOD : _scene.MeshLOD;
        ulong newHullKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomHull: lID={1}, oldKey={2}, newKey={3}", LogHeader, _localID, _hullKey, newHullKey);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == _hullKey) return;

        // DetailLog("{0},BSPrim.CreateGeomHull,create,oldKey={1},newKey={2}", LocalID, _hullKey, newHullKey);
        
        // Since we're recreating new, get rid of any previously generated shape
        if (_hullKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old hull. Key={1}", LogHeader, _hullKey);
            // DetailLog("{0},BSPrim.CreateGeomHull,deleteOldHull,key={1}", LocalID, _hullKey);
            BulletSimAPI.DestroyHull(_scene.WorldID, _hullKey);
            _hullKey = 0;
        }

        _hullKey = newHullKey;

        // Make sure the underlying mesh exists and is correct
        CreateGeomMesh();

        int[] indices = _mesh.getIndexListAsInt();
        List<OMV.Vector3> vertices = _mesh.getVertexList();

        //format conversion from IMesh format to DecompDesc format
        List<int> convIndices = new List<int>();
        List<float3> convVertices = new List<float3>();
        for (int ii = 0; ii < indices.GetLength(0); ii++)
        {
            convIndices.Add(indices[ii]);
        }
        foreach (OMV.Vector3 vv in vertices)
        {
            convVertices.Add(new float3(vv.X, vv.Y, vv.Z));
        }

        // setup and do convex hull conversion
        _hulls = new List<ConvexResult>();
        DecompDesc dcomp = new DecompDesc();
        dcomp.mIndices = convIndices;
        dcomp.mVertices = convVertices;
        ConvexBuilder convexBuilder = new ConvexBuilder(HullReturn);
        // create the hull into the _hulls variable
        convexBuilder.process(dcomp);

        // Convert the vertices and indices for passing to unmanaged.
        // The hull information is passed as a large floating point array. 
        // The format is:
        //  convHulls[0] = number of hulls
        //  convHulls[1] = number of vertices in first hull
        //  convHulls[2] = hull centroid X coordinate
        //  convHulls[3] = hull centroid Y coordinate
        //  convHulls[4] = hull centroid Z coordinate
        //  convHulls[5] = first hull vertex X
        //  convHulls[6] = first hull vertex Y
        //  convHulls[7] = first hull vertex Z
        //  convHulls[8] = second hull vertex X
        //  ...
        //  convHulls[n] = number of vertices in second hull
        //  convHulls[n+1] = second hull centroid X coordinate
        //  ...
        //
        // TODO: is is very inefficient. Someday change the convex hull generator to return
        //   data structures that do not need to be converted in order to pass to Bullet.
        //   And maybe put the values directly into pinned memory rather than marshaling.
        int hullCount = _hulls.Count;
        int totalVertices = 1;          // include one for the count of the hulls
        foreach (ConvexResult cr in _hulls)
        {
            totalVertices += 4;                         // add four for the vertex count and centroid
            totalVertices += cr.HullIndices.Count * 3;  // we pass just triangles
        }
        float[] convHulls = new float[totalVertices];

        convHulls[0] = (float)hullCount;
        int jj = 1;
        foreach (ConvexResult cr in _hulls)
        {
            // copy vertices for index access
            float3[] verts = new float3[cr.HullVertices.Count];
            int kk = 0;
            foreach (float3 ff in cr.HullVertices)
            {
                verts[kk++] = ff;
            }

            // add to the array one hull's worth of data
            convHulls[jj++] = cr.HullIndices.Count;
            convHulls[jj++] = 0f;   // centroid x,y,z
            convHulls[jj++] = 0f;
            convHulls[jj++] = 0f;
            foreach (int ind in cr.HullIndices)
            {
                convHulls[jj++] = verts[ind].x;
                convHulls[jj++] = verts[ind].y;
                convHulls[jj++] = verts[ind].z;
            }
        }

        // create the hull definition in Bullet
        // m_log.DebugFormat("{0}: CreateGeom: calling CreateHull. lid={1}, key={2}, hulls={3}", LogHeader, _localID, _hullKey, hullCount);
        BulletSimAPI.CreateHull(_scene.WorldID, _hullKey, hullCount, convHulls);
        _shapeType = ShapeData.PhysicsShapeType.SHAPE_HULL;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        // DetailLog("{0},BSPrim.CreateGeomHull,done", LocalID);
        return;
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to the collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        _hulls.Add(result);
        return;
    }

    // Create an object in Bullet if it has not already been created
    // No locking here because this is done when the physics engine is not simulating
    // Returns 'true' if an object was actually created.
    private bool CreateObject()
    {
        // this routine is called when objects are rebuilt. 

        // the mesh or hull must have already been created in Bullet
        ShapeData shape;
        FillShapeInfo(out shape);
        // m_log.DebugFormat("{0}: CreateObject: lID={1}, shape={2}", LogHeader, _localID, shape.Type);
        bool ret = BulletSimAPI.CreateObject(_scene.WorldID, shape);

        // the CreateObject() may have recreated the rigid body. Make sure we have the latest.
        Body = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(_scene.World.Ptr, LocalID));

        return ret;
    }

    // Copy prim's info into the BulletSim shape description structure
    public void FillShapeInfo(out ShapeData shape)
    {
        shape.ID = _localID;
        shape.Type = _shapeType;
        shape.Position = _position;
        shape.Rotation = _orientation;
        shape.Velocity = _velocity;
        shape.Scale = _scale;
        shape.Mass = _isPhysical ? _mass : 0f;
        shape.Buoyancy = _buoyancy;
        shape.HullKey = _hullKey;
        shape.MeshKey = _meshKey;
        shape.Friction = _friction;
        shape.Restitution = _restitution;
        shape.Collidable = (!IsPhantom) ? ShapeData.numericTrue : ShapeData.numericFalse;
        shape.Static = _isPhysical ? ShapeData.numericFalse : ShapeData.numericTrue;
    }


    // Rebuild the geometry and object.
    // This is called when the shape changes so we need to recreate the mesh/hull.
    // No locking here because this is done when the physics engine is not simulating
    private void RecreateGeomAndObject()
    {
        // m_log.DebugFormat("{0}: RecreateGeomAndObject. lID={1}", LogHeader, _localID);
        if (CreateGeom(true))
            CreateObject();
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

    public void UpdateProperties(EntityProperties entprop)
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
        if (_linkset.IsRoot(this))
        {
            // Assign to the local variables so the normal set action does not happen
            _position = entprop.Position;
            _orientation = entprop.Rotation;
            _velocity = entprop.Velocity;
            _acceleration = entprop.Acceleration;
            _rotationalVelocity = entprop.RotationalVelocity;

            // m_log.DebugFormat("{0}: RequestTerseUpdate. id={1}, ch={2}, pos={3}, rot={4}, vel={5}, acc={6}, rvel={7}", 
            //         LogHeader, LocalID, changed, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);
            // DetailLog("{0},BSPrim.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
            //         LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);

            base.RequestPhysicsterseUpdate();
        }
            /*
        else
        {
            // For debugging, we also report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity, 
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */
    }

    // I've collided with something
    CollisionEventUpdate collisionCollection;
    public void Collide(uint collidingWith, ActorTypes type, OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        // m_log.DebugFormat("{0}: Collide: ms={1}, id={2}, with={3}", LogHeader, _subscribedEventsMs, LocalID, collidingWith);

        // The following lines make IsColliding() and IsCollidingGround() work
        _collidingStep = _scene.SimulationStep;
        if (collidingWith == BSScene.TERRAIN_ID || collidingWith == BSScene.GROUNDPLANE_ID)
        {
            _collidingGroundStep = _scene.SimulationStep;
        }

        // DetailLog("{0},BSPrim.Collison,call,with={1}", LocalID, collidingWith);

        // if someone is subscribed to collision events....
        if (_subscribedEventsMs != 0) {
            // throttle the collisions to the number of milliseconds specified in the subscription
            int nowTime = _scene.SimulationNowTime;
            if (nowTime >= _nextCollisionOkTime) {
                _nextCollisionOkTime = nowTime + _subscribedEventsMs;

                if (collisionCollection == null)
                    collisionCollection = new CollisionEventUpdate();
                collisionCollection.AddCollider(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
            }
        }
    }

    // The scene is telling us it's time to pass our collected collisions into the simulator
    public void SendCollisions()
    {
        if (collisionCollection != null && collisionCollection.Count > 0)
        {
            base.SendCollisionUpdate(collisionCollection);
            // The collisionCollection structure is passed around in the simulator.
            // Make sure we don't have a handle to that one and that a new one is used next time.
            collisionCollection = null;
        }
    }

    // Invoke the detailed logger and output something if it's enabled.
    private void DetailLog(string msg, params Object[] args)
    {
        Scene.PhysicsLogging.Write(msg, args);
    }
}
}
