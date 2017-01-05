/*
 * Copyright (c) Contributors
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
using Mono.Addins;

using System;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenSim.Region.OptionalModules.Scripting.JsonStore
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "JsonStoreCommandsModule")]

    public class JsonStoreCommandsModule  : INonSharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfig m_config = null;
        private bool m_enabled = false;

        private Scene m_scene = null;
        //private IJsonStoreModule m_store;
        private JsonStoreModule m_store;

#region Region Module interface

        // -----------------------------------------------------------------
        /// <summary>
        /// Name of this shared module is it's class name
        /// </summary>
        // -----------------------------------------------------------------
        public string Name
        {
            get { return this.GetType().Name; }
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Initialise this shared module
        /// </summary>
        /// <param name="scene">this region is getting initialised</param>
        /// <param name="source">nini config, we are not using this</param>
        // -----------------------------------------------------------------
        public void Initialise(IConfigSource config)
        {
            try
            {
                if ((m_config = config.Configs["JsonStore"]) == null)
                {
                    // There is no configuration, the module is disabled
                    // m_log.InfoFormat("[JsonStore] no configuration info");
                    return;
                }

                m_enabled = m_config.GetBoolean("Enabled", m_enabled);
            }
            catch (Exception e)
            {
                m_log.Error("[JsonStore]: initialization error: {0}", e);
                return;
            }

            if (m_enabled)
                m_log.DebugFormat("[JsonStore]: module is enabled");
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// everything is loaded, perform post load configuration
        /// </summary>
        // -----------------------------------------------------------------
        public void PostInitialise()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Nothing to do on close
        /// </summary>
        // -----------------------------------------------------------------
        public void Close()
        {
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;

            }
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public void RemoveRegion(Scene scene)
        {
            // need to remove all references to the scene in the subscription
            // list to enable full garbage collection of the scene object
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Called when all modules have been added for a region. This is
        /// where we hook up events
        /// </summary>
        // -----------------------------------------------------------------
        public void RegionLoaded(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;

                m_store = (JsonStoreModule) m_scene.RequestModuleInterface<IJsonStoreModule>();
                if (m_store == null)
                {
                    m_log.ErrorFormat("[JsonStoreCommands]: JsonModule interface not defined");
                    m_enabled = false;
                    return;
                }

                scene.AddCommand("JsonStore", this, "jsonstore stats", "jsonstore stats",
                                 "Display statistics about the state of the JsonStore module", "",
                                 CmdStats);
            }
        }

        /// -----------------------------------------------------------------
        /// <summary>
        /// </summary>
        // -----------------------------------------------------------------
        public Type ReplaceableInterface
        {
            get { return null; }
        }

#endregion

#region Commands

        private void CmdStats(string module, string[] cmd)
        {
            if (MainConsole.Instance.ConsoleScene != m_scene && MainConsole.Instance.ConsoleScene != null)
                return;

            JsonStoreStats stats = m_store.GetStoreStats();
            MainConsole.Instance.OutputFormat("{0}\t{1}",m_scene.RegionInfo.RegionName,stats.StoreCount);
        }

#endregion

    }
}
