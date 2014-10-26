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
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    [Serializable]
    public class WearableCacheItem
    {
        public uint TextureIndex { get; set; }
        public UUID CacheId { get; set; }
        public UUID TextureID { get; set; }
        public AssetBase TextureAsset { get; set; }


        public static WearableCacheItem[] GetDefaultCacheItem()
        {
            int itemmax = AvatarAppearance.TEXTURE_COUNT;
            WearableCacheItem[] retitems = new WearableCacheItem[itemmax];
            for (uint i=0;i<itemmax;i++)
                retitems[i] = new WearableCacheItem() {CacheId = UUID.Zero, TextureID = UUID.Zero, TextureIndex = i};
            return retitems;          
        }

        public static WearableCacheItem[] FromOSD(OSD pInput, IImprovedAssetCache dataCache)
        {
            List<WearableCacheItem> ret = new List<WearableCacheItem>();
            if (pInput.Type == OSDType.Array)
            {
                OSDArray itemarray = (OSDArray) pInput;
                foreach (OSDMap item in itemarray)
                {
                    ret.Add(new WearableCacheItem()
                                {
                                    TextureIndex = item["textureindex"].AsUInteger(),
                                    CacheId = item["cacheid"].AsUUID(),
                                    TextureID = item["textureid"].AsUUID()
                                });
                    
                    if (dataCache != null && item.ContainsKey("assetdata"))
                    {
                        AssetBase asset = new AssetBase(item["textureid"].AsUUID(),"BakedTexture",(sbyte)AssetType.Texture,UUID.Zero.ToString());
                        asset.Temporary = true;
                        asset.Data = item["assetdata"].AsBinary();
                        dataCache.Cache(asset);
                    }
                }
            }
            else if (pInput.Type == OSDType.Map)
            {
                OSDMap item = (OSDMap) pInput;
                ret.Add(new WearableCacheItem(){
                                    TextureIndex = item["textureindex"].AsUInteger(),
                                    CacheId = item["cacheid"].AsUUID(),
                                    TextureID = item["textureid"].AsUUID()
                                });
                if (dataCache != null && item.ContainsKey("assetdata"))
                {
                    string assetCreator = item["assetcreator"].AsString();
                    string assetName = item["assetname"].AsString();
                    AssetBase asset = new AssetBase(item["textureid"].AsUUID(), assetName, (sbyte)AssetType.Texture, assetCreator);
                    asset.Temporary = true;
                    asset.Data = item["assetdata"].AsBinary();
                    dataCache.Cache(asset);
                }
            }
            else
            {
                return new WearableCacheItem[0];
            }
            return ret.ToArray();

        }

        public static OSD ToOSD(WearableCacheItem[] pcacheItems, IImprovedAssetCache dataCache)
        {
            OSDArray arr = new OSDArray();
            foreach (WearableCacheItem item in pcacheItems)
            {
                OSDMap itemmap = new OSDMap();
                itemmap.Add("textureindex", OSD.FromUInteger(item.TextureIndex));
                itemmap.Add("cacheid", OSD.FromUUID(item.CacheId));
                itemmap.Add("textureid", OSD.FromUUID(item.TextureID));
                if (dataCache != null)
                {
                    if (dataCache.Check(item.TextureID.ToString()))
                    {
                        AssetBase assetItem = dataCache.Get(item.TextureID.ToString());
                        if (assetItem != null)
                        {
                            itemmap.Add("assetdata", OSD.FromBinary(assetItem.Data));
                            itemmap.Add("assetcreator", OSD.FromString(assetItem.CreatorID));
                            itemmap.Add("assetname", OSD.FromString(assetItem.Name));
                        }
                    }
                }
                arr.Add(itemmap);
            }
            return arr;
        }

        public static OSDArray BakedToOSD(WearableCacheItem[] pcacheItems)
        {
            if (pcacheItems.Length < AvatarAppearance.BAKE_INDICES[AvatarAppearance.BAKE_INDICES.Length - 1])
                return null;

            OSDArray arr = new OSDArray();

            for (int i = 0; i < AvatarAppearance.BAKE_INDICES.Length; i++)
            {
                int idx = AvatarAppearance.BAKE_INDICES[i];

                WearableCacheItem item = pcacheItems[idx];

                OSDMap itemmap = new OSDMap();
                itemmap.Add("textureindex", OSD.FromUInteger(item.TextureIndex));
                itemmap.Add("cacheid", OSD.FromUUID(item.CacheId));
                itemmap.Add("textureid", OSD.FromUUID(item.TextureID));
/*
                if (item.TextureAsset != null)
                {
                    itemmap.Add("assetdata", OSD.FromBinary(item.TextureAsset.Data));
                    itemmap.Add("assetcreator", OSD.FromString(item.TextureAsset.CreatorID));
                    itemmap.Add("assetname", OSD.FromString(item.TextureAsset.Name));
                }
 */
                arr.Add(itemmap);
            }
            return arr;
        }

        public static WearableCacheItem[] BakedFromOSD(OSD pInput)
        {
            WearableCacheItem[] pcache = WearableCacheItem.GetDefaultCacheItem();

            if (pInput.Type == OSDType.Array)
            {
                OSDArray itemarray = (OSDArray)pInput;
                foreach (OSDMap item in itemarray)
                {
                    int idx = (int)item["textureindex"].AsUInteger();
                    if (idx < 0 || idx > pcache.Length)
                        continue;
                    pcache[idx].CacheId = item["cacheid"].AsUUID();
                    pcache[idx].TextureID = item["textureid"].AsUUID();
/*
                    if (item.ContainsKey("assetdata"))
                    {
                        AssetBase asset = new AssetBase(item["textureid"].AsUUID(), "BakedTexture", (sbyte)AssetType.Texture, UUID.Zero.ToString());
                        asset.Temporary = true;
                        asset.Local = true;
                        asset.Data = item["assetdata"].AsBinary();
                        pcache[idx].TextureAsset = asset;
                    }
                    else
 */ 
                        pcache[idx].TextureAsset = null;
                }
            }
            return pcache;
        }

        public static WearableCacheItem SearchTextureIndex(uint pTextureIndex,WearableCacheItem[] pcacheItems)
        {
            for (int i = 0; i < pcacheItems.Length; i++)
            {
                if (pcacheItems[i].TextureIndex == pTextureIndex)
                    return pcacheItems[i];
            }
            return null;
        }
        public static WearableCacheItem SearchTextureCacheId(UUID pCacheId, WearableCacheItem[] pcacheItems)
        {
            for (int i = 0; i < pcacheItems.Length; i++)
            {
                if (pcacheItems[i].CacheId == pCacheId)
                    return pcacheItems[i];
            }
            return null;
        }
        public static WearableCacheItem SearchTextureTextureId(UUID pTextureId, WearableCacheItem[] pcacheItems)
        {
            for (int i = 0; i < pcacheItems.Length; i++)
            {
                if (pcacheItems[i].TextureID == pTextureId)
                    return pcacheItems[i];
            }
            return null;
        }
    }


}
