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
        public void ModifyTerrain(float height, float seconds, byte brushsize, byte action, float north, float west)
        {
            // Shiny.
            double size = (double)(1 << brushsize);

            switch (action)
            {
                case 0:
                    // flatten terrain
                    Terrain.FlattenTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 1:
                    // raise terrain
                    Terrain.RaiseTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 2:
                    //lower terrain
                    Terrain.LowerTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 3:
                    // smooth terrain
                    Terrain.SmoothTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 4:
                    // noise
                    Terrain.NoiseTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 5:
                    // revert
                    Terrain.RevertTerrain(north, west, size, (double)seconds / 5.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;

                // CLIENT EXTENSIONS GO HERE
                case 128:
                    // erode-thermal
                    break;
                case 129:
                    // erode-aerobic
                    break;
                case 130:
                    // erode-hydraulic
                    break;
            }
            return;
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
        public void InstantMessage(LLUUID fromAgentID, LLUUID toAgentID, uint timestamp, string fromAgentName, string message)
        {
            if (this.Avatars.ContainsKey(toAgentID))
            {
                if (this.Avatars.ContainsKey(fromAgentID))
                {
                    // Local sim message
                    ScenePresence fromAvatar = this.Avatars[fromAgentID];
                    ScenePresence toAvatar = this.Avatars[toAgentID];
                    string fromName = fromAvatar.firstname + " " + fromAvatar.lastname;
                    toAvatar.ControllingClient.SendInstantMessage(message, toAgentID, fromName);
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
        /// 
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
                fromPos = avatar.Pos;
                fromName = avatar.firstname + " " + avatar.lastname;
                avatar = null;
            }

            this.ForEachScenePresence(delegate(ScenePresence presence)
                                              {
                                                  int dis = -1000;
                                                  if (this.Avatars.ContainsKey(presence.ControllingClient.AgentId))
                                                  {
                                                      avatar = this.Avatars[presence.ControllingClient.AgentId];
                                                      dis = (int)avatar.Pos.GetDistanceTo(fromPos);
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
        /// <param name="primAsset"></param>
        /// <param name="pos"></param>
        public void RezObject(AssetBase primAsset, LLVector3 pos)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="simClient"></param>
        public void DeRezObject(Packet packet, IClientAPI simClient)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendAvatarsToClient(IClientAPI remoteClient)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originalPrim"></param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        public void DuplicateObject(uint originalPrim, LLVector3 offset, uint flags)
        {
            SceneObject originPrim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    if (((SceneObject)ent).rootLocalID == originalPrim)
                    {
                        originPrim = (SceneObject)ent;
                        break;
                    }
                }
            }

            if (originPrim != null)
            {
                SceneObject copy = originPrim.Copy();
                copy.Pos = copy.Pos + offset;
                this.Entities.Add(copy.rootUUID, copy);

                List<ScenePresence> avatars = this.RequestAvatarList();
                for (int i = 0; i < avatars.Count; i++)
                {
                    copy.SendAllChildPrimsToClient(avatars[i].ControllingClient);
                }

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
            SceneObject parenPrim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    if (((SceneObject)ent).rootLocalID == parentPrim)
                    {
                        parenPrim = (SceneObject)ent;
                        break;
                    }
                }
            }

            List<SceneObject> children = new List<SceneObject>();
            if (parenPrim != null)
            {
                for (int i = 0; i < childPrims.Count; i++)
                {
                    foreach (EntityBase ent in Entities.Values)
                    {
                        if (ent is SceneObject)
                        {
                            if (((SceneObject)ent).rootLocalID == childPrims[i])
                            {
                                children.Add((SceneObject)ent);
                            }
                        }
                    }
                }
            }

            foreach (SceneObject sceneObj in children)
            {
                parenPrim.AddNewChildPrims(sceneObj);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        public void UpdatePrimShape(uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(primLocalID);
                    if (prim != null)
                    {
                        prim.UpdateShape(shapeBlock);
                        break;
                    }
                }
            }
        }

        public void UpdateExtraParam(uint primLocalID, ushort type, bool inUse, byte[] data)
        {
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(primLocalID);
                    if (prim != null)
                    {
                        prim.UpdateExtraParam(type, inUse, data);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(primLocalID);
                    if (prim != null)
                    {
                        prim.GetInventory(remoteClient, primLocalID);
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
                if (ent is SceneObject)
                {
                    if (((SceneObject)ent).rootLocalID == primLocalID)
                    {
                        ((SceneObject)ent).GetProperites(remoteClient);
                        ((SceneObject)ent).isSelected = true;
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
                if (ent is SceneObject)
                {
                    if (((SceneObject)ent).rootLocalID == primLocalID)
                    {
                        ((SceneObject)ent).isSelected = false;
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(primLocalID);
                    if (prim != null)
                    {
                        prim.Description = description;
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(primLocalID);
                    if (prim != null)
                    {
                        prim.Name = name;
                        break;
                    }
                }
            }
        }

        public void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(objectID);
                    if (prim != null)
                    {
                        ((SceneObject)ent).GrapMovement(offset, pos, remoteClient);
                        break;
                    }
                }
            }
            /*
            if (this.Entities.ContainsKey(objectID))
            {
                if (this.Entities[objectID] is SceneObject)
                {
                    ((SceneObject)this.Entities[objectID]).GrapMovement(offset, pos, remoteClient);
                }
            }*/
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateTextureEntry(texture);
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateGroupPosition(pos);
                        break;
                    }
                }
            }
        }

        public void UpdatePrimSinglePosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateSinglePosition(pos);
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateGroupMouseRotation(pos, rot);
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateGroupRotation(rot);
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
            //Console.WriteLine("trying to update single prim rotation");
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.UpdateSingleRotation(rot);
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    prim = ((SceneObject)ent).HasChildPrim(localID);
                    if (prim != null)
                    {
                        prim.ResizeGoup(scale);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// temporary method to test out creating new inventory items
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transActionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type"></param>
        /// <param name="wearableType"></param>
        /// <param name="nextOwnerMask"></param>
        public void CreateNewInventoryItem(IClientAPI remoteClient, LLUUID transActionID, LLUUID folderID, uint callbackID, string description, string name, sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask)
        {
            CachedUserInfo userInfo = commsManager.UserProfilesCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                AssetBase asset = new AssetBase();
                asset.Name = name;
                asset.Description = description;
                asset.InvType = invType;
                asset.Type = type;
                asset.FullID = LLUUID.Random();
                asset.Data = new byte[0];
                this.assetCache.AddAsset(asset);

                InventoryItemBase item = new InventoryItemBase();
                item.avatarID = remoteClient.AgentId;
                item.creatorsID = remoteClient.AgentId;
                item.inventoryID = LLUUID.Random();
                item.assetID = asset.FullID;
                item.inventoryDescription = description;
                item.inventoryName = name;
                item.type = invType;
                item.parentFolderID = folderID;
                item.inventoryCurrentPermissions = 2147483647;
                item.inventoryNextPermissions = nextOwnerMask;

                userInfo.ItemReceive(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemUpdate(item);
            }
        }

        /// <summary>
        /// Sends prims to a client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void GetInitialPrims(IClientAPI RemoteClient)
        {

        }
    }
}
