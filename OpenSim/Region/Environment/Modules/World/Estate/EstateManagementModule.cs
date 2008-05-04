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
using System.Reflection;

using libsecondlife;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using Nini.Config;
using log4net;

namespace OpenSim.Region.Environment.Modules.World.Estate
{
    public class EstateManagementModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        


        #region Packet Data Responders
        private void sendDetailedEstateData(IClientAPI remote_client, LLUUID invoice)
        {

            remote_client.sendDetailedEstateData(invoice);
            remote_client.sendEstateManagersList(invoice);

        }

        private void estateSetRegionInfoHandler(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor, int matureLevel, bool restrictPushObject, bool allowParcelChanges)
        {
            
            m_scene.RegionInfo.EstateSettings.regionFlags = Simulator.RegionFlags.None;

            if (blockTerraform)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.BlockTerraform;
            }

            if (noFly)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.NoFly;
            }

            if (allowDamage)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.AllowDamage;
            }

            if (blockLandResell)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.BlockLandResell;
            }

            m_scene.RegionInfo.EstateSettings.maxAgents = (byte)maxAgents;

            m_scene.RegionInfo.EstateSettings.objectBonusFactor = objectBonusFactor;

            m_scene.RegionInfo.EstateSettings.simAccess = (Simulator.SimAccess)matureLevel;


            if (restrictPushObject)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.RestrictPushObject;
            }

            if (allowParcelChanges)
            {
                m_scene.RegionInfo.EstateSettings.regionFlags = m_scene.RegionInfo.EstateSettings.regionFlags |
                                                       Simulator.RegionFlags.AllowParcelChanges;
            }

            sendRegionInfoPacketToAll();
            
        }

        public void setEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, LLUUID texture)
        {
            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.EstateSettings.terrainBase0 = texture;
                    break;
                case 1:
                    m_scene.RegionInfo.EstateSettings.terrainBase1 = texture;
                    break;
                case 2:
                    m_scene.RegionInfo.EstateSettings.terrainBase2 = texture;
                    break;
                case 3:
                    m_scene.RegionInfo.EstateSettings.terrainBase3 = texture;
                    break;
            }
        }

        public void setEstateTerrainDetailTexture(IClientAPI client, int corner, LLUUID textureUUID)
        {
            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.EstateSettings.terrainDetail0 = textureUUID;
                    break;
                case 1:
                    m_scene.RegionInfo.EstateSettings.terrainDetail1 = textureUUID;
                    break;
                case 2:
                    m_scene.RegionInfo.EstateSettings.terrainDetail2 = textureUUID;
                    break;
                case 3:
                    m_scene.RegionInfo.EstateSettings.terrainDetail3 = textureUUID;
                    break;
            }
        }

        public void setEstateTerrainTextureHeights(IClientAPI client, int corner, float lowValue, float highValue)
        {
            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.EstateSettings.terrainStartHeight0 = lowValue;
                    m_scene.RegionInfo.EstateSettings.terrainHeightRange0 = highValue;
                    break;
                case 1:
                    m_scene.RegionInfo.EstateSettings.terrainStartHeight1 = lowValue;
                    m_scene.RegionInfo.EstateSettings.terrainHeightRange1 = highValue;
                    break;
                case 2:
                    m_scene.RegionInfo.EstateSettings.terrainStartHeight2 = lowValue;
                    m_scene.RegionInfo.EstateSettings.terrainHeightRange2 = highValue;
                    break;
                case 3:
                    m_scene.RegionInfo.EstateSettings.terrainStartHeight3 = lowValue;
                    m_scene.RegionInfo.EstateSettings.terrainHeightRange3 = highValue;
                    break;
            }
        }

        private void handleCommitEstateTerrainTextureRequest(IClientAPI remoteClient)
        {
            sendRegionHandshakeToAll();
        }

        public void setRegionTerrainSettings(float WaterHeight, float TerrainRaiseLimit, float TerrainLowerLimit,
                                      bool UseFixedSun, float SunHour)
        {
            // Water Height
            m_scene.RegionInfo.EstateSettings.waterHeight = WaterHeight;

            // Terraforming limits
            m_scene.RegionInfo.EstateSettings.terrainRaiseLimit = TerrainRaiseLimit;
            m_scene.RegionInfo.EstateSettings.terrainLowerLimit = TerrainLowerLimit;

            // Time of day / fixed sun
            m_scene.RegionInfo.EstateSettings.useFixedSun = UseFixedSun;
            m_scene.RegionInfo.EstateSettings.sunHour = SunHour;

            sendRegionInfoPacketToAll();
        }

        private void handleEstateRestartSimRequest(IClientAPI remoteClient, int timeInSeconds)
        {
            m_scene.Restart(timeInSeconds);
        }

        private void handleChangeEstateCovenantRequest(IClientAPI remoteClient, LLUUID estateCovenantID)
        {

            m_scene.RegionInfo.CovenantID = estateCovenantID;
            m_scene.RegionInfo.SaveEstatecovenantUUID(estateCovenantID);

        }

        private void handleEstateAccessDeltaRequest(IClientAPI remote_client, LLUUID invoice, int estateAccessType, LLUUID user)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.

            switch (estateAccessType)
            {
                case 256:

                    // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                    // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                    if (remote_client.AgentId == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.PermissionsMngr.BypassPermissions)
                    {
                        m_scene.RegionInfo.EstateSettings.AddEstateManager(user);
                        remote_client.sendEstateManagersList(invoice);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }

                    break;
                case 512:
                    // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                    // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                    if (remote_client.AgentId == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.PermissionsMngr.BypassPermissions)
                    {
                        m_scene.RegionInfo.EstateSettings.RemoveEstateManager(user);
                        remote_client.sendEstateManagersList(invoice);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }
                    break;

                default:

                    m_log.Error("EstateOwnerMessage: Unknown EstateAccessType requested in estateAccessDelta");
                    break;
            }
        }

        private void SendSimulatorBlueBoxMessage(IClientAPI remote_client, LLUUID invoice, LLUUID senderID, LLUUID sessionID, string senderName, string message)
        {       
            m_scene.SendRegionMessageFromEstateTools(senderID, sessionID, senderName, message);

        }
        private void SendEstateBlueBoxMessage(IClientAPI remote_client, LLUUID invoice, LLUUID senderID, LLUUID sessionID, string senderName, string message)
        {
            m_scene.SendEstateMessageFromEstateTools(senderID, sessionID, senderName, message);
        }

        private void handleEstateDebugRegionRequest(IClientAPI remote_client, LLUUID invoice, LLUUID senderID, bool scripted, bool collisionEvents, bool physics)
        {
            

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

        private void handleEstateTeleportOneUserHomeRequest(IClientAPI remover_client, LLUUID invoice, LLUUID senderID, LLUUID prey)
        {
            
            if (prey != LLUUID.Zero)
            {
                ScenePresence s = m_scene.GetScenePresence(prey);
                if (s != null)
                {
                    m_scene.TeleportClientHome(prey, s.ControllingClient);
                }
            }
        }

        private void HandleRegionInfoRequest(IClientAPI remote_client)
        {
            remote_client.sendRegionInfoToEstateMenu();
        }

        private void HandleEstateCovenantRequest(IClientAPI remote_client)
        {
            remote_client.sendEstateCovenantInformation();
        }


#endregion


        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();

            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].ControllingClient.sendRegionInfoToEstateMenu();
            }
        }
        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            remoteClient.SendRegionHandshake(m_scene.RegionInfo);
        }

        public void sendRegionHandshakeToAll()
        {
            m_scene.Broadcast(
                sendRegionHandshake
                );
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            m_scene.EventManager.OnRequestChangeWaterHeight += changeWaterHeight;
            
        }

        void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnDetailedEstateDataRequest += sendDetailedEstateData;
            client.OnSetEstateFlagsRequest += estateSetRegionInfoHandler;
            client.OnSetEstateTerrainBaseTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainDetailTexture += setEstateTerrainDetailTexture;
            client.OnSetEstateTerrainTextureHeights += setEstateTerrainTextureHeights;
            client.OnCommitEstateTerrainTextureRequest += handleCommitEstateTerrainTextureRequest;
            client.OnSetRegionTerrainSettings += setRegionTerrainSettings;
            client.OnEstateRestartSimRequest += handleEstateRestartSimRequest;
            client.OnEstateChangeCovenantRequest += handleChangeEstateCovenantRequest;
            client.OnUpdateEstateAccessDeltaRequest += handleEstateAccessDeltaRequest;
            client.OnSimulatorBlueBoxMessageRequest += SendSimulatorBlueBoxMessage;
            client.OnEstateBlueBoxMessageRequest += SendEstateBlueBoxMessage;
            client.OnEstateDebugRegionRequest += handleEstateDebugRegionRequest;
            client.OnEstateTeleportOneUserHomeRequest += handleEstateTeleportOneUserHomeRequest;

            client.OnRegionInfoRequest += HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += HandleEstateCovenantRequest;
            sendRegionHandshake(client);
        }


        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EstateManagementModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Other Functions

        public void changeWaterHeight(float height)
        {
            setRegionTerrainSettings(height, m_scene.RegionInfo.EstateSettings.terrainRaiseLimit, m_scene.RegionInfo.EstateSettings.terrainLowerLimit, m_scene.RegionInfo.EstateSettings.useFixedSun, m_scene.RegionInfo.EstateSettings.sunHour);
            sendRegionInfoPacketToAll();
        }
        #endregion

    }
}
