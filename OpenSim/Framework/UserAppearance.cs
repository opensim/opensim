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

        private AvatarWearable[] _wearables;
        private LLUUID _user;
        private int _serial;
        
        public UserAppearance() 
        {
            _wearables = new AvatarWearable[MAX_WEARABLES];
            for (int i = 0; i < MAX_WEARABLES; i++)
            {
                // this makes them all null
                _wearables[i] = new AvatarWearable();
            }
            _serial = 0;
            _user = LLUUID.Zero;
        }

        public LLUUID User {
            get { return _user; }
            set { _user = value; }
        }

        public int Serial {
            get { return _serial; }
            set { _serial = value; }
        }

        public LLUUID BodyItem {
            get { return _wearables[BODY].ItemID; }
            set { _wearables[BODY].ItemID = value; }
        }
        public LLUUID BodyAsset {
            get { return _wearables[BODY].AssetID; }
            set { _wearables[BODY].AssetID = value; }
        }
        public LLUUID SkinItem {
            get { return _wearables[SKIN].ItemID; }
            set { _wearables[SKIN].ItemID = value; }
        }
        public LLUUID SkinAsset {
            get { return _wearables[SKIN].AssetID; }
            set { _wearables[SKIN].AssetID = value; }
        }
        public LLUUID HairItem {
            get { return _wearables[HAIR].ItemID; }
            set { _wearables[HAIR].ItemID = value; }
        }
        public LLUUID HairAsset {
            get { return _wearables[HAIR].AssetID; }
            set { _wearables[HAIR].AssetID = value; }
        }
        public LLUUID EyesItem {
            get { return _wearables[EYES].ItemID; }
            set { _wearables[EYES].ItemID = value; }
        }
        public LLUUID EyesAsset {
            get { return _wearables[EYES].AssetID; }
            set { _wearables[EYES].AssetID = value; }
        }
        public LLUUID ShirtItem {
            get { return _wearables[SHIRT].ItemID; }
            set { _wearables[SHIRT].ItemID = value; }
        }
        public LLUUID ShirtAsset {
            get { return _wearables[SHIRT].AssetID; }
            set { _wearables[SHIRT].AssetID = value; }
        }
        public LLUUID PantsItem {
            get { return _wearables[PANTS].ItemID; }
            set { _wearables[PANTS].ItemID = value; }
        }
        public LLUUID PantsAsset {
            get { return _wearables[BODY].AssetID; }
            set { _wearables[BODY].AssetID = value; }
        }
        public LLUUID ShoesItem {
            get { return _wearables[SHOES].ItemID; }
            set { _wearables[SHOES].ItemID = value; }
        }
        public LLUUID ShoesAsset {
            get { return _wearables[SHOES].AssetID; }
            set { _wearables[SHOES].AssetID = value; }
        }
        public LLUUID SocksItem {
            get { return _wearables[SOCKS].ItemID; }
            set { _wearables[SOCKS].ItemID = value; }
        }
        public LLUUID SocksAsset {
            get { return _wearables[SOCKS].AssetID; }
            set { _wearables[SOCKS].AssetID = value; }
        }
        public LLUUID JacketItem {
            get { return _wearables[JACKET].ItemID; }
            set { _wearables[JACKET].ItemID = value; }
        }
        public LLUUID JacketAsset {
            get { return _wearables[JACKET].AssetID; }
            set { _wearables[JACKET].AssetID = value; }
        }
        public LLUUID GlovesItem {
            get { return _wearables[GLOVES].ItemID; }
            set { _wearables[GLOVES].ItemID = value; }
        }
        public LLUUID GlovesAsset {
            get { return _wearables[GLOVES].AssetID; }
            set { _wearables[GLOVES].AssetID = value; }
        }
        public LLUUID UnderShirtItem {
            get { return _wearables[UNDERSHIRT].ItemID; }
            set { _wearables[UNDERSHIRT].ItemID = value; }
        }
        public LLUUID UnderShirtAsset {
            get { return _wearables[UNDERSHIRT].AssetID; }
            set { _wearables[UNDERSHIRT].AssetID = value; }
        }
        public LLUUID UnderPantsItem {
            get { return _wearables[UNDERPANTS].ItemID; }
            set { _wearables[UNDERPANTS].ItemID = value; }
        }
        public LLUUID UnderPantsAsset {
            get { return _wearables[UNDERPANTS].AssetID; }
            set { _wearables[UNDERPANTS].AssetID = value; }
        }
        public LLUUID SkirtItem {
            get { return _wearables[SKIRT].ItemID; }
            set { _wearables[SKIRT].ItemID = value; }
        }
        public LLUUID SkirtAsset {
            get { return _wearables[SKIRT].AssetID; }
            set { _wearables[SKIRT].AssetID = value; }
        }

        public void SetDefaultWearables() 
        {
            _wearables[BODY].AssetID = BODY_ASSET;
            _wearables[BODY].ItemID = BODY_ITEM;
            _wearables[SKIN].AssetID = SKIN_ASSET;
            _wearables[SKIN].ItemID = SKIN_ITEM;
            _wearables[SHIRT].AssetID = SHIRT_ASSET;
            _wearables[SHIRT].ItemID = SHIRT_ITEM;
            _wearables[PANTS].AssetID = PANTS_ASSET;
            _wearables[PANTS].ItemID = PANTS_ITEM;
        }
    }
}