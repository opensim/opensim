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

using System.Text;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common
{
    public class AssetHelpers
    {
        /// <summary>
        /// Create a notecard asset with a random uuids and dummy text.
        /// </summary>
        /// <returns></returns>
        public static AssetBase CreateNotecardAsset()
        {
            return CreateNotecardAsset(UUID.Random());
        }

        /// <summary>
        /// Create a notecard asset with dummy text and a random owner.
        /// </summary>
        /// <param name="assetId">/param>
        /// <returns></returns>
        public static AssetBase CreateNotecardAsset(UUID assetId)
        {
            return CreateNotecardAsset(assetId, "hello");
        }

        /// <summary>
        /// Create a notecard asset with a random owner.
        /// </summary>
        /// <param name="assetId">/param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static AssetBase CreateNotecardAsset(UUID assetId, string text)
        {
            return CreateAsset(assetId, AssetType.Notecard, text, UUID.Random());
        }

//        /// <summary>
//        /// Create and store a notecard asset with a random uuid and dummy text.
//        /// </summary>
//        /// <param name="creatorId">/param>
//        /// <returns></returns>
//        public static AssetBase CreateNotecardAsset(Scene scene, UUID creatorId)
//        {
//            AssetBase asset = CreateAsset(UUID.Random(), AssetType.Notecard, "hello", creatorId);
//            scene.AssetService.Store(asset);
//            return asset;
//        }

        /// <summary>
        /// Create an asset from the given object.
        /// </summary>
        /// <param name="assetUuidTail">
        /// The hexadecimal last part of the UUID for the asset created.  A UUID of the form "00000000-0000-0000-0000-{0:XD12}"
        /// will be used.
        /// </param>
        /// <param name="sog"></param>
        /// <returns></returns>
        public static AssetBase CreateAsset(int assetUuidTail, SceneObjectGroup sog)
        {
            return CreateAsset(new UUID(string.Format("00000000-0000-0000-0000-{0:X12}", assetUuidTail)), sog);
        }

        /// <summary>
        /// Create an asset from the given object.
        /// </summary>
        /// <param name="assetUuid"></param>
        /// <param name="sog"></param>
        /// <returns></returns>
        public static AssetBase CreateAsset(UUID assetUuid, SceneObjectGroup sog)
        {
            return CreateAsset(
                assetUuid,
                AssetType.Object,
                Encoding.ASCII.GetBytes(SceneObjectSerializer.ToOriginalXmlFormat(sog)),
                sog.OwnerID);
        }

        /// <summary>
        /// Create an asset from the given scene object.
        /// </summary>
        /// <param name="assetUuidTail">
        /// The hexadecimal last part of the UUID for the asset created.  A UUID of the form "00000000-0000-0000-0000-{0:XD12}"
        /// will be used.
        /// </param>
        /// <param name="coa"></param>
        /// <returns></returns>
        public static AssetBase CreateAsset(int assetUuidTail, CoalescedSceneObjects coa)
        {
            return CreateAsset(new UUID(string.Format("00000000-0000-0000-0000-{0:X12}", assetUuidTail)), coa);
        }

        /// <summary>
        /// Create an asset from the given scene object.
        /// </summary>
        /// <param name="assetUuid"></param>
        /// <param name="coa"></param>
        /// <returns></returns>
        public static AssetBase CreateAsset(UUID assetUuid, CoalescedSceneObjects coa)
        {
            return CreateAsset(
                assetUuid,
                AssetType.Object,
                Encoding.ASCII.GetBytes(CoalescedSceneObjectsSerializer.ToXml(coa)),
                coa.CreatorId);
        }

        /// <summary>
        /// Create an asset from the given data.
        /// </summary>
        public static AssetBase CreateAsset(UUID assetUuid, AssetType assetType, string text, UUID creatorID)
        {
            AssetNotecard anc = new AssetNotecard();
            anc.BodyText = text;
            anc.Encode();

            return CreateAsset(assetUuid, assetType, anc.AssetData, creatorID);
        }

        /// <summary>
        /// Create an asset from the given data.
        /// </summary>
        public static AssetBase CreateAsset(UUID assetUuid, AssetType assetType, byte[] data, UUID creatorID)
        {
            AssetBase asset = new AssetBase(assetUuid, assetUuid.ToString(), (sbyte)assetType, creatorID.ToString());
            asset.Data = data;
            return asset;
        }

        public static string ReadAssetAsString(IAssetService assetService, UUID uuid)
        {
            byte[] assetData = assetService.GetData(uuid.ToString());
            return Encoding.ASCII.GetString(assetData);
        }
    }
}
