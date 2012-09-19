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
public sealed class BSPrim : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS PRIM]";

    private IMesh _mesh;
    private PrimitiveBaseShape _pbs;
    private ShapeData.PhysicsShapeType _shapeType;
    private ulong _meshKey;
    private ulong _hullKey;
    private List<ConvexResult> _hulls;

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
        base.BaseInitialize(parent_scene, localID, primName);
        _physicsActorType = (int)ActorTypes.Prim;
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
        _friction = PhysicsScene.Params.defaultFriction;  // TODO: compute based on object material
        _density = PhysicsScene.Params.defaultDensity;    // TODO: compute based on object material
        _restitution = PhysicsScene.Params.defaultRestitution;
        _vehicle = new BSDynamics(PhysicsScene, this);            // add vehicleness
        _mass = CalculateMass();
        DetailLog("{0},BSPrim.constructor,call", LocalID);
        // do the actual object creation at taint time
        PhysicsScene.TaintedObject("BSPrim.create", delegate()
        {
            CreateGeomAndObject(true);

            // Get the pointer to the physical body for this object.
            // At the moment, we're still letting BulletSim manage the creation and destruction
            //    of the object. Someday we'll move that into the C# code.
            BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(PhysicsScene.World.Ptr, LocalID));
            BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));
            CurrentCollisionFlags = BulletSimAPI.GetCollisionFlags2(BSBody.Ptr);
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
            // everything in the C# world will get garbage collected. Tell the C++ world to free stuff.
            BulletSimAPI.DestroyObject(PhysicsScene.WorldID, LocalID);
        });
    }
    
    public override bool Stopped { 
        get { return _stopped; } 
    }
    public override OMV.Vector3 Size { 
        get { return _size; } 
        set {
            _size = value;
            PhysicsScene.TaintedObject("BSPrim.setSize", delegate()
            {
                _mass = CalculateMass();   // changing size changes the mass
                // Since _size changed, the mesh needs to be rebuilt. If rebuilt, all the correct
                //   scale and margins are set.
                CreateGeomAndObject(true);
                DetailLog("{0}: BSPrim.setSize: size={1}, scale={2}, mass={3}, physical={4}", LocalID, _size, _scale, _mass, IsPhysical);
            });
        } 
    }
    public override PrimitiveBaseShape Shape { 
        set {
            _pbs = value;
            PhysicsScene.TaintedObject("BSPrim.setShape", delegate()
            {
                _mass = CalculateMass();   // changing the shape changes the mass
                CreateGeomAndObject(false);
            });
        } 
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
        BulletSimAPI.SetLinearVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetAngularVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
        BulletSimAPI.SetInterpolationVelocity2(BSBody.Ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);
        BulletSimAPI.ClearForces2(BSBody.Ptr);
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
                _position = BulletSimAPI.GetPosition2(BSBody.Ptr);

            // don't do the GetObjectPosition for root elements because this function is called a zillion times
            // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, LocalID);
            return _position; 
        } 
        set {
            _position = value;
            // TODO: what does it mean to set the position of a child prim?? Rebuild the constraint?
            PhysicsScene.TaintedObject("BSPrim.setPosition", delegate()
            {
                DetailLog("{0},BSPrim.SetPosition,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.Ptr, _position, _orientation);
            });
        } 
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
                DetailLog("{0},BSPrim.setForce,taint,force={1}", LocalID, _force);
                BulletSimAPI.SetObjectForce2(BSBody.Ptr, _force);
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
            PhysicsScene.TaintedObject("setVehicleType", delegate()
            {
                // Done at taint time so we're sure the physics engine is not using the variables
                // Vehicle code changes the parameters for this vehicle type.
                _vehicle.ProcessTypeChange(type);
                // Tell the scene about the vehicle so it will get processing each frame.
                PhysicsScene.VehicleInSceneTypeChanged(this, type);
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
            _vehicle.Step(timeStep);
    }

    // Allows the detection of collisions with inherently non-physical prims. see llVolumeDetect for more
    public override void SetVolumeDetect(int param) {
        bool newValue = (param != 0);
        _isVolumeDetect = newValue;
        PhysicsScene.TaintedObject("BSPrim.SetVolumeDetect", delegate()
        {
            DetailLog("{0},setVolumeDetect,taint,volDetect={1}", LocalID, _isVolumeDetect);
            SetObjectDynamic();
        });
        return; 
    }

    public override OMV.Vector3 Velocity { 
        get { return _velocity; } 
        set {
            _velocity = value;
            PhysicsScene.TaintedObject("BSPrim.setVelocity", delegate()
            {
                DetailLog("{0},BSPrim.SetVelocity,taint,vel={1}", LocalID, _velocity);
                BulletSimAPI.SetLinearVelocity2(BSBody.Ptr, _velocity);
            });
        } 
    }
    public override OMV.Vector3 Torque { 
        get { return _torque; } 
        set { _torque = value; 
            DetailLog("{0},BSPrim.SetTorque,call,torque={1}", LocalID, _torque);
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
                _orientation = BulletSimAPI.GetOrientation2(BSBody.Ptr);
            }
            return _orientation;
        } 
        set {
            _orientation = value;
            // TODO: what does it mean if a child in a linkset changes its orientation? Rebuild the constraint?
            PhysicsScene.TaintedObject("BSPrim.setOrientation", delegate()
            {
                // _position = BulletSimAPI.GetObjectPosition(Scene.WorldID, LocalID);
                DetailLog("{0},BSPrim.setOrientation,taint,pos={1},orient={2}", LocalID, _position, _orientation);
                BulletSimAPI.SetTranslation2(BSBody.Ptr, _position, _orientation);
            });
        } 
    }
    public override int PhysicsActorType { 
        get { return _physicsActorType; } 
        set { _physicsActorType = value; } 
    }
    public override bool IsPhysical { 
        get { return _isPhysical; } 
        set {
            _isPhysical = value;
            PhysicsScene.TaintedObject("BSPrim.setIsPhysical", delegate()
            {
                DetailLog("{0},setIsPhysical,taint,isPhys={1}", LocalID, _isPhysical);
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
    // There are four flags we're interested in:
    //     IsStatic: Object does not move, otherwise the object has mass and moves
    //     isSolid: other objects bounce off of this object
    //     isVolumeDetect: other objects pass through but can generate collisions
    //     collisionEvents: whether this object returns collision events
    private void SetObjectDynamic()
    {
        // If it's becoming dynamic, it will need hullness
        VerifyCorrectPhysicalShape();
        UpdatePhysicalParameters();
    }

    private void UpdatePhysicalParameters()
    {
        /*
        // Bullet wants static objects to have a mass of zero
        float mass = IsStatic ? 0f : _mass;

        BulletSimAPI.SetObjectProperties(Scene.WorldID, LocalID, IsStatic, IsSolid, SubscribedEvents(), mass);
         */
        BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);

        // Make solid or not (do things bounce off or pass through this object)
        // This is done first because it can change the collisionObject type.
        MakeSolid(IsSolid);

        // Set up the object physicalness (does gravity and collisions move this object)
        MakeDynamic(IsStatic);

        // Arrange for collisions events if the simulator wants them
        EnableCollisions(SubscribedEvents());

        BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);

        // Recompute any linkset parameters.
        // When going from non-physical to physical, this re-enables the constraints that
        //     had been automatically disabled when the mass was set to zero.
        Linkset.Refresh(this);

        DetailLog("{0},BSPrim.UpdatePhysicalParameters,taint,static={1},solid={2},mass={3},collide={4},cf={5}", 
                        LocalID, IsStatic, IsSolid, _mass, SubscribedEvents(), CurrentCollisionFlags);
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
            CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_STATIC_OBJECT);
            // Stop all movement
            BulletSimAPI.ClearAllForces2(BSBody.Ptr);
            // Center of mass is at the center of the object
            BulletSimAPI.SetCenterOfMassByPosRot2(Linkset.LinksetRoot.BSBody.Ptr, _position, _orientation);
            // Mass is zero which disables a bunch of physics stuff in Bullet
            BulletSimAPI.SetMassProps2(BSBody.Ptr, 0f, OMV.Vector3.Zero);
            // There is no inertia in a static object
            BulletSimAPI.UpdateInertiaTensor2(BSBody.Ptr);
            // There can be special things needed for implementing linksets
            Linkset.MakeStatic(this);
            // The activation state is 'sleeping' so Bullet will not try to act on it
            // BulletSimAPI.ForceActivationState2(BSBody.Ptr, ActivationState.ISLAND_SLEEPING);
            BulletSimAPI.ForceActivationState2(BSBody.Ptr, ActivationState.DISABLE_SIMULATION);
        }
        else
        {
            // Not a Bullet static object
            CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.CF_STATIC_OBJECT);

            // Set various physical properties so internal dynamic properties will get computed correctly as they are set
            BulletSimAPI.SetFriction2(BSBody.Ptr, PhysicsScene.Params.defaultFriction);
            BulletSimAPI.SetRestitution2(BSBody.Ptr, PhysicsScene.Params.defaultRestitution);
            // per http://www.bulletphysics.org/Bullet/phpBB3/viewtopic.php?t=3382
            BulletSimAPI.SetInterpolationLinearVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
            BulletSimAPI.SetInterpolationAngularVelocity2(BSBody.Ptr, OMV.Vector3.Zero);
            BulletSimAPI.SetInterpolationVelocity2(BSBody.Ptr, OMV.Vector3.Zero, OMV.Vector3.Zero);

            // A dynamic object has mass
            IntPtr collisionShapePtr = BulletSimAPI.GetCollisionShape2(BSBody.Ptr);
            OMV.Vector3 inertia = BulletSimAPI.CalculateLocalInertia2(collisionShapePtr, Linkset.LinksetMass);
            BulletSimAPI.SetMassProps2(BSBody.Ptr, _mass, inertia);
            // Inertia is based on our new mass
            BulletSimAPI.UpdateInertiaTensor2(BSBody.Ptr);

            // Various values for simulation limits
            BulletSimAPI.SetDamping2(BSBody.Ptr, PhysicsScene.Params.linearDamping, PhysicsScene.Params.angularDamping);
            BulletSimAPI.SetDeactivationTime2(BSBody.Ptr, PhysicsScene.Params.deactivationTime);
            BulletSimAPI.SetSleepingThresholds2(BSBody.Ptr, PhysicsScene.Params.linearSleepingThreshold, PhysicsScene.Params.angularSleepingThreshold);
            BulletSimAPI.SetContactProcessingThreshold2(BSBody.Ptr, PhysicsScene.Params.contactProcessingThreshold);

            // There can be special things needed for implementing linksets
            Linkset.MakeDynamic(this);

            // Force activation of the object so Bullet will act on it.
            BulletSimAPI.Activate2(BSBody.Ptr, true);
        }
    }

    // "Making solid" means that other object will not pass through this object.
    // To make transparent, we create a Bullet ghost object.
    // Note: This expects to be called from the UpdatePhysicalParameters() routine as
    //     the functions after this one set up the state of a possibly newly created collision body.
    private void MakeSolid(bool makeSolid)
    {
        CollisionObjectTypes bodyType = (CollisionObjectTypes)BulletSimAPI.GetBodyType2(BSBody.Ptr);
        /*
        if (makeSolid)
        {
            if ((bodyType & CollisionObjectTypes.CO_RIGID_BODY) == 0)
            {
                // Solid things are made out of rigid bodies. Remove this old body from the world
                //    and use this shape in a new rigid body.
                BulletBody oldBody = BSBody;
                BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);
                BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));
                BSBody = new BulletBody(LocalID, BulletSimAPI.CreateBodyFromShape2(PhysicsScene.World.Ptr, BSShape.Ptr, _position, _orientation));
                BulletSimAPI.DestroyObject2(PhysicsScene.World.Ptr, oldBody.Ptr);
                BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);
            }
        }
        else
        {
            if ((bodyType & CollisionObjectTypes.CO_GHOST_OBJECT) == 0)
            {
                // Non-solid things are made out of ghost objects. Remove this old body from the world
                //    and use this shape in a new rigid body.
                BulletBody oldBody = BSBody;
                BulletSimAPI.RemoveObjectFromWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);
                BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));
                BSBody = new BulletBody(LocalID, 
                        BulletSimAPI.CreateGhostFromShape2(PhysicsScene.World.Ptr, BSShape.Ptr, _position, _orientation));
                if (BSBody.Ptr == IntPtr.Zero)
                {
                    m_log.ErrorFormat("{0} BSPrim.MakeSolid: failed creation of ghost object. LocalID=[1}", LogHeader, LocalID);
                    BSBody = oldBody;
                }
                else
                {
                    BulletSimAPI.DestroyObject2(PhysicsScene.World.Ptr, oldBody.Ptr);
                    BulletSimAPI.AddObjectToWorld2(PhysicsScene.World.Ptr, BSBody.Ptr);
                }
            }
        }
         */
    }

    // Turn on or off the flag controlling whether collision events are returned to the simulator.
    private void EnableCollisions(bool wantsCollisionEvents)
    {
        if (wantsCollisionEvents)
        {
            CurrentCollisionFlags = BulletSimAPI.AddToCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        }
        else
        {
            CurrentCollisionFlags = BulletSimAPI.RemoveFromCollisionFlags2(BSBody.Ptr, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
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
            PhysicsScene.TaintedObject("BSPrim.setRotationalVelocity", delegate()
            {
                DetailLog("{0},BSPrim.SetRotationalVel,taint,rotvel={1}", LocalID, _rotationalVelocity);
                BulletSimAPI.SetAngularVelocity2(BSBody.Ptr, _rotationalVelocity);
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
            PhysicsScene.TaintedObject("BSPrim.setBuoyancy", delegate()
            {
                DetailLog("{0},BSPrim.SetBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                // Buoyancy is faked by changing the gravity applied to the object
                float grav = PhysicsScene.Params.gravity * (1f - _buoyancy);
                BulletSimAPI.SetGravity2(BSBody.Ptr, new OMV.Vector3(0f, 0f, grav));
                // BulletSimAPI.SetObjectBuoyancy(Scene.WorldID, LocalID, _buoyancy);
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
            m_log.WarnFormat("{0}: Got a NaN force applied to a prim. LocalID={1}", LogHeader, LocalID);
            return;
        }
        PhysicsScene.TaintedObject("BSPrim.AddForce", delegate()
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
            DetailLog("{0},BSPrim.AddObjectForce,taint,force={1}", LocalID, fSum);
            // For unknown reasons, "ApplyCentralForce" adds this force to the total force on the object.
            BulletSimAPI.ApplyCentralForce2(BSBody.Ptr, fSum);
        });
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce) { 
        DetailLog("{0},BSPrim.AddAngularForce,call,angForce={1},push={2}", LocalID, force, pushforce);
        // m_log.DebugFormat("{0}: AddAngularForce. f={1}, push={2}", LogHeader, force, pushforce);
    }
    public override void SetMomentum(OMV.Vector3 momentum) { 
        DetailLog("{0},BSPrim.SetMomentum,call,mom={1}", LocalID, momentum);
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

        if (returnMass > PhysicsScene.MaximumObjectMass)
            returnMass = PhysicsScene.MaximumObjectMass;

        return returnMass;
    }// end CalculateMass
    #endregion Mass Calculation

    // Create the geometry information in Bullet for later use.
    // The objects needs a hull if it's physical otherwise a mesh is enough.
    // No locking here because this is done when we know physics is not simulating.
    // if 'forceRebuild' is true, the geometry is rebuilt. Otherwise a previously built version is used.
    // Returns 'true' if the geometry was rebuilt.
    // Called at taint-time!
    private bool CreateGeom(bool forceRebuild)
    {
        bool ret = false;
        bool haveShape = false;

        // If the prim attributes are simple, this could be a simple Bullet native shape
        if ((_pbs.SculptEntry && !PhysicsScene.ShouldMeshSculptedPrim)
                || (_pbs.ProfileBegin == 0 && _pbs.ProfileEnd == 0
                    && _pbs.ProfileHollow == 0
                    && _pbs.PathTwist == 0 && _pbs.PathTwistBegin == 0
                    && _pbs.PathBegin == 0 && _pbs.PathEnd == 0
                    && _pbs.PathTaperX == 0 && _pbs.PathTaperY == 0
                    && _pbs.PathScaleX == 100 && _pbs.PathScaleY == 100
                    && _pbs.PathShearX == 0 && _pbs.PathShearY == 0) )
        {
            if (_pbs.ProfileShape == ProfileShape.HalfCircle && _pbs.PathCurve == (byte)Extrusion.Curve1)
            {
                haveShape = true;
                if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_SPHERE))
                {
                    DetailLog("{0},BSPrim.CreateGeom,sphere (force={1}", LocalID, forceRebuild);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_SPHERE;
                    // Bullet native objects are scaled by the Bullet engine so pass the size in
                    _scale = _size;
                    // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                    ret = true;
                }
            }
            else
            {
                // m_log.DebugFormat("{0}: CreateGeom: Defaulting to box. lid={1}, type={2}, size={3}", LogHeader, LocalID, _shapeType, _size);
                haveShape = true;
                if (forceRebuild || (_shapeType != ShapeData.PhysicsShapeType.SHAPE_BOX))
                {
                    DetailLog("{0},BSPrim.CreateGeom,box (force={1})", LocalID, forceRebuild);
                    _shapeType = ShapeData.PhysicsShapeType.SHAPE_BOX;
                    _scale = _size;
                    // TODO: do we need to check for and destroy a mesh or hull that might have been left from before?
                    ret = true;
                }
            }
        }
        // If a simple shape isn't happening, create a mesh and possibly a hull
        if (!haveShape)
        {
            if (IsPhysical)
            {
                if (forceRebuild || _hullKey == 0)
                {
                    // physical objects require a hull for interaction.
                    // This also creates the mesh if it doesn't already exist
                    ret = CreateGeomHull();
                }
            }
            else
            {
                if (forceRebuild || _meshKey == 0)
                {
                    // Static (non-physical) objects only need a mesh for bumping into
                    ret = CreateGeomMesh();
                }
            }
        }
        return ret;
    }

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    // Called at taint-time!
    private bool CreateGeomMesh()
    {
        // level of detail based on size and type of the object
        float lod = PhysicsScene.MeshLOD;
        if (_pbs.SculptEntry) 
            lod = PhysicsScene.SculptLOD;
        float maxAxis = Math.Max(_size.X, Math.Max(_size.Y, _size.Z));
        if (maxAxis > PhysicsScene.MeshMegaPrimThreshold) 
            lod = PhysicsScene.MeshMegaPrimLOD;

        ulong newMeshKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomMesh: lID={1}, oldKey={2}, newKey={3}", LogHeader, LocalID, _meshKey, newMeshKey);

        // if this new shape is the same as last time, don't recreate the mesh
        if (_meshKey == newMeshKey) return false;

        DetailLog("{0},BSPrim.CreateGeomMesh,create,key={1}", LocalID, newMeshKey);
        // Since we're recreating new, get rid of any previously generated shape
        if (_meshKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old mesh. lID={1}, Key={2}", LogHeader, LocalID, _meshKey);
            DetailLog("{0},BSPrim.CreateGeomMesh,deleteOld,key={1}", LocalID, _meshKey);
            BulletSimAPI.DestroyMesh(PhysicsScene.WorldID, _meshKey);
            _mesh = null;
            _meshKey = 0;
        }

        _meshKey = newMeshKey;
        // always pass false for physicalness as this creates some sort of bounding box which we don't need
        _mesh = PhysicsScene.mesher.CreateMesh(PhysObjectName, _pbs, _size, lod, false);

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
        //                  LogHeader, LocalID, _meshKey, indices.Length, vertices.Count);
        BulletSimAPI.CreateMesh(PhysicsScene.WorldID, _meshKey, indices.GetLength(0), indices, 
                                                        vertices.Count, verticesAsFloats);

        _shapeType = ShapeData.PhysicsShapeType.SHAPE_MESH;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        return true;
    }

    // No locking here because this is done when we know physics is not simulating
    // Returns 'true' of a mesh was actually rebuild (we could also have one of these specs).
    private bool CreateGeomHull()
    {
        float lod = _pbs.SculptEntry ? PhysicsScene.SculptLOD : PhysicsScene.MeshLOD;
        ulong newHullKey = (ulong)_pbs.GetMeshKey(_size, lod);
        // m_log.DebugFormat("{0}: CreateGeomHull: lID={1}, oldKey={2}, newKey={3}", LogHeader, LocalID, _hullKey, newHullKey);

        // if the hull hasn't changed, don't rebuild it
        if (newHullKey == _hullKey) return false;

        DetailLog("{0},BSPrim.CreateGeomHull,create,oldKey={1},newKey={2}", LocalID, _hullKey, newHullKey);
        
        // Since we're recreating new, get rid of any previously generated shape
        if (_hullKey != 0)
        {
            // m_log.DebugFormat("{0}: CreateGeom: deleting old hull. Key={1}", LogHeader, _hullKey);
            DetailLog("{0},BSPrim.CreateGeomHull,deleteOldHull,key={1}", LocalID, _hullKey);
            BulletSimAPI.DestroyHull(PhysicsScene.WorldID, _hullKey);
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
        // m_log.DebugFormat("{0}: CreateGeom: calling CreateHull. lid={1}, key={2}, hulls={3}", LogHeader, LocalID, _hullKey, hullCount);
        BulletSimAPI.CreateHull(PhysicsScene.WorldID, _hullKey, hullCount, convHulls);
        _shapeType = ShapeData.PhysicsShapeType.SHAPE_HULL;
        // meshes are already scaled by the meshmerizer
        _scale = new OMV.Vector3(1f, 1f, 1f);
        DetailLog("{0},BSPrim.CreateGeomHull,done", LocalID);
        return true;
    }

    // Callback from convex hull creater with a newly created hull.
    // Just add it to the collection of hulls for this shape.
    private void HullReturn(ConvexResult result)
    {
        _hulls.Add(result);
        return;
    }

    private void VerifyCorrectPhysicalShape()
    {
        if (!IsStatic)
        {
            // if not static, it will need a hull to efficiently collide with things
            if (_hullKey == 0)
            {
                CreateGeomAndObject(false);
            }

        }
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
        // m_log.DebugFormat("{0}: CreateObject: lID={1}, shape={2}", LogHeader, LocalID, shape.Type);
        bool ret = BulletSimAPI.CreateObject(PhysicsScene.WorldID, shape);

        // the CreateObject() may have recreated the rigid body. Make sure we have the latest address.
        BSBody = new BulletBody(LocalID, BulletSimAPI.GetBodyHandle2(PhysicsScene.World.Ptr, LocalID));
        BSShape = new BulletShape(BulletSimAPI.GetCollisionShape2(BSBody.Ptr));

        return ret;
    }

    // Copy prim's info into the BulletSim shape description structure
    public void FillShapeInfo(out ShapeData shape)
    {
        shape.ID = LocalID;
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
    private void CreateGeomAndObject(bool forceRebuild)
    {
        // m_log.DebugFormat("{0}: CreateGeomAndObject. lID={1}, force={2}", LogHeader, LocalID, forceRebuild);
        // Create the geometry that will make up the object
        if (CreateGeom(forceRebuild))
        {
            // Create the object and place it into the world
            CreateObject();
            // Make sure the properties are set on the new object
            UpdatePhysicalParameters();
        }
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

            DetailLog("{0},BSPrim.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, _position, _orientation, _velocity, _acceleration, _rotationalVelocity);

            // BulletSimAPI.DumpRigidBody2(Scene.World.Ptr, BSBody.Ptr);

            base.RequestPhysicsterseUpdate();
        }
            /*
        else
        {
            // For debugging, we can also report the movement of children
            DetailLog("{0},BSPrim.UpdateProperties,child,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                    LocalID, entprop.Position, entprop.Rotation, entprop.Velocity, 
                    entprop.Acceleration, entprop.RotationalVelocity);
        }
             */
    }
}
}
