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
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.InventoryService
{
    public class InventoryServiceBase : ServiceBase
    {
        protected IInventoryDataPlugin m_Database = null;

        protected List<IInventoryDataPlugin> m_plugins = new List<IInventoryDataPlugin>();

        public InventoryServiceBase(IConfigSource config) : base(config)
        {
            IConfig assetConfig = config.Configs["InventoryService"];
            if (assetConfig == null)
                throw new Exception("No InventoryService configuration");

            string dllName = assetConfig.GetString("StorageProvider",
                    String.Empty);

            if (dllName == String.Empty)
                throw new Exception("No StorageProvider configured");

            string connString = assetConfig.GetString("ConnectionString",
                    String.Empty);

            m_Database = LoadPlugin<IInventoryDataPlugin>(dllName);
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");

            m_Database.Initialise(connString);

        }

        #region Plugin methods

        /// <summary>
        /// Add a new inventory data plugin - plugins will be requested in the order they were added.
        /// </summary>
        /// <param name="plugin">The plugin that will provide data</param>
        public void AddPlugin(IInventoryDataPlugin plugin)
        {
            m_plugins.Add(plugin);
        }

        /// <summary>
        /// Adds a list of inventory data plugins, as described by `provider'
        /// and `connect', to `m_plugins'.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public void AddPlugin(string provider, string connect)
        {
            m_plugins.AddRange(DataPluginFactory.LoadDataPlugins<IInventoryDataPlugin>(provider, connect));
        }

        #endregion


    }
}
