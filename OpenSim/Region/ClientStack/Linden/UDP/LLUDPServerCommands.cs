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
using System.Text;
using NDesk.Options;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLUDPServerCommands
    {
        private ICommandConsole m_console;
        private LLUDPServer m_udpServer;

        public LLUDPServerCommands(ICommandConsole console, LLUDPServer udpServer)
        {
            m_console = console;
            m_udpServer = udpServer;
        }

        public void Register()
        {
/*
            m_console.Commands.AddCommand(
                "Comms", false, "show server throttles",
                "show server throttles",
                "Show information about server throttles",
                HandleShowServerThrottlesCommand);

            m_console.Commands.AddCommand(
                "Debug", false, "debug lludp packet",
                "debug lludp packet [--default | --all] <level> [<avatar-first-name> <avatar-last-name>]",
                "Turn on packet debugging.  This logs information when the client stack hands a processed packet off to downstream code or when upstream code first requests that a certain packet be sent.",
                "If level >  255 then all incoming and outgoing packets are logged.\n"
                + "If level <= 255 then incoming AgentUpdate and outgoing SimStats and SimulatorViewerTimeMessage packets are not logged.\n"
                + "If level <= 200 then incoming RequestImage and outgoing ImagePacket, ImageData, LayerData and CoarseLocationUpdate packets are not logged.\n"
                + "If level <= 100 then incoming ViewerEffect and AgentAnimation and outgoing ViewerEffect and AvatarAnimation packets are not logged.\n"
                + "If level <=  50 then outgoing ImprovedTerseObjectUpdate packets are not logged.\n"
                + "If level <= 0 then no packets are logged.\n"
                + "If --default is specified then the level becomes the default logging level for all subsequent agents.\n"
                + "If --all is specified then the level becomes the default logging level for all current and subsequent agents.\n"
                + "In these cases, you cannot also specify an avatar name.\n"
                + "If an avatar name is given then only packets from that avatar are logged.",
                HandlePacketCommand);

            m_console.Commands.AddCommand(
                "Debug", false, "debug lludp data out",
                "debug lludp data out <level> <avatar-first-name> <avatar-last-name>\"",
                "Turn on debugging for final outgoing data to the given user's client.",
                "This operates at a much lower level than the packet command and prints out available details when the data is actually sent.\n"
                + "If level >  0 then information about all outgoing UDP data for this avatar is logged.\n"
                + "If level <= 0 then no information about outgoing UDP data for this avatar is logged.",
                HandleDataCommand);

            m_console.Commands.AddCommand(
                "Debug", false, "debug lludp drop",
                "debug lludp drop <in|out> <add|remove> <packet-name>",
                "Drop all in or outbound packets that match the given name",
                "For test purposes.",
                HandleDropCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp start",
                "debug lludp start <in|out|all>",
                "Control LLUDP packet processing.",
                "No effect if packet processing has already started.\n"
                + "in  - start inbound processing.\n"
                + "out - start outbound processing.\n"
                + "all - start in and outbound processing.\n",
                HandleStartCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp stop",
                "debug lludp stop <in|out|all>",
                "Stop LLUDP packet processing.",
                "No effect if packet processing has already stopped.\n"
                + "in  - stop inbound processing.\n"
                + "out - stop outbound processing.\n"
                + "all - stop in and outbound processing.\n",
                HandleStopCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp pool",
                "debug lludp pool <on|off>",
                "Turn object pooling within the lludp component on or off.",
                HandlePoolCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp status",
                "debug lludp status",
                "Return status of LLUDP packet processing.",
                HandleStatusCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp throttles log",
                "debug lludp throttles log <level> [<avatar-first-name> <avatar-last-name>]",
                "Change debug logging level for throttles.",
                "If level >= 0 then throttle debug logging is performed.\n"
                + "If level <= 0 then no throttle debug logging is performed.",
                HandleThrottleCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp throttles get",
                "debug lludp throttles get [<avatar-first-name> <avatar-last-name>]",
                "Return debug settings for throttles.",
                      "adaptive - true/false, controls adaptive throttle setting.\n"
                    + "request  - request drip rate in kbps.\n"
                    + "max      - the max kbps throttle allowed for the specified existing clients.  Use 'debug lludp get new-client-throttle-max' to see the setting for new clients.\n",
                HandleThrottleGetCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp throttles set",
                "debug lludp throttles set <param> <value> [<avatar-first-name> <avatar-last-name>]",
                "Set a throttle parameter for the given client.",
                      "adaptive - true/false, controls adaptive throttle setting.\n"
                    + "current  - current drip rate in kbps.\n"
                    + "request  - requested drip rate in kbps.\n"
                    + "max      - the max kbps throttle allowed for the specified existing clients.  Use 'debug lludp set new-client-throttle-max' to change the settings for new clients.\n",
                HandleThrottleSetCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp get",
                "debug lludp get",
                "Get debug parameters for the server.",
                      "max-scene-throttle      - the current max cumulative kbps provided for this scene to clients.\n"
                    + "max-new-client-throttle - the max kbps throttle allowed to new clients.   Use 'debug lludp throttles get max' to see the settings for existing clients.",
                HandleGetCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp set",
                "debug lludp set <param> <value>",
                "Set a parameter for the server.",
                      "max-scene-throttle      - the current max cumulative kbps provided for this scene to clients.\n"
                    + "max-new-client-throttle - the max kbps throttle allowed to each new client.  Use 'debug lludp throttles set max' to set for existing clients.",
                HandleSetCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp toggle agentupdate",
                "debug lludp toggle agentupdate",
                "Toggle whether agentupdate packets are processed or simply discarded.",
                HandleAgentUpdateCommand);

            MainConsole.Instance.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp oqre",
                "debug lludp oqre <start|stop|status>",
                "Start, stop or get status of OutgoingQueueRefillEngine.",
                "If stopped then refill requests are processed directly via the threadpool.",
                HandleOqreCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp client get",
                "debug lludp client get [<avatar-first-name> <avatar-last-name>]",
                "Get debug parameters for the client.  If no name is given then all client information is returned.",
                "process-unacked-sends - Do we take action if a sent reliable packet has not been acked.",
                HandleClientGetCommand);

            m_console.Commands.AddCommand(
                "Debug",
                false,
                "debug lludp client set",
                "debug lludp client set <param> <value> [<avatar-first-name> <avatar-last-name>]",
                "Set a debug parameter for a particular client.  If no name is given then the value is set on all clients.",
                "process-unacked-sends - Do we take action if a sent reliable packet has not been acked.",
                HandleClientSetCommand);
*/
        }

        private void HandleShowServerThrottlesCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            m_console.OutputFormat("Throttles for {0}", m_udpServer.Scene.Name);
            ConsoleDisplayList cdl = new ConsoleDisplayList();
            cdl.AddRow("Adaptive throttles", m_udpServer.ThrottleRates.AdaptiveThrottlesEnabled);

            long maxSceneDripRate = (long)m_udpServer.Throttle.MaxDripRate;
            cdl.AddRow(
                "Max scene throttle",
                maxSceneDripRate != 0 ? string.Format("{0} kbps", maxSceneDripRate * 8 / 1000) : "unset");

            int maxClientDripRate = m_udpServer.ThrottleRates.Total;
            cdl.AddRow(
                "Max new client throttle",
                maxClientDripRate != 0 ? string.Format("{0} kbps", maxClientDripRate * 8 / 1000) : "unset");

            m_console.Output(cdl.ToString());

            m_console.OutputFormat("{0}\n", GetServerThrottlesReport(m_udpServer));
        }

        private string GetServerThrottlesReport(LLUDPServer udpServer)
        {
            StringBuilder report = new StringBuilder();

            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}\n",
                "Total",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset");

            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}\n",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s");

            ThrottleRates throttleRates = udpServer.ThrottleRates;
            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}",
                (throttleRates.Total * 8) / 1000,
                (throttleRates.Resend * 8) / 1000,
                (throttleRates.Land * 8) / 1000,
                (throttleRates.Wind * 8) / 1000,
                (throttleRates.Cloud * 8) / 1000,
                (throttleRates.Task * 8) / 1000,
                (throttleRates.Texture  * 8) / 1000,
                (throttleRates.Asset  * 8) / 1000);

            return report.ToString();
        }

        protected string GetColumnEntry(string entry, int maxLength, int columnPadding)
        {
            return string.Format(
                "{0,-" + maxLength +  "}{1,-" + columnPadding + "}",
                entry.Length > maxLength ? entry.Substring(0, maxLength) : entry,
                "");
        }

        private void HandleDataCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 7)
            {
                MainConsole.Instance.OutputFormat("Usage: debug lludp data out <true|false> <avatar-first-name> <avatar-last-name>");
                return;
            }

            int level;
            if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[4], out level))
                return;

            string firstName = args[5];
            string lastName = args[6];

            m_udpServer.Scene.ForEachScenePresence(sp =>
                                       {
                if (sp.Firstname == firstName && sp.Lastname == lastName)
                {
                    MainConsole.Instance.OutputFormat(
                        "Data debug for {0} ({1}) set to {2} in {3}",
                        sp.Name, sp.IsChildAgent ? "child" : "root", level, m_udpServer.Scene.Name);

                    ((LLClientView)sp.ControllingClient).UDPClient.DebugDataOutLevel = level;
                }
            });
        }

        private void HandleThrottleCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            bool all = args.Length == 5;
            bool one = args.Length == 7;

            if (!all && !one)
            {
                MainConsole.Instance.OutputFormat(
                    "Usage: debug lludp throttles log <level> [<avatar-first-name> <avatar-last-name>]");
                return;
            }

            int level;
            if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[4], out level))
                return;

            string firstName = null;
            string lastName = null;

            if (one)
            {
                firstName = args[5];
                lastName = args[6];
            }

            m_udpServer.Scene.ForEachScenePresence(sp =>
            {
                if (all || (sp.Firstname == firstName && sp.Lastname == lastName))
                {
                    MainConsole.Instance.OutputFormat(
                        "Throttle log level for {0} ({1}) set to {2} in {3}",
                        sp.Name, sp.IsChildAgent ? "child" : "root", level, m_udpServer.Scene.Name);

                    ((LLClientView)sp.ControllingClient).UDPClient.ThrottleDebugLevel = level;
                }
            });
        }

        private void HandleThrottleSetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            bool all = args.Length == 6;
            bool one = args.Length == 8;

            if (!all && !one)
            {
                MainConsole.Instance.OutputFormat(
                    "Usage: debug lludp throttles set <param> <value> [<avatar-first-name> <avatar-last-name>]");
                return;
            }

            string param = args[4];
            string rawValue = args[5];

            string firstName = null;
            string lastName = null;

            if (one)
            {
                firstName = args[6];
                lastName = args[7];
            }

            if (param == "adaptive")
            {
                bool newValue;
                if (!ConsoleUtil.TryParseConsoleBool(MainConsole.Instance, rawValue, out newValue))
                    return;

                m_udpServer.Scene.ForEachScenePresence(sp =>
                {
                    if (all || (sp.Firstname == firstName && sp.Lastname == lastName))
                    {
                        MainConsole.Instance.OutputFormat(
                            "Setting param {0} to {1} for {2} ({3}) in {4}",
                            param, newValue, sp.Name, sp.IsChildAgent ? "child" : "root", m_udpServer.Scene.Name);

                        LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
                        udpClient.FlowThrottle.AdaptiveEnabled = newValue;
                        //                        udpClient.FlowThrottle.MaxDripRate = 0;
                        //                        udpClient.FlowThrottle.AdjustedDripRate = 0;
                    }
                });
            }
            else if (param == "request")
            {
                int newValue;
                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                int newCurrentThrottleKbps = newValue * 1000 / 8;

                m_udpServer.Scene.ForEachScenePresence(sp =>
                                                       {
                    if (all || (sp.Firstname == firstName && sp.Lastname == lastName))
                    {
                        MainConsole.Instance.OutputFormat(
                            "Setting param {0} to {1} for {2} ({3}) in {4}",
                            param, newValue, sp.Name, sp.IsChildAgent ? "child" : "root", m_udpServer.Scene.Name);

                        LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
                        udpClient.FlowThrottle.RequestedDripRate = newCurrentThrottleKbps;
                    }
                });
            }
            else if (param == "max")
            {
                int newValue;
                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                int newThrottleMaxKbps = newValue * 1000 / 8;

                m_udpServer.Scene.ForEachScenePresence(sp =>
                {
                    if (all || (sp.Firstname == firstName && sp.Lastname == lastName))
                    {
                        MainConsole.Instance.OutputFormat(
                            "Setting param {0} to {1} for {2} ({3}) in {4}",
                            param, newValue, sp.Name, sp.IsChildAgent ? "child" : "root", m_udpServer.Scene.Name);

                        LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
                        udpClient.FlowThrottle.MaxDripRate = newThrottleMaxKbps;
                    }
                });
            }
        }

        private void HandleThrottleGetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            bool all = args.Length == 4;
            bool one = args.Length == 6;

            if (!all && !one)
            {
                MainConsole.Instance.OutputFormat(
                    "Usage: debug lludp throttles get [<avatar-first-name> <avatar-last-name>]");
                return;
            }

            string firstName = null;
            string lastName = null;

            if (one)
            {
                firstName = args[4];
                lastName = args[5];
            }

            m_udpServer.Scene.ForEachScenePresence(sp =>
             {
                if (all || (sp.Firstname == firstName && sp.Lastname == lastName))
                {
                    m_console.OutputFormat(
                        "Status for {0} ({1}) in {2}",
                        sp.Name, sp.IsChildAgent ? "child" : "root", m_udpServer.Scene.Name);

                    LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;

                    ConsoleDisplayList cdl = new ConsoleDisplayList();
                    cdl.AddRow("adaptive", udpClient.FlowThrottle.AdaptiveEnabled);
                    cdl.AddRow("current", string.Format("{0} kbps", udpClient.FlowThrottle.DripRate * 8 / 1000));
                    cdl.AddRow("request", string.Format("{0} kbps", udpClient.FlowThrottle.RequestedDripRate * 8 / 1000));
                    cdl.AddRow("max", string.Format("{0} kbps", udpClient.FlowThrottle.MaxDripRate * 8 / 1000));

                    m_console.Output(cdl.ToString());
                }
            });
        }

        private void HandleGetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            m_console.OutputFormat("Debug settings for {0}", m_udpServer.Scene.Name);
            ConsoleDisplayList cdl = new ConsoleDisplayList();

            long maxSceneDripRate = (long)m_udpServer.Throttle.MaxDripRate;
            cdl.AddRow(
                "max-scene-throttle",
                maxSceneDripRate != 0 ? string.Format("{0} kbps", maxSceneDripRate * 8 / 1000) : "unset");

            int maxClientDripRate = m_udpServer.ThrottleRates.Total;
            cdl.AddRow(
                "max-new-client-throttle",
                maxClientDripRate != 0 ? string.Format("{0} kbps", maxClientDripRate * 8 / 1000) : "unset");

            m_console.Output(cdl.ToString());
        }

        private void HandleSetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 5)
            {
                MainConsole.Instance.OutputFormat("Usage: debug lludp set <param> <value>");
                return;
            }

            string param = args[3];
            string rawValue = args[4];

            int newValue;

            if (param == "max-scene-throttle")
            {
                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                m_udpServer.Throttle.MaxDripRate = newValue * 1000 / 8;
            }
            else if (param == "max-new-client-throttle")
            {
                if (!ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                m_udpServer.ThrottleRates.Total = newValue * 1000 / 8;
            }
            else
            {
                return;
            }

            m_console.OutputFormat("{0} set to {1} in {2}", param, rawValue, m_udpServer.Scene.Name);
        }

/* not in use, nothing to set/get from lludp
        private void HandleClientGetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 4 && args.Length != 6)
            {
                MainConsole.Instance.OutputFormat("Usage: debug lludp client get [<avatar-first-name> <avatar-last-name>]");
                return;
            }

            string name = null;

            if (args.Length == 6)
                name = string.Format("{0} {1}", args[4], args[5]);

            m_udpServer.Scene.ForEachScenePresence(
                sp =>
                {
                    if ((name == null || sp.Name == name) && sp.ControllingClient is LLClientView)
                    {
                        LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;

                        m_console.OutputFormat(
                            "Client debug parameters for {0} ({1}) in {2}",
                            sp.Name, sp.IsChildAgent ? "child" : "root", m_udpServer.Scene.Name);
                    }
                });
        }

        private void HandleClientSetCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 6 && args.Length != 8)
            {
                MainConsole.Instance.OutputFormat("Usage: debug lludp client set <param> <value> [<avatar-first-name> <avatar-last-name>]");
                return;
            }

            string param = args[4];
            string rawValue = args[5];

            string name = null;

            if (args.Length == 8)
                name = string.Format("{0} {1}", args[6], args[7]);
            // nothing here now
        }
*/
        private void HandlePacketCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            bool setAsDefaultLevel = false;
            bool setAll = false;
            OptionSet optionSet = new OptionSet()
                .Add("default", o => setAsDefaultLevel = (o != null))
                    .Add("all", o => setAll = (o != null));
            List<string> filteredArgs = optionSet.Parse(args);

            string name = null;

            if (filteredArgs.Count == 6)
            {
                if (!(setAsDefaultLevel || setAll))
                {
                    name = string.Format("{0} {1}", filteredArgs[4], filteredArgs[5]);
                }
                else
                {
                    MainConsole.Instance.OutputFormat("ERROR: Cannot specify a user name when setting default/all logging level");
                    return;
                }
            }

            if (filteredArgs.Count > 3)
            {
                int newDebug;
                if (int.TryParse(filteredArgs[3], out newDebug))
                {
                    if (setAsDefaultLevel || setAll)
                    {
                        m_udpServer.DefaultClientPacketDebugLevel = newDebug;

                        MainConsole.Instance.OutputFormat(
                            "Packet debug for {0} clients set to {1} in {2}",
                            (setAll ? "all" : "future"), m_udpServer.DefaultClientPacketDebugLevel, m_udpServer.Scene.Name);

                        if (setAll)
                        {
                            m_udpServer.Scene.ForEachScenePresence(sp =>
                                                       {
                                MainConsole.Instance.OutputFormat(
                                    "Packet debug for {0} ({1}) set to {2} in {3}",
                                    sp.Name, sp.IsChildAgent ? "child" : "root", newDebug, m_udpServer.Scene.Name);

                                sp.ControllingClient.DebugPacketLevel = newDebug;
                            });
                        }
                    }
                    else
                    {
                        m_udpServer.Scene.ForEachScenePresence(sp =>
                                                   {
                            if (name == null || sp.Name == name)
                            {
                                MainConsole.Instance.OutputFormat(
                                    "Packet debug for {0} ({1}) set to {2} in {3}",
                                    sp.Name, sp.IsChildAgent ? "child" : "root", newDebug, m_udpServer.Scene.Name);

                                sp.ControllingClient.DebugPacketLevel = newDebug;
                            }
                        });
                    }
                }
                else
                {
                    MainConsole.Instance.Output("Usage: debug lludp packet [--default | --all] 0..255 [<first-name> <last-name>]");
                }
            }
        }

        private void HandleDropCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 6)
            {
                MainConsole.Instance.Output("Usage: debug lludp drop <in|out> <add|remove> <packet-name>");
                return;
            }

            string direction = args[3];
            string subCommand = args[4];
            string packetName = args[5];

            if (subCommand == "add")
            {
                MainConsole.Instance.OutputFormat(
                    "Adding packet {0} to {1} drop list for all connections in {2}",
                    direction, packetName, m_udpServer.Scene.Name);

                m_udpServer.Scene.ForEachScenePresence(
                    sp =>
                    {
                    LLClientView llcv = (LLClientView)sp.ControllingClient;

                    if (direction == "in")
                        llcv.AddInPacketToDropSet(packetName);
                    else if (direction == "out")
                        llcv.AddOutPacketToDropSet(packetName);
                }
                );
            }
            else if (subCommand == "remove")
            {
                MainConsole.Instance.OutputFormat(
                    "Removing packet {0} from {1} drop list for all connections in {2}",
                    direction, packetName, m_udpServer.Scene.Name);

                m_udpServer.Scene.ForEachScenePresence(
                    sp =>
                    {
                    LLClientView llcv = (LLClientView)sp.ControllingClient;

                    if (direction == "in")
                        llcv.RemoveInPacketFromDropSet(packetName);
                    else if (direction == "out")
                        llcv.RemoveOutPacketFromDropSet(packetName);
                }
                );
            }
        }

        private void HandleStartCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 4)
            {
                MainConsole.Instance.Output("Usage: debug lludp start <in|out|all>");
                return;
            }

            string subCommand = args[3];

            if (subCommand == "in" || subCommand == "all")
                m_udpServer.StartInbound();

            if (subCommand == "out" || subCommand == "all")
                m_udpServer.StartOutbound();
        }

        private void HandleStopCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 4)
            {
                MainConsole.Instance.Output("Usage: debug lludp stop <in|out|all>");
                return;
            }

            string subCommand = args[3];

            if (subCommand == "in" || subCommand == "all")
                m_udpServer.StopInbound();

            if (subCommand == "out" || subCommand == "all")
                m_udpServer.StopOutbound();
        }

        private void HandlePoolCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 4)
            {
                MainConsole.Instance.Output("Usage: debug lludp pool <on|off>");
                return;
            }

            string enabled = args[3];

            if (enabled == "on")
            {
                if (m_udpServer.EnablePools())
                {
                    m_udpServer.EnablePoolStats();
                    MainConsole.Instance.OutputFormat("Packet pools enabled on {0}", m_udpServer.Scene.Name);
                }
            }
            else if (enabled == "off")
            {
                if (m_udpServer.DisablePools())
                {
                    m_udpServer.DisablePoolStats();
                    MainConsole.Instance.OutputFormat("Packet pools disabled on {0}", m_udpServer.Scene.Name);
                }
            }
            else
            {
                MainConsole.Instance.Output("Usage: debug lludp pool <on|off>");
            }
        }

        private void HandleAgentUpdateCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            m_udpServer.DiscardInboundAgentUpdates = !m_udpServer.DiscardInboundAgentUpdates;

            MainConsole.Instance.OutputFormat(
                "Discard AgentUpdates now {0} for {1}", m_udpServer.DiscardInboundAgentUpdates, m_udpServer.Scene.Name);
        }

        private void HandleStatusCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            MainConsole.Instance.OutputFormat(
                "IN  LLUDP packet processing for {0} is {1}", m_udpServer.Scene.Name, m_udpServer.IsRunningInbound ? "enabled" : "disabled");

            MainConsole.Instance.OutputFormat(
                "OUT LLUDP packet processing for {0} is {1}", m_udpServer.Scene.Name, m_udpServer.IsRunningOutbound ? "enabled" : "disabled");

            MainConsole.Instance.OutputFormat("LLUDP pools in {0} are {1}", m_udpServer.Scene.Name, m_udpServer.UsePools ? "on" : "off");

            MainConsole.Instance.OutputFormat(
                "Packet debug level for new clients is {0}", m_udpServer.DefaultClientPacketDebugLevel);
        }

        private void HandleOqreCommand(string module, string[] args)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
                return;

            if (args.Length != 4)
            {
                MainConsole.Instance.Output("Usage: debug lludp oqre <stop|start|status>");
                return;
            }

            string subCommand = args[3];

            if (subCommand == "stop")
            {
                m_udpServer.OqrEngine.Stop();
                MainConsole.Instance.OutputFormat("Stopped OQRE for {0}", m_udpServer.Scene.Name);
            }
            else if (subCommand == "start")
            {
                m_udpServer.OqrEngine.Start();
                MainConsole.Instance.OutputFormat("Started OQRE for {0}", m_udpServer.Scene.Name);
            }
            else if (subCommand == "status")
            {
                MainConsole.Instance.OutputFormat("OQRE in {0}", m_udpServer.Scene.Name);
                MainConsole.Instance.OutputFormat("Running: {0}", m_udpServer.OqrEngine.IsRunning);
                MainConsole.Instance.OutputFormat(
                    "Requests waiting: {0}",
                    m_udpServer.OqrEngine.IsRunning ? m_udpServer.OqrEngine.JobsWaiting.ToString() : "n/a");
            }
            else
            {
                MainConsole.Instance.OutputFormat("Unrecognized OQRE subcommand {0}", subCommand);
            }
        }
    }
}