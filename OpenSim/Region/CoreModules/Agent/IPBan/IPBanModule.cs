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
using System.Text;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.IPBan
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "IPBanModule")]
    public class IPBanModule : ISharedRegionModule
    {
        #region Implementation of ISharedRegionModule

        private List<string> m_bans = new List<string>();

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            new SceneBanner(scene, m_bans);

            lock (m_bans)
            {
                foreach (EstateBan ban in scene.RegionInfo.EstateSettings.EstateBans)
                {
                    if (!String.IsNullOrEmpty(ban.BannedHostIPMask))
                        m_bans.Add(ban.BannedHostIPMask);
                    if (!String.IsNullOrEmpty(ban.BannedHostNameMask))
                        m_bans.Add(ban.BannedHostNameMask);
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
            if (File.Exists("bans.txt"))
            {
                string[] bans = File.ReadAllLines("bans.txt");
                foreach (string ban in bans)
                {
                    m_bans.Add(ban);
                }
            }
        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "IPBanModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        /// <summary>
        /// Bans all users from the specified network from connecting.
        /// DNS bans are in the form "somewhere.com" will block ANY
        /// matching domain (including "betasomewhere.com", "beta.somewhere.com",
        /// "somewhere.com.beta") - make sure to be reasonably specific in DNS
        /// bans.
        ///
        /// IP address bans match on first characters, so,
        /// "127.0.0.1" will ban only that address,
        /// "127.0.1" will ban "127.0.10.0"
        /// but "127.0.1." will ban only the "127.0.1.*" network
        /// </summary>
        /// <param name="host">See summary for explanation of parameter</param>
        public void Ban(string host)
        {
            m_bans.Add(host);
        }
    }
}
