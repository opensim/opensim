/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.IO;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Data;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class Scene
    {
        /// <summary>
        /// Modifies terrain using the specified information
        /// </summary>
        /// <param name="height">The height at which the user started modifying the terrain</param>
        /// <param name="seconds">The number of seconds the modify button was pressed</param>
        /// <param name="brushsize">The size of the brush used</param>
        /// <param name="action">The action to be performed</param>
        /// <param name="north">Distance from the north border where the cursor is located</param>
        /// <param name="west">Distance from the west border where the cursor is located</param>
        public void ModifyTerrain(float height, float seconds, byte brushsize, byte action, float north, float west, IClientAPI remoteUser)
        {
            // Do a permissions check before allowing terraforming.
            // random users are now no longer allowed to terraform
            // if permissions are enabled.
            if (!PermissionsMngr.CanTerraform(remoteUser.AgentId, new LLVector3(north, west, 0)))
                return;

            //if it wasn't for the permission checking we could have the terrain module directly subscribe to the OnModifyTerrain event
            Terrain.ModifyTerrain(height, seconds, brushsize, action, north, west, remoteUser);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Inefficient. TODO: Fixme</remarks>
        /// <param name="fromAgentID"></param>
        /// <param name="toAgentID"></param>
        /// <param name="timestamp"></param>
        /// <param name="fromAgentName"></param>
        /// <param name="message"></param>
        public void InstantMessage(LLUUID fromAgentID, LLUUID fromAgentSession, LLUUID toAgentID, LLUUID imSessionID, uint timestamp, string fromAgentName, string message, byte dialog)
        {
            if (this.Avatars.ContainsKey(toAgentID))
            {
                if (this.Avatars.ContainsKey(fromAgentID))
                {
                    // Local sim message
                    ScenePresence fromAvatar = this.Avatars[fromAgentID];
                    ScenePresence toAvatar = this.Avatars[toAgentID];
                    string fromName = fromAvatar.Firstname + " " + fromAvatar.Lastname;
                    toAvatar.ControllingClient.SendInstantMessage(fromAgentID, fromAgentSession, message, toAgentID, imSessionID, fromName, dialog, timestamp);
                }
                else
                {
                    // Message came from a user outside the sim, ignore?
                }
            }
            else
            {
                // Grid message
            }
        }

        /// <summary>
        ///  Should be removed soon as the Chat modules should take over this function
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SimChat(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            ScenePresence avatar = null;
            if (this.Avatars.ContainsKey(fromAgentID))
            {
                avatar = this.Avatars[fromAgentID];
                fromPos = avatar.AbsolutePosition;
                fromName = avatar.Firstname + " " + avatar.Lastname;
                avatar = null;
            }

            this.ForEachScenePresence(delegate(ScenePresence presence)
                                              {
                                                  int dis = -1000;
                                                  if (this.Avatars.ContainsKey(presence.ControllingClient.AgentId))
                                                  {
                                                      avatar = this.Avatars[presence.ControllingClient.AgentId];
                                                      dis = (int)avatar.AbsolutePosition.GetDistanceTo(fromPos);
                                                  }

                                                  switch (type)
                                                  {
                                                      case 0: // Whisper
                                                          if ((dis < 10) && (dis > -10))
                                                          {
                                                              //should change so the message is sent through the avatar rather than direct to the ClientView
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;
                                                      case 1: // Say
                                                          if ((dis < 30) && (dis > -30))
                                                          {
                                                              //Console.WriteLine("sending chat");
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;
                                                      case 2: // Shout
                                                          if ((dis < 100) && (dis > -100))
                                                          {
                                                              presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                     fromAgentID);
                                                          }
                                                          break;

                                                      case 0xff: // Broadcast
                                                          presence.ControllingClient.SendChatMessage(message, type, fromPos, fromName,
                                                                                 fromAgentID);
                                                          break;
                                                  }
                                              });
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
                this.Entities.Add(copy.UUID, copy);

                copy.ScheduleGroupForFullUpdate();
                /* List<ScenePresence> avatars = this.RequestAvatarList();
                 for (int i = 0; i < avatars.Count; i++)
                 {
                    // copy.SendAllChildPrimsToClient(avatars[i].ControllingClient);
                 }*/

            }
            else
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("client", "Attempted to duplicate nonexistant prim");
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
        /// <param name="remoteClient"></param>
        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup)ent).GetProperites(remoteClient);
                        ((SceneObjectGroup)ent).IsSelected = true;
                        this.LandManager.setPrimsTainted();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void DeselectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup)ent).IsSelected = false;
                        this.LandManager.setPrimsTainted();
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
        /// <param name="localID"></param>
        /// <param name="packet"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient)
        {

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

        public virtual void ProcessObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            this.EventManager.TriggerObjectGrab(localID, offsetPos, remoteClient);
        }
    }
}
