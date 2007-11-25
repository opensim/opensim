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
        private Scene m_scene;
        private RegionInfo m_regInfo;

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
            m_scene.Terrain.watermap.Fill(WaterHeight);

            // Terraforming limits
            m_regInfo.EstateSettings.terrainRaiseLimit = TerrainRaiseLimit;
            m_regInfo.EstateSettings.terrainLowerLimit = TerrainLowerLimit;
            m_scene.Terrain.maxRaise = TerrainRaiseLimit;
            m_scene.Terrain.minLower = TerrainLowerLimit;

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
                    sendRegionInfoPacketToAll();
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
                default:
                    MainLog.Instance.Error("EstateOwnerMessage: Unknown method requested\n" + packet.ToString());
                    break;
            }
        }

        private void estateSetRegionInfoHandler(EstateOwnerMessagePacket packet)
        {
            if (packet.ParamList.Length != 9)
            {
                MainLog.Instance.Error("EstateOwnerMessage: SetRegionInfo method has a ParamList of invalid length");
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
                MainLog.Instance.Error("EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
            }
            else
            {
                float WaterHeight = (float) Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[0].Parameter));
                float TerrainRaiseLimit =
                    (float) Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[1].Parameter));
                float TerrainLowerLimit =
                    (float) Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter));
                bool UseFixedSun = convertParamStringToBool(packet.ParamList[4].Parameter);
                float SunHour = (float) Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));

                setRegionSettings(WaterHeight, TerrainRaiseLimit, TerrainLowerLimit, UseFixedSun, SunHour);

                sendRegionInfoPacketToAll();
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
                timeSeconds = BitConverter.ToInt16(block.Parameter, 1);
                timeSeconds = (int)((timeSeconds / 100) - 3);
                m_scene.Restart(timeSeconds);

            }
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
            Encoding _enc = Encoding.ASCII;

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
            regionInfoPacket.RegionInfo.RegionFlags = (uint) m_regInfo.EstateSettings.regionFlags;
            regionInfoPacket.RegionInfo.SimAccess = (byte) m_regInfo.EstateSettings.simAccess;
            regionInfoPacket.RegionInfo.SimName = _enc.GetBytes(m_regInfo.RegionName);
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