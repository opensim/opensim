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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
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
            
            // We do receive a post to ObjectMedia when a new avatar enters the region - though admittedly this is the
            // avatar that set the texture in the first place.
            // Even though we're registering for POST we're going to get GETS and UPDATES too
            caps.RegisterHandler(
                "ObjectMedia", new RestStreamHandler("POST", "/CAPS/" + UUID.Random(), HandleObjectMediaRequest));
            
            // We do get these posts when the url has been changed.
            // Even though we're registering for POST we're going to get GETS and UPDATES too
            caps.RegisterHandler(
                "ObjectMediaNavigate", new RestStreamHandler("POST", "/CAPS/" + UUID.Random(), HandleObjectMediaNavigateRequest));
        }        
        
        /// <summary>
        /// Sets or gets per face media textures.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        protected string HandleObjectMediaRequest(
            string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {            
            m_log.DebugFormat("[MOAP]: Got ObjectMedia raw request [{0}]", request);
            
            Hashtable osdParams = new Hashtable();
            osdParams = (Hashtable)LLSD.LLSDDeserialize(Utils.StringToBytes(request));            
            
            foreach (Object key in osdParams.Keys)
                m_log.DebugFormat("[MOAP]: Param {0}={1}", key, osdParams[key]);
            
            string verb = (string)osdParams["verb"];
            
            if ("GET" == verb)
                return HandleObjectMediaRequestGet(path, osdParams, httpRequest, httpResponse);
            if ("UPDATE" == verb)
                return HandleObjectMediaRequestUpdate(path, osdParams, httpRequest, httpResponse);
                                    
            //NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            
            // TODO: Persist in memory
            // TODO: Tell other agents in the region about the change via the ObjectMediaResponse (?) message
            // TODO: Persist in database             
            
            return string.Empty;
        }
        
        protected string HandleObjectMediaRequestGet(
            string path, Hashtable osdParams, OSHttpRequest httpRequest, OSHttpResponse httpResponse)        
        {
            UUID primId = (UUID)osdParams["object_id"];
            
            SceneObjectPart part = m_scene.GetSceneObjectPart(primId);
            
            if (null == part)
            {
                m_log.WarnFormat(
                    "[MOAP]: Received a GET ObjectMediaRequest for prim {0} but this doesn't exist in the scene", 
                    primId);
                return string.Empty;
            }
                        
            /*
            int faces = part.GetNumberOfSides();
            m_log.DebugFormat("[MOAP]: Faces [{0}] for [{1}]", faces, primId);
            
            MediaEntry[] media = new MediaEntry[faces];
            for (int i = 0; i < faces; i++)
            {
                MediaEntry me = new MediaEntry();                
                me.HomeURL = "google.com";
                me.CurrentURL = "google.com";
                me.AutoScale = true;
                //me.Height = 300;
                //me.Width = 240;
                media[i] = me;
            }
            */
            
            if (null == part.Shape.Media)
                return string.Empty;
            
            ObjectMediaResponse resp = new ObjectMediaResponse();
            
            resp.PrimID = primId;
            resp.FaceMedia = part.Shape.Media.ToArray();
            
            // I know this has to end with the last avatar to edit and the version code shouldn't always be 16.  Just trying
            // to minimally satisfy for now to get something working
            resp.Version = "x-mv:0000000016/" + UUID.Random();
           
            string rawResp = OSDParser.SerializeLLSDXmlString(resp.Serialize());
            
            m_log.DebugFormat("[MOAP]: Got HandleObjectMediaRequestGet raw response is [{0}]", rawResp);
            
            return rawResp;
        }
        
        protected string HandleObjectMediaRequestUpdate(
            string path, Hashtable osdParams, OSHttpRequest httpRequest, OSHttpResponse httpResponse)        
        {
            UUID primId = (UUID)osdParams["object_id"];
            
            SceneObjectPart part = m_scene.GetSceneObjectPart(primId);
            
            if (null == part)
            {
                m_log.WarnFormat(
                    "[MOAP]: Received am UPDATE ObjectMediaRequest for prim {0} but this doesn't exist in the scene", 
                    primId);
                return string.Empty;
            }            
            
            List<MediaEntry> cookedMediaEntries = new List<MediaEntry>();
            
            ArrayList rawMediaEntries = (ArrayList)osdParams["object_media_data"];
            foreach (Object obj in rawMediaEntries)
            {
                Hashtable rawMe = (Hashtable)obj;
                
                // TODO: Yeah, I know this is silly.  Very soon use existing better code in libomv to do this.
                MediaEntry cookedMe = new MediaEntry();
                cookedMe.EnableAlterntiveImage = (bool)rawMe["alt_image_enable"];
                cookedMe.AutoLoop = (bool)rawMe["auto_loop"];
                cookedMe.AutoPlay = (bool)rawMe["auto_play"];
                cookedMe.AutoScale = (bool)rawMe["auto_scale"];
                cookedMe.AutoZoom = (bool)rawMe["auto_zoom"];
                cookedMe.InteractOnFirstClick = (bool)rawMe["first_click_interact"];
                cookedMe.Controls = (MediaControls)rawMe["controls"];
                cookedMe.HomeURL = (string)rawMe["home_url"];
                cookedMe.CurrentURL = (string)rawMe["current_url"];
                cookedMe.Height = (int)rawMe["height_pixels"];
                cookedMe.Width = (int)rawMe["width_pixels"];
                cookedMe.ControlPermissions = (MediaPermission)Enum.Parse(typeof(MediaPermission), rawMe["perms_control"].ToString());
                cookedMe.InteractPermissions = (MediaPermission)Enum.Parse(typeof(MediaPermission), rawMe["perms_interact"].ToString());
                cookedMe.EnableWhiteList = (bool)rawMe["whitelist_enable"];
                //cookedMe.WhiteList = (string[])rawMe["whitelist"];
                
                cookedMediaEntries.Add(cookedMe);
            }
            
            m_log.DebugFormat("[MOAP]: Received {0} media entries for prim {1}", cookedMediaEntries.Count, primId);
            
            part.Shape.Media = cookedMediaEntries;
            
            return string.Empty;
        }
        
        /// <summary>
        /// Received from the viewer if a user has changed the url of a media texture.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">/param>
        /// <param name="httpResponse">/param>
        /// <returns></returns>
        protected string HandleObjectMediaNavigateRequest(
            string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {            
            m_log.DebugFormat("[MOAP]: Got ObjectMediaNavigate request for {0}", path);
            //NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            
            // TODO: Persist in memory
            // TODO: Tell other agents in the region about the change via the ObjectMediaResponse (?) message
            // TODO: Persist in database            
            
            return string.Empty;
        }   
        
        
    }
}