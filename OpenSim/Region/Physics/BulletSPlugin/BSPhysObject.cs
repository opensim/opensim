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
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BulletSPlugin
{
/*
 * Class to wrap all objects.
 * The rest of BulletSim doesn't need to keep checking for avatars or prims
 *        unless the difference is significant.
 * 
 *  Variables in the physicsl objects are in three forms:
 *      VariableName: used by the simulator and performs taint operations, etc
 *      RawVariableName: direct reference to the BulletSim storage for the variable value
 *      ForceVariableName: direct reference (store and fetch) to the value in the physics engine.
 *  The last two (and certainly the last one) should be referenced only in taint-time.
 */

/*
 * As of 20121221, the following are the call sequences (going down) for different script physical functions:
 * llApplyImpulse       llApplyRotImpulse           llSetTorque             llSetForce
 * SOP.ApplyImpulse     SOP.ApplyAngularImpulse     SOP.SetAngularImpulse   SOP.SetForce
 * SOG.ApplyImpulse     SOG.ApplyAngularImpulse     SOG.SetAngularImpulse
 * PA.AddForce          PA.AddAngularForce          PA.Torque = v           PA.Force = v
 * BS.ApplyCentralForce BS.ApplyTorque              
 */

// Flags used to denote which properties updates when making UpdateProperties calls to linksets, etc.
public enum UpdatedProperties : uint
{
    Position                = 1 << 0,
    Orientation             = 1 << 1,
    Velocity                = 1 << 2,
    Acceleration            = 1 << 3,
    RotationalVelocity      = 1 << 4,
    EntPropUpdates          = Position | Orientation | Velocity | Acceleration | RotationalVelocity,
}
public abstract class BSPhysObject : PhysicsActor
{
    protected BSPhysObject()
    {
    }
    protected BSPhysObject(BSScene parentScene, uint localID, string name, string typeName)
    {
        PhysicsScene = parentScene;
        LocalID = localID;
        PhysObjectName = name;
        TypeName = typeName;

        // We don't have any physical representation yet.
        PhysBody = new BulletBody(localID);
        PhysShape = new BulletShape();

        // A linkset of just me
        Linkset = BSLinkset.Factory(PhysicsScene, this);
        PositionDisplacement = OMV.Vector3.Zero;

        LastAssetBuildFailed = false;

        // Default material type
        Material = MaterialAttributes.Material.Wood;

        CollisionCollection = new CollisionEventUpdate();
        CollisionsLastTick = CollisionCollection;
        SubscribedEventsMs = 0;
        CollidingStep = 0;
        CollidingGroundStep = 0;
    }

    // Tell the object to clean up.
    public virtual void Destroy()
    {
        UnRegisterAllPreStepActions();
    }

    public BSScene PhysicsScene { get; protected set; }
    // public override uint LocalID { get; set; } // Use the LocalID definition in PhysicsActor
    public string PhysObjectName { get; protected set; }
    public string TypeName { get; protected set; }

    public BSLinkset Linkset { get; set; }
    public BSLinksetInfo LinksetInfo { get; set; }

    // Return the object mass without calculating it or having side effects
    public abstract float RawMass { get; }
    // Set the raw mass but also update physical mass properties (inertia, ...)
    // 'inWorld' true if the object has already been added to the dynamic world.
    public abstract void UpdatePhysicalMassProperties(float mass, bool inWorld);

    // The last value calculated for the prim's inertia
    public OMV.Vector3 Inertia { get; set; }

    // Reference to the physical body (btCollisionObject) of this object
    public BulletBody PhysBody;
    // Reference to the physical shape (btCollisionShape) of this object
    public BulletShape PhysShape;

    // 'true' if the mesh's underlying asset failed to build.
    // This will keep us from looping after the first time the build failed.
    public bool LastAssetBuildFailed { get; set; }

    // The objects base shape information. Null if not a prim type shape.
    public PrimitiveBaseShape BaseShape { get; protected set; }
    // Some types of objects have preferred physical representations.
    // Returns SHAPE_UNKNOWN if there is no preference.
    public virtual BSPhysicsShapeType PreferredPhysicalShape
    {
        get { return BSPhysicsShapeType.SHAPE_UNKNOWN; }
    }

    // When the physical properties are updated, an EntityProperty holds the update values.
    // Keep the current and last EntityProperties to enable computation of differences
    //      between the current update and the previous values.
    public EntityProperties CurrentEntityProperties { get; set; }
    public EntityProperties LastEntityProperties { get; set; }

    public virtual OMV.Vector3 Scale { get; set; }
    public abstract bool IsSolid { get; }
    public abstract bool IsStatic { get; }
    public abstract bool IsSelected { get; }

    // It can be confusing for an actor to know if it should move or update an object
    //    depeneding on the setting of 'selected', 'physical, ...
    // This flag is the true test -- if true, the object is being acted on in the physical world
    public abstract bool IsPhysicallyActive { get; }

    // Materialness
    public MaterialAttributes.Material Material { get; private set; }
    public override void SetMaterial(int material)
    {
        Material = (MaterialAttributes.Material)material;
    }

    // Stop all physical motion.
    public abstract void ZeroMotion(bool inTaintTime);
    public abstract void ZeroAngularMotion(bool inTaintTime);

    // Step the vehicle simulation for this object. A NOOP if the vehicle was not configured.
    public virtual void StepVehicle(float timeStep) { }

    // Update the physical location and motion of the object. Called with data from Bullet.
    public abstract void UpdateProperties(EntityProperties entprop);

    public abstract OMV.Vector3 RawPosition { get; set; }
    public abstract OMV.Vector3 ForcePosition { get; set; }

    // Position is what the simulator thinks the positions of the prim is.
    // Because Bullet needs the zero coordinate to be the center of mass of the linkset,
    //     sometimes it is necessary to displace the position the physics engine thinks
    //     the position is. PositionDisplacement must be added and removed from the
    //     position as the simulator position is stored and fetched from the physics
    //     engine. 
    public virtual OMV.Vector3 PositionDisplacement { get; set; }

    public abstract OMV.Quaternion RawOrientation { get; set; }
    public abstract OMV.Quaternion ForceOrientation { get; set; }

    // The system is telling us the velocity it wants to move at.
    // Velocity in world coordinates.
    // protected OMV.Vector3 m_targetVelocity;  // use the definition in PhysicsActor
    public override OMV.Vector3 TargetVelocity
    {
        get { return m_targetVelocity; }
        set
        {
            m_targetVelocity = value;
            Velocity = value;
        }
    }
    public virtual float TargetSpeed
    {
        get
        {
            OMV.Vector3 characterOrientedVelocity = TargetVelocity * OMV.Quaternion.Inverse(OMV.Quaternion.Normalize(RawOrientation));
            return characterOrientedVelocity.X;
        }
    }
    public abstract OMV.Vector3 RawVelocity { get; set; }
    public abstract OMV.Vector3 ForceVelocity { get; set; }

    public abstract OMV.Vector3 ForceRotationalVelocity { get; set; }

    public abstract float ForceBuoyancy { get; set; }

    public virtual bool ForceBodyShapeRebuild(bool inTaintTime) { return false; }

    public virtual float ForwardSpeed
    {
        get
        {
            OMV.Vector3 characterOrientedVelocity = RawVelocity * OMV.Quaternion.Inverse(OMV.Quaternion.Normalize(RawOrientation));
            return characterOrientedVelocity.X;
        }
    }

    #region Collisions

    // Requested number of milliseconds between collision events. Zero means disabled.
    protected int SubscribedEventsMs { get; set; }
    // Given subscription, the time that a collision may be passed up
    protected int NextCollisionOkTime { get; set; }
    // The simulation step that last had a collision
    protected long CollidingStep { get; set; }
    // The simulation step that last had a collision with the ground
    protected long CollidingGroundStep { get; set; }
    // The simulation step that last collided with an object
    protected long CollidingObjectStep { get; set; }
    // The collision flags we think are set in Bullet
    protected CollisionFlags CurrentCollisionFlags { get; set; }

    public override bool IsColliding {
        get { return (CollidingStep == PhysicsScene.SimulationStep); }
        set {
            if (value)
                CollidingStep = PhysicsScene.SimulationStep;
            else
                CollidingStep = 0;
            }
    }
    public override bool CollidingGround {
        get { return (CollidingGroundStep == PhysicsScene.SimulationStep); }
        set
        {
            if (value)
                CollidingGroundStep = PhysicsScene.SimulationStep;
            else
                CollidingGroundStep = 0;
        }
    }
    public override bool CollidingObj {
        get { return (CollidingObjectStep == PhysicsScene.SimulationStep); }
        set { 
            if (value)
                CollidingObjectStep = PhysicsScene.SimulationStep;
            else
                CollidingObjectStep = 0;
        }
    }

    // The collisions that have been collected this tick
    protected CollisionEventUpdate CollisionCollection;
    // Remember collisions from last tick for fancy collision based actions
    //     (like a BSCharacter walking up stairs).
    protected CollisionEventUpdate CollisionsLastTick;

    // The simulation step is telling this object about a collision.
    // Return 'true' if a collision was processed and should be sent up.
    // Return 'false' if this object is not enabled/subscribed/appropriate for or has already seen this collision.
    // Called at taint time from within the Step() function
    public virtual bool Collide(uint collidingWith, BSPhysObject collidee,
                    OMV.Vector3 contactPoint, OMV.Vector3 contactNormal, float pentrationDepth)
    {
        bool ret = false;

        // The following lines make IsColliding(), CollidingGround() and CollidingObj work
        CollidingStep = PhysicsScene.SimulationStep;
        if (collidingWith <= PhysicsScene.TerrainManager.HighestTerrainID)
        {
            CollidingGroundStep = PhysicsScene.SimulationStep;
        }
        else
        {
            CollidingObjectStep = PhysicsScene.SimulationStep;
        }

        // prims in the same linkset cannot collide with each other
        if (collidee != null && (this.Linkset.LinksetID == collidee.Linkset.LinksetID))
        {
            return ret;
        }

        // if someone has subscribed for collision events....
        if (SubscribedEvents()) {
            CollisionCollection.AddCollider(collidingWith, new ContactPoint(contactPoint, contactNormal, pentrationDepth));
            DetailLog("{0},{1}.Collison.AddCollider,call,with={2},point={3},normal={4},depth={5}",
                            LocalID, TypeName, collidingWith, contactPoint, contactNormal, pentrationDepth);

            ret = true;
        }
        return ret;
    }

    // Send the collected collisions into the simulator.
    // Called at taint time from within the Step() function thus no locking problems
    //      with CollisionCollection and ObjectsWithNoMoreCollisions.
    // Return 'true' if there were some actual collisions passed up
    public virtual bool SendCollisions()
    {
        bool ret = true;

        // If the 'no collision' call, force it to happen right now so quick collision_end
        bool force = (CollisionCollection.Count == 0 && CollisionsLastTick.Count != 0);

        // throttle the collisions to the number of milliseconds specified in the subscription
        if (force || (PhysicsScene.SimulationNowTime >= NextCollisionOkTime))
        {
            NextCollisionOkTime = PhysicsScene.SimulationNowTime + SubscribedEventsMs;

            // We are called if we previously had collisions. If there are no collisions
            //   this time, send up one last empty event so OpenSim can sense collision end.
            if (CollisionCollection.Count == 0)
            {
                // If I have no collisions this time, remove me from the list of objects with collisions.
                ret = false;
            }

            DetailLog("{0},{1}.SendCollisionUpdate,call,numCollisions={2}", LocalID, TypeName, CollisionCollection.Count);
            base.SendCollisionUpdate(CollisionCollection);

            // Remember the collisions from this tick for some collision specific processing.
            CollisionsLastTick = CollisionCollection;

            // The CollisionCollection instance is passed around in the simulator.
            // Make sure we don't have a handle to that one and that a new one is used for next time.
            //    This fixes an interesting 'gotcha'. If we call CollisionCollection.Clear() here, 
            //    a race condition is created for the other users of this instance.
            CollisionCollection = new CollisionEventUpdate();
        }
        return ret;
    }

    // Subscribe for collision events.
    // Parameter is the millisecond rate the caller wishes collision events to occur.
    public override void SubscribeEvents(int ms) {
        // DetailLog("{0},{1}.SubscribeEvents,subscribing,ms={2}", LocalID, TypeName, ms);
        SubscribedEventsMs = ms;
        if (ms > 0)
        {
            // make sure first collision happens
            NextCollisionOkTime = Util.EnvironmentTickCountSubtract(SubscribedEventsMs);

            PhysicsScene.TaintedObject(TypeName+".SubscribeEvents", delegate()
            {
                if (PhysBody.HasPhysicalBody)
                    CurrentCollisionFlags = PhysicsScene.PE.AddToCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
            });
        }
        else
        {
            // Subscribing for zero or less is the same as unsubscribing
            UnSubscribeEvents();
        }
    }
    public override void UnSubscribeEvents() {
        // DetailLog("{0},{1}.UnSubscribeEvents,unsubscribing", LocalID, TypeName);
        SubscribedEventsMs = 0;
        PhysicsScene.TaintedObject(TypeName+".UnSubscribeEvents", delegate()
        {
            // Make sure there is a body there because sometimes destruction happens in an un-ideal order.
            if (PhysBody.HasPhysicalBody)
                CurrentCollisionFlags = PhysicsScene.PE.RemoveFromCollisionFlags(PhysBody, CollisionFlags.BS_SUBSCRIBE_COLLISION_EVENTS);
        });
    }
    // Return 'true' if the simulator wants collision events
    public override bool SubscribedEvents() {
        return (SubscribedEventsMs > 0);
    }

    #endregion // Collisions

    #region Per Simulation Step actions
    // There are some actions that must be performed for a physical object before each simulation step.
    // These actions are optional so, rather than scanning all the physical objects and asking them
    //     if they have anything to do, a physical object registers for an event call before the step is performed.
    // This bookkeeping makes it easy to add, remove and clean up after all these registrations.
    private Dictionary<string, BSScene.PreStepAction> RegisteredActions = new Dictionary<string, BSScene.PreStepAction>();
    protected void RegisterPreStepAction(string op, uint id, BSScene.PreStepAction actn)
    {
        string identifier = op + "-" + id.ToString();

        lock (RegisteredActions)
        {
            // Clean out any existing action
            UnRegisterPreStepAction(op, id);

            RegisteredActions[identifier] = actn;
        }
        PhysicsScene.BeforeStep += actn;
        DetailLog("{0},BSPhysObject.RegisterPreStepAction,id={1}", LocalID, identifier);
    }

    // Unregister a pre step action. Safe to call if the action has not been registered.
    protected void UnRegisterPreStepAction(string op, uint id)
    {
        string identifier = op + "-" + id.ToString();
        bool removed = false;
        lock (RegisteredActions)
        {
            if (RegisteredActions.ContainsKey(identifier))
            {
                PhysicsScene.BeforeStep -= RegisteredActions[identifier];
                RegisteredActions.Remove(identifier);
                removed = true;
            }
        }
        DetailLog("{0},BSPhysObject.UnRegisterPreStepAction,id={1},removed={2}", LocalID, identifier, removed);
    }

    protected void UnRegisterAllPreStepActions()
    {
        lock (RegisteredActions)
        {
            foreach (KeyValuePair<string, BSScene.PreStepAction> kvp in RegisteredActions)
            {
                PhysicsScene.BeforeStep -= kvp.Value;
            }
            RegisteredActions.Clear();
        }
        DetailLog("{0},BSPhysObject.UnRegisterAllPreStepActions,", LocalID);
    }

    
    #endregion // Per Simulation Step actions

    // High performance detailed logging routine used by the physical objects.
    protected void DetailLog(string msg, params Object[] args)
    {
        if (PhysicsScene.PhysicsLogging.Enabled)
            PhysicsScene.DetailLog(msg, args);
    }

}
}
