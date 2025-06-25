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
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Mono.Addins;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.UserProfilesService;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Region.CoreModules.Avatar.Friends;

namespace OpenSim.Region.CoreModules.Avatar.UserProfiles
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UserProfilesModule")]
    public class UserProfileModule : IProfileModule, INonSharedRegionModule
    {
        const double PROFILECACHEEXPIRE = 300;
        /// <summary>
        /// Logging
        /// </summary>
        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // The pair of Dictionaries are used to handle the switching of classified ads
        // by maintaining a cache of classified id to creator id mappings and an interest
        // count. The entries are removed when the interest count reaches 0.
        readonly Dictionary<UUID, UUID> m_classifiedCache = new();
        readonly Dictionary<UUID, int> m_classifiedInterest = new();
        readonly ExpiringCacheOS<UUID, UserProfileCacheEntry> m_profilesCache = new(60000);
        IGroupsModule m_groupsModule = null;

        private readonly JsonRpcRequestManager rpc = new();
        private bool m_allowUserProfileWebURLs = true;

        struct AsyncPropsRequest
        {
            public IClientAPI client;
            public ScenePresence presence;
            public UUID agent;
            public int reqtype;
        }

        private readonly ConcurrentStack<AsyncPropsRequest> m_asyncRequests = new();
        private readonly object m_asyncRequestsLock = new();
        private bool m_asyncRequestsRunning = false;

        private void ProcessRequests()
        {
            lock(m_asyncRequestsLock)
            {
                while (m_asyncRequests.TryPop(out AsyncPropsRequest req))
                {
                    try
                    {
                        IClientAPI client = req.client;
                        if(!client.IsActive)
                            continue;

                        if(req.reqtype == 0)
                        {
                            ScenePresence p = req.presence;

                            bool foreign = GetUserProfileServerURI(req.agent, out string serverURI);
                            bool ok  = serverURI.Length > 0;

                            byte[] membershipType = new byte[1];
                            string born = string.Empty;
                            uint flags = 0x00;

                           if (ok && GetUserAccountData(req.agent, out UserAccount acc))
                           {
                                flags = (uint)(acc.UserFlags & 0xff);

                                if (acc.UserTitle.Length == 0)
                                    membershipType[0] = (byte)((acc.UserFlags & 0x0f00) >> 8);
                                else
                                    membershipType = Utils.StringToBytes(acc.UserTitle);

                                int val_born = acc.Created;
                                if (val_born != 0)
                                  born = Util.ToDateTime(val_born).ToString("M/d/yyyy", CultureInfo.InvariantCulture);
                            }
                            else
                                ok = false;

                            UserProfileProperties props = new() { UserId = req.agent };

                            if (ok)
                                ok = GetProfileData(ref props, foreign, serverURI, out string result);

                            if (!ok)
                                props.AboutText = "Profile not available at this time. User may still be unknown to this grid";

                            if (!m_allowUserProfileWebURLs)
                                props.WebUrl = "";

                            GroupMembershipData[] agentGroups = null;
                            if(ok && m_groupsModule is not null)
                                agentGroups = m_groupsModule.GetMembershipData(req.agent);

                            HashSet<IClientAPI> clients;
                            lock (m_profilesCache)
                            {
                                if (!m_profilesCache.TryGetValue(props.UserId, out UserProfileCacheEntry uce) || uce is null)
                                    uce = new UserProfileCacheEntry();
                                uce.props = props;
                                uce.born = born;
                                uce.membershipType = membershipType;
                                uce.flags = flags;
                                clients = uce.ClientsWaitingProps;
                                uce.ClientsWaitingProps = null;
                                uce.avatarGroups = agentGroups;
                                m_profilesCache.AddOrUpdate(props.UserId, uce, PROFILECACHEEXPIRE);
                            }

                            if (IsFriendOnline(req.client, req.agent))
                                flags |= (uint)ProfileFlags.Online;
                            else
                                flags &= (uint)~ProfileFlags.Online;

                            if (clients is null)
                            {
                                client.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType, props.FirstLifeText, flags,
                                                              props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                                client.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                                             (uint)props.SkillsMask, props.SkillsText, props.Language);
                                if (agentGroups is not null)
                                    client.SendAvatarGroupsReply(req.agent, agentGroups);
                            }
                            else
                            {
                                if (!clients.Contains(client) && client.IsActive)
                                {
                                    client.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType, props.FirstLifeText, flags,
                                                                  props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                                    client.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                                                 (uint)props.SkillsMask, props.SkillsText, props.Language);
                                    if (agentGroups is not null)
                                        client.SendAvatarGroupsReply(req.agent, agentGroups);
                                }
                                foreach (IClientAPI cli in clients)
                                {
                                    if (!cli.IsActive)
                                        continue;
                                    cli.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType, props.FirstLifeText, flags,
                                                                props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                                    cli.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                                                (uint)props.SkillsMask, props.SkillsText, props.Language);
                                    if (agentGroups is not null)
                                        cli.SendAvatarGroupsReply(req.agent, agentGroups);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[UserProfileModule]: Process fail {0} : {1}", e.Message, e.StackTrace);
                    }

                }
                m_asyncRequestsRunning = false;
            }
        }

        public Scene Scene
        {
            get; private set;
        }

        /// <summary>
        /// Gets or sets the ConfigSource.
        /// </summary>
        /// <value>
        /// The configuration
        /// </value>
        public IConfigSource Config
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the URI to the profile server.
        /// </summary>
        /// <value>
        /// The profile server URI.
        /// </value>
        public string ProfileServerUri
        {
            get;
            set;
        }

        IProfileModule ProfileModule
        {
            get; set;
        }

        IUserManagement UserManagementModule
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this
        /// <see cref="OpenSim.Region.Coremodules.UserProfiles.UserProfileModule"/> is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if enabled; otherwise, <c>false</c>.
        /// </value>
        public bool Enabled
        {
            get;
            set;
        }

        private GridInfo m_thisGridInfo;

        #region IRegionModuleBase implementation
        /// <summary>
        ///  This is called to initialize the region module. For shared modules, this is called exactly once, after
        /// creating the single (shared) instance. For non-shared modules, this is called once on each instance, after
        /// the instace for the region has been created.
        /// </summary>
        /// <param name='source'>
        /// Source.
        /// </param>
        public void Initialise(IConfigSource source)
        {
            Config = source;
            ReplaceableInterface = typeof(IProfileModule);

            IConfig profileConfig = Config.Configs["UserProfiles"];

            if (profileConfig is null)
            {
                //m_log.Debug("[PROFILES]: UserProfiles disabled, no configuration");
                Enabled = false;
                return;
            }

            // If we find ProfileURL then we configure for FULL support
            // else we setup for BASIC support
            ProfileServerUri = profileConfig.GetString("ProfileServiceURL", "");
            if (string.IsNullOrEmpty(ProfileServerUri))
            {
                Enabled = false;
                return;
            }

            OSHTTPURI tmp = new(ProfileServerUri, true);
            if (!tmp.IsResolvedHost)
            {
                m_log.ErrorFormat("[UserProfileModule: {0}", tmp.IsValidHost ?  "Could not resolve ProfileServiceURL" : "ProfileServiceURL is a invalid host");
                throw new Exception("UserProfileModule init error");
            }

            ProfileServerUri = tmp.URI;

            m_allowUserProfileWebURLs = profileConfig.GetBoolean("AllowUserProfileWebURLs", m_allowUserProfileWebURLs);

            m_log.Debug("[UserProfileModule]: Full Profiles Enabled");
            ReplaceableInterface = null;
            Enabled = true;
        }

        /// <summary>
        /// Adds the region.
        /// </summary>
        /// <param name='scene'>
        /// Scene.
        /// </param>
        public void AddRegion(Scene scene)
        {
            if(!Enabled)
                return;

            Scene = scene;
            m_thisGridInfo ??= scene.SceneGridInfo;
            Scene.RegisterModuleInterface<IProfileModule>(this);
            Scene.EventManager.OnNewClient += OnNewClient;
            Scene.EventManager.OnClientClosed += OnClientClosed;

            UserManagementModule = Scene.RequestModuleInterface<IUserManagement>();
        }

        /// <summary>
        /// Removes the region.
        /// </summary>
        /// <param name='scene'>
        /// Scene.
        /// </param>
        public void RemoveRegion(Scene scene)
        {
            if(!Enabled)
                return;

            m_profilesCache.Clear();
            m_classifiedCache.Clear();
            m_classifiedInterest.Clear();
        }

        /// <summary>
        ///  This will be called once for every scene loaded. In a shared module this will be multiple times in one
        /// instance, while a nonshared module instance will only be called once. This method is called after AddRegion
        /// has been called in all modules for that scene, providing an opportunity to request another module's
        /// interface, or hook an event from another module.
        /// </summary>
        /// <param name='scene'>
        /// Scene.
        /// </param>
        public void RegionLoaded(Scene scene)
        {
            if(!Enabled)
                return;
            m_groupsModule = Scene.RequestModuleInterface<IGroupsModule>();
        }

        /// <summary>
        ///  If this returns non-null, it is the type of an interface that this module intends to register. This will
        /// cause the loader to defer loading of this module until all other modules have been loaded. If no other
        /// module has registered the interface by then, this module will be activated, else it will remain inactive,
        /// letting the other module take over. This should return non-null ONLY in modules that are intended to be
        /// easily replaceable, e.g. stub implementations that the developer expects to be replaced by third party
        /// provided modules.
        /// </summary>
        /// <value>
        /// The replaceable interface.
        /// </value>
        public Type ReplaceableInterface
        {
            get; private set;
        }

        /// <summary>
        /// Called as the instance is closed.
        /// </summary>
        public void Close()
        {
            m_thisGridInfo = null;
        }

        /// <value>
        ///  The name of the module
        /// </value>
        /// <summary>
        /// Gets the module name.
        /// </summary>
        public string Name
        {
            get { return "UserProfileModule"; }
        }
        #endregion IRegionModuleBase implementation

        #region Region Event Handlers
        /// <summary>
        /// Raises the new client event.
        /// </summary>
        /// <param name='client'>
        /// Client.
        /// </param>
        void OnNewClient(IClientAPI client)
        {
            //Profile
            client.OnRequestAvatarProperties += RequestAvatarProperties;
            client.OnUpdateAvatarProperties += AvatarPropertiesUpdate;
            client.OnAvatarInterestUpdate += AvatarInterestsUpdate;

            // Classifieds
            client.AddGenericPacketHandler("avatarclassifiedsrequest", ClassifiedsRequest);
            client.OnClassifiedInfoUpdate += ClassifiedInfoUpdate;
            client.OnClassifiedInfoRequest += ClassifiedInfoRequest;
            client.OnClassifiedDelete += ClassifiedDelete;

            // Picks
            client.AddGenericPacketHandler("avatarpicksrequest", PicksRequest);
            client.AddGenericPacketHandler("pickinforequest", PickInfoRequest);
            client.OnPickInfoUpdate += PickInfoUpdate;
            client.OnPickDelete += PickDelete;

            // Notes
            client.AddGenericPacketHandler("avatarnotesrequest", NotesRequest);
            client.OnAvatarNotesUpdate += NotesUpdate;

            // Preferences
            client.OnUserInfoRequest += UserPreferencesRequest;
            client.OnUpdateUserInfo += UpdateUserPreferences;
        }

        void OnClientClosed(UUID AgentId, Scene scene)
        {
            ScenePresence sp = scene.GetScenePresence(AgentId);
            IClientAPI client = sp.ControllingClient;
            if (client is null)
                return;

            //Profile
            client.OnRequestAvatarProperties -= RequestAvatarProperties;
            client.OnUpdateAvatarProperties  -= AvatarPropertiesUpdate;
            client.OnAvatarInterestUpdate    -= AvatarInterestsUpdate;

            // Classifieds
            client.OnClassifiedInfoUpdate    -= ClassifiedInfoUpdate;
            client.OnClassifiedInfoRequest   -= ClassifiedInfoRequest;
            client.OnClassifiedDelete        -= ClassifiedDelete;

            // Picks
            client.OnPickInfoUpdate -= PickInfoUpdate;
            client.OnPickDelete     -= PickDelete;

            // Notes
            client.OnAvatarNotesUpdate -= NotesUpdate;

            // Preferences
            client.OnUserInfoRequest -= UserPreferencesRequest;
            client.OnUpdateUserInfo  -= UpdateUserPreferences;
        }

        #endregion Region Event Handlers

        #region Classified
        ///
        /// <summary>
        /// Handles the avatar classifieds request.
        /// </summary>
        /// <param name='sender'>
        /// Sender.
        /// </param>
        /// <param name='method'>
        /// Method.
        /// </param>
        /// <param name='args'>
        /// Arguments.
        /// </param>
        public void ClassifiedsRequest(Object sender, string method, List<String> args)
        {
            if (sender is not IClientAPI remoteClient)
                return;

            Dictionary<UUID, string> classifieds = new();

            if (!UUID.TryParse(args[0], out UUID targetID) || targetID.IsZero())
                return;

            if (targetID.Equals(Constants.m_MrOpenSimID))
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            ScenePresence p = FindPresence(targetID);
            if (p is not null && p.IsNPC)
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetID, out UserProfileCacheEntry uce) && uce is not null)
                {
                    if(uce.classifiedsLists is not null)
                    {
                        foreach(KeyValuePair<UUID,string> kvp in uce.classifiedsLists)
                        {
                            UUID kvpkey = kvp.Key;
                            classifieds[kvpkey] = kvp.Value;
                            lock (m_classifiedCache)
                            {
                                if (!m_classifiedCache.ContainsKey(kvpkey))
                                {
                                    m_classifiedCache.Add(kvpkey,targetID);
                                    m_classifiedInterest.Add(kvpkey, 0);
                                }

                            m_classifiedInterest[kvpkey]++;
                            }
                        }
                        remoteClient.SendAvatarClassifiedReply(targetID, uce.classifiedsLists);
                        return;
                    }
                }
            }

            GetUserProfileServerURI(targetID, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            OSDMap parameters= new()
            {
                {"creatorId", OSD.FromUUID(targetID)}
            };

            OSD osdtmp = parameters;
            if(!rpc.JsonRpcRequest(ref osdtmp, "avatarclassifiedsrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            parameters = (OSDMap)osdtmp;
            if(!parameters.TryGetValue("result", out osdtmp) || osdtmp is not OSDArray)
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            OSDArray list = (OSDArray)osdtmp;
            foreach(OSD map in list)
            {
                OSDMap m = (OSDMap)map;
                UUID cid = m["classifieduuid"].AsUUID();
                string name = m["name"].AsString();

                classifieds[cid] = name;

                lock (m_classifiedCache)
                {
                    if (!m_classifiedCache.ContainsKey(cid))
                    {
                        m_classifiedCache.Add(cid,targetID);
                        m_classifiedInterest.Add(cid, 0);
                    }

                    m_classifiedInterest[cid]++;
                }
            }

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetID, out UserProfileCacheEntry uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.classifiedsLists = classifieds;

                m_profilesCache.AddOrUpdate(targetID, uce, PROFILECACHEEXPIRE);
            }

            remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
        }

        public void ClassifiedInfoRequest(UUID queryClassifiedID, IClientAPI remoteClient)
        {
            UUID target = remoteClient.AgentId;
            UserClassifiedAdd ad = new() { ClassifiedId = queryClassifiedID };

            lock (m_classifiedCache)
            {
                if (m_classifiedCache.ContainsKey(queryClassifiedID))
                {
                    target = m_classifiedCache[queryClassifiedID];

                    m_classifiedInterest[queryClassifiedID]--;

                    if (m_classifiedInterest[queryClassifiedID] == 0)
                    {
                        m_classifiedInterest.Remove(queryClassifiedID);
                        m_classifiedCache.Remove(queryClassifiedID);
                    }
                }
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(target, out uce) && uce is not null)
                {
                    if(uce.classifieds is not null && uce.classifieds.ContainsKey(queryClassifiedID))
                    {
                        ad = uce.classifieds[queryClassifiedID];
                        if(Vector3.TryParse(ad.GlobalPos, out Vector3 gPos))
                        {
                            remoteClient.SendClassifiedInfoReply(ad.ClassifiedId, ad.CreatorId, (uint)ad.CreationDate,
                                (uint)ad.ExpirationDate, (uint)ad.Category, ad.Name, ad.Description,
                                ad.ParcelId, (uint)ad.ParentEstate, ad.SnapshotId, ad.SimName,
                                gPos, ad.ParcelName, ad.Flags, ad.Price);
                        }
                        return;
                    }
                }
            }

            bool foreign = GetUserProfileServerURI(target, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            object Adobject = ad;
            if(!rpc.JsonRpcRequest(ref Adobject, "classifieds_info_query", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error getting classified info", false);
                return;
            }
            ad = (UserClassifiedAdd) Adobject;

            if(ad.CreatorId.IsZero())
                return;

            if(foreign)
                cacheForeignImage(target, ad.SnapshotId);

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(target, out uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.classifieds ??= new Dictionary<UUID, UserClassifiedAdd>();
                uce.classifieds[ad.ClassifiedId] = ad;

                m_profilesCache.AddOrUpdate(target, uce, PROFILECACHEEXPIRE);
            }

            if(Vector3.TryParse(ad.GlobalPos, out Vector3 globalPos))
                remoteClient.SendClassifiedInfoReply(ad.ClassifiedId, ad.CreatorId, (uint)ad.CreationDate, (uint)ad.ExpirationDate,
                                                 (uint)ad.Category, ad.Name, ad.Description, ad.ParcelId, (uint)ad.ParentEstate,
                                                 ad.SnapshotId, ad.SimName, globalPos, ad.ParcelName, ad.Flags, ad.Price);

        }

        /// <summary>
        /// Classifieds info update.
        /// </summary>
        /// <param name='queryclassifiedID'>
        /// Queryclassified I.
        /// </param>
        /// <param name='queryCategory'>
        /// Query category.
        /// </param>
        /// <param name='queryName'>
        /// Query name.
        /// </param>
        /// <param name='queryDescription'>
        /// Query description.
        /// </param>
        /// <param name='queryParcelID'>
        /// Query parcel I.
        /// </param>
        /// <param name='queryParentEstate'>
        /// Query parent estate.
        /// </param>
        /// <param name='querySnapshotID'>
        /// Query snapshot I.
        /// </param>
        /// <param name='queryGlobalPos'>
        /// Query global position.
        /// </param>
        /// <param name='queryclassifiedFlags'>
        /// Queryclassified flags.
        /// </param>
        /// <param name='queryclassifiedPrice'>
        /// Queryclassified price.
        /// </param>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        public void ClassifiedInfoUpdate(UUID queryclassifiedID, uint queryCategory, string queryName, string queryDescription, UUID queryParcelID,
                                         uint queryParentEstate, UUID querySnapshotID, Vector3 queryGlobalPos, byte queryclassifiedFlags,
                                         int queryclassifiedPrice, IClientAPI remoteClient)
        {
            Scene s = (Scene)remoteClient.Scene;
            Vector3 pos = remoteClient.SceneAgent.AbsolutePosition;
            ILandObject land = s.LandChannel.GetLandObject(pos.X, pos.Y);
            UUID creatorId = remoteClient.AgentId;
            ScenePresence p = FindPresence(creatorId);

            UserProfileCacheEntry uce;
            lock (m_profilesCache)
                m_profilesCache.TryGetValue(remoteClient.AgentId, out uce);

            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            if(foreign)
            {
                remoteClient.SendAgentAlertMessage("Please change classifieds on your home grid", true);
                if(uce is not null && uce.classifiedsLists is not null)
                     remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                return;
            }

            OSDMap parameters = new () { {"creatorId", OSD.FromUUID(creatorId)} };
            OSD osdtmp = parameters;
            if (!rpc.JsonRpcRequest(ref osdtmp, "avatarclassifiedsrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error fetching classifieds", false);
                return;
            }

            parameters = (OSDMap)osdtmp;
            OSDArray list = (OSDArray)parameters["result"];
            bool exists = list.Cast<OSDMap>().Where(map => map.ContainsKey("classifieduuid"))
              .Any(map => map["classifieduuid"].AsUUID().Equals(queryclassifiedID));

            IMoneyModule money = null;
            if (!exists)
            {
                money = s.RequestModuleInterface<IMoneyModule>();
                if (money is not null)
                {
                    if (!money.AmountCovered(remoteClient.AgentId, queryclassifiedPrice))
                    {
                        remoteClient.SendAgentAlertMessage("You do not have enough money to create this classified.", false);
                        if(uce is not null && uce.classifiedsLists is not null)
                            remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                        return;
                    }
                }
            }

            UserClassifiedAdd ad = new()
            {
                ParcelName = land == null ? string.Empty : land.LandData.Name,
                CreatorId = remoteClient.AgentId,
                ClassifiedId = queryclassifiedID,
                Category = Convert.ToInt32(queryCategory),
                Name = queryName,
                Description = queryDescription,
                ParentEstate = Convert.ToInt32(queryParentEstate),
                SnapshotId = querySnapshotID,
                SimName = remoteClient.Scene.RegionInfo.RegionName,
                GlobalPos = queryGlobalPos.ToString(),
                Flags = queryclassifiedFlags,
                Price = queryclassifiedPrice,
                ParcelId = p.currentParcelUUID
            };

            object Ad = ad;
            if(!rpc.JsonRpcRequest(ref Ad, "classified_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error updating classified", false);
                if(uce is not null && uce.classifiedsLists is not null)
                    remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                return;
            }

            // only charge if it worked
            money?.ApplyCharge(remoteClient.AgentId, queryclassifiedPrice, MoneyTransactionType.ClassifiedCharge);

            // just flush cache for now
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce is not null)
                {
                    uce.classifieds = null;
                    uce.classifiedsLists = null;
                }
            }
        }

        /// <summary>
        /// Classifieds delete.
        /// </summary>
        /// <param name='queryClassifiedID'>
        /// Query classified I.
        /// </param>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        public void ClassifiedDelete(UUID queryClassifiedID, IClientAPI remoteClient)
        {
            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            if(foreign)
            {
                remoteClient.SendAgentAlertMessage("Please change classifieds on your home grid", true);
                return;
            }

            if (!UUID.TryParse(queryClassifiedID.ToString(), out UUID classifiedId))
                return;

            OSD Params = new OSDMap() {{ "classifiedId", OSD.FromUUID(classifiedId) }};
            if(!rpc.JsonRpcRequest(ref Params, "classified_delete", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error deleting classified", false);
                return;
            }

            // flush cache
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out UserProfileCacheEntry uce) && uce is not null)
                {
                    uce.classifieds = null;
                    uce.classifiedsLists = null;
                }
            }
        }
        #endregion Classified

        #region Picks
        /// <summary>
        /// Handles the avatar picks request.
        /// </summary>
        /// <param name='sender'>
        /// Sender.
        /// </param>
        /// <param name='method'>
        /// Method.
        /// </param>
        /// <param name='args'>
        /// Arguments.
        /// </param>
        public void PicksRequest(Object sender, string method, List<String> args)
        {
            if (sender is not IClientAPI remoteClient)
                return;

            if(!UUID.TryParse(args[0], out UUID targetId))
                return;

            Dictionary<UUID, string> picks = new();

            if (targetId.Equals(Constants.m_MrOpenSimID))
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            ScenePresence p = FindPresence(targetId);
            if (p is not null && p.IsNPC)
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetId, out uce) && uce is not null)
                {
                    if(uce.picksList is not null)
                    {
                        remoteClient.SendAvatarPicksReply(targetId, uce.picksList);
                        return;
                    }
                }
            }

            GetUserProfileServerURI(targetId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            OSDMap parameters= new()
            {
                {"creatorId", OSD.FromUUID(targetId)}
            };
            OSD osdtmp = parameters;
            if(!rpc.JsonRpcRequest(ref osdtmp, "avatarpicksrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            parameters = (OSDMap)osdtmp;
            if(!parameters.TryGetValue("result", out osdtmp) || osdtmp is not OSDArray)
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            OSDArray list = (OSDArray)osdtmp;
            foreach(OSD map in list)
            {
                OSDMap m = (OSDMap)map;
                UUID cid = m["pickuuid"].AsUUID();
                string name = m["name"].AsString();
                picks[cid] = name;
            }

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetId, out uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.picksList = picks;

                m_profilesCache.AddOrUpdate(targetId, uce, PROFILECACHEEXPIRE);
            }

            remoteClient.SendAvatarPicksReply(targetId, picks);
        }

        public Dictionary<UUID, string> GetPicks(UUID targetId)
        { 
            Dictionary<UUID, string> picks = new();

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetId, out uce) && uce is not null)
                {
                    if(uce.picksList is not null)
                        return uce.picksList;
                }
            }

            GetUserProfileServerURI(targetId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return picks;

            OSDMap parameters= new()
            {
                {"creatorId", OSD.FromUUID(targetId)}
            };
            OSD osdtmp = parameters;
            if(!rpc.JsonRpcRequest(ref osdtmp, "avatarpicksrequest", serverURI, UUID.Random().ToString()))
                return picks;

            parameters = (OSDMap)osdtmp;
            if(!parameters.TryGetValue("result", out osdtmp) || osdtmp is not OSDArray)
                return picks;

            OSDArray list = (OSDArray)osdtmp;
            foreach(OSD map in list)
            {
                OSDMap m = (OSDMap)map;
                UUID cid = m["pickuuid"].AsUUID();
                string name = m["name"].AsString();
                picks[cid] = name;
            }

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetId, out uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.picksList = picks;

                m_profilesCache.AddOrUpdate(targetId, uce, PROFILECACHEEXPIRE);
            }

            return picks;
        }

        /// <summary>
        /// Handles the pick info request.
        /// </summary>
        /// <param name='sender'>
        /// Sender.
        /// </param>
        /// <param name='method'>
        /// Method.
        /// </param>
        /// <param name='args'>
        /// Arguments.
        /// </param>
        public void PickInfoRequest(Object sender, string method, List<String> args)
        {
            if (sender is not IClientAPI)
                return;

            if(!UUID.TryParse(args [0], out UUID targetID))
                return;

            if(!UUID.TryParse (args [1], out UUID PickId))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;
            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetID, out uce) && uce is not null)
                {
                    if(uce.picks is not null && uce.picks.TryGetValue(PickId, out UserProfilePick cpick))
                    {
                        if(Vector3d.TryParse(cpick.GlobalPos, out Vector3d gPos))
                            remoteClient.SendPickInfoReply(cpick.PickId,cpick.CreatorId,cpick.TopPick,cpick.ParcelId,cpick.Name,
                                           cpick.Desc,cpick.SnapshotId,cpick.ParcelName,cpick.OriginalName,cpick.SimName,
                                           gPos,cpick.SortOrder,cpick.Enabled);
                        return;
                    }
                }
            }

            bool foreign =  GetUserProfileServerURI (targetID, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            UserProfilePick pick = new()
            {
                PickId = PickId,
                CreatorId = targetID
            };

            object Pick = (object)pick;
            if (!rpc.JsonRpcRequest (ref Pick, "pickinforequest", serverURI, UUID.Random ().ToString ())) {
                remoteClient.SendAgentAlertMessage ("Error selecting pick", false);
                return;
            }
            pick = (UserProfilePick)Pick;
            if(foreign)
                cacheForeignImage(targetID, pick.SnapshotId);

            if(!Vector3d.TryParse(pick.GlobalPos, out Vector3d globalPos))
                return;

            if (m_thisGridInfo.IsLocalGrid(pick.Gatekeeper, true) == 0)
            {
                // Setup the illusion
                string region = string.Format("{0} {1}",pick.Gatekeeper,pick.SimName);
                GridRegion target = Scene.GridService.GetRegionByName(Scene.RegionInfo.ScopeID, region);

                if(target is null)
                {
                    // This is a unreachable region
                }
                else
                {
                    // we have a proxy on map
                    if (Util.ParseFakeParcelID(pick.ParcelId, out ulong _, out uint oriX, out uint oriY))
                    {
                        pick.ParcelId = Util.BuildFakeParcelID(target.RegionHandle, oriX, oriY);
                        globalPos.X = target.RegionLocX + oriX;
                        globalPos.Y = target.RegionLocY + oriY;
                        pick.GlobalPos = globalPos.ToString();
                    }
                    else
                    {
                        // this is a fail on large regions
                        uint gtmp = (uint)globalPos.X >> 8;
                        globalPos.X -= (gtmp << 8);

                        gtmp = (uint)globalPos.Y >> 8;
                        globalPos.Y -= (gtmp << 8);

                        pick.ParcelId = Util.BuildFakeParcelID(target.RegionHandle, (uint)globalPos.X, (uint)globalPos.Y);

                        globalPos.X += target.RegionLocX;
                        globalPos.Y += target.RegionLocY;
                        pick.GlobalPos = globalPos.ToString();
                    }
                }
            }

            //m_log.DebugFormat("[PROFILES]: PickInfoRequest: {0} : {1}", pick.Name.ToString(), pick.SnapshotId.ToString());

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetID, out uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.picks ??= new Dictionary<UUID, UserProfilePick>();
                uce.picks[pick.PickId] = pick;

                m_profilesCache.AddOrUpdate(targetID, uce, PROFILECACHEEXPIRE);
            }

            // Pull the rabbit out of the hat
            remoteClient.SendPickInfoReply(pick.PickId,pick.CreatorId,pick.TopPick,pick.ParcelId,pick.Name,
                                           pick.Desc,pick.SnapshotId,pick.ParcelName,pick.OriginalName,pick.SimName,
                                           globalPos,pick.SortOrder,pick.Enabled);
        }

        public UserProfilePick GetPick(UUID targetID, UUID PickId)
        {
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetID, out UserProfileCacheEntry uce) && uce is not null)
                {
                    if(uce.picks is not null && uce.picks.TryGetValue(PickId, out UserProfilePick cpick))
                        return cpick;
                }
            }

            UserProfilePick pick = new()
            {
                PickId = PickId,
                CreatorId = targetID
            };

            bool foreign =  GetUserProfileServerURI (targetID, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return null;

            object Pick = (object)pick;
            if (!rpc.JsonRpcRequest (ref Pick, "pickinforequest", serverURI, UUID.Random ().ToString ())) {
                return null;
            }

            pick = (UserProfilePick)Pick;
            if(foreign)
                cacheForeignImage(targetID, pick.SnapshotId);

            if(!Vector3d.TryParse(pick.GlobalPos, out Vector3d globalPos))
                return null;

            if (m_thisGridInfo.IsLocalGrid(pick.Gatekeeper, true) == 0)
            {
                // Setup the illusion
                string region = string.Format("{0} {1}",pick.Gatekeeper,pick.SimName);
                GridRegion target = Scene.GridService.GetRegionByName(Scene.RegionInfo.ScopeID, region);

                if(target is null)
                {
                    // This is a unreachable region
                }
                else
                {
                    // we have a proxy on map
                    if (Util.ParseFakeParcelID(pick.ParcelId, out ulong _, out uint oriX, out uint oriY))
                    {
                        pick.ParcelId = Util.BuildFakeParcelID(target.RegionHandle, oriX, oriY);
                        globalPos.X = target.RegionLocX + oriX;
                        globalPos.Y = target.RegionLocY + oriY;
                        pick.GlobalPos = globalPos.ToString();
                    }
                    else
                    {
                        // this is a fail on large regions
                        uint gtmp = (uint)globalPos.X >> 8;
                        globalPos.X -= (gtmp << 8);

                        gtmp = (uint)globalPos.Y >> 8;
                        globalPos.Y -= (gtmp << 8);

                        pick.ParcelId = Util.BuildFakeParcelID(target.RegionHandle, (uint)globalPos.X, (uint)globalPos.Y);

                        globalPos.X += target.RegionLocX;
                        globalPos.Y += target.RegionLocY;
                        pick.GlobalPos = globalPos.ToString();
                    }
                }
            }
            return pick;
        }

        /// <summary>
        /// Updates the userpicks
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        /// <param name='pickID'>
        /// Pick I.
        /// </param>
        /// <param name='creatorID'>
        /// the creator of the pick
        /// </param>
        /// <param name='topPick'>
        /// Top pick.
        /// </param>
        /// <param name='name'>
        /// Name.
        /// </param>
        /// <param name='desc'>
        /// Desc.
        /// </param>
        /// <param name='snapshotID'>
        /// Snapshot I.
        /// </param>
        /// <param name='sortOrder'>
        /// Sort order.
        /// </param>
        /// <param name='enabled'>
        /// Enabled.
        /// </param>
        public void PickInfoUpdate(IClientAPI remoteClient, UUID pickID, UUID creatorID, bool topPick, string name, string desc, UUID snapshotID, int sortOrder, bool enabled)
        {
            //m_log.DebugFormat("[PROFILES]: Start PickInfoUpdate Name: {0} PickId: {1} SnapshotId: {2}", name, pickID.ToString(), snapshotID.ToString());

            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            ScenePresence p = FindPresence(remoteClient.AgentId);
            if(p is null)
                return;

            if(remoteClient.AgentId.NotEqual(creatorID) && !p.IsViewerUIGod)
                return;

            UserProfilePick pick = null;
            Dictionary<UUID, string> curpicks = GetPicks(creatorID);
            if(!curpicks.ContainsKey(pickID))
            { 
                if(curpicks is not null && curpicks.Count >= Constants.MaxProfilePicks)
                {
                    remoteClient.SendAvatarPicksReply(remoteClient.AgentId, curpicks);
                    return;
                }
            }
            else
                pick = GetPick(creatorID, pickID);

            Vector3d posGlobal;

            if(pick is null)
            {
                Vector3 avaPos = p.AbsolutePosition;
                // Getting the global position for the Avatar
                posGlobal = new(remoteClient.Scene.RegionInfo.WorldLocX + avaPos.X,
                                        remoteClient.Scene.RegionInfo.WorldLocY + avaPos.Y,
                                        avaPos.Z);

                string  landParcelName  = "My Parcel";

                // to locate parcels we use a fake id that encodes the region handle
                // since we do not have a global locator
                // this fails on HG
                UUID  landParcelID = Util.BuildFakeParcelID(remoteClient.Scene.RegionInfo.RegionHandle, (uint)avaPos.X, (uint)avaPos.Y);
                ILandObject land = p.Scene.LandChannel.GetLandObject(avaPos.X, avaPos.Y);

                if (land is not null)
                {
                    // If land found, use parcel uuid from here because the value from SP will be blank if the avatar hasnt moved
                    landParcelName  = land.LandData.Name;
                }
                else
                {
                    m_log.WarnFormat(
                        "[PROFILES]: PickInfoUpdate found no parcel info at {0},{1} in {2}",
                        avaPos.X, avaPos.Y, p.Scene.Name);
                }

                pick = new()
                {
                    PickId = pickID,
                    CreatorId = creatorID,
                    TopPick = topPick,
                    Name = name,
                    Desc = desc,
                    ParcelId = landParcelID,
                    SnapshotId = snapshotID,
                    ParcelName = landParcelName,
                    SimName = remoteClient.Scene.RegionInfo.RegionName,
                    Gatekeeper = m_thisGridInfo.GateKeeperURLNoEndSlash,
                    GlobalPos = posGlobal.ToString(),
                    SortOrder = sortOrder,
                    Enabled = enabled
                };
            }
            else
            {
                    pick.TopPick = topPick;
                    pick.Name = name;
                    pick.Desc = desc;
                    pick.SnapshotId = snapshotID;
                    pick.SortOrder = sortOrder;
                    pick.Enabled = enabled;
            }

            object Pick = (object)pick;
            if(!rpc.JsonRpcRequest(ref Pick, "picks_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error updating pick", false);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(creatorID, out uce) || uce is null)
                    uce = new UserProfileCacheEntry();
                uce.picks ??= new Dictionary<UUID, UserProfilePick>();
                uce.picksList ??= new Dictionary<UUID, string>();
                uce.picks[pick.PickId] = pick;
                uce.picksList[pick.PickId] = pick.Name;
                m_profilesCache.AddOrUpdate(creatorID, uce, PROFILECACHEEXPIRE);
            }

            _ = Vector3d.TryParse(pick.GlobalPos, out posGlobal);
            remoteClient.SendAvatarPicksReply(creatorID, uce.picksList);
            remoteClient.SendPickInfoReply(pick.PickId,pick.CreatorId,pick.TopPick,pick.ParcelId,pick.Name,
                                           pick.Desc,pick.SnapshotId,pick.ParcelName,pick.OriginalName,pick.SimName,
                                           posGlobal,pick.SortOrder,pick.Enabled);

            //m_log.DebugFormat("[PROFILES]: Finish PickInfoUpdate {0} {1}", pick.Name, pick.PickId.ToString());
        }

        /// <summary>
        /// Delete a Pick
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        /// <param name='queryPickID'>
        /// Query pick I.
        /// </param>
        public void PickDelete(IClientAPI remoteClient, UUID queryPickID)
        {
            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            OSDMap parameters = new() { { "pickId", OSD.FromUUID(queryPickID) } };
            OSD Params = (OSD)parameters;
            if(!rpc.JsonRpcRequest(ref Params, "picks_delete", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error picks delete", false);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce is not null)
                {
                    uce.picks?.Remove(queryPickID);
                    uce.picksList?.Remove(queryPickID);
                    m_profilesCache.AddOrUpdate(remoteClient.AgentId, uce, PROFILECACHEEXPIRE);
                }
            }
            if(uce is not null && uce.picksList is not null)
                remoteClient.SendAvatarPicksReply(remoteClient.AgentId, uce.picksList);
            else
                remoteClient.SendAvatarPicksReply(remoteClient.AgentId, new Dictionary<UUID, string>());
        }
        #endregion Picks

        #region Notes
        /// <summary>
        /// Handles the avatar notes request.
        /// </summary>
        /// <param name='sender'>
        /// Sender.
        /// </param>
        /// <param name='method'>
        /// Method.
        /// </param>
        /// <param name='args'>
        /// Arguments.
        /// </param>
        public void NotesRequest(Object sender, string method, List<String> args)
        {
            if (sender is not IClientAPI remoteClient)
                return;

            UserProfileNotes note = new();

            if (!UUID.TryParse(args[0], out note.TargetId))
                return;

            note.UserId = remoteClient.AgentId;

            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                remoteClient.SendAvatarNotesReply(note.TargetId, note.Notes);
                return;
            }

            object Note = (object)note;
            if(!rpc.JsonRpcRequest(ref Note, "avatarnotesrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAvatarNotesReply(note.TargetId, note.Notes);
                return;
            }
            note = (UserProfileNotes) Note;
            remoteClient.SendAvatarNotesReply(note.TargetId, note.Notes);
        }

        /// <summary>
        /// Avatars the notes update.
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        /// <param name='queryTargetID'>
        /// Query target I.
        /// </param>
        /// <param name='queryNotes'>
        /// Query notes.
        /// </param>
        public void NotesUpdate(IClientAPI remoteClient, UUID queryTargetID, string queryNotes)
        {
            if (queryTargetID.Equals(Constants.m_MrOpenSimID))
                return;

            ScenePresence p = FindPresence(queryTargetID);
            if (p is not null && p.IsNPC)
            {
                remoteClient.SendAgentAlertMessage("Notes for NPCs not available", false);
                return;
            }

            UserProfileNotes note = new()
            {
                UserId = remoteClient.AgentId,
                TargetId = queryTargetID,
                Notes = queryNotes
            };

            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            object Note = note;
            if(!rpc.JsonRpcRequest(ref Note, "avatar_notes_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error updating note", false);
                return;
            }
        }
        #endregion Notes


        #region User Preferences
        /// <summary>
        /// Updates the user preferences.
        /// </summary>
        /// <param name='imViaEmail'>
        /// Im via email.
        /// </param>
        /// <param name='visible'>
        /// Visible.
        /// </param>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        public void UpdateUserPreferences(bool imViaEmail, bool visible, IClientAPI remoteClient)
        {
            UserPreferences pref = new()
            {
                UserId = remoteClient.AgentId,
                IMViaEmail = imViaEmail,
                Visible = visible
            };

            _ = GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            object Pref = pref;
            if(!rpc.JsonRpcRequest(ref Pref, "user_preferences_update", serverURI, UUID.Random().ToString()))
            {
                m_log.InfoFormat("[PROFILES]: UserPreferences update error");
                remoteClient.SendAgentAlertMessage("Error updating preferences", false);
                return;
            }
        }

        /// <summary>
        /// Users the preferences request.
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        public void UserPreferencesRequest(IClientAPI remoteClient)
        {

            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            UserPreferences pref = new() { UserId = remoteClient.AgentId };
            object Pref = (object)pref;
            if(!rpc.JsonRpcRequest(ref Pref, "user_preferences_request", serverURI, UUID.Random().ToString()))
            {
                //m_log.InfoFormat("[PROFILES]: UserPreferences request error");
                //remoteClient.SendAgentAlertMessage("Error requesting preferences", false);
                return;
            }
            pref = (UserPreferences) Pref;

            remoteClient.SendUserInfoReply(pref.IMViaEmail, pref.Visible, pref.EMail);

        }
        #endregion User Preferences

        #region Avatar Properties
        /// <summary>
        /// Update the avatars interests .
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        /// <param name='wantmask'>
        /// Wantmask.
        /// </param>
        /// <param name='wanttext'>
        /// Wanttext.
        /// </param>
        /// <param name='skillsmask'>
        /// Skillsmask.
        /// </param>
        /// <param name='skillstext'>
        /// Skillstext.
        /// </param>
        /// <param name='languages'>
        /// Languages.
        /// </param>
        public void AvatarInterestsUpdate(IClientAPI remoteClient, uint wantmask, string wanttext, uint skillsmask, string skillstext, string languages)
        {
            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if (string.IsNullOrWhiteSpace(serverURI))
                return;

            object Param = new UserProfileProperties()
            {
                UserId = remoteClient.AgentId,
                WantToMask = (int)wantmask,
                WantToText = wanttext,
                SkillsMask = (int)skillsmask,
                SkillsText = skillstext,
                Language = languages
            };

            if(!rpc.JsonRpcRequest(ref Param, "avatar_interests_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error updating interests", false);
                return;
            }

            // flush cache
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out UserProfileCacheEntry uce) && uce is not null)
                {
                    uce.props = null;
                    uce.ClientsWaitingProps = null;
                }
            }
            RequestAvatarProperties(remoteClient, remoteClient.AgentId);
        }

        public void RequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (avatarID.IsZero())
            {
                // Looking for a reason that some viewers are sending null Id's
                m_log.Debug("[PROFILES]: got request of null ID");
                return;
            }

            if (avatarID.Equals(Constants.m_MrOpenSimID))
            {
                remoteClient.SendAvatarProperties(avatarID, "Creator of OpenSimulator shared assets library", Constants.m_MrOpenSimBorn.ToString(),
                      Utils.StringToBytes("System agent"), "MrOpenSim has no life", 0x10,
                      UUID.Zero, UUID.Zero, "", UUID.Zero);
                remoteClient.SendAvatarInterestsReply(avatarID, 0, "",
                          0, "Getting into trouble", "Droidspeak");
                return;
            }
            ScenePresence p = FindPresence(avatarID);
            if (p is not null && p.IsNPC)
            {
                remoteClient.SendAvatarProperties(avatarID, ((INPC)(p.ControllingClient)).profileAbout, ((INPC)(p.ControllingClient)).Born,
                      Utils.StringToBytes("Non Player Character (NPC)"), "NPCs have no life", 0x10,
                      UUID.Zero, ((INPC)(p.ControllingClient)).profileImage, "", UUID.Zero);
                remoteClient.SendAvatarInterestsReply(avatarID, 0, "",
                          0, "Getting into trouble", "Droidspeak");
                return;
            }
            UserProfileProperties props;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(avatarID, out UserProfileCacheEntry uce) && uce is not null)
                {
                    if(uce.props is not null)
                    {
                        props = uce.props;
                        uint cflags = uce.flags;

                        if (IsFriendOnline(remoteClient, avatarID))
                            cflags = (uint)ProfileFlags.Online;
                        else
                            cflags &= (uint)~ProfileFlags.Online;

                        remoteClient.SendAvatarProperties(props.UserId, props.AboutText,
                            uce.born, uce.membershipType , props.FirstLifeText, cflags,
                            props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                        remoteClient.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask,
                            props.WantToText, (uint)props.SkillsMask,
                            props.SkillsText, props.Language);
                        if(uce.avatarGroups is not null)
                            remoteClient.SendAvatarGroupsReply(avatarID, uce.avatarGroups);
                        return;
                    }
                    else
                    {
                        if(uce.ClientsWaitingProps == null)
                            uce.ClientsWaitingProps = new HashSet<IClientAPI>();
                        else if(uce.ClientsWaitingProps.Contains(remoteClient))
                            return;
                        uce.ClientsWaitingProps.Add(remoteClient);
                    }
                }
                else
                {
                    uce = new UserProfileCacheEntry { ClientsWaitingProps = new HashSet<IClientAPI>() };
                    uce.ClientsWaitingProps.Add(remoteClient);
                    m_profilesCache.AddOrUpdate(avatarID, uce, PROFILECACHEEXPIRE);
                }
            }

            AsyncPropsRequest req = new()
            {
                client = remoteClient,
                presence = p,
                agent = avatarID,
                reqtype = 0
            };

            m_asyncRequests.Push(req);

            if (Monitor.TryEnter(m_asyncRequestsLock))
            {
                if (!m_asyncRequestsRunning)
                {
                    m_asyncRequestsRunning = true;
                    Util.FireAndForget(x => ProcessRequests());
                }
                Monitor.Exit(m_asyncRequestsLock);
            }

            /*
            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(avatarID, out serverURI);

            UserAccount account = null;
            Dictionary<string,object> userInfo;

            if (!foreign)
            {
                account = Scene.UserAccountService.GetUserAccount(Scene.RegionInfo.ScopeID, avatarID);
            }
            else
            {
                userInfo = new Dictionary<string, object>();
            }

            Byte[] membershipType = new Byte[1];
            string born = string.Empty;
            uint flags = 0x00;

            if (null != account)
            {
                if (account.UserTitle.Length == 0)
                    membershipType[0] = (Byte)((account.UserFlags & 0xf00) >> 8);
                else
                    membershipType = Utils.StringToBytes(account.UserTitle);

                born = Util.ToDateTime(account.Created).ToString(
                                  "M/d/yyyy", CultureInfo.InvariantCulture);
                flags = (uint)(account.UserFlags & 0xff);
            }
            else
            {
                if (GetUserAccountData(avatarID, out userInfo) == true)
                {
                    if ((string)userInfo["user_title"].Length == 0)
                        membershipType[0] = (Byte)(((Byte)userInfo["user_flags"] & 0xf00) >> 8);
                    else
                        membershipType = Utils.StringToBytes((string)userInfo["user_title"]);

                    int val_born = (int)userInfo["user_created"];
                    if(val_born != 0)
                        born = Util.ToDateTime(val_born).ToString(
                                  "M/d/yyyy", CultureInfo.InvariantCulture);

                    // picky, picky
                    int val_flags = (int)userInfo["user_flags"];
                    flags = (uint)(val_flags & 0xff);
                }
            }

            props = new UserProfileProperties();
            props.UserId = avatarID;

            string result = string.Empty;
            if(!GetProfileData(ref props, foreign, serverURI, out result))
            {
                props.AboutText ="Profile not available at this time. User may still be unknown to this grid";
            }

            if(!m_allowUserProfileWebURLs)
                props.WebUrl ="";

            HashSet<IClientAPI> clients;
            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(props.UserId, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                uce.props = props;
                uce.born = born;
                uce.membershipType = membershipType;
                uce.flags = flags;
                clients = uce.ClientsWaitingProps;
                uce.ClientsWaitingProps = null;
                m_profilesCache.AddOrUpdate(props.UserId, uce, PROFILECACHEEXPIRE);
            }

            // if on same region force online
            if(p != null && !p.IsDeleted)
                flags |= 0x10;

            if(clients == null)
            {
                remoteClient.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType , props.FirstLifeText, flags,
                                              props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                remoteClient.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                             (uint)props.SkillsMask, props.SkillsText, props.Language);
            }
            else
            {
                if(!clients.Contains(remoteClient))
                    clients.Add(remoteClient);
                foreach(IClientAPI cli in clients)
                {
                    if(!cli.IsActive)
                        continue;
                    cli.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType , props.FirstLifeText, flags,
                                              props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                    cli.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                             (uint)props.SkillsMask, props.SkillsText, props.Language);

                }
            }
            */
        }

        /// <summary>
        /// Updates the avatar properties.
        /// </summary>
        /// <param name='remoteClient'>
        /// Remote client.
        /// </param>
        /// <param name='newProfile'>
        /// New profile.
        /// </param>
        public void AvatarPropertiesUpdate(IClientAPI remoteClient, UserProfileProperties newProfile)
        {
            GetUserProfileServerURI(remoteClient.AgentId, out string serverURI);
            if (string.IsNullOrWhiteSpace(serverURI))
                return;

            if (!m_allowUserProfileWebURLs)
                newProfile.WebUrl = string.Empty;

            object Prop = newProfile;
            if(!rpc.JsonRpcRequest(ref Prop, "avatar_properties_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error updating properties", false);
                return;
            }

            // flush cache
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out UserProfileCacheEntry uce) && uce is not null)
                {
                    uce.props = null;
                    uce.ClientsWaitingProps = null;
                }
            }

            RequestAvatarProperties(remoteClient, remoteClient.AgentId);
        }

        /// <summary>
        /// Gets the profile data.
        /// </summary>
        /// <returns>
        /// The profile data.
        /// </returns>
        bool GetProfileData(ref UserProfileProperties properties, bool foreign, string serverURI, out string message)
        {
            if (string.IsNullOrEmpty(serverURI))
            {
                message = "User profile service unknown at this time";
                return false;
            }

            object Prop = properties;
            if (!rpc.JsonRpcRequest(ref Prop, "avatar_properties_request", serverURI, UUID.Random().ToString()))
            {
                // If it's a foreign user then try again using OpenProfile, in case that's what the grid is using
                bool secondChanceSuccess = false;
                if (foreign)
                {
                    try
                    {
                        OpenProfileClient client = new(serverURI);
                        if (client.RequestAvatarPropertiesUsingOpenProfile(ref properties))
                            secondChanceSuccess = true;
                    }
                    catch (Exception e)
                    {
                        m_log.Debug(
                                $"[PROFILES]: Request using the OpenProfile API for user {properties.UserId} to {serverURI} failed: {e.Message}");

                        // Allow the return 'message' to say "JsonRpcRequest" and not "OpenProfile", because
                        // the most likely reason that OpenProfile failed is that the remote server
                        // doesn't support OpenProfile, and that's not very interesting.
                    }
                }

                if (!secondChanceSuccess)
                {
                    message = $"JsonRpcRequest for user {properties.UserId} to {serverURI} failed";
                    m_log.Debug($"[PROFILES]: {message}");
                    return false;
                }
            }

            properties = (UserProfileProperties)Prop;
            if(foreign)
            {
                cacheForeignImage(properties.UserId, properties.ImageId);
                cacheForeignImage(properties.UserId, properties.FirstLifeImageId);
            }

            message = "Success";
            return true;
        }
        #endregion Avatar Properties

        #region Utils

        /// <summary>
        /// Gets the user account data.
        /// </summary>
        /// <returns>
        /// The user profile data.
        /// </returns>
        /// <param name='userID'>
        /// If set to <c>true</c> user I.
        /// </param>
        /// <param name='userInfo'>
        /// If set to <c>true</c> user info.
        /// </param>
        bool GetUserAccountData(UUID userID, out UserAccount account)
        {
            account = null;
            if (UserManagementModule.IsLocalGridUser(userID))
            {
                // Is local
                IUserAccountService uas = Scene.UserAccountService;
                account = uas.GetUserAccount(Scene.RegionInfo.ScopeID, userID);
                return account is not null;
            }
            else
            {
                // Is Foreign
                string home_url = UserManagementModule.GetUserServerURL(userID, "HomeURI", out bool recentFailedWeb);
                if (recentFailedWeb || string.IsNullOrEmpty(home_url))
                    return false;

                UserAgentServiceConnector uConn = new(home_url);

                Dictionary<string, object> info;
                try
                {
                    info = uConn.GetUserInfo(userID);
                }
                catch (Exception e)
                {
                    m_log.Debug("[PROFILES]: GetUserInfo call failed ", e);
                    UserManagementModule.UserWebFailed(userID);
                    return false;
                }

                if (info.Count == 0)
                    return false;

                account = new UserAccount();
                if (info.ContainsKey("user_flags"))
                    account.UserFlags = (int)info["user_flags"];

                if (info.ContainsKey("user_created"))
                    account.Created = (int)info["user_created"];

                account.UserTitle = "HG Visitor";
                return true;
            }
        }

        /// <summary>
        /// Gets the user profile server UR.
        /// </summary>
        /// <returns>
        /// The user profile server UR.
        /// </returns>
        /// <param name='userID'>
        /// If set to <c>true</c> user I.
        /// </param>
        /// <param name='serverURI'>
        /// If set to <c>true</c> server UR.
        /// </param>
        bool GetUserProfileServerURI(UUID userID, out string serverURI)
        {
            if (!UserManagementModule.IsLocalGridUser(userID))
            {
                serverURI = UserManagementModule.GetUserServerURL(userID, "ProfileServerURI", out bool failed);
                if(failed)
                    serverURI = string.Empty;
                // Is Foreign
                return true;
            }
            else
            {
                serverURI = ProfileServerUri;
                // Is local
                return false;
            }
        }

        void cacheForeignImage(UUID agent, UUID imageID)
        {
            if(imageID.IsZero())
                return;

            string assetServerURI = UserManagementModule.GetUserServerURL(agent, "AssetServerURI");
            if(string.IsNullOrWhiteSpace(assetServerURI))
                return;

            Scene.AssetService.Get(imageID.ToString(), assetServerURI, false);
        }

        /// <summary>
        /// Finds the presence.
        /// </summary>
        /// <returns>
        /// The presence.
        /// </returns>
        /// <param name='clientID'>
        /// Client I.
        /// </param>
        ScenePresence FindPresence(UUID clientID)
        {
            ScenePresence p = Scene.GetScenePresence(clientID);
            if (p is not null && !p.IsChildAgent)
                return p;

            return null;
        }

        public virtual bool IsFriendOnline(IClientAPI client, UUID agent)
        {
            // if on same region force online
            ScenePresence p = Scene.GetScenePresence(agent);
            if (p is not null && !p.IsChildAgent && !p.IsDeleted)
                return true;

            IFriendsModule friendsModule = Scene.RequestModuleInterface<IFriendsModule>();
            if (friendsModule is not null && friendsModule.IsFriendOnline(client.AgentId, agent))
                return true;

            if(client.SceneAgent is ScenePresence sp && sp.IsViewerUIGod)
            {
                Services.Interfaces.PresenceInfo[] pi = Scene.PresenceService?.GetAgents(new string[] { agent.ToString() });
                return pi is not null && pi.Length > 0;
            }

            return false;
         }
   
    #endregion Util

    #region Web Util
        /// <summary>
        /// Sends json-rpc request with a serializable type.
        /// </summary>
        /// <returns>
        /// OSD Map.
        /// </returns>
        /// <param name='parameters'>
        /// Serializable type .
        /// </param>
        /// <param name='method'>
        /// Json-rpc method to call.
        /// </param>
        /// <param name='uri'>
        /// URI of json-rpc service.
        /// </param>
        /// <param name='jsonId'>
        /// Id for our call.
        /// </param>
        bool JsonRpcRequest(ref object parameters, string method, string uri, string jsonId)
        {
            return rpc?.JsonRpcRequest(ref parameters, method, uri, jsonId) ?? false;
        }

        /// <summary>
        /// Sends json-rpc request with OSD parameter.
        /// </summary>
        /// <returns>
        /// The rpc request.
        /// </returns>
        /// <param name='data'>
        /// data - incoming as parameters, outgong as result/error
        /// </param>
        /// <param name='method'>
        /// Json-rpc method to call.
        /// </param>
        /// <param name='uri'>
        /// URI of json-rpc service.
        /// </param>
        /// <param name='jsonId'>
        /// If set to <c>true</c> json identifier.
        /// </param>
        bool JsonRpcRequest(ref OSD data, string method, string uri, string jsonId)
        {
            return rpc?.JsonRpcRequest(ref data, method, uri, jsonId) ?? false;
        }
        #endregion Web Util
    }
}
