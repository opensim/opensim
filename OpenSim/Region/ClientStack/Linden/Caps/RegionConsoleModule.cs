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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Capabilities.Handlers;

namespace OpenSim.Region.ClientStack.Linden
{

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionConsoleModule")]
    public class RegionConsoleModule : INonSharedRegionModule, IRegionConsole
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IEventQueue m_eventQueue;
        private Commands m_commands = new Commands();
        public ICommands Commands { get { return m_commands; } }

        public event ConsoleMessage OnConsoleMessage;

        public void Initialise(IConfigSource source)
        {
            m_commands.AddCommand( "Help", false, "help", "help [<item>]", "Display help on a particular command or on a list of commands in a category", Help);
        }

        public void AddRegion(Scene s)
        {
            m_scene = s;
            m_scene.RegisterModuleInterface<IRegionConsole>(this);
        }

        public void RemoveRegion(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene s)
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
            m_eventQueue = m_scene.RequestModuleInterface<IEventQueue>();
        }

        public void PostInitialise()
        {
        }

        public void Close() { }

        public string Name { get { return "RegionConsoleModule"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            if (!m_scene.RegionInfo.EstateSettings.IsEstateManagerOrOwner(agentID) && !m_scene.Permissions.IsGod(agentID))
                return;

            UUID capID = UUID.Random();

//            m_log.DebugFormat("[REGION CONSOLE]: /CAPS/{0} in region {1}", capID, m_scene.RegionInfo.RegionName);
            caps.RegisterHandler(
                    "SimConsoleAsync",
                    new ConsoleHandler("/CAPS/" + capID + "/", "SimConsoleAsync", agentID, this, m_scene));
        }

        public void SendConsoleOutput(UUID agentID, string message)
        {
            OSD osd = OSD.FromString(message);

            m_eventQueue.Enqueue(EventQueueHelper.BuildEvent("SimConsoleResponse", osd), agentID);

            ConsoleMessage handlerConsoleMessage = OnConsoleMessage;

            if (handlerConsoleMessage != null)
                handlerConsoleMessage( agentID, message);
        }

        public bool RunCommand(string command, UUID invokerID)
        {
            string[] parts = Parser.Parse(command);
            Array.Resize(ref parts, parts.Length + 1);
            parts[parts.Length - 1] = invokerID.ToString();

            if (m_commands.Resolve(parts).Length == 0)
                return false;

            return true;
        }

        private void Help(string module, string[] cmd)
        {
            UUID agentID = new UUID(cmd[cmd.Length - 1]);
            Array.Resize(ref cmd, cmd.Length - 1);

            List<string> help = Commands.GetHelp(cmd);

            string reply = String.Empty;

            foreach (string s in help)
            {
                reply += s + "\n";
            }

            SendConsoleOutput(agentID, reply);
        }

        public void AddCommand(string module, bool shared, string command, string help, string longhelp, CommandDelegate fn)
        {
            m_commands.AddCommand(module, shared, command, help, longhelp, fn);
        }
    }

    public class ConsoleHandler : BaseStreamHandler
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RegionConsoleModule m_consoleModule;
        private UUID m_agentID;
        private bool m_isGod;
        private Scene m_scene;
        private bool m_consoleIsOn = false;

        public ConsoleHandler(string path, string name, UUID agentID, RegionConsoleModule module, Scene scene)
                :base("POST", path, name, agentID.ToString())
        {
            m_agentID = agentID;
            m_consoleModule = module;
            m_scene = scene;

            m_isGod = m_scene.Permissions.IsGod(agentID);
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader reader = new StreamReader(request);
            string message = reader.ReadToEnd();

            OSD osd = OSDParser.DeserializeLLSDXml(message);

            string cmd = osd.AsString();
            if (cmd == "set console on")
            {
                if (m_isGod)
                {
                    MainConsole.Instance.OnOutput += ConsoleSender;
                    m_consoleIsOn = true;
                    m_consoleModule.SendConsoleOutput(m_agentID, "Console is now on");
                }
                return new byte[0];
            }
            else if (cmd == "set console off")
            {
                MainConsole.Instance.OnOutput -= ConsoleSender;
                m_consoleIsOn = false;
                m_consoleModule.SendConsoleOutput(m_agentID, "Console is now off");
                return new byte[0];
            }

            if (m_consoleIsOn == false && m_consoleModule.RunCommand(osd.AsString().Trim(), m_agentID))
                return new byte[0];

            if (m_isGod && m_consoleIsOn)
            {
                MainConsole.Instance.RunCommand(osd.AsString().Trim());
            }
            else
            {
                m_consoleModule.SendConsoleOutput(m_agentID, "Unknown command");
            }

            return new byte[0];
        }

        private void ConsoleSender(string text)
        {
            m_consoleModule.SendConsoleOutput(m_agentID, text);
        }

        private void OnMakeChildAgent(ScenePresence presence)
        {
            if (presence.UUID == m_agentID)
            {
                MainConsole.Instance.OnOutput -= ConsoleSender;
                m_consoleIsOn = false;
            }
        }
    }
}
