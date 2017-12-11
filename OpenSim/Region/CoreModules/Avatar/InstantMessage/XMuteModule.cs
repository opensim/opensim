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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;
using OpenSim.Data.MySQL;
using MySql.Data.MySqlClient;


namespace OpenSim.Region.CoreModules.Avatar.InstantMessage
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XMute")]
    public class XMuteModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled = true;
        protected List<Scene> m_SceneList = new List<Scene>();
        protected MuteTableHandler m_MuteTable;
        protected string m_DatabaseConnect;

        public void Initialise(IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                m_Enabled = false;
                return;
            }

            if (cnf.GetString("MuteListModule", "None") !=
                    "XMute")
            {
                m_Enabled = false;
                return;
            }

            m_DatabaseConnect = cnf.GetString("MuteDatabaseConnect", String.Empty);
            if (m_DatabaseConnect == String.Empty)
            {
                m_log.Debug("[XMute]: MuteDatabaseConnect missing or empty");
                m_Enabled = false;
                return;
            }

            m_MuteTable = new MuteTableHandler(
                    m_DatabaseConnect, "XMute", String.Empty);
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_SceneList)
            {
                m_SceneList.Add(scene);

                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_SceneList)
            {
                m_SceneList.Remove(scene);
            }
        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;

            m_log.Debug("[XMute]: Mute list enabled");
        }

        public string Name
        {
            get { return "XMuteModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }
        
        public void Close()
        {
        }
       
        private void OnNewClient(IClientAPI client)
        {
            client.OnMuteListRequest += OnMuteListRequest;
            client.OnUpdateMuteListEntry += OnUpdateMuteListEntry;
            client.OnRemoveMuteListEntry += OnRemoveMuteListEntry;
        }

        private void OnMuteListRequest(IClientAPI client, uint crc)
        {
            string filename = "mutes"+client.AgentId.ToString();

            IXfer xfer = client.Scene.RequestModuleInterface<IXfer>();
            if (xfer != null)
            {
                MuteData[] data = m_MuteTable.Get("AgentID", client.AgentId.ToString());
                if (data == null || data.Length == 0)
                {
                    xfer.AddNewFile(filename, new Byte[0]);
                }
                else
                {
                    StringBuilder sb = new StringBuilder(1024);

                    foreach (MuteData d in data)
                        sb.AppendFormat("{0} {1} {2}|{3}\n",
                                d.MuteType,
                                d.MuteID.ToString(),
                                d.MuteName,
                                d.MuteFlags);

                    Byte[] filedata = Util.UTF8.GetBytes(sb.ToString());

                    uint dataCrc = Crc32.Compute(filedata);

                    if (dataCrc == crc)
                    {
                        client.SendUseCachedMuteList();
                        return;
                    }

                    xfer.AddNewFile(filename, filedata);
                }

                client.SendMuteListUpdate(filename);
            }
        }

        private void OnUpdateMuteListEntry(IClientAPI client, UUID muteID, string muteName, int muteType, uint muteFlags)
        {
            MuteData mute = new MuteData();

            mute.AgentID = client.AgentId;
            mute.MuteID = muteID;
            mute.MuteName = muteName;
            mute.MuteType = muteType;
            mute.MuteFlags = (int)muteFlags;
            mute.Stamp = Util.UnixTimeSinceEpoch();

            m_MuteTable.Store(mute);
        }

        private void OnRemoveMuteListEntry(IClientAPI client, UUID muteID, string muteName)
        {
            m_MuteTable.Delete(new string[] { "AgentID",
                                              "MuteID",
                                              "MuteName" },
                               new string[] { client.AgentId.ToString(),
                                              muteID.ToString(),
                                              muteName });
        }
    }

    public class MuteTableHandler : MySQLGenericTableHandler<MuteData>
    {
        public MuteTableHandler(string conn, string realm, string m) : base(conn, realm, m)
        {
        }

        public bool Delete(string[] fields, string[] val)
        {
            if (fields.Length != val.Length)
                return false;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string text = String.Format("delete from {0} where ", m_Realm);

                List<string> terms = new List<string>();

                for (int i = 0 ; i < fields.Length ; i++)
                {
                    terms.Add(String.Format("{0} = ?{0}", fields[i]));
                    cmd.Parameters.AddWithValue("?" + fields[i], val[i]);
                }

                text += string.Join(" and ", terms.ToArray());

                cmd.CommandText = text;

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
                return false;
            }
        }
    }
}

