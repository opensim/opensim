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