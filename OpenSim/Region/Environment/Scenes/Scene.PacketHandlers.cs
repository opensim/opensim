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

            // Shiny.
            double size = (double)(1 << brushsize);

            switch (action)
            {
                case 0:
                    // flatten terrain
                    Terrain.FlattenTerrain(west, north, size, (double)seconds / 5.0);
                    break;
                case 1:
                    // raise terrain
                    Terrain.RaiseTerrain(west, north, size, (double)seconds / 5.0);
                    break;
                case 2:
                    //lower terrain
                    Terrain.LowerTerrain(west, north, size, (double)seconds / 5.0);
                    break;
                case 3:
                    // smooth terrain
                    Terrain.SmoothTerrain(west, north, size, (double)seconds / 5.0);
                    break;
                case 4:
                    // noise
                    Terrain.NoiseTerrain(west, north, size, (double)seconds / 5.0);
                    break;
                case 5:
                    // revert
                    Terrain.RevertTerrain(west, north, size, (double)seconds / 5.0);
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

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    if (Terrain.Tainted(x * 16, y * 16))
                    {
                        remoteUser.SendLayerData(x, y, Terrain.GetHeights1D());
                    }
                }
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
                    toAvatar.ControllingClient.SendInstantMessage( fromAgentID, fromAgentSession, message, toAgentID, imSessionID, fromName, dialog, timestamp);
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
        /// <param name="packet"></param>
        /// <param name="simClient"></param>
        public void DeRezObject(Packet packet, IClientAPI remoteClient)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket)packet;


            if (DeRezPacket.AgentBlock.DestinationID == LLUUID.Zero)
            {
                //currently following code not used (or don't know of any case of destination being zero
            }
            else
            {
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {
                    EntityBase selectedEnt = null;
                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (EntityBase ent in this.Entities.Values)
                    {
                        if (ent.LocalId == Data.ObjectLocalID)
                        {
                            selectedEnt = ent;
                            break;
                        }
                    }
                    if (selectedEnt != null)
                    {
                        if (PermissionsMngr.CanDeRezObject(remoteClient.AgentId,((SceneObjectGroup)selectedEnt).UUID))
                        {
                            string sceneObjectXml = ((SceneObjectGroup)selectedEnt).ToXmlString();
                            CachedUserInfo userInfo = commsManager.UserProfiles.GetUserDetails(remoteClient.AgentId);
                            if (userInfo != null)
                            {
                                AssetBase asset = new AssetBase();
                                asset.Name = ((SceneObjectGroup)selectedEnt).GetPartName(selectedEnt.LocalId);
                                asset.Description = ((SceneObjectGroup)selectedEnt).GetPartDescription(selectedEnt.LocalId);
                                asset.InvType = 6;
                                asset.Type = 6;
                                asset.FullID = LLUUID.Random();
                                asset.Data = Helpers.StringToField(sceneObjectXml);
                                this.assetCache.AddAsset(asset);
                                

                                InventoryItemBase item = new InventoryItemBase();
                                item.avatarID = remoteClient.AgentId;
                                item.creatorsID = remoteClient.AgentId;
                                item.inventoryID = LLUUID.Random();
                                item.assetID = asset.FullID;
                                item.inventoryDescription = asset.Description;
                                item.inventoryName = asset.Name;
                                item.assetType = asset.Type;
                                item.invType = asset.InvType;
                                item.parentFolderID = DeRezPacket.AgentBlock.DestinationID;
                                item.inventoryCurrentPermissions = 2147483647;
                                item.inventoryNextPermissions = 2147483647;

                                userInfo.AddItem(remoteClient.AgentId, item);
                                remoteClient.SendInventoryItemUpdate(item);
                            }

                            storageManager.DataStore.RemoveObject(((SceneObjectGroup)selectedEnt).UUID, m_regInfo.SimUUID);
                            ((SceneObjectGroup)selectedEnt).DeleteGroup();

                            lock (Entities)
                            {
                                Entities.Remove(((SceneObjectGroup) selectedEnt).UUID);
                            }
                        }
                    }
                }
            }
        }

        public void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 pos)
        {
            CachedUserInfo userInfo = commsManager.UserProfiles.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if(userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase rezAsset = this.assetCache.GetAsset(item.assetID, false);
                        if (rezAsset != null)
                        {
                            this.AddRezObject(Util.FieldToString(rezAsset.Data), pos);
                            userInfo.DeleteItem(remoteClient.AgentId, item);
                            remoteClient.SendRemoveInventoryItem(itemID);
                        }
                        else
                        {               
                            rezAsset = this.assetCache.GetAsset(item.assetID, false);
                            if (rezAsset != null)
                            {
                                this.AddRezObject(Util.FieldToString(rezAsset.Data), pos);
                                userInfo.DeleteItem(remoteClient.AgentId, item);
                                remoteClient.SendRemoveInventoryItem(itemID);
                            }
                        }
                    }
                }
            }
        }

        private void AddRezObject(string xmlData, LLVector3 pos)
        {
            SceneObjectGroup group = new SceneObjectGroup(this, this.m_regionHandle, xmlData);
            this.AddEntity(group);
            group.AbsolutePosition = pos;
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
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {

            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).GetPartInventoryFileName(remoteClient, primLocalID);
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
            Primitive prim = null;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    //prim = ((SceneObject)ent).HasChildPrim(localID);
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).UpdateGroupRotation(pos, rot);
                        // prim.UpdateGroupMouseRotation(pos, rot);
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
                        //prim.UpdateGroupRotation(rot);
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
                if (ent is SceneObjectGroup)
                {
                    // prim = ((SceneObject)ent).HasChildPrim(localID);
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
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        ((SceneObjectGroup)ent).Resize(scale, localID);
                        // prim.ResizeGoup(scale);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="avatarID"></param>
        public void RequestAvatarProperty(IClientAPI remoteClient, LLUUID avatarID)
        {
            string about = "OpenSim crash test dummy";
            string bornOn = "Before now";
            string flAbout = "First life? What is one of those? OpenSim is my life!";
            LLUUID partner = new LLUUID("11111111-1111-0000-0000-000100bba000");
            remoteClient.SendAvatarProperties(avatarID, about, bornOn, "", flAbout, 0, LLUUID.Zero, LLUUID.Zero, "", partner);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="fileName"></param>
        public void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName)
        {
            /*
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    ((SceneObjectGroup)ent).RequestInventoryFile(remoteClient, ((SceneObjectGroup)ent).LocalId, xferID);
                    break;
                }
            }*/
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
            if (transActionID == LLUUID.Zero)
            {
                CachedUserInfo userInfo = commsManager.UserProfiles.GetUserDetails(remoteClient.AgentId);
                if (userInfo != null)
                {
                    AssetBase asset = new AssetBase();
                    asset.Name = name;
                    asset.Description = description;
                    asset.InvType = invType;
                    asset.Type = type;
                    asset.FullID = LLUUID.Random();
                    asset.Data = new byte[1];
                    this.assetCache.AddAsset(asset);

                    InventoryItemBase item = new InventoryItemBase();
                    item.avatarID = remoteClient.AgentId;
                    item.creatorsID = remoteClient.AgentId;
                    item.inventoryID = LLUUID.Random();
                    item.assetID = asset.FullID;
                    item.inventoryDescription = description;
                    item.inventoryName = name;
                    item.assetType = invType;
                    item.invType = invType;
                    item.parentFolderID = folderID;
                    item.inventoryCurrentPermissions = 2147483647;
                    item.inventoryNextPermissions = nextOwnerMask;

                    userInfo.AddItem(remoteClient.AgentId, item);
                    remoteClient.SendInventoryItemUpdate(item);
                }
            }
            else
            {
                commsManager.TransactionsManager.HandleInventoryFromTransaction(remoteClient, transActionID, folderID, callbackID, description, name, invType, type, wearableType, nextOwnerMask);
                //System.Console.WriteLine("request to create inventory item from transaction " + transActionID);
            }
        }

        public virtual void ProcessObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            this.EventManager.TriggerObjectGrab(localID, offsetPos, remoteClient);
        }
    }
}
