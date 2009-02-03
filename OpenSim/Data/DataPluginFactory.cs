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

using System.Collections.Generic;
using OpenSim.Framework;

namespace OpenSim.Data
{
    /// <summary>
    /// A static class containing a series of methods for obtaining handles to
    /// database storage objects.
    /// </summary>
    // Yeah, it's not really a factory, but maybe it'll morph into one?
    public static class DataPluginFactory
    {
        /// <summary>
        /// Returns a list of new inventory data plugins. Plugins will be
        /// requested in the order they were added.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public static List<IInventoryDataPlugin> LoadInventoryDataPlugins(string provider, string connect)
        {
            PluginLoader<IInventoryDataPlugin> loader = new PluginLoader<IInventoryDataPlugin> (new InventoryDataInitialiser(connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add ("/OpenSim/InventoryData", new PluginProviderFilter(provider));
            loader.Load();

            return loader.Plugins;
        }

        /// <summary>
        /// Returns a list of new user data plugins. Plugins will be requested
        /// in the order they were added.
        /// </summary>
        /// <param name="provider">
        /// The filename of the user data plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public static List<IUserDataPlugin> LoadUserDataPlugins(string provider, string connect)
        {
            PluginLoader<IUserDataPlugin> loader = new PluginLoader<IUserDataPlugin>(new UserDataInitialiser(connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add("/OpenSim/UserData", new PluginProviderFilter(provider));
            loader.Load();

            return loader.Plugins;
        }

        /// <summary>
        /// Returns a list of new grid data plugins. Plugins will be requested
        /// in the order they were added.
        /// </summary>
        /// <param name="provider">
        /// The filename of the user data plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public static List<IGridDataPlugin> LoadGridDataPlugins(string provider, string connect)
        {
            PluginLoader<IGridDataPlugin> loader = new PluginLoader<IGridDataPlugin>(new GridDataInitialiser(connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add("/OpenSim/GridData", new PluginProviderFilter(provider));
            loader.Load();

            return loader.Plugins;
        }

        /// <summary>
        /// Returns a list of new log data plugins. Plugins will be requested
        /// in the order they were added.
        /// </summary>
        /// <param name="provider">
        /// The filename of the user data plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public static List<ILogDataPlugin> LoadLogDataPlugins(string provider, string connect)
        {
            PluginLoader<ILogDataPlugin> loader = new PluginLoader<ILogDataPlugin>(new LogDataInitialiser(connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add("/OpenSim/LogData", new PluginProviderFilter(provider));
            loader.Load();

            return loader.Plugins;
        }

        public static IAssetDataPlugin LoadAssetDataPlugin(string provider, string connect)
        {
            PluginLoader<IAssetDataPlugin> loader = new PluginLoader<IAssetDataPlugin> (new AssetDataInitialiser (connect));

            // loader will try to load all providers (MySQL, MSSQL, etc)
            // unless it is constrainted to the correct "Provider" entry in the addin.xml
            loader.Add ("/OpenSim/AssetData", new PluginProviderFilter (provider));
            loader.Load();

            return loader.Plugin;
        }

    }
}
