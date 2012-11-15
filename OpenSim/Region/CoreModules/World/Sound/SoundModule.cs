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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Reflection;
using log4net;

namespace OpenSim.Region.CoreModules.World.Sound
{
    public class SoundModule : IRegionModule, ISoundModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Scene m_scene;
        
        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            
            m_scene.EventManager.OnNewClient += OnNewClient;
            
            m_scene.RegisterModuleInterface<ISoundModule>(this);
        }
        
        public void PostInitialise() {}
        public void Close() {}
        public string Name { get { return "Sound Module"; } }
        public bool IsSharedModule { get { return false; } }
        
        private void OnNewClient(IClientAPI client)
        {
            client.OnSoundTrigger += TriggerSound;
        }
        
        public virtual void PlayAttachedSound(
            UUID soundID, UUID ownerID, UUID objectID, double gain, Vector3 position, byte flags, float radius)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
            if (part == null)
                return;

            SceneObjectGroup grp = part.ParentGroup;

            m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                double dis = Util.GetDistanceTo(sp.AbsolutePosition, position);
                if (dis > 100.0) // Max audio distance
                    return;

                if (grp.IsAttachment)
                {
                    if (grp.HasPrivateAttachmentPoint && sp.ControllingClient.AgentId != grp.OwnerID)
                        return;

                    if (sp.ControllingClient.AgentId == grp.OwnerID)
                        dis = 0;
                }

                // Scale by distance
                if (radius == 0)
                    gain = (float)((double)gain * ((100.0 - dis) / 100.0));
                else
                    gain = (float)((double)gain * ((radius - dis) / radius));

                sp.ControllingClient.SendPlayAttachedSound(soundID, objectID, ownerID, (float)gain, flags);
            });
        }
        
        public virtual void TriggerSound(
            UUID soundId, UUID ownerID, UUID objectID, UUID parentID, double gain, Vector3 position, UInt64 handle, float radius)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
            if (part == null)
            {
                ScenePresence sp;
                if (!m_scene.TryGetScenePresence(objectID, out sp))
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

            m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
            {
                double dis = Util.GetDistanceTo(sp.AbsolutePosition, position);

                if (dis > 100.0) // Max audio distance
                    return;

                float thisSpGain;

                // Scale by distance
                if (radius == 0)
                    thisSpGain = (float)((double)gain * ((100.0 - dis) / 100.0));
                else
                    thisSpGain = (float)((double)gain * ((radius - dis) / radius));

                sp.ControllingClient.SendTriggeredSound(
                    soundId, ownerID, objectID, parentID, handle, position, thisSpGain);
            });
        }
    }
}
