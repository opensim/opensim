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

using libsecondlife;

namespace OpenSim.Framework
{
    /// <summary>
    /// Information about a particular user known to the userserver
    /// </summary>

    public class UserAppearance
    {
        // these are guessed at by the list here -
        // http://wiki.secondlife.com/wiki/Avatar_Appearance.  We'll
        // correct them over time for when were are wrong.
        public const int BODY = 0;
        public const int SKIN = 1;
        public const int HAIR = 2;
        public const int EYES = 3;
        public const int SHIRT = 4;
        public const int PANTS = 5;
        public const int SHOES = 6;
        public const int SOCKS = 7;
        public const int JACKET = 8;
        public const int GLOVES = 9;
        public const int UNDERSHIRT = 10;
        public const int UNDERPANTS = 11;
        public const int SKIRT = 12;

        private const int MAX_WEARABLES = 13;
 
        private static LLUUID BODY_ASSET = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
        private static LLUUID BODY_ITEM = new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9");
        private static LLUUID SKIN_ASSET = new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb");
        private static LLUUID SKIN_ITEM = new LLUUID("77c41e39-38f9-f75a-024e-585989bfabc9");
        private static LLUUID SHIRT_ASSET = new LLUUID("00000000-38f9-1111-024e-222222111110");
        private static LLUUID SHIRT_ITEM = new LLUUID("77c41e39-38f9-f75a-0000-585989bf0000");
        private static LLUUID PANTS_ASSET = new LLUUID("00000000-38f9-1111-024e-222222111120");
        private static LLUUID PANTS_ITEM = new LLUUID("77c41e39-38f9-f75a-0000-5859892f1111");

        private AvatarWearable[] wearables;

        public UserAppearance() 
        {
            wearables = new AvatarWearable[MAX_WEARABLES];
            for (int i = 0; i < MAX_WEARABLES; i++)
            {
                // this makes them all null
                wearables[i] = new AvatarWearable();
            }
        }

        public LLUUID BodyItem {
            get { return wearables[BODY].ItemID; }
            set { wearables[BODY].ItemID = value; }
        }
        public LLUUID BodyAsset {
            get { return wearables[BODY].AssetID; }
            set { wearables[BODY].AssetID = value; }
        }
        public LLUUID SkinItem {
            get { return wearables[SKIN].ItemID; }
            set { wearables[SKIN].ItemID = value; }
        }
        public LLUUID SkinAsset {
            get { return wearables[SKIN].AssetID; }
            set { wearables[SKIN].AssetID = value; }
        }
        public LLUUID HairItem {
            get { return wearables[HAIR].ItemID; }
            set { wearables[HAIR].ItemID = value; }
        }
        public LLUUID HairAsset {
            get { return wearables[HAIR].AssetID; }
            set { wearables[HAIR].AssetID = value; }
        }
        public LLUUID EyesItem {
            get { return wearables[EYES].ItemID; }
            set { wearables[EYES].ItemID = value; }
        }
        public LLUUID EyesAsset {
            get { return wearables[EYES].AssetID; }
            set { wearables[EYES].AssetID = value; }
        }
        public LLUUID ShirtItem {
            get { return wearables[SHIRT].ItemID; }
            set { wearables[SHIRT].ItemID = value; }
        }
        public LLUUID ShirtAsset {
            get { return wearables[SHIRT].AssetID; }
            set { wearables[SHIRT].AssetID = value; }
        }
        public LLUUID PantsItem {
            get { return wearables[PANTS].ItemID; }
            set { wearables[PANTS].ItemID = value; }
        }
        public LLUUID PantsAsset {
            get { return wearables[BODY].AssetID; }
            set { wearables[BODY].AssetID = value; }
        }
        public LLUUID ShoesItem {
            get { return wearables[SHOES].ItemID; }
            set { wearables[SHOES].ItemID = value; }
        }
        public LLUUID ShoesAsset {
            get { return wearables[SHOES].AssetID; }
            set { wearables[SHOES].AssetID = value; }
        }
        public LLUUID SocksItem {
            get { return wearables[SOCKS].ItemID; }
            set { wearables[SOCKS].ItemID = value; }
        }
        public LLUUID SocksAsset {
            get { return wearables[SOCKS].AssetID; }
            set { wearables[SOCKS].AssetID = value; }
        }
        public LLUUID JacketItem {
            get { return wearables[JACKET].ItemID; }
            set { wearables[JACKET].ItemID = value; }
        }
        public LLUUID JacketAsset {
            get { return wearables[JACKET].AssetID; }
            set { wearables[JACKET].AssetID = value; }
        }
        public LLUUID GlovesItem {
            get { return wearables[GLOVES].ItemID; }
            set { wearables[GLOVES].ItemID = value; }
        }
        public LLUUID GlovesAsset {
            get { return wearables[GLOVES].AssetID; }
            set { wearables[GLOVES].AssetID = value; }
        }
        public LLUUID UnderShirtItem {
            get { return wearables[UNDERSHIRT].ItemID; }
            set { wearables[UNDERSHIRT].ItemID = value; }
        }
        public LLUUID UnderShirtAsset {
            get { return wearables[UNDERSHIRT].AssetID; }
            set { wearables[UNDERSHIRT].AssetID = value; }
        }
        public LLUUID UnderPantsItem {
            get { return wearables[UNDERPANTS].ItemID; }
            set { wearables[UNDERPANTS].ItemID = value; }
        }
        public LLUUID UnderPantsAsset {
            get { return wearables[UNDERPANTS].AssetID; }
            set { wearables[UNDERPANTS].AssetID = value; }
        }
        public LLUUID SkirtItem {
            get { return wearables[SKIRT].ItemID; }
            set { wearables[SKIRT].ItemID = value; }
        }
        public LLUUID SkirtAsset {
            get { return wearables[SKIRT].AssetID; }
            set { wearables[SKIRT].AssetID = value; }
        }

        public void SetDefaultWearables() 
        {
            wearables[BODY].AssetID = BODY_ASSET;
            wearables[BODY].ItemID = BODY_ITEM;
            wearables[SKIN].AssetID = SKIN_ASSET;
            wearables[SKIN].ItemID = SKIN_ITEM;
            wearables[SHIRT].AssetID = SHIRT_ASSET;
            wearables[SHIRT].ItemID = SHIRT_ITEM;
            wearables[PANTS].AssetID = PANTS_ASSET;
            wearables[PANTS].ItemID = PANTS_ITEM;
        }
    }
}