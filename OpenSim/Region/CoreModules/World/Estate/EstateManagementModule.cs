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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.IO;
using System.Reflection;
using System.Security;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class EstateManagementModule : IEstateModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private delegate void LookupUUIDS(List<UUID> uuidLst);

        private Scene m_scene;
        private Timer m_regionChangeTimer = new Timer();

        private EstateTerrainXferHandler TerrainUploader;

        public event ChangeDelegate OnRegionInfoChange;
        public event ChangeDelegate OnEstateInfoChange;
        public event MessageDelegate OnEstateMessage;

        #region Packet Data Responders

        private void sendDetailedEstateData(IClientAPI remote_client, UUID invoice)
        {
            uint sun = 0;

            if (!m_scene.RegionInfo.EstateSettings.UseGlobalTime)
                sun=(uint)(m_scene.RegionInfo.EstateSettings.SunPosition*1024.0) + 0x1800;
            UUID estateOwner;
            estateOwner = m_scene.RegionInfo.EstateSettings.EstateOwner;

            if (m_scene.Permissions.IsGod(remote_client.AgentId))
                estateOwner = remote_client.AgentId;

            remote_client.SendDetailedEstateData(invoice,
                    m_scene.RegionInfo.EstateSettings.EstateName,
                    m_scene.RegionInfo.EstateSettings.EstateID,
                    m_scene.RegionInfo.EstateSettings.ParentEstateID,
                    GetEstateFlags(),
                    sun,
                    m_scene.RegionInfo.RegionSettings.Covenant,
                    m_scene.RegionInfo.EstateSettings.AbuseEmail,
                    estateOwner);

            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.EstateManagers,
                    m_scene.RegionInfo.EstateSettings.EstateManagers,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.AccessOptions,
                    m_scene.RegionInfo.EstateSettings.EstateAccess,
                    m_scene.RegionInfo.EstateSettings.EstateID);

            remote_client.SendEstateList(invoice,
                    (int)Constants.EstateAccessCodex.AllowedGroups,
                    m_scene.RegionInfo.EstateSettings.EstateGroups,
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
            else if (matureLevel <= 21)
                m_scene.RegionInfo.RegionSettings.Maturity = 1;
            else
                m_scene.RegionInfo.RegionSettings.Maturity = 2;

            if (restrictPushObject)
                m_scene.RegionInfo.RegionSettings.RestrictPushing = true;
            else
                m_scene.RegionInfo.RegionSettings.RestrictPushing = false;

            if (allowParcelChanges)
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = true;
            else
                m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide = false;

            m_scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();

            sendRegionInfoPacketToAll();
        }

        public void setEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, UUID texture)
        {
            if (texture == UUID.Zero)
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
            TriggerRegionInfoChange();
            sendRegionInfoPacketToAll();
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
            TriggerRegionInfoChange();
            sendRegionInfoPacketToAll();
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

            m_scene.TriggerEstateSunUpdate();

            //m_log.Debug("[ESTATE]: UFS: " + UseFixedSun.ToString());
            //m_log.Debug("[ESTATE]: SunHour: " + SunHour.ToString());

            sendRegionInfoPacketToAll();
            m_scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
        }

        private void handleEstateRestartSimRequest(IClientAPI remoteClient, int timeInSeconds)
        {
            IRestartModule restartModule = m_scene.RequestModuleInterface<IRestartModule>();
            if (restartModule != null)
            {
                List<int> times = new List<int>();
                while (timeInSeconds > 0)
                {
                    times.Add(timeInSeconds);
                    if (timeInSeconds > 300)
                        timeInSeconds -= 120;
                    else if (timeInSeconds > 30)
                        timeInSeconds -= 30;
                    else
                        timeInSeconds -= 15;
                }

                restartModule.ScheduleRestart(UUID.Zero, "Region will restart in {0}", times.ToArray(), false);
            }
        }

        private void handleChangeEstateCovenantRequest(IClientAPI remoteClient, UUID estateCovenantID)
        {
            m_scene.RegionInfo.RegionSettings.Covenant = estateCovenantID;
            m_scene.RegionInfo.RegionSettings.Save();
            TriggerRegionInfoChange();
        }

        private void handleEstateAccessDeltaRequest(IClientAPI remote_client, UUID invoice, int estateAccessType, UUID user)
        {
            // EstateAccessDelta handles Estate Managers, Sim Access, Sim Banlist, allowed Groups..  etc.

            if (user == m_scene.RegionInfo.EstateSettings.EstateOwner)
                return; // never process EO

            if ((estateAccessType & 4) != 0) // User add
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.AddEstateUser(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();
                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.AccessOptions, m_scene.RegionInfo.EstateSettings.EstateAccess, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }

            }
            if ((estateAccessType & 8) != 0) // User remove
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.RemoveEstateUser(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();

                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.AccessOptions, m_scene.RegionInfo.EstateSettings.EstateAccess, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
            }
            if ((estateAccessType & 16) != 0) // Group add
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.AddEstateGroup(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();
                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.AllowedGroups, m_scene.RegionInfo.EstateSettings.EstateGroups, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
            }
            if ((estateAccessType & 32) != 0) // Group remove
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.RemoveEstateGroup(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();

                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.AllowedGroups, m_scene.RegionInfo.EstateSettings.EstateGroups, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
            }
            if ((estateAccessType & 64) != 0) // Ban add
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;

                    bool alreadyInList = false;

                    for (int i = 0; i < banlistcheck.Length; i++)
                    {
                        if (user == banlistcheck[i].BannedUserID)
                        {
                            alreadyInList = true;
                            break;
                        }

                    }
                    if (!alreadyInList)
                    {

                        EstateBan item = new EstateBan();

                        item.BannedUserID = user;
                        item.EstateID = m_scene.RegionInfo.EstateSettings.EstateID;
                        item.BannedHostAddress = "0.0.0.0";
                        item.BannedHostIPMask = "0.0.0.0";

                        m_scene.RegionInfo.EstateSettings.AddBan(item);
                        m_scene.RegionInfo.EstateSettings.Save();
                        TriggerEstateInfoChange();

                        ScenePresence s = m_scene.GetScenePresence(user);
                        if (s != null)
                        {
                            if (!s.IsChildAgent)
                            {
                                if (!m_scene.TeleportClientHome(user, s.ControllingClient))
                                {
                                    s.ControllingClient.Kick("Your access to the region was revoked and TP home failed - you have been logged out.");
                                    s.ControllingClient.Close();
                                }
                            }
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
            }
            if ((estateAccessType & 128) != 0) // Ban remove
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, false) || m_scene.Permissions.BypassPermissions())
                {
                    EstateBan[] banlistcheck = m_scene.RegionInfo.EstateSettings.EstateBans;

                    bool alreadyInList = false;
                    EstateBan listitem = null;

                    for (int i = 0; i < banlistcheck.Length; i++)
                    {
                        if (user == banlistcheck[i].BannedUserID)
                        {
                            alreadyInList = true;
                            listitem = banlistcheck[i];
                            break;
                        }

                    }
                    if (alreadyInList && listitem != null)
                    {
                        m_scene.RegionInfo.EstateSettings.RemoveBan(listitem.BannedUserID);
                        m_scene.RegionInfo.EstateSettings.Save();
                        TriggerEstateInfoChange();
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
            }
            if ((estateAccessType & 256) != 0) // Manager add
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, true) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.AddEstateManager(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();
                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
            }
            if ((estateAccessType & 512) != 0) // Manager remove
            {
                if (m_scene.Permissions.CanIssueEstateCommand(remote_client.AgentId, true) || m_scene.Permissions.BypassPermissions())
                {
                    m_scene.RegionInfo.EstateSettings.RemoveEstateManager(user);
                    m_scene.RegionInfo.EstateSettings.Save();
                    TriggerEstateInfoChange();

                    remote_client.SendEstateList(invoice, (int)Constants.EstateAccessCodex.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateManagers, m_scene.RegionInfo.EstateSettings.EstateID);
                }
                else
                {
                    remote_client.SendAlertMessage("Method EstateAccessDelta Failed, you don't have permissions");
                }
            }
        }

        private void SendSimulatorBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            IDialogModule dm = m_scene.RequestModuleInterface<IDialogModule>();
            
            if (dm != null)
                dm.SendNotificationToUsersInRegion(senderID, senderName, message);
        }

        private void SendEstateBlueBoxMessage(
            IClientAPI remote_client, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message)
        {
            TriggerEstateMessage(senderID, senderName, message);
        }

        private void handleEstateDebugRegionRequest(IClientAPI remote_client, UUID invoice, UUID senderID, bool scripted, bool collisionEvents, bool physics)
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
            TriggerRegionInfoChange();

            m_scene.SetSceneCoreDebug(scripted, collisionEvents, physics);
        }

        private void handleEstateTeleportOneUserHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID, UUID prey)
        {
            if (!m_scene.Permissions.CanIssueEstateCommand(remover_client.AgentId, false))
                return;

            if (prey != UUID.Zero)
            {
                ScenePresence s = m_scene.GetScenePresence(prey);
                if (s != null)
                {
                    if (!m_scene.TeleportClientHome(prey, s.ControllingClient))
                    {
                        s.ControllingClient.Kick("You were teleported home by the region owner, but the TP failed - you have been logged out.");
                        s.ControllingClient.Close();
                    }
                }
            }
        }

        private void handleEstateTeleportAllUsersHomeRequest(IClientAPI remover_client, UUID invoice, UUID senderID)
        {
            if (!m_scene.Permissions.CanIssueEstateCommand(remover_client.AgentId, false))
                return;

            m_scene.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (sp.UUID != senderID)
                {
                    ScenePresence p = m_scene.GetScenePresence(sp.UUID);
                    // make sure they are still there, we could be working down a long list
                    // Also make sure they are actually in the region
                    if (p != null && !p.IsChildAgent)
                    {
                        if (!m_scene.TeleportClientHome(p.UUID, p.ControllingClient))
                        {
                            p.ControllingClient.Kick("You were teleported home by the region owner, but the TP failed - you have been logged out.");
                            p.ControllingClient.Close();
                        }
                    }
                }
            });
        }
        private void AbortTerrainXferHandler(IClientAPI remoteClient, ulong XferID)
        {
            if (TerrainUploader != null)
            {
                lock (TerrainUploader)
                {
                    if (XferID == TerrainUploader.XferID)
                    {
                        remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                        remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                        TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                        TerrainUploader = null;
                        remoteClient.SendAlertMessage("Terrain Upload aborted by the client");
                    }
                }
            }

        }
        private void HandleTerrainApplication(string filename, byte[] terrainData, IClientAPI remoteClient)
        {
            lock (TerrainUploader)
            {
                remoteClient.OnXferReceive -= TerrainUploader.XferReceive;
                remoteClient.OnAbortXfer -= AbortTerrainXferHandler;
                TerrainUploader.TerrainUploadDone -= HandleTerrainApplication;

                TerrainUploader = null;
            }
            remoteClient.SendAlertMessage("Terrain Upload Complete. Loading....");
            ITerrainModule terr = m_scene.RequestModuleInterface<ITerrainModule>();

            if (terr != null)
            {
                m_log.Warn("[CLIENT]: Got Request to Send Terrain in region " + m_scene.RegionInfo.RegionName);

                try
                {

                    string localfilename = "terrain.raw";

                    if (terrainData.Length == 851968)
                    {
                        localfilename = Path.Combine(Util.dataDir(),"terrain.raw"); // It's a .LLRAW
                    }

                    if (terrainData.Length == 196662) // 24-bit 256x256 Bitmap
                        localfilename = Path.Combine(Util.dataDir(), "terrain.bmp");

                    if (terrainData.Length == 256 * 256 * 4) // It's a .R32
                        localfilename = Path.Combine(Util.dataDir(), "terrain.r32");

                    if (terrainData.Length == 256 * 256 * 8) // It's a .R64
                        localfilename = Path.Combine(Util.dataDir(), "terrain.r64");

                    if (File.Exists(localfilename))
                    {
                        File.Delete(localfilename);
                    }

                    FileStream input = new FileStream(localfilename, FileMode.CreateNew);
                    input.Write(terrainData, 0, terrainData.Length);
                    input.Close();

                    FileInfo x = new FileInfo(localfilename);

                    terr.LoadFromFile(localfilename);
                    remoteClient.SendAlertMessage("Your terrain was loaded as a ." + x.Extension + " file. It may take a few moments to appear.");

                }
                catch (IOException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was an IO Exception loading your terrain.  Please check free space.");

                    return;
                }
                catch (SecurityException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }
                catch (UnauthorizedAccessException e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error Saving a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a security Exception loading your terrain.  Please check the security on the simulator drive");

                    return;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[TERRAIN]: Error loading a terrain file uploaded via the estate tools.  It gave us the following error: {0}", e.ToString());
                    remoteClient.SendAlertMessage("There was a general error loading your terrain.  Please fix the terrain file and try again");
                }
            }
            else
            {
                remoteClient.SendAlertMessage("Unable to apply terrain.  Cannot get an instance of the terrain module");
            }
        }

        private void handleUploadTerrain(IClientAPI remote_client, string clientFileName)
        {

            if (TerrainUploader == null)
            {

                TerrainUploader = new EstateTerrainXferHandler(remote_client, clientFileName);
                lock (TerrainUploader)
                {
                    remote_client.OnXferReceive += TerrainUploader.XferReceive;
                    remote_client.OnAbortXfer += AbortTerrainXferHandler;
                    TerrainUploader.TerrainUploadDone += HandleTerrainApplication;
                }
                TerrainUploader.RequestStartXfer(remote_client);

            }
            else
            {
                remote_client.SendAlertMessage("Another Terrain Upload is in progress.  Please wait your turn!");
            }

        }
        private void handleTerrainRequest(IClientAPI remote_client, string clientFileName)
        {
            // Save terrain here
            ITerrainModule terr = m_scene.RequestModuleInterface<ITerrainModule>();
            
            if (terr != null)
            {
                m_log.Warn("[CLIENT]: Got Request to Send Terrain in region " + m_scene.RegionInfo.RegionName);
                if (File.Exists(Util.dataDir() + "/terrain.raw"))
                {
                    File.Delete(Util.dataDir() + "/terrain.raw");
                }
                terr.SaveToFile(Util.dataDir() + "/terrain.raw");

                FileStream input = new FileStream(Util.dataDir() + "/terrain.raw", FileMode.Open);
                byte[] bdata = new byte[input.Length];
                input.Read(bdata, 0, (int)input.Length);
                remote_client.SendAlertMessage("Terrain file written, starting download...");
                m_scene.XferManager.AddNewFile("terrain.raw", bdata);
                // Tell client about it
                m_log.Warn("[CLIENT]: Sending Terrain to " + remote_client.Name);
                remote_client.SendInitiateDownload("terrain.raw", clientFileName);
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
           args.simAccess = m_scene.RegionInfo.AccessLevel;
           args.sunHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
           args.terrainLowerLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
           args.terrainRaiseLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;
           args.useEstateSun = m_scene.RegionInfo.RegionSettings.UseEstateSun;
           args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
           args.simName = m_scene.RegionInfo.RegionName;
           args.regionType = m_scene.RegionInfo.RegionType;

           remote_client.SendRegionInfoToEstateMenu(args);
        }

        private void HandleEstateCovenantRequest(IClientAPI remote_client)
        {
            remote_client.SendEstateCovenantInformation(m_scene.RegionInfo.RegionSettings.Covenant);
        }

        private void HandleLandStatRequest(int parcelID, uint reportType, uint requestFlags, string filter, IClientAPI remoteClient)
        {
            if (!m_scene.Permissions.CanIssueEstateCommand(remoteClient.AgentId, false))
                return;

            Dictionary<uint, float> SceneData = new Dictionary<uint,float>();
            List<UUID> uuidNameLookupList = new List<UUID>();

            if (reportType == 1)
            {
                SceneData = m_scene.PhysicsScene.GetTopColliders();
            }
            else if (reportType == 0)
            {
                SceneData = m_scene.SceneGraph.GetTopScripts();
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
                                lsri.OwnerName = "waiting";
                                lock (uuidNameLookupList)
                                    uuidNameLookupList.Add(sog.OwnerID);

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

        private static void LookupUUIDSCompleted(IAsyncResult iar)
        {
            LookupUUIDS icon = (LookupUUIDS)iar.AsyncState;
            icon.EndInvoke(iar);
        }
        private void LookupUUID(List<UUID> uuidLst)
        {
            LookupUUIDS d = LookupUUIDsAsync;

            d.BeginInvoke(uuidLst,
                          LookupUUIDSCompleted,
                          d);
        }
        private void LookupUUIDsAsync(List<UUID> uuidLst)
        {
            UUID[] uuidarr;

            lock (uuidLst)
            {
                uuidarr = uuidLst.ToArray();
            }

            for (int i = 0; i < uuidarr.Length; i++)
            {
                // string lookupname = m_scene.CommsManager.UUIDNameRequestString(uuidarr[i]);

                IUserManagement userManager = m_scene.RequestModuleInterface<IUserManagement>();
                string userName = "Unkown User";
                if (userManager != null)
                    userName = userManager.GetUserName(uuidarr[i]);
                
                // we drop it.  It gets cached though...  so we're ready for the next request.
                // diva commnent 11/21/2010: uh?!? wft?
            }
        }
        #endregion

        #region Outgoing Packets

        public void sendRegionInfoPacketToAll()
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (!sp.IsChildAgent)
                    HandleRegionInfoRequest(sp.ControllingClient);
            });
        }

        public void sendRegionHandshake(IClientAPI remoteClient)
        {
            RegionHandshakeArgs args = new RegionHandshakeArgs();

            args.isEstateManager = m_scene.RegionInfo.EstateSettings.IsEstateManager(remoteClient.AgentId);
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero && m_scene.RegionInfo.EstateSettings.EstateOwner == remoteClient.AgentId)
                args.isEstateManager = true;

            args.billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor;
            args.terrainStartHeight0 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SW;
            args.terrainHeightRange0 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SW;
            args.terrainStartHeight1 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NW;
            args.terrainHeightRange1 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NW;
            args.terrainStartHeight2 = (float)m_scene.RegionInfo.RegionSettings.Elevation1SE;
            args.terrainHeightRange2 = (float)m_scene.RegionInfo.RegionSettings.Elevation2SE;
            args.terrainStartHeight3 = (float)m_scene.RegionInfo.RegionSettings.Elevation1NE;
            args.terrainHeightRange3 = (float)m_scene.RegionInfo.RegionSettings.Elevation2NE;
            args.simAccess = m_scene.RegionInfo.AccessLevel;
            args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
            args.regionFlags = GetRegionFlags();
            args.regionName = m_scene.RegionInfo.RegionName;
            args.SimOwner = m_scene.RegionInfo.EstateSettings.EstateOwner;

            args.terrainBase0 = UUID.Zero;
            args.terrainBase1 = UUID.Zero;
            args.terrainBase2 = UUID.Zero;
            args.terrainBase3 = UUID.Zero;
            args.terrainDetail0 = m_scene.RegionInfo.RegionSettings.TerrainTexture1;
            args.terrainDetail1 = m_scene.RegionInfo.RegionSettings.TerrainTexture2;
            args.terrainDetail2 = m_scene.RegionInfo.RegionSettings.TerrainTexture3;
            args.terrainDetail3 = m_scene.RegionInfo.RegionSettings.TerrainTexture4;

            remoteClient.SendRegionHandshake(m_scene.RegionInfo,args);
        }

        public void sendRegionHandshakeToAll()
        {
            m_scene.ForEachClient(sendRegionHandshake);
        }

        public void handleEstateChangeInfo(IClientAPI remoteClient, UUID invoice, UUID senderID, UInt32 parms1, UInt32 parms2)
        {
            if (parms2 == 0)
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = true;
                m_scene.RegionInfo.EstateSettings.SunPosition = 0.0;
            }
            else
            {
                m_scene.RegionInfo.EstateSettings.UseGlobalTime = false;
                m_scene.RegionInfo.EstateSettings.SunPosition = (parms2 - 0x1800)/1024.0;
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
            TriggerEstateInfoChange();

            m_scene.TriggerEstateSunUpdate();

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
            m_regionChangeTimer.AutoReset = false;
            m_regionChangeTimer.Interval = 2000;
            m_regionChangeTimer.Elapsed += RaiseRegionInfoChange;

            m_scene.AddCommand(this, "set terrain texture",
                               "set terrain texture <number> <uuid> [<x>] [<y>]",
                               "Sets the terrain <number> to <uuid>, if <x> or <y> are specified, it will only " +
                               "set it on regions with a matching coordinate. Specify -1 in <x> or <y> to wildcard" +
                               " that coordinate.",
                               consoleSetTerrainTexture);

            m_scene.AddCommand(this, "set terrain heights",
                               "set terrain heights <corner> <min> <max> [<x>] [<y>]",
                               "Sets the terrain texture heights on corner #<corner> to <min>/<max>, if <x> or <y> are specified, it will only " +
                               "set it on regions with a matching coordinate. Specify -1 in <x> or <y> to wildcard" +
                               " that coordinate. Corner # SW = 0, NW = 1, SE = 2, NE = 3.",
                               consoleSetTerrainHeights);
        }

        #region Console Commands

        public void consoleSetTerrainTexture(string module, string[] args)
        {
            string num = args[3];
            string uuid = args[4];
            int x = (args.Length > 5 ? int.Parse(args[5]) : -1);
            int y = (args.Length > 6 ? int.Parse(args[6]) : -1);

            if (x == -1 || m_scene.RegionInfo.RegionLocX == x)
            {
                if (y == -1 || m_scene.RegionInfo.RegionLocY == y)
                {
                    int corner = int.Parse(num);
                    UUID texture = UUID.Parse(uuid);

                    m_log.Debug("[ESTATEMODULE] Setting terrain textures for " + m_scene.RegionInfo.RegionName +
                                string.Format(" (C#{0} = {1})", corner, texture));

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
                    TriggerRegionInfoChange();
                    sendRegionInfoPacketToAll();

                }
            }
         }
 
        public void consoleSetTerrainHeights(string module, string[] args)
        {
            string num = args[3];
            string min = args[4];
            string max = args[5];
            int x = (args.Length > 6 ? int.Parse(args[6]) : -1);
            int y = (args.Length > 7 ? int.Parse(args[7]) : -1);

            if (x == -1 || m_scene.RegionInfo.RegionLocX == x)
            {
                if (y == -1 || m_scene.RegionInfo.RegionLocY == y)
                {
                    int corner = int.Parse(num);
                    float lowValue = float.Parse(min, Culture.NumberFormatInfo);
                    float highValue = float.Parse(max, Culture.NumberFormatInfo);

                    m_log.Debug("[ESTATEMODULE] Setting terrain heights " + m_scene.RegionInfo.RegionName +
                                string.Format(" (C{0}, {1}-{2}", corner, lowValue, highValue));

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
                    TriggerRegionInfoChange();
                    sendRegionHandshakeToAll();
                }
            }
        }

        #endregion

        public void PostInitialise()
        {
            // Sets up the sun module based no the saved Estate and Region Settings
            // DO NOT REMOVE or the sun will stop working
            m_scene.TriggerEstateSunUpdate();
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
            client.OnEstateTeleportAllUsersHomeRequest += handleEstateTeleportAllUsersHomeRequest;
            client.OnRequestTerrain += handleTerrainRequest;
            client.OnUploadTerrain += handleUploadTerrain;

            client.OnRegionInfoRequest += HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += HandleEstateCovenantRequest;
            client.OnLandStatRequest += HandleLandStatRequest;
            sendRegionHandshake(client);
        }

        public uint GetRegionFlags()
        {
            RegionFlags flags = RegionFlags.None;

            // Fully implemented
            //
            if (m_scene.RegionInfo.RegionSettings.AllowDamage)
                flags |= RegionFlags.AllowDamage;
            if (m_scene.RegionInfo.RegionSettings.BlockTerraform)
                flags |= RegionFlags.BlockTerraform;
            if (!m_scene.RegionInfo.RegionSettings.AllowLandResell)
                flags |= RegionFlags.BlockLandResell;
            if (m_scene.RegionInfo.RegionSettings.DisableCollisions)
                flags |= RegionFlags.SkipCollisions;
            if (m_scene.RegionInfo.RegionSettings.DisableScripts)
                flags |= RegionFlags.SkipScripts;
            if (m_scene.RegionInfo.RegionSettings.DisablePhysics)
                flags |= RegionFlags.SkipPhysics;
            if (m_scene.RegionInfo.RegionSettings.BlockFly)
                flags |= RegionFlags.NoFly;
            if (m_scene.RegionInfo.RegionSettings.RestrictPushing)
                flags |= RegionFlags.RestrictPushObject;
            if (m_scene.RegionInfo.RegionSettings.AllowLandJoinDivide)
                flags |= RegionFlags.AllowParcelChanges;
            if (m_scene.RegionInfo.RegionSettings.BlockShowInSearch)
                flags |= RegionFlags.BlockParcelSearch;

            if (m_scene.RegionInfo.RegionSettings.FixedSun)
                flags |= RegionFlags.SunFixed;
            if (m_scene.RegionInfo.RegionSettings.Sandbox)
                flags |= RegionFlags.Sandbox;
            if (m_scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;

            // Fudge these to always on, so the menu options activate
            //
            flags |= RegionFlags.AllowLandmark;
            flags |= RegionFlags.AllowSetHome;

            // TODO: SkipUpdateInterestList

            // Omitted
            //
            // Omitted: NullLayer (what is that?)
            // Omitted: SkipAgentAction (what does it do?)

            return (uint)flags;
        }

        public uint GetEstateFlags()
        {
            RegionFlags flags = RegionFlags.None;

            if (m_scene.RegionInfo.EstateSettings.FixedSun)
                flags |= RegionFlags.SunFixed;
            if (m_scene.RegionInfo.EstateSettings.PublicAccess)
                flags |= (RegionFlags.PublicAllowed |
                          RegionFlags.ExternallyVisible);
            if (m_scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;
            if (m_scene.RegionInfo.EstateSettings.AllowDirectTeleport)
                flags |= RegionFlags.AllowDirectTeleport;
            if (m_scene.RegionInfo.EstateSettings.DenyAnonymous)
                flags |= RegionFlags.DenyAnonymous;
            if (m_scene.RegionInfo.EstateSettings.DenyIdentified)
                flags |= RegionFlags.DenyIdentified;
            if (m_scene.RegionInfo.EstateSettings.DenyTransacted)
                flags |= RegionFlags.DenyTransacted;
            if (m_scene.RegionInfo.EstateSettings.AbuseEmailToEstateOwner)
                flags |= RegionFlags.AbuseEmailToEstateOwner;
            if (m_scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (m_scene.RegionInfo.EstateSettings.EstateSkipScripts)
                flags |= RegionFlags.EstateSkipScripts;
            if (m_scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;
            if (m_scene.RegionInfo.EstateSettings.TaxFree)
                flags |= RegionFlags.TaxFree;
            if (m_scene.RegionInfo.EstateSettings.DenyMinors)
                flags |= (RegionFlags)(1 << 30);

            return (uint)flags;
        }

        public bool IsManager(UUID avatarID)
        {
            if (avatarID == m_scene.RegionInfo.EstateSettings.EstateOwner)
                return true;

            List<UUID> ems = new List<UUID>(m_scene.RegionInfo.EstateSettings.EstateManagers);
            if (ems.Contains(avatarID))
                return true;

            return false;
        }

        protected void TriggerRegionInfoChange()
        {
            m_regionChangeTimer.Stop();
            m_regionChangeTimer.Start();
        }

        protected void RaiseRegionInfoChange(object sender, ElapsedEventArgs e)
        {
            ChangeDelegate change = OnRegionInfoChange;

            if (change != null)
                change(m_scene.RegionInfo.RegionID);
        }

        protected void TriggerEstateInfoChange()
        {
            ChangeDelegate change = OnEstateInfoChange;

            if (change != null)
                change(m_scene.RegionInfo.RegionID);
        }

        protected void TriggerEstateMessage(UUID fromID, string fromName, string message)
        {
            MessageDelegate onmessage = OnEstateMessage;

            if (onmessage != null)
                onmessage(m_scene.RegionInfo.RegionID, fromID, fromName, message);
        }
    }
}
