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
 *     * Neither the name of the OpenSim Project nor the
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
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void PhysicsCrash();

    public class InnerScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Events

        public event PhysicsCrash UnRecoverableError;
        private PhysicsCrash handlerPhysicsCrash = null;

        #endregion

        #region Fields

        public Dictionary<LLUUID, ScenePresence> ScenePresences;
        // SceneObjects is not currently populated or used.
        //public Dictionary<LLUUID, SceneObjectGroup> SceneObjects;
        public Dictionary<LLUUID, EntityBase> Entities;
        public Dictionary<LLUUID, ScenePresence> RestorePresences;

        public BasicQuadTreeNode QuadTree;

        protected RegionInfo m_regInfo;
        protected Scene m_parentScene;
        protected PermissionManager PermissionsMngr;
        protected List<EntityBase> m_updateList = new List<EntityBase>();
        protected int m_numRootAgents = 0;
        protected int m_numPrim = 0;
        protected int m_numChildAgents = 0;
        protected int m_physicalPrim = 0;

        protected int m_activeScripts = 0;
        protected int m_scriptLPS = 0;

        internal object m_syncRoot = new object();

        public PhysicsScene _PhyScene;

        #endregion

        public InnerScene(Scene parent, RegionInfo regInfo, PermissionManager permissionsMngr)
        {
            m_parentScene = parent;
            m_regInfo = regInfo;
            PermissionsMngr = permissionsMngr;
            QuadTree = new BasicQuadTreeNode(null, "/0/", 0, 0, (short)Constants.RegionSize, (short)Constants.RegionSize);
            QuadTree.Subdivide();
            QuadTree.Subdivide();
        }

        public PhysicsScene PhysicsScene
        {
            get { return _PhyScene; }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous 
                // event handler
                try
                {
                    _PhyScene.OnPhysicsCrash -= physicsBasedCrash;
                }
                catch (NullReferenceException)
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
            //SceneObjects.Clear();
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
            List<EntityBase> updateEntities = GetEntities();

            foreach (EntityBase entity in updateEntities)
            {
                entity.Update();
            }
        }

        internal void UpdatePresences()
        {
            List<ScenePresence> updateScenePresences = GetScenePresences();
            foreach (ScenePresence pres in updateScenePresences)
            {
                pres.Update();
            }
        }

        internal float UpdatePhysics(double elapsed)
        {
            lock (m_syncRoot)
            {
                return _PhyScene.Simulate((float)elapsed);
            }
        }

        internal void UpdateEntityMovement()
        {
            List<EntityBase> moveEntities = GetEntities();

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
                part.LocalId = m_parentScene.PrimIDAllocate();

            }
            sceneObject.UpdateParentIDs();
            AddEntity(sceneObject);
        }

        public void AddEntity(SceneObjectGroup sceneObject)
        {
            if (!Entities.ContainsKey(sceneObject.UUID))
            {
                //  QuadTree.AddObject(sceneObject);
                lock (Entities)
                {
                    Entities.Add(sceneObject.UUID, sceneObject);
                }
                m_numPrim++;
            }
        }

        /// <summary>
        /// Add an entity to the list of prims to process on the next update
        /// </summary>
        /// <param name="obj">
        /// A <see cref="EntityBase"/>
        /// </param>
        internal void AddToUpdateList(EntityBase obj)
        {
            lock (m_updateList)
            {
                if (!m_updateList.Contains(obj))
                {
                    m_updateList.Add(obj);
                }
            }
        }

        /// <summary>
        /// Process all pending updates
        /// </summary>
        internal void ProcessUpdates()
        {
            lock (m_updateList)
            {
                for (int i = 0; i < m_updateList.Count; i++)
                {
                    EntityBase entity = m_updateList[i];
                    
                    // Don't abort the whole update if one entity happens to give us an exception.
                    try
                    {
                        // A null name signals that this group was deleted before the scheduled update
                        // FIXME: This is merely a temporary measure to reduce the incidence of failure, when
                        // an object has been deleted from a scene before update was processed.
                        // A more fundamental overhaul of the update mechanism is required to eliminate all
                        // the race conditions.
                        if (entity.Name != null)
                        {
                            m_updateList[i].Update();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[INNER SCENE]: Failed to update {0}, - {1}", entity.Name, e);//entity.m_uuid
                    }
                }
                m_updateList.Clear();
            }
        }

        public void AddPhysicalPrim(int number)
        {
            m_physicalPrim++;
        }

        public void RemovePhysicalPrim(int number)
        {
            m_physicalPrim--;
        }

        public void AddToScriptLPS(int number)
        {
            m_scriptLPS += number;
        }

        public void AddActiveScripts(int number)
        {
            m_activeScripts += number;
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == localID)
                    {
                        m_parentScene.RemoveEntity((SceneObjectGroup)obj);
                        m_numPrim--;
                        return;
                    }
                }
            }
        }
        public void DetachObject(uint objectLocalID, IClientAPI remoteClient)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == objectLocalID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;
                        group.DetachToGround();
                    }
                }
            }

        }
        public void AttachObject(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, LLQuaternion rot)
        {
            List<EntityBase> EntityList = GetEntities();

            if (AttachmentPt == 0)
                AttachmentPt = (uint)AttachmentPoint.LeftHand;

            foreach (EntityBase obj in EntityList)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == objectLocalID)
                    {
                        SceneObjectGroup group = (SceneObjectGroup)obj;
                        group.AttachToAgent(remoteClient.AgentId, AttachmentPt);
                        
                    }

                }
            }

        }
        // Use the above method.
        public void AttachObject(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, LLQuaternion rot, 
            bool deadMethod)
        {
            Console.WriteLine("Attaching object " + objectLocalID + " to " + AttachmentPt);
            SceneObjectPart p = GetSceneObjectPart(objectLocalID);
            if (p != null)
            {
                ScenePresence av = null;
                if (TryGetAvatar(remoteClient.AgentId, out av))
                {
                    ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
                    objupdate.RegionData.RegionHandle = m_regInfo.RegionHandle;
                    objupdate.RegionData.TimeDilation = ushort.MaxValue;
                    objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[2];
                    // avatar stuff - horrible group copypaste

                    objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
                    objupdate.ObjectData[0].PSBlock = new byte[0];
                    objupdate.ObjectData[0].ExtraParams = new byte[1];
                    objupdate.ObjectData[0].MediaURL = new byte[0];
                    objupdate.ObjectData[0].NameValue = new byte[0];
                    objupdate.ObjectData[0].Text = new byte[0];
                    objupdate.ObjectData[0].TextColor = new byte[4];
                    objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0, 0, 0);
                    objupdate.ObjectData[0].JointPivot = new LLVector3(0, 0, 0);
                    objupdate.ObjectData[0].Material = 4;
                    objupdate.ObjectData[0].TextureAnim = new byte[0];
                    objupdate.ObjectData[0].Sound = LLUUID.Zero;

                    objupdate.ObjectData[0].State = 0;
                    objupdate.ObjectData[0].Data = new byte[0];

                    objupdate.ObjectData[0].ObjectData = new byte[76];
                    objupdate.ObjectData[0].ObjectData[15] = 128;
                    objupdate.ObjectData[0].ObjectData[16] = 63;
                    objupdate.ObjectData[0].ObjectData[56] = 128;
                    objupdate.ObjectData[0].ObjectData[61] = 102;
                    objupdate.ObjectData[0].ObjectData[62] = 40;
                    objupdate.ObjectData[0].ObjectData[63] = 61;
                    objupdate.ObjectData[0].ObjectData[64] = 189;


                    objupdate.ObjectData[0].UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
                    objupdate.ObjectData[0].PathCurve = 16;
                    objupdate.ObjectData[0].ProfileCurve = 1;
                    objupdate.ObjectData[0].PathScaleX = 100;
                    objupdate.ObjectData[0].PathScaleY = 100;
                    objupdate.ObjectData[0].ParentID = 0;
                    objupdate.ObjectData[0].OwnerID = LLUUID.Zero;
                    objupdate.ObjectData[0].Scale = new LLVector3(1, 1, 1);
                    objupdate.ObjectData[0].PCode = (byte)PCode.Avatar;
                    objupdate.ObjectData[0].TextureEntry = ScenePresence.DefaultTexture;

                    objupdate.ObjectData[0].ID = av.LocalId;
                    objupdate.ObjectData[0].FullID = remoteClient.AgentId;
                    objupdate.ObjectData[0].ParentID = 0;
                    objupdate.ObjectData[0].NameValue =
                           Helpers.StringToField("FirstName STRING RW SV " + av.Firstname + "\nLastName STRING RW SV " + av.Lastname);
                    LLVector3 pos2 = av.AbsolutePosition;
                    // new LLVector3((float) Pos.X, (float) Pos.Y, (float) Pos.Z);
                    byte[] pb = pos2.GetBytes();
                    Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);


                    // primitive part
                    objupdate.ObjectData[1] = new ObjectUpdatePacket.ObjectDataBlock();
                    // SetDefaultPrimPacketValues
                    objupdate.ObjectData[1].PSBlock = new byte[0];
                    objupdate.ObjectData[1].ExtraParams = new byte[1];
                    objupdate.ObjectData[1].MediaURL = new byte[0];
                    objupdate.ObjectData[1].NameValue = new byte[0];
                    objupdate.ObjectData[1].Text = new byte[0];
                    objupdate.ObjectData[1].TextColor = new byte[4];
                    objupdate.ObjectData[1].JointAxisOrAnchor = new LLVector3(0, 0, 0);
                    objupdate.ObjectData[1].JointPivot = new LLVector3(0, 0, 0);
                    objupdate.ObjectData[1].Material = 3;
                    objupdate.ObjectData[1].TextureAnim = new byte[0];
                    objupdate.ObjectData[1].Sound = LLUUID.Zero;
                    objupdate.ObjectData[1].State = 0;
                    objupdate.ObjectData[1].Data = new byte[0];

                    objupdate.ObjectData[1].ObjectData = new byte[60];
                    objupdate.ObjectData[1].ObjectData[46] = 128;
                    objupdate.ObjectData[1].ObjectData[47] = 63;

                    // SetPrimPacketShapeData
                    PrimitiveBaseShape primData = p.Shape;

                    objupdate.ObjectData[1].TextureEntry = primData.TextureEntry;
                    objupdate.ObjectData[1].PCode = primData.PCode;
                    objupdate.ObjectData[1].State = (byte)(((byte)AttachmentPt) << 4);
                    objupdate.ObjectData[1].PathBegin = primData.PathBegin;
                    objupdate.ObjectData[1].PathEnd = primData.PathEnd;
                    objupdate.ObjectData[1].PathScaleX = primData.PathScaleX;
                    objupdate.ObjectData[1].PathScaleY = primData.PathScaleY;
                    objupdate.ObjectData[1].PathShearX = primData.PathShearX;
                    objupdate.ObjectData[1].PathShearY = primData.PathShearY;
                    objupdate.ObjectData[1].PathSkew = primData.PathSkew;
                    objupdate.ObjectData[1].ProfileBegin = primData.ProfileBegin;
                    objupdate.ObjectData[1].ProfileEnd = primData.ProfileEnd;
                    objupdate.ObjectData[1].Scale = primData.Scale;
                    objupdate.ObjectData[1].PathCurve = primData.PathCurve;
                    objupdate.ObjectData[1].ProfileCurve = primData.ProfileCurve;
                    objupdate.ObjectData[1].ProfileHollow = primData.ProfileHollow;
                    objupdate.ObjectData[1].PathRadiusOffset = primData.PathRadiusOffset;
                    objupdate.ObjectData[1].PathRevolutions = primData.PathRevolutions;
                    objupdate.ObjectData[1].PathTaperX = primData.PathTaperX;
                    objupdate.ObjectData[1].PathTaperY = primData.PathTaperY;
                    objupdate.ObjectData[1].PathTwist = primData.PathTwist;
                    objupdate.ObjectData[1].PathTwistBegin = primData.PathTwistBegin;
                    objupdate.ObjectData[1].ExtraParams = primData.ExtraParams;


                    objupdate.ObjectData[1].UpdateFlags = 276957500; // flags;  // ??
                    objupdate.ObjectData[1].ID = p.LocalId;
                    objupdate.ObjectData[1].FullID = p.UUID;
                    objupdate.ObjectData[1].OwnerID = p.OwnerID;
                    objupdate.ObjectData[1].Text = Helpers.StringToField(p.Text);
                    objupdate.ObjectData[1].TextColor[0] = 255;
                    objupdate.ObjectData[1].TextColor[1] = 255;
                    objupdate.ObjectData[1].TextColor[2] = 255;
                    objupdate.ObjectData[1].TextColor[3] = 128;
                    objupdate.ObjectData[1].ParentID = objupdate.ObjectData[0].ID;
                    //objupdate.ObjectData[1].PSBlock = particleSystem;
                    //objupdate.ObjectData[1].ClickAction = clickAction;
                    objupdate.ObjectData[1].Radius = 20;
                    objupdate.ObjectData[1].NameValue =
                           Helpers.StringToField("AttachItemID STRING RW SV " + p.UUID);
                    LLVector3 pos = new LLVector3((float)0.0, (float)0.0, (float)0.0);

                    pb = pos.GetBytes();
                    Array.Copy(pb, 0, objupdate.ObjectData[1].ObjectData, 0, pb.Length);

                    byte[] brot = rot.GetBytes();
                    Array.Copy(brot, 0, objupdate.ObjectData[1].ObjectData, 36, brot.Length);

                    remoteClient.OutPacket(objupdate, ThrottleOutPacketType.Task);
                }
                else
                {
                    m_log.Info("[SCENE]: Avatar " + remoteClient.AgentId + " not found");
                }
            }
            else
            {
                m_log.Info("[SCENE]: Attempting to attach object; Object " + objectLocalID + "(localID) not found");
            }
        }


        public ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child, AvatarAppearance appearance)
        {
            ScenePresence newAvatar = null;

            newAvatar = new ScenePresence(client, m_parentScene, m_regInfo, appearance);
            newAvatar.IsChildAgent = child;

            if (child)
            {
                m_numChildAgents++;
                m_log.Info("[SCENE]: " + m_regInfo.RegionName + ": Creating new child agent.");
            }
            else
            {
                m_numRootAgents++;
                m_log.Info("[SCENE]: " + m_regInfo.RegionName + ": Creating new root agent.");
                m_log.Info("[SCENE]: " + m_regInfo.RegionName + ": Adding Physical agent.");

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

        public void AddScenePresence(ScenePresence presence)
        {
            bool child = presence.IsChildAgent;

            if (child)
            {
                m_numChildAgents++;
                m_log.Info("[SCENE]" + m_regInfo.RegionName + ": Creating new child agent.");
            }
            else
            {
                m_numRootAgents++;
                m_log.Info("[SCENE] " + m_regInfo.RegionName + ": Creating new root agent.");
                m_log.Info("[SCENE] " + m_regInfo.RegionName + ": Adding Physical agent.");

                presence.AddToPhysicalScene();
            }

            lock (Entities)
            {
                Entities[presence.UUID] = presence;
            }

            lock (ScenePresences)
            {
                ScenePresences[presence.UUID] = presence;
            }
        }

        public void SwapRootChildAgent(bool direction_RC_CR_T_F)
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

        public void RemoveAPrimCount()
        {
            m_numPrim--;
        }

        public void AddAPrimCount()
        {
            m_numPrim++;
        }

        public int GetChildAgentCount()
        {
            // some network situations come in where child agents get closed twice.
            if (m_numChildAgents < 0)
            {
                m_numChildAgents = 0;
            }

            return m_numChildAgents;
        }

        public int GetRootAgentCount()
        {
            return m_numRootAgents;
        }

        public int GetTotalObjects()
        {
            return m_numPrim;
        }

        public int GetActiveObjects()
        {
            return m_physicalPrim;
        }

        public int GetActiveScripts()
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
        /// Request a List of all m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            List<ScenePresence> result;

            lock (ScenePresences)
            {
                result = new List<ScenePresence>(ScenePresences.Values);
            }

            return result;
        }

        public List<ScenePresence> GetAvatars()
        {
            List<ScenePresence> result =
                GetScenePresences(delegate(ScenePresence scenePresence) { return !scenePresence.IsChildAgent; });

            return result;
        }
        
        /// <summary>
        /// Get the controlling client for the given avatar, if there is one.
        /// 
        /// FIXME: The only user of the method right now is Caps.cs, in order to resolve a client API since it can't 
        /// use the ScenePresence.  This could be better solved in a number of ways - we could establish an
        /// OpenSim.Framework.IScenePresence, or move the caps code into a region package (which might be the more 
        /// suitable solution).
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns>null if either the avatar wasn't in the scene, or they do not have a controlling client</returns>
        public IClientAPI GetControllingClient(LLUUID agentId)
        {
            ScenePresence presence = GetScenePresence(agentId);
            
            if (presence != null)
            {
                return presence.ControllingClient;
            }
            
            return null;
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            List<ScenePresence> result = new List<ScenePresence>();
            List<ScenePresence> ScenePresencesList = GetScenePresences();

            foreach (ScenePresence avatar in ScenePresencesList)
            {
                if (filter(avatar))
                {
                    result.Add(avatar);
                }
            }

            return result;
        }

        /// <summary>
        /// Request a scene presence by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the agent was not found</returns>
        public ScenePresence GetScenePresence(LLUUID agentID)
        {
            if (ScenePresences.ContainsKey(agentID))
            {
                return ScenePresences[agentID];
            }
            
            return null;
        }

        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
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
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
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
                else
                {
                    m_log.WarnFormat(
                        "[INNER SCENE]: Requested avatar {0} could not be found in scene {1} since it is only registered as a child agent!", 
                        avatarId, m_parentScene.RegionInfo.RegionName);
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

        public List<EntityBase> GetEntities()
        {
            List<EntityBase> result;

            lock (Entities)
            {
                result = new List<EntityBase>(Entities.Values);
            }

            return result;
        }

        #endregion

        #region Other Methods

        public void physicsBasedCrash()
        {
            handlerPhysicsCrash = UnRecoverableError;
            if (handlerPhysicsCrash != null)
            {
                handlerPhysicsCrash();
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
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    // Only send child agents stuff in their draw distance.
                    // This will need to be done for every agent once we figure out
                    // what we're going to use to store prim that agents already got 
                    // the initial update for and what we'll use to limit the 
                    // space we check for new objects on movement.

                    if (presence.IsChildAgent && m_parentScene.m_seeIntoRegionFromNeighbor)
                    {
                        LLVector3 oLoc = ((SceneObjectGroup)ent).AbsolutePosition;
                        float distResult = (float)Util.GetDistanceTo(presence.AbsolutePosition, oLoc);

                        //m_log.Info("[DISTANCE]: " + distResult.ToString());

                        if (distResult < presence.DrawDistance)
                        {
                            // Send Only if we don't already know about it.
                            // KnownPrim also makes the prim known when called.
                            if (!presence.KnownPrim(((SceneObjectGroup)ent).UUID))
                                ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                        }
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
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.Resize(scale, localID);
                }
            }
        }
        public void UpdatePrimGroupScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.GroupResize(scale, localID);
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
        public void RequestObjectPropertiesFamily(IClientAPI remoteClient, LLUUID AgentID, uint RequestFlags,
                                                  LLUUID ObjectID)
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
        public void UpdatePrimSingleRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
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
        public void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.UpdateGroupRotation(rot);
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
        public void UpdatePrimRotation(uint localID, LLVector3 pos, LLQuaternion rot, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.UpdateGroupRotation(pos, rot);
                }
            }
        }

        public void UpdatePrimSinglePosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                LLVector3 oldPos = group.AbsolutePosition;
                if (!PermissionsMngr.CanObjectEntry(remoteClient.AgentId, oldPos, pos))
                {
                    group.SendGroupTerseUpdate();
                    return;
                }
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.UpdateSinglePosition(pos, localID);
                }
            }
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
            {
                
                LLVector3 oldPos = group.AbsolutePosition;
                if (group.RootPart.m_IsAttachment)
                {
                    group.UpdateGroupPosition(pos);
                }
                else 
                {
                    if (!PermissionsMngr.CanObjectEntry(remoteClient.AgentId, oldPos, pos))
                    {
                        group.SendGroupTerseUpdate();
                        return;
                    }
                    if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                    {
                        group.UpdateGroupPosition(pos);
                    }
                }
            }
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
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))
                {
                    group.UpdateTextureEntry(localID, texture);
                }
            }
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
            {
                if (PermissionsMngr.CanEditObject(remoteClient.AgentId, group.UUID))
                {
                    group.UpdatePrimFlags(localID, (ushort)packet.Type, true, packet.ToBytes());
                }
            }
        }

        public void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            SceneObjectGroup group = GetGroupByPrim(objectID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(remoteClient.AgentId, group.UUID))// && PermissionsMngr.)
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
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimName(IClientAPI remoteClient, uint primLocalID, string name)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObject(remoteClient.AgentId, group.UUID))
                {
                    group.SetPartName(Util.CleanString(name), primLocalID);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimDescription(IClientAPI remoteClient, uint primLocalID, string description)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObject(remoteClient.AgentId, group.UUID))
                {
                    group.SetPartDescription(Util.CleanString(description), primLocalID);
                }
            }
        }

        public void UpdateExtraParam(LLUUID agentID, uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);

            if (group != null)
            {
                if (PermissionsMngr.CanEditObject(agentID, group.UUID))
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
        public void UpdatePrimShape(LLUUID agentID, uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                if (PermissionsMngr.CanEditObjectPosition(agentID, group.GetPartsFullID(primLocalID)))
                {
                    group.UpdateShape(shapeBlock, primLocalID);
                }
            }
        }

        /// <summary>
        /// Initial method invoked when we receive a link objects request from the client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="parentPrim"></param>
        /// <param name="childPrims"></param>
        public void LinkObjects(IClientAPI client, uint parentPrim, List<uint> childPrims)
        {
            List<EntityBase> EntityList = GetEntities();

            SceneObjectGroup parenPrim = null;
            foreach (EntityBase ent in EntityList)
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
                    foreach (EntityBase ent in EntityList)
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
            
            // We need to explicitly resend the newly link prim's object properties since no other actions
            // occur on link to invoke this elsewhere (such as object selection)
            parenPrim.GetProperties(client);
        }

        /// <summary>
        /// Delink a linkset
        /// </summary>
        /// <param name="prims"></param>    
        public void DelinkObjects(List<uint> primIds)
        {
            SceneObjectGroup parenPrim = null;

            // Need a list of the SceneObjectGroup local ids
            // XXX I'm anticipating that building this dictionary once is more efficient than
            // repeated scanning of the Entity.Values for a large number of primIds.  However, it might
            // be more efficient yet to keep this dictionary permanently on hand.

            Dictionary<uint, SceneObjectGroup> sceneObjects = new Dictionary<uint, SceneObjectGroup>();

            List<EntityBase> EntitieList = GetEntities();
            foreach (EntityBase ent in EntitieList)
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
                // If the first scan failed, we need to do a /deep/ scan of the linkages.  This is /really/ slow
                // We know that this is not the root prim now essentially, so we don't have to worry about remapping 
                // which one is the root prim
                bool delinkedSomething = false;
                for (int i = 0; i < primIds.Count; i++)
                {
                    foreach (SceneObjectGroup grp in sceneObjects.Values)
                    {
                        SceneObjectPart gPart = grp.GetChildPart(primIds[i]);
                        if (gPart != null)
                        {
                            grp.DelinkFromGroup(primIds[i]);
                            delinkedSomething = true;
                        }

                    }
                }
                if (!delinkedSomething)
                {
                    m_log.InfoFormat("[SCENE]: " +
                                    "DelinkObjects(): Could not find a root prim out of {0} as given to a delink request!",
                                    primIds);
                }
            }
        }

        public void MakeObjectSearchable(IClientAPI remoteClient, bool IncludeInSearch, uint localID)
        {
            LLUUID user = remoteClient.AgentId;
            LLUUID objid = null;
            SceneObjectPart obj = null;

            List<EntityBase> EntityList = GetEntities();
            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    foreach (KeyValuePair<LLUUID, SceneObjectPart> subent in ((SceneObjectGroup)ent).Children)
                    {
                        if (subent.Value.LocalId == localID)
                        {
                            objid = subent.Key;
                            obj = subent.Value;
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

            if (IncludeInSearch && PermissionsMngr.CanEditObject(user, objid))
            {
                obj.AddFlag(LLObject.ObjectFlags.JointWheel);
            }
            else if (!IncludeInSearch && PermissionsMngr.CanEditObjectPosition(user, objid))
            {
                obj.RemFlag(LLObject.ObjectFlags.JointWheel);
            }
        }

        /// <summary>
        /// Duplicate the given object.
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        public void DuplicateObject(uint originalPrim, LLVector3 offset, uint flags, LLUUID AgentID, LLUUID GroupID)
        {
            m_log.DebugFormat("[SCENE]: Duplication of object {0} at offset {1} requested by agent {2}", originalPrim, offset, AgentID);
            
            List<EntityBase> EntityList = GetEntities();

            SceneObjectGroup originPrim = null;
            foreach (EntityBase ent in EntityList)
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
                    copy.ResetIDs();

                    lock (Entities)
                    {
                        Entities.Add(copy.UUID, copy);
                    }

                    m_numPrim++;

                    copy.StartScripts();
                    copy.ScheduleGroupForFullUpdate();
                }
            }
            else
            {
                m_log.WarnFormat("[SCENE]: Attempted to duplicate nonexistant prim id {0}", GroupID);
            }
        }

        /// <summary>
        /// Calculates the distance between two Vector3s
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public float Vector3Distance(Vector3 v1, Vector3 v2)
        {
            // We don't really need the double floating point precision...   
            // so casting it to a single

            return
                (float)
                Math.Sqrt((v1.x - v2.x) * (v1.x - v2.x) + (v1.y - v2.y) * (v1.y - v2.y) + (v1.z - v2.z) * (v1.z - v2.z));
        }

        #endregion
    }
}
