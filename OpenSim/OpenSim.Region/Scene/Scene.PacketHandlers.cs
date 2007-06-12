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
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region
{
    public partial class Scene
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="north"></param>
        /// <param name="west"></param>
        public void ModifyTerrain(byte action, float north, float west)
        {
            switch (action)
            {
                case 1:
                    // raise terrain
                    Terrain.raise(north, west, 10.0, 0.001);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 2:
                    //lower terrain
                    Terrain.lower(north, west, 10.0, 0.001);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
            }
            return;
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
            Console.WriteLine("Chat message");
            Avatar avatar = null;
            foreach (IClientAPI client in m_clientThreads.Values)
            {
                int dis = -1000;
                if (this.Avatars.ContainsKey(client.AgentId))
                {
                    
                    avatar = this.Avatars[client.AgentId];
                    // int dis = Util.fast_distance2d((int)(client.ClientAvatar.Pos.X - simClient.ClientAvatar.Pos.X), (int)(client.ClientAvatar.Pos.Y - simClient.ClientAvatar.Pos.Y));
                    dis= (int)avatar.Pos.GetDistanceTo(fromPos);
                    Console.WriteLine("found avatar at " +dis);

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
        /// <param name="parentPrim"></param>
        /// <param name="childPrims"></param>
        public void LinkObjects(uint parentPrim, List<uint> childPrims)
        {
            

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="shapeBlock"></param>
        public void UpdatePrimShape(uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
           
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
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ((OpenSim.Region.Primitive)ent).UpdatePosition(pos);
                    break;
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
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="scale"></param>
        /// <param name="remoteClient"></param>
        public void UpdatePrimScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
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
