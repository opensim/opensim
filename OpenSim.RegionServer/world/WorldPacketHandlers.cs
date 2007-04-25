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
using OpenSim.Assets;

namespace OpenSim.world
{
    partial class World
    {
        public bool ModifyTerrain(SimClient simClient, Packet packet)
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

        public bool SimChat(SimClient simClient, Packet packet)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)packet;
            if (Helpers.FieldToString(inchatpack.ChatData.Message) == "")
            {
                //empty message so don't bother with it
                return true;
            }

            libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = inchatpack.ChatData.Message;
            reply.ChatData.ChatType = 1;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = simClient.ClientAvatar.Pos;
            reply.ChatData.FromName = enc.GetBytes(simClient.ClientAvatar.firstname + " " + simClient.ClientAvatar.lastname + "\0");
            reply.ChatData.OwnerID = simClient.AgentID;
            reply.ChatData.SourceID = simClient.AgentID;
            foreach (SimClient client in m_clientThreads.Values)
            {
                client.OutPacket(reply);
            }
            return true;
        }

        public bool RezObject(SimClient simClient, Packet packet)
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

        public bool DeRezObject(SimClient simClient, Packet packet)
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
                            foreach (SimClient client in m_clientThreads.Values)
                            {
                                client.OutPacket(kill);
                            }
                            //Uncommenting this means an old UUID will be re-used, thus crashing the asset server
                            //Uncomment when prim/object UUIDs are random or such
                            //2007-03-22 - Randomskk
                            //this._primCount--;
                            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Deleted UUID " + ent.uuid);
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
                        foreach (SimClient client in m_clientThreads.Values)
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
            return true;
        }

    }
}
