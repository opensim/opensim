using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Types;
using OpenSim.RegionServer.Simulator;
using OpenSim.RegionServer.Client;

using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.RegionServer.Estate
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

        public void handleEstateOwnerMessage(EstateOwnerMessagePacket packet, ClientView remote_client)
        {
            if (remote_client.AgentID == m_world.m_regInfo.MasterAvatarAssignedUUID)
            {
                switch (Helpers.FieldToUTF8String(packet.MethodData.Method))
                {
                    case "getinfo":
                        Console.WriteLine("GETINFO Requested");
                        RegionInfoPacket regionInfoPacket = new RegionInfoPacket();
                        regionInfoPacket.AgentData.AgentID = remote_client.AgentID;
                        regionInfoPacket.AgentData.SessionID = remote_client.SessionID;
                        regionInfoPacket.RegionInfo.BillableFactor = m_world.m_regInfo.estateSettings.billableFactor;
                        regionInfoPacket.RegionInfo.EstateID = m_world.m_regInfo.estateSettings.estateID;
                        regionInfoPacket.RegionInfo.MaxAgents = m_world.m_regInfo.estateSettings.maxAgents;
                        regionInfoPacket.RegionInfo.ObjectBonusFactor = m_world.m_regInfo.estateSettings.objectBonusFactor;
                        regionInfoPacket.RegionInfo.ParentEstateID = m_world.m_regInfo.estateSettings.parentEstateID;
                        regionInfoPacket.RegionInfo.PricePerMeter = m_world.m_regInfo.estateSettings.pricePerMeter;
                        regionInfoPacket.RegionInfo.RedirectGridX = m_world.m_regInfo.estateSettings.redirectGridX;
                        regionInfoPacket.RegionInfo.RedirectGridY = m_world.m_regInfo.estateSettings.redirectGridY;
                        regionInfoPacket.RegionInfo.RegionFlags = m_world.m_regInfo.estateSettings.regionFlags;
                        regionInfoPacket.RegionInfo.SimAccess = m_world.m_regInfo.estateSettings.simAccess;
                        regionInfoPacket.RegionInfo.SimName = Helpers.StringToField(m_world.m_regInfo.RegionName);
                        regionInfoPacket.RegionInfo.SunHour = m_world.m_regInfo.estateSettings.sunHour;
                        regionInfoPacket.RegionInfo.TerrainLowerLimit = m_world.m_regInfo.estateSettings.terrainLowerLimit;
                        regionInfoPacket.RegionInfo.TerrainRaiseLimit = m_world.m_regInfo.estateSettings.terrainRaiseLimit;
                        regionInfoPacket.RegionInfo.UseEstateSun = m_world.m_regInfo.estateSettings.useEstateSun;
                        regionInfoPacket.RegionInfo.WaterHeight = m_world.m_regInfo.estateSettings.waterHeight;

                        remote_client.OutPacket(regionInfoPacket);
                        break;
                    default:
                        OpenSim.Framework.Console.MainConsole.Instance.Error("EstateOwnerMessage: Unknown method requested\n" + packet.ToString());
                        break;
                }
            }
        }
    }
}
