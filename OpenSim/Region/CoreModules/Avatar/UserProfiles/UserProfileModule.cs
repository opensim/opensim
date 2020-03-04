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
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Xml;
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
using Microsoft.CSharp;

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
        Dictionary<UUID, UUID> m_classifiedCache = new Dictionary<UUID, UUID>();
        Dictionary<UUID, int> m_classifiedInterest = new Dictionary<UUID, int>();
        ExpiringCache<UUID, UserProfileCacheEntry> m_profilesCache = new ExpiringCache<UUID, UserProfileCacheEntry>();
        IAssetCache m_assetCache;
        IGroupsModule m_groupsModule = null;

        static readonly UUID m_MrOpenSimID = new UUID("11111111-1111-0000-0000-000100bba000");
        static readonly DateTime m_MrOpenSimBorn = new DateTime(2007,1,1,0,0,0,DateTimeKind.Utc);

        private JsonRpcRequestManager rpc = new JsonRpcRequestManager();
        private bool m_allowUserProfileWebURLs = true;

        struct AsyncPropsRequest
        {
            public IClientAPI client;
            public ScenePresence presence;
            public UUID agent;
            public int reqtype;
        }

        private ConcurrentQueue<AsyncPropsRequest> m_asyncRequests = new ConcurrentQueue<AsyncPropsRequest>();
        private object m_asyncRequestsLock = new object();
        private bool m_asyncRequestsRunning = false;

        private void ProcessRequests()
        {
            lock(m_asyncRequestsLock)
            {
                try
                {
                    while(m_asyncRequests.TryDequeue(out AsyncPropsRequest req))
                    {
                        IClientAPI client = req.client;
                        if(!client.IsActive)
                            continue;

                        if(req.reqtype == 0)
                        {
                            UUID avatarID = req.agent;
                            ScenePresence p = req.presence;

                            string serverURI = string.Empty;
                            bool foreign = GetUserProfileServerURI(avatarID, out serverURI);

                            UserAccount account = null;

                            if (!foreign)
                                account = Scene.UserAccountService.GetUserAccount(Scene.RegionInfo.ScopeID, avatarID);

                            Byte[] membershipType = new Byte[1];
                            string born = string.Empty;
                            uint flags = 0x00;

                            if (null != account)
                            {
                                if (account.UserTitle == "")
                                    membershipType[0] = (Byte)((account.UserFlags & 0xf00) >> 8);
                                else
                                    membershipType = Utils.StringToBytes(account.UserTitle);

                                born = Util.ToDateTime(account.Created).ToString(
                                                  "M/d/yyyy", CultureInfo.InvariantCulture);
                                flags = (uint)(account.UserFlags & 0xff);
                            }
                            else
                            {
                                if (GetUserAccountData(avatarID, out Dictionary<string, object> userInfo) == true)
                                {
                                    if ((string)userInfo["user_title"] == "")
                                        membershipType[0] = (Byte)(((Byte)userInfo["user_flags"] & 0xf00) >> 8);
                                    else
                                        membershipType = Utils.StringToBytes((string)userInfo["user_title"]);

                                    int val_born = (int)userInfo["user_created"];
                                    if (val_born != 0)
                                        born = Util.ToDateTime(val_born).ToString(
                                                  "M/d/yyyy", CultureInfo.InvariantCulture);

                                    // picky, picky
                                    int val_flags = (int)userInfo["user_flags"];
                                    flags = (uint)(val_flags & 0xff);
                                }
                            }

                            UserProfileProperties props = new UserProfileProperties();
                            props.UserId = avatarID;

                            string result = string.Empty;
                            if (!GetProfileData(ref props, foreign, serverURI, out result))
                            {
                                props.AboutText = "Profile not available at this time. User may still be unknown to this grid";
                            }

                            if (!m_allowUserProfileWebURLs)
                                props.WebUrl = "";

                            GroupMembershipData[] agentGroups = null;
                            if(m_groupsModule != null)
                                agentGroups = m_groupsModule.GetMembershipData(avatarID);

                            HashSet<IClientAPI> clients;
                            lock (m_profilesCache)
                            {
                                if (!m_profilesCache.TryGetValue(props.UserId, out UserProfileCacheEntry uce) || uce == null)
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

                            // if on same region force online
                            if (p != null && !p.IsDeleted)
                                flags |= 0x10;

                            if(!clients.Contains(client) && client.IsActive)
                            {
                                client.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType, props.FirstLifeText, flags,
                                                              props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                                client.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                                             (uint)props.SkillsMask, props.SkillsText, props.Language);
                                if (agentGroups != null)
                                    client.SendAvatarGroupsReply(avatarID, agentGroups);
                            }
                            foreach (IClientAPI cli in clients)
                            {
                                if (!cli.IsActive)
                                    continue;
                                cli.SendAvatarProperties(props.UserId, props.AboutText, born, membershipType, props.FirstLifeText, flags,
                                                            props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                                cli.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask, props.WantToText,
                                                            (uint)props.SkillsMask, props.SkillsText, props.Language);
                                if (agentGroups != null)
                                    cli.SendAvatarGroupsReply(avatarID, agentGroups);
                            }
                        }
                    }
                    m_asyncRequestsRunning = false;
                }
                catch { }
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

        public string MyGatekeeper
        {
            get; private set;
        }

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

            if (profileConfig == null)
            {
                //m_log.Debug("[PROFILES]: UserProfiles disabled, no configuration");
                Enabled = false;
                return;
            }

            // If we find ProfileURL then we configure for FULL support
            // else we setup for BASIC support
            ProfileServerUri = profileConfig.GetString("ProfileServiceURL", "");
            if (ProfileServerUri == "")
            {
                Enabled = false;
                return;
            }

            m_allowUserProfileWebURLs = profileConfig.GetBoolean("AllowUserProfileWebURLs", m_allowUserProfileWebURLs);

            m_log.Debug("[PROFILES]: Full Profiles Enabled");
            ReplaceableInterface = null;
            Enabled = true;

            MyGatekeeper = Util.GetConfigVarFromSections<string>(source, "GatekeeperURI",
                new string[] { "Startup", "Hypergrid", "UserProfiles" }, String.Empty);
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
            m_assetCache = Scene.RequestModuleInterface<IAssetCache>();
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
            if (client == null)
                return;

            //Profile
            client.OnRequestAvatarProperties -= RequestAvatarProperties;
            client.OnUpdateAvatarProperties  -= AvatarPropertiesUpdate;
            client.OnAvatarInterestUpdate    -= AvatarInterestsUpdate;

            // Classifieds
//            client.r GenericPacketHandler("avatarclassifiedsrequest", ClassifiedsRequest);
            client.OnClassifiedInfoUpdate    -= ClassifiedInfoUpdate;
            client.OnClassifiedInfoRequest   -= ClassifiedInfoRequest;
            client.OnClassifiedDelete        -= ClassifiedDelete;

            // Picks
//            client.AddGenericPacketHandler("avatarpicksrequest", PicksRequest);
//            client.AddGenericPacketHandler("pickinforequest", PickInfoRequest);
            client.OnPickInfoUpdate -= PickInfoUpdate;
            client.OnPickDelete     -= PickDelete;

            // Notes
//            client.AddGenericPacketHandler("avatarnotesrequest", NotesRequest);
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
            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;
            Dictionary<UUID, string> classifieds = new Dictionary<UUID, string>();

            UUID targetID;
            if(!UUID.TryParse(args[0], out targetID) || targetID == UUID.Zero)
                return;

            if (targetID == m_MrOpenSimID)
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            ScenePresence p = FindPresence(targetID);
            if (p != null && p.IsNPC)
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetID, out uce) && uce != null)
                {
                    if(uce.classifiedsLists != null)
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

            string serverURI = string.Empty;
            GetUserProfileServerURI(targetID, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            OSDMap parameters= new OSDMap();

            parameters.Add("creatorId", OSD.FromUUID(targetID));
            OSD Params = (OSD)parameters;
            if(!rpc.JsonRpcRequest(ref Params, "avatarclassifiedsrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            parameters = (OSDMap)Params;

            if(!parameters.ContainsKey("result") || parameters["result"] == null)
            {
                remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
                return;
            }

            OSDArray list = (OSDArray)parameters["result"];

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
                if(!m_profilesCache.TryGetValue(targetID, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                uce.classifiedsLists = classifieds;

                m_profilesCache.AddOrUpdate(targetID, uce, PROFILECACHEEXPIRE);
            }

            remoteClient.SendAvatarClassifiedReply(targetID, classifieds);
        }

        public void ClassifiedInfoRequest(UUID queryClassifiedID, IClientAPI remoteClient)
        {
            UUID target = remoteClient.AgentId;
            UserClassifiedAdd ad = new UserClassifiedAdd();
            ad.ClassifiedId = queryClassifiedID;

            lock (m_classifiedCache)
            {
                if (m_classifiedCache.ContainsKey(queryClassifiedID))
                {
                    target = m_classifiedCache[queryClassifiedID];

                    m_classifiedInterest[queryClassifiedID] --;

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
                if(m_profilesCache.TryGetValue(target, out uce) && uce != null)
                {
                    if(uce.classifieds != null && uce.classifieds.ContainsKey(queryClassifiedID))
                    {
                        ad = uce.classifieds[queryClassifiedID];
                        Vector3 gPos = new Vector3();
                        Vector3.TryParse(ad.GlobalPos, out gPos);

                        remoteClient.SendClassifiedInfoReply(ad.ClassifiedId, ad.CreatorId, (uint)ad.CreationDate,
                                (uint)ad.ExpirationDate, (uint)ad.Category, ad.Name, ad.Description,
                                ad.ParcelId, (uint)ad.ParentEstate, ad.SnapshotId, ad.SimName,
                                gPos, ad.ParcelName, ad.Flags, ad.Price);
                        return;
                    }
                }
            }

            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(target, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            object Ad = (object)ad;
            if(!rpc.JsonRpcRequest(ref Ad, "classifieds_info_query", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error getting classified info", false);
                return;
            }
            ad = (UserClassifiedAdd) Ad;

            if(ad.CreatorId == UUID.Zero)
                return;

            if(foreign)
                cacheForeignImage(target, ad.SnapshotId);

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(target, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                if(uce.classifieds == null)
                    uce.classifieds = new Dictionary<UUID, UserClassifiedAdd>();
                uce.classifieds[ad.ClassifiedId] = ad;

                m_profilesCache.AddOrUpdate(target, uce, PROFILECACHEEXPIRE);
            }

            Vector3 globalPos = new Vector3();
            Vector3.TryParse(ad.GlobalPos, out globalPos);

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

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
                m_profilesCache.TryGetValue(remoteClient.AgentId, out uce);

            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            if(foreign)
            {
                remoteClient.SendAgentAlertMessage("Please change classifieds on your home grid", true);
                if(uce != null && uce.classifiedsLists != null)
                     remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                return;
            }

            OSDMap parameters = new OSDMap {{"creatorId", OSD.FromUUID(creatorId)}};
            OSD Params = (OSD)parameters;
            if (!rpc.JsonRpcRequest(ref Params, "avatarclassifiedsrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error fetching classifieds", false);
                return;
            }
            parameters = (OSDMap)Params;
            OSDArray list = (OSDArray)parameters["result"];
            bool exists = list.Cast<OSDMap>().Where(map => map.ContainsKey("classifieduuid"))
              .Any(map => map["classifieduuid"].AsUUID().Equals(queryclassifiedID));

            IMoneyModule money = null;
            if (!exists)
            {
                money = s.RequestModuleInterface<IMoneyModule>();
                if (money != null)
                {
                    if (!money.AmountCovered(remoteClient.AgentId, queryclassifiedPrice))
                    {
                        remoteClient.SendAgentAlertMessage("You do not have enough money to create this classified.", false);
                        if(uce != null && uce.classifiedsLists != null)
                            remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                        return;
                    }
//                    money.ApplyCharge(remoteClient.AgentId, queryclassifiedPrice, MoneyTransactionType.ClassifiedCharge);
                }
            }

            UserClassifiedAdd ad = new UserClassifiedAdd();

            ad.ParcelName = land == null ? string.Empty : land.LandData.Name;
            ad.CreatorId = remoteClient.AgentId;
            ad.ClassifiedId = queryclassifiedID;
            ad.Category = Convert.ToInt32(queryCategory);
            ad.Name = queryName;
            ad.Description = queryDescription;
            ad.ParentEstate = Convert.ToInt32(queryParentEstate);
            ad.SnapshotId = querySnapshotID;
            ad.SimName = remoteClient.Scene.RegionInfo.RegionName;
            ad.GlobalPos = queryGlobalPos.ToString ();
            ad.Flags = queryclassifiedFlags;
            ad.Price = queryclassifiedPrice;
            ad.ParcelId = p.currentParcelUUID;

            object Ad = ad;

            OSD.SerializeMembers(Ad);

            if(!rpc.JsonRpcRequest(ref Ad, "classified_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage("Error updating classified", false);
                if(uce != null && uce.classifiedsLists != null)
                    remoteClient.SendAvatarClassifiedReply(remoteClient.AgentId, uce.classifiedsLists);
                return;
            }

            // only charge if it worked
            if (money != null)
                money.ApplyCharge(remoteClient.AgentId, queryclassifiedPrice, MoneyTransactionType.ClassifiedCharge);

            // just flush cache for now
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce != null)
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

            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            if(foreign)
            {
                remoteClient.SendAgentAlertMessage("Please change classifieds on your home grid", true);
                return;
            }

            UUID classifiedId;
            if(!UUID.TryParse(queryClassifiedID.ToString(), out classifiedId))
                return;

            OSDMap parameters= new OSDMap();
            parameters.Add("classifiedId", OSD.FromUUID(classifiedId));
            OSD Params = (OSD)parameters;
            if(!rpc.JsonRpcRequest(ref Params, "classified_delete", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error classified delete", false);
                return;
            }

            // flush cache
            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce != null)
                {
                    uce.classifieds = null;
                    uce.classifiedsLists = null;
                }
            }

            parameters = (OSDMap)Params;
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
            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;

            UUID targetId;
            if(!UUID.TryParse(args[0], out targetId))
                return;

            Dictionary<UUID, string> picks = new Dictionary<UUID, string>();

            if (targetId == m_MrOpenSimID)
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            ScenePresence p = FindPresence(targetId);
            if (p != null && p.IsNPC)
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetId, out uce) && uce != null)
                {
                    if(uce != null && uce.picksList != null)
                    {
                        remoteClient.SendAvatarPicksReply(targetId, uce.picksList);
                        return;
                    }
                }
            }

            string serverURI = string.Empty;
            GetUserProfileServerURI(targetId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            OSDMap parameters= new OSDMap();
            parameters.Add("creatorId", OSD.FromUUID(targetId));
            OSD Params = (OSD)parameters;
            if(!rpc.JsonRpcRequest(ref Params, "avatarpicksrequest", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }

            parameters = (OSDMap)Params;
            if(!parameters.ContainsKey("result") || parameters["result"] == null)
            {
                remoteClient.SendAvatarPicksReply(targetId, picks);
                return;
            }
            OSDArray list = (OSDArray)parameters["result"];

            foreach(OSD map in list)
            {
                OSDMap m = (OSDMap)map;
                UUID cid = m["pickuuid"].AsUUID();
                string name = m["name"].AsString();
                picks[cid] = name;
            }

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetId, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                uce.picksList = picks;

                m_profilesCache.AddOrUpdate(targetId, uce, PROFILECACHEEXPIRE);
            }

            remoteClient.SendAvatarPicksReply(targetId, picks);
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
            if (!(sender is IClientAPI))
                return;

            UserProfilePick pick = new UserProfilePick ();
            UUID targetID;
            if(!UUID.TryParse(args [0], out targetID))
                return;

            pick.CreatorId = targetID;

            if(!UUID.TryParse (args [1], out pick.PickId))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;
            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(targetID, out uce) && uce != null)
                {
                    if(uce != null && uce.picks != null && uce.picks.ContainsKey(pick.PickId))
                    {
                        pick = uce.picks[pick.PickId];
                        Vector3 gPos = new Vector3(Vector3.Zero);
                        Vector3.TryParse(pick.GlobalPos, out gPos);
                        remoteClient.SendPickInfoReply(pick.PickId,pick.CreatorId,pick.TopPick,pick.ParcelId,pick.Name,
                                           pick.Desc,pick.SnapshotId,pick.ParcelName,pick.OriginalName,pick.SimName,
                                           gPos,pick.SortOrder,pick.Enabled);
                        return;
                    }
                }
            }

            string serverURI = string.Empty;
            bool foreign =  GetUserProfileServerURI (targetID, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            string theirGatekeeperURI;
            GetUserGatekeeperURI(targetID, out theirGatekeeperURI);

            object Pick = (object)pick;
            if (!rpc.JsonRpcRequest (ref Pick, "pickinforequest", serverURI, UUID.Random ().ToString ())) {
                remoteClient.SendAgentAlertMessage (
                        "Error selecting pick", false);
                return;
            }
            pick = (UserProfilePick)Pick;
            if(foreign)
                cacheForeignImage(targetID, pick.SnapshotId);

            Vector3 globalPos = new Vector3(Vector3.Zero);
            Vector3.TryParse(pick.GlobalPos, out globalPos);

            if (!string.IsNullOrWhiteSpace(MyGatekeeper) && pick.Gatekeeper != MyGatekeeper)
            {
                // Setup the illusion
                string region = string.Format("{0} {1}",pick.Gatekeeper,pick.SimName);
                GridRegion target = Scene.GridService.GetRegionByName(Scene.RegionInfo.ScopeID, region);

                if(target == null)
                {
                    // This is a unreachable region
                }
                else
                {
                    // we have a proxy on map
                    ulong oriHandle;
                    uint oriX;
                    uint oriY;
                    if(Util.ParseFakeParcelID(pick.ParcelId, out oriHandle, out oriX, out oriY))
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

            m_log.DebugFormat("[PROFILES]: PickInfoRequest: {0} : {1}", pick.Name.ToString(), pick.SnapshotId.ToString());

            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(targetID, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                if(uce.picks == null)
                    uce.picks = new Dictionary<UUID, UserProfilePick>();
                uce.picks[pick.PickId] = pick;

                m_profilesCache.AddOrUpdate(targetID, uce, PROFILECACHEEXPIRE);
            }

            // Pull the rabbit out of the hat
            remoteClient.SendPickInfoReply(pick.PickId,pick.CreatorId,pick.TopPick,pick.ParcelId,pick.Name,
                                           pick.Desc,pick.SnapshotId,pick.ParcelName,pick.OriginalName,pick.SimName,
                                           globalPos,pick.SortOrder,pick.Enabled);
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
            m_log.DebugFormat("[PROFILES]: Start PickInfoUpdate Name: {0} PickId: {1} SnapshotId: {2}", name, pickID.ToString(), snapshotID.ToString());

            UserProfilePick pick = new UserProfilePick();
            string serverURI = string.Empty;
            GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            ScenePresence p = FindPresence(remoteClient.AgentId);

            Vector3 avaPos = p.AbsolutePosition;
            // Getting the global position for the Avatar
            Vector3 posGlobal = new Vector3(remoteClient.Scene.RegionInfo.WorldLocX + avaPos.X,
                                            remoteClient.Scene.RegionInfo.WorldLocY + avaPos.Y,
                                            avaPos.Z);

            string  landParcelName  = "My Parcel";
//            UUID    landParcelID    = p.currentParcelUUID;

            // to locate parcels we use a fake id that encodes the region handle
            // since we do not have a global locator
            // this fails on HG
            UUID  landParcelID = Util.BuildFakeParcelID(remoteClient.Scene.RegionInfo.RegionHandle, (uint)avaPos.X, (uint)avaPos.Y);
            ILandObject land = p.Scene.LandChannel.GetLandObject(avaPos.X, avaPos.Y);

            if (land != null)
            {
                // If land found, use parcel uuid from here because the value from SP will be blank if the avatar hasnt moved
                landParcelName  = land.LandData.Name;
//                landParcelID    = land.LandData.GlobalID;
            }
            else
            {
                m_log.WarnFormat(
                    "[PROFILES]: PickInfoUpdate found no parcel info at {0},{1} in {2}",
                    avaPos.X, avaPos.Y, p.Scene.Name);
            }

            pick.PickId = pickID;
            pick.CreatorId = creatorID;
            pick.TopPick = topPick;
            pick.Name = name;
            pick.Desc = desc;
            pick.ParcelId = landParcelID;
            pick.SnapshotId = snapshotID;
            pick.ParcelName = landParcelName;
            pick.SimName = remoteClient.Scene.RegionInfo.RegionName;
            pick.Gatekeeper = MyGatekeeper;
            pick.GlobalPos = posGlobal.ToString();
            pick.SortOrder = sortOrder;
            pick.Enabled = enabled;

            object Pick = (object)pick;
            if(!rpc.JsonRpcRequest(ref Pick, "picks_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error updating pick", false);
                return;
            }

            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(!m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) || uce == null)
                    uce = new UserProfileCacheEntry();
                if(uce.picks == null)
                    uce.picks = new Dictionary<UUID, UserProfilePick>();
                if(uce.picksList == null)
                    uce.picksList = new Dictionary<UUID, string>();
                uce.picks[pick.PickId] = pick;
                uce.picksList[pick.PickId] = pick.Name;
                m_profilesCache.AddOrUpdate(remoteClient.AgentId, uce, PROFILECACHEEXPIRE);
            }
            remoteClient.SendAvatarPicksReply(remoteClient.AgentId, uce.picksList);
            remoteClient.SendPickInfoReply(pick.PickId,pick.CreatorId,pick.TopPick,pick.ParcelId,pick.Name,
                                           pick.Desc,pick.SnapshotId,pick.ParcelName,pick.OriginalName,pick.SimName,
                                           posGlobal,pick.SortOrder,pick.Enabled);

            m_log.DebugFormat("[PROFILES]: Finish PickInfoUpdate {0} {1}", pick.Name, pick.PickId.ToString());
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
            string serverURI = string.Empty;
            GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
            {
                return;
            }

            OSDMap parameters= new OSDMap();
            parameters.Add("pickId", OSD.FromUUID(queryPickID));
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
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce != null)
                {
                    if(uce.picks != null && uce.picks.ContainsKey(queryPickID))
                        uce.picks.Remove(queryPickID);
                    if(uce.picksList != null && uce.picksList.ContainsKey(queryPickID))
                        uce.picksList.Remove(queryPickID);
                    m_profilesCache.AddOrUpdate(remoteClient.AgentId, uce, PROFILECACHEEXPIRE);
                }
            }
            if(uce != null && uce.picksList != null)
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
            UserProfileNotes note = new UserProfileNotes();

            if (!(sender is IClientAPI))
                return;

            if(!UUID.TryParse(args[0], out note.TargetId))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;
            note.UserId = remoteClient.AgentId;

            string serverURI = string.Empty;
            GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
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
            if (queryTargetID == m_MrOpenSimID)
                return;

            ScenePresence p = FindPresence(queryTargetID);
            if (p != null && p.IsNPC)
            {
                remoteClient.SendAgentAlertMessage(
                        "Notes for NPCs not available", false);
                return;
            }

            UserProfileNotes note = new UserProfileNotes();

            note.UserId = remoteClient.AgentId;
            note.TargetId = queryTargetID;
            note.Notes = queryNotes;

            string serverURI = string.Empty;
            GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
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
            UserPreferences pref = new UserPreferences();

            pref.UserId = remoteClient.AgentId;
            pref.IMViaEmail = imViaEmail;
            pref.Visible = visible;

            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
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
            UserPreferences pref = new UserPreferences();

            pref.UserId = remoteClient.AgentId;

            string serverURI = string.Empty;
            bool foreign = GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            object Pref = (object)pref;
            if(!rpc.JsonRpcRequest(ref Pref, "user_preferences_request", serverURI, UUID.Random().ToString()))
            {
//                m_log.InfoFormat("[PROFILES]: UserPreferences request error");
//                remoteClient.SendAgentAlertMessage("Error requesting preferences", false);
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

            UserProfileProperties prop = new UserProfileProperties();

            prop.UserId = remoteClient.AgentId;
            prop.WantToMask = (int)wantmask;
            prop.WantToText = wanttext;
            prop.SkillsMask = (int)skillsmask;
            prop.SkillsText = skillstext;
            prop.Language = languages;

            string serverURI = string.Empty;
            GetUserProfileServerURI(remoteClient.AgentId, out serverURI);
            if(string.IsNullOrWhiteSpace(serverURI))
                return;

            object Param = prop;
            if(!rpc.JsonRpcRequest(ref Param, "avatar_interests_update", serverURI, UUID.Random().ToString()))
            {
                remoteClient.SendAgentAlertMessage(
                        "Error updating interests", false);
                return;
            }

            // flush cache
            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce != null)
                {
                    uce.props = null;
                }
            }

        }

        public void RequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (avatarID == UUID.Zero)
            {
                // Looking for a reason that some viewers are sending null Id's
                m_log.Debug("[PROFILES]: got request of null ID");
                return;
            }

            if (avatarID == m_MrOpenSimID)
            {
                remoteClient.SendAvatarProperties(avatarID, "Creator of OpenSimulator shared assets library", m_MrOpenSimBorn.ToString(),
                      Utils.StringToBytes("System agent"), "MrOpenSim has no life", 0x10,
                      UUID.Zero, UUID.Zero, "", UUID.Zero);
                remoteClient.SendAvatarInterestsReply(avatarID, 0, "",
                          0, "Getting into trouble", "Droidspeak");
                return;
            }
            ScenePresence p = FindPresence(avatarID);
            if (p != null && p.IsNPC)
            {
                remoteClient.SendAvatarProperties(avatarID, ((INPC)(p.ControllingClient)).profileAbout, ((INPC)(p.ControllingClient)).Born,
                      Utils.StringToBytes("Non Player Character (NPC)"), "NPCs have no life", 0x10,
                      UUID.Zero, ((INPC)(p.ControllingClient)).profileImage, "", UUID.Zero);
                remoteClient.SendAvatarInterestsReply(avatarID, 0, "",
                          0, "Getting into trouble", "Droidspeak");
                return;
            }
            UserProfileProperties props;
            UserProfileCacheEntry uce = null;
            lock(m_profilesCache)
            {
                if(m_profilesCache.TryGetValue(avatarID, out uce) && uce != null)
                {
                    if(uce.props != null)
                    {
                        props = uce.props;
                        uint cflags = uce.flags;
                        // if on same region force online
                        if(p != null && !p.IsDeleted)
                            cflags |= 0x10;

                        remoteClient.SendAvatarProperties(props.UserId, props.AboutText,
                            uce.born, uce.membershipType , props.FirstLifeText, cflags,
                            props.FirstLifeImageId, props.ImageId, props.WebUrl, props.PartnerId);

                        remoteClient.SendAvatarInterestsReply(props.UserId, (uint)props.WantToMask,
                            props.WantToText, (uint)props.SkillsMask,
                            props.SkillsText, props.Language);
                        if(uce.avatarGroups != null)
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
                    uce = new UserProfileCacheEntry();
                    uce.ClientsWaitingProps = new HashSet<IClientAPI>();
                    uce.ClientsWaitingProps.Add(remoteClient);
                    m_profilesCache.AddOrUpdate(avatarID, uce, PROFILECACHEEXPIRE);
                }
            }

            AsyncPropsRequest req = new AsyncPropsRequest();
            req.client = remoteClient;
            req.presence = p;
            req.agent = avatarID;
            req.reqtype = 0;

            m_asyncRequests.Enqueue(req);
            if(Monitor.TryEnter(m_asyncRequestsLock))
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
                if (account.UserTitle == "")
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
                    if ((string)userInfo["user_title"] == "")
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
        public void AvatarPropertiesUpdate(IClientAPI remoteClient, UserProfileData newProfile)
        {
            if (remoteClient.AgentId == newProfile.ID)
            {

                UserProfileProperties prop = new UserProfileProperties();

                prop.UserId = remoteClient.AgentId;
                prop.WebUrl = newProfile.ProfileUrl;
                prop.ImageId = newProfile.Image;
                prop.AboutText = newProfile.AboutText;
                prop.FirstLifeImageId = newProfile.FirstLifeImage;
                prop.FirstLifeText = newProfile.FirstLifeAboutText;

                if(!m_allowUserProfileWebURLs)
                    prop.WebUrl ="";

                string serverURI = string.Empty;
                GetUserProfileServerURI(remoteClient.AgentId, out serverURI);

                object Prop = prop;

                if(!rpc.JsonRpcRequest(ref Prop, "avatar_properties_update", serverURI, UUID.Random().ToString()))
                {
                    remoteClient.SendAgentAlertMessage(
                            "Error updating properties", false);
                    return;
                }

                // flush cache
                UserProfileCacheEntry uce = null;
                lock(m_profilesCache)
                {
                    if(m_profilesCache.TryGetValue(remoteClient.AgentId, out uce) && uce != null)
                    {
                        uce.props = null;
                    }
                }

                RequestAvatarProperties(remoteClient, newProfile.ID);
            }
        }

        /// <summary>
        /// Gets the profile data.
        /// </summary>
        /// <returns>
        /// The profile data.
        /// </returns>
        bool GetProfileData(ref UserProfileProperties properties, bool foreign, string serverURI, out string message)
        {
            if (String.IsNullOrEmpty(serverURI))
            {
                message = "User profile service unknown at this time";
                return false;
            }

            object Prop = (object)properties;
            if (!rpc.JsonRpcRequest(ref Prop, "avatar_properties_request", serverURI, UUID.Random().ToString()))
            {
                // If it's a foreign user then try again using OpenProfile, in case that's what the grid is using
                bool secondChanceSuccess = false;
                if (foreign)
                {
                    try
                    {
                        OpenProfileClient client = new OpenProfileClient(serverURI);
                        if (client.RequestAvatarPropertiesUsingOpenProfile(ref properties))
                            secondChanceSuccess = true;
                    }
                    catch (Exception e)
                    {
                        m_log.Debug(
                            string.Format(
                                "[PROFILES]: Request using the OpenProfile API for user {0} to {1} failed",
                                properties.UserId, serverURI),
                            e);

                        // Allow the return 'message' to say "JsonRpcRequest" and not "OpenProfile", because
                        // the most likely reason that OpenProfile failed is that the remote server
                        // doesn't support OpenProfile, and that's not very interesting.
                    }
                }

                if (!secondChanceSuccess)
                {
                    message = string.Format("JsonRpcRequest for user {0} to {1} failed", properties.UserId, serverURI);
                    m_log.DebugFormat("[PROFILES]: {0}", message);

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
        bool GetUserAccountData(UUID userID, out Dictionary<string, object> userInfo)
        {
            Dictionary<string,object> info = new Dictionary<string, object>();

            if (UserManagementModule.IsLocalGridUser(userID))
            {
                // Is local
                IUserAccountService uas = Scene.UserAccountService;
                UserAccount account = uas.GetUserAccount(Scene.RegionInfo.ScopeID, userID);

                info["user_flags"] = account.UserFlags;
                info["user_created"] = account.Created;

                if (!String.IsNullOrEmpty(account.UserTitle))
                    info["user_title"] = account.UserTitle;
                else
                    info["user_title"] = "";

                userInfo = info;

                return false;
            }
            else
            {
                // Is Foreign
                string home_url = UserManagementModule.GetUserServerURL(userID, "HomeURI");

                if (String.IsNullOrEmpty(home_url))
                {
                    info["user_flags"] = 0;
                    info["user_created"] = 0;
                    info["user_title"] = "Unavailable";

                    userInfo = info;
                    return true;
                }

                UserAgentServiceConnector uConn = new UserAgentServiceConnector(home_url);

                Dictionary<string, object> account;
                try
                {
                    account = uConn.GetUserInfo(userID);
                }
                catch (Exception e)
                {
                    m_log.Debug("[PROFILES]: GetUserInfo call failed ", e);
                    account = new Dictionary<string, object>();
                }

                if (account.Count > 0)
                {
                    if (account.ContainsKey("user_flags"))
                        info["user_flags"] = account["user_flags"];
                    else
                        info["user_flags"] = "";

                    if (account.ContainsKey("user_created"))
                        info["user_created"] = account["user_created"];
                    else
                        info["user_created"] = "";

                    info["user_title"] = "HG Visitor";
                }
                else
                {
                   info["user_flags"] = 0;
                   info["user_created"] = 0;
                   info["user_title"] = "HG Visitor";
                }
                userInfo = info;
                return true;
            }
        }

        /// <summary>
        /// Gets the user gatekeeper server URI.
        /// </summary>
        /// <returns>
        /// The user gatekeeper server URI.
        /// </returns>
        /// <param name='userID'>
        /// If set to <c>true</c> user URI.
        /// </param>
        /// <param name='serverURI'>
        /// If set to <c>true</c> server URI.
        /// </param>
        bool GetUserGatekeeperURI(UUID userID, out string serverURI)
        {
            bool local;
            local = UserManagementModule.IsLocalGridUser(userID);

            if (!local)
            {
                serverURI = UserManagementModule.GetUserServerURL(userID, "GatekeeperURI");
                // Is Foreign
                return true;
            }
            else
            {
                serverURI = MyGatekeeper;
                // Is local
                return false;
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
            bool local;
            local = UserManagementModule.IsLocalGridUser(userID);

            if (!local)
            {
                serverURI = UserManagementModule.GetUserServerURL(userID, "ProfileServerURI");
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
            if(imageID == null || imageID == UUID.Zero)
                return;

            string assetServerURI = UserManagementModule.GetUserServerURL(agent, "AssetServerURI");
            if(string.IsNullOrWhiteSpace(assetServerURI))
                return;

            string imageIDstr = imageID.ToString();


            if(m_assetCache != null && m_assetCache.Check(imageIDstr))
                return;

            if(Scene.AssetService.Get(imageIDstr) != null)
                return;

            Scene.AssetService.Get(string.Format("{0}/{1}", assetServerURI, imageIDstr));
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
            ScenePresence p;

            p = Scene.GetScenePresence(clientID);
            if (p != null && !p.IsChildAgent)
                return p;

            return null;
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
            if (jsonId == null)
                throw new ArgumentNullException ("jsonId");
            if (uri == null)
                throw new ArgumentNullException ("uri");
            if (method == null)
                throw new ArgumentNullException ("method");
            if (parameters == null)
                throw new ArgumentNullException ("parameters");

            // Prep our payload
            OSDMap json = new OSDMap();

            json.Add("jsonrpc", OSD.FromString("2.0"));
            json.Add("id", OSD.FromString(jsonId));
            json.Add("method", OSD.FromString(method));

            json.Add("params", OSD.SerializeMembers(parameters));

            string jsonRequestData = OSDParser.SerializeJsonString(json);
            byte[] content = Encoding.UTF8.GetBytes(jsonRequestData);

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";

            WebResponse webResponse = null;
            try
            {
                using(Stream dataStream = webRequest.GetRequestStream())
                    dataStream.Write(content,0,content.Length);

                webResponse = webRequest.GetResponse();
            }
            catch (WebException e)
            {
                Console.WriteLine("Web Error" + e.Message);
                Console.WriteLine ("Please check input");
                return false;
            }

            OSDMap mret = new OSDMap();

            using (Stream rstream = webResponse.GetResponseStream())
            {
                try
                {
                    mret = (OSDMap)OSDParser.DeserializeJson(rstream);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[PROFILES]: JsonRpcRequest Error {0} - remote user with legacy profiles?", e.Message);
                    if (webResponse != null)
                        webResponse.Close();
                    return false;
                }
            }

            if (webResponse != null)
                webResponse.Close();

            if (mret.ContainsKey("error"))
                return false;

            // get params...
            OSD.DeserializeMembers(ref parameters, (OSDMap) mret["result"]);
            return true;
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
            OSDMap map = new OSDMap();

            map["jsonrpc"] = "2.0";
            if(string.IsNullOrEmpty(jsonId))
                map["id"] = UUID.Random().ToString();
            else
                map["id"] = jsonId;

            map["method"] = method;
            map["params"] = data;

            string jsonRequestData = OSDParser.SerializeJsonString(map);
            byte[] content = Encoding.UTF8.GetBytes(jsonRequestData);

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";

            WebResponse webResponse = null;
            try
            {
                using(Stream dataStream = webRequest.GetRequestStream())
                    dataStream.Write(content,0,content.Length);

                webResponse = webRequest.GetResponse();
            }
            catch (WebException e)
            {
                Console.WriteLine("Web Error" + e.Message);
                Console.WriteLine ("Please check input");
                return false;
            }

            OSDMap response = new OSDMap();

            using (Stream rstream = webResponse.GetResponseStream())
            {
                try
                {
                    response = (OSDMap)OSDParser.DeserializeJson(rstream);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[PROFILES]: JsonRpcRequest Error {0} - remote user with legacy profiles?", e.Message);
                    if (webResponse != null)
                        webResponse.Close();
                    return false;
                }
            }

            if (webResponse != null)
                webResponse.Close();

            if(response.ContainsKey("error"))
            {
                data = response["error"];
                return false;
            }

            data = response;

            return true;
        }
        #endregion Web Util
    }
}
