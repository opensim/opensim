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
using System.Globalization;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.Profiles
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class AvatarProfilesModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        private IProfileModule m_profileModule = null;
        private bool m_enabled = true;

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig profileConfig = config.Configs["Profile"];
            if (profileConfig != null)
            {
                if (profileConfig.GetString("Module", Name) != Name)
                {
                    m_enabled = false;
                    return;
                }
            }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
                return;
            m_profileModule = m_scene.RequestModuleInterface<IProfileModule>();
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnNewClient -= NewClient;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AvatarProfilesModule"; }
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            client.OnRequestAvatarProperties += RequestAvatarProperty;
            client.OnUpdateAvatarProperties += UpdateAvatarProperties;
        }

        public void RemoveClient(IClientAPI client)
        {
            client.OnRequestAvatarProperties -= RequestAvatarProperty;
            client.OnUpdateAvatarProperties -= UpdateAvatarProperties;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="avatarID"></param>
        public void RequestAvatarProperty(IClientAPI remoteClient, UUID avatarID)
        {
            // FIXME: finish adding fields such as url, masking, etc.
            UserProfileData profile = m_scene.CommsManager.UserService.GetUserProfile(avatarID);
            if (null != profile)
            {
                Byte[] charterMember;
                if (profile.CustomType == "")
                {
                    charterMember = new Byte[1];
                    charterMember[0] = (Byte)((profile.UserFlags & 0xf00) >> 8);
                }
                else
                {
                    charterMember = Utils.StringToBytes(profile.CustomType);
                }

                if (m_profileModule != null)
                {
                    Hashtable profileData = m_profileModule.GetProfileData(remoteClient.AgentId);
                    if (profileData["ProfileUrl"] != null)
                        profile.ProfileUrl = profileData["ProfileUrl"].ToString();
                }
                remoteClient.SendAvatarProperties(profile.ID, profile.AboutText,
                                                  Util.ToDateTime(profile.Created).ToString("M/d/yyyy", CultureInfo.InvariantCulture),
                                                  charterMember, profile.FirstLifeAboutText, (uint)(profile.UserFlags & 0xff),
                                                  profile.FirstLifeImage, profile.Image, profile.ProfileUrl, profile.Partner);
            }
            else
            {
                m_log.Debug("[AvatarProfilesModule]: Got null for profile for " + avatarID.ToString());
            }
        }

        public void UpdateAvatarProperties(IClientAPI remoteClient, UserProfileData newProfile)
        {
            UserProfileData Profile = m_scene.CommsManager.UserService.GetUserProfile(newProfile.ID);

            // if it's the profile of the user requesting the update, then we change only a few things.
            if (remoteClient.AgentId.CompareTo(Profile.ID) == 0)
            {
                Profile.Image = newProfile.Image;
                Profile.FirstLifeImage = newProfile.FirstLifeImage;
                Profile.AboutText = newProfile.AboutText;
                Profile.FirstLifeAboutText = newProfile.FirstLifeAboutText;
                Profile.ProfileUrl = newProfile.ProfileUrl;
            }
            else
            {
                return;
            }
            
            if (m_scene.CommsManager.UserService.UpdateUserProfile(Profile))
            {
                RequestAvatarProperty(remoteClient, newProfile.ID);
            }
        }
    }
}
