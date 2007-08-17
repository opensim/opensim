/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Configuration;

using Nini.Config;
namespace OpenSim.Framework.Types
{
    public class NetworkServersInfo
    {
        public string AssetURL = "http://127.0.0.1:8003/";
        public string AssetSendKey = "";

        public string GridURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";
        public string UserURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";
        public bool isSandbox;

        public int HttpListenerPort = 9000;
        public int RemotingListenerPort = 8895;


        public NetworkServersInfo()
        {
        }

        public NetworkServersInfo(uint defaultHomeLocX, uint defaultHomeLocY)
        {
            m_defaultHomeLocX = defaultHomeLocX;
            m_defaultHomeLocY = defaultHomeLocY;
        }

        private uint? m_defaultHomeLocX;
        public uint DefaultHomeLocX
        {
            get { return m_defaultHomeLocX.Value; }
        }

        private uint? m_defaultHomeLocY;
        public uint DefaultHomeLocY
        {
            get { return m_defaultHomeLocY.Value; }
        }

        public void loadFromConfiguration(IConfigSource config)
        {
            m_defaultHomeLocX = (uint)config.Configs["StandAlone"].GetInt("default_location_x", 1000);
            m_defaultHomeLocY = (uint)config.Configs["StandAlone"].GetInt("default_location_y", 1000);

            HttpListenerPort = config.Configs["Network"].GetInt("http_listener_port", 9000);
            RemotingListenerPort = config.Configs["Network"].GetInt("remoting_listener_port", 8895);
            GridURL = config.Configs["Network"].GetString("grid_server_url", "http://127.0.0.1:8001");
            GridSendKey = config.Configs["Network"].GetString("grid_send_key", "null");
            GridRecvKey = config.Configs["Network"].GetString("grid_recv_key", "null");
            UserURL = config.Configs["Network"].GetString("user_server_url", "http://127.0.0.1:8002");
            UserSendKey = config.Configs["Network"].GetString("user_send_key", "null");
            UserRecvKey = config.Configs["Network"].GetString("user_recv_key", "null");
            AssetURL = config.Configs["Network"].GetString("asset_server_url", "http://127.0.0.1:8003");

        }
    }
}
