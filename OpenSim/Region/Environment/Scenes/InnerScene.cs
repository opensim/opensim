using System;
using System.Collections.Generic;
using System.Text;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void PhysicsCrash();

    public class InnerScene
    {
        #region Events
        public event PhysicsCrash UnRecoverableError;
        #endregion

        #region Fields
        public Dictionary<LLUUID, ScenePresence> ScenePresences;
        public Dictionary<LLUUID, SceneObjectGroup> SceneObjects;
        public Dictionary<LLUUID, EntityBase> Entities;

        public BasicQuadTreeNode QuadTree;

        protected RegionInfo m_regInfo;
        protected Scene m_parentScene;
        protected PermissionManager PermissionsMngr;

        internal object m_syncRoot = new object();

        public PhysicsScene _PhyScene;
        #endregion

        public InnerScene(Scene parent, RegionInfo regInfo, PermissionManager permissionsMngr)
        {

            m_parentScene = parent;
            m_regInfo = regInfo;
            PermissionsMngr = permissionsMngr;
            QuadTree = new BasicQuadTreeNode(null, "/0/", 0, 0, 256, 256);
            QuadTree.Subdivide();
            QuadTree.Subdivide();
            

        }
        public PhysicsScene PhysicsScene
        {
            get
            { return _PhyScene; }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous 
                // event handler
                    try
                    {
                        _PhyScene.OnPhysicsCrash -= physicsBasedCrash;
                    }
                    catch (System.NullReferenceException)
                    {
                        // This occurs when storing to _PhyScene the first time.
                        // Is there a better way to check the event handler before
                        // getting here
                        // This can be safely ignored.  We're setting the first inital 
                        // there are no event handler's registered.
                    }
                
                _PhyScene = value;
                
                _PhyScene.OnPhysicsCrash += physicsBasedCrash;
            }
        }

        public void Close()
        {
            ScenePresences.Clear();
            SceneObjects.Clear();
            Entities.Clear();
        }

        #region Update Methods
        internal void UpdatePreparePhysics()
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

        internal void UpdateEntities()
        {
            List<EntityBase> updateEntities = new List<EntityBase>(Entities.Values);

            foreach (EntityBase entity in updateEntities)
            {
                entity.Update();
            }
        }

        internal void UpdatePhysics(double elapsed)
        {
            lock (m_syncRoot)
            {
                _PhyScene.Simulate((float)elapsed);
            }
        }

        internal void UpdateEntityMovement()
        {
            List<EntityBase> moveEntities = new List<EntityBase>(Entities.Values);

            foreach (EntityBase entity in moveEntities)
            {
                entity.UpdateMovement();
            }
        }
        #endregion

        #region Entity Methods
        public void AddEntityFromStorage(SceneObjectGroup sceneObject)
        {
            sceneObject.RegionHandle = m_regInfo.RegionHandle;
            sceneObject.SetScene(m_parentScene);
            foreach (SceneObjectPart part in sceneObject.Children.Values)
            {
                part.LocalID = m_parentScene.PrimIDAllocate();
            }
            sceneObject.UpdateParentIDs();
            AddEntity(sceneObject);
        }

        public void AddEntity(SceneObjectGroup sceneObject)
        {
            if (!Entities.ContainsKey(sceneObject.UUID))
            {
                //  QuadTree.AddObject(sceneObject);
                Entities.Add(sceneObject.UUID, sceneObject);
            }
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            foreach (EntityBase obj in Entities.Values)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == localID)
                    {
                        m_parentScene.RemoveEntity((SceneObjectGroup)obj);
                        return;
                    }
                }
            }
        }

        public ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child, AvatarWearable[] wearables, byte[] visualParams)
        {
            ScenePresence newAvatar = null;

            newAvatar = new ScenePresence(client, m_parentScene, m_regInfo, visualParams, wearables);
            newAvatar.IsChildAgent = child;

            if (child)
            {
                MainLog.Instance.Verbose("SCENE", m_regInfo.RegionName + ": Creating new child agent.");
            }
            else
            {
                MainLog.Instance.Verbose("SCENE", m_regInfo.RegionName + ": Creating new root agent.");
                MainLog.Instance.Verbose("SCENE", m_regInfo.RegionName + ": Adding Physical agent.");

                newAvatar.AddToPhysicalScene();
            }

            lock (Entities)
            {
                if (!Entities.ContainsKey(client.AgentId))
                {
                    Entities.Add(client.AgentId, newAvatar);
                }
                else
                {
                    Entities[client.AgentId] = newAvatar;
                }
            }
            lock (ScenePresences)
            {
                if (ScenePresences.ContainsKey(client.AgentId))
                {
                    ScenePresences[client.AgentId] = newAvatar;
                }
                else
                {
                    ScenePresences.Add(client.AgentId, newAvatar);
                }
            }

            return newAvatar;
        }
        #endregion

        #region Get Methods

        /// <summary>
        /// Request a List of all m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            List<ScenePresence> result = new List<ScenePresence>(ScenePresences.Values);

            return result;
        }

        public List<ScenePresence> GetAvatars()
        {
            List<ScenePresence> result =
                GetScenePresences(delegate(ScenePresence scenePresence) { return !scenePresence.IsChildAgent; });

            return result;
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            List<ScenePresence> result = new List<ScenePresence>();

            foreach (ScenePresence avatar in ScenePresences.Values)
            {
                if (filter(avatar))
                {
                    result.Add(avatar);
                }
            }

            return result;
        }

        /// <summary>
        /// Request a Avatar by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns></returns>
        public ScenePresence GetScenePresence(LLUUID avatarID)
        {
            if (ScenePresences.ContainsKey(avatarID))
            {
                return ScenePresences[avatarID];
            }
            return null;
        }

        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(localID))
                        return (SceneObjectGroup)ent;
                }
            }
            return null;
        }

        private SceneObjectGroup GetGroupByPrim(LLUUID fullID)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(fullID))
                        return (SceneObjectGroup)ent;
                }
            }
            return null;
        }

        public EntityIntersection GetClosestIntersectingPrim(Ray hray)
        {
            // Primitive Ray Tracing
            bool gothit = false;
            float closestDistance = 280f;
            EntityIntersection returnResult = new EntityIntersection();
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup reportingG = (SceneObjectGroup)ent;
                    EntityIntersection result = reportingG.TestIntersection(hray);
                    if (result.HitTF)
                    {
                        if (result.distance < closestDistance)
                        {
                            gothit = true;
                            closestDistance = result.distance;
                            returnResult = result;
                        }
                    }
                }

            }
            return returnResult;
        }

        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                return group.GetChildPart(localID);
            else
                return null;
        }

        public SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            SceneObjectGroup group = GetGroupByPrim(fullID);
            if (group != null)
                return group.GetChildPart(fullID);
            else
                return null;
        }

        internal bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            ScenePresence presence;
            if (ScenePresences.TryGetValue(avatarId, out presence))
            {
                if (!presence.IsChildAgent)
                {
                    avatar = presence;
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            foreach (ScenePresence presence in ScenePresences.Values)
            {
                if (!presence.IsChildAgent)
                {
                    string name = presence.ControllingClient.FirstName + " " + presence.ControllingClient.LastName;

                    if (String.Compare(avatarName, name, true) == 0)
                    {
                        avatar = presence;
                        return true;
                    }
                }
            }

            avatar = null;
            return false;
        }

        #endregion

        #region Other Methods


        public void physicsBasedCrash()
        {
            if (UnRecoverableError != null)
            {
                UnRecoverableError();
            }
        }

        public LLUUID ConvertLocalIDToFullID(uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                return group.GetPartsFullID(localID);
            else
                return LLUUID.Zero;
        }

        public void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    // Only send child agents stuff in their draw distance.
                    // This will need to be done for every agent once we figure out
                    // what we're going to use to store prim that agents already got 
                    // the initial update for and what we'll use to limit the 
                    // space we check for new objects on movement.

                    if (presence.IsChildAgent)
                    {
                        //Vector3 avPosition = new Vector3(presence.AbsolutePosition.X,presence.AbsolutePosition.Y,presence.AbsolutePosition.Z);
                        //LLVector3 oLoc = ((SceneObjectGroup)ent).AbsolutePosition;
                        //Vector3 objPosition = new Vector3(oLoc.X,oLoc.Y,oLoc.Z);
                        //float distResult = Vector3Distance(avPosition, objPosition);
                        //if (distResult > 512)
                        //{
                            //int x = 0;
                        //}
                        //if (distResult < presence.DrawDistance)
                        //{
                            ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                        //}
                        
                    }
                    else 
                    {
                        ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                    }
                }
            }
        }

        internal void ForEachClient(Action<IClientAPI> action)
        {
            foreach (ScenePresence presence in ScenePresences.Values)
            {
                action(presence.ControllingClient);
            }
        }
        #endregion

        #region Client Event handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.Resize(scale, localID);
        }

        /// <summary>
        /// This handles the nifty little tool tip that you get when you drag your mouse over an object 
        /// Send to the Object Group to process.  We don't know enough to service the request
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="AgentID"></param>
        /// <param name="RequestFlags"></param>
        /// <param name="ObjectID"></param>
        public void RequestObjectPropertiesFamily(IClientAPI remoteClient, LLUUID AgentID, uint RequestFlags, LLUUID ObjectID)
        {
            SceneObjectGroup group = GetGroupByPrim(ObjectID);
            if (group != null)
                group.ServiceObjectPropertiesFamilyRequest(remoteClient, AgentID, RequestFlags);


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimSingleRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateSingleRotation(rot, localID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateGroupRotation(rot);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimRotation(uint localID, LLVector3 pos, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateGroupRotation(pos, rot);
        }

        public void UpdatePrimSinglePosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateSinglePosition(pos, localID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimPosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateGroupPosition(pos);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="texture"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdateTextureEntry(localID, texture);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="packet"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
                group.UpdatePrimFlags(localID, (ushort)packet.Type, true, packet.ToBytes());
            //System.Console.WriteLine("Got primupdate packet: " + packet.UsePhysics.ToString());
        }

        public void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            if (PermissionsMngr.CanEditObject(remoteClient.AgentId, objectID))
            {
                SceneObjectGroup group = GetGroupByPrim(objectID);
                if (group != null)
                    group.GrabMovement(offset, pos, remoteClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimName(uint primLocalID, string name)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
                group.SetPartName(name, primLocalID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimDescription(uint primLocalID, string description)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
                group.SetPartDescription(description, primLocalID);
        }

        public void UpdateExtraParam(LLUUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (this.m_parentScene.PermissionsMngr.CanEditObject(agentID, group.GetPartsFullID(primLocalID)))
            {
                if (group != null)
                    group.UpdateExtraParam(primLocalID, type, inUse, data);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        public void UpdatePrimShape(LLUUID agentID, uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (this.m_parentScene.PermissionsMngr.CanEditObject(agentID, group.GetPartsFullID(primLocalID)))
            {
                if (group != null)
                    group.UpdateShape(shapeBlock, primLocalID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentPrim"></param>
        /// <param name="childPrims"></param>
        public void LinkObjects(uint parentPrim, List<uint> childPrims)
        {
            SceneObjectGroup parenPrim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == parentPrim)
                    {
                        parenPrim = (SceneObjectGroup)ent;
                        break;
                    }
                }
            }

            List<SceneObjectGroup> children = new List<SceneObjectGroup>();
            if (parenPrim != null)
            {
                for (int i = 0; i < childPrims.Count; i++)
                {
                    foreach (EntityBase ent in Entities.Values)
                    {
                        if (ent is SceneObjectGroup)
                        {
                            if (((SceneObjectGroup)ent).LocalId == childPrims[i])
                            {
                                children.Add((SceneObjectGroup)ent);
                            }
                        }
                    }
                }
            }

            foreach (SceneObjectGroup sceneObj in children)
            {
                parenPrim.LinkToGroup(sceneObj);
            }
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>    
        public void DelinkObjects(List<uint> primIds)
        {
            //OpenSim.Framework.Console.MainLog.Instance.Verbose("DelinkObjects()");

            SceneObjectGroup parenPrim = null;

            // Need a list of the SceneObjectGroup local ids
            // XXX I'm anticipating that building this dictionary once is more efficient than
            // repeated scanning of the Entity.Values for a large number of primIds.  However, it might
            // be more efficient yet to keep this dictionary permanently on hand.
            Dictionary<uint, SceneObjectGroup> sceneObjects = new Dictionary<uint, SceneObjectGroup>();
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = (SceneObjectGroup)ent;
                    sceneObjects.Add(obj.LocalId, obj);
                }
            }

            // Find the root prim among the prim ids we've been given
            for (int i = 0; i < primIds.Count; i++)
            {
                if (sceneObjects.ContainsKey(primIds[i]))
                {
                    parenPrim = sceneObjects[primIds[i]];
                    primIds.RemoveAt(i);
                    break;
                }
            }

            if (parenPrim != null)
            {
                foreach (uint childPrimId in primIds)
                {
                    parenPrim.DelinkFromGroup(childPrimId);
                }
            }
            else
            {
                OpenSim.Framework.Console.MainLog.Instance.Verbose(
                    "DelinkObjects(): Could not find a root prim out of {0} as given to a delink request!",
                    primIds);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        public void DuplicateObject(uint originalPrim, LLVector3 offset, uint flags, LLUUID AgentID, LLUUID GroupID)
        {
            SceneObjectGroup originPrim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == originalPrim)
                    {
                        originPrim = (SceneObjectGroup)ent;
                        break;
                    }
                }
            }

            if (originPrim != null)
            {
                if (PermissionsMngr.CanCopyObject(AgentID, originPrim.UUID))
                {
                    SceneObjectGroup copy = originPrim.Copy(AgentID, GroupID);
                    copy.AbsolutePosition = copy.AbsolutePosition + offset;
                    Entities.Add(copy.UUID, copy);

                    copy.ScheduleGroupForFullUpdate();
                }
            }
            else
            {
                MainLog.Instance.Warn("client", "Attempted to duplicate nonexistant prim");
            }

        }
        public float Vector3Distance(Vector3 v1, Vector3 v2)
        {
            // Calculates the distance between two Vector3s
            // We don't really need the double floating point precision...   
            // so casting it to a single

            return (float)Math.Sqrt((v1.x - v2.x) * (v1.x - v2.x) + (v1.y - v2.y) * (v1.y - v2.y) + (v1.z - v2.z) * (v1.z - v2.z));

        }
        #endregion
    }
}


