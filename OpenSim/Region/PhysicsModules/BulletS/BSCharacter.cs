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
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModule.BulletS
{
public sealed class BSCharacter : BSPhysObject
{
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly string LogHeader = "[BULLETS CHAR]";

    // private bool _stopped;
    private OMV.Vector3 _size;
    private bool _grabbed;
    private bool _selected;
    private float _mass;
    private float _avatarVolume;
    private float _collisionScore;
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

    private BSActorAvatarMove m_moveActor;
    private const string AvatarMoveActorName = "BSCharacter.AvatarMove";

    private OMV.Vector3 _PIDTarget;
    private float _PIDTau;

//        public override OMV.Vector3 RawVelocity 
//        { get { return base.RawVelocity; } 
//            set { 
//                if (value != base.RawVelocity)
//                    Util.PrintCallStack();
//                Console.WriteLine("Set rawvel to {0}", value);
//                base.RawVelocity = value; }
//        }

    // Avatars are always complete (in the physics engine sense)
    public override bool IsIncomplete { get { return false; } }

    public BSCharacter(
            uint localID, String avName, BSScene parent_scene, OMV.Vector3 pos, OMV.Vector3 vel, OMV.Vector3 size, bool isFlying)

            : base(parent_scene, localID, avName, "BSCharacter")
    {
        _physicsActorType = (int)ActorTypes.Agent;
        RawPosition = pos;        

        _flying = isFlying;
        RawOrientation = OMV.Quaternion.Identity;
        RawVelocity = vel;
        _buoyancy = ComputeBuoyancyFromFlying(isFlying);
        Friction = BSParam.AvatarStandingFriction;
        Density = BSParam.AvatarDensity;

        // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
        //     replace with the default values.
        _size = size;
        if (_size.X == 0f) _size.X = BSParam.AvatarCapsuleDepth;
        if (_size.Y == 0f) _size.Y = BSParam.AvatarCapsuleWidth;

        // The dimensions of the physical capsule are kept in the scale.
        // Physics creates a unit capsule which is scaled by the physics engine.
        Scale = ComputeAvatarScale(_size);
        // set _avatarVolume and _mass based on capsule size, _density and Scale
        ComputeAvatarVolumeAndMass();

        DetailLog(
            "{0},BSCharacter.create,call,size={1},scale={2},density={3},volume={4},mass={5},pos={6},vel={7}",
            LocalID, _size, Scale, Density, _avatarVolume, RawMass, pos, vel);

        // do actual creation in taint time
        PhysScene.TaintedObject(LocalID, "BSCharacter.create", delegate()
        {
            DetailLog("{0},BSCharacter.create,taint", LocalID);

            // New body and shape into PhysBody and PhysShape
            PhysScene.Shapes.GetBodyAndShape(true, PhysScene.World, this);

            // The avatar's movement is controlled by this motor that speeds up and slows down
            //    the avatar seeking to reach the motor's target speed.
            // This motor runs as a prestep action for the avatar so it will keep the avatar
            //    standing as well as moving. Destruction of the avatar will destroy the pre-step action.
            m_moveActor = new BSActorAvatarMove(PhysScene, this, AvatarMoveActorName);
            PhysicalActors.Add(AvatarMoveActorName, m_moveActor);

            SetPhysicalProperties();

            IsInitialized = true;
        });
        return;
    }

    // called when this character is being destroyed and the resources should be released
    public override void Destroy()
    {
        IsInitialized = false;

        base.Destroy();

        DetailLog("{0},BSCharacter.Destroy", LocalID);
        PhysScene.TaintedObject(LocalID, "BSCharacter.destroy", delegate()
        {
            PhysScene.Shapes.DereferenceBody(PhysBody, null /* bodyCallback */);
            PhysBody.Clear();
            PhysShape.Dereference(PhysScene);
            PhysShape = new BSShapeNull();
        });
    }

    private void SetPhysicalProperties()
    {
        PhysScene.PE.RemoveObjectFromWorld(PhysScene.World, PhysBody);

        ForcePosition = RawPosition;

        // Set the velocity
        if (m_moveActor != null)
            m_moveActor.SetVelocityAndTarget(RawVelocity, RawVelocity, false);

        ForceVelocity = RawVelocity;
        TargetVelocity = RawVelocity;

        // This will enable or disable the flying buoyancy of the avatar.
        // Needs to be reset especially when an avatar is recreated after crossing a region boundry.
        Flying = _flying;

        PhysScene.PE.SetRestitution(PhysBody, BSParam.AvatarRestitution);
        PhysScene.PE.SetMargin(PhysShape.physShapeInfo, PhysScene.Params.collisionMargin);
        PhysScene.PE.SetLocalScaling(PhysShape.physShapeInfo, Scale);
        PhysScene.PE.SetContactProcessingThreshold(PhysBody, BSParam.ContactProcessingThreshold);
        if (BSParam.CcdMotionThreshold > 0f)
        {
            PhysScene.PE.SetCcdMotionThreshold(PhysBody, BSParam.CcdMotionThreshold);
            PhysScene.PE.SetCcdSweptSphereRadius(PhysBody, BSParam.CcdSweptSphereRadius);
        }

        UpdatePhysicalMassProperties(RawMass, false);

        // Make so capsule does not fall over
        PhysScene.PE.SetAngularFactorV(PhysBody, OMV.Vector3.Zero);

        // The avatar mover sets some parameters.
        PhysicalActors.Refresh();

        PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.CF_CHARACTER_OBJECT);

        PhysScene.PE.AddObjectToWorld(PhysScene.World, PhysBody);

        // PhysicsScene.PE.ForceActivationState(PhysBody, ActivationState.ACTIVE_TAG);
        PhysScene.PE.ForceActivationState(PhysBody, ActivationState.DISABLE_DEACTIVATION);
        PhysScene.PE.UpdateSingleAabb(PhysScene.World, PhysBody);

        // Do this after the object has been added to the world
        if (BSParam.AvatarToAvatarCollisionsByDefault)
            PhysBody.collisionType = CollisionType.Avatar;
        else
            PhysBody.collisionType = CollisionType.PhantomToOthersAvatar;

        PhysBody.ApplyCollisionMask(PhysScene);
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
            // This is how much the avatar size is changing. Positive means getting bigger.
            // The avatar altitude must be adjusted for this change.
            float heightChange = value.Z - _size.Z;

            _size = value;
            // Old versions of ScenePresence passed only the height. If width and/or depth are zero,
            //     replace with the default values.
            if (_size.X == 0f) _size.X = BSParam.AvatarCapsuleDepth;
            if (_size.Y == 0f) _size.Y = BSParam.AvatarCapsuleWidth;

            Scale = ComputeAvatarScale(_size);
            ComputeAvatarVolumeAndMass();
            DetailLog("{0},BSCharacter.setSize,call,size={1},scale={2},density={3},volume={4},mass={5}",
                            LocalID, _size, Scale, Density, _avatarVolume, RawMass);

            PhysScene.TaintedObject(LocalID, "BSCharacter.setSize", delegate()
            {
                if (PhysBody.HasPhysicalBody && PhysShape.physShapeInfo.HasPhysicalShape)
                {
                    PhysScene.PE.SetLocalScaling(PhysShape.physShapeInfo, Scale);
                    UpdatePhysicalMassProperties(RawMass, true);

                    // Adjust the avatar's position to account for the increase/decrease in size
                    ForcePosition = new OMV.Vector3(RawPosition.X, RawPosition.Y, RawPosition.Z + heightChange / 2f);

                    // Make sure this change appears as a property update event
                    PhysScene.PE.PushUpdate(PhysBody);
                }
            });

        }
    }

    public override PrimitiveBaseShape Shape
    {
        set { BaseShape = value; }
    }

    public override bool Grabbed {
        set { _grabbed = value; }
    }
    public override bool Selected {
        set { _selected = value; }
    }
    public override bool IsSelected
    {
        get { return _selected; }
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
        RawVelocity = OMV.Vector3.Zero;
        _acceleration = OMV.Vector3.Zero;
        _rotationalVelocity = OMV.Vector3.Zero;

        // Zero some other properties directly into the physics engine
        PhysScene.TaintedObject(inTaintTime, LocalID, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
                PhysScene.PE.ClearAllForces(PhysBody);
        });
    }

    public override void ZeroAngularMotion(bool inTaintTime)
    {
        _rotationalVelocity = OMV.Vector3.Zero;

        PhysScene.TaintedObject(inTaintTime, LocalID, "BSCharacter.ZeroMotion", delegate()
        {
            if (PhysBody.HasPhysicalBody)
            {
                PhysScene.PE.SetInterpolationAngularVelocity(PhysBody, OMV.Vector3.Zero);
                PhysScene.PE.SetAngularVelocity(PhysBody, OMV.Vector3.Zero);
                // The next also get rid of applied linear force but the linear velocity is untouched.
                PhysScene.PE.ClearForces(PhysBody);
            }
        });
    }


    public override void LockAngularMotion(byte axislocks) { return; }

    public override OMV.Vector3 Position {
        get {
            // Don't refetch the position because this function is called a zillion times
            // RawPosition = PhysicsScene.PE.GetObjectPosition(Scene.World, LocalID);
            return RawPosition;
        }
        set {
            RawPosition = value;

            PhysScene.TaintedObject(LocalID, "BSCharacter.setPosition", delegate()
            {
                DetailLog("{0},BSCharacter.SetPosition,taint,pos={1},orient={2}", LocalID, RawPosition, RawOrientation);
                PositionSanityCheck();
                ForcePosition = RawPosition;
            });
        }
    }
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
            }
        }
    }


    // Check that the current position is sane and, if not, modify the position to make it so.
    // Check for being below terrain or on water.
    // Returns 'true' of the position was made sane by some action.
    private bool PositionSanityCheck()
    {
        bool ret = false;

        // TODO: check for out of bounds
        if (!PhysScene.TerrainManager.IsWithinKnownTerrain(RawPosition))
        {
            // The character is out of the known/simulated area.
            // Force the avatar position to be within known. ScenePresence will use the position
            //    plus the velocity to decide if the avatar is moving out of the region.
            RawPosition = PhysScene.TerrainManager.ClampPositionIntoKnownTerrain(RawPosition);
            DetailLog("{0},BSCharacter.PositionSanityCheck,notWithinKnownTerrain,clampedPos={1}", LocalID, RawPosition);
            return true;
        }

        // If below the ground, move the avatar up
        float terrainHeight = PhysScene.TerrainManager.GetTerrainHeightAtXYZ(RawPosition);
        if (Position.Z < terrainHeight)
        {
            DetailLog("{0},BSCharacter.PositionSanityCheck,adjustForUnderGround,pos={1},terrain={2}", LocalID, RawPosition, terrainHeight);
            RawPosition = new OMV.Vector3(RawPosition.X, RawPosition.Y, terrainHeight + BSParam.AvatarBelowGroundUpCorrectionMeters);
            ret = true;
        }
        if ((CurrentCollisionFlags & CollisionFlags.BS_FLOATS_ON_WATER) != 0)
        {
            float waterHeight = PhysScene.TerrainManager.GetWaterLevelAtXYZ(RawPosition);
            if (Position.Z < waterHeight)
            {
                RawPosition = new OMV.Vector3(RawPosition.X, RawPosition.Y, waterHeight);
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
            PhysScene.TaintedObject(inTaintTime, LocalID, "BSCharacter.PositionSanityCheck", delegate()
            {
                DetailLog("{0},BSCharacter.PositionSanityCheck,taint,pos={1},orient={2}", LocalID, RawPosition, RawOrientation);
                ForcePosition = RawPosition;
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
    public override void UpdatePhysicalMassProperties(float physMass, bool inWorld)
    {
        OMV.Vector3 localInertia = PhysScene.PE.CalculateLocalInertia(PhysShape.physShapeInfo, physMass);
        PhysScene.PE.SetMassProps(PhysBody, physMass, localInertia);
    }

    public override OMV.Vector3 Force {
        get { return RawForce; }
        set {
            RawForce = value;
            // m_log.DebugFormat("{0}: Force = {1}", LogHeader, _force);
            PhysScene.TaintedObject(LocalID, "BSCharacter.SetForce", delegate()
            {
                DetailLog("{0},BSCharacter.setForce,taint,force={1}", LocalID, RawForce);
                if (PhysBody.HasPhysicalBody)
                    PhysScene.PE.SetObjectForce(PhysBody, RawForce);
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
    public override bool IsVolumeDetect { get { return false; } }

    public override OMV.Vector3 GeometricCenter { get { return OMV.Vector3.Zero; } }
    public override OMV.Vector3 CenterOfMass { get { return OMV.Vector3.Zero; } }

    // Sets the target in the motor. This starts the changing of the avatar's velocity.
    public override OMV.Vector3 TargetVelocity
    {
        get
        {
            return base.m_targetVelocity;
        }
        set
        {
            DetailLog("{0},BSCharacter.setTargetVelocity,call,vel={1}", LocalID, value);
            m_targetVelocity = value;
            OMV.Vector3 targetVel = value;
            if (_setAlwaysRun && !_flying)
                targetVel *= new OMV.Vector3(BSParam.AvatarAlwaysRunFactor, BSParam.AvatarAlwaysRunFactor, 1f);

            if (m_moveActor != null)
                m_moveActor.SetVelocityAndTarget(RawVelocity, targetVel, false /* inTaintTime */);
        }
    }
    // Directly setting velocity means this is what the user really wants now.
    public override OMV.Vector3 Velocity {
        get { return RawVelocity; }
        set {
            RawVelocity = value;
                OMV.Vector3 vel = RawVelocity;

            DetailLog("{0}: set Velocity = {1}", LocalID, value);

            PhysScene.TaintedObject(LocalID, "BSCharacter.setVelocity", delegate()
            {
                if (m_moveActor != null)
                    m_moveActor.SetVelocityAndTarget(vel, vel, true /* inTaintTime */);

                DetailLog("{0},BSCharacter.setVelocity,taint,vel={1}", LocalID, vel);
                ForceVelocity = vel;
            });
        }
    }

    public override OMV.Vector3 ForceVelocity {
        get { return RawVelocity; }
        set {
            PhysScene.AssertInTaintTime("BSCharacter.ForceVelocity");
//                Util.PrintCallStack();
            DetailLog("{0}: set ForceVelocity = {1}", LocalID, value);

            RawVelocity = value;
            PhysScene.PE.SetLinearVelocity(PhysBody, RawVelocity);
            PhysScene.PE.Activate(PhysBody, true);
        }
    }

    public override OMV.Vector3 Torque {
        get { return RawTorque; }
        set { RawTorque = value;
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
        get { return RawOrientation; }
        set {
            // Orientation is set zillions of times when an avatar is walking. It's like
            //      the viewer doesn't trust us.
            if (RawOrientation != value)
            {
                RawOrientation = value;
                PhysScene.TaintedObject(LocalID, "BSCharacter.setOrientation", delegate()
                {
                    // Bullet assumes we know what we are doing when forcing orientation
                    //    so it lets us go against all the rules and just compensates for them later.
                    // This forces rotation to be only around the Z axis and doesn't change any of the other axis.
                    // This keeps us from flipping the capsule over which the veiwer does not understand.
                    float oRoll, oPitch, oYaw;
                    RawOrientation.GetEulerAngles(out oRoll, out oPitch, out oYaw);
                    OMV.Quaternion trimmedOrientation = OMV.Quaternion.CreateFromEulers(0f, 0f, oYaw);
                    // DetailLog("{0},BSCharacter.setOrientation,taint,val={1},valDir={2},conv={3},convDir={4}",
                    //                 LocalID, RawOrientation, OMV.Vector3.UnitX * RawOrientation,
                    //                 trimmedOrientation, OMV.Vector3.UnitX * trimmedOrientation);
                    ForceOrientation = trimmedOrientation;
                });
            }
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
            {
                // RawPosition = PhysicsScene.PE.GetPosition(BSBody);
                PhysScene.PE.SetTranslation(PhysBody, RawPosition, RawOrientation);
            }
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
    public override bool IsPhysicallyActive {
        get { return true; }
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
    public override bool FloatOnWater {
        set {
            _floatOnWater = value;
            PhysScene.TaintedObject(LocalID, "BSCharacter.setFloatOnWater", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                {
                    if (_floatOnWater)
                        CurrentCollisionFlags = PhysScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
                    else
                        CurrentCollisionFlags = PhysScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_FLOATS_ON_WATER);
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
            PhysScene.TaintedObject(LocalID, "BSCharacter.setBuoyancy", delegate()
            {
                DetailLog("{0},BSCharacter.setBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
                ForceBuoyancy = _buoyancy;
            });
        }
    }
    public override float ForceBuoyancy {
        get { return _buoyancy; }
        set {
            PhysScene.AssertInTaintTime("BSCharacter.ForceBuoyancy");

            _buoyancy = value;
            DetailLog("{0},BSCharacter.setForceBuoyancy,taint,buoy={1}", LocalID, _buoyancy);
            // Buoyancy is faked by changing the gravity applied to the object
            float  grav = BSParam.Gravity * (1f - _buoyancy);
            Gravity = new OMV.Vector3(0f, 0f, grav);
            if (PhysBody.HasPhysicalBody)
                PhysScene.PE.SetGravity(PhysBody, Gravity);
        }
    }

    // Used for MoveTo
    public override OMV.Vector3 PIDTarget {
        set { _PIDTarget = value; }
    }

    public override bool PIDActive { get; set; }

    public override float PIDTau {
        set { _PIDTau = value; }
    }

    public override void AddForce(OMV.Vector3 force, bool pushforce)
    {
        // Since this force is being applied in only one step, make this a force per second.
        OMV.Vector3 addForce = force / PhysScene.LastTimeStep;
        AddForce(addForce, pushforce, false);
    }
    public override void AddForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
        if (force.IsFinite())
        {
            OMV.Vector3 addForce = Util.ClampV(force, BSParam.MaxAddForceMagnitude);
            // DetailLog("{0},BSCharacter.addForce,call,force={1}", LocalID, addForce);

            PhysScene.TaintedObject(inTaintTime, LocalID, "BSCharacter.AddForce", delegate()
            {
                // Bullet adds this central force to the total force for this tick
                // DetailLog("{0},BSCharacter.addForce,taint,force={1}", LocalID, addForce);
                if (PhysBody.HasPhysicalBody)
                {
                    PhysScene.PE.ApplyCentralForce(PhysBody, addForce);
                }
            });
        }
        else
        {
            m_log.WarnFormat("{0}: Got a NaN force applied to a character. LocalID={1}", LogHeader, LocalID);
            return;
        }
    }

    public override void AddAngularForce(OMV.Vector3 force, bool pushforce, bool inTaintTime) {
    }
    public override void SetMomentum(OMV.Vector3 momentum) {
    }

    private OMV.Vector3 ComputeAvatarScale(OMV.Vector3 size)
    {
        OMV.Vector3 newScale = size;

        // Bullet's capsule total height is the "passed height + radius * 2";
        // The base capsule is 1 unit in diameter and 2 units in height (passed radius=0.5, passed height = 1)
        // The number we pass in for 'scaling' is the multiplier to get that base
        //     shape to be the size desired.
        // So, when creating the scale for the avatar height, we take the passed height
        //     (size.Z) and remove the caps.
        // An oddity of the Bullet capsule implementation is that it presumes the Y
        //     dimension is the radius of the capsule. Even though some of the code allows
        //     for a asymmetrical capsule, other parts of the code presume it is cylindrical.

        // Scale is multiplier of radius with one of "0.5"

        float heightAdjust = BSParam.AvatarHeightMidFudge;
        if (BSParam.AvatarHeightLowFudge != 0f || BSParam.AvatarHeightHighFudge != 0f)
        {
            const float AVATAR_LOW = 1.1f;
            const float AVATAR_MID = 1.775f; // 1.87f
            const float AVATAR_HI = 2.45f;
            // An avatar is between 1.1 and 2.45 meters. Midpoint is 1.775m.
            float midHeightOffset = size.Z - AVATAR_MID;
            if (midHeightOffset < 0f)
            {
                // Small avatar. Add the adjustment based on the distance from midheight
                heightAdjust += ((-1f * midHeightOffset) / (AVATAR_MID - AVATAR_LOW)) * BSParam.AvatarHeightLowFudge;
            }
            else
            {
                // Large avatar. Add the adjustment based on the distance from midheight
                heightAdjust += ((midHeightOffset) / (AVATAR_HI - AVATAR_MID)) * BSParam.AvatarHeightHighFudge;
            }
        }
        if (BSParam.AvatarShape == BSShapeCollection.AvatarShapeCapsule)
        {
            newScale.X = size.X / 2f;
            newScale.Y = size.Y / 2f;
            // The total scale height is the central cylindar plus the caps on the two ends.
            newScale.Z = (size.Z + (Math.Min(size.X, size.Y) * 2) + heightAdjust) / 2f;
        }
        else
        {
            newScale.Z = size.Z + heightAdjust;
        }
        // m_log.DebugFormat("{0} ComputeAvatarScale: size={1},adj={2},scale={3}", LogHeader, size, heightAdjust, newScale);

        // If smaller than the endcaps, just fake like we're almost that small
        if (newScale.Z < 0)
            newScale.Z = 0.1f;

        DetailLog("{0},BSCharacter.ComputerAvatarScale,size={1},lowF={2},midF={3},hiF={4},adj={5},newScale={6}",
            LocalID, size, BSParam.AvatarHeightLowFudge, BSParam.AvatarHeightMidFudge, BSParam.AvatarHeightHighFudge, heightAdjust, newScale);

        return newScale;
    }

    // set _avatarVolume and _mass based on capsule size, _density and Scale
    private void ComputeAvatarVolumeAndMass()
    {
        _avatarVolume = (float)(
                        Math.PI
                        * Size.X / 2f
                        * Size.Y / 2f    // the area of capsule cylinder
                        * Size.Z         // times height of capsule cylinder
                      + 1.33333333f
                        * Math.PI
                        * Size.X / 2f
                        * Math.Min(Size.X, Size.Y) / 2
                        * Size.Y / 2f    // plus the volume of the capsule end caps
                        );
        _mass = Density * BSParam.DensityScaleFactor * _avatarVolume;
    }

    // The physics engine says that properties have updated. Update same and inform
    // the world that things have changed.
    public override void UpdateProperties(EntityProperties entprop)
    {
        // Let anyone (like the actors) modify the updated properties before they are pushed into the object and the simulator.
        TriggerPreUpdatePropertyAction(ref entprop);

        RawPosition = entprop.Position;
        RawOrientation = entprop.Rotation;

        // Smooth velocity. OpenSimulator is VERY sensitive to changes in velocity of the avatar
        //    and will send agent updates to the clients if velocity changes by more than
        //    0.001m/s. Bullet introduces a lot of jitter in the velocity which causes many
        //    extra updates.
        //
        // XXX: Contrary to the above comment, setting an update threshold here above 0.4 actually introduces jitter to 
        // avatar movement rather than removes it.  The larger the threshold, the bigger the jitter.
        // This is most noticeable in level flight and can be seen with
        // the "show updates" option in a viewer.  With an update threshold, the RawVelocity cycles between a lower
        // bound and an upper bound, where the difference between the two is enough to trigger a large delta v update
        // and subsequently trigger an update in ScenePresence.SendTerseUpdateToAllClients().  The cause of this cycle (feedback?)
        // has not yet been identified.
        //
        // If there is a threshold below 0.4 or no threshold check at all (as in ODE), then RawVelocity stays constant and extra
        // updates are not triggered in ScenePresence.SendTerseUpdateToAllClients().
//        if (!entprop.Velocity.ApproxEquals(RawVelocity, 0.1f))
            RawVelocity = entprop.Velocity;

        _acceleration = entprop.Acceleration;
        _rotationalVelocity = entprop.RotationalVelocity;

        // Do some sanity checking for the avatar. Make sure it's above ground and inbounds.
        if (PositionSanityCheck(true))
        {
            DetailLog("{0},BSCharacter.UpdateProperties,updatePosForSanity,pos={1}", LocalID, RawPosition);
            entprop.Position = RawPosition;
        }

        // remember the current and last set values
        LastEntityProperties = CurrentEntityProperties;
        CurrentEntityProperties = entprop;

        // Tell the linkset about value changes
        // Linkset.UpdateProperties(UpdatedProperties.EntPropUpdates, this);

        // Avatars don't report their changes the usual way. Changes are checked for in the heartbeat loop.
        // PhysScene.PostUpdate(this);

        DetailLog("{0},BSCharacter.UpdateProperties,call,pos={1},orient={2},vel={3},accel={4},rotVel={5}",
                LocalID, RawPosition, RawOrientation, RawVelocity, _acceleration, _rotationalVelocity);
    }
}
}
