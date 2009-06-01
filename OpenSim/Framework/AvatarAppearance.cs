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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AvatarAppearance
    {
//        private static readonly ILog m_log
//            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        private static UUID BODY_ASSET = new UUID("66c41e39-38f9-f75a-024e-585989bfab73");
        private static UUID BODY_ITEM = new UUID("66c41e39-38f9-f75a-024e-585989bfaba9");
        private static UUID SKIN_ASSET = new UUID("77c41e39-38f9-f75a-024e-585989bbabbb");
        private static UUID SKIN_ITEM = new UUID("77c41e39-38f9-f75a-024e-585989bfabc9");
        private static UUID SHIRT_ASSET = new UUID("00000000-38f9-1111-024e-222222111110");
        private static UUID SHIRT_ITEM = new UUID("77c41e39-38f9-f75a-0000-585989bf0000");
        private static UUID PANTS_ASSET = new UUID("00000000-38f9-1111-024e-222222111120");
        private static UUID PANTS_ITEM = new UUID("77c41e39-38f9-f75a-0000-5859892f1111");

        public readonly static int VISUALPARAM_COUNT = 218;

        protected UUID m_owner;

        public virtual UUID Owner
        {
            get { return m_owner; }
            set { m_owner = value; }
        }
        protected int m_serial = 1;

        public virtual int Serial
        {
            get { return m_serial; }
            set { m_serial = value; }
        }

        protected byte[] m_visualparams;

        public virtual byte[] VisualParams
        {
            get { return m_visualparams; }
            set { m_visualparams = value; }
        }

        protected AvatarWearable[] m_wearables;

        public virtual AvatarWearable[] Wearables
        {
            get { return m_wearables; }
            set { m_wearables = value; }
        }

        public virtual UUID BodyItem {
            get { return m_wearables[BODY].ItemID; }
            set { m_wearables[BODY].ItemID = value; }
        }

        public virtual UUID BodyAsset {
            get { return m_wearables[BODY].AssetID; }
            set { m_wearables[BODY].AssetID = value; }
        }

        public virtual UUID SkinItem {
            get { return m_wearables[SKIN].ItemID; }
            set { m_wearables[SKIN].ItemID = value; }
        }

        public virtual UUID SkinAsset {
            get { return m_wearables[SKIN].AssetID; }
            set { m_wearables[SKIN].AssetID = value; }
        }

        public virtual UUID HairItem {
            get { return m_wearables[HAIR].ItemID; }
            set { m_wearables[HAIR].ItemID = value; }
        }

        public virtual UUID HairAsset {
            get { return m_wearables[HAIR].AssetID; }
            set { m_wearables[HAIR].AssetID = value; }
        }

        public virtual UUID EyesItem {
            get { return m_wearables[EYES].ItemID; }
            set { m_wearables[EYES].ItemID = value; }
        }

        public virtual UUID EyesAsset {
            get { return m_wearables[EYES].AssetID; }
            set { m_wearables[EYES].AssetID = value; }
        }

        public virtual UUID ShirtItem {
            get { return m_wearables[SHIRT].ItemID; }
            set { m_wearables[SHIRT].ItemID = value; }
        }

        public virtual UUID ShirtAsset {
            get { return m_wearables[SHIRT].AssetID; }
            set { m_wearables[SHIRT].AssetID = value; }
        }

        public virtual UUID PantsItem {
            get { return m_wearables[PANTS].ItemID; }
            set { m_wearables[PANTS].ItemID = value; }
        }

        public virtual UUID PantsAsset {
            get { return m_wearables[PANTS].AssetID; }
            set { m_wearables[PANTS].AssetID = value; }
        }

        public virtual UUID ShoesItem {
            get { return m_wearables[SHOES].ItemID; }
            set { m_wearables[SHOES].ItemID = value; }
        }

        public virtual UUID ShoesAsset {
            get { return m_wearables[SHOES].AssetID; }
            set { m_wearables[SHOES].AssetID = value; }
        }

        public virtual UUID SocksItem {
            get { return m_wearables[SOCKS].ItemID; }
            set { m_wearables[SOCKS].ItemID = value; }
        }

        public virtual UUID SocksAsset {
            get { return m_wearables[SOCKS].AssetID; }
            set { m_wearables[SOCKS].AssetID = value; }
        }

        public virtual UUID JacketItem {
            get { return m_wearables[JACKET].ItemID; }
            set { m_wearables[JACKET].ItemID = value; }
        }

        public virtual UUID JacketAsset {
            get { return m_wearables[JACKET].AssetID; }
            set { m_wearables[JACKET].AssetID = value; }
        }

        public virtual UUID GlovesItem {
            get { return m_wearables[GLOVES].ItemID; }
            set { m_wearables[GLOVES].ItemID = value; }
        }

        public virtual UUID GlovesAsset {
            get { return m_wearables[GLOVES].AssetID; }
            set { m_wearables[GLOVES].AssetID = value; }
        }

        public virtual UUID UnderShirtItem {
            get { return m_wearables[UNDERSHIRT].ItemID; }
            set { m_wearables[UNDERSHIRT].ItemID = value; }
        }

        public virtual UUID UnderShirtAsset {
            get { return m_wearables[UNDERSHIRT].AssetID; }
            set { m_wearables[UNDERSHIRT].AssetID = value; }
        }

        public virtual UUID UnderPantsItem {
            get { return m_wearables[UNDERPANTS].ItemID; }
            set { m_wearables[UNDERPANTS].ItemID = value; }
        }

        public virtual UUID UnderPantsAsset {
            get { return m_wearables[UNDERPANTS].AssetID; }
            set { m_wearables[UNDERPANTS].AssetID = value; }
        }

        public virtual UUID SkirtItem {
            get { return m_wearables[SKIRT].ItemID; }
            set { m_wearables[SKIRT].ItemID = value; }
        }

        public virtual UUID SkirtAsset {
            get { return m_wearables[SKIRT].AssetID; }
            set { m_wearables[SKIRT].AssetID = value; }
        }

        public virtual void SetDefaultWearables()
        {
            m_wearables[BODY].AssetID = BODY_ASSET;
            m_wearables[BODY].ItemID = BODY_ITEM;
            m_wearables[SKIN].AssetID = SKIN_ASSET;
            m_wearables[SKIN].ItemID = SKIN_ITEM;
            m_wearables[SHIRT].AssetID = SHIRT_ASSET;
            m_wearables[SHIRT].ItemID = SHIRT_ITEM;
            m_wearables[PANTS].AssetID = PANTS_ASSET;
            m_wearables[PANTS].ItemID = PANTS_ITEM;
        }

        public virtual void ClearWearables()
        {
            for (int i = 0; i < 13; i++)
            {
                m_wearables[i].AssetID = UUID.Zero;
                m_wearables[i].ItemID = UUID.Zero;
            }
        }

        public virtual void SetDefaultParams(byte[] vparams)
        {
            // TODO: Figure out better values then 'fat scientist 150' or 'alien 0'
            for (int i = 0; i < VISUALPARAM_COUNT; i++)
            {
                vparams[i] = 150;
            }
        }

        protected Primitive.TextureEntry m_texture;

        public virtual Primitive.TextureEntry Texture
        {
            get { return m_texture; }
            set { m_texture = value; }
        }

        protected float m_avatarHeight = 0;
        protected float m_hipOffset = 0;

        public virtual float AvatarHeight
        {
            get { return m_avatarHeight; }
            set { m_avatarHeight = value; }
        }

        public virtual float HipOffset
        {
            get { return m_hipOffset; }
        }

        public AvatarAppearance()
            : this(UUID.Zero)
        {
        }

        public AvatarAppearance(UUID owner)
        {
            m_wearables = new AvatarWearable[MAX_WEARABLES];
            for (int i = 0; i < MAX_WEARABLES; i++)
            {
                // this makes them all null
                m_wearables[i] = new AvatarWearable();
            }
            m_serial = 0;
            m_owner = owner;
            m_visualparams = new byte[VISUALPARAM_COUNT];
            // This sets Visual Params with *less* weirder values then default. Instead of a ugly alien, it looks like a fat scientist
            SetDefaultParams(m_visualparams);
            SetDefaultWearables();
            m_texture = GetDefaultTexture();
        }

        public AvatarAppearance(UUID avatarID, AvatarWearable[] wearables, byte[] visualParams)
        {
            m_owner = avatarID;
            m_serial = 1;
            m_wearables = wearables;
            m_visualparams = visualParams;
            m_texture = GetDefaultTexture();
        }

        /// <summary>
        /// Set up appearance textures and avatar parameters, including a height calculation
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public virtual void SetAppearance(byte[] texture, List<byte> visualParam)
        {
            Primitive.TextureEntry textureEnt = new Primitive.TextureEntry(texture, 0, texture.Length);
            m_texture = textureEnt;
            m_visualparams = visualParam.ToArray();
            m_avatarHeight = 1.23077f  // Shortest possible avatar height
                           + 0.516945f * (float)m_visualparams[25] / 255.0f   // Body height
                           + 0.072514f * (float)m_visualparams[120] / 255.0f  // Head size
                           + 0.3836f * (float)m_visualparams[125] / 255.0f    // Leg length
                           + 0.08f * (float)m_visualparams[77] / 255.0f    // Shoe heel height
                           + 0.07f * (float)m_visualparams[78] / 255.0f    // Shoe platform height
                           + 0.076f * (float)m_visualparams[148] / 255.0f;    // Neck length
            m_hipOffset = (0.615385f // Half of avatar
                           + 0.08f * (float)m_visualparams[77] / 255.0f    // Shoe heel height
                           + 0.07f * (float)m_visualparams[78] / 255.0f    // Shoe platform height
                           + 0.3836f * (float)m_visualparams[125] / 255.0f    // Leg length
                           - m_avatarHeight / 2) * 0.3f - 0.04f;
            //System.Console.WriteLine(">>>>>>> [APPEARANCE]: Height {0} Hip offset {1}" + m_avatarHeight + " " + m_hipOffset);
            //m_log.Debug("------------- Set Appearance Texture ---------------");
            //Primitive.TextureEntryFace[] faces = Texture.FaceTextures;
            //foreach (Primitive.TextureEntryFace face in faces)
            //{
            //    if (face != null)
            //        m_log.Debug("  ++ " + face.TextureID);
            //    else
            //        m_log.Debug("  ++ NULL ");
            //}
            //m_log.Debug("----------------------------");

        }

        public virtual void SetWearable(int wearableId, AvatarWearable wearable)
        {
            m_wearables[wearableId] = wearable;
        }

        public static Primitive.TextureEntry GetDefaultTexture()
        {
            Primitive.TextureEntry textu = new Primitive.TextureEntry(new UUID("C228D1CF-4B5D-4BA8-84F4-899A0796AA97"));
            textu.CreateFace(0).TextureID = new UUID("00000000-0000-1111-9999-000000000012");
            textu.CreateFace(1).TextureID = Util.BLANK_TEXTURE_UUID;
            textu.CreateFace(2).TextureID = Util.BLANK_TEXTURE_UUID;
            textu.CreateFace(3).TextureID = new UUID("6522E74D-1660-4E7F-B601-6F48C1659A77");
            textu.CreateFace(4).TextureID = new UUID("7CA39B4C-BD19-4699-AFF7-F93FD03D3E7B");
            textu.CreateFace(5).TextureID = new UUID("00000000-0000-1111-9999-000000000010");
            textu.CreateFace(6).TextureID = new UUID("00000000-0000-1111-9999-000000000011");
            return textu;
        }

        public static byte[] GetDefaultVisualParams()
        {
            byte[] visualParams;
            visualParams = new byte[VISUALPARAM_COUNT];
            for (int i = 0; i < VISUALPARAM_COUNT; i++)
            {
                visualParams[i] = 100;
            }
            return visualParams;
        }

        public override String ToString()
        {
            String s = "[Wearables] =>";
            s += " Body Item: " + BodyItem.ToString() + ";";
            s += " Skin Item: " + SkinItem.ToString() + ";";
            s += " Shirt Item: " + ShirtItem.ToString() + ";";
            s += " Pants Item: " + PantsItem.ToString() + ";";
            return s;
        }

        // this is used for OGS1
        public virtual Hashtable ToHashTable()
        {
            Hashtable h = new Hashtable();
            h["owner"] = Owner.ToString();
            h["serial"] = Serial.ToString();
            h["visual_params"] = VisualParams;
            h["texture"] = Texture.GetBytes();
            h["avatar_height"] = AvatarHeight.ToString();
            h["body_item"] = BodyItem.ToString();
            h["body_asset"] = BodyAsset.ToString();
            h["skin_item"] = SkinItem.ToString();
            h["skin_asset"] = SkinAsset.ToString();
            h["hair_item"] = HairItem.ToString();
            h["hair_asset"] = HairAsset.ToString();
            h["eyes_item"] = EyesItem.ToString();
            h["eyes_asset"] = EyesAsset.ToString();
            h["shirt_item"] = ShirtItem.ToString();
            h["shirt_asset"] = ShirtAsset.ToString();
            h["pants_item"] = PantsItem.ToString();
            h["pants_asset"] = PantsAsset.ToString();
            h["shoes_item"] = ShoesItem.ToString();
            h["shoes_asset"] = ShoesAsset.ToString();
            h["socks_item"] = SocksItem.ToString();
            h["socks_asset"] = SocksAsset.ToString();
            h["jacket_item"] = JacketItem.ToString();
            h["jacket_asset"] = JacketAsset.ToString();
            h["gloves_item"] = GlovesItem.ToString();
            h["gloves_asset"] = GlovesAsset.ToString();
            h["undershirt_item"] = UnderShirtItem.ToString();
            h["undershirt_asset"] = UnderShirtAsset.ToString();
            h["underpants_item"] = UnderPantsItem.ToString();
            h["underpants_asset"] = UnderPantsAsset.ToString();
            h["skirt_item"] = SkirtItem.ToString();
            h["skirt_asset"] = SkirtAsset.ToString();

            string attachments = GetAttachmentsString();
            if (attachments != String.Empty)
                h["attachments"] = attachments;

            return h;
        }

        public AvatarAppearance(Hashtable h)
        {
            Owner = new UUID((string)h["owner"]);
            Serial = Convert.ToInt32((string)h["serial"]);
            VisualParams = (byte[])h["visual_params"];
            Texture = new Primitive.TextureEntry((byte[])h["texture"], 0, ((byte[])h["texture"]).Length);
            AvatarHeight = (float)Convert.ToDouble((string)h["avatar_height"]);

            m_wearables = new AvatarWearable[MAX_WEARABLES];
            for (int i = 0; i < MAX_WEARABLES; i++)
            {
                // this makes them all null
                m_wearables[i] = new AvatarWearable();
            }

            BodyItem = new UUID((string)h["body_item"]);
            BodyAsset = new UUID((string)h["body_asset"]);
            SkinItem = new UUID((string)h["skin_item"]);
            SkinAsset = new UUID((string)h["skin_asset"]);
            HairItem = new UUID((string)h["hair_item"]);
            HairAsset = new UUID((string)h["hair_asset"]);
            EyesItem = new UUID((string)h["eyes_item"]);
            EyesAsset = new UUID((string)h["eyes_asset"]);
            ShirtItem = new UUID((string)h["shirt_item"]);
            ShirtAsset = new UUID((string)h["shirt_asset"]);
            PantsItem = new UUID((string)h["pants_item"]);
            PantsAsset = new UUID((string)h["pants_asset"]);
            ShoesItem = new UUID((string)h["shoes_item"]);
            ShoesAsset = new UUID((string)h["shoes_asset"]);
            SocksItem = new UUID((string)h["socks_item"]);
            SocksAsset = new UUID((string)h["socks_asset"]);
            JacketItem = new UUID((string)h["jacket_item"]);
            JacketAsset = new UUID((string)h["jacket_asset"]);
            GlovesItem = new UUID((string)h["gloves_item"]);
            GlovesAsset = new UUID((string)h["gloves_asset"]);
            UnderShirtItem = new UUID((string)h["undershirt_item"]);
            UnderShirtAsset = new UUID((string)h["undershirt_asset"]);
            UnderPantsItem = new UUID((string)h["underpants_item"]);
            UnderPantsAsset = new UUID((string)h["underpants_asset"]);
            SkirtItem = new UUID((string)h["skirt_item"]);
            SkirtAsset = new UUID((string)h["skirt_asset"]);

            if (h.ContainsKey("attachments"))
            {
                SetAttachmentsString(h["attachments"].ToString());
            }
        }

        private Dictionary<int, UUID[]> m_attachments = new Dictionary<int, UUID[]>();

        public void SetAttachments(Hashtable data)
        {
            m_attachments.Clear();

            if (data == null)
                return;

            foreach (DictionaryEntry e in data)
            {
                int attachpoint = Convert.ToInt32(e.Key);

                if (m_attachments.ContainsKey(attachpoint))
                    continue;

                UUID item;
                UUID asset;

                Hashtable uuids = (Hashtable) e.Value;
                UUID.TryParse(uuids["item"].ToString(), out item);
                UUID.TryParse(uuids["asset"].ToString(), out asset);

                UUID[] attachment = new UUID[2];
                attachment[0] = item;
                attachment[1] = asset;

                m_attachments[attachpoint] = attachment;
            }
        }

        public Hashtable GetAttachments()
        {
            if (m_attachments.Count == 0)
                return null;

            Hashtable ret = new Hashtable();

            foreach (KeyValuePair<int, UUID[]> kvp in m_attachments)
            {
                int attachpoint = kvp.Key;
                UUID[] uuids = kvp.Value;

                Hashtable data = new Hashtable();
                data["item"] = uuids[0].ToString();
                data["asset"] = uuids[1].ToString();

                ret[attachpoint] = data;
            }

            return ret;
        }

        public List<int> GetAttachedPoints()
        {
            return new List<int>(m_attachments.Keys);
        }

        public UUID GetAttachedItem(int attachpoint)
        {
            if (!m_attachments.ContainsKey(attachpoint))
                return UUID.Zero;

            return m_attachments[attachpoint][0];
        }

        public UUID GetAttachedAsset(int attachpoint)
        {
            if (!m_attachments.ContainsKey(attachpoint))
                return UUID.Zero;

            return m_attachments[attachpoint][1];
        }

        public void SetAttachment(int attachpoint, UUID item, UUID asset)
        {
            if (attachpoint == 0)
                return;

            if (item == UUID.Zero)
            {
                if (m_attachments.ContainsKey(attachpoint))
                    m_attachments.Remove(attachpoint);
                return;
            }

            if (!m_attachments.ContainsKey(attachpoint))
                m_attachments[attachpoint] = new UUID[2];

            m_attachments[attachpoint][0] = item;
            m_attachments[attachpoint][1] = asset;
        }

        public int GetAttachpoint(UUID itemID)
        {
            foreach (KeyValuePair<int, UUID[]> kvp in m_attachments)
            {
                if (kvp.Value[0] == itemID)
                {
                    return kvp.Key;
                }
            }
            return 0;
        }

        public void DetachAttachment(UUID itemID)
        {
            int attachpoint = GetAttachpoint(itemID);

            if (attachpoint > 0)
                m_attachments.Remove(attachpoint);
        }

        public void ClearAttachments()
        {
            m_attachments.Clear();
        }

        string GetAttachmentsString()
        {
            List<string> strings = new List<string>();

            foreach (KeyValuePair<int, UUID[]> e in m_attachments)
            {
                strings.Add(e.Key.ToString());
                strings.Add(e.Value[0].ToString());
                strings.Add(e.Value[1].ToString());
            }

            return String.Join(",", strings.ToArray());
        }

        void SetAttachmentsString(string data)
        {
            string[] strings = data.Split(new char[] {','});
            int i = 0;

            m_attachments.Clear();

            while (strings.Length - i > 2)
            {
                int attachpoint = Int32.Parse(strings[i]);
                UUID item = new UUID(strings[i+1]);
                UUID asset = new UUID(strings[i+2]);
                i += 3;

                if (!m_attachments.ContainsKey(attachpoint))
                {
                    m_attachments[attachpoint] = new UUID[2];
                    m_attachments[attachpoint][0] = item;
                    m_attachments[attachpoint][1] = asset;
                }
            }
        }
    }
}
