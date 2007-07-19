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
using System.Net;
using System.Text;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Data;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.ClientStack
{
    partial class ClientView
    {
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatFromViewer OnChatFromViewer;
        public event RezObject OnRezObject;
        public event GenericCall4 OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event GenericCall OnRegionHandShakeReply;
        public event GenericCall OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event StartAnim OnStartAnim;
        public event GenericCall OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event UpdateVector OnGrapObject;
        public event ObjectSelect OnDeGrapObject;
        public event ObjectDuplicate OnObjectDuplicate;
        public event MoveObject OnGrapUpdate;
        public event AddNewPrim OnAddPrim;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdateVector OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event StatusChange OnChildAgentStatus;   // HOUSEKEEPING : Do we really need this?
        public event GenericCall2 OnStopMovement;       // HOUSEKEEPING : Do we really need this?
        public event NewAvatar OnNewAvatar;             // HOUSEKEEPING : Do we really need this?
        public event GenericCall6 OnRemoveAvatar;       // HOUSEKEEPING : Do we really need this?
        public event RequestMapBlocks OnRequestMapBlocks;
        public event TeleportLocationRequest OnTeleportLocationRequest;

        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event RequestTaskInventory OnRequestTaskInventory;

        public event UUIDNameRequest OnNameFromUUIDRequest;

        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event EstateOwnerMessageRequest OnEstateOwnerMessage;

        /// <summary>
        /// 
        /// </summary>
        public LLVector3 StartPos
        {
            get
            {
                return startpos;
            }
            set
            {
                startpos = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public LLUUID AgentId
        {
            get
            {
                return this.AgentID;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string FirstName
        {
            get
            {
                return this.firstName;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        public string LastName
        {
            get
            {
                return this.lastName;
            }
        }

        #region World/Avatar to Client

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        public void SendRegionHandshake(RegionInfo regionInfo)
        {
            Encoding _enc = Encoding.ASCII;
            RegionHandshakePacket handshake = new RegionHandshakePacket();

            handshake.RegionInfo.BillableFactor = regionInfo.estateSettings.billableFactor;
            handshake.RegionInfo.IsEstateManager = false;
            handshake.RegionInfo.TerrainHeightRange00 = regionInfo.estateSettings.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = regionInfo.estateSettings.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = regionInfo.estateSettings.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = regionInfo.estateSettings.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = regionInfo.estateSettings.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = regionInfo.estateSettings.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = regionInfo.estateSettings.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = regionInfo.estateSettings.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = (byte)regionInfo.estateSettings.simAccess;
            handshake.RegionInfo.WaterHeight = regionInfo.estateSettings.waterHeight;


            handshake.RegionInfo.RegionFlags = (uint)regionInfo.estateSettings.regionFlags;

            handshake.RegionInfo.SimName = _enc.GetBytes(regionInfo.RegionName + "\0");
            handshake.RegionInfo.SimOwner = regionInfo.MasterAvatarAssignedUUID;
            handshake.RegionInfo.TerrainBase0 = regionInfo.estateSettings.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = regionInfo.estateSettings.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = regionInfo.estateSettings.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = regionInfo.estateSettings.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = regionInfo.estateSettings.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = regionInfo.estateSettings.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = regionInfo.estateSettings.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = regionInfo.estateSettings.terrainDetail3;
            handshake.RegionInfo.CacheID = LLUUID.Random(); //I guess this is for the client to remember an old setting?

            this.OutPacket(handshake);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regInfo"></param>
        public void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look)
        {
            AgentMovementCompletePacket mov = new AgentMovementCompletePacket();
            mov.AgentData.SessionID = this.SessionID;
            mov.AgentData.AgentID = this.AgentID;
            mov.Data.RegionHandle = regInfo.RegionHandle;
            mov.Data.Timestamp = 1172750370; // TODO - dynamicalise this

            if ((pos.X == 0) && (pos.Y == 0) && (pos.Z == 0))
            {
                mov.Data.Position = this.startpos;
            }
            else
            {
                mov.Data.Position = pos;
            }
            mov.Data.LookAt = look;

            OutPacket(mov);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            SendChatMessage(Helpers.StringToField(message), type, fromPos, fromName, fromAgentID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            Encoding enc = Encoding.ASCII;
            ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = message;
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = enc.GetBytes(fromName + "\0");
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            this.OutPacket(reply);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>TODO</remarks>
        /// <param name="message"></param>
        /// <param name="target"></param>
        public void SendInstantMessage(string message, LLUUID target, string fromName)
        {
            if (message != "typing")
            {
                Encoding enc = Encoding.ASCII;
                ImprovedInstantMessagePacket msg = new ImprovedInstantMessagePacket();
                msg.AgentData.AgentID = this.AgentID;
                msg.AgentData.SessionID = this.SessionID;
                msg.MessageBlock.FromAgentName = enc.GetBytes(fromName + " \0");
                msg.MessageBlock.Dialog = 0;
                msg.MessageBlock.FromGroup = false;
                msg.MessageBlock.ID = target.Combine(this.SecureSessionID);
                msg.MessageBlock.Offline = 0;
                msg.MessageBlock.ParentEstateID = 0;
                msg.MessageBlock.Position = new LLVector3();
                msg.MessageBlock.RegionID = new LLUUID();
                msg.MessageBlock.Timestamp = 0;
                msg.MessageBlock.ToAgentID = target;
                msg.MessageBlock.Message = enc.GetBytes(message + "\0");
                msg.MessageBlock.BinaryBucket = new byte[0];

                this.OutPacket(msg);
            }
        }

        /// <summary>
        ///  Send the region heightmap to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        public virtual void SendLayerData(float[] map)
        {
            try
            {
                int[] patches = new int[4];

                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x = x + 4)
                    {
                        patches[0] = x + 0 + y * 16;
                        patches[1] = x + 1 + y * 16;
                        patches[2] = x + 2 + y * 16;
                        patches[3] = x + 3 + y * 16;

                        Packet layerpack = TerrainManager.CreateLandPacket(map, patches);
                        OutPacket(layerpack);
                    }
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("ClientView API.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..16</param>
        /// <param name="py">Patch coordinate (y) 0..16</param>
        /// <param name="map">heightmap</param>
        public void SendLayerData(int px, int py, float[] map)
        {
            try
            {
                int[] patches = new int[1];
                int patchx, patchy;
                patchx = px / 16;
                patchy = py / 16;

                patches[0] = patchx + 0 + patchy * 16;

                Packet layerpack = TerrainManager.CreateLandPacket(map, patches);
                OutPacket(layerpack);
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("ClientView API .cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="neighbourHandle"></param>
        /// <param name="neighbourIP"></param>
        /// <param name="neighbourPort"></param>
        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourEndPoint)
        {
            IPAddress neighbourIP = neighbourEndPoint.Address;
            ushort neighbourPort = (ushort)neighbourEndPoint.Port;

            EnableSimulatorPacket enablesimpacket = new EnableSimulatorPacket();
            enablesimpacket.SimulatorInfo = new EnableSimulatorPacket.SimulatorInfoBlock();
            enablesimpacket.SimulatorInfo.Handle = neighbourHandle;

            byte[] byteIP = neighbourIP.GetAddressBytes();
            enablesimpacket.SimulatorInfo.IP = (uint)byteIP[3] << 24;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[2] << 16;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[1] << 8;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[0];
            enablesimpacket.SimulatorInfo.Port = neighbourPort;
            OutPacket(enablesimpacket);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public AgentCircuitData RequestClientInfo()
        {
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = this.AgentId;
            agentData.SessionID = this.SessionID;
            agentData.SecureSessionID = this.SecureSessionID;
            agentData.circuitcode = this.CircuitCode;
            agentData.child = false;
            agentData.firstname = this.firstName;
            agentData.lastname = this.lastName;

            return agentData;
        }

        public void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint externalIPEndPoint)
        {
            LLVector3 look = new LLVector3(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);

            CrossedRegionPacket newSimPack = new CrossedRegionPacket();
            newSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
            newSimPack.AgentData.AgentID = this.AgentID;
            newSimPack.AgentData.SessionID = this.SessionID;
            newSimPack.Info = new CrossedRegionPacket.InfoBlock();
            newSimPack.Info.Position = pos;
            newSimPack.Info.LookAt = look; // new LLVector3(0.0f, 0.0f, 0.0f);	// copied from Avatar.cs - SHOULD BE DYNAMIC!!!!!!!!!!
            newSimPack.RegionData = new CrossedRegionPacket.RegionDataBlock();
            newSimPack.RegionData.RegionHandle = newRegionHandle;
            byte[] byteIP = externalIPEndPoint.Address.GetAddressBytes();
            newSimPack.RegionData.SimIP = (uint)byteIP[3] << 24;
            newSimPack.RegionData.SimIP += (uint)byteIP[2] << 16;
            newSimPack.RegionData.SimIP += (uint)byteIP[1] << 8;
            newSimPack.RegionData.SimIP += (uint)byteIP[0];
            newSimPack.RegionData.SimPort = (ushort)externalIPEndPoint.Port;
            newSimPack.RegionData.SeedCapability = new byte[0];

            this.OutPacket(newSimPack);
            //this.DowngradeClient();
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks)
        {
            MapBlockReplyPacket mapReply = new MapBlockReplyPacket();
            mapReply.AgentData.AgentID = this.AgentID;
            mapReply.Data = new MapBlockReplyPacket.DataBlock[mapBlocks.Count];
            mapReply.AgentData.Flags = 0;

            for (int i = 0; i < mapBlocks.Count; i++)
            {
                mapReply.Data[i] = new MapBlockReplyPacket.DataBlock();
                mapReply.Data[i].MapImageID = mapBlocks[i].MapImageId;
                mapReply.Data[i].X = mapBlocks[i].X;
                mapReply.Data[i].Y = mapBlocks[i].Y;
                mapReply.Data[i].WaterHeight = mapBlocks[i].WaterHeight;
                mapReply.Data[i].Name = Helpers.StringToField(mapBlocks[i].Name);
                mapReply.Data[i].RegionFlags = mapBlocks[i].RegionFlags;
                mapReply.Data[i].Access = mapBlocks[i].Access;
                mapReply.Data[i].Agents = mapBlocks[i].Agents;
            }
            this.OutPacket(mapReply);
        }

        public void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags)
        {
            TeleportLocalPacket tpLocal = new TeleportLocalPacket();
            tpLocal.Info.AgentID = this.AgentID;
            tpLocal.Info.TeleportFlags = flags;
            tpLocal.Info.LocationID = 2;
            tpLocal.Info.LookAt = lookAt;
            tpLocal.Info.Position = position;
            OutPacket(tpLocal);
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint newRegionEndPoint, uint locationID, uint flags)
        {
            TeleportFinishPacket teleport = new TeleportFinishPacket();
            teleport.Info.AgentID = this.AgentID;
            teleport.Info.RegionHandle = regionHandle;
            teleport.Info.SimAccess = simAccess;
            teleport.Info.SeedCapability = new byte[0];

            IPAddress oIP = newRegionEndPoint.Address;
            byte[] byteIP = oIP.GetAddressBytes();
            uint ip = (uint)byteIP[3] << 24;
            ip += (uint)byteIP[2] << 16;
            ip += (uint)byteIP[1] << 8;
            ip += (uint)byteIP[0];

            teleport.Info.SimIP = ip;
            teleport.Info.SimPort = (ushort)newRegionEndPoint.Port;
            teleport.Info.LocationID = 4;
            teleport.Info.TeleportFlags = 1 << 4;
            OutPacket(teleport);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTeleportCancel()
        {
            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
            tpCancel.Info.SessionID = this.SessionID;
            tpCancel.Info.AgentID = this.AgentID;

            OutPacket(tpCancel);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTeleportLocationStart()
        {
            TeleportStartPacket tpStart = new TeleportStartPacket();
            tpStart.Info.TeleportFlags = 16; // Teleport via location
            OutPacket(tpStart);
        }

        public void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance)
        {
            MoneyBalanceReplyPacket money = new MoneyBalanceReplyPacket();
            money.MoneyData.AgentID = this.AgentID;
            money.MoneyData.TransactionID = transaction;
            money.MoneyData.TransactionSuccess = success;
            money.MoneyData.Description = description;
            money.MoneyData.MoneyBalance = balance;
            OutPacket(money);
        }

        public void SendStartPingCheck(byte seq)
        {
            StartPingCheckPacket pc = new StartPingCheckPacket();
            pc.PingID.PingID = seq;
            OutPacket(pc);
        }

        public void SendKillObject(ulong regionHandle, uint avatarLocalID)
        {
            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = avatarLocalID;
            OutPacket(kill);
        }

        public void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = 2147483647;
            InventoryDescendentsPacket descend =  new InventoryDescendentsPacket();
            descend.AgentData.AgentID = this.AgentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Descendents = items.Count;
            descend.AgentData.Version = 0;
            descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count];
            int i = 0;
            foreach (InventoryItemBase item in items)
            {
                descend.ItemData[i] = new InventoryDescendentsPacket.ItemDataBlock();
                descend.ItemData[i].ItemID = item.inventoryID;
                descend.ItemData[i].AssetID = item.assetID;
                descend.ItemData[i].CreatorID = item.creatorsID;
                descend.ItemData[i].BaseMask = FULL_MASK_PERMISSIONS;
                descend.ItemData[i].CreationDate = 1000;
                descend.ItemData[i].Description = enc.GetBytes(item.inventoryDescription+ "\0");
                descend.ItemData[i].EveryoneMask = FULL_MASK_PERMISSIONS;
                descend.ItemData[i].Flags = 1;
                descend.ItemData[i].FolderID = item.parentFolderID;
                descend.ItemData[i].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
                descend.ItemData[i].GroupMask = FULL_MASK_PERMISSIONS;
                descend.ItemData[i].InvType = (sbyte)item.type;
                descend.ItemData[i].Name = enc.GetBytes(item.inventoryName+ "\0");
                descend.ItemData[i].NextOwnerMask = FULL_MASK_PERMISSIONS;
                descend.ItemData[i].OwnerID = item.avatarID;
                descend.ItemData[i].OwnerMask = FULL_MASK_PERMISSIONS;
                descend.ItemData[i].SalePrice = 0;
                descend.ItemData[i].SaleType = 0;
                descend.ItemData[i].Type = (sbyte)item.type;
                descend.ItemData[i].CRC = Helpers.InventoryCRC(1000, 0, descend.ItemData[i].InvType, descend.ItemData[i].Type, descend.ItemData[i].AssetID, descend.ItemData[i].GroupID, 100,descend.ItemData[i].OwnerID, descend.ItemData[i].CreatorID, descend.ItemData[i].ItemID, descend.ItemData[i].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);
                
                i++;
            }

            this.OutPacket(descend);

        }

        public void SendInventoryItemDetails(LLUUID ownerID, LLUUID folderID, InventoryItemBase item)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = 2147483647;
            FetchInventoryReplyPacket inventoryReply = new FetchInventoryReplyPacket();
            inventoryReply.AgentData.AgentID = this.AgentId;
            inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[1];
            inventoryReply.InventoryData[0] = new FetchInventoryReplyPacket.InventoryDataBlock();
            inventoryReply.InventoryData[0].ItemID = item.inventoryID;
            inventoryReply.InventoryData[0].AssetID = item.assetID;
            inventoryReply.InventoryData[0].CreatorID = item.creatorsID;
            inventoryReply.InventoryData[0].BaseMask = FULL_MASK_PERMISSIONS;
            inventoryReply.InventoryData[0].CreationDate = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            inventoryReply.InventoryData[0].Description = enc.GetBytes(item.inventoryDescription + "\0");
            inventoryReply.InventoryData[0].EveryoneMask = FULL_MASK_PERMISSIONS;
            inventoryReply.InventoryData[0].Flags = 0;
            inventoryReply.InventoryData[0].FolderID = item.parentFolderID;
            inventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            inventoryReply.InventoryData[0].GroupMask = FULL_MASK_PERMISSIONS;
            inventoryReply.InventoryData[0].InvType = (sbyte)item.type;
            inventoryReply.InventoryData[0].Name = enc.GetBytes(item.inventoryName + "\0");
            inventoryReply.InventoryData[0].NextOwnerMask = FULL_MASK_PERMISSIONS;
            inventoryReply.InventoryData[0].OwnerID = item.avatarID;
            inventoryReply.InventoryData[0].OwnerMask = FULL_MASK_PERMISSIONS;
            inventoryReply.InventoryData[0].SalePrice = 0;
            inventoryReply.InventoryData[0].SaleType = 0;
            inventoryReply.InventoryData[0].Type = (sbyte)item.type;
            inventoryReply.InventoryData[0].CRC = Helpers.InventoryCRC(1000, 0, inventoryReply.InventoryData[0].InvType, inventoryReply.InventoryData[0].Type, inventoryReply.InventoryData[0].AssetID, inventoryReply.InventoryData[0].GroupID, 100, inventoryReply.InventoryData[0].OwnerID, inventoryReply.InventoryData[0].CreatorID, inventoryReply.InventoryData[0].ItemID, inventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

            this.OutPacket(inventoryReply);
        }

        public void SendInventoryItemUpdate(InventoryItemBase Item)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = 2147483647;
            UpdateCreateInventoryItemPacket InventoryReply = new UpdateCreateInventoryItemPacket();
            InventoryReply.AgentData.AgentID = this.AgentID;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            InventoryReply.InventoryData[0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            InventoryReply.InventoryData[0].ItemID = Item.inventoryID;
            InventoryReply.InventoryData[0].AssetID = Item.assetID;
            InventoryReply.InventoryData[0].CreatorID = Item.creatorsID;
            InventoryReply.InventoryData[0].BaseMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].CreationDate = 1000;
            InventoryReply.InventoryData[0].Description = enc.GetBytes(Item.inventoryDescription + "\0");
            InventoryReply.InventoryData[0].EveryoneMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].Flags = 0;
            InventoryReply.InventoryData[0].FolderID = Item.parentFolderID;
            InventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            InventoryReply.InventoryData[0].GroupMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].InvType =(sbyte) Item.type;
            InventoryReply.InventoryData[0].Name = enc.GetBytes(Item.inventoryName + "\0");
            InventoryReply.InventoryData[0].NextOwnerMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].OwnerID = Item.avatarID;
            InventoryReply.InventoryData[0].OwnerMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].SalePrice = 100;
            InventoryReply.InventoryData[0].SaleType = 0;
            InventoryReply.InventoryData[0].Type =(sbyte) Item.type;
            InventoryReply.InventoryData[0].CRC = Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType, InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID, InventoryReply.InventoryData[0].GroupID, 100, InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID, InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

            OutPacket(InventoryReply);
        }

        public void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName)
        {
            ReplyTaskInventoryPacket replytask = new ReplyTaskInventoryPacket();
            replytask.InventoryData.TaskID = taskID;
            replytask.InventoryData.Serial = serial;
            replytask.InventoryData.Filename = fileName;
            OutPacket(replytask);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void SendAlertMessage(string message)
        {
            AlertMessagePacket alertPack = new AlertMessagePacket();
            alertPack.AlertData.Message = Helpers.StringToField(message);
            OutPacket(alertPack);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        public void SendAgentAlertMessage(string message, bool modal)
        {
            AgentAlertMessagePacket alertPack = new AgentAlertMessagePacket();
            alertPack.AgentData.AgentID = this.AgentID;
            alertPack.AlertData.Message = Helpers.StringToField(message);
            alertPack.AlertData.Modal = modal;
            OutPacket(alertPack);
        }

        #region Appearance/ Wearables Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wearables"></param>
        public void SendWearables(AvatarWearable[] wearables)
        {
            AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.AgentID;
            aw.AgentData.SerialNum = 0;
            aw.AgentData.SessionID = this.SessionID;

            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < wearables.Length; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = wearables[i].AssetID;
                awb.ItemID = wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }

            this.OutPacket(aw);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="visualParams"></param>
        /// <param name="textureEntry"></param>
        public void SendAppearance(LLUUID agentID, byte[] visualParams, byte[] textureEntry)
        {
            AvatarAppearancePacket avp = new AvatarAppearancePacket();
            avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock[218];
            avp.ObjectData.TextureEntry = textureEntry;

            AvatarAppearancePacket.VisualParamBlock avblock = null;
            for (int i = 0; i < visualParams.Length; i++)
            {
                avblock = new AvatarAppearancePacket.VisualParamBlock();
                avblock.ParamValue = visualParams[i];
                avp.VisualParam[i] = avblock;
            }

            avp.Sender.IsTrial = false;
            avp.Sender.ID = agentID;
            OutPacket(avp);
        }

        public void SendAnimation(LLUUID animID, int seq, LLUUID sourceAgentId)
        {
            AvatarAnimationPacket ani = new AvatarAnimationPacket();
            ani.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock[1];
            ani.AnimationSourceList[0] = new AvatarAnimationPacket.AnimationSourceListBlock();
            ani.AnimationSourceList[0].ObjectID = sourceAgentId;
            ani.Sender = new AvatarAnimationPacket.SenderBlock();
            ani.Sender.ID = sourceAgentId;
            ani.AnimationList = new AvatarAnimationPacket.AnimationListBlock[1];
            ani.AnimationList[0] = new AvatarAnimationPacket.AnimationListBlock();
            ani.AnimationList[0].AnimID = animID;
            ani.AnimationList[0].AnimSequenceID = seq;
            this.OutPacket(ani);
        }

        #endregion

        #region Avatar Packet/data sending Methods

        /// <summary>
        /// send a objectupdate packet with information about the clients avatar
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="avatarID"></param>
        /// <param name="avatarLocalID"></param>
        /// <param name="Pos"></param>
        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos, byte[] textureEntry)
        {
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            objupdate.ObjectData[0] = this.CreateDefaultAvatarPacket(textureEntry);

            //give this avatar object a local id and assign the user a name
            objupdate.ObjectData[0].ID = avatarLocalID;
            objupdate.ObjectData[0].FullID = avatarID;
            objupdate.ObjectData[0].NameValue = Helpers.StringToField("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " + lastName );
            LLVector3 pos2 = new LLVector3((float)Pos.X, (float)Pos.Y, (float)Pos.Z);
            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);

            OutPacket(objupdate);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLVector3 velocity)
        {
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = this.CreateAvatarImprovedBlock(localID, position, velocity);
            ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = terseBlock;

            this.OutPacket(terse);
        }

        #endregion

        #region Primitive Packet/data Sending Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rotation"></param>
        /// <param name="attachPoint"></param>
        public void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint)
        {
            ObjectAttachPacket attach = new ObjectAttachPacket();
            attach.AgentData.AgentID = this.AgentID;
            attach.AgentData.SessionID = this.SessionID;
            attach.AgentData.AttachmentPoint = attachPoint;
            attach.ObjectData = new ObjectAttachPacket.ObjectDataBlock[1];
            attach.ObjectData[0] = new ObjectAttachPacket.ObjectDataBlock();
            attach.ObjectData[0].ObjectLocalID = localID;
            attach.ObjectData[0].Rotation = rotation;

            this.OutPacket(attach);
        }
       
        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos, LLQuaternion rotation, uint flags, LLUUID objectID, LLUUID ownerID, string text, uint parentID, byte[] particleSystem)
        {
            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            outPacket.ObjectData[0] = this.CreatePrimUpdateBlock(primShape, flags);
            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = objectID;
            outPacket.ObjectData[0].OwnerID = ownerID;
            outPacket.ObjectData[0].Text = Helpers.StringToField( text );
            outPacket.ObjectData[0].ParentID = parentID;
            outPacket.ObjectData[0].PSBlock = particleSystem;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);
            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, outPacket.ObjectData[0].ObjectData, 36, rot.Length);
            OutPacket(outPacket);
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos, uint flags, LLUUID objectID, LLUUID ownerID, string text, uint parentID, byte[] particleSystem)
        {
            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            outPacket.ObjectData[0] = this.CreatePrimUpdateBlock(primShape, flags);
            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = objectID;
            outPacket.ObjectData[0].OwnerID = ownerID;
            outPacket.ObjectData[0].Text = Helpers.StringToField( text );
            outPacket.ObjectData[0].ParentID = parentID;
            outPacket.ObjectData[0].PSBlock = particleSystem;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);

            OutPacket(outPacket);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLQuaternion rotation)
        {
            ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = this.CreatePrimImprovedBlock(localID, position, rotation);

            this.OutPacket(terse);
        }

        #endregion

        #endregion

        #region Helper Methods

        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateAvatarImprovedBlock(uint localID, LLVector3 pos, LLVector3 velocity)
        {
            byte[] bytes = new byte[60];
            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();

            dat.TextureEntry = new byte[0];// AvatarTemplate.TextureEntry;

            uint ID = localID;

            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = 0;
            bytes[i++] = 1;
            i += 14;
            bytes[i++] = 128;
            bytes[i++] = 63;

            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort InternVelocityX;
            ushort InternVelocityY;
            ushort InternVelocityZ;
            Vector3 internDirec = new Vector3(0, 0, 0);

            internDirec = new Vector3(velocity.X, velocity.Y, velocity.Z);

            internDirec = internDirec / 128.0f;
            internDirec.x += 1;
            internDirec.y += 1;
            internDirec.z += 1;

            InternVelocityX = (ushort)(32768 * internDirec.x);
            InternVelocityY = (ushort)(32768 * internDirec.y);
            InternVelocityZ = (ushort)(32768 * internDirec.z);

            ushort ac = 32767;
            bytes[i++] = (byte)(InternVelocityX % 256);
            bytes[i++] = (byte)((InternVelocityX >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityY % 256);
            bytes[i++] = (byte)((InternVelocityY >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityZ % 256);
            bytes[i++] = (byte)((InternVelocityZ >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //rot
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //rotation vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            dat.Data = bytes;
            return (dat);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreatePrimImprovedBlock(uint localID, LLVector3 position, LLQuaternion rotation)
        {
            uint ID = localID;
            byte[] bytes = new byte[60];

            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
            dat.TextureEntry = new byte[0];
            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = 0;
            bytes[i++] = 0;

            byte[] pb = position.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort ac = 32767;

            //vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            ushort rw, rx, ry, rz;
            rw = (ushort)(32768 * (rotation.W + 1));
            rx = (ushort)(32768 * (rotation.X + 1));
            ry = (ushort)(32768 * (rotation.Y + 1));
            rz = (ushort)(32768 * (rotation.Z + 1));

            //rot
            bytes[i++] = (byte)(rx % 256);
            bytes[i++] = (byte)((rx >> 8) % 256);
            bytes[i++] = (byte)(ry % 256);
            bytes[i++] = (byte)((ry >> 8) % 256);
            bytes[i++] = (byte)(rz % 256);
            bytes[i++] = (byte)((rz >> 8) % 256);
            bytes[i++] = (byte)(rw % 256);
            bytes[i++] = (byte)((rw >> 8) % 256);

            //rotation vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            dat.Data = bytes;
            return dat;
        }

        /// <summary>
        /// Create the ObjectDataBlock for a ObjectUpdatePacket  (for a Primitive)
        /// </summary>
        /// <param name="primData"></param>
        /// <returns></returns>
        protected ObjectUpdatePacket.ObjectDataBlock CreatePrimUpdateBlock(PrimitiveBaseShape primShape, uint flags)
        {
            ObjectUpdatePacket.ObjectDataBlock objupdate = new ObjectUpdatePacket.ObjectDataBlock();
            this.SetDefaultPrimPacketValues(objupdate);
            objupdate.UpdateFlags = flags;
            this.SetPrimPacketShapeData(objupdate, primShape);

            return objupdate;
        }

        protected void SetPrimPacketShapeData(ObjectUpdatePacket.ObjectDataBlock objectData, PrimitiveBaseShape primData)
        {
            
            objectData.TextureEntry = primData.TextureEntry;
            objectData.PCode = primData.PCode;
            objectData.PathBegin = primData.PathBegin;
            objectData.PathEnd = primData.PathEnd;
            objectData.PathScaleX = primData.PathScaleX;
            objectData.PathScaleY = primData.PathScaleY;
            objectData.PathShearX = primData.PathShearX;
            objectData.PathShearY = primData.PathShearY;
            objectData.PathSkew = primData.PathSkew;
            objectData.ProfileBegin = primData.ProfileBegin;
            objectData.ProfileEnd = primData.ProfileEnd;
            objectData.Scale = primData.Scale;
            objectData.PathCurve = primData.PathCurve;
            objectData.ProfileCurve = primData.ProfileCurve;
            objectData.ProfileHollow = primData.ProfileHollow;
            objectData.PathRadiusOffset = primData.PathRadiusOffset;
            objectData.PathRevolutions = primData.PathRevolutions;
            objectData.PathTaperX = primData.PathTaperX;
            objectData.PathTaperY = primData.PathTaperY;
            objectData.PathTwist = primData.PathTwist;
            objectData.PathTwistBegin = primData.PathTwistBegin;
            objectData.ExtraParams = primData.ExtraParams;
        }

        /// <summary>
        /// Set some default values in a ObjectUpdatePacket
        /// </summary>
        /// <param name="objdata"></param>
        protected void SetDefaultPrimPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 3;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[60];
            objdata.ObjectData[46] = 128;
            objdata.ObjectData[47] = 63;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected ObjectUpdatePacket.ObjectDataBlock CreateDefaultAvatarPacket(byte[] textureEntry)
        {
            ObjectUpdatePacket.ObjectDataBlock objdata = new ObjectUpdatePacket.ObjectDataBlock(); //  new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);

            SetDefaultAvatarPacketValues(ref objdata);
            objdata.UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
            objdata.PathCurve = 16;
            objdata.ProfileCurve = 1;
            objdata.PathScaleX = 100;
            objdata.PathScaleY = 100;
            objdata.ParentID = 0;
            objdata.OwnerID = LLUUID.Zero;
            objdata.Scale = new LLVector3(1, 1, 1);
            objdata.PCode = 47;
            if (textureEntry != null)
            {
                objdata.TextureEntry = textureEntry;
            }
            Encoding enc = Encoding.ASCII;
            LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
            pos.X = 100f;
            objdata.ID = 8880000;
            objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
            LLVector3 pos2 = new LLVector3(100f, 100f, 23f);
            //objdata.FullID=user.AgentID;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, objdata.ObjectData, 16, pb.Length);

            return objdata;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objdata"></param>
        protected void SetDefaultAvatarPacketValues(ref ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 4;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            objdata.TextureEntry = ntex.ToBytes();
            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[76];
            objdata.ObjectData[15] = 128;
            objdata.ObjectData[16] = 63;
            objdata.ObjectData[56] = 128;
            objdata.ObjectData[61] = 102;
            objdata.ObjectData[62] = 40;
            objdata.ObjectData[63] = 61;
            objdata.ObjectData[64] = 189;
        }

        #endregion

        public void SendNameReply(LLUUID profileId, string firstname, string lastname)
        {
            UUIDNameReplyPacket packet = new UUIDNameReplyPacket();
            
            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[1];
            packet.UUIDNameBlock[0] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
            packet.UUIDNameBlock[0].ID = profileId;
            packet.UUIDNameBlock[0].FirstName = Helpers.StringToField( firstname );
            packet.UUIDNameBlock[0].LastName = Helpers.StringToField( lastname );
            
            OutPacket( packet );
        }
    }
}
