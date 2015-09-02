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

using System;
using System.Collections.Generic;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Combat.CombatModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CombatModule")]
    public class CombatModule : ISharedRegionModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Region UUIDS indexed by AgentID
        /// </summary>
        //private Dictionary<UUID, UUID> m_rootAgents = new Dictionary<UUID, UUID>();

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();

        /// <summary>
        /// Startup
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="config"></param>
        public void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            lock (m_scenel)
            {
                if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
                {
                    m_scenel[scene.RegionInfo.RegionHandle] = scene;
                }
                else
                {
                    m_scenel.Add(scene.RegionInfo.RegionHandle, scene);
                }
            }

            scene.EventManager.OnAvatarKilled += KillAvatar;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
                m_scenel.Remove(scene.RegionInfo.RegionHandle);

            scene.EventManager.OnAvatarKilled -= KillAvatar;
            scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "CombatModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }


        private void KillAvatar(uint killerObjectLocalID, ScenePresence deadAvatar)
        {
            string deadAvatarMessage;
            ScenePresence killingAvatar = null;
//            string killingAvatarMessage;

            // check to see if it is an NPC and just remove it
            INPCModule NPCmodule = deadAvatar.Scene.RequestModuleInterface<INPCModule>();
            if (NPCmodule != null && NPCmodule.DeleteNPC(deadAvatar.UUID, deadAvatar.Scene))
            {
                return;
            }

            if (killerObjectLocalID == 0)
                deadAvatarMessage = "You committed suicide!";
            else
            {
                // Try to get the avatar responsible for the killing
                killingAvatar = deadAvatar.Scene.GetScenePresence(killerObjectLocalID);
                if (killingAvatar == null)
                {
                    // Try to get the object which was responsible for the killing
                    SceneObjectPart part = deadAvatar.Scene.GetSceneObjectPart(killerObjectLocalID);
                    if (part == null)
                    {
                        // Cause of death: Unknown
                        deadAvatarMessage = "You died!";
                    }
                    else
                    {
                        // Try to find the avatar wielding the killing object
                        killingAvatar = deadAvatar.Scene.GetScenePresence(part.OwnerID);
                        if (killingAvatar == null)
                        {
                            IUserManagement userManager = deadAvatar.Scene.RequestModuleInterface<IUserManagement>();
                            string userName = "Unkown User";
                            if (userManager != null)
                                userName = userManager.GetUserName(part.OwnerID);
                            deadAvatarMessage = String.Format("You impaled yourself on {0} owned by {1}!", part.Name, userName);
                        }
                        else
                        {
                            //                            killingAvatarMessage = String.Format("You fragged {0}!", deadAvatar.Name);
                            deadAvatarMessage = String.Format("You got killed by {0}!", killingAvatar.Name);
                        }
                    }
                }
                else
                {
//                    killingAvatarMessage = String.Format("You fragged {0}!", deadAvatar.Name);
                    deadAvatarMessage = String.Format("You got killed by {0}!", killingAvatar.Name);
                }
            }
            try
            {
                deadAvatar.ControllingClient.SendAgentAlertMessage(deadAvatarMessage, true);
                if (killingAvatar != null)
                    killingAvatar.ControllingClient.SendAlertMessage("You fragged " + deadAvatar.Firstname + " " + deadAvatar.Lastname);
            }
            catch (InvalidOperationException)
            { }

            deadAvatar.setHealthWithUpdate(100.0f);
            deadAvatar.Scene.TeleportClientHome(deadAvatar.UUID, deadAvatar.ControllingClient);
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            try
            {
                ILandObject obj = avatar.Scene.LandChannel.GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                if (obj == null)
                    return;
                if ((obj.LandData.Flags & (uint)ParcelFlags.AllowDamage) != 0
                    || avatar.Scene.RegionInfo.RegionSettings.AllowDamage)
                {
                    avatar.Invulnerable = false;
                }
                else
                {
                    avatar.Invulnerable = true;
                    if (avatar.Health < 100.0f)
                    {
                        avatar.setHealthWithUpdate(100.0f);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
