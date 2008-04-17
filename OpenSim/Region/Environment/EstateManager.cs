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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment
{
    /// <summary>
    /// Processes requests regarding estates. Refer to EstateSettings.cs in OpenSim.Framework. Types for all of the core settings
    /// </summary>
    public class EstateManager
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private RegionInfo m_regInfo;

        public enum EstateAccessCodex : uint
        {
            AccessOptions = 17, 
            AllowedGroups = 18,
            EstateBans = 20,
            EstateManagers = 24
        }


        public EstateManager(Scene scene, RegionInfo reginfo)
        {
            m_scene = scene;
            m_regInfo = reginfo;
        }

        private bool convertParamStringToBool(byte[] field)
        {
            string s = Helpers.FieldToUTF8String(field);
            if (s == "1" || s.ToLower() == "y" || s.ToLower() == "yes" || s.ToLower() == "t" || s.ToLower() == "true")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets terrain texture heights for each of the four corners of the region - textures are distributed as a linear range between the two heights.
        /// </summary>
        /// <param name="corner">Which corner</param>
        /// <param name="lowValue">Minimum height that texture range should cover</param>
        /// <param name="highValue">Maximum height that texture range should cover</param>
        public void setEstateTextureRange(Int16 corner, float lowValue, float highValue)
        {
            switch (corner)
            {
                case 0:
                    m_regInfo.EstateSettings.terrainStartHeight0 = lowValue;
                    m_regInfo.EstateSettings.terrainHeightRange0 = highValue;
                    break;
                case 1:
                    m_regInfo.EstateSettings.terrainStartHeight1 = lowValue;
                    m_regInfo.EstateSettings.terrainHeightRange1 = highValue;
                    break;
                case 2:
                    m_regInfo.EstateSettings.terrainStartHeight2 = lowValue;
                    m_regInfo.EstateSettings.terrainHeightRange2 = highValue;
                    break;
                case 3:
                    m_regInfo.EstateSettings.terrainStartHeight3 = lowValue;
                    m_regInfo.EstateSettings.terrainHeightRange3 = highValue;
                    break;
            }
        }

        /// <summary>
        /// Sets the 'detail' terrain texture on each of the bands.
        /// </summary>
        /// <param name="band">Which texture band</param>
        /// <param name="textureUUID">The UUID of the texture</param>
        public void setTerrainTexture(Int16 band, LLUUID textureUUID)
        {
            switch (band)
            {
                case 0:
                    m_regInfo.EstateSettings.terrainDetail0 = textureUUID;
                    break;
                case 1:
                    m_regInfo.EstateSettings.terrainDetail1 = textureUUID;
                    break;
                case 2:
                    m_regInfo.EstateSettings.terrainDetail2 = textureUUID;
                    break;
                case 3:
                    m_regInfo.EstateSettings.terrainDetail3 = textureUUID;
                    break;
            }
        }

        /// <summary>
        /// Sets common region settings
        /// </summary>
        /// <param name="WaterHeight">Water height of the waterplane (may not nessecarily be one value)</param>
        /// <param name="TerrainRaiseLimit">Maximum amount terrain can be raised from previous baking</param>
        /// <param name="TerrainLowerLimit">Minimum amount terrain can be lowered from previous baking</param>
        /// <param name="UseFixedSun">Use a fixed time of day on the sun?</param>
        /// <param name="SunHour">The offset hour of the day</param>
        public void setRegionSettings(float WaterHeight, float TerrainRaiseLimit, float TerrainLowerLimit,
                                      bool UseFixedSun, float SunHour)
        {
            // Water Height
            m_regInfo.EstateSettings.waterHeight = WaterHeight;

            // Terraforming limits
            m_regInfo.EstateSettings.terrainRaiseLimit = TerrainRaiseLimit;
            m_regInfo.EstateSettings.terrainLowerLimit = TerrainLowerLimit;

            // Time of day / fixed sun
            m_regInfo.EstateSettings.useFixedSun = UseFixedSun;
            m_regInfo.EstateSettings.sunHour = SunHour;
        }

        #region Packet Handlers

        public void handleEstateOwnerMessage(EstateOwnerMessagePacket packet, IClientAPI remote_client)
        {
            switch (Helpers.FieldToUTF8String(packet.MethodData.Method))
            {
                case "getinfo":

                    //System.Console.WriteLine("[ESTATE]: CLIENT--->" +  packet.ToString());
                    //sendRegionInfoPacketToAll();
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        sendDetailedEstateData(remote_client, packet);
                    }
                    break;
                case "setregioninfo":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                        estateSetRegionInfoHandler(packet);
                    break;
                case "texturebase":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                        estateTextureBaseHandler(packet);
                    break;
                case "texturedetail":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                        estateTextureDetailHandler(packet);
                    break;
                case "textureheights":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                        estateTextureHeightsHandler(packet);
                    break;
                case "texturecommit":
                    sendRegionHandshakeToAll();
                    break;
                case "setregionterrain":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                        estateSetRegionTerrainHandler(packet);
                    break;
                case "restart":
                    if (m_scene.PermissionsMngr.CanRestartSim(remote_client.AgentId))
                    {
                        estateRestartSim(packet);
                    }
                    break;
                case "estatechangecovenantid":
                    if (m_scene.PermissionsMngr.CanEditEstateTerrain(remote_client.AgentId))
                    {
                        EstateChangeCovenant(packet);
                    }
                    break;
                case "estateaccessdelta": // Estate access delta manages the banlist and allow list too.
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        estateAccessDelta(remote_client, packet);
                    }
                    break;
                case "simulatormessage": 
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        SendSimulatorBlueBoxMessage(remote_client, packet);
                    }
                    break;
                case "instantmessage": 
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        SendEstateBlueBoxMessage(remote_client, packet);
                    }
                    break;
                case "setregiondebug":
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        SetRegionDebug(remote_client, packet);
                    }
                    break;
                case "teleporthomeuser":
                    if (m_scene.PermissionsMngr.GenericEstatePermission(remote_client.AgentId))
                    {
                        TeleportOneUserHome(remote_client,packet);
                    }
                    break;
                default:
                    m_log.Error("EstateOwnerMessage: Unknown method requested\n" + packet.ToString());
                    break;
            }


        }

        private void TeleportOneUserHome(object remove_client,EstateOwnerMessagePacket packet)
        {
            LLUUID invoice = packet.MethodData.Invoice;
            LLUUID SenderID = packet.AgentData.AgentID;
            LLUUID Prey = LLUUID.Zero;

            Helpers.TryParse(Helpers.FieldToUTF8String(packet.ParamList[1].Parameter),out Prey);
            if (Prey != LLUUID.Zero)
            {
                ScenePresence s = m_scene.GetScenePresence(Prey);
                if (s != null)
                {
                    m_scene.TeleportClientHome(Prey, s.ControllingClient);
                }
            }
        }

        private void SetRegionDebug(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            LLUUID invoice = packet.MethodData.Invoice;
            LLUUID SenderID = packet.AgentData.AgentID;
            bool scripted = convertParamStringToBool(packet.ParamList[0].Parameter);
            bool collisionEvents = convertParamStringToBool(packet.ParamList[1].Parameter);
            bool physics = convertParamStringToBool(packet.ParamList[2].Parameter);
            
            if (physics)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags |= Simulator.RegionFlags.SkipPhysics;
            }
            else
            {
                m_scene.RegionInfo.EstateSettings.regionFlags &= ~Simulator.RegionFlags.SkipPhysics;
            }

            if (scripted)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags |= Simulator.RegionFlags.SkipScripts;
            }
            else
            {
                m_scene.RegionInfo.EstateSettings.regionFlags &= ~Simulator.RegionFlags.SkipScripts;
            }


            m_scene.SetSceneCoreDebug(scripted, collisionEvents, physics);
        }

        private void SendSimulatorBlueBoxMessage(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            LLUUID invoice = packet.MethodData.Invoice;
            LLUUID SenderID = new LLUUID(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter));
            string SenderName = Helpers.FieldToUTF8String(packet.ParamList[3].Parameter);
            string Message = Helpers.FieldToUTF8String(packet.ParamList[4].Parameter);
            m_scene.SendRegionMessageFromEstateTools(SenderID, packet.AgentData.SessionID, SenderName, Message);
            
        }
        private void SendEstateBlueBoxMessage(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            LLUUID invoice = packet.MethodData.Invoice;
            LLUUID SenderID = packet.AgentData.AgentID;
            string SenderName = Helpers.FieldToUTF8String(packet.ParamList[0].Parameter);
            string Message = Helpers.FieldToUTF8String(packet.ParamList[1].Parameter);
            m_scene.SendEstateMessageFromEstateTools(SenderID, packet.AgentData.SessionID, SenderName, Message);

        }
        private void sendDetailedEstateData(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            
            LLUUID invoice = packet.MethodData.Invoice;
            packet.AgentData.TransactionID = LLUUID.Random();
            packet.MethodData.Method = Helpers.StringToField("estateupdateinfo");
            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[9];

            for (int i = 0; i < 9; i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }

            //Sending Estate Settings
            returnblock[0].Parameter = Helpers.StringToField(m_scene.RegionInfo.EstateSettings.estateName);
            returnblock[1].Parameter = Helpers.StringToField(m_scene.RegionInfo.MasterAvatarAssignedUUID.ToString());
            returnblock[2].Parameter = Helpers.StringToField(m_scene.RegionInfo.EstateSettings.estateID.ToString());
            
            // TODO: Resolve Magic numbers here
            returnblock[3].Parameter = Helpers.StringToField("269516800");
            returnblock[4].Parameter = Helpers.StringToField("0");
            returnblock[5].Parameter = Helpers.StringToField("1");
            returnblock[6].Parameter = Helpers.StringToField(m_scene.RegionInfo.RegionID.ToString());
            returnblock[7].Parameter = Helpers.StringToField("1160895077");
            returnblock[8].Parameter = Helpers.StringToField("1");

            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            //System.Console.WriteLine("[ESTATE]: SIM--->" + packet.ToString());
            remote_client.OutPacket(packet, ThrottleOutPacketType.Task);

            sendEstateManagerList(remote_client, packet);
            
        }

        private void sendEstateManagerList(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            LLUUID invoice = packet.MethodData.Invoice;
            
            //Sending Estate Managers
            packet = new EstateOwnerMessagePacket();
            packet.AgentData.TransactionID = LLUUID.Random();
            packet.AgentData.AgentID = remote_client.AgentId;
            packet.AgentData.SessionID = remote_client.SessionId;
            packet.MethodData.Invoice = invoice;
            packet.MethodData.Method = Helpers.StringToField("setaccess");

            LLUUID[] EstateManagers = m_scene.RegionInfo.EstateSettings.estateManagers;

            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[6 + EstateManagers.Length];

            for (int i = 0; i < (6 + EstateManagers.Length); i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }
            int j = 0;
            
            returnblock[j].Parameter = Helpers.StringToField(m_scene.RegionInfo.EstateSettings.estateID.ToString()); j++;
            returnblock[j].Parameter = Helpers.StringToField(((int)EstateAccessCodex.EstateManagers).ToString()); j++;
            returnblock[j].Parameter = Helpers.StringToField("0"); j++;
            returnblock[j].Parameter = Helpers.StringToField("0"); j++;
            returnblock[j].Parameter = Helpers.StringToField("0"); j++;
            returnblock[j].Parameter = Helpers.StringToField(EstateManagers.Length.ToString()); j++;
            for (int i = 0; i < EstateManagers.Length; i++)
            {
                returnblock[j].Parameter = EstateManagers[i].GetBytes(); j++;
            }
            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            //System.Console.WriteLine("[ESTATE]: SIM--->" + packet.ToString());
            remote_client.OutPacket(packet, ThrottleOutPacketType.Task);
        }

        private void estateAccessDelta(IClientAPI remote_client, EstateOwnerMessagePacket packet)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.
            int estateAccessType = Convert.ToInt16(Helpers.FieldToUTF8String(packet.ParamList[1].Parameter));

            switch (estateAccessType)
            {
                case 256:
            
                // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                if (packet.AgentData.AgentID == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.PermissionsMngr.BypassPermissions)
                {
                    m_scene.RegionInfo.EstateSettings.AddEstateManager(new LLUUID(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter)));
                    sendEstateManagerList(remote_client, packet);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
                
                break;
                case 512:
                    // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                    // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                    if (packet.AgentData.AgentID == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.PermissionsMngr.BypassPermissions)
                    {
                        m_scene.RegionInfo.EstateSettings.RemoveEstateManager(new LLUUID(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter)));
                        sendEstateManagerList(remote_client, packet);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }
                    break;

            default:
            
                m_log.Error("EstateOwnerMessage: Unknown EstateAccessType requested in estateAccessDelta\n" + packet.ToString());
                break;
            }
            //m_log.Error("EstateOwnerMessage: estateAccessDelta\n" + packet.ToString());     


        }
        private void estateSetRegionInfoHandler(EstateOwnerMessagePacket packet)
        {
            if (packet.ParamList.Length != 9)
            {
                m_log.Error("EstateOwnerMessage: SetRegionInfo method has a ParamList of invalid length");
            }
            else
            {
                m_regInfo.EstateSettings.regionFlags = Simulator.RegionFlags.None;

                if (convertParamStringToBool(packet.ParamList[0].Parameter))
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.BlockTerraform;
                }

                if (convertParamStringToBool(packet.ParamList[1].Parameter))
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.NoFly;
                }

                if (convertParamStringToBool(packet.ParamList[2].Parameter))
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.AllowDamage;
                }

                if (convertParamStringToBool(packet.ParamList[3].Parameter) == false)
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.BlockLandResell;
                }


                int tempMaxAgents =
                    Convert.ToInt16(Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[4].Parameter)));
                m_regInfo.EstateSettings.maxAgents = (byte) tempMaxAgents;

                float tempObjectBonusFactor =
                    (float) Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));
                m_regInfo.EstateSettings.objectBonusFactor = tempObjectBonusFactor;

                int tempMatureLevel = Convert.ToInt16(Helpers.FieldToUTF8String(packet.ParamList[6].Parameter));
                m_regInfo.EstateSettings.simAccess = (Simulator.SimAccess) tempMatureLevel;


                if (convertParamStringToBool(packet.ParamList[7].Parameter))
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.RestrictPushObject;
                }

                if (convertParamStringToBool(packet.ParamList[8].Parameter))
                {
                    m_regInfo.EstateSettings.regionFlags = m_regInfo.EstateSettings.regionFlags |
                                                           Simulator.RegionFlags.AllowParcelChanges;
                }

                sendRegionInfoPacketToAll();
            }
        }

        private void estateSetRegionTerrainHandler(EstateOwnerMessagePacket packet)
        {
            if (packet.ParamList.Length != 9)
            {
                m_log.Error("EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
            }
            else
            {
                try
                {
                    string tmp;
                    tmp = Helpers.FieldToUTF8String(packet.ParamList[0].Parameter);
                    if (!tmp.Contains(".")) tmp += ".00";
                    float WaterHeight = (float)Convert.ToDecimal(tmp);
                    tmp = Helpers.FieldToUTF8String(packet.ParamList[1].Parameter);
                    if (!tmp.Contains(".")) tmp += ".00";
                    float TerrainRaiseLimit = (float)Convert.ToDecimal(tmp);
                    tmp = Helpers.FieldToUTF8String(packet.ParamList[2].Parameter);
                    if (!tmp.Contains(".")) tmp += ".00";
                    float TerrainLowerLimit = (float)Convert.ToDecimal(tmp);
                    bool UseFixedSun = convertParamStringToBool(packet.ParamList[4].Parameter);
                    float SunHour = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));

                    setRegionSettings(WaterHeight, TerrainRaiseLimit, TerrainLowerLimit, UseFixedSun, SunHour);

                    sendRegionInfoPacketToAll();
                }
                catch (Exception ex)
                {
                    m_log.Error("EstateManager: Exception while setting terrain settings: \n" + packet.ToString() + "\n" + ex.ToString());
                }
            }
        }

        private void estateTextureHeightsHandler(EstateOwnerMessagePacket packet)
        {
            foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
            {
                string s = Helpers.FieldToUTF8String(block.Parameter);
                string[] splitField = s.Split(' ');
                if (splitField.Length == 3)
                {
                    Int16 corner = Convert.ToInt16(splitField[0]);
                    float lowValue = (float) Convert.ToDecimal(splitField[1]);
                    float highValue = (float) Convert.ToDecimal(splitField[2]);

                    setEstateTextureRange(corner, lowValue, highValue);
                }
            }
        }

        private void estateTextureDetailHandler(EstateOwnerMessagePacket packet)
        {
            foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
            {
                string s = Helpers.FieldToUTF8String(block.Parameter);
                string[] splitField = s.Split(' ');
                if (splitField.Length == 2)
                {
                    Int16 corner = Convert.ToInt16(splitField[0]);
                    LLUUID textureUUID = new LLUUID(splitField[1]);

                    setTerrainTexture(corner, textureUUID);
                }
            }
        }

        private void estateTextureBaseHandler(EstateOwnerMessagePacket packet)
        {
            foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
            {
                string s = Helpers.FieldToUTF8String(block.Parameter);
                string[] splitField = s.Split(' ');
                if (splitField.Length == 2)
                {
                    LLUUID tempUUID = new LLUUID(splitField[1]);
                    switch (Convert.ToInt16(splitField[0]))
                    {
                        case 0:
                            m_regInfo.EstateSettings.terrainBase0 = tempUUID;
                            break;
                        case 1:
                            m_regInfo.EstateSettings.terrainBase1 = tempUUID;
                            break;
                        case 2:
                            m_regInfo.EstateSettings.terrainBase2 = tempUUID;
                            break;
                        case 3:
                            m_regInfo.EstateSettings.terrainBase3 = tempUUID;
                            break;
                    }
                }
            }
        }

        private void estateRestartSim(EstateOwnerMessagePacket packet)
        {
            // There's only 1 block in the estateResetSim..   and that's the number of seconds till restart.
            foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
            {
                float timeSeconds = 0;
                Helpers.TryParse(Helpers.FieldToUTF8String(block.Parameter), out timeSeconds);
                timeSeconds = (int)timeSeconds;
                m_scene.Restart(timeSeconds);
                
            }
        }

        private void EstateChangeCovenant(EstateOwnerMessagePacket packet)
        {
            foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
            {
                LLUUID newCovenantID = new LLUUID(Helpers.FieldToUTF8String(block.Parameter));
                m_regInfo.CovenantID = newCovenantID;
                m_scene.RegionInfo.SaveEstatecovenantUUID(newCovenantID);
            }
        }

        public void HandleRegionInfoRequest(IClientAPI client, LLUUID sessionID)
        {
            RegionInfoPacket rinfopack = new RegionInfoPacket();
            RegionInfoPacket.RegionInfoBlock rinfoblk = new RegionInfoPacket.RegionInfoBlock();
            rinfopack.AgentData.AgentID = client.AgentId;
            rinfopack.AgentData.SessionID = client.SessionId;
            rinfoblk.BillableFactor = m_regInfo.EstateSettings.billableFactor;
            rinfoblk.EstateID = m_regInfo.EstateSettings.estateID;
            rinfoblk.MaxAgents = m_regInfo.EstateSettings.maxAgents;
            rinfoblk.ObjectBonusFactor = m_regInfo.EstateSettings.objectBonusFactor;
            rinfoblk.ParentEstateID = m_regInfo.EstateSettings.parentEstateID;
            rinfoblk.PricePerMeter = m_regInfo.EstateSettings.pricePerMeter;
            rinfoblk.RedirectGridX = m_regInfo.EstateSettings.redirectGridX;
            rinfoblk.RedirectGridY = m_regInfo.EstateSettings.redirectGridY;
            rinfoblk.RegionFlags = (uint)( m_regInfo.EstateSettings.regionFlags);
            rinfoblk.SimAccess = (byte) m_regInfo.EstateSettings.simAccess;
            rinfoblk.SunHour = m_regInfo.EstateSettings.sunHour;
            rinfoblk.TerrainLowerLimit = m_regInfo.EstateSettings.terrainLowerLimit;
            rinfoblk.TerrainRaiseLimit = m_regInfo.EstateSettings.terrainRaiseLimit;
            rinfoblk.UseEstateSun = !m_regInfo.EstateSettings.useFixedSun;
            rinfoblk.WaterHeight = m_regInfo.EstateSettings.waterHeight;
            rinfoblk.SimName = Helpers.StringToField(m_regInfo.RegionName);

            rinfopack.RegionInfo = rinfoblk;

            client.OutPacket(rinfopack, ThrottleOutPacketType.Task);
        }

        public void HandleEstateCovenantRequest(IClientAPI client, LLUUID sessionID)
        {
            EstateCovenantReplyPacket einfopack = new EstateCovenantReplyPacket();
            EstateCovenantReplyPacket.DataBlock edata = new EstateCovenantReplyPacket.DataBlock();
            edata.CovenantID = m_regInfo.CovenantID;
            edata.CovenantTimestamp = 0;
            edata.EstateOwnerID = m_regInfo.MasterAvatarAssignedUUID;
            edata.EstateName =
                Helpers.StringToField(m_regInfo.MasterAvatarFirstName + " " + m_regInfo.MasterAvatarLastName);
            einfopack.Data = edata;
            client.OutPacket(einfopack, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();

            for (int i = 0; i < avatars.Count; i++)
            {
                sendRegionInfoPacket(avatars[i].ControllingClient);
            }
        }

        public void sendRegionHandshakeToAll()
        {
            m_scene.Broadcast(
                sendRegionHandshake
                );
        }

        public void sendRegionInfoPacket(IClientAPI remote_client)
        {
            AgentCircuitData circuitData = remote_client.RequestClientInfo();

            RegionInfoPacket regionInfoPacket = new RegionInfoPacket();
            regionInfoPacket.AgentData.AgentID = circuitData.AgentID;
            regionInfoPacket.AgentData.SessionID = circuitData.SessionID;
            regionInfoPacket.RegionInfo.BillableFactor = m_regInfo.EstateSettings.billableFactor;
            regionInfoPacket.RegionInfo.EstateID = m_regInfo.EstateSettings.estateID;
            regionInfoPacket.RegionInfo.MaxAgents = m_regInfo.EstateSettings.maxAgents;
            regionInfoPacket.RegionInfo.ObjectBonusFactor = m_regInfo.EstateSettings.objectBonusFactor;
            regionInfoPacket.RegionInfo.ParentEstateID = m_regInfo.EstateSettings.parentEstateID;
            regionInfoPacket.RegionInfo.PricePerMeter = m_regInfo.EstateSettings.pricePerMeter;
            regionInfoPacket.RegionInfo.RedirectGridX = m_regInfo.EstateSettings.redirectGridX;
            regionInfoPacket.RegionInfo.RedirectGridY = m_regInfo.EstateSettings.redirectGridY;
            regionInfoPacket.RegionInfo.RegionFlags = (uint)(m_regInfo.EstateSettings.regionFlags);
            regionInfoPacket.RegionInfo.SimAccess = (byte) m_regInfo.EstateSettings.simAccess;
            regionInfoPacket.RegionInfo.SimName = Helpers.StringToField(m_regInfo.RegionName);
            regionInfoPacket.RegionInfo.SunHour = m_regInfo.EstateSettings.sunHour;
            regionInfoPacket.RegionInfo.TerrainLowerLimit = m_regInfo.EstateSettings.terrainLowerLimit;
            regionInfoPacket.RegionInfo.TerrainRaiseLimit = m_regInfo.EstateSettings.terrainRaiseLimit;
            regionInfoPacket.RegionInfo.UseEstateSun = !m_regInfo.EstateSettings.useFixedSun;
            regionInfoPacket.RegionInfo.WaterHeight = m_regInfo.EstateSettings.waterHeight;
            

            remote_client.OutPacket(regionInfoPacket, ThrottleOutPacketType.Task);
        }

        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            remoteClient.SendRegionHandshake(m_regInfo);
        }

        #endregion
    }
}
