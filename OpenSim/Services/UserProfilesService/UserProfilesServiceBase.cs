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
using log4net;
using OpenSim.Services.Base;
using OpenSim.Data;

namespace OpenSim.Services.ProfilesService
{
    public class UserProfilesServiceBase: ServiceBase
    {
        static readonly ILog m_log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public IProfilesData ProfilesData;

        public string ConfigName
        {
            get; private set;
        }

        public UserProfilesServiceBase(IConfigSource config, string configName):
            base(config)
        {
            if(string.IsNullOrEmpty(configName))
            {
                m_log.WarnFormat("[PROFILES SERVICE]: Configuration section not given!");
                return;
            }

            string dllName = String.Empty;
            string connString = null;
            string realm = String.Empty;

            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (string.IsNullOrEmpty(connString))
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            IConfig ProfilesConfig = config.Configs[configName];
            if (ProfilesConfig != null)
            {
                dllName = ProfilesConfig.GetString("StorageProvider", dllName);
                connString = ProfilesConfig.GetString("ConnectionString", connString);
                realm = ProfilesConfig.GetString("Realm", realm);
            }

            ProfilesData = LoadPlugin<IProfilesData>(dllName, new Object[] { connString });
            if (ProfilesData == null)
                throw new Exception("Could not find a storage interface in the given module");

        }
    }
}

