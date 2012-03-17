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
using System.IO;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;

using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.UserManagement
{
    public class HGUserManagementModule : UserManagementModule, ISharedRegionModule, IUserManagement
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        #region ISharedRegionModule

        public new void Initialise(IConfigSource config)
        {
            string umanmod = config.Configs["Modules"].GetString("UserManagementModule", base.Name);
            if (umanmod == Name)
            {
                m_Enabled = true;
                RegisterConsoleCmds();
                m_log.DebugFormat("[USER MANAGEMENT MODULE]: {0} is enabled", Name);
            }
        }

        public override string Name
        {
            get { return "HGUserManagementModule"; }
        }

        #endregion ISharedRegionModule

        protected override void AddAdditionalUsers(UUID avatarID, string query, List<UserData> users)
        {
            string[] words = query.Split(new char[] { ' ' });

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                {
                    if (i != words.Length - 1)
                        Array.Copy(words, i + 1, words, i, words.Length - i - 1);
                    Array.Resize(ref words, words.Length - 1);
                }
            }

            if (words.Length == 0 || words.Length > 2)
                return;

            if (words.Length == 2)  // First.Last @foo.com, maybe?
            {
                bool found = false;
                foreach (UserData d in m_UserCache.Values)
                {
                    if (d.LastName.StartsWith("@") && (d.FirstName.Equals(words[0]) || d.LastName.Equals(words[1])))
                    {
                        users.Add(d);
                        found = true;
                        break;
                    }
                }
                if (!found) // This is it! Let's ask the other world
                {
                    // TODO
                    //UserAgentServiceConnector uasConn = new UserAgentServiceConnector(words[0]);
                    //uasConn.GetUserInfo(...);
                }
            }
            else
            {
                foreach (UserData d in m_UserCache.Values)
                {
                    if (d.LastName.StartsWith("@") && (d.FirstName.StartsWith(query) || d.LastName.StartsWith(query)))
                        users.Add(d);
                }
            }
        }

    }
}