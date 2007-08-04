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
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Scenes;
using Avatar = OpenSim.Region.Environment.Scenes.ScenePresence;


namespace OpenSim.Region.Environment
{

    /// <summary>
    /// Processes requests regarding estates. Refer to EstateSettings.cs in OpenSim.Framework. Types for all of the core settings
    /// </summary>
    public class EstateManager
    {
        private Scene m_scene;
        private RegionInfo m_regInfo;

        public EstateManager(Scene scene,RegionInfo reginfo)
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
                    m_regInfo.estateSettings.terrainStartHeight0 = lowValue;
                    m_regInfo.estateSettings.terrainHeightRange0 = highValue;
                    break;
                case 1:
                    m_regInfo.estateSettings.terrainStartHeight1 = lowValue;
                    m_regInfo.estateSettings.terrainHeightRange1 = highValue;
                    break;
                case 2:
                    m_regInfo.estateSettings.terrainStartHeight2 = lowValue;
                    m_regInfo.estateSettings.terrainHeightRange2 = highValue;
                    break;
                case 3:
                    m_regInfo.estateSettings.terrainStartHeight3 = lowValue;
                    m_regInfo.estateSettings.terrainHeightRange3 = highValue;
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
                    m_regInfo.estateSettings.terrainDetail0 = textureUUID;
                    break;
                case 1:
                    m_regInfo.estateSettings.terrainDetail1 = textureUUID;
                    break;
                case 2:
                    m_regInfo.estateSettings.terrainDetail2 = textureUUID;
                    break;
                case 3:
                    m_regInfo.estateSettings.terrainDetail3 = textureUUID;
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
        public void setRegionSettings(float WaterHeight, float TerrainRaiseLimit, float TerrainLowerLimit, bool UseFixedSun, float SunHour)
        {
            // Water Height
            m_regInfo.estateSettings.waterHeight = WaterHeight;
            m_scene.Terrain.watermap.Fill(WaterHeight);

            // Terraforming limits
            m_regInfo.estateSettings.terrainRaiseLimit = TerrainRaiseLimit;
            m_regInfo.estateSettings.terrainLowerLimit = TerrainLowerLimit;
            m_scene.Terrain.maxRaise = TerrainRaiseLimit;
            m_scene.Terrain.minLower = TerrainLowerLimit;

            // Time of day / fixed sun
            m_regInfo.estateSettings.useFixedSun = UseFixedSun;
            m_regInfo.estateSettings.sunHour = SunHour;
        }

        #region Packet Handlers


        public void handleEstateOwnerMessage(EstateOwnerMessagePacket packet, IClientAPI remote_client)
        {
            if (remote_client.AgentId == m_regInfo.MasterAvatarAssignedUUID)
            {
                switch (Helpers.FieldToUTF8String(packet.MethodData.Method))
                {
                    case "getinfo":
                        this.sendRegionInfoPacketToAll();
                        break;
                    case "setregioninfo":
                        estateSetRegionInfoHandler(packet);
                        break;
                    case "texturebase":
                        estateTextureBaseHandler(packet);
                        break;
                    case "texturedetail":
                        estateTextureDetailHandler(packet);
                        break;
                    case "textureheights":
                        estateTextureHeightsHandler(packet);
                        break;
                    case "texturecommit":
                        sendRegionHandshakeToAll();
                        break;
                    case "setregionterrain":
                        estateSetRegionTerrainHandler(packet);
                        break;
                    default:
                        MainLog.Instance.Error("EstateOwnerMessage: Unknown method requested\n" + packet.ToString());
                        break;
                }
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
                m_regInfo.estateSettings.regionFlags = Simulator.RegionFlags.None;

                if (convertParamStringToBool(packet.ParamList[0].Parameter))
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.BlockTerraform;
                }

                if (convertParamStringToBool(packet.ParamList[1].Parameter))
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.NoFly;
                }

                if (convertParamStringToBool(packet.ParamList[2].Parameter))
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.AllowDamage;
                }

                if (convertParamStringToBool(packet.ParamList[3].Parameter) == false)
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.BlockLandResell;
                }


                int tempMaxAgents = Convert.ToInt16(Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[4].Parameter)));
                m_regInfo.estateSettings.maxAgents = (byte)tempMaxAgents;

                float tempObjectBonusFactor = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));
                m_regInfo.estateSettings.objectBonusFactor = tempObjectBonusFactor;

                int tempMatureLevel = Convert.ToInt16(Helpers.FieldToUTF8String(packet.ParamList[6].Parameter));
                m_regInfo.estateSettings.simAccess = (Simulator.SimAccess)tempMatureLevel;


                if (convertParamStringToBool(packet.ParamList[7].Parameter))
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.RestrictPushObject;
                }

                if (convertParamStringToBool(packet.ParamList[8].Parameter))
                {
                    m_regInfo.estateSettings.regionFlags = m_regInfo.estateSettings.regionFlags | Simulator.RegionFlags.AllowParcelChanges;
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
                float WaterHeight = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[0].Parameter));
                float TerrainRaiseLimit = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[1].Parameter));
                float TerrainLowerLimit = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter));
                bool UseFixedSun = this.convertParamStringToBool(packet.ParamList[4].Parameter);
                float SunHour = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));

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
                    float lowValue = (float)Convert.ToDecimal(splitField[1]);
                    float highValue = (float)Convert.ToDecimal(splitField[2]);

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
                            m_regInfo.estateSettings.terrainBase0 = tempUUID;
                            break;
                        case 1:
                            m_regInfo.estateSettings.terrainBase1 = tempUUID;
                            break;
                        case 2:
                            m_regInfo.estateSettings.terrainBase2 = tempUUID;
                            break;
                        case 3:
                            m_regInfo.estateSettings.terrainBase3 = tempUUID;
                            break;
                    }
                }
            }
        }

        #endregion

        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
             List<Avatar> avatars = m_scene.RequestAvatarList();

             for (int i = 0; i < avatars.Count; i++)
             {
                 this.sendRegionInfoPacket(avatars[i].ControllingClient);
             }
        }

        public void sendRegionHandshakeToAll()
        {
            List<Avatar> avatars = m_scene.RequestAvatarList();

            for (int i = 0; i < avatars.Count; i++)
            {
                this.sendRegionHandshake(avatars[i].ControllingClient);
            }         
        }

        public void sendRegionInfoPacket(IClientAPI remote_client)
        {
            Encoding _enc = Encoding.ASCII;

            AgentCircuitData circuitData = remote_client.RequestClientInfo();

            RegionInfoPacket regionInfoPacket = new RegionInfoPacket();
            regionInfoPacket.AgentData.AgentID = circuitData.AgentID;
            regionInfoPacket.AgentData.SessionID = circuitData.SessionID;
            regionInfoPacket.RegionInfo.BillableFactor = m_regInfo.estateSettings.billableFactor;
            regionInfoPacket.RegionInfo.EstateID = m_regInfo.estateSettings.estateID;
            regionInfoPacket.RegionInfo.MaxAgents = m_regInfo.estateSettings.maxAgents;
            regionInfoPacket.RegionInfo.ObjectBonusFactor = m_regInfo.estateSettings.objectBonusFactor;
            regionInfoPacket.RegionInfo.ParentEstateID = m_regInfo.estateSettings.parentEstateID;
            regionInfoPacket.RegionInfo.PricePerMeter = m_regInfo.estateSettings.pricePerMeter;
            regionInfoPacket.RegionInfo.RedirectGridX = m_regInfo.estateSettings.redirectGridX;
            regionInfoPacket.RegionInfo.RedirectGridY = m_regInfo.estateSettings.redirectGridY;
            regionInfoPacket.RegionInfo.RegionFlags = (uint)m_regInfo.estateSettings.regionFlags;
            regionInfoPacket.RegionInfo.SimAccess = (byte)m_regInfo.estateSettings.simAccess;
            regionInfoPacket.RegionInfo.SimName = _enc.GetBytes( m_regInfo.RegionName);
            regionInfoPacket.RegionInfo.SunHour = m_regInfo.estateSettings.sunHour;
            regionInfoPacket.RegionInfo.TerrainLowerLimit = m_regInfo.estateSettings.terrainLowerLimit;
            regionInfoPacket.RegionInfo.TerrainRaiseLimit = m_regInfo.estateSettings.terrainRaiseLimit;
            regionInfoPacket.RegionInfo.UseEstateSun = !m_regInfo.estateSettings.useFixedSun;
            regionInfoPacket.RegionInfo.WaterHeight = m_regInfo.estateSettings.waterHeight;

            remote_client.OutPacket(regionInfoPacket);
        }

        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            remoteClient.SendRegionHandshake(m_regInfo);
        }

        #endregion

    }
}
