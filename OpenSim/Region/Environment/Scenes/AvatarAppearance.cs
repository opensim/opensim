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
using System.Runtime.Serialization;
using System.Security.Permissions;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Scenes
{
    [Serializable]
    public class AvatarAppearance : ISerializable
    {
        // these are guessed at by the list here -
        // http://wiki.secondlife.com/wiki/Avatar_Appearance.  We'll
        // correct them over time for when were are wrong.
        public readonly static int BODY = 0;
        public readonly static int SKIN = 1;
        public readonly static int HAIR = 2;
        public readonly static int EYES = 3;
        public readonly static int SHIRT = 4;
        public readonly static int PANTS = 5;
        public readonly static int SHOES = 6;
        public readonly static int SOCKS = 7;
        public readonly static int JACKET = 8;
        public readonly static int GLOVES = 9;
        public readonly static int UNDERSHIRT = 10;
        public readonly static int UNDERPANTS = 11;
        public readonly static int SKIRT = 12;

        private readonly static int MAX_WEARABLES = 13;
 
        private static LLUUID BODY_ASSET = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
        private static LLUUID BODY_ITEM = new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9");
        private static LLUUID SKIN_ASSET = new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb");
        private static LLUUID SKIN_ITEM = new LLUUID("77c41e39-38f9-f75a-024e-585989bfabc9");
        private static LLUUID SHIRT_ASSET = new LLUUID("00000000-38f9-1111-024e-222222111110");
        private static LLUUID SHIRT_ITEM = new LLUUID("77c41e39-38f9-f75a-0000-585989bf0000");
        private static LLUUID PANTS_ASSET = new LLUUID("00000000-38f9-1111-024e-222222111120");
        private static LLUUID PANTS_ITEM = new LLUUID("77c41e39-38f9-f75a-0000-5859892f1111");

        public readonly static int VISUALPARAM_COUNT = 218;

        protected LLUUID m_scenePresenceID;

        public LLUUID ScenePresenceID
        {
            get { return m_scenePresenceID; }
            set { m_scenePresenceID = value; }
        }
        protected int m_wearablesSerial = 1;

        public int WearablesSerial
        {
            get { return m_wearablesSerial; }
            set { m_wearablesSerial = value; }
        }

        protected byte[] m_visualParams;

        public byte[] VisualParams
        {
            get { return m_visualParams; }
            set { m_visualParams = value; }
        }

        protected AvatarWearable[] m_wearables;

        public AvatarWearable[] Wearables
        {
            get { return m_wearables; }
            set { m_wearables = value; }
        }

        protected LLObject.TextureEntry m_textureEntry;

        public LLObject.TextureEntry TextureEntry
        {
            get { return m_textureEntry; }
            set { m_textureEntry = value; }
        }

        protected float m_avatarHeight = 0;

        public float AvatarHeight
        {
            get { return m_avatarHeight; }
            set { m_avatarHeight = value; }
        }

        public AvatarAppearance()
        {
            m_wearables = new AvatarWearable[MAX_WEARABLES];
            for (int i = 0; i < MAX_WEARABLES; i++)
            {
                // this makes them all null
                m_wearables[i] = new AvatarWearable();
            }
            m_wearablesSerial = 0;
            m_scenePresenceID = LLUUID.Zero;
            m_visualParams = new byte[VISUALPARAM_COUNT];
        }

        public AvatarAppearance(LLUUID avatarID, AvatarWearable[] wearables, byte[] visualParams)
        {
            m_scenePresenceID = avatarID;
            m_wearablesSerial = 1;
            m_wearables = wearables;
            m_visualParams = visualParams;
            m_textureEntry = GetDefaultTextureEntry();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(byte[] texture, List<byte> visualParam)
        {
            LLObject.TextureEntry textureEnt = new LLObject.TextureEntry(texture, 0, texture.Length);
            m_textureEntry = textureEnt;

            m_visualParams = visualParam.ToArray();

            // Teravus : Nifty AV Height Getting Maaaaagical formula.  Oh how we love turning 0-255 into meters.
            // (float)m_visualParams[25] = Height
            // (float)m_visualParams[125] = LegLength
            m_avatarHeight = (1.50856f + (((float) m_visualParams[25]/255.0f)*(2.525506f - 1.50856f)))
                             + (((float) m_visualParams[125]/255.0f)/1.5f);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatar"></param>
        public void SendAppearanceToOtherAgent(ScenePresence avatar)
        {
            avatar.ControllingClient.SendAppearance(m_scenePresenceID, m_visualParams,
                                                    m_textureEntry.ToBytes());
        }

        public void SetWearable(IClientAPI client, int wearableId, AvatarWearable wearable)
        {
            m_wearables[wearableId] = wearable;
            SendOwnWearables(client);
        }

        public void SendOwnWearables(IClientAPI ourClient)
        {
            ourClient.SendWearables(m_wearables, m_wearablesSerial++);
        }

        public static LLObject.TextureEntry GetDefaultTextureEntry()
        {
            LLObject.TextureEntry textu = new LLObject.TextureEntry(new LLUUID("C228D1CF-4B5D-4BA8-84F4-899A0796AA97"));
            textu.CreateFace(0).TextureID = new LLUUID("00000000-0000-1111-9999-000000000012");
            textu.CreateFace(1).TextureID = new LLUUID("5748decc-f629-461c-9a36-a35a221fe21f");
            textu.CreateFace(2).TextureID = new LLUUID("5748decc-f629-461c-9a36-a35a221fe21f");
            textu.CreateFace(3).TextureID = new LLUUID("6522E74D-1660-4E7F-B601-6F48C1659A77");
            textu.CreateFace(4).TextureID = new LLUUID("7CA39B4C-BD19-4699-AFF7-F93FD03D3E7B");
            textu.CreateFace(5).TextureID = new LLUUID("00000000-0000-1111-9999-000000000010");
            textu.CreateFace(6).TextureID = new LLUUID("00000000-0000-1111-9999-000000000011");
            return textu;
        }

        protected AvatarAppearance(SerializationInfo info, StreamingContext context)
        {
            //System.Console.WriteLine("AvatarAppearance Deserialize BGN");

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            m_scenePresenceID = new LLUUID((Guid)info.GetValue("m_scenePresenceID", typeof(Guid)));
            m_wearablesSerial = (int)info.GetValue("m_wearablesSerial", typeof(int));
            m_visualParams = (byte[])info.GetValue("m_visualParams", typeof(byte[]));
            m_wearables = (AvatarWearable[])info.GetValue("m_wearables", typeof(AvatarWearable[]));

            byte[] m_textureEntry_work = (byte[])info.GetValue("m_textureEntry", typeof(byte[]));
            m_textureEntry = new LLObject.TextureEntry(m_textureEntry_work, 0, m_textureEntry_work.Length);

            m_avatarHeight = (float)info.GetValue("m_avatarHeight", typeof(float));

            //System.Console.WriteLine("AvatarAppearance Deserialize END");
        }

        [SecurityPermission(SecurityAction.LinkDemand,
            Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(
                        SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("m_scenePresenceID", m_scenePresenceID.UUID);
            info.AddValue("m_wearablesSerial", m_wearablesSerial);
            info.AddValue("m_visualParams", m_visualParams);
            info.AddValue("m_wearables", m_wearables);
            info.AddValue("m_textureEntry", m_textureEntry.ToBytes());
            info.AddValue("m_avatarHeight", m_avatarHeight);
        }
    }
}
