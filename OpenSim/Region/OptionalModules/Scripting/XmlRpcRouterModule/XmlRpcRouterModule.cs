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
using System.Reflection;

using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;


namespace OpenSim.Region.OptionalModules.Scripting.XmlRpcRouterModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XmlRpcRouter")]
    public class XmlRpcRouter : INonSharedRegionModule, IXmlRpcRouter
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;

        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["XMLRPC"];
            if (startupConfig == null)
                return;

            if (startupConfig.GetString("XmlRpcRouterModule",
                    "XmlRpcRouterModule") == "XmlRpcRouterModule")
                m_Enabled = true;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IXmlRpcRouter>(this);
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
            get { return "XmlRpcRouterModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void RegisterNewReceiver(IScriptModule scriptEngine, UUID channel, UUID objectID, UUID itemID, string uri)
        {
            if (m_Enabled)
            {
                scriptEngine.PostScriptEvent(itemID, "xmlrpc_uri", new Object[] { uri });
            }
        }

        public void UnRegisterReceiver(string channelID, UUID itemID)
        {
        }

        public void ScriptRemoved(UUID itemID)
        {
            // System.Console.WriteLine("TEST Script Removed!");
        }

        public void ObjectRemoved(UUID objectID)
        {
            // System.Console.WriteLine("TEST Obj Removed!");
        }
    }
}
