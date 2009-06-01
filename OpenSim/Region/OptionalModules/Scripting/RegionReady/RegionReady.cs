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

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Scripting.RegionReady
{
    public class RegionReady : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IConfig m_config = null;
        private bool m_firstEmptyCompileQueue;
        private bool m_oarFileLoading;
        private bool m_lastOarLoadedOk;
        private int m_channelNotify = -1000;
        private bool m_enabled = false;
        
        Scene m_scene = null;
        
        #region IRegionModule interface
            
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log.Info("[RegionReady] Initialising");
            m_scene = scene;
            m_firstEmptyCompileQueue = true;
            m_oarFileLoading = false;
            m_lastOarLoadedOk = true;
            m_config = config.Configs["RegionReady"];

            if (m_config != null) 
            {
                m_enabled = m_config.GetBoolean("enabled", false);
                if (m_enabled) 
                {
                    m_channelNotify = m_config.GetInt("channel_notify", m_channelNotify);
                } 
            }
        }

        public void PostInitialise()
        {
            if (m_enabled) 
            {
                m_log.Info("[RegionReady]: Enabled");
                m_scene.EventManager.OnEmptyScriptCompileQueue += new EventManager.EmptyScriptCompileQueue(OnEmptyScriptCompileQueue);
                m_scene.EventManager.OnOarFileLoaded += new EventManager.OarFileLoaded(OnOarFileLoaded);
            }
            else
            {
                m_log.Info("[RegionReady]: Disabled");
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "RegionReadyModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        
        void OnEmptyScriptCompileQueue(int numScriptsFailed, string message)
        {
            if (m_firstEmptyCompileQueue || m_oarFileLoading) 
            {
                OSChatMessage c = new OSChatMessage();
                if (m_firstEmptyCompileQueue) 
                    c.Message = "server_startup,";
                else 
                    c.Message = "oar_file_load,";
                m_firstEmptyCompileQueue = false;
                m_oarFileLoading = false;

                m_scene.Backup();

                c.From = "RegionReady";
                if (m_lastOarLoadedOk) 
                    c.Message += "1,";
                else
                    c.Message += "0,";
                c.Channel = m_channelNotify;
                c.Message += numScriptsFailed.ToString() + "," + message;
                c.Type = ChatTypeEnum.Region;
                c.Position = new Vector3(128, 128, 30);
                c.Sender = null;
                c.SenderUUID = UUID.Zero;

                m_log.InfoFormat("[RegionReady]: Region \"{0}\" is ready: \"{1}\" on channel {2}",
                                 m_scene.RegionInfo.RegionName, c.Message, m_channelNotify);
                m_scene.EventManager.TriggerOnChatBroadcast(this, c); 
            }
        }

        void OnOarFileLoaded(Guid requestId, string message)
        {
            m_oarFileLoading = true;
            if (message==String.Empty) 
            {
                m_lastOarLoadedOk = true;
            } else {
                m_log.InfoFormat("[RegionReady]: Oar file load errors: {0}", message);
                m_lastOarLoadedOk = false;
            }
        }
    }
}
