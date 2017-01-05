/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
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
using Nini.Config;

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    public delegate void physicsCrash();

    public delegate void RaycastCallback(bool hitYN, Vector3 collisionPoint, uint localid, float distance, Vector3 normal);
    public delegate void RayCallback(List<ContactResult> list);
    public delegate void ProbeBoxCallback(List<ContactResult> list);
    public delegate void ProbeSphereCallback(List<ContactResult> list);
    public delegate void ProbePlaneCallback(List<ContactResult> list);
    public delegate void SitAvatarCallback(int status, uint partID, Vector3 offset, Quaternion Orientation);

    public delegate void JointMoved(PhysicsJoint joint);
    public delegate void JointDeactivated(PhysicsJoint joint);
    public delegate void JointErrorMessage(PhysicsJoint joint, string message); // this refers to an "error message due to a problem", not "amount of joint constraint violation"

    public enum RayFilterFlags : ushort
    {
        // the flags
        water = 0x01,
        land = 0x02,
        agent = 0x04,
        nonphysical = 0x08,
        physical = 0x10,
        phantom = 0x20,
        volumedtc = 0x40,

        // ray cast colision control (may only work for meshs)
        ContactsUnImportant = 0x2000,
        BackFaceCull = 0x4000,
        ClosestHit = 0x8000,

        // some combinations
        LSLPhantom = phantom | volumedtc,
        PrimsNonPhantom = nonphysical | physical,
        PrimsNonPhantomAgents = nonphysical | physical | agent,

        AllPrims = nonphysical | phantom | volumedtc | physical,
        AllButLand = agent | nonphysical | physical | phantom | volumedtc,

        ClosestAndBackCull = ClosestHit | BackFaceCull,

        All = 0x3f
    }

    public delegate void RequestAssetDelegate(UUID assetID, AssetReceivedDelegate callback);
    public delegate void AssetReceivedDelegate(AssetBase asset);

    /// <summary>
    /// Contact result from a raycast.
    /// </summary>
    public struct ContactResult
    {
        public Vector3 Pos;
        public float Depth;
        public uint ConsumerID;
        public Vector3 Normal;
    }



    public abstract class PhysicsScene
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A unique identifying string for this instance of the physics engine.
        /// Useful in debug messages to distinguish one OdeScene instance from another.
        /// Usually set to include the region name that the physics engine is acting for.
        /// </summary>
        public string PhysicsSceneName { get; protected set; }

        /// <summary>
        /// A string identifying the family of this physics engine. Most common values returned
        /// are "OpenDynamicsEngine" and "BulletSim" but others are possible.
        /// </summary>
        public string EngineType { get; protected set; }

        public string EngineName { get; protected set; }

        // The only thing that should register for this event is the SceneGraph
        // Anything else could cause problems.
        public event physicsCrash OnPhysicsCrash;

        public static PhysicsScene Null
        {
            get { return new NullPhysicsScene(); }
        }

        public RequestAssetDelegate RequestAssetMethod { get; set; }

        protected void Initialise(RequestAssetDelegate m, float[] terrain, float waterHeight)
        {
            RequestAssetMethod = m;
            SetTerrain(terrain);
            SetWaterLevel(waterHeight);

        }

        public virtual void TriggerPhysicsBasedRestart()
        {
            physicsCrash handler = OnPhysicsCrash;
            if (handler != null)
            {
                OnPhysicsCrash();
            }
        }

        /// <summary>
        /// Add an avatar
        /// </summary>
        /// <param name="avName"></param>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        /// <param name="size"></param>
        /// <param name="isFlying"></param>
        /// <returns></returns>

        public abstract PhysicsActor AddAvatar(
            string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying);

        /// <summary>
        /// Add an avatar
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="avName"></param>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        /// <param name="size"></param>
        /// <param name="isFlying"></param>
        /// <returns></returns>
        public virtual PhysicsActor AddAvatar(
            uint localID, string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            PhysicsActor ret = AddAvatar(avName, position, velocity, size, isFlying);

            if (ret != null)
                ret.LocalID = localID;

            return ret;
        }

        public virtual PhysicsActor AddAvatar(
            uint localID, string avName, Vector3 position, Vector3 size, bool isFlying)
        {
            PhysicsActor ret = AddAvatar(localID, avName, position, Vector3.Zero, size, isFlying);
            return ret;
        }

        public virtual PhysicsActor AddAvatar(
            uint localID, string avName, Vector3 position, Vector3 size, float feetOffset, bool isFlying)
        {
            PhysicsActor ret = AddAvatar(localID, avName, position, Vector3.Zero, size, isFlying);
            return ret;
        }

        /// <summary>
        /// Remove an avatar.
        /// </summary>
        /// <param name="actor"></param>
        public abstract void RemoveAvatar(PhysicsActor actor);

        /// <summary>
        /// Remove a prim.
        /// </summary>
        /// <param name="prim"></param>
        public abstract void RemovePrim(PhysicsActor prim);

        public abstract PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, uint localid);

        public virtual PhysicsActor AddPrimShape(string primName, PhysicsActor parent, PrimitiveBaseShape pbs, Vector3 position,
                                                  uint localid, byte[] sdata)
        {
            return null;
        }

        public virtual PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, bool isPhantom, uint localid)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, isPhysical, localid);
        }


        public virtual PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, bool isPhantom, byte shapetype, uint localid)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, isPhysical, localid);
        }

        public virtual float TimeDilation
        {
            get { return 1.0f; }
        }

        public virtual bool SupportsNINJAJoints
        {
            get { return false; }
        }

        public virtual PhysicsJoint RequestJointCreation(string objectNameInScene, PhysicsJointType jointType, Vector3 position,
                                            Quaternion rotation, string parms, List<string> bodyNames, string trackedBodyName, Quaternion localRotation)
        { return null; }

        public virtual void RequestJointDeletion(string objectNameInScene)
        { return; }

        public virtual void RemoveAllJointsConnectedToActorThreadLocked(PhysicsActor actor)
        { return; }

        public virtual void DumpJointInfo()
        { return; }

        public event JointMoved OnJointMoved;

        protected virtual void DoJointMoved(PhysicsJoint joint)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointMoved != null)
            {
                OnJointMoved(joint);
            }
        }

        public event JointDeactivated OnJointDeactivated;

        protected virtual void DoJointDeactivated(PhysicsJoint joint)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointDeactivated != null)
            {
                OnJointDeactivated(joint);
            }
        }

        public event JointErrorMessage OnJointErrorMessage;

        protected virtual void DoJointErrorMessage(PhysicsJoint joint, string message)
        {
            // We need this to allow subclasses (but not other classes) to invoke the event; C# does
            // not allow subclasses to invoke the parent class event.
            if (OnJointErrorMessage != null)
            {
                OnJointErrorMessage(joint, message);
            }
        }

        public virtual Vector3 GetJointAnchor(PhysicsJoint joint)
        { return Vector3.Zero; }

        public virtual Vector3 GetJointAxis(PhysicsJoint joint)
        { return Vector3.Zero; }

        public abstract void AddPhysicsActorTaint(PhysicsActor prim);


        public virtual void ProcessPreSimulation() { }

        /// <summary>
        /// Perform a simulation of the current physics scene over the given timestep.
        /// </summary>
        /// <param name="timeStep"></param>
        /// <returns>The number of frames simulated over that period.</returns>
        public abstract float Simulate(float timeStep);

        /// <summary>
        /// Get statistics about this scene.
        /// </summary>
        /// <remarks>This facility is currently experimental and subject to change.</remarks>
        /// <returns>
        /// A dictionary where the key is the statistic name.  If no statistics are supplied then returns null.
        /// </returns>
        public virtual Dictionary<string, float> GetStats() { return null; }

        public abstract void GetResults();

        public abstract void SetTerrain(float[] heightMap);

        public abstract void SetWaterLevel(float baseheight);

        public abstract void DeleteTerrain();

        public abstract void Dispose();

        public abstract Dictionary<uint, float> GetTopColliders();

        public abstract bool IsThreaded { get; }

        /// <summary>
        /// True if the physics plugin supports raycasting against the physics scene
        /// </summary>
        public virtual bool SupportsRayCast()
        {
            return false;
        }

        /// <summary>
        /// Queue a raycast against the physics scene.
        /// The provided callback method will be called when the raycast is complete
        ///
        /// Many physics engines don't support collision testing at the same time as
        /// manipulating the physics scene, so we queue the request up and callback
        /// a custom method when the raycast is complete.
        /// This allows physics engines that give an immediate result to callback immediately
        /// and ones that don't, to callback when it gets a result back.
        ///
        /// ODE for example will not allow you to change the scene while collision testing or
        /// it asserts, 'opteration not valid for locked space'.  This includes adding a ray to the scene.
        ///
        /// This is named RayCastWorld to not conflict with modrex's Raycast method.
        /// </summary>
        /// <param name="position">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="length">Length of ray in meters</param>
        /// <param name="retMethod">Method to call when the raycast is complete</param>
        public virtual void RaycastWorld(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            if (retMethod != null)
                retMethod(false, Vector3.Zero, 0, 999999999999f, Vector3.Zero);
        }

        public virtual void RaycastWorld(Vector3 position, Vector3 direction, float length, int Count, RayCallback retMethod)
        {
            if (retMethod != null)
                retMethod(new List<ContactResult>());
        }

        public virtual List<ContactResult> RaycastWorld(Vector3 position, Vector3 direction, float length, int Count)
        {
            return new List<ContactResult>();
        }

        public virtual object RaycastWorld(Vector3 position, Vector3 direction, float length, int Count, RayFilterFlags filter)
        {
            return null;
        }

        public virtual bool SupportsRaycastWorldFiltered()
        {
            return false;
        }

        public virtual List<ContactResult> RaycastActor(PhysicsActor actor, Vector3 position, Vector3 direction, float length, int Count, RayFilterFlags flags)
        {
            return new List<ContactResult>();
        }

        public virtual List<ContactResult> BoxProbe(Vector3 position, Vector3 size, Quaternion orientation, int Count, RayFilterFlags flags)
        {
            return new List<ContactResult>();
        }

        public virtual List<ContactResult> SphereProbe(Vector3 position, float radius, int Count, RayFilterFlags flags)
        {
            return new List<ContactResult>();
        }

        public virtual List<ContactResult> PlaneProbe(PhysicsActor actor, Vector4 plane, int Count, RayFilterFlags flags)
        {
            return new List<ContactResult>();
        }

        public virtual int SitAvatar(PhysicsActor actor, Vector3 AbsolutePosition, Vector3 CameraPosition, Vector3 offset, Vector3 AvatarSize, SitAvatarCallback PhysicsSitResponse)
        {
            return 0;
        }

        // Extendable interface for new, physics engine specific operations
        public virtual object Extension(string pFunct, params object[] pParams)
        {
            // A NOP if the extension thing is not implemented by the physics engine
            return null;
        }
    }
}
