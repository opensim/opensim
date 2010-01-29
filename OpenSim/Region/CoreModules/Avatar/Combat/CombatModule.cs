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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.Avatar.Combat.CombatModule
{
    public class CombatModule : IRegionModule
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
        public void Initialise(Scene scene, IConfigSource config)
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

        public bool IsSharedModule
        {
            get { return true; }
        }

        private void KillAvatar(uint killerObjectLocalID, ScenePresence DeadAvatar)
        {
            if (killerObjectLocalID == 0)
                DeadAvatar.ControllingClient.SendAgentAlertMessage("You committed suicide!", true);
            else
            {
                bool foundResult = false;
                string resultstring = String.Empty;
                ScenePresence[] allav = DeadAvatar.Scene.GetScenePresences();
                try
                {
                    for (int i = 0; i < allav.Length; i++)
                    {
                        ScenePresence av = allav[i];

                        if (av.LocalId == killerObjectLocalID)
                        {
                            av.ControllingClient.SendAlertMessage("You fragged " + DeadAvatar.Firstname + " " + DeadAvatar.Lastname);
                            resultstring = av.Firstname + " " + av.Lastname;
                            foundResult = true;
                        }
                    }
                } catch (InvalidOperationException)
                {

                }

                if (!foundResult)
                {
                    SceneObjectPart part = DeadAvatar.Scene.GetSceneObjectPart(killerObjectLocalID);
                    if (part != null)
                    {
                        ScenePresence av = DeadAvatar.Scene.GetScenePresence(part.OwnerID);
                        if (av != null)
                        {
                            av.ControllingClient.SendAlertMessage("You fragged " + DeadAvatar.Firstname + " " + DeadAvatar.Lastname);
                            resultstring = av.Firstname + " " + av.Lastname;
                            DeadAvatar.ControllingClient.SendAgentAlertMessage("You got killed by " + resultstring + "!", true);
                        }
                        else
                        {
                            string killer = DeadAvatar.Scene.CommsManager.UUIDNameRequestString(part.OwnerID);
                            DeadAvatar.ControllingClient.SendAgentAlertMessage("You impaled yourself on " + part.Name + " owned by " + killer +"!", true);
                        }
                        //DeadAvatar.Scene. part.ObjectOwner
                    }
                    else
                    {
                        DeadAvatar.ControllingClient.SendAgentAlertMessage("You died!", true);
                    }
                }
            }
            DeadAvatar.Health = 100;
            DeadAvatar.Scene.TeleportClientHome(DeadAvatar.UUID, DeadAvatar.ControllingClient);
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            ILandObject obj = avatar.Scene.LandChannel.GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
            if ((obj.LandData.Flags & (uint)ParcelFlags.AllowDamage) != 0)
            {
                avatar.Invulnerable = false;
            }
            else
            {
                avatar.Invulnerable = true;
            }
        }
    }
}
