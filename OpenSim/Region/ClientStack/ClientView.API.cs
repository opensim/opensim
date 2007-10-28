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
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.ClientStack
{
    partial class ClientView
    {
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;
        public event ViewerEffectEventHandler OnViewerEffect;
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
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event StartAnim OnStartAnim;
        public event GenericCall OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event UpdateVector OnGrabObject;
        public event ObjectSelect OnDeGrabObject;
        public event ObjectDuplicate OnObjectDuplicate;
        public event MoveObject OnGrabUpdate;
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
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event GenericCall6 OnRemoveAvatar;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;

        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItemTransaction OnUpdateInventoryItem;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event RemoveTaskInventory OnRemoveTaskItem;

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
        private LLUUID m_agentId;
        public LLUUID AgentId
        {
            get
            {
                return m_agentId;
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

        #region Scene/Avatar to Client

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        public void SendRegionHandshake(RegionInfo regionInfo)
        {
            Encoding _enc = Encoding.ASCII;
            RegionHandshakePacket handshake = new RegionHandshakePacket();

            handshake.RegionInfo.BillableFactor = regionInfo.EstateSettings.billableFactor;
            handshake.RegionInfo.IsEstateManager = false;
            handshake.RegionInfo.TerrainHeightRange00 = regionInfo.EstateSettings.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = regionInfo.EstateSettings.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = regionInfo.EstateSettings.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = regionInfo.EstateSettings.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = regionInfo.EstateSettings.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = regionInfo.EstateSettings.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = regionInfo.EstateSettings.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = regionInfo.EstateSettings.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = (byte)regionInfo.EstateSettings.simAccess;
            handshake.RegionInfo.WaterHeight = regionInfo.EstateSettings.waterHeight;


            handshake.RegionInfo.RegionFlags = (uint)regionInfo.EstateSettings.regionFlags;

            handshake.RegionInfo.SimName = _enc.GetBytes(regionInfo.RegionName + "\0");
            handshake.RegionInfo.SimOwner = regionInfo.MasterAvatarAssignedUUID;
            handshake.RegionInfo.TerrainBase0 = regionInfo.EstateSettings.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = regionInfo.EstateSettings.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = regionInfo.EstateSettings.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = regionInfo.EstateSettings.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = regionInfo.EstateSettings.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = regionInfo.EstateSettings.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = regionInfo.EstateSettings.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = regionInfo.EstateSettings.terrainDetail3;
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
            mov.AgentData.SessionID = this.m_sessionId;
            mov.AgentData.AgentID = this.AgentId;
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
        public void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent, LLUUID imSessionID, string fromName, byte dialog, uint timeStamp)
        {

                Encoding enc = Encoding.ASCII;
                ImprovedInstantMessagePacket msg = new ImprovedInstantMessagePacket();
                msg.AgentData.AgentID = fromAgent;
                msg.AgentData.SessionID = fromAgentSession;
                msg.MessageBlock.FromAgentName = enc.GetBytes(fromName + " \0");
                msg.MessageBlock.Dialog = dialog;
                msg.MessageBlock.FromGroup = false;
                msg.MessageBlock.ID = imSessionID;
                msg.MessageBlock.Offline = 0;
                msg.MessageBlock.ParentEstateID = 0;
                msg.MessageBlock.Position = new LLVector3();
                msg.MessageBlock.RegionID = LLUUID.Random();
                msg.MessageBlock.Timestamp = timeStamp;
                msg.MessageBlock.ToAgentID = toAgent;
                msg.MessageBlock.Message = enc.GetBytes(message + "\0");
                msg.MessageBlock.BinaryBucket = new byte[0];

                this.OutPacket(msg);
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
                MainLog.Instance.Warn("client", "ClientView API.cs: SendLayerData() - Failed with exception " + e.ToString());
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
                patchx = px;
                patchy = py;

                patches[0] = patchx + 0 + patchy * 16;

                Packet layerpack = TerrainManager.CreateLandPacket(map, patches);
                OutPacket(layerpack);
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("client", "ClientView API .cs: SendLayerData() - Failed with exception " + e.ToString());
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
            agentData.SessionID = this.m_sessionId;
            agentData.SecureSessionID = this.SecureSessionID;
            agentData.circuitcode = this.m_circuitCode;
            agentData.child = false;
            agentData.firstname = this.firstName;
            agentData.lastname = this.lastName;
            agentData.CapsPath = "";
            return agentData;
        }

        public void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint externalIPEndPoint, string capsURL)
        {
            LLVector3 look = new LLVector3(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);

            CrossedRegionPacket newSimPack = new CrossedRegionPacket();
            newSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
            newSimPack.AgentData.AgentID = this.AgentId;
            newSimPack.AgentData.SessionID = this.m_sessionId;
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
            //newSimPack.RegionData.SeedCapability = new byte[0];
            newSimPack.RegionData.SeedCapability = Helpers.StringToField(capsURL);

            this.OutPacket(newSimPack);
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks)
        {
            MapBlockReplyPacket mapReply = new MapBlockReplyPacket();
            mapReply.AgentData.AgentID = this.AgentId;
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
            tpLocal.Info.AgentID = this.AgentId;
            tpLocal.Info.TeleportFlags = flags;
            tpLocal.Info.LocationID = 2;
            tpLocal.Info.LookAt = lookAt;
            tpLocal.Info.Position = position;
            OutPacket(tpLocal);
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint newRegionEndPoint, uint locationID, uint flags, string capsURL)
        {
            TeleportFinishPacket teleport = new TeleportFinishPacket();
            teleport.Info.AgentID = this.AgentId;
            teleport.Info.RegionHandle = regionHandle;
            teleport.Info.SimAccess = simAccess;

            teleport.Info.SeedCapability = Helpers.StringToField(capsURL);
            //teleport.Info.SeedCapability = new byte[0];

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
            tpCancel.Info.SessionID = this.m_sessionId;
            tpCancel.Info.AgentID = this.AgentId;

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
            money.MoneyData.AgentID = this.AgentId;
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
            pc.Header.Reliable = false;
            OutPacket(pc);

        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = localID;
            OutPacket(kill);
        }

        public void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = 2147483647;
            InventoryDescendentsPacket descend = this.CreateInventoryDescendentsPacket(ownerID, folderID);
            
            int count = 0;
            if (items.Count < 40)
            {
                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count];
                descend.AgentData.Descendents = items.Count;
            }
            else
            {
                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[40];
                descend.AgentData.Descendents = 40;
            }
            int i = 0;
            foreach (InventoryItemBase item in items)
            {
                descend.ItemData[i] = new InventoryDescendentsPacket.ItemDataBlock();
                descend.ItemData[i].ItemID = item.inventoryID;
                descend.ItemData[i].AssetID = item.assetID;
                descend.ItemData[i].CreatorID = item.creatorsID;
                descend.ItemData[i].BaseMask = item.inventoryBasePermissions;
                descend.ItemData[i].CreationDate = 1000;
                descend.ItemData[i].Description = enc.GetBytes(item.inventoryDescription + "\0");
                descend.ItemData[i].EveryoneMask = item.inventoryEveryOnePermissions;
                descend.ItemData[i].Flags = 1;
                descend.ItemData[i].FolderID = item.parentFolderID;
                descend.ItemData[i].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
                descend.ItemData[i].GroupMask = 0;
                descend.ItemData[i].InvType = (sbyte)item.invType;
                descend.ItemData[i].Name = enc.GetBytes(item.inventoryName + "\0");
                descend.ItemData[i].NextOwnerMask = item.inventoryNextPermissions;
                descend.ItemData[i].OwnerID = item.avatarID;
                descend.ItemData[i].OwnerMask = item.inventoryCurrentPermissions;
                descend.ItemData[i].SalePrice = 0;
                descend.ItemData[i].SaleType = 0;
                descend.ItemData[i].Type = (sbyte)item.assetType;
                descend.ItemData[i].CRC = Helpers.InventoryCRC(1000, 0, descend.ItemData[i].InvType, descend.ItemData[i].Type, descend.ItemData[i].AssetID, descend.ItemData[i].GroupID, 100, descend.ItemData[i].OwnerID, descend.ItemData[i].CreatorID, descend.ItemData[i].ItemID, descend.ItemData[i].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

                i++;
                count++;
                if (i == 40)
                {
                    this.OutPacket(descend);
                    
                    if ((items.Count - count) > 0)
                    {
                        descend = this.CreateInventoryDescendentsPacket(ownerID, folderID);
                        if ((items.Count - count) < 40)
                        {
                            descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count - count];
                            descend.AgentData.Descendents = items.Count - count;
                        }
                        else
                        {
                            descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[40];
                            descend.AgentData.Descendents = 40;
                        }
                        i = 0;
                    }
                }
            }

            if (i < 40)
            {
                this.OutPacket(descend);
            }

        }

        private InventoryDescendentsPacket CreateInventoryDescendentsPacket(LLUUID ownerID, LLUUID folderID)
        {
            InventoryDescendentsPacket descend = new InventoryDescendentsPacket();
            descend.AgentData.AgentID = this.AgentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Version = 0;

            return descend;
        }

        public void SendInventoryItemDetails(LLUUID ownerID, InventoryItemBase item)
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
            inventoryReply.InventoryData[0].BaseMask = item.inventoryBasePermissions;
            inventoryReply.InventoryData[0].CreationDate = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            inventoryReply.InventoryData[0].Description = enc.GetBytes(item.inventoryDescription + "\0");
            inventoryReply.InventoryData[0].EveryoneMask = item.inventoryEveryOnePermissions;
            inventoryReply.InventoryData[0].Flags = 0;
            inventoryReply.InventoryData[0].FolderID = item.parentFolderID;
            inventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            inventoryReply.InventoryData[0].GroupMask = 0;
            inventoryReply.InventoryData[0].InvType = (sbyte)item.invType;
            inventoryReply.InventoryData[0].Name = enc.GetBytes(item.inventoryName + "\0");
            inventoryReply.InventoryData[0].NextOwnerMask = item.inventoryNextPermissions;
            inventoryReply.InventoryData[0].OwnerID = item.avatarID;
            inventoryReply.InventoryData[0].OwnerMask = item.inventoryCurrentPermissions;
            inventoryReply.InventoryData[0].SalePrice = 0;
            inventoryReply.InventoryData[0].SaleType = 0;
            inventoryReply.InventoryData[0].Type = (sbyte)item.assetType;
            inventoryReply.InventoryData[0].CRC = Helpers.InventoryCRC(1000, 0, inventoryReply.InventoryData[0].InvType, inventoryReply.InventoryData[0].Type, inventoryReply.InventoryData[0].AssetID, inventoryReply.InventoryData[0].GroupID, 100, inventoryReply.InventoryData[0].OwnerID, inventoryReply.InventoryData[0].CreatorID, inventoryReply.InventoryData[0].ItemID, inventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

            this.OutPacket(inventoryReply);
        }

        public void SendInventoryItemUpdate(InventoryItemBase Item)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = 2147483647;
            UpdateCreateInventoryItemPacket InventoryReply = new UpdateCreateInventoryItemPacket();
            InventoryReply.AgentData.AgentID = this.AgentId;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            InventoryReply.InventoryData[0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            InventoryReply.InventoryData[0].ItemID = Item.inventoryID;
            InventoryReply.InventoryData[0].AssetID = Item.assetID;
            InventoryReply.InventoryData[0].CreatorID = Item.creatorsID;
            InventoryReply.InventoryData[0].BaseMask = Item.inventoryBasePermissions;
            InventoryReply.InventoryData[0].CreationDate = 1000;
            InventoryReply.InventoryData[0].Description = enc.GetBytes(Item.inventoryDescription + "\0");
            InventoryReply.InventoryData[0].EveryoneMask = Item.inventoryEveryOnePermissions;
            InventoryReply.InventoryData[0].Flags = 0;
            InventoryReply.InventoryData[0].FolderID = Item.parentFolderID;
            InventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            InventoryReply.InventoryData[0].GroupMask = 0;
            InventoryReply.InventoryData[0].InvType = (sbyte)Item.invType;
            InventoryReply.InventoryData[0].Name = enc.GetBytes(Item.inventoryName + "\0");
            InventoryReply.InventoryData[0].NextOwnerMask = Item.inventoryNextPermissions;
            InventoryReply.InventoryData[0].OwnerID = Item.avatarID;
            InventoryReply.InventoryData[0].OwnerMask = Item.inventoryCurrentPermissions;
            InventoryReply.InventoryData[0].SalePrice = 100;
            InventoryReply.InventoryData[0].SaleType = 0;
            InventoryReply.InventoryData[0].Type = (sbyte)Item.assetType;
            InventoryReply.InventoryData[0].CRC = Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType, InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID, InventoryReply.InventoryData[0].GroupID, 100, InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID, InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

            OutPacket(InventoryReply);
        }

        public void SendRemoveInventoryItem(LLUUID itemID)
        {
            RemoveInventoryItemPacket remove = new RemoveInventoryItemPacket();
            remove.AgentData.AgentID = this.AgentId;
            remove.AgentData.SessionID = this.m_sessionId;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[1];
            remove.InventoryData[0] = new RemoveInventoryItemPacket.InventoryDataBlock();
            remove.InventoryData[0].ItemID = itemID;

            OutPacket(remove);
        }

        public void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName)
        {
            ReplyTaskInventoryPacket replytask = new ReplyTaskInventoryPacket();
            replytask.InventoryData.TaskID = taskID;
            replytask.InventoryData.Serial = serial;
            replytask.InventoryData.Filename = fileName;
            OutPacket(replytask);
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            SendXferPacketPacket sendXfer = new SendXferPacketPacket();
            sendXfer.XferID.ID = xferID;
            sendXfer.XferID.Packet = packet;
            sendXfer.DataPacket.Data = data;
            OutPacket(sendXfer);
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
            alertPack.AgentData.AgentID = this.AgentId;
            alertPack.AlertData.Message = Helpers.StringToField(message);
            alertPack.AlertData.Modal = modal;
            OutPacket(alertPack);
        }

        public void SendLoadURL(string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned, string message, string url)
        {
            LoadURLPacket loadURL = new LoadURLPacket();
            loadURL.Data.ObjectName = Helpers.StringToField(objectname);
            loadURL.Data.ObjectID = objectID;
            loadURL.Data.OwnerID = ownerID;
            loadURL.Data.OwnerIsGroup = groupOwned;
            loadURL.Data.Message = Helpers.StringToField(message);
            loadURL.Data.URL = Helpers.StringToField(url);

            OutPacket(loadURL);
        }


        public void SendPreLoadSound(LLUUID objectID, LLUUID ownerID, LLUUID soundID)
        {
            PreloadSoundPacket preSound = new PreloadSoundPacket();
            preSound.DataBlock = new PreloadSoundPacket.DataBlockBlock[1];
            preSound.DataBlock[0] = new PreloadSoundPacket.DataBlockBlock();
            preSound.DataBlock[0].ObjectID = objectID;
            preSound.DataBlock[0].OwnerID = ownerID;
            preSound.DataBlock[0].SoundID = soundID;
            OutPacket(preSound);
        }

        public void SendPlayAttachedSound(LLUUID soundID, LLUUID objectID, LLUUID ownerID, float gain, byte flags)
        {
            AttachedSoundPacket sound = new AttachedSoundPacket();
            sound.DataBlock.SoundID = soundID;
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.OwnerID = ownerID;
            sound.DataBlock.Gain = gain;
            sound.DataBlock.Flags = flags;

            OutPacket(sound);
        }

        public void SendViewerTime(int phase)
        {

            SimulatorViewerTimeMessagePacket viewertime = new SimulatorViewerTimeMessagePacket();
            //viewertime.TimeInfo.SecPerDay = 86400;
            // viewertime.TimeInfo.SecPerYear = 31536000;
            viewertime.TimeInfo.SecPerDay = 1000;
            viewertime.TimeInfo.SecPerYear = 365000;
            viewertime.TimeInfo.SunPhase = 1;
            int sunPhase = (phase + 2) / 2;
            if ((sunPhase < 12) || (sunPhase > 36))
            {
                viewertime.TimeInfo.SunDirection = new LLVector3(0f, 0.8f, -0.8f);
                //Console.WriteLine("sending night");
            }
            else
            {
                sunPhase = sunPhase - 12;
                float yValue = 0.1f * (sunPhase);
                if (yValue > 1.2f) { yValue = yValue - 1.2f; }
                if (yValue > 1 ) { yValue = 1; }
                if (yValue < 0) { yValue = 0; }
                if (sunPhase < 14)
                {
                    yValue = 1 - yValue;
                }
                if (sunPhase < 12) { yValue *= -1; }
                viewertime.TimeInfo.SunDirection = new LLVector3(0f, yValue, 0.3f);
                //Console.WriteLine("sending sun update " + yValue);
            }
            viewertime.TimeInfo.SunAngVelocity = new LLVector3(0, 0.0f, 10.0f);
            viewertime.TimeInfo.UsecSinceStart = (ulong)Util.UnixTimeSinceEpoch();
            OutPacket(viewertime);
        }

        public void SendAvatarProperties(LLUUID avatarID, string aboutText, string bornOn, string charterMember, string flAbout, uint flags, LLUUID flImageID, LLUUID imageID, string profileURL, LLUUID partnerID)
        {
            AvatarPropertiesReplyPacket avatarReply = new AvatarPropertiesReplyPacket();
            avatarReply.AgentData.AgentID = this.AgentId;
            avatarReply.AgentData.AvatarID = avatarID;
            avatarReply.PropertiesData.AboutText = Helpers.StringToField(aboutText);
            avatarReply.PropertiesData.BornOn = Helpers.StringToField(bornOn);
            avatarReply.PropertiesData.CharterMember = Helpers.StringToField(charterMember);
            avatarReply.PropertiesData.FLAboutText = Helpers.StringToField(flAbout);
            avatarReply.PropertiesData.Flags = 0;
            avatarReply.PropertiesData.FLImageID = flImageID;
            avatarReply.PropertiesData.ImageID = imageID;
            avatarReply.PropertiesData.ProfileURL = Helpers.StringToField(profileURL);
            avatarReply.PropertiesData.PartnerID = partnerID;
            OutPacket(avatarReply);
        }

        public void SendSitResponse(LLUUID targetID, LLVector3 offset)
        {
            AvatarSitResponsePacket avatarSitResponse = new AvatarSitResponsePacket();

            avatarSitResponse.SitObject.ID = targetID;

            avatarSitResponse.SitTransform.AutoPilot = true;
            avatarSitResponse.SitTransform.SitPosition = offset;
            avatarSitResponse.SitTransform.SitRotation = new LLQuaternion(0.0f, 0.0f, 0.0f, 1.0f);

            OutPacket(avatarSitResponse);
        }
        #endregion

        #region Appearance/ Wearables Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wearables"></param>
        public void SendWearables(AvatarWearable[] wearables)
        {
            AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.AgentId;
            aw.AgentData.SerialNum = 0;
            aw.AgentData.SessionID = this.m_sessionId;

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
        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos, byte[] textureEntry, uint parentID)
        {
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            objupdate.ObjectData[0] = this.CreateDefaultAvatarPacket(textureEntry);

            //give this avatar object a local id and assign the user a name
            objupdate.ObjectData[0].ID = avatarLocalID;
            objupdate.ObjectData[0].FullID = avatarID;
            objupdate.ObjectData[0].ParentID = parentID;
            objupdate.ObjectData[0].NameValue = Helpers.StringToField("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " + lastName);
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
        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLVector3 velocity, LLQuaternion rotation)
        {
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = this.CreateAvatarImprovedBlock(localID, position, velocity, rotation);
            ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = terseBlock;

            this.OutPacket(terse);
        }

	public void SendCoarseLocationUpdate(List<LLVector3> CoarseLocations)
	{
	    CoarseLocationUpdatePacket loc = new CoarseLocationUpdatePacket();
	    int total = CoarseLocations.Count;
	    CoarseLocationUpdatePacket.IndexBlock ib = 
	                   new CoarseLocationUpdatePacket.IndexBlock();
	    loc.Location = new CoarseLocationUpdatePacket.LocationBlock[total];
	    for(int i=0; i<total; i++) {
	        CoarseLocationUpdatePacket.LocationBlock lb = 
		           new CoarseLocationUpdatePacket.LocationBlock();
	        lb.X = (byte)CoarseLocations[i].X;
	        lb.Y = (byte)CoarseLocations[i].Y;
	        lb.Z = (byte)(CoarseLocations[i].Z/4);
	        loc.Location[i] = lb;
	    }
	    ib.You = -1;
	    ib.Prey = -1;
	    loc.Index = ib;
	    this.OutPacket(loc);
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
            attach.AgentData.AgentID = this.AgentId;
            attach.AgentData.SessionID = this.m_sessionId;
            attach.AgentData.AttachmentPoint = attachPoint;
            attach.ObjectData = new ObjectAttachPacket.ObjectDataBlock[1];
            attach.ObjectData[0] = new ObjectAttachPacket.ObjectDataBlock();
            attach.ObjectData[0].ObjectLocalID = localID;
            attach.ObjectData[0].Rotation = rotation;

            this.OutPacket(attach);
        }

        public void SendPrimitiveToClient(
            ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos, uint flags,
            LLUUID objectID, LLUUID ownerID, string text, uint parentID, byte[] particleSystem, LLQuaternion rotation)
        {
            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation =  timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];

            outPacket.ObjectData[0] = this.CreatePrimUpdateBlock(primShape, flags);

            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = objectID;
            outPacket.ObjectData[0].OwnerID = ownerID;
            outPacket.ObjectData[0].Text = Helpers.StringToField(text);
            outPacket.ObjectData[0].ParentID = parentID;
            outPacket.ObjectData[0].PSBlock = particleSystem;
            outPacket.ObjectData[0].ClickAction = 0;
            //outPacket.ObjectData[0].Flags = 0;
            outPacket.ObjectData[0].Radius = 20;

            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);

            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, outPacket.ObjectData[0].ObjectData, 36, rot.Length);

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

        #region Helper Methods

        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateAvatarImprovedBlock(uint localID, LLVector3 pos, LLVector3 velocity, LLQuaternion rotation)
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

            //rotation
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
            objdata.UpdateFlags =  61 + (9 << 8) + (130 << 16) + (16 << 24);
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
            //objdata.FullID=user.AgentId;
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

        public void SendNameReply(LLUUID profileId, string firstname, string lastname)
        {
            UUIDNameReplyPacket packet = new UUIDNameReplyPacket();

            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[1];
            packet.UUIDNameBlock[0] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
            packet.UUIDNameBlock[0].ID = profileId;
            packet.UUIDNameBlock[0].FirstName = Helpers.StringToField(firstname);
            packet.UUIDNameBlock[0].LastName = Helpers.StringToField(lastname);

            OutPacket(packet);
        }

        #endregion

    }
}
