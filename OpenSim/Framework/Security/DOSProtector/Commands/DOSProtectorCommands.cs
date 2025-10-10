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
using System.Linq;
using System.Text;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Security.DOSProtector.Commands
{
    /// <summary>
    /// Console commands for managing DOS Protector plugins
    /// </summary>
    public static class DOSProtectorCommands
    {
        private static bool _registered = false;

        /// <summary>
        /// Registers DOS Protector console commands with the given command console
        /// </summary>
        public static void RegisterCommands(ICommandConsole console)
        {
            if (_registered)
                return;

            console.Commands.AddCommand(
                "DOSProtector",
                false,
                "dosprotector list",
                "dosprotector list",
                "List all discovered DOS protector implementations",
                HandleListCommand);

            console.Commands.AddCommand(
                "DOSProtector",
                false,
                "dosprotector refresh",
                "dosprotector refresh",
                "Refresh DOS protector plugin cache (re-scan all assemblies)",
                HandleRefreshCommand);

            console.Commands.AddCommand(
                "DOSProtector",
                false,
                "dosprotector reload-config",
                "dosprotector reload-config",
                "Reload DOS protector configuration from INI file",
                HandleReloadConfigCommand);

            _registered = true;
        }

        private static void HandleListCommand(string module, string[] cmdparams)
        {
            var protectors = DOSProtectorBuilder.GetDiscoveredProtectors();

            if (protectors.Count == 0)
            {
                MainConsole.Instance.Output("No DOS protector implementations discovered.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Discovered {protectors.Count} DOS Protector implementation(s):");
            sb.AppendLine();

            int index = 1;
            foreach (var kvp in protectors.OrderBy(p => p.Key))
            {
                sb.AppendLine($"  {index}. {kvp.Value}");
                sb.AppendLine($"     Options Type: {kvp.Key}");
                sb.AppendLine();
                index++;
            }

            MainConsole.Instance.Output(sb.ToString());
        }

        private static void HandleRefreshCommand(string module, string[] cmdparams)
        {
            MainConsole.Instance.Output("Refreshing DOS protector plugin cache...");

            try
            {
                DOSProtectorBuilder.RefreshCache();
                var protectors = DOSProtectorBuilder.GetDiscoveredProtectors();
                MainConsole.Instance.Output($"Cache refreshed successfully. Discovered {protectors.Count} implementation(s).");
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Output($"Error refreshing cache: {ex.Message}");
            }
        }

        private static void HandleReloadConfigCommand(string module, string[] cmdparams)
        {
            MainConsole.Instance.Output("Reloading DOS protector configuration...");

            try
            {
                DOSProtectorConfigLoader.Reset();
                DOSProtectorConfigLoader.LoadConfig();
                MainConsole.Instance.Output("Configuration reloaded successfully.");

                // Show discovered protectors after reload
                var protectors = DOSProtectorBuilder.GetDiscoveredProtectors();
                MainConsole.Instance.Output($"Discovered {protectors.Count} DOS protector implementation(s).");
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Output($"Error reloading configuration: {ex.Message}");
            }
        }
    }
}
