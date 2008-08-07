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
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Estate
{
    public class EstateManagementModule : IEstateModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private delegate void LookupUUIDS(List<LLUUID> uuidLst);

        private Scene m_scene;

        #region Packet Data Responders

        private void sendDetailedEstateData(IClientAPI remote_client, LLUUID invoice)
        {
        //SendDetailedEstateData(LLUUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, LLUUID covenant)

            uint sun = 0;
            if (!m_scene.RegionInfo.EstateSettings.UseGlobalTime)
                sun=(uint)(m_scene.RegionInfo.EstateSettings.SunPosition*1024.0) + 0x1800;
            remote_client.SendDetailedEstateData(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateName,
                    m_scene.RegionInfo.EstateSettings.EstateID,
                    m_scene.RegionInfo.EstateSettings.ParentEstateID,
                    GetEstateFlags(),
                    sun,
                    m_scene.RegionInfo.RegionSettings.Covenant,
                    m_scene.RegionInfo.EstateSettings.AbuseEmail);

            remote_client.SendEstateManagersList(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateManagers,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendBannedUserList(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateBans,
                    m_scene.RegionInfo.EstateSettings.EstateID);
        }

        private void estateSetRegionInfoHandler(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor,
                                                int matureLevel, bool restrictPushObject, bool allowParcelChanges)
        {
            if (blockTerraform)
                m_scene.RegionInfo.RegionSettings.BlockTerraform = true;
            else
                m_scene.RegionInfo.RegionSettings.BlockTerraform = false;

            if (noFly)
                m_scene.RegionInfo.RegionSettings.BlockFly = true;
            else
                m_scene.RegionInfo.RegionSettings.BlockFly = false;

            if (allowDamage)
                m_scene.RegionInfo.RegionSettings.AllowDamage = true;
            else
                m_scene.RegionInfo.RegionSettings.AllowDamage = false;

            if (blockLandResell)
                m_scene.RegionInfo.RegionSettings.AllowLandResell = false;
            else
                m_scene.RegionInfo.RegionSettings.AllowLandResell = true;

            m_scene.RegionInfo.RegionSettings.AgentLimit = (byte) maxAgents;

            m_scene.RegionInfo.RegionSettings.ObjectBonus = objectBonusFactor;

            if (matureLevel <= 13)
                m_scene.RegionInfo.RegionSettings.Maturity = 0;
            else
                m_scene.RegionInfo.RegionSettings.Maturity = 1;

            if (restrictPushObject)
                m_scene.RegionInfo.RegionSettings.RestrictPushing = true;
            else
                m_scene.RegionInfo.RegionSettings.RestrictPushing = false;

            if (allowParcelChanges)
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = true;
            else
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = false;

            m_scene.RegionInfo.RegionSettings.Save();

            sendRegionInfoPacketToAll();
        }

        public void setEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, LLUUID texture)
        {
            if (texture == LLUUID.Zero)
                return;

            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture1 = texture;
                    break;
                case 1:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture2 = texture;
                    break;
                case 2:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture3 = texture;
                    break;
                case 3:
                    m_scene.RegionInfo.RegionSettings.TerrainTexture4 = texture;
                    break;
            }
            m_scene.RegionInfo.RegionSettings.Save();
        }

        public void setEstateTerrainTextureHeights(IClientAPI client, int corner, float lowValue, float highValue)
        {
            switch (corner)
            {
                case 0:
                    m_scene.RegionInfo.RegionSettings.Elevation1SW = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2SW = highValue;
                    break;
                case 1:
                    m_scene.RegionInfo.RegionSettings.Elevation1NW = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2NW = highValue;
                    break;
                case 2:
                    m_scene.RegionInfo.RegionSettings.Elevation1SE = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2SE = highValue;
                    break;
                case 3:
                    m_scene.RegionInfo.RegionSettings.Elevation1NE = lowValue;
                    m_scene.RegionInfo.RegionSettings.Elevation2NE = highValue;
                    break;
            }
            m_scene.RegionInfo.RegionSettings.Save();
        }

        private void handleCommitEstateTerrainTextureRequest(IClientAPI remoteClient)
        {
            sendRegionHandshakeToAll();
        }

        public void setRegionTerrainSettings(float WaterHeight,
                float TerrainRaiseLimit, float TerrainLowerLimit,
                bool UseEstateSun, bool UseFixedSun, float SunHour,
                bool UseGlobal, bool EstateFixedSun, float EstateSunHour)
        {
            // Water Height
            m_scene.RegionInfo.RegionSettings.WaterHeight = WaterHeight;

            // Terraforming limits
            m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit = TerrainRaiseLimit;
            m_scene.RegionInfo.RegionSettings.TerrainLowerLimit = TerrainLowerLimit;

            // Time of day / fixed sun
            m_scene.RegionInfo.RegionSettings.UseEstateSun = UseEstateSun;
            m_scene.RegionInfo.RegionSettings.FixedSun = UseFixedSun;
            m_scene.RegionInfo.RegionSettings.SunPosition = SunHour;

            m_scene.EventManager.TriggerEstateToolsTimeUpdate(m_scene.RegionInfo.RegionHandle, UseFixedSun, UseEstateSun, SunHour);

            //m_log.Debug("[ESTATE]: UFS: " + UseFixedSun.ToString());
            //m_log.Debug("[ESTATE]: SunHour: " + SunHour.ToString());

            sendRegionInfoPacketToAll();
            m_scene.RegionInfo.RegionSettings.Save();
        }

        private void handleEstateRestartSimRequest(IClientAPI remoteClient, int timeInSeconds)
        {
            m_scene.Restart(timeInSeconds);
        }

        private void handleChangeEstateCovenantRequest(IClientAPI remoteClient, LLUUID estateCovenantID)
        {
            m_scene.RegionInfo.RegionSettings.Covenant = estateCovenantID;
            m_scene.RegionInfo.RegionSettings.Save();
        }

        private void handleEstateAccessDeltaRequest(IClientAPI remote_client, LLUUID invoice, int estateAccessType, LLUUID user)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.

            if (user == m_scene.RegionInfo.MasterAvatarAssignedUUID)
                return; // never process owner

            switch (estateAccessType)
            {
                case 64:
                    if (m_scene.ExternalChecks.ExternalChecksCanIssueEstateCommand(remote_client.AgentId) || m_scene.ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;
                        
                        bool alreadyInList = false;

                        for (int i = 0; i < banlistcheck.Length; i++)
                        {
                            if (user == banlistcheck[i].bannedUUID)
                            {
                                alreadyInList = true;
                                break;
                            }

                        }
                        if (!alreadyInList)
                        {

                            EstateBan item = new EstateBan();

                            item.bannedUUID = user;
                            item.estateID = m_scene.RegionInfo.EstateSettings.EstateID;
                            item.bannedIP = "0.0.0.0";
                            item.bannedIPHostMask = "0.0.0.0";

                            m_scene.RegionInfo.EstateSettings.AddBan(item);
                            m_scene.RegionInfo.EstateSettings.Save();

                            ScenePresence s = m_scene.GetScenePresence(user);
                            if (s != null)
                            {
                                if (!s.IsChildAgent)
                                    m_scene.TeleportClientHome(user, s.ControllingClient);
                            }

                        }
                        else
                        {
                            remote_client.SendAlertMessage("User is already on the region ban list");
                        }
                        //m_scene.RegionInfo.regionBanlist.Add(Manager(user);
                        remote_client.SendBannedUserList(invoice, m_scene.RegionInfo.EstateSettings.EstateBans, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }
                    break;
                case 128:
                    if (m_scene.ExternalChecks.ExternalChecksCanIssueEstateCommand(remote_client.AgentId) || m_scene.ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;

                        bool alreadyInList = false;
                        EstateBan listitem = null;

                        for (int i = 0; i < banlistcheck.Length; i++)
                        {
                            if (user == banlistcheck[i].bannedUUID)
                            {
                                alreadyInList = true;
                                listitem = banlistcheck[i];
                                break;
                            }

                        }
                        if (alreadyInList && listitem != null)
                        {
                            m_scene.RegionInfo.EstateSettings.RemoveBan(listitem.bannedUUID);
                            m_scene.RegionInfo.EstateSettings.Save();
                        }
                        else
                        {
                            remote_client.SendAlertMessage("User is not on the region ban list");
                        }
                        //m_scene.RegionInfo.regionBanlist.Add(Manager(user);
                        remote_client.SendBannedUserList(invoice, m_scene.RegionInfo.EstateSettings.EstateBans, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }
                    break;
                case 256:

                    // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                    // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                    if (remote_client.AgentId == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        m_scene.RegionInfo.EstateSettings.AddEstateManager(user);
                        m_scene.RegionInfo.EstateSettings.Save();
                        remote_client.SendEstateManagersList(invoice, m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }

                    break;
                case 512:
                    // This needs to be updated for SuperEstateOwnerUser..   a non existing user in the estatesettings.xml
                    // So make sure you really trust your region owners.   because they can add other estate manaagers to your other estates
                    if (remote_client.AgentId == m_scene.RegionInfo.MasterAvatarAssignedUUID || m_scene.ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        m_scene.RegionInfo.EstateSettings.RemoveEstateManager(user);
                        m_scene.RegionInfo.EstateSettings.Save();

                        remote_client.SendEstateManagersList(invoice, m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                    }
                    else
                    {
                        remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                    }
                    break;

                default:

                    m_log.ErrorFormat("EstateOwnerMessage: Unknown EstateAccessType requested in estateAccessDelta: {0}", estateAccessType.ToString());
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
                m_scene.RegionInfo.RegionSettings.DisablePhysics = true;
            else
                m_scene.RegionInfo.RegionSettings.DisablePhysics = false;

            if (scripted)
                m_scene.RegionInfo.RegionSettings.DisableScripts = true;
            else
                m_scene.RegionInfo.RegionSettings.DisableScripts = false;

            if (collisionEvents)
                m_scene.RegionInfo.RegionSettings.DisableCollisions = true;
            else
                m_scene.RegionInfo.RegionSettings.DisableCollisions = false;


            m_scene.RegionInfo.RegionSettings.Save();

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

           RegionInfoForEstateMenuArgs args = new RegionInfoForEstateMenuArgs();
           args.billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor;
           args.estateID = m_scene.RegionInfo.EstateSettings.EstateID;
           args.maxAgents = (byte)m_scene.RegionInfo.RegionSettings.AgentLimit;
           args.objectBonusFactor = (float)m_scene.RegionInfo.RegionSettings.ObjectBonus;
           args.parentEstateID = m_scene.RegionInfo.EstateSettings.ParentEstateID;
           args.pricePerMeter = m_scene.RegionInfo.EstateSettings.PricePerMeter;
           args.redirectGridX = m_scene.RegionInfo.EstateSettings.RedirectGridX;
           args.redirectGridY = m_scene.RegionInfo.EstateSettings.RedirectGridY;
           args.regionFlags = GetRegionFlags();
           byte mature = 13;
           if (m_scene.RegionInfo.RegionSettings.Maturity == 1)
              mature = 21;
           args.simAccess = mature;

           args.sunHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
           args.terrainLowerLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
           args.terrainRaiseLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;
           args.useEstateSun = m_scene.RegionInfo.RegionSettings.UseEstateSun;
           args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
           args.simName = m_scene.RegionInfo.RegionName;
           
           
           remote_client.SendRegionInfoToEstateMenu(args);
        }

        private void HandleEstateCovenantRequest(IClientAPI remote_client)
        {
            remote_client.SendEstateCovenantInformation(m_scene.RegionInfo.RegionSettings.Covenant);
        }
        private void HandleLandStatRequest(int parcelID, uint reportType, uint requestFlags, string filter, IClientAPI remoteClient)
        {
            Dictionary<uint, float> SceneData = new Dictionary<uint,float>();
            List<LLUUID> uuidNameLookupList = new List<LLUUID>();

            if (reportType == 1)
            {
                SceneData = m_scene.PhysicsScene.GetTopColliders();
            }
            else if (reportType == 0)
            {
                SceneData = m_scene.m_innerScene.GetTopScripts();
            }

            List<LandStatReportItem> SceneReport = new List<LandStatReportItem>();
            lock (SceneData)
            {
                foreach (uint obj in SceneData.Keys)
                {
                    SceneObjectPart prt = m_scene.GetSceneObjectPart(obj);
                    if (prt != null)
                    {
                        if (prt.ParentGroup != null)
                        {
                            SceneObjectGroup sog = prt.ParentGroup;
                            if (sog != null)
                            {
                                LandStatReportItem lsri = new LandStatReportItem();
                                lsri.LocationX = sog.AbsolutePosition.X;
                                lsri.LocationY = sog.AbsolutePosition.Y;
                                lsri.LocationZ = sog.AbsolutePosition.Z;
                                lsri.Score = SceneData[obj];
                                lsri.TaskID = sog.UUID;
                                lsri.TaskLocalID = sog.LocalId;
                                lsri.TaskName = sog.GetPartName(obj);
                                if (m_scene.CommsManager.UUIDNameCachedTest(sog.OwnerID))
                                {
                                    lsri.OwnerName = m_scene.CommsManager.UUIDNameRequestString(sog.OwnerID);
                                }
                                else
                                {
                                    lsri.OwnerName = "waiting";
                                    lock (uuidNameLookupList)
                                        uuidNameLookupList.Add(sog.OwnerID);
                                }

                                if (filter.Length != 0)
                                {
                                    if ((lsri.OwnerName.Contains(filter) || lsri.TaskName.Contains(filter)))
                                    {
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                
                                SceneReport.Add(lsri);
                            }
                        }
                    }

                }
            }
            remoteClient.SendLandStatReply(reportType, requestFlags, (uint)SceneReport.Count,SceneReport.ToArray());
            
            if (uuidNameLookupList.Count > 0)
                LookupUUID(uuidNameLookupList);
        }

        private void LookupUUIDSCompleted(IAsyncResult iar)
        {
            LookupUUIDS icon = (LookupUUIDS)iar.AsyncState;
            icon.EndInvoke(iar);
        }
        private void LookupUUID(List<LLUUID> uuidLst)
        {
            LookupUUIDS d = LookupUUIDsAsync;

            d.BeginInvoke(uuidLst,
                          LookupUUIDSCompleted,
                          d);
        }
        private void LookupUUIDsAsync(List<LLUUID> uuidLst)
        {
            LLUUID[] uuidarr = new LLUUID[0];
            
            lock (uuidLst)
            {
                uuidarr = uuidLst.ToArray();
            }

            for (int i = 0; i < uuidarr.Length; i++)
            {
                // string lookupname = m_scene.CommsManager.UUIDNameRequestString(uuidarr[i]);
                m_scene.CommsManager.UUIDNameRequestString(uuidarr[i]);
                // we drop it.  It gets cached though...  so we're ready for the next request.
            }
        }
        #endregion

        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
            List<ScenePresence> avatars = m_scene.GetAvatars();

            for (int i = 0; i < avatars.Count; i++)
            {
                HandleRegionInfoRequest(avatars[i].ControllingClient); ;
            }
        }

        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            RegionHandshakeArgs args = new RegionHandshakeArgs();
            bool estatemanager = false;
            LLUUID[] EstateManagers = m_scene.RegionInfo.EstateSettings.EstateManagers;
            for (int i = 0; i < EstateManagers.Length; i++)
            {
                if (EstateManagers[i] == remoteClient.AgentId)
                    estatemanager = true;
            }
            
            args.isEstateManager = estatemanager;

            args.billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor;
            args.terrainStartHeight0 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SW;
            args.terrainHeightRange0 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SW;
            args.terrainStartHeight1 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NW;
            args.terrainHeightRange1 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NW;
            args.terrainStartHeight2 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SE;
            args.terrainHeightRange2 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SE;
            args.terrainStartHeight3 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NE;
            args.terrainHeightRange3 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NE;
            byte mature = 13;
            if (m_scene.RegionInfo.RegionSettings.Maturity == 1)
                mature = 21;
            args.simAccess = mature;
            args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            args.regionFlags = GetRegionFlags();
            args.regionName = m_scene.RegionInfo.RegionName;
            args.SimOwner = m_scene.RegionInfo.MasterAvatarAssignedUUID;
            args.terrainBase0 = LLUUID.Zero;
            args.terrainBase1 = LLUUID.Zero;
            args.terrainBase2 = LLUUID.Zero;
            args.terrainBase3 = LLUUID.Zero;
            args.terrainDetail0 = m_scene.RegionInfo.RegionSettings.TerrainTexture1;
            args.terrainDetail1 = m_scene.RegionInfo.RegionSettings.TerrainTexture2;
            args.terrainDetail2 = m_scene.RegionInfo.RegionSettings.TerrainTexture3;
            args.terrainDetail3 = m_scene.RegionInfo.RegionSettings.TerrainTexture4;

            remoteClient.SendRegionHandshake(m_scene.RegionInfo,args);
        }

        public void sendRegionHandshakeToAll()
        {
            m_scene.Broadcast(
                sendRegionHandshake
                );
        }

        public void handleEstateChangeInfo(IClientAPI remoteClient, LLUUID invoice, LLUUID senderID, UInt32 parms1, UInt32 parms2)
        {
            if (parms2 == 0)
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = true;
                m_scene.RegionInfo.EstateSettings.SunPosition = 0.0;
            }
            else
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = false;
                m_scene.RegionInfo.EstateSettings.SunPosition = (double)(parms2 - 0x1800)/1024.0;
            }

            if ((parms1 & 0x00000010) != 0)
                m_scene.RegionInfo.EstateSettings.FixedSun = true;
            else
                m_scene.RegionInfo.EstateSettings.FixedSun = false;

            if ((parms1 & 0x00008000) != 0)
                m_scene.RegionInfo.EstateSettings.PublicAccess = true;
            else
                m_scene.RegionInfo.EstateSettings.PublicAccess = false;

            if ((parms1 & 0x10000000) != 0)
                m_scene.RegionInfo.EstateSettings.AllowVoice = true;
            else
                m_scene.RegionInfo.EstateSettings.AllowVoice = false;

            if ((parms1 & 0x00100000) != 0)
                m_scene.RegionInfo.EstateSettings.AllowDirectTeleport = true;
            else
                m_scene.RegionInfo.EstateSettings.AllowDirectTeleport = false;

            if ((parms1 & 0x00800000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyAnonymous = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyAnonymous = false;

            if ((parms1 & 0x01000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyIdentified = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyIdentified = false;

            if ((parms1 & 0x02000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyTransacted = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyTransacted = false;

            if ((parms1 & 0x40000000) != 0)
                m_scene.RegionInfo.EstateSettings.DenyMinors = true;
            else
                m_scene.RegionInfo.EstateSettings.DenyMinors = false;

            m_scene.RegionInfo.EstateSettings.Save();

            float sun = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
            if (m_scene.RegionInfo.RegionSettings.UseEstateSun)
            {
                sun = (float)m_scene.RegionInfo.EstateSettings.SunPosition;
                if (m_scene.RegionInfo.EstateSettings.UseGlobalTime)
                    sun  = m_scene.EventManager.GetSunLindenHour();
            }

            m_scene.EventManager.TriggerEstateToolsTimeUpdate(
                    m_scene.RegionInfo.RegionHandle,
                    m_scene.RegionInfo.EstateSettings.FixedSun ||
                    m_scene.RegionInfo.RegionSettings.FixedSun,
                    m_scene.RegionInfo.RegionSettings.UseEstateSun, sun);

            sendDetailedEstateData(remoteClient, invoice);
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IEstateModule>(this);
            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            m_scene.EventManager.OnRequestChangeWaterHeight += changeWaterHeight;
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
            setRegionTerrainSettings(height,
                    (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit,
                    (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit,
                    m_scene.RegionInfo.RegionSettings.UseEstateSun,
                    m_scene.RegionInfo.RegionSettings.FixedSun,
                    (float)m_scene.RegionInfo.RegionSettings.SunPosition,
                    m_scene.RegionInfo.EstateSettings.UseGlobalTime,
                    m_scene.RegionInfo.EstateSettings.FixedSun,
                    (float)m_scene.RegionInfo.EstateSettings.SunPosition);

            sendRegionInfoPacketToAll();
        }

        #endregion

        private void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnDetailedEstateDataRequest += sendDetailedEstateData;
            client.OnSetEstateFlagsRequest += estateSetRegionInfoHandler;
//            client.OnSetEstateTerrainBaseTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainDetailTexture += setEstateTerrainBaseTexture;
            client.OnSetEstateTerrainTextureHeights += setEstateTerrainTextureHeights;
            client.OnCommitEstateTerrainTextureRequest += handleCommitEstateTerrainTextureRequest;
            client.OnSetRegionTerrainSettings += setRegionTerrainSettings;
            client.OnEstateRestartSimRequest += handleEstateRestartSimRequest;
            client.OnEstateChangeCovenantRequest += handleChangeEstateCovenantRequest;
            client.OnEstateChangeInfo += handleEstateChangeInfo;
            client.OnUpdateEstateAccessDeltaRequest += handleEstateAccessDeltaRequest;
            client.OnSimulatorBlueBoxMessageRequest += SendSimulatorBlueBoxMessage;
            client.OnEstateBlueBoxMessageRequest += SendEstateBlueBoxMessage;
            client.OnEstateDebugRegionRequest += handleEstateDebugRegionRequest;
            client.OnEstateTeleportOneUserHomeRequest += handleEstateTeleportOneUserHomeRequest;

            client.OnRegionInfoRequest += HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += HandleEstateCovenantRequest;
            client.OnLandStatRequest += HandleLandStatRequest;
            sendRegionHandshake(client);
        }
        
        public uint GetRegionFlags()
        {
            Simulator.RegionFlags flags = Simulator.RegionFlags.None;
            
            // Fully implemented
            //
            if (m_scene.RegionInfo.RegionSettings.AllowDamage)
                flags |= Simulator.RegionFlags.AllowDamage;
            if (m_scene.RegionInfo.RegionSettings.BlockTerraform)
                flags |= Simulator.RegionFlags.BlockTerraform;
            if (!m_scene.RegionInfo.RegionSettings.AllowLandResell)
                flags |= Simulator.RegionFlags.BlockLandResell;
            if (m_scene.RegionInfo.RegionSettings.DisableCollisions)
                flags |= Simulator.RegionFlags.SkipCollisions;
            if (m_scene.RegionInfo.RegionSettings.DisableScripts)
                flags |= Simulator.RegionFlags.SkipScripts;
            if (m_scene.RegionInfo.RegionSettings.DisablePhysics)
                flags |= Simulator.RegionFlags.SkipPhysics;
            if (m_scene.RegionInfo.RegionSettings.BlockFly)
                flags |= Simulator.RegionFlags.NoFly;
            if (m_scene.RegionInfo.RegionSettings.RestrictPushing)
                flags |= Simulator.RegionFlags.RestrictPushObject;
            if (m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide)
                flags |= Simulator.RegionFlags.AllowParcelChanges;
            if (m_scene.RegionInfo.RegionSettings.BlockShowInSearch)
                flags |= (Simulator.RegionFlags)(1 << 29);

            if (m_scene.RegionInfo.RegionSettings.FixedSun)
                flags |= Simulator.RegionFlags.SunFixed;
            if (m_scene.RegionInfo.RegionSettings.Sandbox)
                flags |= Simulator.RegionFlags.Sandbox;

            // Fudge these to always on, so the menu options activate
            //
            flags |= Simulator.RegionFlags.AllowLandmark;
            flags |= Simulator.RegionFlags.AllowSetHome;

            // TODO: SkipUpdateInterestList

            // Omitted
            //
            // Omitted: NullLayer (what is that?)
            // Omitted: SkipAgentAction (what does it do?)

            return (uint)flags;
        }

        public uint GetEstateFlags()
        {
            Simulator.RegionFlags flags = Simulator.RegionFlags.None;

            if (m_scene.RegionInfo.EstateSettings.FixedSun)
                flags |= Simulator.RegionFlags.SunFixed;
            if (m_scene.RegionInfo.EstateSettings.PublicAccess)
                flags |= (Simulator.RegionFlags.PublicAllowed |
                          Simulator.RegionFlags.ExternallyVisible);
            if (m_scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= Simulator.RegionFlags.AllowVoice;
            if (m_scene.RegionInfo.EstateSettings.AllowDirectTeleport)
                flags |= Simulator.RegionFlags.AllowDirectTeleport;
            if (m_scene.RegionInfo.EstateSettings.DenyAnonymous)
                flags |= Simulator.RegionFlags.DenyAnonymous;
            if (m_scene.RegionInfo.EstateSettings.DenyIdentified)
                flags |= Simulator.RegionFlags.DenyIdentified;
            if (m_scene.RegionInfo.EstateSettings.DenyTransacted)
                flags |= Simulator.RegionFlags.DenyTransacted;
            if (m_scene.RegionInfo.EstateSettings.AbuseEmailToEstateOwner)
                flags |= Simulator.RegionFlags.AbuseEmailToEstateOwner;
            if (m_scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= Simulator.RegionFlags.BlockDwell;
            if (m_scene.RegionInfo.EstateSettings.EstateSkipScripts)
                flags |= Simulator.RegionFlags.EstateSkipScripts;
            if (m_scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= Simulator.RegionFlags.ResetHomeOnTeleport;
            if (m_scene.RegionInfo.EstateSettings.TaxFree)
                flags |= Simulator.RegionFlags.TaxFree;
            if (m_scene.RegionInfo.EstateSettings.DenyMinors)
                flags |= (Simulator.RegionFlags)(1 << 30);

            return (uint)flags;
        }

        public bool IsManager(LLUUID avatarID)
        {
            if (avatarID == m_scene.RegionInfo.MasterAvatarAssignedUUID)
                return true;

            List<LLUUID> ems = new List<LLUUID>(m_scene.RegionInfo.EstateSettings.EstateManagers);
            if (ems.Contains(avatarID))
                return true;

            return false;
        }
    }
}
