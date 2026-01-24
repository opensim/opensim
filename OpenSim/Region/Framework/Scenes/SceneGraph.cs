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
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using System.Runtime.InteropServices;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate void PhysicsCrash();
    public delegate void AttachToBackupDelegate(SceneObjectGroup sog);
    public delegate void DetachFromBackupDelegate(SceneObjectGroup sog);
    public delegate void ChangedBackupDelegate(SceneObjectGroup sog);

    /// <summary>
    /// This class used to be called InnerScene and may not yet truly be a SceneGraph.  The non scene graph components
    /// should be migrated out over time.
    /// </summary>
    public class SceneGraph
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Events

        protected internal event PhysicsCrash UnRecoverableError;
        public event AttachToBackupDelegate OnAttachToBackup;
        public event DetachFromBackupDelegate OnDetachFromBackup;
        public event ChangedBackupDelegate OnChangeBackup;

        #endregion

        #region Fields


        protected internal EntityManager Entities = new();

        private Dictionary<UUID, SceneObjectPart> m_scenePartsByID = new(1024);
        private Dictionary<uint, SceneObjectPart> m_scenePartsByLocalID = new(1024);
        private SceneObjectPart[] m_scenePartsArray;
        private Dictionary<UUID, ScenePresence> m_scenePresenceMap = new();
        private Dictionary<uint, ScenePresence> m_scenePresenceLocalIDMap = new();
        private Dictionary<UUID, SceneObjectGroup> m_updateList = new();
        private List<ScenePresence> m_scenePresenceList;

        private readonly Scene m_parentScene;
        private PhysicsScene _PhyScene;

        private int m_numRootAgents = 0;
        private int m_numChildAgents = 0;
        private int m_numRootNPC = 0;

        private int m_numPrim = 0;
        private int m_numMesh = 0;
        private int m_physicalPrim = 0;

        private int m_activeScripts = 0;
        //private int m_scriptLPS = 0;

        /// <summary>
        /// Lock to prevent object group update, linking, delinking and duplication operations from running concurrently.
        /// </summary>
        /// <remarks>
        /// These operations rely on the parts composition of the object.  If allowed to run concurrently then race
        /// conditions can occur.
        /// </remarks>
        private readonly Object m_updateLock = new();
        private readonly  Object m_linkLock = new();
        private readonly ReaderWriterLockSlim m_scenePresencesLock;
        private readonly ReaderWriterLockSlim m_scenePartsLock;

        #endregion

        protected internal SceneGraph(Scene parent)
        {
            m_scenePresencesLock = new ReaderWriterLockSlim();
            m_scenePartsLock = new ReaderWriterLockSlim();
            m_parentScene = parent;
            m_scenePresenceList = null;
            m_scenePartsArray = null;
        }

        ~SceneGraph()
        {
            m_scenePartsLock.Dispose();
            m_scenePresencesLock.Dispose();
        }

        public PhysicsScene PhysicsScene
        {
            get
            {
                _PhyScene ??= m_parentScene.RequestModuleInterface<PhysicsScene>();
                return _PhyScene;
            }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous
                // event handler
                if (_PhyScene is not null)
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;

                _PhyScene = value;

                if (_PhyScene is not null)
                    _PhyScene.OnPhysicsCrash += physicsBasedCrash;
            }
        }

        protected internal void Close()
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterWriteLock();
                    entered = true;
                }
                m_scenePresenceMap = new Dictionary<UUID, ScenePresence>();
                m_scenePresenceLocalIDMap = new Dictionary<uint, ScenePresence>();
                m_scenePresenceList = null;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitWriteLock();
            }

            entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterWriteLock();
                    entered = true;
                }

                Entities.Clear();
                m_scenePartsArray = null;
                m_scenePartsByID = new Dictionary<UUID, SceneObjectPart>();
                m_scenePartsByLocalID = new Dictionary<uint, SceneObjectPart>();
                if (_PhyScene is not null)
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;
                _PhyScene = null;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitWriteLock();
            }
        }

        #region Update Methods

        protected internal void UpdatePreparePhysics()
        {
        }

        /// <summary>
        /// Update the position of all the scene presences.
        /// </summary>
        /// <remarks>
        /// Called only from the main scene loop.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void UpdatePresences()
        {
            ForEachScenePresence(delegate(ScenePresence presence)
            {
                presence.Update();
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void UpdateScenePresenceMovement()
        {
            ForEachScenePresence(delegate (ScenePresence presence)
            {
                presence.UpdateMovement();
            });
        }

        /// <summary>
        /// Perform a physics frame update.
        /// </summary>
        /// <param name="elapsed"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal float UpdatePhysics(double elapsed)
        {
            return PhysicsScene is null ? 0 : PhysicsScene.Simulate((float)elapsed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void ProcessPhysicsPreSimulation()
        {
            PhysicsScene?.ProcessPreSimulation();
        }

        public void GetCoarseLocations(out List<Vector3> coarseLocations, out List<UUID> avatarUUIDs, int maxLocations)
        {
            coarseLocations = new List<Vector3>();
            avatarUUIDs = new List<UUID>();

            // coarse locations are sent as BYTE, so limited to the 255m max of normal regions
            // try to work around that scale down X and Y acording to region size, so reducing the resolution
            //
            // viewers need to scale up
            float scaleX = (float)m_parentScene.RegionInfo.RegionSizeX / (float)Constants.RegionSize;
            if (scaleX == 0)
                scaleX = 1.0f;
            else
                scaleX = 1.0f / scaleX;
            float scaleY = (float)m_parentScene.RegionInfo.RegionSizeY / (float)Constants.RegionSize;
            if (scaleY == 0)
                scaleY = 1.0f;
            else
                scaleY = 1.0f / scaleY;

            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence sp in CollectionsMarshal.AsSpan(presences))
            {
                // If this presence is a child agent, we don't want its coarse locations
                if (sp.IsChildAgent)
                    continue;
                Vector3 pos = sp.AbsolutePosition;
                pos.X *= scaleX;
                pos.Y *= scaleY;

                coarseLocations.Add(pos);
                avatarUUIDs.Add(sp.UUID);
                if (--maxLocations <= 0)
                    break;
            }
        }

        #endregion

        #region Entity Methods

        /// <summary>
        /// Add an object into the scene that has come from storage
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, changes to the object will be reflected in its persisted data
        /// If false, the persisted data will not be changed even if the object in the scene is changed
        /// </param>
        /// <param name="alreadyPersisted">
        /// If true, we won't persist this object until it changes
        /// If false, we'll persist this object immediately
        /// </param>
        /// <param name="sendClientUpdates">
        /// If true, we send updates to the client to tell it about this object
        /// If false, we leave it up to the caller to do this
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected internal bool AddRestoredSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, bool alreadyPersisted, bool sendClientUpdates)
        {
            // temporary checks to remove after varsize suport
            float regionSizeX = m_parentScene.RegionInfo.RegionSizeX;
            float regionSizeY = m_parentScene.RegionInfo.RegionSizeY;

            // KF: Check for out-of-region, move inside and make static.
            Vector3 npos = sceneObject.RootPart.GroupPosition;

            if (!((sceneObject.RootPart.Shape.PCode == (byte)PCode.Prim) && sceneObject.RootPart.Shape.State != 0))
            {
                bool clamped = false;
                if (npos.X < 0.0f)
                { 
                    npos.X = 1.0f;
                    clamped = true;
                }
                else if (npos.X > regionSizeX)
                {
                    npos.X = regionSizeX - 1.0f;
                    clamped = true;
                }
                if (npos.Y < 0.0f)
                {
                    npos.Y = 1.0f;
                    clamped = true;
                }
                else if (npos.Y > regionSizeY)
                {
                    npos.Y = regionSizeY - 1.0f;
                    clamped = true;
                }
                if (npos.Z < Constants.MinSimulationHeight)
                {
                    npos.Z = Constants.MinSimulationHeight;
                    clamped = true;
                }

                if(clamped)
                {
                    SceneObjectPart rootpart = sceneObject.RootPart;
                    rootpart.GroupPosition = npos;

                    foreach (SceneObjectPart part in sceneObject.Parts)
                    {
                        if (part == rootpart)
                            continue;
                        part.GroupPosition = npos;
                    }
                    rootpart.Velocity = Vector3.Zero;
                    rootpart.AngularVelocity = Vector3.Zero;
                    rootpart.Acceleration = Vector3.Zero;
                }
            }

            bool ret = AddSceneObject(sceneObject, attachToBackup, sendClientUpdates);

            if (attachToBackup && (!alreadyPersisted))
            {
                sceneObject.ForceInventoryPersistence();
                sceneObject.HasGroupChanged = true;
            }
            sceneObject.InvalidateDeepEffectivePerms();
            return ret;
        }

        /// <summary>
        /// Add a newly created object to the scene.  This will both update the scene, and send information about the
        /// new object to all clients interested in the scene.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected internal bool AddNewSceneObject(SceneObjectGroup sceneObject, bool attachToBackup, bool sendClientUpdates)
        {

            bool ret = AddSceneObject(sceneObject, attachToBackup, sendClientUpdates);

           // Ensure that we persist this new scene object if it's not an
            // attachment

            if (attachToBackup)
                sceneObject.HasGroupChanged = true;

            return ret;
        }

        /// <summary>
        /// Add a newly created object to the scene.
        /// </summary>
        ///
        /// This method does not send updates to the client - callers need to handle this themselves.
        /// Caller should also trigger EventManager.TriggerObjectAddedToScene
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup"></param>
        /// <param name="pos">Position of the object.  If null then the position stored in the object is used.</param>
        /// <param name="rot">Rotation of the object.  If null then the rotation stored in the object is used.</param>
        /// <param name="vel">Velocity of the object.  This parameter only has an effect if the object is physical</param>
        /// <returns></returns>
        public bool AddNewSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, Vector3? pos, Quaternion? rot, Vector3 vel)
        {
            if (pos is not null)
                sceneObject.AbsolutePosition = (Vector3)pos;

            if (rot is not null)
                sceneObject.UpdateGroupRotationR((Quaternion)rot);

            AddNewSceneObject(sceneObject, attachToBackup, false);

            if (sceneObject.RootPart.Shape.PCode == (byte)PCode.Prim)
            {
                sceneObject.ClearPartAttachmentData();
            }

            PhysicsActor pa = sceneObject.RootPart.PhysActor;
            if (pa is not null && pa.IsPhysical && vel.IsNotZero())
            {
                sceneObject.RootPart.ApplyImpulse(vel * sceneObject.GetMass(), false);
            }

            return true;
        }

        /// <summary>
        /// Add an object to the scene.  This will both update the scene, and send information about the
        /// new object to all clients interested in the scene.
        /// </summary>
        /// <remarks>
        /// The object's stored position, rotation and velocity are used.
        /// </remarks>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <param name="sendClientUpdates">
        /// If true, updates for the new scene object are sent to all viewers in range.
        /// If false, it is left to the caller to schedule the update
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        protected bool AddSceneObject(SceneObjectGroup sceneObject, bool attachToBackup, bool sendClientUpdates)
        {
            if (sceneObject is null)
            {
                m_log.Error("[SCENEGRAPH]: Tried to add null scene object");
                return false;
            }
            if (sceneObject.UUID.IsZero())
            {
                m_log.Error(
                    $"[SCENEGRAPH]: Tried to add scene object {sceneObject.Name} to {m_parentScene.RegionInfo.RegionName} with Zero UUID");
                return false;
            }

            if (Entities.ContainsKey(sceneObject.UUID))
            {
                m_log.Debug(
                    $"[SCENEGRAPH]: Scene graph for {m_parentScene.RegionInfo.RegionName} already contains object {sceneObject.UUID} in AddSceneObject()");
                return false;
            }

            //m_log.DebugFormat(
            //    "[SCENEGRAPH]: Adding scene object {0} {1}, with {2} parts on {3}",
            //    sceneObject.Name, sceneObject.UUID, sceneObject.Parts.Length, m_parentScene.RegionInfo.RegionName);

            ReadOnlySpan<SceneObjectPart> parts = sceneObject.Parts.AsSpan();
            SceneObjectPart part;

            // Clamp the sizes (scales) of the child prims and add the child prims to the count of all primitives
            // (meshes and geometric primitives) in the scene; add child prims to m_numTotalPrim count
            if (m_parentScene.m_clampPrimSize)
            {
                for (int i = 0; i < parts.Length; ++i)
                {
                    part = parts[i];
                    Vector3 scale = part.Shape.Scale;

                    scale.X = Utils.Clamp(scale.X, m_parentScene.m_minNonphys, m_parentScene.m_maxNonphys);
                    scale.Y = Utils.Clamp(scale.Y, m_parentScene.m_minNonphys, m_parentScene.m_maxNonphys);
                    scale.Z = Utils.Clamp(scale.Z, m_parentScene.m_minNonphys, m_parentScene.m_maxNonphys);

                    part.Shape.Scale = scale;
                }
            }

            sceneObject.AttachToScene(m_parentScene);

            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterWriteLock();
                    entered = true;
                }

                Entities.Add(sceneObject);
                m_scenePartsArray = null;
                for (int i = 0; i < parts.Length; ++i)
                {
                    part = parts[i];
                    if (!m_scenePartsByID.ContainsKey(part.UUID))
                    {
                        m_scenePartsByID[part.UUID] = part;
                        m_scenePartsByLocalID[part.LocalId] = part;
                        if (part.GetPrimType() == PrimType.SCULPT)
                            ++m_numMesh;
                        else
                            ++m_numPrim;
                    }
                }
            }
            finally
            {
                if(entered)
                    m_scenePartsLock.ExitWriteLock();
            }

            if (sendClientUpdates)
                sceneObject.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);

            if (attachToBackup)
                sceneObject.AttachToBackup();

            return true;
        }

        /// <summary>
        /// Delete an object from the scene
        /// </summary>
        /// <returns>true if the object was deleted, false if there was no object to delete</returns>
        public bool DeleteSceneObject(UUID uuid, bool resultOfObjectLinked)
        {
//            m_log.DebugFormat(
//                "[SCENE GRAPH]: Deleting scene object with uuid {0}, resultOfObjectLinked = {1}",
//                uuid, resultOfObjectLinked);

            if (!Entities.TryGetValue(uuid, out EntityBase entity) || (entity is not SceneObjectGroup grp))
                return false;

            SceneObjectPart[] parts = grp.Parts;
            int partsLength = parts.Length;
            SceneObjectPart part;

            if (!resultOfObjectLinked)
            {
                bool isPh = (grp.RootPart.Flags & PrimFlags.Physics) == PrimFlags.Physics;
                int nphysparts = 0;
                
                // Go through all parts (primitives and meshes) of this Scene Object
                for (int i= 0; i < partsLength; ++i)
                {
                    part = parts[i];
                    // Keep track of the total number of meshes or geometric primitives left in the scene;
                    // determine which object this is based on its primitive type: sculpted (sculpt) prim refers to
                    // a mesh and all other prims (i.e. box, sphere, etc) are geometric primitives
                    if (part.GetPrimType() == PrimType.SCULPT)
                        m_numMesh--;
                    else
                        m_numPrim--;

                    if(isPh && part.PhysicsShapeType != (byte)PhysShapeType.none)
                        nphysparts++;
                }

                if (nphysparts > 0 )
                    RemovePhysicalPrim(nphysparts);
            }

            bool ret = false;
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterWriteLock();
                    entered = true;
                }
                if (!resultOfObjectLinked)
                {
                    for (int i = 0; i < parts.Length; ++i)
                    {
                        part = parts[i];
                        m_scenePartsByID.Remove(part.UUID);
                        m_scenePartsByLocalID.Remove(part.LocalId);
                    }
                    m_scenePartsArray = null;
                }
                ret = Entities.Remove(uuid);
            }
            finally
            {
                if(entered)
                    m_scenePartsLock.ExitWriteLock();
            }
            return ret;
        }

        /// <summary>
        /// Add an object to the list of prims to process on the next update
        /// </summary>
        /// <param name="obj">
        /// A <see cref="SceneObjectGroup"/>
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void AddToUpdateList(SceneObjectGroup obj)
        {
            lock(m_updateLock)
                m_updateList[obj.UUID] = obj;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FireAttachToBackup(SceneObjectGroup obj)
        {
            OnAttachToBackup?.Invoke(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FireDetachFromBackup(SceneObjectGroup obj)
        {
            OnDetachFromBackup?.Invoke(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FireChangeBackup(SceneObjectGroup obj)
        {
            OnChangeBackup?.Invoke(obj);
        }

        /// <summary>
        /// Process all pending updates
        /// </summary>
        protected internal void UpdateObjectGroups()
        {
            Dictionary<UUID, SceneObjectGroup> updates;
            // Get the current list of updates and clear the list before iterating
            lock (m_updateLock)
            {
                if(m_updateList.Count == 0)
                    return;

                updates = m_updateList;
                m_updateList = new Dictionary<UUID, SceneObjectGroup>();
            }

            // Go through all updates
            foreach (SceneObjectGroup sog in updates.Values)
            {
                if (sog.IsDeleted)
                    continue;

                // Don't abort the whole update if one entity happens to give us an exception.
                try
                {
                    sog.Update();
                }
                catch (Exception e)
                {
                    m_log.Error($"[INNER SCENE]: Failed to update {sog.Name}, {sog.UUID} - {e.Message}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void AddPhysicalPrim(int number)
        {
            Interlocked.Add(ref m_physicalPrim, number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RemovePhysicalPrim(int number)
        {
            Interlocked.Add(ref m_physicalPrim, -number);
        }

        protected internal void AddToScriptLPS(int number)
        {
            //m_scriptLPS += number;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void AddActiveScripts(int number)
        {
            Interlocked.Add(ref m_activeScripts, number);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void HandleUndo(IClientAPI remoteClient, UUID primId)
        {
            if (primId.IsNotZero())
            {
                SceneObjectPart part =  m_parentScene.GetSceneObjectPart(primId);
                part?.Undo();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void HandleRedo(IClientAPI remoteClient, UUID primId)
        {
            if (primId.IsNotZero())
            {
                SceneObjectPart part = m_parentScene.GetSceneObjectPart(primId);
                part?.Redo();
            }
        }

        protected internal ScenePresence CreateAndAddChildScenePresence(
                IClientAPI client, AvatarAppearance appearance, PresenceType type)
        {
            ScenePresence presence = new(client, m_parentScene, appearance, type);

            bool entered = false;
            try
            {
                try{ }
                finally
                {
                    m_scenePresencesLock.EnterWriteLock();
                    entered = true;
                }

                UUID id = presence.UUID;
                Entities[id] = presence;
                // ScenePresence always defaults to child agent
                ++m_numChildAgents;

                uint localid = presence.LocalId;
                if (m_scenePresenceMap.TryGetValue(id, out ScenePresence oldref))
                {
                    uint oldLocalID = oldref.LocalId;
                    if (localid != oldLocalID)
                        m_scenePresenceLocalIDMap.Remove(oldLocalID);
                }
                m_scenePresenceMap[id] = presence;
                m_scenePresenceLocalIDMap[localid] = presence;
                m_scenePresenceList = null;
            }
            finally
            {
                if(entered)
                    m_scenePresencesLock.ExitWriteLock();
            }
            return presence;
        }

        /// <summary>
        /// Remove a presence from the scene
        /// </summary>
        protected internal void RemoveScenePresence(UUID agentID)
        {
            if (!Entities.Remove(agentID))
            {
                m_log.Warn($"[SCENE GRAPH]: Tried to remove non-existent scene presence with ID {agentID}");
            }

            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterWriteLock();
                    entered = true;
                }
                // Remove the presence reference from the dictionary
                if(m_scenePresenceMap.TryGetValue(agentID, out ScenePresence oldref))
                {
                    m_scenePresenceMap.Remove(agentID);
                    // Find the index in the list where the old ref was stored and remove the reference
                    m_scenePresenceLocalIDMap.Remove(oldref.LocalId);
                    m_scenePresenceList = null;
                    if(oldref.IsChildAgent)
                        --m_numChildAgents;
                    else
                    {
                        --m_numRootAgents;
                        if(oldref.IsNPC)
                            --m_numRootNPC;
                    }
                }
                else
                {
                    m_log.Warn($"[SCENE GRAPH]: Tried to remove non-existent scene presence with ID {agentID}");
                }
            }
            finally
            {
                if(entered)
                    m_scenePresencesLock.ExitWriteLock();
            }
        }

        protected internal void SwapRootChildAgent(bool direction_RootToChild, bool isnpc = false)
        {
            if (direction_RootToChild)
            {
                --m_numRootAgents;
                if(isnpc)
                    --m_numRootNPC;
                m_numChildAgents++;
            }
            else
            {
                --m_numChildAgents;
                ++m_numRootAgents;
                if (isnpc)
                    ++m_numRootNPC;
            }
        }

        public void removeUserCount(bool TypeRCTF)
        {
            if (TypeRCTF)
            {
                m_numRootAgents--;
            }
            else
            {
                m_numChildAgents--;
            }
        }

        public void RecalculateStats()
        {
            int rootcount = 0;
            int childcount = 0;
            int rootnpccount = 0;

            List<ScenePresence> presences = GetScenePresences();
            foreach(ScenePresence sp in CollectionsMarshal.AsSpan(presences))
            {
                if (sp.IsChildAgent)
                    ++childcount;
                else
                {
                    ++rootcount;
                    if(sp.IsNPC)
                        ++rootnpccount;
                }
            }

            m_numRootAgents = rootcount;
            m_numChildAgents = childcount;
            m_numRootNPC = rootnpccount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChildAgentCount()
        {
            return m_numChildAgents;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRootAgentCount()
        {
            return m_numRootAgents;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRootNPCCount()
        {
            return m_numRootNPC;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTotalObjectsCount()
        {
            return m_scenePartsByID.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTotalPrimObjectsCount()
        {
            return m_numPrim;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTotalMeshObjectsCount()
        {
            return m_numMesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetActiveObjectsCount()
        {
            return m_physicalPrim;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetActiveScriptsCount()
        {
            return m_activeScripts;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetScriptLPS()
        {
            //int returnval = m_scriptLPS;
            //m_scriptLPS = 0;
            //return returnval;
            return 0;
        }

        #endregion

        #region Get Methods

        /// <summary>
        /// Get the controlling client for the given avatar, if there is one.
        ///
        /// FIXME: The only user of the method right now is Caps.cs, in order to resolve a client API since it can't
        /// use the ScenePresence.  This could be better solved in a number of ways - we could establish an
        /// OpenSim.Framework.IScenePresence, or move the caps code into a region package (which might be the more
        /// suitable solution).
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns>null if either the avatar wasn't in the scene, or
        /// they do not have a controlling client</returns>
        /// <remarks>this used to be protected internal, but that
        /// prevents CapabilitiesModule from accessing it</remarks>
        public IClientAPI GetControllingClient(UUID agentId)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterReadLock();
                    entered = true;
                }
                if (m_scenePresenceMap.TryGetValue(agentId, out ScenePresence presence))
                    return presence.ControllingClient;
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get a reference to the scene presence list. Changes to the list will be done in a copy
        /// There is no guarantee that presences will remain in the scene after the list is returned.
        /// This list should remain private to SceneGraph. Callers wishing to iterate should instead
        /// pass a delegate to ForEachScenePresence.
        /// </summary>
        /// <returns></returns>
        protected internal List<ScenePresence> GetScenePresences()
        {
            bool entered = false;
            try
            {
                try{ }
                finally
                {
                    m_scenePresencesLock.EnterWriteLock();
                    entered = true;
                }

                m_scenePresenceList ??= new List<ScenePresence>(m_scenePresenceMap.Values);

                return m_scenePresenceList;
            }
            catch
            {
                return new List<ScenePresence>();
            }
            finally
            {
                if(entered)
                    m_scenePresencesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Request a scene presence by UUID. Fast, indexed lookup.
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns>null if the presence was not found</returns>
        protected internal ScenePresence GetScenePresence(UUID agentID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterReadLock();
                    entered = true;
                }
                if(m_scenePresenceMap.TryGetValue(agentID, out ScenePresence presence))
                    return presence;
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Request the scene presence by name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <returns>null if the presence was not found</returns>
        protected internal ScenePresence GetScenePresence(string firstName, string lastName)
        {
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence presence in CollectionsMarshal.AsSpan(presences))
            {
                if (string.Equals(presence.Firstname, firstName, StringComparison.CurrentCultureIgnoreCase)
                    && string.Equals(presence.Lastname, lastName, StringComparison.CurrentCultureIgnoreCase))
                    return presence;
            }
            return null;
        }

        /// <summary>
        /// Request the scene presence by localID.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the presence was not found</returns>
        protected internal ScenePresence GetScenePresence(uint localID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterReadLock();
                    entered = true;
                }
                if (m_scenePresenceLocalIDMap.TryGetValue(localID, out ScenePresence sp))
                    return sp;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitReadLock();
            }
            return null;
        }

        protected internal bool TryGetScenePresence(UUID agentID, out ScenePresence avatar)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePresenceMap.TryGetValue(agentID, out avatar);
            }
            catch
            {
                avatar = null;
                return false;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitReadLock();
            }
        }

        protected internal bool TryGetSceneRootPresence(UUID agentID, out ScenePresence avatar)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePresencesLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePresenceMap.TryGetValue(agentID, out avatar) && !avatar.IsChildAgent;
            }
            catch
            {
                avatar = null;
                return false;
            }
            finally
            {
                if (entered)
                    m_scenePresencesLock.ExitReadLock();
            }
        }


        protected internal bool TryGetAvatarByName(string name, out ScenePresence avatar)
        {
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence presence in CollectionsMarshal.AsSpan(presences))
            {
                if (string.Equals(name, presence.ControllingClient.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    avatar = presence;
                    return true;
                }
            }
            avatar = null;
            return false;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        public SceneObjectGroup GetGroupByPrim(uint localID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePartsByLocalID.TryGetValue(localID, out SceneObjectPart sop) ? sop.ParentGroup : null;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        public SceneObjectGroup GetGroupByPrim(UUID fullID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePartsByID.TryGetValue(fullID, out SceneObjectPart sop) ? sop.ParentGroup : null;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        protected internal EntityIntersection GetClosestIntersectingPrim(Ray hray, bool frontFacesOnly, bool faceCenters)
        {
            // Primitive Ray Tracing
            float closestDistance = 280f;
            EntityIntersection result = new();
            EntityBase[] EntityList = GetEntities();
            foreach (EntityBase ent in EntityList.AsSpan())
            {
                if (ent is SceneObjectGroup reportingG)
                {
                    EntityIntersection inter = reportingG.TestIntersection(hray, frontFacesOnly, faceCenters);
                    if (inter.HitTF && inter.distance < closestDistance)
                    {
                        closestDistance = inter.distance;
                        result = inter;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get all the scene object groups.
        /// </summary>
        /// <returns>
        /// The scene object groups.  If the scene is empty then an empty list is returned.
        /// </returns>
        protected internal List<SceneObjectGroup> GetSceneObjectGroups()
        {
            EntityBase[] entities = Entities.GetEntities();
            List<SceneObjectGroup> ret = new(entities.Length);

            foreach(EntityBase et in entities.AsSpan())
            {
                if(et is SceneObjectGroup sog)
                    ret.Add(sog);
            }
            return ret;
        }

        /// <summary>
        /// Get a group in the scene
        /// </summary>
        /// <param name="fullID">UUID of the group</param>
        /// <returns>null if no such group was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal SceneObjectGroup GetSceneObjectGroup(UUID fullID)
        {
            if (Entities.TryGetValue(fullID, out EntityBase entity) && (entity is SceneObjectGroup sog))
                return sog;
            return null;
        }

        /// <summary>
        /// Get a group in the scene
        /// <param name="localID">Local id of the root part of the group</param>
        /// <returns>null if no such group was found</retu
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal SceneObjectGroup GetSceneObjectGroup(uint localID)
        {
            if (Entities.TryGetValue(localID, out EntityBase entity) && (entity is SceneObjectGroup sog))
                return sog;
            return null;
        }

        /// <summary>
        /// Get a group by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectGroup GetSceneObjectGroup(string name)
        {
            foreach(EntityBase entity in Entities.GetEntities().AsSpan())
            {
                if (entity is SceneObjectGroup sog && sog.Name.Equals(name))
                    return sog;
            }
            return null;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(uint localID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                 return m_scenePartsByLocalID.TryGetValue(localID, out SceneObjectPart sop) &&
                                                                sop.ParentGroup is not null && !sop.ParentGroup.IsDeleted ? sop : null;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        protected internal bool TryGetSceneObjectPart(uint localID, out SceneObjectPart sop)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePartsByLocalID.TryGetValue(localID, out sop) && 
                                               sop.ParentGroup is not null &&
                                                !sop.ParentGroup.IsDeleted;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(UUID fullID)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePartsByID.TryGetValue(fullID, out SceneObjectPart sop) &&
                                                         sop.ParentGroup is not null && !sop.ParentGroup.IsDeleted ? sop : null;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        protected internal bool TryGetSceneObjectPart(UUID fullID, out SceneObjectPart sop)
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterReadLock();
                    entered = true;
                }
                return m_scenePartsByID.TryGetValue(fullID, out sop) && sop.ParentGroup is not null && !sop.ParentGroup.IsDeleted;
            }
            finally
            {
                if (entered)
                    m_scenePartsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get a prim by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(string name)
        {
            SceneObjectPart[] parts = GetPartsArray();
            foreach (SceneObjectPart sop in parts)
            {
                if (sop.ParentGroup is null || sop.ParentGroup.IsDeleted)
                    continue;
                if (sop.Name.Equals(name))
                    return sop;
            }
            return null;
        }

        protected internal SceneObjectPart[] GetPartsArray()
        {
            bool entered = false;
            try
            {
                try { }
                finally
                {
                    m_scenePartsLock.EnterWriteLock();
                    entered = true;
                }
                if(m_scenePartsArray is null)
                {
                    m_scenePartsArray = new SceneObjectPart[m_scenePartsByID.Count];
                    m_scenePartsByID.Values.CopyTo(m_scenePartsArray, 0);
                }
                return m_scenePartsArray;
            }
            finally
            {
                if(entered)
                    m_scenePartsLock.ExitWriteLock();
            }
        }
        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so no locking is required to iterate over
        /// it
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal EntityBase[] GetEntities()
        {
            return Entities.GetEntities();
        }

        #endregion

        #region Other Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void physicsBasedCrash()
        {
            UnRecoverableError?.Invoke();
        }

        /// <summary>
        /// Performs action once on all scene object groups.
        /// </summary>
        /// <param name="action"></param>
        protected internal void ForEachSOG(Action<SceneObjectGroup> action)
        {
            EntityBase[] entities = Entities.GetEntities();
            foreach (EntityBase entity in entities.AsSpan())
            {
                if (entity is SceneObjectGroup sog)
                {
                    try
                    {
                        action(sog);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn($"[SCENEGRAPH]: Problem processing action in ForEachSOG: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Performs action on all ROOT (not child) scene presences.
        /// This is just a shortcut function since frequently actions only appy to root SPs
        /// </summary>
        /// <param name="action"></param>
        public void ForEachRootScenePresence(Action<ScenePresence> action)
        {
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence sp in CollectionsMarshal.AsSpan(presences))
            {
                if(sp.IsChildAgent || sp.IsDeleted)
                    continue;

                try
                {
                    action(sp);
                }
                catch (Exception e)
                {
                    m_log.Error($"[SCENEGRAPH]: Error in {m_parentScene.RegionInfo.RegionName}: {e.Message}");
                }
            };
        }

        /// <summary>
        /// Performs action on all scene presences
        /// </summary>
        /// <param name="action"></param>
        public void ForEachScenePresence(Action<ScenePresence> action)
        {
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence sp in CollectionsMarshal.AsSpan(presences))
            {
                if (sp.IsDeleted)
                    continue;
                try
                {
                    action(sp);
                }
                catch (Exception e)
                {
                    m_log.Error($"[SCENEGRAPH]: Error in {m_parentScene.RegionInfo.RegionName}: {e.Message}");
                }
            }
        }

        #endregion

        #region Client Event handlers

        protected internal void ClientChangeObject(uint localID, object odata, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if(part is null)
                return;
            SceneObjectGroup grp = part.ParentGroup;
            if (grp is null)
            return;

            ObjectChangeData data = (ObjectChangeData)odata;

            if (m_parentScene.Permissions.CanEditObject(grp, remoteClient))
            {
                // These two are exceptions SL makes in the interpretation
                // of the change flags. Must check them here because otherwise
                // the group flag (see below) would be lost
                if (data.change == ObjectChangeType.groupS)
                    data.change = ObjectChangeType.primS;
                if (data.change == ObjectChangeType.groupPS)
                    data.change = ObjectChangeType.primPS;
                part.StoreUndoState(data.change); // lets test only saving what we changed
                grp.doChangeObject(part, data);
            }
            else
            {
                // Is this any kind of group operation?
                if ((data.change & ObjectChangeType.Group) != 0)
                {
                    // Is a move and/or rotation requested?
                    if ((data.change & (ObjectChangeType.Position | ObjectChangeType.Rotation)) != 0)
                    {
                        // Are we allowed to move it?
                        if (m_parentScene.Permissions.CanMoveObject(grp, remoteClient))
                        {
                            // Strip all but move and rotation from request
                            data.change &= (ObjectChangeType.Group | ObjectChangeType.Position | ObjectChangeType.Rotation);

                            part.StoreUndoState(data.change);
                            grp.doChangeObject(part, data);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the scale of an individual prim.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimScale(uint localID, in Vector3 scale, IClientAPI remoteClient)
        {
            if(TryGetSceneObjectPart(localID, out SceneObjectPart part))
            {
                if (m_parentScene.Permissions.CanEditObject(part.ParentGroup, remoteClient))
                {
                    bool physbuild = false;
                    if (part.ParentGroup.RootPart.PhysActor is not null)
                    {
                        part.ParentGroup.RootPart.PhysActor.Building = true;
                        physbuild = true;
                    }

                    part.Resize(scale);

                    if (physbuild)
                        part.ParentGroup.RootPart.PhysActor.Building = false;
                }
            }
        }

        protected internal void UpdatePrimGroupScale(uint localID, in Vector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
                {
                    bool physbuild = false;
                    if (group.RootPart.PhysActor is not null)
                    {
                        group.RootPart.PhysActor.Building = true;
                        physbuild = true;
                    }

                    group.GroupResize(scale);

                    if (physbuild)
                        group.RootPart.PhysActor.Building = false;
                }
            }
        }

        /// <summary>
        /// This handles the nifty little tool tip that you get when you drag your mouse over an object
        /// Send to the Object Group to process.  We don't know enough to service the request
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="AgentID"></param>
        /// <param name="RequestFlags"></param>
        /// <param name="ObjectID"></param>
        protected internal void RequestObjectPropertiesFamily(
             IClientAPI remoteClient, UUID AgentID, uint RequestFlags, UUID ObjectID)
        {
            SceneObjectGroup group = GetGroupByPrim(ObjectID);
            group?.ServiceObjectPropertiesFamilyRequest(remoteClient, AgentID, RequestFlags);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSingleRotation(uint localID, in Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group, remoteClient))
                {
                    group.UpdateSingleRotation(rot, localID);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSingleRotationPosition(uint localID, in Quaternion rot, in Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group, remoteClient))
                {
                    group.UpdateSingleRotation(rot, pos, localID);
                }
            }
        }

        /// <summary>
        /// Update the rotation of a whole group.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimGroupRotation(uint localID, in Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group, remoteClient))
                {
                    group.UpdateGroupRotationR(rot);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimGroupRotation(uint localID, in Vector3 pos, in Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group, remoteClient))
                {
                    group.UpdateGroupRotationPR(pos, rot);
                }
            }
        }

        /// <summary>
        /// Update the position of the given part
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSinglePosition(uint localID, in Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group, remoteClient) || group.IsAttachment)
                {
                    group.UpdateSinglePosition(pos, localID);
                }
            }
        }

        /// <summary>
        /// Update the position of the given group.
        /// </summary>
        /// <param name="localId"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimGroupPosition(uint localId, in Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localId);
            if (group is not null)
            {
                if (group.IsAttachment || (group.RootPart.Shape.PCode == 9 && group.RootPart.Shape.State != 0))
                {
                    // Set the new attachment point data in the object
                    byte attachmentPoint = (byte)group.AttachmentPoint;
                    group.UpdateGroupPosition(pos);
                    group.IsAttachment = false;
                    group.AbsolutePosition = group.RootPart.AttachedPos;
                    group.AttachmentPoint = attachmentPoint;
                    group.HasGroupChanged = true;
                }
                else
                {
                    if (m_parentScene.Permissions.CanMoveObject(group, remoteClient)
                        && m_parentScene.Permissions.CanObjectEntry(group, false, pos))
                    {
                        group.UpdateGroupPosition(pos);
                    }
                }
            }
        }

        /// <summary>
        /// Update the texture entry of the given prim.
        /// </summary>
        /// <remarks>
        /// A texture entry is an object that contains details of all the textures of the prim's face.  In this case,
        /// the texture is given in its byte serialized form.
        /// </remarks>
        /// <param name="localID"></param>
        /// <param name="texture"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if(part is null)
                return;

            SceneObjectGroup group = part.ParentGroup;
            if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
            {
                part.UpdateTextureEntry(texture);
            }
        }

        /// <summary>
        /// Update the flags on a scene object.  This covers properties such as phantom, physics and temporary.
        /// </summary>
        /// <remarks>
        /// This is currently handling the incoming call from the client stack (e.g. LLClientView).
        /// </remarks>
        /// <param name="localID"></param>
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimFlags(
            uint localID, bool UsePhysics, bool SetTemporary, bool SetPhantom, in ExtraPhysicsData PhysData, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if(part is null)
                return;
            SceneObjectGroup group = part.ParentGroup;

            if (!m_parentScene.Permissions.CanEditObject(group, remoteClient))
                return;

            // VolumeDetect can't be set via UI and will always be off when a change is made there
            // now only change volume dtc if phantom off

            bool wantedPhys = UsePhysics;
            if (PhysData.PhysShapeType == PhysShapeType.invalid) // check for extraPhysics data
            {
                bool vdtc;
                if (SetPhantom) // if phantom keep volumedtc
                    vdtc = group.RootPart.VolumeDetectActive;
                else // else turn it off
                    vdtc = false;

                group.UpdateFlags(UsePhysics, SetTemporary, SetPhantom, vdtc);
            }
            else
            {
                part.UpdateExtraPhysics(PhysData);
                remoteClient?.SendPartPhysicsProprieties(part);
            }

            if (wantedPhys != group.UsesPhysics && remoteClient is not null)
            {
                if(m_parentScene.m_linksetPhysCapacity != 0)
                    remoteClient.SendAlertMessage("Object physics cancelled because it exceeds limits for physical prims, either size or number of primswith shape type not set to None");
                else
                    remoteClient.SendAlertMessage("Object physics cancelled because it exceeds size limits for physical prims");
                        
                group.RootPart.ScheduleFullUpdate();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        protected internal void PrimName(IClientAPI remoteClient, uint primLocalID, string name)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
                {
                    group.SetPartName(Util.CleanString(name), primLocalID);
                    group.HasGroupChanged = true;
                }
            }
        }

        /// <summary>
        /// Handle a prim description set request from a viewer.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        protected internal void PrimDescription(IClientAPI remoteClient, uint primLocalID, string description)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
                {
                    group.SetPartDescription(Util.CleanString(description), primLocalID);
                    group.HasGroupChanged = true;
                }
            }
        }

        /// <summary>
        /// Set a click action for the prim.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        /// <param name="clickAction"></param>
        protected internal void PrimClickAction(IClientAPI remoteClient, uint primLocalID, string clickAction)
        {
            //m_log.DebugFormat(
            //    "[SCENEGRAPH]: User {0} set click action for {1} to {2}", remoteClient.Name, primLocalID, clickAction);

            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
                {
                    SceneObjectPart part = group.GetPart(primLocalID);
                    if (part is not null)
                    {
                        part.ClickAction = Convert.ToByte(clickAction);
                        group.HasGroupChanged = true;
                    }
                }
            }
        }

        protected internal void PrimMaterial(IClientAPI remoteClient, uint primLocalID, string material)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group, remoteClient))
                {
                    SceneObjectPart part = group.GetPart(primLocalID);
                    if (part is not null)
                    {
                        part.Material = Convert.ToByte(material);
                        group.HasGroupChanged = true;
                        remoteClient.SendPartPhysicsProprieties(part);
                    }
                }
            }
        }

        protected internal void UpdateExtraParam(UUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, agentID))
                {
                    group.UpdateExtraParam(primLocalID, type, inUse, data);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        protected internal void UpdatePrimShape(UUID agentID, uint primLocalID, UpdateShapeArgs shapeBlock)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group is not null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, agentID))
                {
                    ObjectShapePacket.ObjectDataBlock shapeData = new()
                    {
                        ObjectLocalID = shapeBlock.ObjectLocalID,
                        PathBegin = shapeBlock.PathBegin,
                        PathCurve = shapeBlock.PathCurve,
                        PathEnd = shapeBlock.PathEnd,
                        PathRadiusOffset = shapeBlock.PathRadiusOffset,
                        PathRevolutions = shapeBlock.PathRevolutions,
                        PathScaleX = shapeBlock.PathScaleX,
                        PathScaleY = shapeBlock.PathScaleY,
                        PathShearX = shapeBlock.PathShearX,
                        PathShearY = shapeBlock.PathShearY,
                        PathSkew = shapeBlock.PathSkew,
                        PathTaperX = shapeBlock.PathTaperX,
                        PathTaperY = shapeBlock.PathTaperY,
                        PathTwist = shapeBlock.PathTwist,
                        PathTwistBegin = shapeBlock.PathTwistBegin,
                        ProfileBegin = shapeBlock.ProfileBegin,
                        ProfileCurve = shapeBlock.ProfileCurve,
                        ProfileEnd = shapeBlock.ProfileEnd,
                        ProfileHollow = shapeBlock.ProfileHollow
                    };
                    group.UpdateShape(shapeData, primLocalID);
                }
            }
        }

        /// <summary>
        /// Initial method invoked when we receive a link objects request from the client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="parentPrim"></param>
        /// <param name="childPrims"></param>
        protected internal void LinkObjects(SceneObjectPart root, List<SceneObjectPart> children)
        {
            if (root.KeyframeMotion is not null)
            {
                root.KeyframeMotion.Stop();
                root.KeyframeMotion = null;
            }

            SceneObjectGroup parentGroup = root.ParentGroup;
            if (parentGroup is null) return;

            // Cowardly refuse to link to a group owned root
            if (parentGroup.OwnerID == parentGroup.GroupID)
                return;

            Monitor.Enter(m_linkLock);

            try
            {
                List<SceneObjectGroup> childGroups = new();

                // We do this in reverse to get the link order of the prims correct
                foreach (SceneObjectPart childpart in CollectionsMarshal.AsSpan(children))
                {
                    SceneObjectGroup child = childpart.ParentGroup;
                    // Don't try and add a group to itself - this will only cause severe problems later on.
                    if (child == parentGroup)
                        continue;

                    // Make sure no child prim is set for sale
                    // So that, on delink, no prims are unwittingly
                    // left for sale and sold off

                    if (child is not null)
                    {
                        child.RootPart.ObjectSaleType = 0;
                        child.RootPart.SalePrice = 10;
                        childGroups.Add(child);
                    }
                }

                foreach (SceneObjectGroup child in CollectionsMarshal.AsSpan(childGroups))
                {
                    if (parentGroup.OwnerID == child.OwnerID)
                    {
                        child.DetachFromBackup();
                        parentGroup.LinkToGroup(child);

                        // this is here so physics gets updated!
                        // Don't remove!  Bad juju!  Stay away! or fix physics!
                        // already done in LinkToGroup
//                        child.AbsolutePosition = child.AbsolutePosition;
                    }
                }

                // We need to explicitly resend the newly link prim's object properties since no other actions
                // occur on link to invoke this elsewhere (such as object selection)
                if (childGroups.Count > 0)
                {
                    //parentGroup.RootPart.CreateSelected = true;
                    parentGroup.TriggerScriptChangedEvent(Changed.LINK);
                }
            }
            finally
            {
/*
                lock (SceneObjectGroupsByLocalPartID)
                {
                    foreach (SceneObjectPart part in parentGroup.Parts)
                        SceneObjectGroupsByLocalPartID[part.LocalId] = parentGroup;
                }
*/
                parentGroup.AdjustChildPrimPermissions(false);
                parentGroup.HasGroupChanged = true;
                parentGroup.ScheduleGroupForFullAnimUpdate();
                Monitor.Exit(m_linkLock);
            }
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>
        protected internal void DelinkObjects(List<SceneObjectPart> prims)
        {
            List<SceneObjectPart> childParts = new();
            List<SceneObjectPart> rootParts = new();
            List<SceneObjectGroup> affectedGroups = new();
            // Look them all up in one go, since that is comparatively expensive
            //
            Monitor.Enter(m_linkLock);
            try
            {
                foreach (SceneObjectPart part in CollectionsMarshal.AsSpan(prims))
                {
                    if(part is null)
                        continue;
                    SceneObjectGroup parentSOG = part.ParentGroup;
                    if(parentSOG is null || parentSOG.IsDeleted || parentSOG.inTransit || parentSOG.PrimCount == 1)
                        continue;

                    if (!affectedGroups.Contains(parentSOG))
                    {
                        affectedGroups.Add(parentSOG);
                        if(parentSOG.RootPart.PhysActor is not null)
                            parentSOG.RootPart.PhysActor.Building = true;
                    }

                    if (part.KeyframeMotion is not null)
                    {
                        part.KeyframeMotion.Stop();
                        part.KeyframeMotion = null;
                    }

                    if (part.LinkNum < 2) // Root
                    {
                        rootParts.Add(part);
                    }
                    else
                    {
                        part.LastOwnerID = part.ParentGroup.RootPart.LastOwnerID;
                        part.RezzerID = part.ParentGroup.RootPart.RezzerID;
                        childParts.Add(part);
                    }
                }

                if (childParts.Count > 0)
                {
                    foreach (SceneObjectPart child in CollectionsMarshal.AsSpan(childParts))
                    {
                        // Unlink all child parts from their groups
                        child.ParentGroup.DelinkFromGroup(child, true);
                        //child.ParentGroup is now other
                        child.ParentGroup.HasGroupChanged = true;
                        child.ParentGroup.ScheduleGroupForFullAnimUpdate();
                    }
                }

                foreach (SceneObjectPart root in CollectionsMarshal.AsSpan(rootParts))
                {
                    // In most cases, this will run only one time, and the prim
                    // will be a solo prim
                    // However, editing linked parts and unlinking may be different
                    //
                    SceneObjectGroup group = root.ParentGroup;

                    List<SceneObjectPart> newSet = new(group.Parts);

                    newSet.Remove(root);
                    int numChildren = newSet.Count;
                    if(numChildren == 0)
                        break;

                    foreach (SceneObjectPart p in newSet)
                        group.DelinkFromGroup(p, false);

                    SceneObjectPart newRoot = newSet[0];

                    // If there is more than one prim remaining, we
                    // need to re-link
                    //
                    if (numChildren > 1)
                    {
                        // Determine new root
                        //
                        newSet.RemoveAt(0);
                        foreach (SceneObjectPart newChild in CollectionsMarshal.AsSpan(newSet))
                            newChild.ClearUpdateSchedule();

                        LinkObjects(newRoot, newSet);
                    }
                    else
                    {
                        newRoot.TriggerScriptChangedEvent(Changed.LINK);
                        newRoot.ParentGroup.HasGroupChanged = true;
                        newRoot.ParentGroup.InvalidatePartsLinkMaps();
                        newRoot.ParentGroup.ScheduleGroupForFullAnimUpdate();
                    }
                }

                // trigger events in the roots
                //
                foreach (SceneObjectGroup g in CollectionsMarshal.AsSpan(affectedGroups))
                {
                    if(g.RootPart.PhysActor is not null)
                        g.RootPart.PhysActor.Building = false;
                    g.AdjustChildPrimPermissions(false);
                    // Child prims that have been unlinked and deleted will
                    // return unless the root is deleted. This will remove them
                    // from the database. They will be rewritten immediately,
                    // minus the rows for the unlinked child prims.
                    m_parentScene.SimulationDataService.RemoveObject(g.UUID, m_parentScene.RegionInfo.RegionID);
                    g.SetPartsInventoryChanged(); // so we also need to force inventory save
                    g.InvalidatePartsLinkMaps();
                    g.TriggerScriptChangedEvent(Changed.LINK);
                    g.HasGroupChanged = true; // Persist
                    g.ScheduleGroupForFullUpdate();
                }
            }
            finally
            {
                Monitor.Exit(m_linkLock);
            }
        }

        protected internal void MakeObjectSearchable(IClientAPI remoteClient, bool IncludeInSearch, uint localID)
        {
            SceneObjectGroup sog = GetGroupByPrim(localID);
            if(sog is null)
                return;

            //Protip: In my day, we didn't call them searchable objects, we called them limited point-to-point joints
            //aka ObjectFlags.JointWheel = IncludeInSearch

            //Permissions model: Object can be REMOVED from search IFF:
            // * User owns object
            //use CanEditObject

            //Object can be ADDED to search IFF:
            // * User owns object
            // * Asset/DRM permission bit "modify" is enabled
            //use CanEditObjectPosition

            // libomv will complain about PrimFlags.JointWheel being
            // deprecated, so we
            if (IncludeInSearch && m_parentScene.Permissions.CanEditObject(sog, remoteClient))
            {
                sog.RootPart.AddFlag(PrimFlags.JointWheel);
                sog.HasGroupChanged = true;
            }
            else if (!IncludeInSearch && m_parentScene.Permissions.CanMoveObject(sog, remoteClient))
            {
                sog.RootPart.RemFlag(PrimFlags.JointWheel);
                sog.HasGroupChanged = true;
            }
        }

        /// <summary>
        /// Duplicate the given object.
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        /// <param name="AgentID"></param>
        /// <param name="GroupID"></param>
        /// <param name="rot"></param>
        /// <returns>null if duplication fails, otherwise the duplicated object</returns>
        /// <summary>
        public SceneObjectGroup DuplicateObject(uint originalPrimID, Vector3 offset, UUID AgentID, UUID GroupID, Quaternion rot, bool createSelected)
        {
//            m_log.DebugFormat(
//                "[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}",
//                originalPrimID, offset, AgentID);

            SceneObjectGroup original = GetGroupByPrim(originalPrimID);
            if (original is not null)
            {
                if (m_parentScene.Permissions.CanDuplicateObject(original, AgentID))
                {
                    SceneObjectGroup copy = original.Copy(true);
                    copy.AbsolutePosition += offset;

                    copy.RootPart.Rezzed = DateTime.UtcNow;
                    copy.RootPart.RezzerID = AgentID;

                    ReadOnlySpan<SceneObjectPart> parts = copy.Parts.AsSpan();

                    if (original.OwnerID.NotEqual(AgentID))
                    {
                        copy.SetOwner(AgentID, GroupID);

                        if (m_parentScene.Permissions.PropagatePermissions())
                        {
                            foreach (SceneObjectPart child in parts)
                            {
                                child.Inventory.ChangeInventoryOwner(AgentID);
                                child.TriggerScriptChangedEvent(Changed.OWNER);
                                child.ApplyNextOwnerPermissions();
                            }
                            copy.InvalidateEffectivePerms();
                        }
                    }

                    bool entered = false;
                    try
                    {
                        try { }
                        finally
                        {
                            m_scenePartsLock.EnterWriteLock();
                            entered = true;
                        }

                        Entities.Add(copy);
                        m_scenePartsArray = null;
                        foreach (SceneObjectPart part in parts)
                        {
                            if (!m_scenePartsByID.ContainsKey(part.UUID))
                            {
                                if (part.GetPrimType() == PrimType.SCULPT)
                                    m_numMesh++;
                                else
                                    m_numPrim++;

                                m_scenePartsByID[part.UUID] = part;
                                m_scenePartsByLocalID[part.LocalId] = part;
                            }
                        }
                    }
                    finally
                    {
                        if(entered)
                            m_scenePartsLock.ExitWriteLock();
                    }

                    copy.IsSelected = createSelected;

                    if (rot != Quaternion.Identity)
                        copy.UpdateGroupRotationR(rot);

                    // required for physics to update it's position
                    copy.ResetChildPrimPhysicsPositions();

                    copy.CreateScriptInstances(0, false, m_parentScene.DefaultScriptEngine, 1);
                    copy.ResumeScripts();

                    copy.HasGroupChanged = true;
                    copy.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);
                    return copy;
                }
            }
            else
            {
                m_log.Warn($"[SCENE]: Attempted to duplicate nonexistant prim id {GroupID}");
            }

            return null;
        }

        #endregion

    }
}
