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
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

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
                    Terrain.flatten(north, west, size, (double)seconds / 100.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 1:
                    // raise terrain
                    Terrain.raise(north, west, size, (double)seconds / 100.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 2:
                    //lower terrain
                    Terrain.lower(north, west, size, (double)seconds / 100.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 3:
                    // smooth terrain
                    Terrain.smooth(north, west, size, (double)seconds / 100.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 4:
                    // noise
                    Terrain.noise(north, west, size, (double)seconds / 100.0);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 5:
                    // revert
                    Terrain.revert(north, west, size, (double)seconds / 100.0);
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
                    ScenePresence avatar = this.Avatars[fromAgentID];
                    avatar.ControllingClient.SendInstantMessage(message, toAgentID);
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
           // Console.WriteLine("Chat message");
            ScenePresence avatar = null;
            foreach (IClientAPI client in m_clientThreads.Values)
            {
                int dis = -1000;
                if (this.Avatars.ContainsKey(client.AgentId))
                {
                    
                    avatar = this.Avatars[client.AgentId];
                    // int dis = Util.fast_distance2d((int)(client.ClientAvatar.Pos.X - simClient.ClientAvatar.Pos.X), (int)(client.ClientAvatar.Pos.Y - simClient.ClientAvatar.Pos.Y));
                    dis= (int)avatar.Pos.GetDistanceTo(fromPos);
                    //Console.WriteLine("found avatar at " +dis);

                }
             
                switch (type)
                {
                    case 0: // Whisper
                        if ((dis < 10) && (dis > -10))
                        {
                            //should change so the message is sent through the avatar rather than direct to the ClientView
                            client.SendChatMessage(message, type, fromPos, fromName, fromAgentID);
                        }
                        break;
                    case 1: // Say
                        if ((dis < 30) && (dis > -30))
                        {
                            Console.WriteLine("sending chat");
                            client.SendChatMessage(message, type, fromPos, fromName, fromAgentID);
                        }
                        break;
                    case 2: // Shout
                        if ((dis < 100) && (dis > -100))
                        {
                            client.SendChatMessage(message, type, fromPos, fromName, fromAgentID);
                        }
                        break;

                    case 0xff: // Broadcast
                        client.SendChatMessage(message, type, fromPos, fromName, fromAgentID);
                        break;
                }

            }
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
                //SceneObject copy = originPrim.Copy();

            }
            else
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("Attempted to duplicate nonexistant prim");
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
                        prim.UpdateGroupMouseRotation( pos, rot);
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
        /// Sends prims to a client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void GetInitialPrims(IClientAPI RemoteClient)
        {

        }
    }
}
