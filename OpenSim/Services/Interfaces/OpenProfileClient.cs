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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Services.UserProfilesService
{
    /// <summary>
    /// A client for accessing a profile server using the OpenProfile protocol.
    /// </summary>
    /// <remarks>
    /// This class was adapted from the full OpenProfile class. Since it's only a client, and not a server,
    /// it's much simpler.
    /// </remarks>
    public class OpenProfileClient
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverURI;

        /// <summary>
        /// Creates a client for accessing a foreign grid's profile server using the OpenProfile protocol.
        /// </summary>
        /// <param name="serverURI">The grid's profile server URL</param>
        public OpenProfileClient(string serverURI)
        {
            m_serverURI = serverURI;
        }

        /// <summary>
        /// Gets an avatar's profile using the OpenProfile protocol.
        /// </summary>
        /// <param name="props">On success, this will contain the avatar's profile</param>
        /// <returns>Success/failure</returns>
        /// <remarks>
        /// There are two profile modules currently in use in OpenSim: the older one is OpenProfile, and the newer
        /// one is UserProfileModule (this file). This method attempts to read an avatar's profile from a foreign
        /// grid using the OpenProfile protocol.
        /// </remarks>
        public bool RequestAvatarPropertiesUsingOpenProfile(ref UserProfileProperties props)
        {
            Hashtable ReqHash = new Hashtable();
            ReqHash["avatar_id"] = props.UserId.ToString();

            Hashtable profileData = XMLRPCRequester.SendRequest(ReqHash, "avatar_properties_request", m_serverURI);

            if (profileData == null)
                return false;
            if (!profileData.ContainsKey("data"))
                return false;

            ArrayList dataArray = (ArrayList)profileData["data"];

            if (dataArray == null || dataArray[0] == null)
                return false;
            profileData = (Hashtable)dataArray[0];

            props.WebUrl = string.Empty;
            props.AboutText = String.Empty;
            props.FirstLifeText = String.Empty;
            props.ImageId = UUID.Zero;
            props.FirstLifeImageId = UUID.Zero;
            props.PartnerId = UUID.Zero;

            if (profileData["ProfileUrl"] != null)
                props.WebUrl = profileData["ProfileUrl"].ToString();
            if (profileData["AboutText"] != null)
                props.AboutText = profileData["AboutText"].ToString();
            if (profileData["FirstLifeAboutText"] != null)
                props.FirstLifeText = profileData["FirstLifeAboutText"].ToString();
            if (profileData["Image"] != null)
                props.ImageId = new UUID(profileData["Image"].ToString());
            if (profileData["FirstLifeImage"] != null)
                props.FirstLifeImageId = new UUID(profileData["FirstLifeImage"].ToString());
            if (profileData["Partner"] != null)
                props.PartnerId = new UUID(profileData["Partner"].ToString());

            props.WantToMask = 0;
            props.WantToText = String.Empty;
            props.SkillsMask = 0;
            props.SkillsText = String.Empty;
            props.Language = String.Empty;

            if (profileData["wantmask"] != null)
                props.WantToMask = Convert.ToInt32(profileData["wantmask"].ToString());
            if (profileData["wanttext"] != null)
                props.WantToText = profileData["wanttext"].ToString();

            if (profileData["skillsmask"] != null)
                props.SkillsMask = Convert.ToInt32(profileData["skillsmask"].ToString());
            if (profileData["skillstext"] != null)
                props.SkillsText = profileData["skillstext"].ToString();

            if (profileData["languages"] != null)
                props.Language = profileData["languages"].ToString();

            return true;
        }
    }
}