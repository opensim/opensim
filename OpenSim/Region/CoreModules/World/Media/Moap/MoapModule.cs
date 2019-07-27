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
using System.Xml;
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
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.CoreModules.World.Media.Moap
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MoapModule")]
    public class MoapModule : INonSharedRegionModule, IMoapModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "MoapModule"; } }
        public Type ReplaceableInterface { get { return null; } }

        /// <summary>
        /// Is this module enabled?
        /// </summary>
        protected bool m_isEnabled = true;

        /// <summary>
        /// The scene to which this module is attached
        /// </summary>
        protected Scene m_scene;

        /// <summary>
        /// Track the ObjectMedia capabilities given to users keyed by path
        /// </summary>
        protected Dictionary<string, UUID> m_omCapUsers = new Dictionary<string, UUID>();

        /// <summary>
        /// Track the ObjectMedia capabilities given to users keyed by agent.  Lock m_omCapUsers to manipulate.
        /// </summary>
        protected Dictionary<UUID, string> m_omCapUrls = new Dictionary<UUID, string>();

        /// <summary>
        /// Track the ObjectMediaUpdate capabilities given to users keyed by path
        /// </summary>
        protected Dictionary<string, UUID> m_omuCapUsers = new Dictionary<string, UUID>();

        /// <summary>
        /// Track the ObjectMediaUpdate capabilities given to users keyed by agent.  Lock m_omuCapUsers to manipulate
        /// </summary>
        protected Dictionary<UUID, string> m_omuCapUrls = new Dictionary<UUID, string>();

        public void Initialise(IConfigSource configSource)
        {
            IConfig config = configSource.Configs["MediaOnAPrim"];

            if (config != null && !config.GetBoolean("Enabled", false))
                m_isEnabled = false;
//            else
//                m_log.Debug("[MOAP]: Initialised module.")l
        }

        public void AddRegion(Scene scene)
        {
            if (!m_isEnabled)
                return;

            m_scene = scene;
            m_scene.RegisterModuleInterface<IMoapModule>(this);
        }

        public void RemoveRegion(Scene scene) {}

        public void RegionLoaded(Scene scene)
        {
            if (!m_isEnabled)
                return;

            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnDeregisterCaps += OnDeregisterCaps;
            m_scene.EventManager.OnSceneObjectPartCopy += OnSceneObjectPartCopy;
        }

        public void Close()
        {
            if (!m_isEnabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene.EventManager.OnDeregisterCaps -= OnDeregisterCaps;
            m_scene.EventManager.OnSceneObjectPartCopy -= OnSceneObjectPartCopy;
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
//            m_log.DebugFormat(
//                "[MOAP]: Registering ObjectMedia and ObjectMediaNavigate capabilities for agent {0}", agentID);

            string omCapUrl = "/CAPS/" + UUID.Random();

            lock (m_omCapUsers)
            {
                m_omCapUsers[omCapUrl] = agentID;
                m_omCapUrls[agentID] = omCapUrl;

                // Even though we're registering for POST we're going to get GETS and UPDATES too
                caps.RegisterHandler(
                    "ObjectMedia",
                    new RestStreamHandler(
                        "POST", omCapUrl, HandleObjectMediaMessage, "ObjectMedia", agentID.ToString()));
            }

            string omuCapUrl = "/CAPS/" + UUID.Random();

            lock (m_omuCapUsers)
            {
                m_omuCapUsers[omuCapUrl] = agentID;
                m_omuCapUrls[agentID] = omuCapUrl;

                // Even though we're registering for POST we're going to get GETS and UPDATES too
                caps.RegisterHandler(
                    "ObjectMediaNavigate",
                    new RestStreamHandler(
                        "POST", omuCapUrl, HandleObjectMediaNavigateMessage, "ObjectMediaNavigate", agentID.ToString()));
            }
        }

        public void OnDeregisterCaps(UUID agentID, Caps caps)
        {
            lock (m_omCapUsers)
            {
                string path = m_omCapUrls[agentID];
                m_omCapUrls.Remove(agentID);
                m_omCapUsers.Remove(path);
            }

            lock (m_omuCapUsers)
            {
                string path = m_omuCapUrls[agentID];
                m_omuCapUrls.Remove(agentID);
                m_omuCapUsers.Remove(path);
            }
        }

        protected void OnSceneObjectPartCopy(SceneObjectPart copy, SceneObjectPart original, bool userExposed)
        {
            if (original.Shape.Media != null)
            {
                PrimitiveBaseShape.MediaList dupeMedia = new PrimitiveBaseShape.MediaList();
                lock (original.Shape.Media)
                {
                    foreach (MediaEntry me in original.Shape.Media)
                    {
                        if (me != null)
                            dupeMedia.Add(MediaEntry.FromOSD(me.GetOSD()));
                        else
                            dupeMedia.Add(null);
                    }
                }

                copy.Shape.Media = dupeMedia;
            }
        }

        public MediaEntry GetMediaEntry(SceneObjectPart part, int face)
        {
            MediaEntry me = null;

            CheckFaceParam(part, face);

            List<MediaEntry> media = part.Shape.Media;

            if (null == media)
            {
                me = null;
            }
            else
            {
                lock (media)
                    me = media[face];

                if (me != null)
                {
                    Primitive.TextureEntry te = part.Shape.Textures;
                    Primitive.TextureEntryFace teFace = te.GetFace((uint)face);
                    if (teFace != null && teFace.MediaFlags)
                        me = MediaEntry.FromOSD(me.GetOSD());
                }
            }

//            m_log.DebugFormat("[MOAP]: GetMediaEntry for {0} face {1} found {2}", part.Name, face, me);

            return me;
        }

        /// <summary>
        /// Set the media entry on the face of the given part.
        /// </summary>
        /// <param name="part">/param>
        /// <param name="face"></param>
        /// <param name="me">If null, then the media entry is cleared.</param>
        public void SetMediaEntry(SceneObjectPart part, int face, MediaEntry me)
        {
//            m_log.DebugFormat("[MOAP]: SetMediaEntry for {0}, face {1}", part.Name, face);

            CheckFaceParam(part, face);

            if (null == part.Shape.Media)
            {
                if (me == null)
                    return;
                else
                    part.Shape.Media = new PrimitiveBaseShape.MediaList(new MediaEntry[part.GetNumberOfSides()]);
            }

            lock (part.Shape.Media)
                part.Shape.Media[face] = me;

            UpdateMediaUrl(part, UUID.Zero);

            SetPartMediaFlags(part, face, me != null);

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
            part.TriggerScriptChangedEvent(Changed.MEDIA);
        }

        /// <summary>
        /// Clear the media entry from the face of the given part.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="face"></param>
        public void ClearMediaEntry(SceneObjectPart part, int face)
        {
            SetMediaEntry(part, face, null);
        }

        /// <summary>
        /// Set the media flags on the texture face of the given part.
        /// </summary>
        /// <remarks>
        /// The fact that we need a separate function to do what should be a simple one line operation is BUTT UGLY.
        /// </remarks>
        /// <param name="part"></param>
        /// <param name="face"></param>
        /// <param name="flag"></param>
        protected void SetPartMediaFlags(SceneObjectPart part, int face, bool flag)
        {
            Primitive.TextureEntry te = part.Shape.Textures;
            Primitive.TextureEntryFace teFace = te.CreateFace((uint)face);
            teFace.MediaFlags = flag;
            part.Shape.Textures = te;
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
        protected string HandleObjectMediaMessage(
            string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat("[MOAP]: Got ObjectMedia path [{0}], raw request [{1}]", path, request);

            OSDMap osd = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            ObjectMediaMessage omm = new ObjectMediaMessage();
            omm.Deserialize(osd);

            if (omm.Request is ObjectMediaRequest)
                return HandleObjectMediaRequest(omm.Request as ObjectMediaRequest);
            else if (omm.Request is ObjectMediaUpdate)
                return HandleObjectMediaUpdate(path, omm.Request as ObjectMediaUpdate);

            throw new Exception(
                string.Format(
                    "[MOAP]: ObjectMediaMessage has unrecognized ObjectMediaBlock of {0}",
                    omm.Request.GetType()));
        }

        /// <summary>
        /// Handle a fetch request for media textures
        /// </summary>
        /// <param name="omr"></param>
        /// <returns></returns>
        protected string HandleObjectMediaRequest(ObjectMediaRequest omr)
        {
            UUID primId = omr.PrimID;

            SceneObjectPart part = m_scene.GetSceneObjectPart(primId);

            if (null == part)
            {
                m_log.WarnFormat(
                    "[MOAP]: Received a GET ObjectMediaRequest for prim {0} but this doesn't exist in region {1}",
                    primId, m_scene.RegionInfo.RegionName);
                return string.Empty;
            }

            if (part.Shape.Media == null)
                return string.Empty;

            MediaEntry[] currentML = part.Shape.Media.ToArray();

            int nentries = currentML.Length;
            int nsides = part.GetNumberOfSides();
            if(nentries > nsides)
                nentries = nsides;

            Primitive.TextureEntry te = part.Shape.Textures;
            bool isnull = true;

            for(int face = 0; face < nentries; ++face)
            {
                Primitive.TextureEntryFace teFace = te.GetFace((uint)face);
                if(!teFace.MediaFlags)
                    currentML[face] = null;
                else
                    isnull = false;
            }

            if(isnull)
            {
                //remove the damm thing
                part.Shape.Media = null;
                part.MediaUrl = null;
                return string.Empty;
            }

            ObjectMediaResponse resp = new ObjectMediaResponse();
            resp.PrimID = primId;

            resp.FaceMedia = currentML;

            resp.Version = part.MediaUrl;

            string rawResp = OSDParser.SerializeLLSDXmlString(resp.Serialize());

//            m_log.DebugFormat("[MOAP]: Got HandleObjectMediaRequestGet raw response is [{0}]", rawResp);

            return rawResp;
        }

        /// <summary>
        /// Handle an update of media textures.
        /// </summary>
        /// <param name="path">Path on which this request was made</param>
        /// <param name="omu">/param>
        /// <returns></returns>
        protected string HandleObjectMediaUpdate(string path, ObjectMediaUpdate omu)
        {
            UUID primId = omu.PrimID;

            SceneObjectPart part = m_scene.GetSceneObjectPart(primId);

            if (null == part)
            {
                m_log.WarnFormat(
                    "[MOAP]: Received an UPDATE ObjectMediaRequest for prim {0} but this doesn't exist in region {1}",
                    primId, m_scene.RegionInfo.RegionName);
                return string.Empty;
            }

//            m_log.DebugFormat("[MOAP]: Received {0} media entries for prim {1}", omu.FaceMedia.Length, primId);
//
//            for (int i = 0; i < omu.FaceMedia.Length; i++)
//            {
//                MediaEntry me = omu.FaceMedia[i];
//                string v = (null == me ? "null": OSDParser.SerializeLLSDXmlString(me.GetOSD()));
//                m_log.DebugFormat("[MOAP]: Face {0} [{1}]", i, v);
//            }

            if (omu.FaceMedia.Length > part.GetNumberOfSides())
            {
                m_log.WarnFormat(
                    "[MOAP]: Received {0} media entries from client for prim {1} {2} but this prim has only {3} faces.  Dropping request.",
                    omu.FaceMedia.Length, part.Name, part.UUID, part.GetNumberOfSides());
                return string.Empty;
            }

            UUID agentId = default(UUID);

            lock (m_omCapUsers)
                agentId = m_omCapUsers[path];

            List<MediaEntry> media = part.Shape.Media;

            if (null == media)
            {
//                m_log.DebugFormat("[MOAP]: Setting all new media list for {0}", part.Name);
                part.Shape.Media = new PrimitiveBaseShape.MediaList(omu.FaceMedia);

                for (int i = 0; i < omu.FaceMedia.Length; i++)
                {
                    if (omu.FaceMedia[i] != null)
                    {
                        // FIXME: Race condition here since some other texture entry manipulator may overwrite/get
                        // overwritten.  Unfortunately, PrimitiveBaseShape does not allow us to change texture entry
                        // directly.
                        SetPartMediaFlags(part, i, true);
//                        m_log.DebugFormat(
//                            "[MOAP]: Media flags for face {0} is {1}",
//                            i, part.Shape.Textures.FaceTextures[i].MediaFlags);
                    }
                }
            }
            else
            {
//                m_log.DebugFormat("[MOAP]: Setting existing media list for {0}", part.Name);

                // We need to go through the media textures one at a time to make sure that we have permission
                // to change them

                // FIXME: Race condition here since some other texture entry manipulator may overwrite/get
                // overwritten.  Unfortunately, PrimitiveBaseShape does not allow us to change texture entry
                // directly.
                Primitive.TextureEntry te = part.Shape.Textures;

                lock (media)
                {
                    for (int i = 0; i < media.Count; i++)
                    {
                        if (m_scene.Permissions.CanControlPrimMedia(agentId, part.UUID, i))
                        {
                            media[i] = omu.FaceMedia[i];

                            // When a face is cleared this is done by setting the MediaFlags in the TextureEntry via a normal
                            // texture update, so we don't need to worry about clearing MediaFlags here.
                            if (null == media[i])
                                continue;

                            SetPartMediaFlags(part, i, true);

    //                        m_log.DebugFormat(
    //                            "[MOAP]: Media flags for face {0} is {1}",
    //                            i, face.MediaFlags);
    //                        m_log.DebugFormat("[MOAP]: Set media entry for face {0} on {1}", i, part.Name);
                        }
                    }
                }

                part.Shape.Textures = te;

//                for (int i2 = 0; i2 < part.Shape.Textures.FaceTextures.Length; i2++)
//                    m_log.DebugFormat("[MOAP]: FaceTexture[{0}] is {1}", i2, part.Shape.Textures.FaceTextures[i2]);
            }

            UpdateMediaUrl(part, agentId);

            // Arguably, we could avoid sending a full update to the avatar that just changed the texture.
            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();

            part.TriggerScriptChangedEvent(Changed.MEDIA);

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
        protected string HandleObjectMediaNavigateMessage(
            string request, string path, string param, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat("[MOAP]: Got ObjectMediaNavigate request [{0}]", request);

            OSDMap osd = (OSDMap)OSDParser.DeserializeLLSDXml(request);
            ObjectMediaNavigateMessage omn = new ObjectMediaNavigateMessage();
            omn.Deserialize(osd);

            UUID primId = omn.PrimID;

            SceneObjectPart part = m_scene.GetSceneObjectPart(primId);

            if (null == part)
            {
                m_log.WarnFormat(
                    "[MOAP]: Received an ObjectMediaNavigateMessage for prim {0} but this doesn't exist in region {1}",
                    primId, m_scene.RegionInfo.RegionName);
                return string.Empty;
            }

            UUID agentId = default(UUID);

            lock (m_omuCapUsers)
                agentId = m_omuCapUsers[path];

            if (!m_scene.Permissions.CanInteractWithPrimMedia(agentId, part.UUID, omn.Face))
                return string.Empty;

//            m_log.DebugFormat(
//                "[MOAP]: Received request to update media entry for face {0} on prim {1} {2} to {3}",
//                omn.Face, part.Name, part.UUID, omn.URL);

            // If media has never been set for this prim, then just return.
            if (null == part.Shape.Media)
                return string.Empty;

            MediaEntry me = null;

            lock (part.Shape.Media)
                me = part.Shape.Media[omn.Face];

            // Do the same if media has not been set up for a specific face
            if (null == me)
                return string.Empty;

            if (me.EnableWhiteList)
            {
                if (!CheckUrlAgainstWhitelist(omn.URL, me.WhiteList))
                {
//                    m_log.DebugFormat(
//                        "[MOAP]: Blocking change of face {0} on prim {1} {2} to {3} since it's not on the enabled whitelist",
//                        omn.Face, part.Name, part.UUID, omn.URL);

                    return string.Empty;
                }
            }

            me.CurrentURL = omn.URL;

            UpdateMediaUrl(part, agentId);

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();

            part.TriggerScriptChangedEvent(Changed.MEDIA);

            return OSDParser.SerializeLLSDXmlString(new OSD());
        }

        /// <summary>
        /// Check that the face number is valid for the given prim.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="face"></param>
        protected void CheckFaceParam(SceneObjectPart part, int face)
        {
            if (face < 0)
                throw new ArgumentException("Face cannot be less than zero");

            int maxFaces = part.GetNumberOfSides() - 1;
            if (face > maxFaces)
                throw new ArgumentException(
                    string.Format("Face argument was {0} but max is {1}", face, maxFaces));
        }

        /// <summary>
        /// Update the media url of the given part
        /// </summary>
        /// <param name="part"></param>
        /// <param name="updateId">
        /// The id to attach to this update.  Normally, this is the user that changed the
        /// texture
        /// </param>
        protected void UpdateMediaUrl(SceneObjectPart part, UUID updateId)
        {
            if (null == part.MediaUrl)
            {
                // TODO: We can't set the last changer until we start tracking which cap we give to which agent id
                part.MediaUrl = "x-mv:0000000000/" + updateId;
            }
            else
            {
                string rawVersion = part.MediaUrl.Substring(5, 10);
                int version = int.Parse(rawVersion);
                part.MediaUrl = string.Format("x-mv:{0:D10}/{1}", ++version, updateId);
            }

//            m_log.DebugFormat("[MOAP]: Storing media url [{0}] in prim {1} {2}", part.MediaUrl, part.Name, part.UUID);
        }

        /// <summary>
        /// Check the given url against the given whitelist.
        /// </summary>
        /// <param name="rawUrl"></param>
        /// <param name="whitelist"></param>
        /// <returns>true if the url matches an entry on the whitelist, false otherwise</returns>
        protected bool CheckUrlAgainstWhitelist(string rawUrl, string[] whitelist)
        {
            if (whitelist == null)
                return false;

            Uri url = new Uri(rawUrl);

            foreach (string origWlUrl in whitelist)
            {
                string wlUrl = origWlUrl;

                // Deal with a line-ending wildcard
                if (wlUrl.EndsWith("*"))
                    wlUrl = wlUrl.Remove(wlUrl.Length - 1);

//                m_log.DebugFormat("[MOAP]: Checking whitelist URL pattern {0}", origWlUrl);

                // Handle a line starting wildcard slightly differently since this can only match the domain, not the path
                if (wlUrl.StartsWith("*"))
                {
                    wlUrl = wlUrl.Substring(1);

                    if (url.Host.Contains(wlUrl))
                    {
//                        m_log.DebugFormat("[MOAP]: Whitelist URL {0} matches {1}", origWlUrl, rawUrl);
                        return true;
                    }
                }
                else
                {
                    string urlToMatch = url.Authority + url.AbsolutePath;

                    if (urlToMatch.StartsWith(wlUrl))
                    {
//                        m_log.DebugFormat("[MOAP]: Whitelist URL {0} matches {1}", origWlUrl, rawUrl);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}