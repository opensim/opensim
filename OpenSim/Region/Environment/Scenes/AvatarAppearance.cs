using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Types;

namespace OpenSim.Region.Environment.Scenes
{
    public class AvatarAppearance
    {
        protected LLUUID m_scenePresenceID;
        protected int m_wearablesSerial = 1;

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
           m_avatarHeight = (1.50856f + (((float)m_visualParams[25] / 255.0f) * (2.525506f - 1.50856f)))
               + (((float)m_visualParams[125] / 255.0f) / 1.5f);
           
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
    }
}
