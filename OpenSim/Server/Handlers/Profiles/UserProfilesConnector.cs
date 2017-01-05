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
using System.Reflection;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework;
using OpenSim.Server.Handlers.Base;
using log4net;

namespace OpenSim.Server.Handlers.Profiles
{
    public class UserProfilesConnector: ServiceConnector
    {
//        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Our Local Module
        public IUserProfilesService ServiceModule
        {
            get; private set;
        }

        // The HTTP server.
        public IHttpServer Server
        {
            get; private set;
        }

        public bool Enabled
        {
            get; private set;
        }

        public UserProfilesConnector(IConfigSource config, IHttpServer server, string configName) :
            base(config, server, configName)
        {
            ConfigName = "UserProfilesService";
            if(!string.IsNullOrEmpty(configName))
                ConfigName = configName;

            IConfig serverConfig = config.Configs[ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", ConfigName));

            if(!serverConfig.GetBoolean("Enabled",false))
            {
                Enabled = false;
                return;
            }

            Enabled = true;

            Server = server;

            string service = serverConfig.GetString("LocalServiceModule", String.Empty);

            Object[] args = new Object[] { config, ConfigName };
            ServiceModule = ServerUtils.LoadPlugin<IUserProfilesService>(service, args);

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
    }
}