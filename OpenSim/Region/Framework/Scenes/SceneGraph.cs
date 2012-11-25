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
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes.Types;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate void PhysicsCrash();

    /// <summary>
    /// This class used to be called InnerScene and may not yet truly be a SceneGraph.  The non scene graph components
    /// should be migrated out over time.
    /// </summary>
    public class SceneGraph
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Events

        protected internal event PhysicsCrash UnRecoverableError;
        private PhysicsCrash handlerPhysicsCrash = null;

        #endregion

        #region Fields

        protected object m_presenceLock = new object();
        protected Dictionary<UUID, ScenePresence> m_scenePresenceMap = new Dictionary<UUID, ScenePresence>();
        protected List<ScenePresence> m_scenePresenceArray = new List<ScenePresence>();

        protected internal EntityManager Entities = new EntityManager();

        protected Scene m_parentScene;
        protected Dictionary<UUID, SceneObjectGroup> m_updateList = new Dictionary<UUID, SceneObjectGroup>();
        protected int m_numRootAgents = 0;
        protected int m_numPrim = 0;
        protected int m_numChildAgents = 0;
        protected int m_physicalPrim = 0;

        protected int m_activeScripts = 0;
        protected int m_scriptLPS = 0;

        protected internal PhysicsScene _PhyScene;
        
        /// <summary>
        /// Index the SceneObjectGroup for each part by the root part's UUID.
        /// </summary>
        protected internal Dictionary<UUID, SceneObjectGroup> SceneObjectGroupsByFullID = new Dictionary<UUID, SceneObjectGroup>();
        
        /// <summary>
        /// Index the SceneObjectGroup for each part by that part's UUID.
        /// </summary>
        protected internal Dictionary<UUID, SceneObjectGroup> SceneObjectGroupsByFullPartID = new Dictionary<UUID, SceneObjectGroup>();
        
        /// <summary>
        /// Index the SceneObjectGroup for each part by that part's local ID.
        /// </summary>
        protected internal Dictionary<uint, SceneObjectGroup> SceneObjectGroupsByLocalPartID = new Dictionary<uint, SceneObjectGroup>();        

        /// <summary>
        /// Lock to prevent object group update, linking, delinking and duplication operations from running concurrently.
        /// </summary>
        /// <remarks>
        /// These operations rely on the parts composition of the object.  If allowed to run concurrently then race
        /// conditions can occur.
        /// </remarks>
        private Object m_updateLock = new Object();

        #endregion

        protected internal SceneGraph(Scene parent)
        {
            m_parentScene = parent;
        }

        public PhysicsScene PhysicsScene
        {
            get { return _PhyScene; }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous
                // event handler
                if (_PhyScene != null)
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;

                _PhyScene = value;

                if (_PhyScene != null)
                    _PhyScene.OnPhysicsCrash += physicsBasedCrash;
            }
        }

        protected internal void Close()
        {
            lock (m_presenceLock)
            {
                Dictionary<UUID, ScenePresence> newmap = new Dictionary<UUID, ScenePresence>();
                List<ScenePresence> newlist = new List<ScenePresence>();
                m_scenePresenceMap = newmap;
                m_scenePresenceArray = newlist;
            }

            lock (SceneObjectGroupsByFullID)
                SceneObjectGroupsByFullID.Clear();
            lock (SceneObjectGroupsByFullPartID)
                SceneObjectGroupsByFullPartID.Clear();
            lock (SceneObjectGroupsByLocalPartID)
                SceneObjectGroupsByLocalPartID.Clear();

            Entities.Clear();
        }

        #region Update Methods

        protected internal void UpdatePreparePhysics()
        {
            // If we are using a threaded physics engine
            // grab the latest scene from the engine before
            // trying to process it.

            // PhysX does this (runs in the background).

            if (_PhyScene.IsThreaded)
            {
                _PhyScene.GetResults();
            }
        }

        /// <summary>
        /// Update the position of all the scene presences.
        /// </summary>
        /// <remarks>
        /// Called only from the main scene loop.
        /// </remarks>
        protected internal void UpdatePresences()
        {
            ForEachScenePresence(delegate(ScenePresence presence)
            {
                presence.Update();
            });
        }

        /// <summary>
        /// Perform a physics frame update.
        /// </summary>
        /// <param name="elapsed"></param>
        /// <returns></returns>
        protected internal float UpdatePhysics(double elapsed)
        {
            // Here is where the Scene calls the PhysicsScene. This is a one-way
            // interaction; the PhysicsScene cannot access the calling Scene directly.
            // But with joints, we want a PhysicsActor to be able to influence a
            // non-physics SceneObjectPart. In particular, a PhysicsActor that is connected
            // with a joint should be able to move the SceneObjectPart which is the visual
            // representation of that joint (for editing and serialization purposes).
            // However the PhysicsActor normally cannot directly influence anything outside
            // of the PhysicsScene, and the non-physical SceneObjectPart which represents
            // the joint in the Scene does not exist in the PhysicsScene.
            //
            // To solve this, we have an event in the PhysicsScene that is fired when a joint
            // has changed position (because one of its associated PhysicsActors has changed 
            // position).
            //
            // Therefore, JointMoved and JointDeactivated events will be fired as a result of the following Simulate().
            return _PhyScene.Simulate((float)elapsed);
        }

        protected internal void UpdateScenePresenceMovement()
        {
            ForEachScenePresence(delegate(ScenePresence presence)
            {
                presence.UpdateMovement();
            });
        }

        public void GetCoarseLocations(out List<Vector3> coarseLocations, out List<UUID> avatarUUIDs, uint maxLocations)
        {
            coarseLocations = new List<Vector3>();
            avatarUUIDs = new List<UUID>();

            List<ScenePresence> presences = GetScenePresences();
            for (int i = 0; i < Math.Min(presences.Count, maxLocations); ++i)
            {
                ScenePresence sp = presences[i];
                
                // If this presence is a child agent, we don't want its coarse locations
                if (sp.IsChildAgent)
                    continue;

                coarseLocations.Add(sp.AbsolutePosition);

                avatarUUIDs.Add(sp.UUID);
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
            if (attachToBackup && (!alreadyPersisted))
            {
                sceneObject.ForceInventoryPersistence();
                sceneObject.HasGroupChanged = true;
            }

            return AddSceneObject(sceneObject, attachToBackup, sendClientUpdates);
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
            // Ensure that we persist this new scene object if it's not an
            // attachment
            if (attachToBackup)
                sceneObject.HasGroupChanged = true;

            return AddSceneObject(sceneObject, attachToBackup, sendClientUpdates);
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
            AddNewSceneObject(sceneObject, attachToBackup, false);

            if (pos != null)
                sceneObject.AbsolutePosition = (Vector3)pos;

            if (sceneObject.RootPart.Shape.PCode == (byte)PCode.Prim)
            {
                sceneObject.ClearPartAttachmentData();
            }

            if (rot != null)
                sceneObject.UpdateGroupRotationR((Quaternion)rot);

            PhysicsActor pa = sceneObject.RootPart.PhysActor;
            if (pa != null && pa.IsPhysical && vel != Vector3.Zero)
            {
                sceneObject.RootPart.ApplyImpulse((vel * sceneObject.GetMass()), false);
                sceneObject.Velocity = vel;
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
            if (sceneObject.UUID == UUID.Zero)
            {
                m_log.ErrorFormat(
                    "[SCENEGRAPH]: Tried to add scene object {0} to {1} with illegal UUID of {2}",
                    sceneObject.Name, m_parentScene.RegionInfo.RegionName, UUID.Zero);

                return false;
            }

            if (Entities.ContainsKey(sceneObject.UUID))
            {
                m_log.DebugFormat(
                    "[SCENEGRAPH]: Scene graph for {0} already contains object {1} in AddSceneObject()",
                    m_parentScene.RegionInfo.RegionName, sceneObject.UUID);

                return false;
            }

//            m_log.DebugFormat(
//                "[SCENEGRAPH]: Adding scene object {0} {1}, with {2} parts on {3}",
//                sceneObject.Name, sceneObject.UUID, sceneObject.Parts.Length, m_parentScene.RegionInfo.RegionName);

            SceneObjectPart[] parts = sceneObject.Parts;

            // Clamp child prim sizes and add child prims to the m_numPrim count
            if (m_parentScene.m_clampPrimSize)
            {
                foreach (SceneObjectPart part in parts)
                {
                    Vector3 scale = part.Shape.Scale;

                    scale.X = Math.Max(m_parentScene.m_minNonphys, Math.Min(m_parentScene.m_maxNonphys, scale.X));
                    scale.Y = Math.Max(m_parentScene.m_minNonphys, Math.Min(m_parentScene.m_maxNonphys, scale.Y));
                    scale.Z = Math.Max(m_parentScene.m_minNonphys, Math.Min(m_parentScene.m_maxNonphys, scale.Z));

                    part.Shape.Scale = scale;
                }
            }
            m_numPrim += parts.Length;

            sceneObject.AttachToScene(m_parentScene);

            if (sendClientUpdates)
                sceneObject.ScheduleGroupForFullUpdate();

            Entities.Add(sceneObject);

            if (attachToBackup)
                sceneObject.AttachToBackup();

            lock (SceneObjectGroupsByFullID)
                SceneObjectGroupsByFullID[sceneObject.UUID] = sceneObject;

            lock (SceneObjectGroupsByFullPartID)
            {
                foreach (SceneObjectPart part in parts)
                    SceneObjectGroupsByFullPartID[part.UUID] = sceneObject;
            }

            lock (SceneObjectGroupsByLocalPartID)
            {
//                m_log.DebugFormat(
//                    "[SCENE GRAPH]: Adding scene object {0} {1} {2} to SceneObjectGroupsByLocalPartID in {3}",
//                    sceneObject.Name, sceneObject.UUID, sceneObject.LocalId, m_parentScene.RegionInfo.RegionName);

                foreach (SceneObjectPart part in parts)
                    SceneObjectGroupsByLocalPartID[part.LocalId] = sceneObject;
            }

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

            EntityBase entity;
            if (!Entities.TryGetValue(uuid, out entity) || (!(entity is SceneObjectGroup)))
                return false;

            SceneObjectGroup grp = (SceneObjectGroup)entity;

            if (entity == null)
                return false;

            if (!resultOfObjectLinked)
            {
                m_numPrim -= grp.PrimCount;

                if ((grp.RootPart.Flags & PrimFlags.Physics) == PrimFlags.Physics)
                    RemovePhysicalPrim(grp.PrimCount);
            }
            
            lock (SceneObjectGroupsByFullID)
                SceneObjectGroupsByFullID.Remove(grp.UUID);

            lock (SceneObjectGroupsByFullPartID)
            {
                SceneObjectPart[] parts = grp.Parts;
                for (int i = 0; i < parts.Length; i++)
                    SceneObjectGroupsByFullPartID.Remove(parts[i].UUID);
            }

            lock (SceneObjectGroupsByLocalPartID)
            {
                SceneObjectPart[] parts = grp.Parts;
                for (int i = 0; i < parts.Length; i++)
                    SceneObjectGroupsByLocalPartID.Remove(parts[i].LocalId);
            }

            return Entities.Remove(uuid);
        }

        /// <summary>
        /// Add an object to the list of prims to process on the next update
        /// </summary>
        /// <param name="obj">
        /// A <see cref="SceneObjectGroup"/>
        /// </param>
        protected internal void AddToUpdateList(SceneObjectGroup obj)
        {
            lock (m_updateList)
                m_updateList[obj.UUID] = obj;
        }

        /// <summary>
        /// Process all pending updates
        /// </summary>
        protected internal void UpdateObjectGroups()
        {
            if (!Monitor.TryEnter(m_updateLock))
                return;
            try
            {
                List<SceneObjectGroup> updates;

                // Some updates add more updates to the updateList. 
                // Get the current list of updates and clear the list before iterating
                lock (m_updateList)
                {
                    updates = new List<SceneObjectGroup>(m_updateList.Values);
                    m_updateList.Clear();
                }

                // Go through all updates
                for (int i = 0; i < updates.Count; i++)
                {
                    SceneObjectGroup sog = updates[i];

                    // Don't abort the whole update if one entity happens to give us an exception.
                    try
                    {
                        sog.Update();
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[INNER SCENE]: Failed to update {0}, {1} - {2}", sog.Name, sog.UUID, e);
                    }
                }
            }
            finally
            {
                Monitor.Exit(m_updateLock);
            }
        }

        protected internal void AddPhysicalPrim(int number)
        {
            m_physicalPrim++;
        }

        protected internal void RemovePhysicalPrim(int number)
        {
            m_physicalPrim--;
        }

        protected internal void AddToScriptLPS(int number)
        {
            m_scriptLPS += number;
        }

        protected internal void AddActiveScripts(int number)
        {
            m_activeScripts += number;
        }

        protected internal void HandleUndo(IClientAPI remoteClient, UUID primId)
        {
            if (primId != UUID.Zero)
            {
                SceneObjectPart part =  m_parentScene.GetSceneObjectPart(primId);
                if (part != null)
                    part.Undo();
            }
        }

        protected internal void HandleRedo(IClientAPI remoteClient, UUID primId)
        {
            if (primId != UUID.Zero)
            {
                SceneObjectPart part = m_parentScene.GetSceneObjectPart(primId);

                if (part != null)
                    part.Redo();
            }
        }

        protected internal ScenePresence CreateAndAddChildScenePresence(
            IClientAPI client, AvatarAppearance appearance, PresenceType type)
        {
            ScenePresence newAvatar = null;

            // ScenePresence always defaults to child agent
            newAvatar = new ScenePresence(client, m_parentScene, appearance, type);

            AddScenePresence(newAvatar);

            return newAvatar;
        }

        /// <summary>
        /// Add a presence to the scene
        /// </summary>
        /// <param name="presence"></param>
        protected internal void AddScenePresence(ScenePresence presence)
        {
            // Always a child when added to the scene
            bool child = presence.IsChildAgent;

            if (child)
            {
                m_numChildAgents++;
            }
            else
            {
                m_numRootAgents++;
                presence.AddToPhysicalScene(false);
            }

            Entities[presence.UUID] = presence;

            lock (m_presenceLock)
            {
                Dictionary<UUID, ScenePresence> newmap = new Dictionary<UUID, ScenePresence>(m_scenePresenceMap);
                List<ScenePresence> newlist = new List<ScenePresence>(m_scenePresenceArray);

                if (!newmap.ContainsKey(presence.UUID))
                {
                    newmap.Add(presence.UUID, presence);
                    newlist.Add(presence);
                }
                else
                {
                    // Remember the old presene reference from the dictionary
                    ScenePresence oldref = newmap[presence.UUID];
                    // Replace the presence reference in the dictionary with the new value
                    newmap[presence.UUID] = presence;
                    // Find the index in the list where the old ref was stored and update the reference
                    newlist[newlist.IndexOf(oldref)] = presence;
                }

                // Swap out the dictionary and list with new references
                m_scenePresenceMap = newmap;
                m_scenePresenceArray = newlist;
            }
        }

        /// <summary>
        /// Remove a presence from the scene
        /// </summary>
        protected internal void RemoveScenePresence(UUID agentID)
        {
            if (!Entities.Remove(agentID))
            {
                m_log.WarnFormat(
                    "[SCENE GRAPH]: Tried to remove non-existent scene presence with agent ID {0} from scene Entities list",
                    agentID);
            }

            lock (m_presenceLock)
            {
                Dictionary<UUID, ScenePresence> newmap = new Dictionary<UUID, ScenePresence>(m_scenePresenceMap);
                List<ScenePresence> newlist = new List<ScenePresence>(m_scenePresenceArray);
                
                // Remove the presence reference from the dictionary
                if (newmap.ContainsKey(agentID))
                {
                    ScenePresence oldref = newmap[agentID];
                    newmap.Remove(agentID);

                    // Find the index in the list where the old ref was stored and remove the reference
                    newlist.RemoveAt(newlist.IndexOf(oldref));
                    // Swap out the dictionary and list with new references
                    m_scenePresenceMap = newmap;
                    m_scenePresenceArray = newlist;
                }
                else
                {
                    m_log.WarnFormat("[SCENE GRAPH]: Tried to remove non-existent scene presence with agent ID {0} from scene ScenePresences list", agentID);
                }
            }
        }

        protected internal void SwapRootChildAgent(bool direction_RC_CR_T_F)
        {
            if (direction_RC_CR_T_F)
            {
                m_numRootAgents--;
                m_numChildAgents++;
            }
            else
            {
                m_numChildAgents--;
                m_numRootAgents++;
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

            ForEachScenePresence(delegate(ScenePresence presence)
            {
                if (presence.IsChildAgent)
                    ++childcount;
                else
                    ++rootcount;
            });

            m_numRootAgents = rootcount;
            m_numChildAgents = childcount;
        }

        public int GetChildAgentCount()
        {
            return m_numChildAgents;
        }

        public int GetRootAgentCount()
        {
            return m_numRootAgents;
        }

        public int GetTotalObjectsCount()
        {
            return m_numPrim;
        }

        public int GetActiveObjectsCount()
        {
            return m_physicalPrim;
        }

        public int GetActiveScriptsCount()
        {
            return m_activeScripts;
        }

        public int GetScriptLPS()
        {
            int returnval = m_scriptLPS;
            m_scriptLPS = 0;
            return returnval;
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
            ScenePresence presence = GetScenePresence(agentId);

            if (presence != null)
            {
                return presence.ControllingClient;
            }

            return null;
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
            return m_scenePresenceArray;
        }

        /// <summary>
        /// Request a scene presence by UUID. Fast, indexed lookup.
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns>null if the presence was not found</returns>
        protected internal ScenePresence GetScenePresence(UUID agentID)
        {
            Dictionary<UUID, ScenePresence> presences = m_scenePresenceMap;
            ScenePresence presence;
            presences.TryGetValue(agentID, out presence);
            return presence;
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
            foreach (ScenePresence presence in presences)
            {
                if (presence.Firstname == firstName && presence.Lastname == lastName)
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
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence presence in presences)
                if (presence.LocalId == localID)
                    return presence;
            return null;
        }

        protected internal bool TryGetScenePresence(UUID agentID, out ScenePresence avatar)
        {
            Dictionary<UUID, ScenePresence> presences = m_scenePresenceMap;
            presences.TryGetValue(agentID, out avatar);
            return (avatar != null);
        }

        protected internal bool TryGetAvatarByName(string name, out ScenePresence avatar)
        {
            avatar = null;
            foreach (ScenePresence presence in GetScenePresences())
            {
                if (String.Compare(name, presence.ControllingClient.Name, true) == 0)
                {
                    avatar = presence;
                    break;
                }
            }
            return (avatar != null);
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        public SceneObjectGroup GetGroupByPrim(uint localID)
        {
            EntityBase entity;
            if (Entities.TryGetValue(localID, out entity))
                return entity as SceneObjectGroup;

//            m_log.DebugFormat("[SCENE GRAPH]: Entered GetGroupByPrim with localID {0}", localID);

            SceneObjectGroup sog;
            lock (SceneObjectGroupsByLocalPartID)
                SceneObjectGroupsByLocalPartID.TryGetValue(localID, out sog);

            if (sog != null)
            {
                if (sog.ContainsPart(localID))
                {
//                    m_log.DebugFormat(
//                        "[SCENE GRAPH]: Found scene object {0} {1} {2} containing part with local id {3} in {4}.  Returning.",
//                        sog.Name, sog.UUID, sog.LocalId, localID, m_parentScene.RegionInfo.RegionName);

                    return sog;
                }
                else
                {
                    lock (SceneObjectGroupsByLocalPartID)
                    {
                        m_log.WarnFormat(
                            "[SCENE GRAPH]: Found scene object {0} {1} {2} via SceneObjectGroupsByLocalPartID index but it doesn't contain part with local id {3}.  Removing from entry from index in {4}.",
                            sog.Name, sog.UUID, sog.LocalId, localID, m_parentScene.RegionInfo.RegionName);

                        SceneObjectGroupsByLocalPartID.Remove(localID);
                    }
                }
            }

            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                //m_log.DebugFormat("Looking at entity {0}", ent.UUID);
                if (ent is SceneObjectGroup)
                {
                    sog = (SceneObjectGroup)ent;
                    if (sog.ContainsPart(localID))
                    {
                        lock (SceneObjectGroupsByLocalPartID)
                            SceneObjectGroupsByLocalPartID[localID] = sog;
                        return sog;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        public SceneObjectGroup GetGroupByPrim(UUID fullID)
        {
            SceneObjectGroup sog;
            lock (SceneObjectGroupsByFullPartID)
                SceneObjectGroupsByFullPartID.TryGetValue(fullID, out sog);

            if (sog != null)
            {
                if (sog.ContainsPart(fullID))
                    return sog;

                lock (SceneObjectGroupsByFullPartID)
                    SceneObjectGroupsByFullPartID.Remove(fullID);
            }

            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    sog = (SceneObjectGroup)ent;
                    if (sog.ContainsPart(fullID))
                    {
                        lock (SceneObjectGroupsByFullPartID)
                            SceneObjectGroupsByFullPartID[fullID] = sog;
                        return sog;
                    }
                }
            }

            return null;
        }

        protected internal EntityIntersection GetClosestIntersectingPrim(Ray hray, bool frontFacesOnly, bool faceCenters)
        {
            // Primitive Ray Tracing
            float closestDistance = 280f;
            EntityIntersection result = new EntityIntersection();
            EntityBase[] EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup reportingG = (SceneObjectGroup)ent;
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
            lock (SceneObjectGroupsByFullID)
                return new List<SceneObjectGroup>(SceneObjectGroupsByFullID.Values);
        }

        /// <summary>
        /// Get a group in the scene
        /// </summary>
        /// <param name="fullID">UUID of the group</param>
        /// <returns>null if no such group was found</returns>
        protected internal SceneObjectGroup GetSceneObjectGroup(UUID fullID)
        {
            lock (SceneObjectGroupsByFullID)
            {
                if (SceneObjectGroupsByFullID.ContainsKey(fullID))
                    return SceneObjectGroupsByFullID[fullID];
            }

            return null;
        }

        /// <summary>
        /// Get a group in the scene
        /// </summary>
        /// <remarks>
        /// This will only return a group if the local ID matches the root part, not other parts.
        /// </remarks>
        /// <param name="localID">Local id of the root part of the group</param>
        /// <returns>null if no such group was found</returns>
        protected internal SceneObjectGroup GetSceneObjectGroup(uint localID)
        {
            lock (SceneObjectGroupsByLocalPartID)
            {
                if (SceneObjectGroupsByLocalPartID.ContainsKey(localID))
                {
                    SceneObjectGroup so = SceneObjectGroupsByLocalPartID[localID];

                    if (so.LocalId == localID)
                        return so;
                }
            }

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
            SceneObjectGroup so = null;

            Entities.Find(
                delegate(EntityBase entity)
                {
                    if (entity is SceneObjectGroup)
                    {
                        if (entity.Name == name)
                        {
                            so = (SceneObjectGroup)entity;
                            return true;
                        }
                    }

                    return false;
                }
            );

            return so;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group == null)
                return null;
            return group.GetPart(localID);
        }
        
        /// <summary>
        /// Get a prim by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(string name)
        {
            SceneObjectPart sop = null;

            Entities.Find(
                delegate(EntityBase entity)
                {
                    if (entity is SceneObjectGroup)
                    {
                        foreach (SceneObjectPart p in ((SceneObjectGroup)entity).Parts)
                        {
//                            m_log.DebugFormat("[SCENE GRAPH]: Part {0} has name {1}", p.UUID, p.Name);
                        
                            if (p.Name == name)
                            {
                                sop = p;
                                return true;
                            }
                        }
                    }

                    return false;
                }
            );

            return sop;
        }

        /// <summary>
        /// Get a part contained in this scene.
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if the part was not found</returns>
        protected internal SceneObjectPart GetSceneObjectPart(UUID fullID)
        {
            SceneObjectGroup group = GetGroupByPrim(fullID);
            if (group == null)
                return null;
            return group.GetPart(fullID);
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so no locking is required to iterate over
        /// it
        /// </summary>
        /// <returns></returns>
        protected internal EntityBase[] GetEntities()
        {
            return Entities.GetEntities();
        }

        #endregion

        #region Other Methods

        protected internal void physicsBasedCrash()
        {
            handlerPhysicsCrash = UnRecoverableError;
            if (handlerPhysicsCrash != null)
            {
                handlerPhysicsCrash();
            }
        }

        protected internal UUID ConvertLocalIDToFullID(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                return group.GetPartsFullID(localID);
            else
                return UUID.Zero;
        }

        /// <summary>
        /// Performs action once on all scene object groups.
        /// </summary>
        /// <param name="action"></param>
        protected internal void ForEachSOG(Action<SceneObjectGroup> action)
        {
            foreach (SceneObjectGroup obj in GetSceneObjectGroups())
            {
                try
                {
                    action(obj);
                }
                catch (Exception e)
                {
                    // Catch it and move on. This includes situations where objlist has inconsistent info
                    m_log.WarnFormat(
                        "[SCENEGRAPH]: Problem processing action in ForEachSOG: {0} {1}", e.Message, e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Performs action on all ROOT (not child) scene presences.
        /// This is just a shortcut function since frequently actions only appy to root SPs
        /// </summary>
        /// <param name="action"></param>
        public void ForEachAvatar(Action<ScenePresence> action)
        {
            ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (!sp.IsChildAgent)
                    action(sp);
            });
        }

        /// <summary>
        /// Performs action on all scene presences. This can ultimately run the actions in parallel but
        /// any delegates passed in will need to implement their own locking on data they reference and
        /// modify outside of the scope of the delegate. 
        /// </summary>
        /// <param name="action"></param>
        public void ForEachScenePresence(Action<ScenePresence> action)
        {
            // Once all callers have their delegates configured for parallelism, we can unleash this
            /*
            Action<ScenePresence> protectedAction = new Action<ScenePresence>(delegate(ScenePresence sp)
                {
                    try
                    {
                        action(sp);
                    }
                    catch (Exception e)
                    {
                        m_log.Info("[SCENEGRAPH]: Error in " + m_parentScene.RegionInfo.RegionName + ": " + e.ToString());
                        m_log.Info("[SCENEGRAPH]: Stack Trace: " + e.StackTrace);
                    }
                });
            Parallel.ForEach<ScenePresence>(GetScenePresences(), protectedAction);
            */
            // For now, perform actions serially
            List<ScenePresence> presences = GetScenePresences();
            foreach (ScenePresence sp in presences)
            {
                try
                {
                    action(sp);
                }
                catch (Exception e)
                {
                    m_log.Error("[SCENEGRAPH]: Error in " + m_parentScene.RegionInfo.RegionName + ": " + e.ToString());
                }
            }
        }
        
        #endregion

        #region Client Event handlers

        /// <summary>
        /// Update the scale of an individual prim.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);

            if (part != null)
            {
                if (m_parentScene.Permissions.CanEditObject(part.ParentGroup.UUID, remoteClient.AgentId))
                {
                    part.Resize(scale);
                }
            }
        }

        protected internal void UpdatePrimGroupScale(uint localID, Vector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    group.GroupResize(scale);
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
            if (group != null)
            {
                group.ServiceObjectPropertiesFamilyRequest(remoteClient, AgentID, RequestFlags);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        protected internal void UpdatePrimSingleRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
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
        protected internal void UpdatePrimSingleRotationPosition(uint localID, Quaternion rot, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
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
        protected internal void UpdatePrimGroupRotation(uint localID, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
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
        protected internal void UpdatePrimGroupRotation(uint localID, Vector3 pos, Quaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))
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
        protected internal void UpdatePrimSinglePosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId) || group.IsAttachment)
                {
                    group.UpdateSinglePosition(pos, localID);
                }
            }
        }

        /// <summary>
        /// Update the position of the given group.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimGroupPosition(uint localID, Vector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            
            if (group != null)
            {
                if (group.IsAttachment || (group.RootPart.Shape.PCode == 9 && group.RootPart.Shape.State != 0))
                {
                    if (m_parentScene.AttachmentsModule != null)
                        m_parentScene.AttachmentsModule.UpdateAttachmentPosition(group, pos);
                }
                else
                {
                    if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId) 
                        && m_parentScene.Permissions.CanObjectEntry(group.UUID, false, pos))
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
            SceneObjectGroup group = GetGroupByPrim(localID);
            
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID,remoteClient.AgentId))
                {
                    group.UpdateTextureEntry(localID, texture);
                }
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
            uint localID, bool UsePhysics, bool SetTemporary, bool SetPhantom, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    // VolumeDetect can't be set via UI and will always be off when a change is made there
                    group.UpdatePrimFlags(localID, UsePhysics, SetTemporary, SetPhantom, false);
                }
            }
        }

        /// <summary>
        /// Move the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        protected internal void MoveObject(UUID objectID, Vector3 offset, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.GrabMovement(offset, pos, remoteClient);
                }
                // This is outside the above permissions condition
                // so that if the object is locked the client moving the object
                // get's it's position on the simulator even if it was the same as before
                // This keeps the moving user's client in sync with the rest of the world.
                group.SendGroupTerseUpdate();
            }
        }

        /// <summary>
        /// Start spinning the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="rotation"></param>
        /// <param name="remoteClient"></param>
        protected internal void SpinStart(UUID objectID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.SpinStart(remoteClient);
                }
            }
        }

        /// <summary>
        /// Spin the given object
        /// </summary>
        /// <param name="objectID"></param>
        /// <param name="rotation"></param>
        /// <param name="remoteClient"></param>
        protected internal void SpinObject(UUID objectID, Quaternion rotation, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanMoveObject(group.UUID, remoteClient.AgentId))// && PermissionsMngr.)
                {
                    group.SpinMovement(rotation, remoteClient);
                }
                // This is outside the above permissions condition
                // so that if the object is locked the client moving the object
                // get's it's position on the simulator even if it was the same as before
                // This keeps the moving user's client in sync with the rest of the world.
                group.SendGroupTerseUpdate();
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
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
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
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
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
//            m_log.DebugFormat(
//                "[SCENEGRAPH]: User {0} set click action for {1} to {2}", remoteClient.Name, primLocalID, clickAction);

            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    SceneObjectPart part = m_parentScene.GetSceneObjectPart(primLocalID);
                    if (part != null)
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
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, remoteClient.AgentId))
                {
                    SceneObjectPart part = m_parentScene.GetSceneObjectPart(primLocalID);
                    if (part != null)
                    {
                        part.Material = Convert.ToByte(material);
                        group.HasGroupChanged = true;
                    }
                }
            }
        }

        protected internal void UpdateExtraParam(UUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);

            if (group != null)
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
            if (group != null)
            {
                if (m_parentScene.Permissions.CanEditObject(group.UUID, agentID))
                {
                    ObjectShapePacket.ObjectDataBlock shapeData = new ObjectShapePacket.ObjectDataBlock();
                    shapeData.ObjectLocalID = shapeBlock.ObjectLocalID;
                    shapeData.PathBegin = shapeBlock.PathBegin;
                    shapeData.PathCurve = shapeBlock.PathCurve;
                    shapeData.PathEnd = shapeBlock.PathEnd;
                    shapeData.PathRadiusOffset = shapeBlock.PathRadiusOffset;
                    shapeData.PathRevolutions = shapeBlock.PathRevolutions;
                    shapeData.PathScaleX = shapeBlock.PathScaleX;
                    shapeData.PathScaleY = shapeBlock.PathScaleY;
                    shapeData.PathShearX = shapeBlock.PathShearX;
                    shapeData.PathShearY = shapeBlock.PathShearY;
                    shapeData.PathSkew = shapeBlock.PathSkew;
                    shapeData.PathTaperX = shapeBlock.PathTaperX;
                    shapeData.PathTaperY = shapeBlock.PathTaperY;
                    shapeData.PathTwist = shapeBlock.PathTwist;
                    shapeData.PathTwistBegin = shapeBlock.PathTwistBegin;
                    shapeData.ProfileBegin = shapeBlock.ProfileBegin;
                    shapeData.ProfileCurve = shapeBlock.ProfileCurve;
                    shapeData.ProfileEnd = shapeBlock.ProfileEnd;
                    shapeData.ProfileHollow = shapeBlock.ProfileHollow;

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
            SceneObjectGroup parentGroup = root.ParentGroup;
            if (parentGroup == null) return;

            // Cowardly refuse to link to a group owned root
            if (parentGroup.OwnerID == parentGroup.GroupID)
                return;

            Monitor.Enter(m_updateLock);
            try
            {
                List<SceneObjectGroup> childGroups = new List<SceneObjectGroup>();

                // We do this in reverse to get the link order of the prims correct
                for (int i = 0 ; i < children.Count ; i++)
                {
                    SceneObjectGroup child = children[i].ParentGroup;

                    // Don't try and add a group to itself - this will only cause severe problems later on.
                    if (child == parentGroup)
                        continue;

                    // Make sure no child prim is set for sale
                    // So that, on delink, no prims are unwittingly
                    // left for sale and sold off
                    child.RootPart.ObjectSaleType = 0;
                    child.RootPart.SalePrice = 10;
                    childGroups.Add(child);
                }

                foreach (SceneObjectGroup child in childGroups)
                {
                    if (parentGroup.OwnerID == child.OwnerID)
                    {
                        parentGroup.LinkToGroup(child);

                        child.DetachFromBackup();

                        // this is here so physics gets updated!
                        // Don't remove!  Bad juju!  Stay away! or fix physics!
                        child.AbsolutePosition = child.AbsolutePosition;
                    }
                }

                // We need to explicitly resend the newly link prim's object properties since no other actions
                // occur on link to invoke this elsewhere (such as object selection)
                if (childGroups.Count > 0)
                {
                    parentGroup.RootPart.CreateSelected = true;
                    parentGroup.TriggerScriptChangedEvent(Changed.LINK);
                    parentGroup.HasGroupChanged = true;
                    parentGroup.ScheduleGroupForFullUpdate();
                }
            }
            finally
            {
                Monitor.Exit(m_updateLock);
            }
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>
        protected internal void DelinkObjects(List<SceneObjectPart> prims)
        {
            Monitor.Enter(m_updateLock);
            try
            {
                List<SceneObjectPart> childParts = new List<SceneObjectPart>();
                List<SceneObjectPart> rootParts = new List<SceneObjectPart>();
                List<SceneObjectGroup> affectedGroups = new List<SceneObjectGroup>();
                // Look them all up in one go, since that is comparatively expensive
                //
                foreach (SceneObjectPart part in prims)
                {
                    if (part != null)
                    {
                        if (part.ParentGroup.PrimCount != 1) // Skip single
                        {
                            if (part.LinkNum < 2) // Root
                            {
                                rootParts.Add(part);
                            }
                            else
                            {
                                part.LastOwnerID = part.ParentGroup.RootPart.LastOwnerID;
                                childParts.Add(part);
                            }

                            SceneObjectGroup group = part.ParentGroup;
                            if (!affectedGroups.Contains(group))
                                affectedGroups.Add(group);
                        }
                    }
                }

                foreach (SceneObjectPart child in childParts)
                {
                    // Unlink all child parts from their groups
                    //
                    child.ParentGroup.DelinkFromGroup(child, true);

                    // These are not in affected groups and will not be
                    // handled further. Do the honors here.
                    child.ParentGroup.HasGroupChanged = true;
                    child.ParentGroup.ScheduleGroupForFullUpdate();
                }

                foreach (SceneObjectPart root in rootParts)
                {
                    // In most cases, this will run only one time, and the prim
                    // will be a solo prim
                    // However, editing linked parts and unlinking may be different
                    //
                    SceneObjectGroup group = root.ParentGroup;
                    
                    List<SceneObjectPart> newSet = new List<SceneObjectPart>(group.Parts);
                    int numChildren = newSet.Count;

                    // If there are prims left in a link set, but the root is
                    // slated for unlink, we need to do this
                    //
                    if (numChildren != 1)
                    {
                        // Unlink the remaining set
                        //
                        bool sendEventsToRemainder = true;
                        if (numChildren > 1)
                            sendEventsToRemainder = false;

                        foreach (SceneObjectPart p in newSet)
                        {
                            if (p != group.RootPart)
                                group.DelinkFromGroup(p, sendEventsToRemainder);
                        }

                        // If there is more than one prim remaining, we
                        // need to re-link
                        //
                        if (numChildren > 2)
                        {
                            // Remove old root
                            //
                            if (newSet.Contains(root))
                                newSet.Remove(root);

                            // Preserve link ordering
                            //
                            newSet.Sort(delegate (SceneObjectPart a, SceneObjectPart b)
                            {
                                return a.LinkNum.CompareTo(b.LinkNum);
                            });

                            // Determine new root
                            //
                            SceneObjectPart newRoot = newSet[0];
                            newSet.RemoveAt(0);

                            foreach (SceneObjectPart newChild in newSet)
                                newChild.ClearUpdateSchedule();

                            LinkObjects(newRoot, newSet);
                            if (!affectedGroups.Contains(newRoot.ParentGroup))
                                affectedGroups.Add(newRoot.ParentGroup);
                        }
                    }
                }

                // Finally, trigger events in the roots
                //
                foreach (SceneObjectGroup g in affectedGroups)
                {
                    g.TriggerScriptChangedEvent(Changed.LINK);
                    g.HasGroupChanged = true; // Persist
                    g.ScheduleGroupForFullUpdate();
                }
            }
            finally
            {
                Monitor.Exit(m_updateLock);
            }
        }

        protected internal void MakeObjectSearchable(IClientAPI remoteClient, bool IncludeInSearch, uint localID)
        {
            UUID user = remoteClient.AgentId;
            UUID objid = UUID.Zero;
            SceneObjectPart obj = null;

            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup sog = ent as SceneObjectGroup;

                    foreach (SceneObjectPart part in sog.Parts)
                    {
                        if (part.LocalId == localID)
                        {
                            objid = part.UUID;
                            obj = part;
                        }
                    }
                }
            }

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
            #pragma warning disable 0612
            if (IncludeInSearch && m_parentScene.Permissions.CanEditObject(objid, user))
            {
                obj.ParentGroup.RootPart.AddFlag(PrimFlags.JointWheel);
                obj.ParentGroup.HasGroupChanged = true;
            }
            else if (!IncludeInSearch && m_parentScene.Permissions.CanMoveObject(objid,user))
            {
                obj.ParentGroup.RootPart.RemFlag(PrimFlags.JointWheel);
                obj.ParentGroup.HasGroupChanged = true;
            }
            #pragma warning restore 0612
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
        public SceneObjectGroup DuplicateObject(
            uint originalPrimID, Vector3 offset, uint flags, UUID AgentID, UUID GroupID, Quaternion rot)
        {
            Monitor.Enter(m_updateLock);

            try
            {
    //            m_log.DebugFormat(
    //                "[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}",
    //                originalPrimID, offset, AgentID);

                SceneObjectGroup original = GetGroupByPrim(originalPrimID);
                if (original == null)
                {
                    m_log.WarnFormat(
                        "[SCENEGRAPH]: Attempt to duplicate nonexistant prim id {0} by {1}", originalPrimID, AgentID);

                    return null;
                }

                if (!m_parentScene.Permissions.CanDuplicateObject(
                    original.PrimCount, original.UUID, AgentID, original.AbsolutePosition))
                    return null;

                SceneObjectGroup copy = original.Copy(true);
                copy.AbsolutePosition = copy.AbsolutePosition + offset;

                if (original.OwnerID != AgentID)
                {
                    copy.SetOwnerId(AgentID);
                    copy.SetRootPartOwner(copy.RootPart, AgentID, GroupID);

                    SceneObjectPart[] partList = copy.Parts;

                    if (m_parentScene.Permissions.PropagatePermissions())
                    {
                        foreach (SceneObjectPart child in partList)
                        {
                            child.Inventory.ChangeInventoryOwner(AgentID);
                            child.TriggerScriptChangedEvent(Changed.OWNER);
                            child.ApplyNextOwnerPermissions();
                        }
                    }

                    copy.RootPart.ObjectSaleType = 0;
                    copy.RootPart.SalePrice = 10;
                }

                // FIXME: This section needs to be refactored so that it just calls AddSceneObject()
                Entities.Add(copy);
                
                lock (SceneObjectGroupsByFullID)
                    SceneObjectGroupsByFullID[copy.UUID] = copy;
                
                SceneObjectPart[] children = copy.Parts;
                
                lock (SceneObjectGroupsByFullPartID)
                {
                    SceneObjectGroupsByFullPartID[copy.UUID] = copy;
                    foreach (SceneObjectPart part in children)
                        SceneObjectGroupsByFullPartID[part.UUID] = copy;
                }
    
                lock (SceneObjectGroupsByLocalPartID)
                {
                    SceneObjectGroupsByLocalPartID[copy.LocalId] = copy;
                    foreach (SceneObjectPart part in children)
                        SceneObjectGroupsByLocalPartID[part.LocalId] = copy;
                }   
                // PROBABLE END OF FIXME

                // Since we copy from a source group that is in selected
                // state, but the copy is shown deselected in the viewer,
                // We need to clear the selection flag here, else that
                // prim never gets persisted at all. The client doesn't
                // think it's selected, so it will never send a deselect...
                copy.IsSelected = false;

                m_numPrim += copy.Parts.Length;

                if (rot != Quaternion.Identity)
                {
                    copy.UpdateGroupRotationR(rot);
                }

                copy.CreateScriptInstances(0, false, m_parentScene.DefaultScriptEngine, 1);
                copy.HasGroupChanged = true;
                copy.ScheduleGroupForFullUpdate();
                copy.ResumeScripts();

                // required for physics to update it's position
                copy.AbsolutePosition = copy.AbsolutePosition;

                return copy;
            }
            finally
            {
                Monitor.Exit(m_updateLock);
            }
        }
        
        /// <summary>
        /// Calculates the distance between two Vector3s
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        protected internal float Vector3Distance(Vector3 v1, Vector3 v2)
        {
            // We don't really need the double floating point precision...
            // so casting it to a single

            return
                (float)
                Math.Sqrt((v1.X - v2.X) * (v1.X - v2.X) + (v1.Y - v2.Y) * (v1.Y - v2.Y) + (v1.Z - v2.Z) * (v1.Z - v2.Z));
        }

        #endregion

       
    }
}
