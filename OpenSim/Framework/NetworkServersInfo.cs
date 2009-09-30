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

namespace OpenSim.Framework
{
    public class NetworkServersInfo
    {
        public string AssetSendKey = String.Empty;
        public string AssetURL = "http://127.0.0.1:" + ConfigSettings.DefaultAssetServerHttpPort.ToString() + "/";

        public string GridRecvKey = String.Empty;
        public string GridSendKey = String.Empty;
        public string GridURL = String.Empty;
        public uint HttpListenerPort = ConfigSettings.DefaultRegionHttpPort;
        public string InventoryURL = String.Empty;
        public bool secureInventoryServer = false;
        public bool isSandbox;
        private uint? m_defaultHomeLocX;
        private uint? m_defaultHomeLocY;
        public string UserRecvKey = String.Empty;
        public string UserSendKey = String.Empty;
        public string UserURL = String.Empty;
        public bool HttpUsesSSL = false;
        public string HttpSSLCN = "";
        public uint httpSSLPort = 9001;

        public string MessagingURL = String.Empty;

        public NetworkServersInfo()
        {
        }

        public NetworkServersInfo(uint defaultHomeLocX, uint defaultHomeLocY)
        {
            m_defaultHomeLocX = defaultHomeLocX;
            m_defaultHomeLocY = defaultHomeLocY;
        }

        public uint DefaultHomeLocX
        {
            get { return m_defaultHomeLocX.Value; }
        }

        public uint DefaultHomeLocY
        {
            get { return m_defaultHomeLocY.Value; }
        }

        public void loadFromConfiguration(IConfigSource config)
        {
            m_defaultHomeLocX = (uint) config.Configs["StandAlone"].GetInt("default_location_x", 1000);
            m_defaultHomeLocY = (uint) config.Configs["StandAlone"].GetInt("default_location_y", 1000);

            HttpListenerPort =
                (uint) config.Configs["Network"].GetInt("http_listener_port", (int) ConfigSettings.DefaultRegionHttpPort);
            httpSSLPort =
                (uint)config.Configs["Network"].GetInt("http_listener_sslport", ((int)ConfigSettings.DefaultRegionHttpPort+1));
            HttpUsesSSL = config.Configs["Network"].GetBoolean("http_listener_ssl", false);
            HttpSSLCN = config.Configs["Network"].GetString("http_listener_cn", "localhost");
            ConfigSettings.DefaultRegionRemotingPort =
                (uint) config.Configs["Network"].GetInt("remoting_listener_port", (int) ConfigSettings.DefaultRegionRemotingPort);
            GridURL =
                config.Configs["Network"].GetString("grid_server_url",
                                                    "http://127.0.0.1:" + ConfigSettings.DefaultGridServerHttpPort.ToString());
            GridSendKey = config.Configs["Network"].GetString("grid_send_key", "null");
            GridRecvKey = config.Configs["Network"].GetString("grid_recv_key", "null");
            UserURL =
                config.Configs["Network"].GetString("user_server_url",
                                                    "http://127.0.0.1:" + ConfigSettings.DefaultUserServerHttpPort.ToString());
            UserSendKey = config.Configs["Network"].GetString("user_send_key", "null");
            UserRecvKey = config.Configs["Network"].GetString("user_recv_key", "null");
            AssetURL = config.Configs["Network"].GetString("asset_server_url", AssetURL);
            InventoryURL = config.Configs["Network"].GetString("inventory_server_url",
                                                               "http://127.0.0.1:" +
                                                               ConfigSettings.DefaultInventoryServerHttpPort.ToString());
            secureInventoryServer = config.Configs["Network"].GetBoolean("secure_inventory_server", true);

            MessagingURL = config.Configs["Network"].GetString("messaging_server_url", string.Empty);
        }
    }
}
