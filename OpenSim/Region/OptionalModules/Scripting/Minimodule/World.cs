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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class World : IWorld 
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Scene m_internalScene;
        private readonly Heightmap m_heights;

        private readonly ObjectAccessor m_objs;

        public World(Scene internalScene)
        {
            m_internalScene = internalScene;
            m_heights = new Heightmap(m_internalScene);
            m_objs = new ObjectAccessor(m_internalScene);
        }

        #region Events

        #region OnChat
        private event OnChatDelegate _OnChat;
        private bool _OnChatActive;

        public event OnChatDelegate OnChat
        {
            add
            {
                if (!_OnChatActive)
                {
                    _OnChatActive = true;
                    m_internalScene.EventManager.OnChatFromClient += EventManager_OnChatFromClient;
                    m_internalScene.EventManager.OnChatFromWorld += EventManager_OnChatFromWorld;
                }

                _OnChat += value;
            }
            remove
            {
                _OnChat -= value;

                if (_OnChat == null)
                {
                    _OnChatActive = false;
                    m_internalScene.EventManager.OnChatFromClient -= EventManager_OnChatFromClient;
                    m_internalScene.EventManager.OnChatFromWorld -= EventManager_OnChatFromWorld;
                }
            }
        }

        void EventManager_OnChatFromWorld(object sender, OpenSim.Framework.OSChatMessage chat)
        {
            if (_OnChat != null)
            {
                HandleChatPacket(chat);
                return;
            }
        }

        private void HandleChatPacket(OSChatMessage chat)
        {
            // Object?
            if (chat.Sender == null && chat.SenderObject != null)
            {
                ChatEventArgs e = new ChatEventArgs();
                e.Sender = new SOPObject(m_internalScene, ((SceneObjectPart) chat.SenderObject).LocalId);
                e.Text = chat.Message;

                _OnChat(this, e);
                return;
            }
            // Avatar?
            if (chat.Sender != null && chat.SenderObject == null)
            {
                ChatEventArgs e = new ChatEventArgs();
                e.Sender = new SPAvatar(m_internalScene, chat.SenderUUID);
                e.Text = chat.Message;

                _OnChat(this, e);
                return;
            }
            // Skip if other
        }

        void EventManager_OnChatFromClient(object sender, OpenSim.Framework.OSChatMessage chat)
        {
            if (_OnChat != null)
            {
                HandleChatPacket(chat);
                return;
            }
        }
        #endregion

        #endregion

        public IObjectAccessor Objects
        {
            get { return m_objs; }
        }

        public IParcel[] Parcels
        {
            get
            {
                List<ILandObject> m_los = m_internalScene.LandChannel.AllParcels();
                List<IParcel> m_parcels = new List<IParcel>(m_los.Count);

                foreach (ILandObject landObject in m_los)
                {
                    m_parcels.Add(new LOParcel(m_internalScene, landObject.landData.LocalID));
                }

                return m_parcels.ToArray();
            }
        }


        public IAvatar[] Avatars
        {
            get
            {
                List<EntityBase> ents = m_internalScene.Entities.GetAllByType<ScenePresence>();
                IAvatar[] rets = new IAvatar[ents.Count];

                for (int i = 0; i < ents.Count; i++)
                {
                    EntityBase ent = ents[i];
                    rets[i] = new SPAvatar(m_internalScene, ent.UUID);
                }

                return rets;
            }
        }

        public IHeightmap Terrain
        {
            get { return m_heights; }
        }
    }
}
