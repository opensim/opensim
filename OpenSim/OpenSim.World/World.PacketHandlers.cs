using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Terrain;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.Assets;

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
            foreach (ClientView client in m_clientThreads.Values)
            {
                // int dis = Util.fast_distance2d((int)(client.ClientAvatar.Pos.X - simClient.ClientAvatar.Pos.X), (int)(client.ClientAvatar.Pos.Y - simClient.ClientAvatar.Pos.Y));
                int dis = (int)client.ClientAvatar.Pos.GetDistanceTo(fromPos);

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
            PrimData primd = new PrimData(primAsset.Data);
            Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
            nPrim.CreateFromStorage(primd, pos, this._primCount, true);
            this.Entities.Add(nPrim.uuid, nPrim);
            this._primCount++;
        }

        public void DeRezObject(Packet packet, ClientView simClient)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket)packet;

            //Needs to delete object from physics at a later date
            if (DeRezPacket.AgentBlock.DestinationID == LLUUID.Zero)
            {
                //currently following code not used (or don't know of any case of destination being zero
               
            }
            else
            {
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {
                    Entity selectedEnt = null;
                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (Entity ent in this.Entities.Values)
                    {
                        if (ent.localid == Data.ObjectLocalID)
                        {
                            AssetBase primAsset = new AssetBase();
                            primAsset.FullID = LLUUID.Random();//DeRezPacket.AgentBlock.TransactionID.Combine(LLUUID.Zero); //should be combining with securesessionid
                            primAsset.InvType = 6;
                            primAsset.Type = 6;
                            primAsset.Name = "Prim";
                            primAsset.Description = "";
                            primAsset.Data = ((Primitive)ent).GetByteArray();
                            this._assetCache.AddAsset(primAsset);
                            this._inventoryCache.AddNewInventoryItem(simClient, DeRezPacket.AgentBlock.DestinationID, primAsset);
                            selectedEnt = ent;
                            break;
                        }
                    }
                    if (selectedEnt != null)
                    {
                        this.localStorage.RemovePrim(selectedEnt.uuid);
                        KillObjectPacket kill = new KillObjectPacket();
                        kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                        kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                        kill.ObjectData[0].ID = selectedEnt.localid;
                        foreach (ClientView client in m_clientThreads.Values)
                        {
                            client.OutPacket(kill);
                        }
                        lock (Entities)
                        {
                            Entities.Remove(selectedEnt.uuid);
                        }
                    }
                }
            }
            
        }

        public void SendAvatarsToClient(ClientView remoteClient)
        {
            foreach (ClientView client in m_clientThreads.Values)
            {
                if (client.AgentID != remoteClient.AgentID)
                {
                    // ObjectUpdatePacket objupdate = client.ClientAvatar.CreateUpdatePacket();
                    // RemoteClient.OutPacket(objupdate);
                    client.ClientAvatar.SendUpdateToOtherClient(remoteClient.ClientAvatar);
                    client.ClientAvatar.SendAppearanceToOtherAgent(remoteClient.ClientAvatar);
                }
            }
        }

        public void LinkObjects(uint parentPrim, List<uint> childPrims)
        {
            Primitive parentprim = null;
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == parentPrim)
                {
                    parentprim = (OpenSim.world.Primitive)ent;

                }
            }

            for (int i = 0; i < childPrims.Count; i++)
            {
                uint childId = childPrims[i];
                foreach (Entity ent in Entities.Values)
                {
                    if (ent.localid == childId)
                    {
                        ((OpenSim.world.Primitive)ent).MakeParent(parentprim);
                    }
                }
            }

        }

        public void UpdatePrimShape(uint primLocalID, ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == primLocalID)
                {
                    ((OpenSim.world.Primitive)ent).UpdateShape(shapeBlock);
                    break;
                }
            }
        }

        public void SelectPrim(uint primLocalID, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == primLocalID)
                {
                    ((OpenSim.world.Primitive)ent).GetProperites(remoteClient);
                    break;
                }
            }
        }

        public void UpdatePrimFlags(uint localID, Packet packet, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ((OpenSim.world.Primitive)ent).UpdateObjectFlags((ObjectFlagUpdatePacket) packet);
                    break;
                }
            }
        }

        public void UpdatePrimTexture(uint localID, byte[] texture, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ((OpenSim.world.Primitive)ent).UpdateTexture(texture);
                    break;
                }
            }
        }

        public void UpdatePrimPosition(uint localID, LLVector3 pos, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ((OpenSim.world.Primitive)ent).UpdatePosition(pos);
                    break;
                }
            }
        }

        public void UpdatePrimRotation(uint localID, LLQuaternion rot, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ent.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
                    ((OpenSim.world.Primitive)ent).UpdateFlag = true;
                    break;
                }
            }
        }

        public void UpdatePrimScale(uint localID, LLVector3 scale, ClientView remoteClient)
        {
            foreach (Entity ent in Entities.Values)
            {
                if (ent.localid == localID)
                {
                    ((OpenSim.world.Primitive)ent).Scale = scale;
                    break;
                }
            }
        }
    }
}
