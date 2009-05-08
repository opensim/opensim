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

        public AssetServiceBase(IConfigSource config) : base(config)
        {
            IConfig assetConfig = config.Configs["AssetService"];
            if (assetConfig == null)
                throw new Exception("No AssetService configuration");

            string dllName = assetConfig.GetString("StorageProvider",
                    String.Empty);

            if (dllName == String.Empty)
                throw new Exception("No StorageProvider configured");

            string connString = assetConfig.GetString("ConnectionString",
                    String.Empty);

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
