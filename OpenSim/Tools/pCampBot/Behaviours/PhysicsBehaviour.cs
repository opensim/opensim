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
using OpenMetaverse;
using OpenSim.Framework;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// Stress physics by moving and bouncing around bots a whole lot.
    /// </summary>
    /// <remarks>
    /// TODO: talkarray should be in a separate behaviour.
    /// </remarks>
    public class PhysicsBehaviour : IBehaviour
    {
        private string[] talkarray;

        public PhysicsBehaviour()
        {
            talkarray = readexcuses();
        }

        public void Action(Bot bot)
        {
            int walkorrun = bot.Random.Next(4); // Randomize between walking and running. The greater this number,
                                                // the greater the bot's chances to walk instead of run.
            bot.Client.Self.Jump(false);
            if (walkorrun == 0)
            {
                bot.Client.Self.Movement.AlwaysRun = true;
            }
            else
            {
                bot.Client.Self.Movement.AlwaysRun = false;
            }

            // TODO: unused: Vector3 pos = client.Self.SimPosition;
            Vector3 newpos = new Vector3(bot.Random.Next(1, 254), bot.Random.Next(1, 254), bot.Random.Next(1, 254));
            bot.Client.Self.Movement.TurnToward(newpos);

            bot.Client.Self.Movement.AtPos = true;
            Thread.Sleep(bot.Random.Next(3000, 13000));
            bot.Client.Self.Movement.AtPos = false;
            bot.Client.Self.Jump(true);

            string randomf = talkarray[bot.Random.Next(talkarray.Length)];
            if (talkarray.Length > 1 && randomf.Length > 1)
                bot.Client.Self.Chat(randomf, 0, ChatType.Normal);

            Thread.Sleep(bot.Random.Next(1000, 10000));
        }

        private string[] readexcuses()
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