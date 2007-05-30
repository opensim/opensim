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

namespace OpenSim.world
{
    public partial class World
    {
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

        public void SimChat(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            Console.WriteLine("chat message");
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

        public void RezObject(AssetBase primAsset, LLVector3 pos)
        {
          
        }

        public void DeRezObject(Packet packet, IClientAPI simClient)
        {
           
        }

        public void SendAvatarsToClient(IClientAPI remoteClient)
        {
            
        }

        public void LinkObjects(uint parentPrim, List<uint> childPrims)
        {
            

        }

        public void UpdatePrimShape(uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
           
        }

        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
           
        }

        public void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient)
        {
           
        }

        public void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient)
        {
            
        }

        public void UpdatePrimPosition(uint localID, LLVector3 pos, IClientAPI remoteClient)
        {
           
        }

        public void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient)
        {
            
        }

        public void UpdatePrimScale(uint localID, LLVector3 scale, IClientAPI remoteClient)
        {
        }
    }
}
