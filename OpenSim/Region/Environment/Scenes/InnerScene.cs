using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public class InnerScene
    {
        public Dictionary<LLUUID, ScenePresence> ScenePresences;
        public Dictionary<LLUUID, SceneObjectGroup> SceneObjects;
        public Dictionary<LLUUID, EntityBase> Entities;

        public BasicQuadTreeNode QuadTree;

        protected RegionInfo m_regInfo;

        protected Scene m_parentScene;
        public PhysicsScene PhyScene;

        private PermissionManager PermissionsMngr;

        public InnerScene(Scene parent, RegionInfo regInfo, PermissionManager permissionsMngr)
        {
            m_parentScene = parent;
            m_regInfo = regInfo;
            PermissionsMngr = permissionsMngr;
            QuadTree = new BasicQuadTreeNode(null, "/0/", 0, 0, 256, 256);
            QuadTree.Subdivide();
            QuadTree.Subdivide();
        }

        public void Close()
        {
            ScenePresences.Clear();
            SceneObjects.Clear();
            Entities.Clear();
        }

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
                //newAvatar.OnSignificantClientMovement += m_LandManager.handleSignificantClientMovement;

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


        public LLUUID ConvertLocalIDToFullID(uint localID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetPartsFullID(localID);
                    }
                }
            }
            return LLUUID.Zero;
        }

        public void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                }
            }
        }

        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetChildPart(localID);
                    }
                }
            }
            return null;
        }

        public SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(fullID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetChildPart(fullID);
                    }
                }
            }
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


        internal void ForEachClient(Action<IClientAPI> action)
        {
            foreach (ScenePresence presence in ScenePresences.Values)
            {
                action(presence.ControllingClient);
            }
        }

        #region Client Event handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).Resize(scale, localID);
                        break;
                    }
                }
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateSingleRotation(rot, localID);
                        break;
                    }
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateGroupRotation(rot);
                        break;
                    }
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateGroupRotation(pos, rot);
                        break;
                    }
                }
            }
        }

        public void UpdatePrimSinglePosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateSinglePosition(pos, localID);
                        break;
                    }
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateGroupPosition(pos);
                        break;
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateTextureEntry(localID, texture);
                        break;
                    }
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
            bool hasprim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasprim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasprim != false)
                    {
                        ((SceneObjectGroup)ent).UpdatePrimFlags(localID, (ushort)packet.Type, true, packet.ToBytes());
                    }
                }
            }

            //System.Console.WriteLine("Got primupdate packet: " + packet.UsePhysics.ToString());
        }

        public void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            if (PermissionsMngr.CanEditObject(remoteClient.AgentId, objectID))
            {
                bool hasPrim = false;
                foreach (EntityBase ent in Entities.Values)
                {
                    if (ent is SceneObjectGroup)
                    {
                        hasPrim = ((SceneObjectGroup)ent).HasChildPrim(objectID);
                        if (hasPrim != false)
                        {
                            ((SceneObjectGroup)ent).GrabMovement(offset, pos, remoteClient);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimName(uint primLocalID, string name)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).SetPartName(name, primLocalID);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="description"></param>
        public void PrimDescription(uint primLocalID, string description)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).SetPartDescription(description, primLocalID);
                        break;
                    }
                }
            }
        }

        public void UpdateExtraParam(uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateExtraParam(primLocalID, type, inUse, data);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        public void UpdatePrimShape(uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateShape(shapeBlock, primLocalID);
                        break;
                    }
                }
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
        /// 
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        public void DuplicateObject(uint originalPrim, LLVector3 offset, uint flags)
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
                SceneObjectGroup copy = originPrim.Copy();
                copy.AbsolutePosition = copy.AbsolutePosition + offset;
                Entities.Add(copy.UUID, copy);

                copy.ScheduleGroupForFullUpdate();

            }
            else
            {
                MainLog.Instance.Warn("client", "Attempted to duplicate nonexistant prim");
            }
        }


        #endregion
    }
}
