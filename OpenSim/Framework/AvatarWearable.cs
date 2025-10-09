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
using System.Linq;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public struct WearableItem
    {
        public UUID ItemID;
        public UUID AssetID;

        public WearableItem(UUID itemID, UUID assetID)
        {
            ItemID = itemID;
            AssetID = assetID;
        }
    }

    public class AvatarWearable
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

        public const int MAX_BASICWEARABLES = 13;

        public const int ALPHA = 13;
        public const int TATTOO = 14;
        public const int LEGACY_VERSION_MAX_WEARABLES = 15;

        public const int PHYSICS = 15;

        public static int MAX_WEARABLES_PV7 = 16;

        public const int UNIVERSAL = 16;
        public static int MAX_WEARABLES = 17;


        public static readonly UUID DEFAULT_BODY_ITEM = new("66c41e39-38f9-f75a-024e-585989bfaba9");
        public static readonly UUID DEFAULT_BODY_ASSET = new("66c41e39-38f9-f75a-024e-585989bfab73");

        public static readonly UUID DEFAULT_HAIR_ITEM = new("d342e6c1-b9d2-11dc-95ff-0800200c9a66");
        public static readonly UUID DEFAULT_HAIR_ASSET = new("d342e6c0-b9d2-11dc-95ff-0800200c9a66");

        public static readonly UUID DEFAULT_SKIN_ITEM = new("77c41e39-38f9-f75a-024e-585989bfabc9");
        public static readonly UUID DEFAULT_SKIN_ASSET = new("77c41e39-38f9-f75a-024e-585989bbabbb");

        public static readonly UUID DEFAULT_EYES_ITEM = new("cdc31054-eed8-4021-994f-4e0c6e861b50");
        public static readonly UUID DEFAULT_EYES_ASSET = new("4bb6fa4d-1cd2-498a-a84c-95c1a0e745a7");

        public static readonly UUID DEFAULT_SHIRT_ITEM = new("77c41e39-38f9-f75a-0000-585989bf0000");
        public static readonly UUID DEFAULT_SHIRT_ASSET = new("00000000-38f9-1111-024e-222222111110");

        public static readonly UUID DEFAULT_PANTS_ITEM = new("77c41e39-38f9-f75a-0000-5859892f1111");
        public static readonly UUID DEFAULT_PANTS_ASSET = new("00000000-38f9-1111-024e-222222111120");

        public static readonly UUID DEFAULT_ALPHA_ITEM = new("bfb9923c-4838-4d2d-bf07-608c5b1165c8");
        public static readonly UUID DEFAULT_ALPHA_ASSET = new("1578a2b1-5179-4b53-b618-fe00ca5a5594");

        public static readonly UUID DEFAULT_TATTOO_ITEM = new("c47e22bd-3021-4ba4-82aa-2b5cb34d35e1");
        public static readonly UUID DEFAULT_TATTOO_ASSET = new("00000000-0000-2222-3333-100000001007");

        protected readonly Dictionary<UUID, UUID> m_items = new();
        protected readonly List<UUID> m_ids = [];

        public AvatarWearable()
        {
        }

        public AvatarWearable(UUID itemID, UUID assetID)
        {
            Wear(itemID, assetID);
        }

        public AvatarWearable(OSDArray args)
        {
            Unpack(args);
        }

        public OSD Pack()
        {
            OSDArray wearlist = [];

            foreach (var weardata in m_ids.Select(id => new OSDMap
                     {
                         ["item"] = OSD.FromUUID(id),
                         ["asset"] = OSD.FromUUID(m_items[id])
                     }))
            {
                wearlist.Add(weardata);
            }

            return wearlist;
        }

        public void Unpack(OSDArray args)
        {
            Clear();
            OSD tmpOSDA, tmpOSDB;
            foreach (var osd in args)
            {
                var weardata = (OSDMap)osd;
                tmpOSDA = weardata["item"];
                tmpOSDB = weardata["asset"];
                Add(tmpOSDA.AsUUID(), tmpOSDB.AsUUID());
            }
        }

        public int Count => m_ids.Count;

        public void Add(UUID itemID, UUID assetID)
        {
            if (itemID.IsZero())
                return;
            if (m_items.ContainsKey(itemID))
            {
                m_items[itemID] = assetID;
                return;
            }
            if (m_ids.Count >= 5)
                return;

            m_ids.Add(itemID);
            m_items[itemID] = assetID;
        }

        public void Wear(WearableItem item)
        {
            Wear(item.ItemID, item.AssetID);
        }

        public void Wear(UUID itemID, UUID assetID)
        {
            Clear();
            Add(itemID, assetID);
        }

        public void Clear()
        {
            m_ids.Clear();
            m_items.Clear();
        }

        public void RemoveItem(UUID itemID)
        {
            m_ids.Remove(itemID);
            m_items.Remove(itemID);
        }

        public void RemoveAsset(UUID assetID)
        {
            var itemID = UUID.Zero;

            foreach (var kvp in m_items.Where(kvp => kvp.Value == assetID))
            {
                itemID = kvp.Key;
                break;
            }

            if (itemID.IsZero()) 
                return;
            
            m_ids.Remove(itemID);
            m_items.Remove(itemID);
        }

        public WearableItem this [int idx]
        {
            get
            {
                if (idx >= m_ids.Count || idx < 0)
                    return new WearableItem(UUID.Zero, UUID.Zero);

                return new WearableItem(m_ids[idx], m_items[m_ids[idx]]);
            }
        }

        public UUID GetAsset(UUID itemID)
        {
            return m_items.TryGetValue(itemID, out UUID id) ? id : UUID.Zero;
        }

        public static AvatarWearable[] DefaultWearables
        {
            get
            {
                // We use the legacy count here because this is just a fallback anyway
                var defaultWearables = new AvatarWearable[LEGACY_VERSION_MAX_WEARABLES];
                for (var i = 0; i < LEGACY_VERSION_MAX_WEARABLES; i++)
                {
                    defaultWearables[i] = new AvatarWearable();
                }

                // Body
                defaultWearables[BODY].Add(DEFAULT_BODY_ITEM, DEFAULT_BODY_ASSET);

                // Hair
                defaultWearables[HAIR].Add(DEFAULT_HAIR_ITEM, DEFAULT_HAIR_ASSET);

                // Skin
                defaultWearables[SKIN].Add(DEFAULT_SKIN_ITEM, DEFAULT_SKIN_ASSET);

                // Eyes
                defaultWearables[EYES].Add(DEFAULT_EYES_ITEM, DEFAULT_EYES_ASSET);

                // Shirt
                defaultWearables[SHIRT].Add(DEFAULT_SHIRT_ITEM, DEFAULT_SHIRT_ASSET);

                // Pants
                defaultWearables[PANTS].Add(DEFAULT_PANTS_ITEM, DEFAULT_PANTS_ASSET);

//                // Alpha
//                defaultWearables[ALPHA].Add(DEFAULT_ALPHA_ITEM, DEFAULT_ALPHA_ASSET);

                //                // Tattoo
                //                defaultWearables[TATTOO].Add(DEFAULT_TATTOO_ITEM, DEFAULT_TATTOO_ASSET);

                //                // Physics
                //                defaultWearables[PHYSICS].Add(DEFAULT_TATTOO_ITEM, DEFAULT_TATTOO_ASSET);

                return defaultWearables;
            }
        }
    }
}
