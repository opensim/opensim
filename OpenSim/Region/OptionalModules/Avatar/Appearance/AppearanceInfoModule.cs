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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.Appearance
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar appearance.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AppearanceInfoModule")]
    public class AppearanceInfoModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                
        
        protected Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        protected IAvatarFactory m_avatarFactory;
        
        public string Name { get { return "Appearance Information Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);                                     
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene; 
            
            scene.AddCommand(
                this, "appearance show",
                "appearance show",
                "Show appearance information for each avatar in the simulator.",
                "At the moment this actually just checks that we have all the required baked textures.  If not, then appearance is 'corrupt' and other avatars will continue to see a cloud.",
                ShowAppearanceInfo);
        }                 

        protected void ShowAppearanceInfo(string module, string[] cmd)
        {     
            lock (m_scenes)
            {   
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView && !((LLClientView)client).ChildAgentStatus())
                            {
                                bool bakedTextureValid = scene.AvatarFactory.ValidateBakedTextureCache(client);
                                MainConsole.Instance.OutputFormat(
                                    "{0} baked apperance texture is {1}", client.Name, bakedTextureValid ? "OK" : "corrupt");
                            }
                        });
                }
            }
        }      
    }
}