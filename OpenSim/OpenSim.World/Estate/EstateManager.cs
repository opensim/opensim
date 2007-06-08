using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Types;
using OpenSim.Framework.Interfaces;
using OpenSim.world;
using OpenSim;

using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.world.Estate
{

    /// <summary>
    /// Processes requests regarding estates. Refer to EstateSettings.cs in OpenSim.Framework. Types for all of the core settings
    /// </summary>
    public class EstateManager
    {
        private World m_world;

        public EstateManager(World world)
        {
            m_world = world; //Estate settings found at world.m_regInfo.estateSettings            
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

        public void handleEstateOwnerMessage(EstateOwnerMessagePacket packet, IClientAPI remote_client)
        {
            if (remote_client.AgentId == m_world.m_regInfo.MasterAvatarAssignedUUID)
            {
                switch (Helpers.FieldToUTF8String(packet.MethodData.Method))
                {
                    case "getinfo":
                        Console.WriteLine("GETINFO Requested");
                        this.sendRegionInfoPacketToAll();

                        break;
                    case "setregioninfo":
                        if (packet.ParamList.Length != 9)
                        {
                            OpenSim.Framework.Console.MainConsole.Instance.Error("EstateOwnerMessage: SetRegionInfo method has a ParamList of invalid length");
                        }
                        else
                        {
                            m_world.m_regInfo.estateSettings.regionFlags = libsecondlife.Simulator.RegionFlags.None;

                            if (convertParamStringToBool(packet.ParamList[0].Parameter))
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.BlockTerraform;
                            }

                            if (convertParamStringToBool(packet.ParamList[1].Parameter))
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.NoFly;
                            }

                            if (convertParamStringToBool(packet.ParamList[2].Parameter))
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.AllowDamage;
                            }

                            if (convertParamStringToBool(packet.ParamList[3].Parameter) == false)
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.BlockLandResell;
                            }


                            int tempMaxAgents = Convert.ToInt16(Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[4].Parameter)));
                            m_world.m_regInfo.estateSettings.maxAgents = (byte)tempMaxAgents;

                            float tempObjectBonusFactor = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));
                            m_world.m_regInfo.estateSettings.objectBonusFactor = tempObjectBonusFactor;

                            int tempMatureLevel = Convert.ToInt16(Helpers.FieldToUTF8String(packet.ParamList[6].Parameter));
                            m_world.m_regInfo.estateSettings.simAccess = (libsecondlife.Simulator.SimAccess)tempMatureLevel;
                            

                            if (convertParamStringToBool(packet.ParamList[7].Parameter))
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.RestrictPushObject;
                            }

                            if (convertParamStringToBool(packet.ParamList[8].Parameter))
                            {
                                m_world.m_regInfo.estateSettings.regionFlags = m_world.m_regInfo.estateSettings.regionFlags | libsecondlife.Simulator.RegionFlags.AllowParcelChanges;
                            }

                            sendRegionInfoPacketToAll();
                            
                        }
                        break;
                    case "texturebase":
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
                                        m_world.m_regInfo.estateSettings.terrainBase0 = tempUUID;
                                        break;
                                    case 1:
                                        m_world.m_regInfo.estateSettings.terrainBase1 = tempUUID;
                                        break;
                                    case 2:
                                        m_world.m_regInfo.estateSettings.terrainBase2 = tempUUID;
                                        break;
                                    case 3:
                                        m_world.m_regInfo.estateSettings.terrainBase3 = tempUUID;
                                        break;
                                }
                            }
                        }
                        break;
                    case "texturedetail":
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
                                        m_world.m_regInfo.estateSettings.terrainDetail0 = tempUUID;
                                        break;
                                    case 1:
                                        m_world.m_regInfo.estateSettings.terrainDetail1 = tempUUID;
                                        break;
                                    case 2:
                                        m_world.m_regInfo.estateSettings.terrainDetail2 = tempUUID;
                                        break;
                                    case 3:
                                        m_world.m_regInfo.estateSettings.terrainDetail3 = tempUUID;
                                        break;
                                }
                            }
                        }
                        break;
                    case "textureheights":
                        foreach (EstateOwnerMessagePacket.ParamListBlock block in packet.ParamList)
                        {

                            string s = Helpers.FieldToUTF8String(block.Parameter);
                            string[] splitField = s.Split(' ');
                            if (splitField.Length == 3)
                            {

                                float tempHeightLow = (float)Convert.ToDecimal(splitField[1]);
                                float tempHeightHigh = (float)Convert.ToDecimal(splitField[2]);

                                switch (Convert.ToInt16(splitField[0]))
                                {
                                    case 0:
                                        m_world.m_regInfo.estateSettings.terrainStartHeight0 = tempHeightLow;
                                        m_world.m_regInfo.estateSettings.terrainHeightRange0 = tempHeightHigh;
                                        break;
                                    case 1:
                                        m_world.m_regInfo.estateSettings.terrainStartHeight1 = tempHeightLow;
                                        m_world.m_regInfo.estateSettings.terrainHeightRange1 = tempHeightHigh;
                                        break;
                                    case 2:
                                        m_world.m_regInfo.estateSettings.terrainStartHeight2 = tempHeightLow;
                                        m_world.m_regInfo.estateSettings.terrainHeightRange2 = tempHeightHigh;
                                        break;
                                    case 3:
                                        m_world.m_regInfo.estateSettings.terrainStartHeight3 = tempHeightLow;
                                        m_world.m_regInfo.estateSettings.terrainHeightRange3 = tempHeightHigh;
                                        break;
                                }
                            }
                        }
                        break;
                    case "texturecommit":
                        sendRegionHandshakeToAll();
                        break;
                    case "setregionterrain":
                        if (packet.ParamList.Length != 9)
                        {
                            OpenSim.Framework.Console.MainConsole.Instance.Error("EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
                        }
                        else
                        {
                            m_world.m_regInfo.estateSettings.waterHeight = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[0].Parameter));
                            m_world.m_regInfo.estateSettings.terrainRaiseLimit = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[1].Parameter));
                            m_world.m_regInfo.estateSettings.terrainLowerLimit = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[2].Parameter));
                            m_world.m_regInfo.estateSettings.useFixedSun = this.convertParamStringToBool(packet.ParamList[4].Parameter);
                            m_world.m_regInfo.estateSettings.sunHour = (float)Convert.ToDecimal(Helpers.FieldToUTF8String(packet.ParamList[5].Parameter));

                            sendRegionInfoPacketToAll();
                        }
                        break;
                    default:
                        OpenSim.Framework.Console.MainConsole.Instance.Error("EstateOwnerMessage: Unknown method requested\n" + packet.ToString());
                        break;
                }
            }
        }

        public void sendRegionInfoPacketToAll()
        {
            foreach (OpenSim.world.Avatar av in m_world.Avatars.Values)
            {
                this.sendRegionInfoPacket(av.ControllingClient);
            }
        }

        public void sendRegionHandshakeToAll()
        {
            foreach (OpenSim.world.Avatar av in m_world.Avatars.Values)
            {
                this.sendRegionHandshake(av.ControllingClient);
            }
        }

        public void sendRegionInfoPacket(IClientAPI remote_client)
        {

            AgentCircuitData circuitData = remote_client.RequestClientInfo();

            RegionInfoPacket regionInfoPacket = new RegionInfoPacket();
            regionInfoPacket.AgentData.AgentID = circuitData.AgentID;
            regionInfoPacket.AgentData.SessionID = circuitData.SessionID;
            regionInfoPacket.RegionInfo.BillableFactor = m_world.m_regInfo.estateSettings.billableFactor;
            regionInfoPacket.RegionInfo.EstateID = m_world.m_regInfo.estateSettings.estateID;
            regionInfoPacket.RegionInfo.MaxAgents = m_world.m_regInfo.estateSettings.maxAgents;
            regionInfoPacket.RegionInfo.ObjectBonusFactor = m_world.m_regInfo.estateSettings.objectBonusFactor;
            regionInfoPacket.RegionInfo.ParentEstateID = m_world.m_regInfo.estateSettings.parentEstateID;
            regionInfoPacket.RegionInfo.PricePerMeter = m_world.m_regInfo.estateSettings.pricePerMeter;
            regionInfoPacket.RegionInfo.RedirectGridX = m_world.m_regInfo.estateSettings.redirectGridX;
            regionInfoPacket.RegionInfo.RedirectGridY = m_world.m_regInfo.estateSettings.redirectGridY;
            regionInfoPacket.RegionInfo.RegionFlags = (uint)m_world.m_regInfo.estateSettings.regionFlags;
            regionInfoPacket.RegionInfo.SimAccess = (byte)m_world.m_regInfo.estateSettings.simAccess;
            regionInfoPacket.RegionInfo.SimName = Helpers.StringToField(m_world.m_regInfo.RegionName);
            regionInfoPacket.RegionInfo.SunHour = m_world.m_regInfo.estateSettings.sunHour;
            regionInfoPacket.RegionInfo.TerrainLowerLimit = m_world.m_regInfo.estateSettings.terrainLowerLimit;
            regionInfoPacket.RegionInfo.TerrainRaiseLimit = m_world.m_regInfo.estateSettings.terrainRaiseLimit;
            regionInfoPacket.RegionInfo.UseEstateSun = !m_world.m_regInfo.estateSettings.useFixedSun;
            regionInfoPacket.RegionInfo.WaterHeight = m_world.m_regInfo.estateSettings.waterHeight;

            remote_client.OutPacket(regionInfoPacket);
        }

        public void sendRegionHandshake(IClientAPI remote_client)
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            RegionHandshakePacket handshake = new RegionHandshakePacket();

            handshake.RegionInfo.BillableFactor = m_world.m_regInfo.estateSettings.billableFactor;
            handshake.RegionInfo.IsEstateManager = false;
            handshake.RegionInfo.TerrainHeightRange00 = m_world.m_regInfo.estateSettings.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = m_world.m_regInfo.estateSettings.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = m_world.m_regInfo.estateSettings.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = m_world.m_regInfo.estateSettings.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = m_world.m_regInfo.estateSettings.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = m_world.m_regInfo.estateSettings.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = m_world.m_regInfo.estateSettings.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = m_world.m_regInfo.estateSettings.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = (byte)m_world.m_regInfo.estateSettings.simAccess;
            handshake.RegionInfo.WaterHeight = m_world.m_regInfo.estateSettings.waterHeight;


            handshake.RegionInfo.RegionFlags = (uint)m_world.m_regInfo.estateSettings.regionFlags;

            handshake.RegionInfo.SimName = _enc.GetBytes(m_world.m_regInfo.estateSettings.waterHeight + "\0");
            handshake.RegionInfo.SimOwner = m_world.m_regInfo.MasterAvatarAssignedUUID;
            handshake.RegionInfo.TerrainBase0 = m_world.m_regInfo.estateSettings.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = m_world.m_regInfo.estateSettings.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = m_world.m_regInfo.estateSettings.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = m_world.m_regInfo.estateSettings.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = m_world.m_regInfo.estateSettings.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = m_world.m_regInfo.estateSettings.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = m_world.m_regInfo.estateSettings.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = m_world.m_regInfo.estateSettings.terrainDetail3;
            handshake.RegionInfo.CacheID = LLUUID.Random(); //I guess this is for the client to remember an old setting?

            remote_client.OutPacket(handshake);
        }
    }
}
