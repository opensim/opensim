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
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mono.Addins.Setup;
using Mono.Addins;
using Mono.Addins.Description;
using OpenSim.Framework;

namespace OpenSim.Server.Base
{
    /// <summary>
    /// Command manager -
    /// Wrapper for OpenSim.Framework.PluginManager to allow
    /// us to add commands to the console to perform operations
    /// on our repos and plugins
    /// </summary>
    public class CommandManager
    {
        public AddinRegistry PluginRegistry;
        protected PluginManager PluginManager;

        public CommandManager(AddinRegistry registry)
        {
            PluginRegistry = registry;
            PluginManager = new PluginManager(PluginRegistry);
            AddManagementCommands();
        }

        private void AddManagementCommands()
        {
            // add plugin
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin add", "plugin add \"plugin index\"",
                                                     "Install plugin from repository.",
                                                     HandleConsoleInstallPlugin);

            // remove plugin
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin remove", "plugin remove \"plugin index\"",
                                                     "Remove plugin from repository",
                                                     HandleConsoleUnInstallPlugin);

            // list installed plugins
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin list installed",
                                                     "plugin list installed","List install plugins",
                                                     HandleConsoleListInstalledPlugin);

            // list plugins available from registered repositories
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin list available",
                                                     "plugin list available","List available plugins",
                                                     HandleConsoleListAvailablePlugin);
            // List available updates
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin updates", "plugin updates","List available updates",
                                                     HandleConsoleListUpdates);

            // Update plugin
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin update", "plugin update \"plugin index\"","Update the plugin",
                                                     HandleConsoleUpdatePlugin);

            // Add repository
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo add", "repo add \"url\"","Add repository",
                                                     HandleConsoleAddRepo);

            // Refresh repo
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo refresh", "repo refresh \"url\"", "Sync with a registered repository",
                                                     HandleConsoleGetRepo);

            // Remove repository from registry
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo remove",
                                                     "repo remove \"[url | index]\"",
                                                     "Remove repository from registry",
                                                     HandleConsoleRemoveRepo);

            // Enable repo
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo enable", "repo enable \"[url | index]\"",
                                                     "Enable registered repository",
                                                     HandleConsoleEnableRepo);

            // Disable repo
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo disable", "repo disable\"[url | index]\"",
                                                     "Disable registered repository",
                                                     HandleConsoleDisableRepo);

            // List registered repositories
            MainConsole.Instance.Commands.AddCommand("Repository", true,
                                                     "repo list", "repo list",
                                                     "List registered repositories",
                                                     HandleConsoleListRepos);

            // *
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin info", "plugin info \"plugin index\"","Show detailed information for plugin",
                                                     HandleConsoleShowAddinInfo);

            // Plugin disable
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin disable", "plugin disable \"plugin index\"",
                                                     "Disable a plugin",
                                                     HandleConsoleDisablePlugin);

            // Enable plugin
            MainConsole.Instance.Commands.AddCommand("Plugin", true,
                                                     "plugin enable", "plugin enable \"plugin index\"",
                                                     "Enable the selected plugin plugin",
                                                     HandleConsoleEnablePlugin);
        }

        #region console handlers
        // Handle our console commands
        //
        // Install plugin from registered repository
        /// <summary>
        /// Handles the console install plugin command. Attempts to install the selected plugin
        /// and
        /// </summary>
        /// <param name='module'>
        /// Module.
        /// </param>
        /// <param name='cmd'>
        /// Cmd.
        /// </param>
        private void HandleConsoleInstallPlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (cmd.Length == 3)
            {
                int ndx = Convert.ToInt16(cmd[2]);
                if (PluginManager.InstallPlugin(ndx, out result) == true)
                {
                    ArrayList s = new ArrayList();
                    s.AddRange(result.Keys);
                    s.Sort();

                    var list = result.Keys.ToList();
                    list.Sort();
                    foreach (var k in list)
                    {
                        Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                        bool enabled = (bool)plugin["enabled"];
                        MainConsole.Instance.Output("{0}) {1} {2} rev. {3}",
                                                  null,
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  plugin["name"], plugin["version"]);
                    }
                }
            }
            return;
        }

        // Remove installed plugin
        private void HandleConsoleUnInstallPlugin(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                int ndx = Convert.ToInt16(cmd[2]);
                PluginManager.UnInstall(ndx);
            }
            return;
        }

        // List installed plugins
        private void HandleConsoleListInstalledPlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            PluginManager.ListInstalledAddins(out result);

            ArrayList s = new ArrayList();
            s.AddRange(result.Keys);
            s.Sort();

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                bool enabled = (bool)plugin["enabled"];
                MainConsole.Instance.Output("{0}) {1} {2} rev. {3}",
                                                  null,
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  plugin["name"], plugin["version"]);
            }
            return;
        }

        // List available plugins on registered repositories
        private void HandleConsoleListAvailablePlugin(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            PluginManager.ListAvailable(out result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                // name, version, repository
                Dictionary<string, object> plugin = (Dictionary<string, object>)result[k];
                MainConsole.Instance.Output("{0}) {1} rev. {2} {3}",
                                                  null,
                                                  k,
                                                  plugin["name"],
                                                  plugin["version"],
                                                  plugin["repository"]);
            }
            return;
        }

        // List available updates **not ready
        private void HandleConsoleListUpdates(string module, string[] cmd)
        {
            PluginManager.ListUpdates();
            return;
        }

        // Update plugin **not ready
        private void HandleConsoleUpdatePlugin(string module, string[] cmd)
        {
            MainConsole.Instance.Output(PluginManager.Update());
            return;
        }

        // Register repository
        private void HandleConsoleAddRepo(string module, string[] cmd)
        {
            if ( cmd.Length == 3)
            {
                PluginManager.AddRepository(cmd[2]);
            }
            return;
        }

        // Get repository status **not working
        private void HandleConsoleGetRepo(string module, string[] cmd)
        {
            PluginManager.GetRepository();
            return;
        }

        // Remove registered repository
        private void HandleConsoleRemoveRepo(string module, string[] cmd)
        {
            if (cmd.Length == 3)
                PluginManager.RemoveRepository(cmd);
            return;
        }

        // Enable repository
        private void HandleConsoleEnableRepo(string module, string[] cmd)
        {
            PluginManager.EnableRepository(cmd);
            return;
        }

        // Disable repository
        private void HandleConsoleDisableRepo(string module, string[] cmd)
        {
            PluginManager.DisableRepository(cmd);
            return;
        }

        // List repositories
        private void HandleConsoleListRepos(string module, string[] cmd)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            PluginManager.ListRepositories(out result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                Dictionary<string, object> repo = (Dictionary<string, object>)result[k];
                bool enabled = (bool)repo["enabled"];
                MainConsole.Instance.Output("{0}) {1} {2}",
                                                  null,
                                                  k,
                                                  enabled == true ? "[ ]" : "[X]",
                                                  repo["name"], repo["url"]);
            }

            return;
        }

        // Show description information
        private void HandleConsoleShowAddinInfo(string module, string[] cmd)
        {
            if (cmd.Length >= 3)
            {

                Dictionary<string, object> result = new Dictionary<string, object>();

                int ndx = Convert.ToInt16(cmd[2]);
                PluginManager.AddinInfo(ndx, out result);

                MainConsole.Instance.Output("Name: {0}\nURL: {1}\nFile: {2}\nAuthor: {3}\nCategory: {4}\nDesc: {5}",
                                                  null,
                                                  result["name"],
                                                  result["url"],
                                                  result["file_name"],
                                                  result["author"],
                                                  result["category"],
                                                  result["description"]);

                return;
            }
        }

        // Disable plugin
        private void HandleConsoleDisablePlugin(string module, string[] cmd)
        {
            PluginManager.DisablePlugin(cmd);
            return;
        }

        // Enable plugin
        private void HandleConsoleEnablePlugin(string module, string[] cmd)
        {
            PluginManager.EnablePlugin(cmd);
            return;
        }
        #endregion
    }
}
