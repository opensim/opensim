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
using System.Threading;
using System.Timers;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using Timer=System.Timers.Timer;

namespace OpenSim.TestSuite
{
    public class PhysicsBot
    {
        public delegate void AnEvent(PhysicsBot callbot, EventType someevent); // event delegate for bot events
        public IConfig startupConfig; // bot config, passed from BotManager

        public string firstname;
        public string lastname;
        public string password;
        public string loginURI;

        public event AnEvent OnConnected;
        public event AnEvent OnDisconnected;

        protected Timer m_action; // Action Timer

        protected Random somthing = new Random(Environment.TickCount);// We do stuff randomly here

        //New instance of a SecondLife client
        public GridClient client = new GridClient();

        protected string[] talkarray;
        /// <summary>
        ///
        /// </summary>
        /// <param name="bsconfig">nini config for the bot</param>
        public PhysicsBot(IConfig bsconfig)
        {
            startupConfig = bsconfig;
            readconfig();
            talkarray = readexcuses();
        }

        //We do our actions here.  This is where one would
        //add additional steps and/or things the bot should do

        void m_action_Elapsed(object sender, ElapsedEventArgs e)
        {
            //client.Throttle.Task = 500000f;
            //client.Throttle.Set();
            int walkorrun = somthing.Next(4); // Randomize between walking and running. The greater this number,
                                              // the greater the bot's chances to walk instead of run.
            if (walkorrun == 0)
            {
                client.Self.Movement.AlwaysRun = true;
            }
            else
            {
                client.Self.Movement.AlwaysRun = false;
            }

            // TODO: unused: Vector3 pos = client.Self.SimPosition;
            Vector3 newpos = new Vector3(somthing.Next(255), somthing.Next(255), somthing.Next(255));
            client.Self.Movement.TurnToward(newpos);

            for (int i = 0; i < 2000; i++)
            {
                client.Self.Movement.AtPos = true;
                Thread.Sleep(somthing.Next(25, 75)); // Makes sure the bots keep walking for this time.
            }
            client.Self.Jump(true);

            string randomf = talkarray[somthing.Next(talkarray.Length)];
            if (talkarray.Length > 1 && randomf.Length > 1)
                client.Self.Chat(randomf, 0, ChatType.Normal);

            //Thread.Sleep(somthing.Next(1, 10)); // Apparently its better without it right now.
        }

        /// <summary>
        /// Read the Nini config and initialize
        /// </summary>
        public void readconfig()
        {
            firstname = startupConfig.GetString("firstname", "random");
            lastname = startupConfig.GetString("lastname", "random");
            password = startupConfig.GetString("password", "12345");
            loginURI = startupConfig.GetString("loginuri");
        }

        /// <summary>
        /// Tells LibSecondLife to logout and disconnect.  Raises the disconnect events once it finishes.
        /// </summary>
        public void shutdown()
        {
            client.Network.Logout();
        }

        /// <summary>
        /// This is the bot startup loop.
        /// </summary>
        public void startup()
        {
            client.Settings.LOGIN_SERVER = loginURI;
            client.Network.LoginProgress += this.Network_LoginProgress;
            client.Network.SimConnected += this.Network_SimConnected;
            client.Network.Disconnected += this.Network_OnDisconnected;
            if (client.Network.Login(firstname, lastname, password, "pCampBot", "Your name"))
            {

                if (OnConnected != null)
                {
                    m_action = new Timer(somthing.Next(1000, 10000));
                    m_action.Elapsed += new ElapsedEventHandler(m_action_Elapsed);
                    m_action.Start();
                    OnConnected(this, EventType.CONNECTED);
                    client.Self.Jump(true);
                }
            }
            else
            {
                MainConsole.Instance.Output(firstname + " " + lastname + "Can't login: " + client.Network.LoginMessage);
                if (OnDisconnected != null)
                {
                    OnDisconnected(this, EventType.DISCONNECTED);
                }
            }
        }

        public void Network_LoginProgress(object sender, LoginProgressEventArgs args)
        {
            if (args.Status == LoginStatus.Success)
            {
                if (OnConnected != null)
                {
                    OnConnected(this, EventType.CONNECTED);
                }
            }
        }

        public void Network_SimConnected(object sender, SimConnectedEventArgs args)
        {
        }

        public void Network_OnDisconnected(object sender, DisconnectedEventArgs args)
        {
            if (OnDisconnected != null)
            {
                OnDisconnected(this, EventType.DISCONNECTED);
            }
        }

        public string[] readexcuses()
        {
            string allexcuses = "";

            string file = Path.Combine(Util.configDir(), "pCampBotSentences.txt");
            if (File.Exists(file))
            {
                StreamReader csr = File.OpenText(file);
                allexcuses = csr.ReadToEnd();
                csr.Close();
            }

            return allexcuses.Split(Environment.NewLine.ToCharArray());
        }
    }
}
