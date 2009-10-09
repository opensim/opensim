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
using System.Runtime.Serialization;
using System.Security.Permissions;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AvatarWearable
    {
        public UUID AssetID = new UUID("00000000-0000-0000-0000-000000000000");
        public UUID ItemID = new UUID("00000000-0000-0000-0000-000000000000");

        public AvatarWearable()
        {
        }

        public AvatarWearable(UUID itemId, UUID assetId)
        {
            AssetID = assetId;
            ItemID = itemId;
        }

        public static AvatarWearable[] DefaultWearables
        {
            get
            {
                AvatarWearable[] defaultWearables = new AvatarWearable[13]; //should be 13 of these
                for (int i = 0; i < 13; i++)
                {
                    defaultWearables[i] = new AvatarWearable();
                }
                
                // Body
                defaultWearables[0].ItemID  = new UUID("66c41e39-38f9-f75a-024e-585989bfaba9");
                defaultWearables[0].AssetID = new UUID("66c41e39-38f9-f75a-024e-585989bfab73");
                
                // Hair
                defaultWearables[2].ItemID  = new UUID("d342e6c1-b9d2-11dc-95ff-0800200c9a66");
                defaultWearables[2].AssetID = new UUID("d342e6c0-b9d2-11dc-95ff-0800200c9a66");

                // Skin
                defaultWearables[1].ItemID  = new UUID("77c41e39-38f9-f75a-024e-585989bfabc9");
                defaultWearables[1].AssetID = new UUID("77c41e39-38f9-f75a-024e-585989bbabbb");

                // Shirt
                defaultWearables[4].ItemID  = new UUID("77c41e39-38f9-f75a-0000-585989bf0000");
                defaultWearables[4].AssetID = new UUID("00000000-38f9-1111-024e-222222111110");

                // Pants
                defaultWearables[5].ItemID  = new UUID("77c41e39-38f9-f75a-0000-5859892f1111");
                defaultWearables[5].AssetID = new UUID("00000000-38f9-1111-024e-222222111120");
                
                return defaultWearables;
            }
        }
    }
}
