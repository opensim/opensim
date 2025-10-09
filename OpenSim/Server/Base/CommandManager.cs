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



using System.Linq;
using System.Collections.Generic;
using Mono.Addins;
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
        public readonly AddinRegistry PluginRegistry;
        protected readonly PluginManager PluginManager;

        public CommandManager(AddinRegistry registry)
        {
            PluginRegistry = registry;
            PluginManager = new PluginManager(PluginRegistry);
            AddManagementCommands();
        }

        #region private static aliases 
        
        // alias for MainConsole.Instance.Commands.AddCommand
        private static void AddCommand(string module, bool shared, string command, string help, string longHelp,
            CommandDelegate fn)
        {
            MainConsole.Instance.Commands.AddCommand(module, shared, command, help, longHelp, fn);
        }

        // alias for MainConsole.Instance.Output
        private static void Output(string format, params object[] components)
        {
            MainConsole.Instance.Output(format, components);
        }
        
        #endregion

        private void AddManagementCommands()
        {
            
            // add plugin
            AddCommand("Plugin", true,
                 "plugin add", 
                 "plugin add \"plugin index\"",
                 "Install plugin from repository.",
                 HandleConsoleInstallPlugin);

            // remove plugin
            AddCommand("Plugin", true,
                 "plugin remove", 
                 "plugin remove \"plugin index\"",
                 "Remove plugin from repository",
                 HandleConsoleUnInstallPlugin);

            // list installed plugins
            AddCommand("Plugin", true,
                 "plugin list installed",
                 "plugin list installed",
                 "List install plugins",
                 HandleConsoleListInstalledPlugin);

            // list plugins available from registered repositories
            AddCommand("Plugin", true,
                 "plugin list available",
                 "plugin list available",
                 "List available plugins",
                 HandleConsoleListAvailablePlugin);
            // List available updates
            AddCommand("Plugin", true,
                 "plugin updates", 
                 "plugin updates",
                 "List available updates",
                 HandleConsoleListUpdates);

            // Update plugin
            AddCommand("Plugin", true,
                 "plugin update", 
                 "plugin update \"plugin index\"",
                 "Update the plugin",
                 HandleConsoleUpdatePlugin);

            // Add repository
            AddCommand("Repository", true,
                 "repo add", 
                 "repo add \"url\"",
                 "Add repository",
                 HandleConsoleAddRepo);

            // Refresh repo
            AddCommand("Repository", true,
                "repo refresh", 
                "repo refresh \"url\"", 
                "Sync with a registered repository",
                HandleConsoleGetRepo);

            // Remove repository from registry
            AddCommand("Repository", true,
                "repo remove",
                "repo remove \"[url | index]\"",
                "Remove repository from registry",
                HandleConsoleRemoveRepo);

            // Enable repo
            AddCommand("Repository", true,
                "repo enable", "repo enable \"[url | index]\"",
                "Enable registered repository",
                HandleConsoleEnableRepo);

            // Disable repo
            AddCommand("Repository", true,
                "repo disable", "repo disable \"[url | index]\"",
                "Disable registered repository",
                HandleConsoleDisableRepo);

            // List registered repositories
            AddCommand("Repository", true,
                "repo list", "repo list",
                "List registered repositories",
                HandleConsoleListRepos);

            // *
            AddCommand("Plugin", true,
                "plugin info", 
                "plugin info \"plugin index\"",
                "Show detailed information for plugin", 
                HandleConsoleShowAddinInfo);

            // Plugin disable
            AddCommand("Plugin", true,
                "plugin disable", 
                "plugin disable \"plugin index\"",
                "Disable a plugin",
                HandleConsoleDisablePlugin);

            // Enable plugin
            AddCommand("Plugin", true, 
                "plugin enable", 
                "plugin enable \"plugin index\"",
                "Enable the selected plugin plugin",
                HandleConsoleEnablePlugin
            );
        }

        #region console handlers
        // Handle our console commands
        //
        // Install plugin from a registered repository
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
            if (cmd.Length != 3) return;
            
            if (!int.TryParse(cmd[2], out var ndx))
            {
                Output("Invalid plugin index: {0}", cmd[2]);
                return;
            }

            if (!PluginManager.InstallPlugin(ndx, out var result)) return;
            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                var plugin = (Dictionary<string, object>)result[k];
                var enabled = (bool)plugin["enabled"];
                Output("{0}) {1} {2} rev. {3}",
                    k,
                    enabled ? "[ ]" : "[X]",
                    plugin["name"], plugin["version"]);
            }
        }

        // Remove installed plugin
        private void HandleConsoleUnInstallPlugin(string module, string[] cmd)
        {
            if (cmd.Length != 3) return;
            if (int.TryParse(cmd[2], out int ndx))
                PluginManager.UnInstall(ndx);
            else
                Output("Invalid plugin index: {0}", cmd[2]);
        }

        // List installed plugins
        private void HandleConsoleListInstalledPlugin(string module, string[] cmd)
        {
            PluginManager.ListInstalledAddins(out var result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                var plugin = (Dictionary<string, object>)result[k];
                var enabled = (bool)plugin["enabled"];
                Output("{0}) {1} {2} rev. {3}",
                                                  k,
                                                  enabled ? "[ ]" : "[X]",
                                                  plugin["name"], plugin["version"]);
            }
        }

        // List available plugins on registered repositories
        private void HandleConsoleListAvailablePlugin(string module, string[] cmd)
        {
            PluginManager.ListAvailable(out var result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                // name, version, repository
                var plugin = (Dictionary<string, object>)result[k];
                Output("{0}) {1} rev. {2} {3}",
                                                  k,
                                                  plugin["name"],
                                                  plugin["version"],
                                                  plugin["repository"]);
            }
        }

        // List available updates **not ready
        private void HandleConsoleListUpdates(string module, string[] cmd)
        {
            PluginManager.ListUpdates();
        }

        // Update plugin **not ready
        private void HandleConsoleUpdatePlugin(string module, string[] cmd)
        {
            Output(PluginManager.Update());
        }

        // Register repository
        private void HandleConsoleAddRepo(string module, string[] cmd)
        {
            if (cmd.Length == 3)
            {
                PluginManager.AddRepository(cmd[2]);
            }
        }

        // Get repository status **not working
        private void HandleConsoleGetRepo(string module, string[] cmd)
        {
            PluginManager.GetRepository();
        }

        // Remove a registered repository
        private void HandleConsoleRemoveRepo(string module, string[] cmd)
        {
            if (cmd.Length == 3)
                PluginManager.RemoveRepository(cmd);
        }

        // Enable repository
        private void HandleConsoleEnableRepo(string module, string[] cmd)
        {
            PluginManager.EnableRepository(cmd);
        }

        // Disable repository
        private void HandleConsoleDisableRepo(string module, string[] cmd)
        {
            PluginManager.DisableRepository(cmd);
        }

        // List repositories
        private void HandleConsoleListRepos(string module, string[] cmd)
        {
            PluginManager.ListRepositories(out var result);

            var list = result.Keys.ToList();
            list.Sort();
            foreach (var k in list)
            {
                var repo = (Dictionary<string, object>)result[k];
                var enabled = (bool)repo["enabled"];
                Output("{0}) {1} {2}",
                                                  k,
                                                  enabled ? "[ ]" : "[X]",
                                                  repo["name"], repo["url"]);
            }
        }

        // Show description information
        private void HandleConsoleShowAddinInfo(string module, string[] cmd)
        {
            if (cmd.Length < 3) return;
            
            if (!int.TryParse(cmd[2], out int ndx))
            {
                Output("Invalid plugin index: {0}", cmd[2]);
                return;
            }

            PluginManager.AddinInfo(ndx, out var result);

            Output("Name: {0}\nURL: {1}\nFile: {2}\nAuthor: {3}\nCategory: {4}\nDesc: {5}",
                result["name"],
                result["url"],
                result["file_name"],
                result["author"],
                result["category"],
                result["description"]);
        }

        // Disable plugin
        private void HandleConsoleDisablePlugin(string module, string[] cmd)
        {
            PluginManager.DisablePlugin(cmd);
        }

        // Enable plugin
        private void HandleConsoleEnablePlugin(string module, string[] cmd)
        {
            PluginManager.EnablePlugin(cmd);
        }
        #endregion
    }
}
