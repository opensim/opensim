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
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Console;

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
        public void ModifyTerrain(float height, float seconds, byte brushsize, byte action, float north, float west,
                                  IClientAPI remoteUser)
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
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SimChat(byte[] message, ChatTypeEnum type, int channel, LLVector3 fromPos, string fromName,
                            LLUUID fromAgentID)
        {
            if (m_simChatModule != null)
            {
                ChatFromViewerArgs args = new ChatFromViewerArgs();

                args.Message = Helpers.FieldToUTF8String(message);
                args.Channel = channel;
                args.Type = type;
                args.Position = fromPos;

                ScenePresence user = GetScenePresence(fromAgentID);
                if (user != null)
                    args.Sender = user.ControllingClient;
                else
                    args.Sender = null;

                args.From = fromName;

                m_simChatModule.SimChat(this, args);
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
                    if (((SceneObjectGroup) ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup) ent).GetProperites(remoteClient);
                        ((SceneObjectGroup) ent).IsSelected = true;
                        LandManager.setPrimsTainted();
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
                    if (((SceneObjectGroup) ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup) ent).IsSelected = false;
                        LandManager.setPrimsTainted();
                        break;
                    }
                }
            }
        }

        public void StartAnimation(LLUUID animID, int seq, LLUUID agentId)
        {
            Broadcast(delegate(IClientAPI client) { client.SendAnimation(animID, seq, agentId); });
        }

        public virtual void ProcessObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            EventManager.TriggerObjectGrab(localID, offsetPos, remoteClient);

            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = ent as SceneObjectGroup;

                    if (obj.HasChildPrim(localID))
                    {
                        obj.ObjectGrabHandler(localID, offsetPos, remoteClient);
                        return;
                    }
                }
            }
        }
 
        public void ProcessAvatarPickerRequest(IClientAPI client, LLUUID avatarID, LLUUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            List<AvatarPickerAvatar> AvatarResponses = new List<AvatarPickerAvatar>();
            AvatarResponses = CommsManager.GenerateAgentPickerRequestResponse(RequestID, query);

            AvatarPickerReplyPacket replyPacket = new AvatarPickerReplyPacket();
            AvatarPickerReplyPacket.DataBlock[] searchData = new AvatarPickerReplyPacket.DataBlock[AvatarResponses.Count];
            AvatarPickerReplyPacket.AgentDataBlock agentData = new AvatarPickerReplyPacket.AgentDataBlock();

            agentData.AgentID = avatarID;
            agentData.QueryID = RequestID;
            replyPacket.AgentData = agentData;
            byte[] bytes = new byte[AvatarResponses.Count * 32];

            int i = 0;
            foreach (AvatarPickerAvatar item in AvatarResponses)
            {
                LLUUID translatedIDtem = item.AvatarID;
                searchData[i] = new AvatarPickerReplyPacket.DataBlock();
                searchData[i].AvatarID = translatedIDtem;
                searchData[i].FirstName = Helpers.StringToField((string)item.firstName);
                searchData[i].LastName = Helpers.StringToField((string)item.lastName);
                i++;
                
            }
            if (AvatarResponses.Count == 0)
            {
                searchData = new AvatarPickerReplyPacket.DataBlock[0];
            }
            replyPacket.Data = searchData;
            client.SendAvatarPickerReply(replyPacket);
        }
    }
}