
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

using log4net;
using Mono.Addins;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Profile
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalUserProfilesServicesConnector")]
    public class LocalUserProfilesServicesConnector : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, Scene> regions = new Dictionary<UUID, Scene>();

        public IUserProfilesService ServiceModule
        {
            get; private set;
        }

         public bool Enabled
        {
            get; private set;
        }

        public string Name
        {
            get
            {
                return "LocalUserProfilesServicesConnector";
            }
        }

        public string ConfigName
        {
            get; private set;
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public LocalUserProfilesServicesConnector()
        {
            //m_log.Debug("[LOCAL USERPROFILES SERVICE CONNECTOR]: LocalUserProfileServicesConnector no params");
        }

        public LocalUserProfilesServicesConnector(IConfigSource source)
        {
            //m_log.Debug("[LOCAL USERPROFILES SERVICE CONNECTOR]: LocalUserProfileServicesConnector instantiated directly.");
            InitialiseService(source);
        }

        public void InitialiseService(IConfigSource source)
        {
            ConfigName = "UserProfilesService";

            // Instantiate the request handler
            IHttpServer Server = MainServer.Instance;

            IConfig config = source.Configs[ConfigName];
            if (config == null)
            {
                //m_log.Error("[LOCAL USERPROFILES SERVICE CONNECTOR]: UserProfilesService missing from OpenSim.ini");
                return;
            }

            if(!config.GetBoolean("Enabled",false))
            {
                Enabled = false;
                return;
            }

            Enabled = true;

            string serviceDll = config.GetString("LocalServiceModule",
                                                      String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[LOCAL USERPROFILES SERVICE CONNECTOR]: No LocalServiceModule named in section UserProfilesService");
                return;
            }

            Object[] args = new Object[] { source, ConfigName };
            ServiceModule =
                ServerUtils.LoadPlugin<IUserProfilesService>(serviceDll,
                                                     args);

            if (ServiceModule == null)
            {
                m_log.Error("[LOCAL USERPROFILES SERVICE CONNECTOR]: Can't load user profiles service");
                return;
            }

            Enabled = true;

            JsonRpcProfileHandlers handler = new JsonRpcProfileHandlers(ServiceModule);

            Server.AddJsonRPCHandler("avatarclassifiedsrequest", handler.AvatarClassifiedsRequest);
            Server.AddJsonRPCHandler("classified_update", handler.ClassifiedUpdate);
            Server.AddJsonRPCHandler("classifieds_info_query", handler.ClassifiedInfoRequest);
            Server.AddJsonRPCHandler("classified_delete", handler.ClassifiedDelete);
            Server.AddJsonRPCHandler("avatarpicksrequest", handler.AvatarPicksRequest);
            Server.AddJsonRPCHandler("pickinforequest", handler.PickInfoRequest);
            Server.AddJsonRPCHandler("picks_update", handler.PicksUpdate);
            Server.AddJsonRPCHandler("picks_delete", handler.PicksDelete);
            Server.AddJsonRPCHandler("avatarnotesrequest", handler.AvatarNotesRequest);
            Server.AddJsonRPCHandler("avatar_notes_update", handler.NotesUpdate);
            Server.AddJsonRPCHandler("avatar_properties_request", handler.AvatarPropertiesRequest);
            Server.AddJsonRPCHandler("avatar_properties_update", handler.AvatarPropertiesUpdate);
            Server.AddJsonRPCHandler("avatar_interests_update", handler.AvatarInterestsUpdate);
            Server.AddJsonRPCHandler("user_preferences_update", handler.UserPreferenecesUpdate);
            Server.AddJsonRPCHandler("user_preferences_request", handler.UserPreferencesRequest);
            Server.AddJsonRPCHandler("image_assets_request", handler.AvatarImageAssetsRequest);
            Server.AddJsonRPCHandler("user_data_request", handler.RequestUserAppData);
            Server.AddJsonRPCHandler("user_data_update", handler.UpdateUserAppData);

        }

        #region ISharedRegionModule implementation

        void ISharedRegionModule.PostInitialise()
        {
            if(!Enabled)
                return;
        }

        #endregion

        #region IRegionModuleBase implementation

        void IRegionModuleBase.Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("UserProfilesServices", "");
                if (name == Name)
                {
                    InitialiseService(source);
                    m_log.Info("[LOCAL USERPROFILES SERVICE CONNECTOR]: Local user profiles connector enabled");
                }
            }
        }

        void IRegionModuleBase.Close()
        {
            return;
        }

        void IRegionModuleBase.AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            lock (regions)
            {
                if (regions.ContainsKey(scene.RegionInfo.RegionID))
                    m_log.ErrorFormat("[LOCAL USERPROFILES SERVICE CONNECTOR]: simulator seems to have more than one region with the same UUID. Please correct this!");
                else
                    regions.Add(scene.RegionInfo.RegionID, scene);
            }
        }

        void IRegionModuleBase.RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            lock (regions)
            {
                if (regions.ContainsKey(scene.RegionInfo.RegionID))
                    regions.Remove(scene.RegionInfo.RegionID);
            }
        }

        void IRegionModuleBase.RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;
        }
        #endregion
    }
}
