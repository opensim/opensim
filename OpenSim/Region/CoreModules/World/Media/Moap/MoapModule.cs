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
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Media.Moap
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MoapModule")]
    public class MoapModule : INonSharedRegionModule
    {    
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public string Name { get { return "MoapModule"; } }                
        public Type ReplaceableInterface { get { return null; } }        
        
        protected Scene m_scene;
        
        public void Initialise(IConfigSource config) {}

        public void AddRegion(Scene scene) 
        { 
            m_scene = scene;
        }

        public void RemoveRegion(Scene scene) {}

        public void RegionLoaded(Scene scene) 
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }
        
        public void Close() {}        
        
        public void RegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[MOAP]: Registering ObjectMedia and ObjectMediaNavigate capabilities for agent {0}", agentID);
            
            caps.RegisterHandler(
                "ObjectMedia", new RestStreamHandler("GET", "/CAPS/" + UUID.Random(), OnObjectMediaRequest));
            caps.RegisterHandler(
                "ObjectMediaNavigate", new RestStreamHandler("GET", "/CAPS/" + UUID.Random(), OnObjectMediaNavigateRequest));
        }        
        
        protected string OnObjectMediaRequest(
            string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {            
            m_log.DebugFormat("[MOAP]: Got ObjectMedia request for {0}", path);
            //NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            
            return string.Empty;
        }
        
        protected string OnObjectMediaNavigateRequest(
            string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {            
            m_log.DebugFormat("[MOAP]: Got ObjectMediaNavigate request for {0}", path);
            //NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            
            return string.Empty;
        }        
    }
}