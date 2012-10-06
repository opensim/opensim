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

using Nini.Config;
using OpenMetaverse;
using log4net;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Sound
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SoundModuleNonShared")]
    public class SoundModuleNonShared : INonSharedRegionModule, ISoundModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public bool Enabled { get; private set; }

        public float MaxDistance { get; private set; }

        #region INonSharedRegionModule

        public void Initialise(IConfigSource configSource)
        {
            IConfig config = configSource.Configs["Sounds"];

            if (config == null)
                return;

            Enabled = config.GetString("Module", "SoundModuleNonShared") == "SoundModuleNonShared";
            MaxDistance = config.GetFloat("MaxDistance", 100.0f);
        }

        public void AddRegion(Scene scene) { }

        public void RemoveRegion(Scene scene)
        {
            m_scene.EventManager.OnClientLogin -= OnNewClient;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            m_scene = scene;
            m_scene.EventManager.OnClientLogin += OnNewClient;

            m_scene.RegisterModuleInterface<ISoundModule>(this);
        }

        public void Close() { }

        public Type ReplaceableInterface
        {
            get { return typeof(ISoundModule); }
        }

        public string Name { get { return "Sound Module"; } }

        #endregion

        #region Event Handlers

        private void OnNewClient(IClientAPI client)
        {
            client.OnSoundTrigger += TriggerSound;
        }

        #endregion

        #region ISoundModule

        public virtual void PlayAttachedSound(
            UUID soundID, UUID ownerID, UUID objectID, double gain, Vector3 position, byte flags, float radius)
        {
            SceneObjectPart part;
            if (!m_scene.TryGetSceneObjectPart(objectID, out part))
                return;

            SceneObjectGroup grp = part.ParentGroup;

            if (radius == 0)
                radius = MaxDistance;

            m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                double dis = Util.GetDistanceTo(sp.AbsolutePosition, position);
                if (dis > MaxDistance) // Max audio distance
                    return;

                if (grp.IsAttachment)
                {
                    if (grp.HasPrivateAttachmentPoint && sp.ControllingClient.AgentId != grp.OwnerID)
                        return;

                    if (sp.ControllingClient.AgentId == grp.OwnerID)
                        dis = 0;
                }

                // Scale by distance
                double thisSpGain = gain * ((radius - dis) / radius);

                sp.ControllingClient.SendPlayAttachedSound(soundID, objectID,
                        ownerID, (float)thisSpGain, flags);
            });
        }

        public virtual void TriggerSound(
            UUID soundId, UUID ownerID, UUID objectID, UUID parentID, double gain, Vector3 position, UInt64 handle, float radius)
        {
            SceneObjectPart part;
            if (!m_scene.TryGetSceneObjectPart(objectID, out part))
            {
                ScenePresence sp;
                if (!m_scene.TryGetScenePresence(ownerID, out sp))
                    return;
            }
            else
            {
                SceneObjectGroup grp = part.ParentGroup;

                if (grp.IsAttachment && grp.AttachmentPoint > 30)
                {
                    objectID = ownerID;
                    parentID = ownerID;
                }
            }

            if (radius == 0)
                radius = MaxDistance;

            m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                double dis = Util.GetDistanceTo(sp.AbsolutePosition, position);

                if (dis > MaxDistance) // Max audio distance
                    return;

                // Scale by distance
                double thisSpGain = gain * ((radius - dis) / radius);

                sp.ControllingClient.SendTriggeredSound(soundId, ownerID,
                        objectID, parentID, handle, position,
                        (float)thisSpGain);
            });
        }

        public virtual void StopSound(UUID objectID)
        {
            SceneObjectPart m_host;
            if (!m_scene.TryGetSceneObjectPart(objectID, out m_host))
                return;

            m_host.AdjustSoundGain(0);
            // Xantor 20080528: Clear prim data of sound instead
            if (m_host.ParentGroup.LoopSoundSlavePrims.Contains(m_host))
            {
                if (m_host.ParentGroup.LoopSoundMasterPrim == m_host)
                {
                    foreach (SceneObjectPart part in m_host.ParentGroup.LoopSoundSlavePrims)
                    {
                        part.Sound = UUID.Zero;
                        part.SoundGain = 0;
                        part.SoundFlags = 0;
                        part.SoundRadius = 0;
                        part.ScheduleFullUpdate();
                        part.SendFullUpdateToAllClients();
                    }
                    m_host.ParentGroup.LoopSoundMasterPrim = null;
                    m_host.ParentGroup.LoopSoundSlavePrims.Clear();
                }
                else
                {
                    m_host.Sound = UUID.Zero;
                    m_host.SoundGain = 0;
                    m_host.SoundFlags = 0;
                    m_host.SoundRadius = 0;
                    m_host.ScheduleFullUpdate();
                    m_host.SendFullUpdateToAllClients();
                }
            }
            else
            {
                m_host.Sound = UUID.Zero;
                m_host.SoundGain = 0;
                m_host.SoundFlags = 0;
                m_host.SoundRadius = 0;
                m_host.ScheduleFullUpdate();
                m_host.SendFullUpdateToAllClients();
            }
        }

        public virtual void PreloadSound(UUID objectID, UUID soundID, float radius)
        {
            SceneObjectPart part;
            if (soundID == UUID.Zero
                    || !m_scene.TryGetSceneObjectPart(objectID, out part))
            {
                return;
            }

            if (radius == 0)
                radius = MaxDistance;

            m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                if (!(Util.GetDistanceTo(sp.AbsolutePosition, part.AbsolutePosition) >= MaxDistance))
                    sp.ControllingClient.SendPreLoadSound(objectID, objectID, soundID);
            });
        }

        public virtual void LoopSoundMaster(UUID objectID, UUID soundID,
                double volume, double radius)
        {
            SceneObjectPart m_host;
            if (!m_scene.TryGetSceneObjectPart(objectID, out m_host))
                return;

            m_host.ParentGroup.LoopSoundMasterPrim = m_host;

            if (m_host.Sound != UUID.Zero)
                StopSound(objectID);

            m_host.Sound = soundID;
            m_host.SoundGain = volume;
            m_host.SoundFlags = 1;      // looping
            m_host.SoundRadius = radius;

            m_host.ScheduleFullUpdate();
            m_host.SendFullUpdateToAllClients();
        }

        #endregion
    }
}
