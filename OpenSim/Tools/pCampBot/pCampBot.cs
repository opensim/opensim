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
using System.IO;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Config;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace pCampBot
{
    /// <summary>
    /// Event Types from the BOT.  Add new events here
    /// </summary>
    public enum EventType:int
    {
        NONE = 0,
        CONNECTED = 1,
        DISCONNECTED = 2
    }

    public class pCampBot
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string ConfigFileName = "pCampBot.ini";

        [STAThread]
        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            IConfig commandLineConfig = ParseConfig(args);
            if (commandLineConfig.Get("help") != null || commandLineConfig.Get("loginuri") == null)
            {
                Help();
            }
            else if (
                commandLineConfig.Get("firstname") == null
                    ||  commandLineConfig.Get("lastname") == null
                    || commandLineConfig.Get("password") == null)
            {
                Console.WriteLine("ERROR: You must supply a firstname, lastname and password for the bots.");
            }
            else
            {
                BotManager bm = new BotManager();

                string iniFilePath = Path.GetFullPath(Path.Combine(Util.configDir(), ConfigFileName));

                if (File.Exists(iniFilePath))
                {
                    m_log.InfoFormat("[PCAMPBOT]: Reading configuration settings from {0}", iniFilePath);

                    IConfigSource configSource = new IniConfigSource(iniFilePath);

                    IConfig botManagerConfig = configSource.Configs["BotManager"];

                    if (botManagerConfig != null)
                    {
                        bm.LoginDelay = botManagerConfig.GetInt("LoginDelay", bm.LoginDelay);
                    }

                    IConfig botConfig = configSource.Configs["Bot"];

                    if (botConfig != null)
                    {
                        bm.InitBotSendAgentUpdates
                            = botConfig.GetBoolean("SendAgentUpdates", bm.InitBotSendAgentUpdates);
                        bm.InitBotRequestObjectTextures
                            = botConfig.GetBoolean("RequestObjectTextures", bm.InitBotRequestObjectTextures);
                    }
                }

                int botcount = commandLineConfig.GetInt("botcount", 1);
                bool startConnected = commandLineConfig.Get("connect") != null;

                bm.CreateBots(botcount, commandLineConfig);

                if (startConnected)
                    bm.ConnectBots(botcount);

                while (true)
                {
                    try
                    {
                        MainConsole.Instance.Prompt();
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("Command error: {0}", e);
                    }
                }
            }
        }

        private static IConfig ParseConfig(String[] args)
        {
            //Set up our nifty config..  thanks to nini
            ArgvConfigSource cs = new ArgvConfigSource(args);

            cs.AddSwitch("Startup", "connect", "c");
            cs.AddSwitch("Startup", "botcount", "n");
            cs.AddSwitch("Startup", "from", "f");
            cs.AddSwitch("Startup", "loginuri", "l");
            cs.AddSwitch("Startup", "start", "s");
            cs.AddSwitch("Startup", "firstname");
            cs.AddSwitch("Startup", "lastname");
            cs.AddSwitch("Startup", "password");
            cs.AddSwitch("Startup", "behaviours", "b");
            cs.AddSwitch("Startup", "help", "h");
            cs.AddSwitch("Startup", "wear");

            IConfig ol = cs.Configs["Startup"];
            return ol;
        }

        private static void Help()
        {
            // Added the wear command. This allows the bot to wear real clothes instead of default locked ones.
            // You can either say no, to not load anything, yes, to load one of the default wearables, a folder
            // name, to load an specific folder, or save, to save an avatar with some already existing wearables
            // worn to the folder MyAppearance/FirstName_LastName, and the load it.

            Console.WriteLine(
                "Usage: pCampBot -loginuri <loginuri> -firstname <first-name> -lastname <last-name> -password <password> [OPTIONS]\n"
                    + "Spawns a set of bots to test an OpenSim region\n\n"
                    + "  -l, -loginuri      loginuri for grid/standalone (required)\n"
                    + "  -s, -start         start location for bots (default: last) (optional).  Can be \"last\", \"home\" or a specific location with or without co-ords (e.g. \"region1\" or \"region2/50/30/90\"\n"
                    + "  -firstname         first name for the bots (required)\n"
                    + "  -lastname          lastname for the bots (required).  Each lastname will have _<bot-number> appended, e.g. Ima Bot_0\n"
                    + "  -password          password for the bots (required)\n"
                    + "  -n, -botcount      number of bots to start (default: 1) (optional)\n"
                    + "  -f, -from          starting number for login bot names, e.g. 25 will login Ima Bot_25, Ima Bot_26, etc. (default: 0) (optional)\n"
                    + "  -c, -connect       connect all bots at startup (optional)\n"
                    + "  -b, behaviours     behaviours for bots.  Comma separated, e.g. p,g (default: p) (optional)\n"
                    + "    current options are:\n"
                    + "       p (physics  - bots constantly move and jump around)\n"
                    + "       g (grab     - bots randomly click prims whether set clickable or not)\n"
                    + "       n (none     - bots do nothing)\n"
                    + "       t (teleport - bots regularly teleport between regions on the grid)\n"
//                "       c (cross)\n" +
                    + "  -wear              folder from which to load appearance data, \"no\" if there is no such folder (default: no) (optional)\n"
                    + "  -h, -help          show this message.\n");
        }
    }
}
