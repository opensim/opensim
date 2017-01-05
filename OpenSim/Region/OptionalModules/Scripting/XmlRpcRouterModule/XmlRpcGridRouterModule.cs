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

using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.XmlRpcGridRouterModule
{
    public class XmlRpcInfo
    {
        public UUID item;
        public UUID channel;
        public string uri;
    }

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XmlRpcGridRouter")]
    public class XmlRpcGridRouter : INonSharedRegionModule, IXmlRpcRouter
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, UUID> m_Channels =
                new Dictionary<UUID, UUID>();

        private bool m_Enabled = false;
        private string m_ServerURI = String.Empty;

        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["XMLRPC"];
            if (startupConfig == null)
                return;

            if (startupConfig.GetString("XmlRpcRouterModule",
                    "XmlRpcRouterModule") == "XmlRpcGridRouterModule")
            {
                m_ServerURI = startupConfig.GetString("XmlRpcHubURI", String.Empty);
                if (m_ServerURI == String.Empty)
                {
                    m_log.Error("[XMLRPC GRID ROUTER] Module configured but no URI given. Disabling");
                    return;
                }
                m_Enabled = true;
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IXmlRpcRouter>(this);

            IScriptModule scriptEngine = scene.RequestModuleInterface<IScriptModule>();
            if ( scriptEngine != null )
            {
                scriptEngine.OnScriptRemoved += this.ScriptRemoved;
                scriptEngine.OnObjectRemoved += this.ObjectRemoved;

            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<IXmlRpcRouter>(this);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "XmlRpcGridRouterModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void RegisterNewReceiver(IScriptModule scriptEngine, UUID channel, UUID objectID, UUID itemID, string uri)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[XMLRPC GRID ROUTER]: New receiver Obj: {0} Ch: {1} ID: {2} URI: {3}",
                                objectID.ToString(), channel.ToString(), itemID.ToString(), uri);

            XmlRpcInfo info = new XmlRpcInfo();
            info.channel = channel;
            info.uri = uri;
            info.item = itemID;

            bool success = SynchronousRestObjectRequester.MakeRequest<XmlRpcInfo, bool>(
                    "POST", m_ServerURI+"/RegisterChannel/", info);

            if (!success)
            {
                m_log.Error("[XMLRPC GRID ROUTER] Error contacting server");
            }

            m_Channels[itemID] = channel;

        }

        public void UnRegisterReceiver(string channelID, UUID itemID)
        {
            if (!m_Enabled)
                return;

            RemoveChannel(itemID);

        }

        public void ScriptRemoved(UUID itemID)
        {
            if (!m_Enabled)
                return;

            RemoveChannel(itemID);

        }

        public void ObjectRemoved(UUID objectID)
        {
            // m_log.InfoFormat("[XMLRPC GRID ROUTER]: Object Removed {0}",objectID.ToString());
        }

        private bool RemoveChannel(UUID itemID)
        {
            if(!m_Channels.ContainsKey(itemID))
            {
                //m_log.InfoFormat("[XMLRPC GRID ROUTER]: Attempted to unregister non-existing Item: {0}", itemID.ToString());
                return false;
            }

            XmlRpcInfo info = new XmlRpcInfo();

            info.channel = m_Channels[itemID];
            info.item = itemID;
            info.uri = "http://0.0.0.0:00";

            if (info != null)
            {
                bool success = SynchronousRestObjectRequester.MakeRequest<XmlRpcInfo, bool>(
                        "POST", m_ServerURI+"/RemoveChannel/", info);

                if (!success)
                {
                    m_log.Error("[XMLRPC GRID ROUTER] Error contacting server");
                }

                m_Channels.Remove(itemID);
                return true;
            }
            return false;
        }
    }
}
