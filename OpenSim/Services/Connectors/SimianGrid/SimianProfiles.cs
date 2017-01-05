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
using System.Collections.Specialized;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Avatar profile flags
    /// </summary>
    [Flags]
    public enum ProfileFlags : uint
    {
        AllowPublish = 1,
        MaturePublish = 2,
        Identified = 4,
        Transacted = 8,
        Online = 16
    }

    /// <summary>
    /// Connects avatar profile and classified queries to the SimianGrid
    /// backend
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianProfiles")]
    public class SimianProfiles : INonSharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverUrl = String.Empty;
        private bool m_Enabled = false;

        #region INonSharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void Close() { }

        public SimianProfiles() { }
        public string Name { get { return "SimianProfiles"; } }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                CheckEstateManager(scene);
                scene.EventManager.OnClientConnect += ClientConnectHandler;
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.EventManager.OnClientConnect -= ClientConnectHandler;
            }
        }

        #endregion INonSharedRegionModule

        public SimianProfiles(IConfigSource source)
        {
            Initialise(source);
        }

        public void Initialise(IConfigSource source)
        {
            IConfig profileConfig = source.Configs["Profiles"];
            if (profileConfig == null)
                return;

            if (profileConfig.GetString("Module", String.Empty) != Name)
                return;

            m_log.DebugFormat("[SIMIAN PROFILES] module enabled");
            m_Enabled = true;

            IConfig gridConfig = source.Configs["UserAccountService"];
            if (gridConfig != null)
            {
                string serviceUrl = gridConfig.GetString("UserAccountServerURI");
                if (!String.IsNullOrEmpty(serviceUrl))
                {
                    if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                        serviceUrl = serviceUrl + '/';
                    m_serverUrl = serviceUrl;
                }
            }

            if (String.IsNullOrEmpty(m_serverUrl))
                m_log.Info("[SIMIAN PROFILES]: No UserAccountServerURI specified, disabling connector");
        }

        private void ClientConnectHandler(IClientCore clientCore)
        {
            if (clientCore is IClientAPI)
            {
                IClientAPI client = (IClientAPI)clientCore;

                // Classifieds
                client.AddGenericPacketHandler("avatarclassifiedsrequest", AvatarClassifiedsRequestHandler);
                client.OnClassifiedInfoRequest += ClassifiedInfoRequestHandler;
                client.OnClassifiedInfoUpdate += ClassifiedInfoUpdateHandler;
                client.OnClassifiedDelete += ClassifiedDeleteHandler;

                // Picks
                client.AddGenericPacketHandler("avatarpicksrequest", HandleAvatarPicksRequest);
                client.AddGenericPacketHandler("pickinforequest", HandlePickInfoRequest);
                client.OnPickInfoUpdate += PickInfoUpdateHandler;
                client.OnPickDelete += PickDeleteHandler;

                // Notes
                client.AddGenericPacketHandler("avatarnotesrequest", HandleAvatarNotesRequest);
                client.OnAvatarNotesUpdate += AvatarNotesUpdateHandler;

                // Profiles
                client.OnRequestAvatarProperties += RequestAvatarPropertiesHandler;

                client.OnUpdateAvatarProperties += UpdateAvatarPropertiesHandler;
                client.OnAvatarInterestUpdate += AvatarInterestUpdateHandler;
                client.OnUserInfoRequest += UserInfoRequestHandler;
                client.OnUpdateUserInfo += UpdateUserInfoHandler;
            }
        }

        #region Classifieds

        private void AvatarClassifiedsRequestHandler(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;
            IClientAPI client = (IClientAPI)sender;

            UUID targetAvatarID;
            if (args.Count < 1 || !UUID.TryParse(args[0], out targetAvatarID))
            {
                m_log.Error("[SIMIAN PROFILES]: Unrecognized arguments for " + method);
                return;
            }

            // FIXME: Query the generic key/value store for classifieds
            client.SendAvatarClassifiedReply(targetAvatarID, new Dictionary<UUID, string>(0));
        }

        private void ClassifiedInfoRequestHandler(UUID classifiedID, IClientAPI client)
        {
            // FIXME: Fetch this info
            client.SendClassifiedInfoReply(classifiedID, UUID.Zero, 0, Utils.DateTimeToUnixTime(DateTime.UtcNow + TimeSpan.FromDays(1)),
                0, String.Empty, String.Empty, UUID.Zero, 0, UUID.Zero, String.Empty, Vector3.Zero, String.Empty, 0, 0);
        }

        private void ClassifiedInfoUpdateHandler(UUID classifiedID, uint category, string name, string description,
            UUID parcelID, uint parentEstate, UUID snapshotID, Vector3 globalPos, byte classifiedFlags, int price,
            IClientAPI client)
        {
            // FIXME: Save this info
        }

        private void ClassifiedDeleteHandler(UUID classifiedID, IClientAPI client)
        {
            // FIXME: Delete the specified classified ad
        }

        #endregion Classifieds

        #region Picks

        private void HandleAvatarPicksRequest(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;
            IClientAPI client = (IClientAPI)sender;

            UUID targetAvatarID;
            if (args.Count < 1 || !UUID.TryParse(args[0], out targetAvatarID))
            {
                m_log.Error("[SIMIAN PROFILES]: Unrecognized arguments for " + method);
                return;
            }

            // FIXME: Fetch these
            client.SendAvatarPicksReply(targetAvatarID, new Dictionary<UUID, string>(0));
        }

        private void HandlePickInfoRequest(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;
            IClientAPI client = (IClientAPI)sender;

            UUID avatarID;
            UUID pickID;
            if (args.Count < 2 || !UUID.TryParse(args[0], out avatarID) || !UUID.TryParse(args[1], out pickID))
            {
                m_log.Error("[SIMIAN PROFILES]: Unrecognized arguments for " + method);
                return;
            }

            // FIXME: Fetch this
            client.SendPickInfoReply(pickID, avatarID, false, UUID.Zero, String.Empty, String.Empty, UUID.Zero, String.Empty,
                String.Empty, String.Empty, Vector3.Zero, 0, false);
        }

        private void PickInfoUpdateHandler(IClientAPI client, UUID pickID, UUID creatorID, bool topPick, string name,
            string desc, UUID snapshotID, int sortOrder, bool enabled)
        {
            // FIXME: Save this
        }

        private void PickDeleteHandler(IClientAPI client, UUID pickID)
        {
            // FIXME: Delete
        }

        #endregion Picks

        #region Notes

        private void HandleAvatarNotesRequest(Object sender, string method, List<String> args)
        {
            if (!(sender is IClientAPI))
                return;
            IClientAPI client = (IClientAPI)sender;

            UUID targetAvatarID;
            if (args.Count < 1 || !UUID.TryParse(args[0], out targetAvatarID))
            {
                m_log.Error("[SIMIAN PROFILES]: Unrecognized arguments for " + method);
                return;
            }

            // FIXME: Fetch this
            client.SendAvatarNotesReply(targetAvatarID, String.Empty);
        }

        private void AvatarNotesUpdateHandler(IClientAPI client, UUID targetID, string notes)
        {
            // FIXME: Save this
        }

        #endregion Notes

        #region Profiles

        private void RequestAvatarPropertiesHandler(IClientAPI client, UUID avatarID)
        {
            m_log.DebugFormat("[SIMIAN PROFILES]: Request avatar properties for {0}",avatarID);

            OSDMap user = FetchUserData(avatarID);

            ProfileFlags flags = ProfileFlags.AllowPublish | ProfileFlags.MaturePublish;

            if (user != null)
            {
                OSDMap about = null;
                if (user.ContainsKey("LLAbout"))
                {
                    try
                    {
                        about = OSDParser.DeserializeJson(user["LLAbout"].AsString()) as OSDMap;
                    }
                    catch
                    {
                        m_log.WarnFormat("[SIMIAN PROFILES]: Unable to decode LLAbout");
                    }
                }

                if (about == null)
                    about = new OSDMap(0);

                // Check if this user is a grid operator
                byte[] membershipType;
                if (user["AccessLevel"].AsInteger() >= 200)
                    membershipType = Utils.StringToBytes("Operator");
                else
                    membershipType = Utils.EmptyBytes;

                // Check if the user is online
                if (client.Scene is Scene)
                {
                    OpenSim.Services.Interfaces.PresenceInfo[] presences = ((Scene)client.Scene).PresenceService.GetAgents(new string[] { avatarID.ToString() });
                    if (presences != null && presences.Length > 0)
                        flags |= ProfileFlags.Online;
                }

                // Check if the user is identified
                if (user["Identified"].AsBoolean())
                    flags |= ProfileFlags.Identified;

                client.SendAvatarProperties(avatarID, about["About"].AsString(), user["CreationDate"].AsDate().ToString("M/d/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture), membershipType, about["FLAbout"].AsString(), (uint)flags,
                    about["FLImage"].AsUUID(), about["Image"].AsUUID(), about["URL"].AsString(), user["Partner"].AsUUID());

                OSDMap interests = null;
                if (user.ContainsKey("LLInterests"))
                {
                    try
                    {
                        interests = OSDParser.DeserializeJson(user["LLInterests"].AsString()) as OSDMap;
                        client.SendAvatarInterestsReply(avatarID, interests["WantMask"].AsUInteger(), interests["WantText"].AsString(), interests["SkillsMask"].AsUInteger(), interests["SkillsText"].AsString(), interests["Languages"].AsString());
                    }
                    catch { }
                }

                if (about == null)
                    about = new OSDMap(0);
            }
            else
            {
                m_log.Warn("[SIMIAN PROFILES]: Failed to fetch profile information for " + client.Name + ", returning default values");
                client.SendAvatarProperties(avatarID, String.Empty, "1/1/1970", Utils.EmptyBytes,
                        String.Empty, (uint)flags, UUID.Zero, UUID.Zero, String.Empty, UUID.Zero);
            }
        }

        private void UpdateAvatarPropertiesHandler(IClientAPI client, UserProfileData profileData)
        {
            OSDMap map = new OSDMap
            {
                { "About", OSD.FromString(profileData.AboutText) },
                { "Image", OSD.FromUUID(profileData.Image) },
                { "FLAbout", OSD.FromString(profileData.FirstLifeAboutText) },
                { "FLImage", OSD.FromUUID(profileData.FirstLifeImage) },
                { "URL", OSD.FromString(profileData.ProfileUrl) }
            };

            AddUserData(client.AgentId, "LLAbout", map);
        }

        private void AvatarInterestUpdateHandler(IClientAPI client, uint wantmask, string wanttext, uint skillsmask,
            string skillstext, string languages)
        {
            OSDMap map = new OSDMap
            {
                { "WantMask", OSD.FromInteger(wantmask) },
                { "WantText", OSD.FromString(wanttext) },
                { "SkillsMask", OSD.FromInteger(skillsmask) },
                { "SkillsText", OSD.FromString(skillstext) },
                { "Languages", OSD.FromString(languages) }
            };

            AddUserData(client.AgentId, "LLInterests", map);
        }

        private void UserInfoRequestHandler(IClientAPI client)
        {
            m_log.Error("[SIMIAN PROFILES]: UserInfoRequestHandler");

            // Fetch this user's e-mail address
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUser" },
                { "UserID", client.AgentId.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            string email = response["Email"].AsString();

            if (!response["Success"].AsBoolean())
                m_log.Warn("[SIMIAN PROFILES]: GetUser failed during a user info request for " + client.Name);

            client.SendUserInfoReply(false, true, email);
        }

        private void UpdateUserInfoHandler(bool imViaEmail, bool visible, IClientAPI client)
        {
            m_log.Info("[SIMIAN PROFILES]: Ignoring user info update from " + client.Name);
        }

        #endregion Profiles

        /// <summary>
        /// Sanity checks regions for a valid estate owner at startup
        /// </summary>
        private void CheckEstateManager(Scene scene)
        {
            EstateSettings estate = scene.RegionInfo.EstateSettings;

            if (estate.EstateOwner == UUID.Zero)
            {
                // Attempt to lookup the grid admin
                UserAccount admin = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, UUID.Zero);
                if (admin != null)
                {
                    m_log.InfoFormat("[SIMIAN PROFILES]: Setting estate {0} (ID: {1}) owner to {2}", estate.EstateName,
                        estate.EstateID, admin.Name);

                    estate.EstateOwner = admin.PrincipalID;
                    scene.EstateDataService.StoreEstateSettings(estate);
                }
                else
                {
                    m_log.WarnFormat("[SIMIAN PROFILES]: Estate {0} (ID: {1}) does not have an owner", estate.EstateName, estate.EstateID);
                }
            }
        }

        private bool AddUserData(UUID userID, string key, OSDMap value)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddUserData" },
                { "UserID", userID.ToString() },
                { key, OSDParser.SerializeJsonString(value) }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.WarnFormat("[SIMIAN PROFILES]: Failed to add user data with key {0} for {1}: {2}", key, userID, response["Message"].AsString());

            return success;
        }

        private OSDMap FetchUserData(UUID userID)
        {
            m_log.DebugFormat("[SIMIAN PROFILES]: Fetch information about {0}",userID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUser" },
                { "UserID", userID.ToString() }
            };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["User"] is OSDMap)
            {
                return (OSDMap)response["User"];
            }
            else
            {
                m_log.Error("[SIMIAN PROFILES]: Failed to fetch user data for " + userID + ": " + response["Message"].AsString());
            }

            return null;
        }
    }
}
