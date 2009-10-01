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

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public partial class HGScene : Scene
    {
        /// <summary>
        /// Teleport an avatar to their home region
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="client"></param>
        public override void TeleportClientHome(UUID agentId, IClientAPI client)
        {
            m_log.Debug("[HGScene]: TeleportClientHome " + client.FirstName + " " + client.LastName);

            CachedUserInfo uinfo = CommsManager.UserProfileCacheService.GetUserDetails(agentId);
            if (uinfo != null)
            {
                UserProfileData UserProfile = uinfo.UserProfile;

                if (UserProfile != null)
                {
                    GridRegion regionInfo = GridService.GetRegionByUUID(UUID.Zero, UserProfile.HomeRegionID);
                    //if (regionInfo != null)
                    //{
                    //    UserProfile.HomeRegionID = regionInfo.RegionID;
                    //    //CommsManager.UserService.UpdateUserProfile(UserProfile);
                    //}
                    if (regionInfo == null)
                    {
                        // can't find the Home region: Tell viewer and abort
                        client.SendTeleportFailed("Your home-region could not be found.");
                        return;
                    }
                    RequestTeleportLocation(
                        client, regionInfo.RegionHandle, UserProfile.HomeLocation, UserProfile.HomeLookAt,
                        (uint)(TPFlags.SetLastToTarget | TPFlags.ViaHome));
                }
            }
            else
                client.SendTeleportFailed("Sorry! I lost your home-region information.");

        }

    }
}
