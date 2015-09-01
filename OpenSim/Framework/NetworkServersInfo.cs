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
        public uint HttpListenerPort = ConfigSettings.DefaultRegionHttpPort;
        public bool secureInventoryServer = false;
        public bool isSandbox;
        public bool HttpUsesSSL = false;
        public string HttpSSLCN = "";
        public uint httpSSLPort = 9001;

        // "Out of band" managemnt https
        public bool ssl_listener = false;
        public uint https_port = 0;
        public string cert_path = String.Empty;
        public string cert_pass = String.Empty;

        public NetworkServersInfo()
        {
        }

        public NetworkServersInfo(uint defaultHomeLocX, uint defaultHomeLocY)
        {
        }

        public void loadFromConfiguration(IConfigSource config)
        {
            HttpListenerPort =
                (uint) config.Configs["Network"].GetInt("http_listener_port", (int) ConfigSettings.DefaultRegionHttpPort);
            httpSSLPort =
                (uint)config.Configs["Network"].GetInt("http_listener_sslport", ((int)ConfigSettings.DefaultRegionHttpPort+1));
            HttpUsesSSL = config.Configs["Network"].GetBoolean("http_listener_ssl", false);
            HttpSSLCN = config.Configs["Network"].GetString("http_listener_cn", "localhost");

            // "Out of band management https"
            ssl_listener = config.Configs["Network"].GetBoolean("https_listener",false);
            if( ssl_listener)
            {
                cert_path = config.Configs["Network"].GetString("cert_path",String.Empty);
                cert_pass = config.Configs["Network"].GetString("cert_pass",String.Empty);
                https_port = (uint)config.Configs["Network"].GetInt("https_port", 0);
            }
        }
    }
}
