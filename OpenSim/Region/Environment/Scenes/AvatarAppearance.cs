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
* 
*/

using System;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using System.Runtime.Serialization;
using System.Security.Permissions;
namespace OpenSim.Region.Environment.Scenes
{
    [Serializable]
    public class AvatarAppearance : ISerializable
    {
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
        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
            LLObject.TextureEntry textureEnt = new LLObject.TextureEntry(texture, 0, texture.Length);
            m_textureEntry = textureEnt;

            for (int i = 0; i < visualParam.Length; i++)
            {
                m_visualParams[i] = visualParam[i].ParamValue;
            }

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
                throw new System.ArgumentNullException("info");
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
                throw new System.ArgumentNullException("info");
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
