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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Static class used to register/deregister checks on runtime conditions.
    /// </summary>
    public static class ChecksManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Subcommand used to list other stats.
        public const string ListSubCommand = "list";

        // All subcommands
        public static HashSet<string> SubCommands = new HashSet<string> { ListSubCommand };

        /// <summary>
        /// Checks categorized by category/container/shortname
        /// </summary>
        /// <remarks>
        /// Do not add or remove directly from this dictionary.
        /// </remarks>
        public static SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, Check>>> RegisteredChecks
            = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, Check>>>();

        public static void RegisterConsoleCommands(ICommandConsole console)
        {
            console.Commands.AddCommand(
                "General",
                false,
                "show checks",
                "show checks",
                "Show checks configured for this server",
                "If no argument is specified then info on all checks will be shown.\n"
                    + "'list' argument will show check categories.\n"
                    + "THIS FACILITY IS EXPERIMENTAL",
                HandleShowchecksCommand);
        }

        public static void HandleShowchecksCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length > 2)
            {
                foreach (string name in cmd.Skip(2))
                {
                    string[] components = name.Split('.');

                    string categoryName = components[0];
//                    string containerName = components.Length > 1 ? components[1] : null;

                    if (categoryName == ListSubCommand)
                    {
                        con.Output("check categories available are:");

                        foreach (string category in RegisteredChecks.Keys)
                            con.Output("  {0}", category);
                    }
//                    else
//                    {
//                        SortedDictionary<string, SortedDictionary<string, Check>> category;
//                        if (!Registeredchecks.TryGetValue(categoryName, out category))
//                        {
//                            con.OutputFormat("No such category as {0}", categoryName);
//                        }
//                        else
//                        {
//                            if (String.IsNullOrEmpty(containerName))
//                            {
//                                OutputConfiguredToConsole(con, category);
//                            }
//                            else
//                            {
//                                SortedDictionary<string, Check> container;
//                                if (category.TryGetValue(containerName, out container))
//                                {
//                                    OutputContainerChecksToConsole(con, container);
//                                }
//                                else
//                                {
//                                    con.OutputFormat("No such container {0} in category {1}", containerName, categoryName);
//                                }
//                            }
//                        }
//                    }
                }
            }
            else
            {
                OutputAllChecksToConsole(con);
            }
        }

        /// <summary>
        /// Registers a statistic.
        /// </summary>
        /// <param name='stat'></param>
        /// <returns></returns>
        public static bool RegisterCheck(Check check)
        {
            SortedDictionary<string, SortedDictionary<string, Check>> category = null;
            SortedDictionary<string, Check> container = null;

            lock (RegisteredChecks)
            {
                // Check name is not unique across category/container/shortname key.
                // XXX: For now just return false.  This is to avoid problems in regression tests where all tests
                // in a class are run in the same instance of the VM.
                if (TryGetCheckParents(check, out category, out container))
                    return false;

                // We take a copy-on-write approach here of replacing dictionaries when keys are added or removed.
                // This means that we don't need to lock or copy them on iteration, which will be a much more
                // common operation after startup.
                if (container == null)
                    container = new SortedDictionary<string, Check>();

                if (category == null)
                    category = new SortedDictionary<string, SortedDictionary<string, Check>>();

                container[check.ShortName] = check;
                category[check.Container] = container;
                RegisteredChecks[check.Category] = category;
            }

            return true;
        }

        /// <summary>
        /// Deregister an check
        /// </summary>>
        /// <param name='stat'></param>
        /// <returns></returns>
        public static bool DeregisterCheck(Check check)
        {
            SortedDictionary<string, SortedDictionary<string, Check>> category = null;
            SortedDictionary<string, Check> container = null;

            lock (RegisteredChecks)
            {
                if (!TryGetCheckParents(check, out category, out container))
                    return false;

                if(container != null)
                {
                    container.Remove(check.ShortName);
                    if(category != null && container.Count == 0)
                    {
                        category.Remove(check.Container);
                        if(category.Count == 0)
                            RegisteredChecks.Remove(check.Category);
                    }
                }
                return true;
            }
        }

        public static bool TryGetCheckParents(
            Check check,
            out SortedDictionary<string, SortedDictionary<string, Check>> category,
            out SortedDictionary<string, Check> container)
        {
            category = null;
            container = null;

            lock (RegisteredChecks)
            {
                if (RegisteredChecks.TryGetValue(check.Category, out category))
                {
                    if (category.TryGetValue(check.Container, out container))
                    {
                        if (container.ContainsKey(check.ShortName))
                            return true;
                    }
                }
            }

            return false;
        }

        public static void CheckChecks()
        {
            lock (RegisteredChecks)
            {
                foreach (SortedDictionary<string, SortedDictionary<string, Check>> category in RegisteredChecks.Values)
                {
                    foreach (SortedDictionary<string, Check> container in category.Values)
                    {
                        foreach (Check check in container.Values)
                        {
                            if (!check.CheckIt())
                                m_log.WarnFormat(
                                    "[CHECKS MANAGER]: Check {0}.{1}.{2} failed with message {3}", check.Category, check.Container, check.ShortName, check.LastFailureMessage);
                        }
                    }
                }
            }
        }

        private static void OutputAllChecksToConsole(ICommandConsole con)
        {
            foreach (var category in RegisteredChecks.Values)
            {
                OutputCategoryChecksToConsole(con, category);
            }
        }

        private static void OutputCategoryChecksToConsole(
            ICommandConsole con, SortedDictionary<string, SortedDictionary<string, Check>> category)
        {
            foreach (var container in category.Values)
            {
                OutputContainerChecksToConsole(con, container);
            }
        }

        private static void OutputContainerChecksToConsole(ICommandConsole con, SortedDictionary<string, Check> container)
        {
            foreach (Check check in container.Values)
            {
                con.Output(check.ToConsoleString());
            }
        }
    }
}