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
        [STAThread]
        public static void Main(string[] args)
        {
            IConfig config = ParseConfig(args);
            if (config.Get("help") != null || config.Get("loginuri") == null)
            {
                Help();
            }
            else
            {
                int botcount = config.GetInt("botcount", 1);

                BotManager bm = new BotManager();

                //startup specified number of bots.  1 is the default
                bm.dobotStartup(botcount, config);
                while (true)
                {
                    MainConsole.Instance.Prompt();
                }
            }
        }

        private static IConfig ParseConfig(String[] args)
        {
            //Set up our nifty config..  thanks to nini
            ArgvConfigSource cs = new ArgvConfigSource(args);

            cs.AddSwitch("Startup", "botcount","n");
            cs.AddSwitch("Startup", "loginuri","l");
            cs.AddSwitch("Startup", "firstname");
            cs.AddSwitch("Startup", "lastname");
            cs.AddSwitch("Startup", "password");
            cs.AddSwitch("Startup", "help","h");
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
                                     "usage: pCampBot <-loginuri loginuri> [OPTIONS]\n" +
                                     "Spawns a set of bots to test an OpenSim region\n\n" +
                                     "  -l, -loginuri      loginuri for sim to log into (required)\n" +
                                     "  -n, -botcount      number of bots to start (default: 1)\n" +
                                     "  -firstname         first name for the bot(s) (default: random string)\n" +
                                     "  -lastname          lastname for the bot(s) (default: random string)\n" +
                                     "  -password          password for the bots(s) (default: random string)\n" +
                                     "  -wear              set appearance folder to load from (default: no)\n" +
                                     "  -h, -help          show this message"
                                     );
        }
    }
}
