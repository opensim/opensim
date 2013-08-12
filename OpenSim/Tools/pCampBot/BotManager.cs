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
using System.Threading;
using OpenMetaverse;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// Thread/Bot manager for the application
    /// </summary>
    public class BotManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int DefaultLoginDelay = 5000;

        /// <summary>
        /// Delay between logins of multiple bots.
        /// </summary>
        /// <remarks>TODO: This value needs to be configurable by a command line argument.</remarks>
        public int LoginDelay { get; set; }

        /// <summary>
        /// Command console
        /// </summary>
        protected CommandConsole m_console;

        /// <summary>
        /// Created bots, whether active or inactive.
        /// </summary>
        protected List<Bot> m_lBot;

        /// <summary>
        /// Random number generator.
        /// </summary>
        public Random Rng { get; private set; }

        /// <summary>
        /// Overall configuration.
        /// </summary>
        public IConfig Config { get; private set; }

        /// <summary>
        /// Track the assets we have and have not received so we don't endlessly repeat requests.
        /// </summary>
        public Dictionary<UUID, bool> AssetsReceived { get; private set; }

        /// <summary>
        /// The regions that we know about.
        /// </summary>
        public Dictionary<ulong, GridRegion> RegionsKnown { get; private set; }

        /// <summary>
        /// Constructor Creates MainConsole.Instance to take commands and provide the place to write data
        /// </summary>
        public BotManager()
        {
            LoginDelay = DefaultLoginDelay;

            Rng = new Random(Environment.TickCount);
            AssetsReceived = new Dictionary<UUID, bool>();
            RegionsKnown = new Dictionary<ulong, GridRegion>();

            m_console = CreateConsole();
            MainConsole.Instance = m_console;

            // Make log4net see the console
            //
            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();
            OpenSimAppender consoleAppender = null;

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "Console")
                {
                    consoleAppender = (OpenSimAppender)appender;
                    consoleAppender.Console = m_console;
                    break;
                }
            }

            m_console.Commands.AddCommand("bot", false, "shutdown",
                    "shutdown",
                    "Shutdown bots and exit", HandleShutdown);

            m_console.Commands.AddCommand("bot", false, "quit",
                    "quit",
                    "Shutdown bots and exit",
                    HandleShutdown);

            m_console.Commands.AddCommand("bot", false, "show regions",
                    "show regions",
                    "Show regions known to bots",
                    HandleShowRegions);

            m_console.Commands.AddCommand("bot", false, "show bots",
                    "show bots",
                    "Shows the status of all bots",
                    HandleShowStatus);

//            m_console.Commands.AddCommand("bot", false, "add bots",
//                    "add bots <number>",
//                    "Add more bots", HandleAddBots);

            m_lBot = new List<Bot>();
        }

        /// <summary>
        /// Startup number of bots specified in the starting arguments
        /// </summary>
        /// <param name="botcount">How many bots to start up</param>
        /// <param name="cs">The configuration for the bots to use</param>
        public void dobotStartup(int botcount, IConfig cs)
        {
            Config = cs;

            string firstName = cs.GetString("firstname");
            string lastNameStem = cs.GetString("lastname");
            string password = cs.GetString("password");
            string loginUri = cs.GetString("loginuri");

            HashSet<string> behaviourSwitches = new HashSet<string>();
            Array.ForEach<string>(
                cs.GetString("behaviours", "p").Split(new char[] { ',' }), b => behaviourSwitches.Add(b));

            MainConsole.Instance.OutputFormat(
                "[BOT MANAGER]: Starting {0} bots connecting to {1}, named {2} {3}_<n>",
                botcount,
                loginUri,
                firstName,
                lastNameStem);

            MainConsole.Instance.OutputFormat("[BOT MANAGER]: Delay between logins is {0}ms", LoginDelay);

            for (int i = 0; i < botcount; i++)
            {
                string lastName = string.Format("{0}_{1}", lastNameStem, i);

                // We must give each bot its own list of instantiated behaviours since they store state.
                List<IBehaviour> behaviours = new List<IBehaviour>();
    
                // Hard-coded for now        
                if (behaviourSwitches.Contains("c"))
                    behaviours.Add(new CrossBehaviour());

                if (behaviourSwitches.Contains("g"))
                    behaviours.Add(new GrabbingBehaviour());

                if (behaviourSwitches.Contains("n"))
                    behaviours.Add(new NoneBehaviour());

                if (behaviourSwitches.Contains("p"))
                    behaviours.Add(new PhysicsBehaviour());
    
                if (behaviourSwitches.Contains("t"))
                    behaviours.Add(new TeleportBehaviour());

                StartBot(this, behaviours, firstName, lastName, password, loginUri);
            }
        }

//        /// <summary>
//        /// Add additional bots (and threads) to our bot pool
//        /// </summary>
//        /// <param name="botcount">How Many of them to add</param>
//        public void addbots(int botcount)
//        {
//            int len = m_td.Length;
//            Thread[] m_td2 = new Thread[len + botcount];
//            for (int i = 0; i < len; i++)
//            {
//                m_td2[i] = m_td[i];
//            }
//            m_td = m_td2;
//            int newlen = len + botcount;
//            for (int i = len; i < newlen; i++)
//            {
//                startupBot(Config);
//            }
//        }

        /// <summary>
        /// This starts up the bot and stores the thread for the bot in the thread array
        /// </summary>
        /// <param name="bm"></param>
        /// <param name="behaviours">Behaviours for this bot to perform.</param>
        /// <param name="firstName">First name</param>
        /// <param name="lastName">Last name</param>
        /// <param name="password">Password</param>
        /// <param name="loginUri">Login URI</param>
        public void StartBot(
             BotManager bm, List<IBehaviour> behaviours,
             string firstName, string lastName, string password, string loginUri)
        {
            MainConsole.Instance.OutputFormat(
                "[BOT MANAGER]: Starting bot {0} {1}, behaviours are {2}",
                firstName, lastName, string.Join(",", behaviours.ConvertAll<string>(b => b.Name).ToArray()));

            Bot pb = new Bot(bm, behaviours, firstName, lastName, password, loginUri);

            pb.OnConnected += handlebotEvent;
            pb.OnDisconnected += handlebotEvent;

            lock (m_lBot)
                m_lBot.Add(pb);

            Thread pbThread = new Thread(pb.startup);
            pbThread.Name = pb.Name;
            pbThread.IsBackground = true;

            pbThread.Start();

            // Stagger logins
            Thread.Sleep(LoginDelay);
        }

        /// <summary>
        /// High level connnected/disconnected events so we can keep track of our threads by proxy
        /// </summary>
        /// <param name="callbot"></param>
        /// <param name="eventt"></param>
        private void handlebotEvent(Bot callbot, EventType eventt)
        {
            switch (eventt)
            {
                case EventType.CONNECTED:
                    m_log.Info("[" + callbot.FirstName + " " + callbot.LastName + "]: Connected");
                    break;
                case EventType.DISCONNECTED:
                    m_log.Info("[" + callbot.FirstName + " " + callbot.LastName + "]: Disconnected");

                    lock (m_lBot)
                    {
                        if (m_lBot.TrueForAll(b => b.ConnectionState == ConnectionState.Disconnected))
                            Environment.Exit(0);

                        break;
                    }
            }
        }

        /// <summary>
        /// Shut down all bots
        /// </summary>
        /// <remarks>
        /// We launch each shutdown on its own thread so that a slow shutting down bot doesn't hold up all the others.
        /// </remarks>
        public void doBotShutdown()
        {
            lock (m_lBot)
            {
                foreach (Bot bot in m_lBot)
                {
                    Bot thisBot = bot;
                    Util.FireAndForget(o => thisBot.shutdown());
                }
            }
        }

        /// <summary>
        /// Standard CreateConsole routine
        /// </summary>
        /// <returns></returns>
        protected CommandConsole CreateConsole()
        {
            return new LocalConsole("pCampbot");
        }

        private void HandleShutdown(string module, string[] cmd)
        {
            m_log.Info("[BOTMANAGER]: Shutting down bots");
            doBotShutdown();
        }

        private void HandleShowRegions(string module, string[] cmd)
        {
            string outputFormat = "{0,-30}  {1, -20}  {2, -5}  {3, -5}";
            MainConsole.Instance.OutputFormat(outputFormat, "Name", "Handle", "X", "Y");

            lock (RegionsKnown)
            {
                foreach (GridRegion region in RegionsKnown.Values)
                {
                    MainConsole.Instance.OutputFormat(
                        outputFormat, region.Name, region.RegionHandle, region.X, region.Y);
                }
            }
        }

        private void HandleShowStatus(string module, string[] cmd)
        {
            string outputFormat = "{0,-30}  {1, -30}  {2,-14}";
            MainConsole.Instance.OutputFormat(outputFormat, "Name", "Region", "Status");

            Dictionary<ConnectionState, int> totals = new Dictionary<ConnectionState, int>();
            foreach (object o in Enum.GetValues(typeof(ConnectionState)))
                totals[(ConnectionState)o] = 0;

            lock (m_lBot)
            {
                foreach (Bot pb in m_lBot)
                {
                    Simulator currentSim = pb.Client.Network.CurrentSim;
                    totals[pb.ConnectionState]++;

                    MainConsole.Instance.OutputFormat(
                        outputFormat,
                        pb.Name, currentSim != null ? currentSim.Name : "(none)", pb.ConnectionState);
                }
            }

            ConsoleDisplayList cdl = new ConsoleDisplayList();

            foreach (KeyValuePair<ConnectionState, int> kvp in totals)
                cdl.AddRow(kvp.Key, kvp.Value);


            MainConsole.Instance.OutputFormat("\n{0}", cdl.ToString());
        }

        /*
        private void HandleQuit(string module, string[] cmd)
        {
            m_console.Warn("DANGER", "This should only be used to quit the program if you've already used the shutdown command and the program hasn't quit");
            Environment.Exit(0);
        }
        */
//
//        private void HandleAddBots(string module, string[] cmd)
//        {
//            int newbots = 0;
//            
//            if (cmd.Length > 2)
//            {
//                Int32.TryParse(cmd[2], out newbots);
//            }
//            if (newbots > 0)
//                addbots(newbots);
//        }

        internal void Grid_GridRegion(object o, GridRegionEventArgs args)
        {
            lock (RegionsKnown)
            {
                GridRegion newRegion = args.Region;

                if (RegionsKnown.ContainsKey(newRegion.RegionHandle))
                {
                    return;
                }
                else
                {
                    m_log.DebugFormat(
                        "[BOT MANAGER]: Adding {0} {1} to known regions", newRegion.Name, newRegion.RegionHandle);
                    RegionsKnown[newRegion.RegionHandle] = newRegion;
                }
            }
        }
    }
}
