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
                    Terrain.raise(north, west, 10.0, 0.1);
                    RegenerateTerrain(true, (int)north, (int)west);
                    break;
                case 2:
                    //lower terrain
                    Terrain.lower(north, west, 10.0, 0.1);
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
                libsecondlife.LLUUID[] DeRezEnts;
                DeRezEnts = new libsecondlife.LLUUID[DeRezPacket.ObjectData.Length];
                int i = 0;
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {

                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (Entity ent in this.Entities.Values)
                    {
                        if (ent.localid == Data.ObjectLocalID)
                        {
                            DeRezEnts[i++] = ent.uuid;
                            this.localStorage.RemovePrim(ent.uuid);
                            KillObjectPacket kill = new KillObjectPacket();
                            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                            kill.ObjectData[0].ID = ent.localid;
                            foreach (ClientView client in m_clientThreads.Values)
                            {
                                client.OutPacket(kill);
                            }
                            //Uncommenting this means an old UUID will be re-used, thus crashing the asset server
                            //Uncomment when prim/object UUIDs are random or such
                            //2007-03-22 - Randomskk
                            //this._primCount--;
                            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.VERBOSE, "Deleted UUID " + ent.uuid);
                        }
                    }
                }
                foreach (libsecondlife.LLUUID uuid in DeRezEnts)
                {
                    lock (Entities)
                    {
                        Entities.Remove(uuid);
                    }
                }
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

        /*
        public void RequestMapBlock(ClientView simClient, int minX, int minY, int maxX, int maxY)
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            if (((m_regInfo.RegionLocX > minX) && (m_regInfo.RegionLocX < maxX)) && ((m_regInfo.RegionLocY > minY) && (m_regInfo.RegionLocY < maxY)))
            {
                MapBlockReplyPacket mapReply = new MapBlockReplyPacket();
                mapReply.AgentData.AgentID = simClient.AgentID;
                mapReply.AgentData.Flags = 0;
                mapReply.Data = new MapBlockReplyPacket.DataBlock[1];
                mapReply.Data[0] = new MapBlockReplyPacket.DataBlock();
                mapReply.Data[0].MapImageID = new LLUUID("00000000-0000-0000-9999-000000000007");
                mapReply.Data[0].X = (ushort)m_regInfo.RegionLocX;
                mapReply.Data[0].Y = (ushort)m_regInfo.RegionLocY;
                mapReply.Data[0].WaterHeight = (byte)m_regInfo.RegionWaterHeight;
                mapReply.Data[0].Name = _enc.GetBytes(this.m_regionName);
                mapReply.Data[0].RegionFlags = 72458694;
                mapReply.Data[0].Access = 13;
                mapReply.Data[0].Agents = 1; //should send number of clients connected
                simClient.OutPacket(mapReply);
            }
        }
         public bool RezObjectHandler(ClientView simClient, Packet packet)
        {
            RezObjectPacket rezPacket = (RezObjectPacket)packet;
            AgentInventory inven = this._inventoryCache.GetAgentsInventory(simClient.AgentID);
            if (inven != null)
            {
                if (inven.InventoryItems.ContainsKey(rezPacket.InventoryData.ItemID))
                {
                    AssetBase asset = this._assetCache.GetAsset(inven.InventoryItems[rezPacket.InventoryData.ItemID].AssetID);
                    if (asset != null)
                    {
                        PrimData primd = new PrimData(asset.Data);
                        Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
                        nPrim.CreateFromStorage(primd, rezPacket.RezData.RayEnd, this._primCount, true);
                        this.Entities.Add(nPrim.uuid, nPrim);
                        this._primCount++;
                        this._inventoryCache.DeleteInventoryItem(simClient, rezPacket.InventoryData.ItemID);
                    }
                }
            }
            return true;
        }
         public bool ModifyTerrain(ClientView simClient, Packet packet)
        {
            ModifyLandPacket modify = (ModifyLandPacket)packet;

            switch (modify.ModifyBlock.Action)
            {
                case 1:
                    // raise terrain
                    if (modify.ParcelData.Length > 0)
                    {
                        Terrain.raise(modify.ParcelData[0].North, modify.ParcelData[0].West, 10.0, 0.1);
                        RegenerateTerrain(true, (int)modify.ParcelData[0].North, (int)modify.ParcelData[0].West);
                    }
                    break;
                case 2:
                    //lower terrain
                    if (modify.ParcelData.Length > 0)
                    {
                        Terrain.lower(modify.ParcelData[0].North, modify.ParcelData[0].West, 10.0, 0.1);
                        RegenerateTerrain(true, (int)modify.ParcelData[0].North, (int)modify.ParcelData[0].West);
                    }
                    break;
            }
            return true;
        }
         */

    }
}
