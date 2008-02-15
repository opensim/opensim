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
*     * Neither the name of the OpenSim Project nor the
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
* 
*/

using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using Nini.Config;
using System.Threading;
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
            //Set up our nifty config..  thanks to nini
            ArgvConfigSource cs = new ArgvConfigSource(args);

            cs.AddSwitch("Startup", "botcount");
            cs.AddSwitch("Startup", "loginuri");
            cs.AddSwitch("Startup", "firstname");
            cs.AddSwitch("Startup", "lastname");
            cs.AddSwitch("Startup", "password");

            IConfig ol = cs.Configs["Startup"];
            int botcount = ol.GetInt("botcount", 1);
            BotManager bm = new BotManager();

            //startup specified number of bots.  1 is the default
            bm.dobotStartup(botcount, ol);
            while (true)
            {
                MainConsole.Instance.Prompt();
            }
        }
    }
}
