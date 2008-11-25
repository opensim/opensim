/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */

using System;
using System.Collections.Generic;
using System.Net;

using OpenSim.Framework;

namespace OpenSim.Framework
{
    public class HGNetworkServersInfo
    {

        public readonly string LocalAssetServerURI, LocalInventoryServerURI, LocalUserServerURI;

        private static HGNetworkServersInfo m_singleton;
        public static HGNetworkServersInfo Singleton
        {
            get { return m_singleton; }
        }

        public static void Init(string assetserver, string inventoryserver, string userserver)
        {
            m_singleton = new HGNetworkServersInfo(assetserver, inventoryserver, userserver);

        }

        private HGNetworkServersInfo(string a, string i, string u)
        {
            LocalAssetServerURI = ServerURI(a);
            LocalInventoryServerURI = ServerURI(i);
            LocalUserServerURI = ServerURI(u);
        }

        public bool IsLocalUser(string userserver)
        {
            string userServerURI = ServerURI(userserver);
            bool ret = (((userServerURI == null) || (userServerURI == "") || (userServerURI == LocalUserServerURI)));
            //Console.WriteLine("-------------> HGNetworkServersInfo.IsLocalUser? " + ret + "(userServer=" + userServerURI + "; localuserserver=" + LocalUserServerURI + ")");
            return ret;
        }

        public static string ServerURI(string uri)
        {
            IPAddress ipaddr1 = null;
            string port1 = "";
            try
            {
                ipaddr1 = Util.GetHostFromURL(uri);
            }
            catch { }

            try
            {
                port1 = uri.Split(new char[] { ':' })[2];
            }
            catch { }

            // We tried our best to convert the domain names to IP addresses
            return (ipaddr1 != null) ? "http://" + ipaddr1.ToString() + ":" + port1 : uri;
        }

    }
}
