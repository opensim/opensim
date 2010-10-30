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

using OpenSim.Framework;

using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IAvatarService
    {
        /// <summary>
        /// Called by the login service
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        AvatarAppearance GetAppearance(UUID userID);

        /// <summary>
        /// Called by everyone who can change the avatar data (so, regions)
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="appearance"></param>
        /// <returns></returns>
        bool SetAppearance(UUID userID, AvatarAppearance appearance);
        
        /// <summary>
        /// Called by the login service
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        AvatarData GetAvatar(UUID userID);

        /// <summary>
        /// Called by everyone who can change the avatar data (so, regions)
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="avatar"></param>
        /// <returns></returns>
        bool SetAvatar(UUID userID, AvatarData avatar);

        /// <summary>
        /// Not sure if it's needed
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        bool ResetAvatar(UUID userID);

        /// <summary>
        /// These methods raison d'etre: 
        /// No need to send the entire avatar data (SetAvatar) for changing attachments
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="attach"></param>
        /// <returns></returns>
        bool SetItems(UUID userID, string[] names, string[] values);
        bool RemoveItems(UUID userID, string[] names);
    }

    /// <summary>
    /// Each region/client that uses avatars will have a data structure
    /// of this type representing the avatars.
    /// </summary>
    public class AvatarData
    {
        // This pretty much determines which name/value pairs will be
        // present below. The name/value pair describe a part of
        // the avatar. For SL avatars, these would be "shape", "texture1",
        // etc. For other avatars, they might be "mesh", "skin", etc.
        // The value portion is a URL that is expected to resolve to an
        // asset of the type required by the handler for that field.
        // It is required that regions can access these URLs. Allowing
        // direct access by a viewer is not required, and, if provided,
        // may be read-only. A "naked" UUID can be used to refer to an
        // asset int he current region's asset service, which is not
        // portable, but allows legacy appearance to continue to
        // function. Closed, LL-based  grids will never need URLs here.

        public int AvatarType;
        public Dictionary<string,string> Data;

        public AvatarData()
        {
        }

        public AvatarData(Dictionary<string, object> kvp)
        {
            Data = new Dictionary<string, string>();

            if (kvp.ContainsKey("AvatarType"))
                Int32.TryParse(kvp["AvatarType"].ToString(), out AvatarType);

            foreach (KeyValuePair<string, object> _kvp in kvp)
            {
                if (_kvp.Value != null)
                    Data[_kvp.Key] = _kvp.Value.ToString();
            }
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            result["AvatarType"] = AvatarType.ToString();
            foreach (KeyValuePair<string, string> _kvp in Data)
            {
                if (_kvp.Value != null)
                    result[_kvp.Key] = _kvp.Value;
            }
            return result;
        }

        public AvatarData(AvatarAppearance appearance)
        {
            AvatarType = 1; // SL avatars
            Data = new Dictionary<string, string>();

            Data["Serial"] = appearance.Serial.ToString();
            // Wearables
            Data["AvatarHeight"] = appearance.AvatarHeight.ToString();
            Data["BodyItem"] = appearance.Wearables[AvatarWearable.BODY][0].ItemID.ToString();
            Data["EyesItem"] = appearance.Wearables[AvatarWearable.EYES][0].ItemID.ToString();
            Data["GlovesItem"] = appearance.Wearables[AvatarWearable.GLOVES][0].ItemID.ToString();
            Data["HairItem"] = appearance.Wearables[AvatarWearable.HAIR][0].ItemID.ToString();
            Data["JacketItem"] = appearance.Wearables[AvatarWearable.JACKET][0].ItemID.ToString();
            Data["PantsItem"] = appearance.Wearables[AvatarWearable.PANTS][0].ItemID.ToString();
            Data["ShirtItem"] = appearance.Wearables[AvatarWearable.SHIRT][0].ItemID.ToString();
            Data["ShoesItem"] = appearance.Wearables[AvatarWearable.SHOES][0].ItemID.ToString();
            Data["SkinItem"] = appearance.Wearables[AvatarWearable.SKIN][0].ItemID.ToString();
            Data["SkirtItem"] = appearance.Wearables[AvatarWearable.SKIRT][0].ItemID.ToString();
            Data["SocksItem"] = appearance.Wearables[AvatarWearable.SOCKS][0].ItemID.ToString();
            Data["UnderPantsItem"] = appearance.Wearables[AvatarWearable.UNDERPANTS][0].ItemID.ToString();
            Data["UnderShirtItem"] = appearance.Wearables[AvatarWearable.UNDERSHIRT][0].ItemID.ToString();

            Data["BodyAsset"] = appearance.Wearables[AvatarWearable.BODY][0].AssetID.ToString();
            Data["EyesAsset"] = appearance.Wearables[AvatarWearable.EYES][0].AssetID.ToString();
            Data["GlovesAsset"] = appearance.Wearables[AvatarWearable.GLOVES][0].AssetID.ToString();
            Data["HairAsset"] = appearance.Wearables[AvatarWearable.HAIR][0].AssetID.ToString();
            Data["JacketAsset"] = appearance.Wearables[AvatarWearable.JACKET][0].AssetID.ToString();
            Data["PantsAsset"] = appearance.Wearables[AvatarWearable.PANTS][0].AssetID.ToString();
            Data["ShirtAsset"] = appearance.Wearables[AvatarWearable.SHIRT][0].AssetID.ToString();
            Data["ShoesAsset"] = appearance.Wearables[AvatarWearable.SHOES][0].AssetID.ToString();
            Data["SkinAsset"] = appearance.Wearables[AvatarWearable.SKIN][0].AssetID.ToString();
            Data["SkirtAsset"] = appearance.Wearables[AvatarWearable.SKIRT][0].AssetID.ToString();
            Data["SocksAsset"] = appearance.Wearables[AvatarWearable.SOCKS][0].AssetID.ToString();
            Data["UnderPantsAsset"] = appearance.Wearables[AvatarWearable.UNDERPANTS][0].AssetID.ToString();
            Data["UnderShirtAsset"] = appearance.Wearables[AvatarWearable.UNDERSHIRT][0].AssetID.ToString();

            // Attachments
            List<AvatarAttachment> attachments = appearance.GetAttachments();
            foreach (AvatarAttachment attach in attachments)
            {
                Data["_ap_" + attach.AttachPoint] = attach.ItemID.ToString();
            }
        }

        public AvatarAppearance ToAvatarAppearance(UUID owner)
        {
            AvatarAppearance appearance = new AvatarAppearance(owner);
            try
            {
                appearance.Serial = Int32.Parse(Data["Serial"]);

                // Wearables
                appearance.Wearables[AvatarWearable.BODY].Wear(
                        UUID.Parse(Data["BodyItem"]),
                        UUID.Parse(Data["BodyAsset"]));

                appearance.Wearables[AvatarWearable.SKIN].Wear(
                        UUID.Parse(Data["SkinItem"]),
                        UUID.Parse(Data["SkinAsset"]));

                appearance.Wearables[AvatarWearable.HAIR].Wear(
                        UUID.Parse(Data["HairItem"]),
                        UUID.Parse(Data["HairAsset"]));

                appearance.Wearables[AvatarWearable.EYES].Wear(
                        UUID.Parse(Data["EyesItem"]),
                        UUID.Parse(Data["EyesAsset"]));

                appearance.Wearables[AvatarWearable.SHIRT].Wear(
                        UUID.Parse(Data["ShirtItem"]),
                        UUID.Parse(Data["ShirtAsset"]));

                appearance.Wearables[AvatarWearable.PANTS].Wear(
                        UUID.Parse(Data["PantsItem"]),
                        UUID.Parse(Data["PantsAsset"]));

                appearance.Wearables[AvatarWearable.SHOES].Wear(
                        UUID.Parse(Data["ShoesItem"]),
                        UUID.Parse(Data["ShoesAsset"]));

                appearance.Wearables[AvatarWearable.SOCKS].Wear(
                        UUID.Parse(Data["SocksItem"]),
                        UUID.Parse(Data["SocksAsset"]));

                appearance.Wearables[AvatarWearable.JACKET].Wear(
                        UUID.Parse(Data["JacketItem"]),
                        UUID.Parse(Data["JacketAsset"]));

                appearance.Wearables[AvatarWearable.GLOVES].Wear(
                        UUID.Parse(Data["GlovesItem"]),
                        UUID.Parse(Data["GlovesAsset"]));

                appearance.Wearables[AvatarWearable.UNDERSHIRT].Wear(
                        UUID.Parse(Data["UnderShirtItem"]),
                        UUID.Parse(Data["UnderShirtAsset"]));

                appearance.Wearables[AvatarWearable.UNDERPANTS].Wear(
                        UUID.Parse(Data["UnderPantsItem"]),
                        UUID.Parse(Data["UnderPantsAsset"]));

                appearance.Wearables[AvatarWearable.SKIRT].Wear(
                        UUID.Parse(Data["SkirtItem"]),
                        UUID.Parse(Data["SkirtAsset"]));

                // Attachments
                Dictionary<string, string> attchs = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> _kvp in Data)
                    if (_kvp.Key.StartsWith("_ap_"))
                        attchs[_kvp.Key] = _kvp.Value;

                foreach (KeyValuePair<string, string> _kvp in attchs)
                {
                    string pointStr = _kvp.Key.Substring(4);
                    int point = 0;
                    if (!Int32.TryParse(pointStr, out point))
                        continue;

                    UUID uuid = UUID.Zero;
                    UUID.TryParse(_kvp.Value, out uuid);

                    appearance.SetAttachment(point,uuid,UUID.Zero);
                }
            }
            catch
            {
                // We really should report something here, returning null
                // will at least break the wrapper
                return null;
            }

            return appearance;
        }
    }
}
