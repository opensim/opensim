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
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Types;

using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
    partial class ClientView
    {
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
        public event GenericCall4 OnAddPrim;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectSelect OnObjectSelect;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdatePrimVector OnUpdatePrimPosition;
        public event UpdatePrimRotation OnUpdatePrimRotation;
        public event UpdatePrimVector OnUpdatePrimScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event NewAvatar OnNewAvatar;
        public event GenericCall6 OnRemoveAvatar;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event TeleportLocationRequest OnTeleportLocationRequest;

        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;

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
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
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
            // TODO - dynamicalise this stuff
            mov.Data.Timestamp = 1172750370;
            if (pos != null)
            {
                mov.Data.Position = pos;
            }
            else
            {
                mov.Data.Position = this.startpos;
            }
            mov.Data.LookAt = look;

            OutPacket(mov);
        }

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
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
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
                OpenSim.Framework.Console.MainLog.Instance.Warn("ClientView API.cs: SendLayerData() - Failed with exception " + e.ToString());
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
                OpenSim.Framework.Console.MainLog.Instance.Warn("ClientView API .cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="neighbourHandle"></param>
        /// <param name="neighbourIP"></param>
        /// <param name="neighbourPort"></param>
        public void InformClientOfNeighbour(ulong neighbourHandle, System.Net.IPAddress neighbourIP, ushort neighbourPort)
        {
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

        public void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, System.Net.IPAddress newRegionIP, ushort newRegionPort)
        {
            LLVector3 look = new LLVector3(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);

            CrossedRegionPacket newSimPack = new CrossedRegionPacket();
            newSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
            newSimPack.AgentData.AgentID = this.AgentID;
            newSimPack.AgentData.SessionID = this.SessionID;
            newSimPack.Info = new CrossedRegionPacket.InfoBlock();
            newSimPack.Info.Position = pos;
            newSimPack.Info.LookAt = look; // new LLVector3(0.0f, 0.0f, 0.0f);	// copied from Avatar.cs - SHOULD BE DYNAMIC!!!!!!!!!!
            newSimPack.RegionData = new libsecondlife.Packets.CrossedRegionPacket.RegionDataBlock();
            newSimPack.RegionData.RegionHandle = newRegionHandle;
            byte[] byteIP = newRegionIP.GetAddressBytes();
            newSimPack.RegionData.SimIP = (uint)byteIP[3] << 24;
            newSimPack.RegionData.SimIP += (uint)byteIP[2] << 16;
            newSimPack.RegionData.SimIP += (uint)byteIP[1] << 8;
            newSimPack.RegionData.SimIP += (uint)byteIP[0];
            newSimPack.RegionData.SimPort = newRegionPort;
            newSimPack.RegionData.SeedCapability = new byte[0];

            this.OutPacket(newSimPack);
            //this.DowngradeClient();
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks)
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;

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
                mapReply.Data[i].Name = _enc.GetBytes(mapBlocks[i].Name);
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

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, string ipAddress, ushort ipPort, uint locationID, uint flags)
        {
            TeleportFinishPacket teleport = new TeleportFinishPacket();
            teleport.Info.AgentID = this.AgentID;
            teleport.Info.RegionHandle = regionHandle;
            teleport.Info.SimAccess = simAccess;
            teleport.Info.SeedCapability = new byte[0];

            System.Net.IPAddress oIP = System.Net.IPAddress.Parse(ipAddress);
            byte[] byteIP = oIP.GetAddressBytes();
            uint ip = (uint)byteIP[3] << 24;
            ip += (uint)byteIP[2] << 16;
            ip += (uint)byteIP[1] << 8;
            ip += (uint)byteIP[0];

            teleport.Info.SimIP = ip;
            teleport.Info.SimPort = ipPort;
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

        #endregion

        #region Avatar Packet/data sending Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="avatarID"></param>
        /// <param name="avatarLocalID"></param>
        /// <param name="Pos"></param>
        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos)
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            //send a objectupdate packet with information about the clients avatar

            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
            objupdate.ObjectData[0] = this.CreateDefaultAvatarPacket();
            //give this avatar object a local id and assign the user a name

            objupdate.ObjectData[0].ID = avatarLocalID;
            objupdate.ObjectData[0].FullID = avatarID;
            objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " + lastName + " \0");
            libsecondlife.LLVector3 pos2 = new LLVector3((float)Pos.X, (float)Pos.Y, (float)Pos.Z);
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

        /// <summary>
        /// Sends a full ObjectUpdatePacket to a client to inform it of a new primitive 
        /// or big changes to a existing primitive.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="primData"></param>
        /// <param name="pos"></param>
        /// <param name="rotation"></param>
        /// <param name="textureID"></param>
        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLQuaternion rotation, LLUUID textureID)
        {
            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            outPacket.ObjectData[0] = this.CreatePrimUpdateBlock(primData, textureID);
            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = primData.FullID;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);
            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, outPacket.ObjectData[0].ObjectData, 48, rot.Length);
            OutPacket(outPacket);
        }

        /// <summary>
        /// Sends a full ObjectUpdatePacket to a client to inform it of a new primitive 
        /// or big changes to a existing primitive.
        /// Uses default rotation
        /// </summary>
        /// <param name="primData"></param>
        /// <param name="pos"></param>
        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLUUID textureID)
        {
            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            outPacket.ObjectData[0] = this.CreatePrimUpdateBlock(primData, textureID);
            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = primData.FullID;
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
            Axiom.MathLib.Vector3 internDirec = new Axiom.MathLib.Vector3(0, 0, 0);

            internDirec = new Axiom.MathLib.Vector3(velocity.X, velocity.Y, velocity.Z);

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
        protected ObjectUpdatePacket.ObjectDataBlock CreatePrimUpdateBlock(PrimData primData, LLUUID textureID)
        {
            ObjectUpdatePacket.ObjectDataBlock objupdate = new ObjectUpdatePacket.ObjectDataBlock();
            this.SetDefaultPrimPacketValues(objupdate);
            objupdate.UpdateFlags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456;
            this.SetPrimPacketShapeData(objupdate, primData, textureID);

            return objupdate;
        }

        /// <summary>
        /// Copy the data from a PrimData object to a ObjectUpdatePacket
        /// </summary>
        /// <param name="objectData"></param>
        /// <param name="primData"></param>
        protected void SetPrimPacketShapeData(ObjectUpdatePacket.ObjectDataBlock objectData, PrimData primData, LLUUID textureID)
        {
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(textureID);
            objectData.TextureEntry = ntex.ToBytes();
            objectData.OwnerID = primData.OwnerID;
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
            objectData.ParentID = primData.ParentID;
            objectData.ProfileHollow = primData.ProfileHollow;
            objectData.PathRadiusOffset = primData.PathRadiusOffset;
            objectData.PathRevolutions = primData.PathRevolutions;
            objectData.PathTaperX = primData.PathTaperX;
            objectData.PathTaperY = primData.PathTaperY;
            objectData.PathTwist = primData.PathTwist;
            objectData.PathTwistBegin = primData.PathTwistBegin;
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
        protected ObjectUpdatePacket.ObjectDataBlock CreateDefaultAvatarPacket()
        {
            libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new ObjectUpdatePacket.ObjectDataBlock(); //  new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);

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
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            libsecondlife.LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
            pos.X = 100f;
            objdata.ID = 8880000;
            objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
            libsecondlife.LLVector3 pos2 = new LLVector3(100f, 100f, 23f);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <returns></returns>
        protected PrimData CreatePrimFromObjectAdd(ObjectAddPacket addPacket)
        {
            PrimData PData = new PrimData();
            PData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            PData.PCode = addPacket.ObjectData.PCode;
            PData.PathBegin = addPacket.ObjectData.PathBegin;
            PData.PathEnd = addPacket.ObjectData.PathEnd;
            PData.PathScaleX = addPacket.ObjectData.PathScaleX;
            PData.PathScaleY = addPacket.ObjectData.PathScaleY;
            PData.PathShearX = addPacket.ObjectData.PathShearX;
            PData.PathShearY = addPacket.ObjectData.PathShearY;
            PData.PathSkew = addPacket.ObjectData.PathSkew;
            PData.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            PData.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            PData.Scale = addPacket.ObjectData.Scale;
            PData.PathCurve = addPacket.ObjectData.PathCurve;
            PData.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            PData.ParentID = 0;
            PData.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            PData.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            PData.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            PData.PathTaperX = addPacket.ObjectData.PathTaperX;
            PData.PathTaperY = addPacket.ObjectData.PathTaperY;
            PData.PathTwist = addPacket.ObjectData.PathTwist;
            PData.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;

            return PData;
        }
        #endregion

    }
}
