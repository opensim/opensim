using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.IPBan
{
    public class IPBanModule : IRegionModule 
    {
        #region Implementation of IRegionModule

        private List<string> m_bans = new List<string>();

        public void Initialise(Scene scene, IConfigSource source)
        {
            new SceneBanner(scene, m_bans);

            lock(m_bans)
            {
                foreach (EstateBan ban in scene.RegionInfo.EstateSettings.EstateBans)
                {
                    if(!String.IsNullOrEmpty(ban.BannedHostIPMask)) 
                        m_bans.Add(ban.BannedHostIPMask);
                    if (!String.IsNullOrEmpty(ban.BannedHostNameMask))
                        m_bans.Add(ban.BannedHostNameMask);
                }
            }
        }

        public void PostInitialise()
        {
            if(File.Exists("bans.txt"))
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

        public bool IsSharedModule
        {
            get { return true; }
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
