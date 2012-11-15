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
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.AssetService
{
    public class AssetServiceBase : ServiceBase
    {
        protected IAssetDataPlugin m_Database = null;
        protected IAssetLoader m_AssetLoader = null;
        protected string m_ConfigName = "AssetService";

        public AssetServiceBase(IConfigSource config)
            : this(config, "AssetService")
        {
        }

        public AssetServiceBase(IConfigSource config, string configName) : base(config)
        {
            if (configName != string.Empty)
                m_ConfigName = configName;

            string dllName = String.Empty;
            string connString = String.Empty;

            //
            // Try reading the [AssetService] section, if it exists
            //
            IConfig assetConfig = config.Configs[m_ConfigName];
            if (assetConfig != null)
            {
                dllName = assetConfig.GetString("StorageProvider", dllName);
                connString = assetConfig.GetString("ConnectionString", connString);
            }

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(String.Empty))
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IAssetDataPlugin>(dllName);
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");

            m_Database.Initialise(connString);

            string loaderName = assetConfig.GetString("DefaultAssetLoader",
                    String.Empty);

            if (loaderName != String.Empty)
            {
                m_AssetLoader = LoadPlugin<IAssetLoader>(loaderName);

                if (m_AssetLoader == null)
                    throw new Exception("Asset loader could not be loaded");
            }
        }
    }
}
