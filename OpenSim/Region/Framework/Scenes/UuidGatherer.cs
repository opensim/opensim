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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Gather uuids for a given entity.
    /// </summary>
    /// <remarks>
    /// This does a deep inspection of the entity to retrieve all the assets it uses (whether as textures, as scripts
    /// contained in inventory, as scripts contained in objects contained in another object's inventory, etc.  Assets
    /// are only retrieved when they are necessary to carry out the inspection (i.e. a serialized object needs to be
    /// retrieved to work out which assets it references).
    /// </remarks>
    public class UuidGatherer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly HashSet<UUID> ToSkip = new()
        {
            new UUID("11111111-1111-0000-0000-000100bba000"),
            new UUID("5a9f4a74-30f2-821c-b88d-70499d3e7183"),
            new UUID("ae2de45c-d252-50b8-5c6e-19f39ce79317"),
            new UUID("24daea5f-0539-cfcf-047f-fbc40b2786ba"),
            new UUID("52cc6bb6-2ee5-e632-d3ad-50197b1dcb8a"),
            new UUID("43529ce8-7faa-ad92-165a-bc4078371687"),
            new UUID("09aac1fb-6bce-0bee-7d44-caac6dbb6c63"),
            new UUID("ff62763f-d60a-9855-890b-0c96f8f8cd98"),
            new UUID("8e915e25-31d1-cc95-ae08-d58a47488251"),
            new UUID("9742065b-19b5-297c-858a-29711d539043"),
            new UUID("03642e83-2bd1-4eb9-34b4-4c47ed586d2d"),
            new UUID("edd51b77-fc10-ce7a-4b3d-011dfc349e4f"),
            new UUID("44e87126-e794-4ded-05b3-7c42da3d5cdb"),
            new UUID("3d6181b0-6a4b-97ef-18d8-722652995cf1"),
            new UUID("b4ba225c-373f-446d-9f7e-6cb7b5cf9b3d"),
            new UUID("d2114404-dd59-4a4d-8e6c-49359e91bbf0"),
            new UUID("c228d1cf-4b5d-4ba8-84f4-899a0796aa97"),
            new UUID("e97cf410-8e61-7005-ec06-629eba4cd1fb"),
            new UUID("38b86f85-2575-52a9-a531-23108d8da837"),
            new UUID("8dcd4a48-2d37-4909-9f78-f7a9eb4ef903"),
            new UUID("3c59f7fe-9dc8-47f9-8aaf-a9dd1fbc3bef"),
            new UUID("0bc58228-74a0-7e83-89bc-5c23464bcec5"),
            new UUID("63338ede-0037-c4fd-855b-015d77112fc8"),
            new UUID("303cd381-8560-7579-23f1-f0a880799740"),
            new UUID("53a2f406-4895-1d13-d541-d2e3b86bc19c"),
            new UUID("822ded49-9a6c-f61c-cb89-6df54f42cdf4"),
            new UUID("6b61c8e8-4747-0d75-12d7-e49ff207a4ca"),
            new UUID("b5b4a67d-0aee-30d2-72cd-77b333e932ef"),
            new UUID("46bb4359-de38-4ed8-6a22-f1f52fe8f506"),
            new UUID("3147d815-6338-b932-f011-16b56d9ac18b"),
            new UUID("ea633413-8006-180a-c3ba-96dd1d756720"),
            new UUID("5747a48e-073e-c331-f6f3-7c2149613d3e"),
            new UUID("fd037134-85d4-f241-72c6-4f42164fedee"),
            new UUID("c4ca6188-9127-4f31-0158-23c4e2f93304"),
            new UUID("18b3a4b5-b463-bd48-e4b6-71eaac76c515"),
            new UUID("db84829b-462c-ee83-1e27-9bbee66bd624"),
            new UUID("b906c4ba-703b-1940-32a3-0c7f7d791510"),
            new UUID("82e99230-c906-1403-4d9c-3889dd98daba"),
            new UUID("349a3801-54f9-bf2c-3bd0-1ac89772af01"),
            new UUID("efcf670c-2d18-8128-973a-034ebc806b67"),
            new UUID("9b0c1c4e-8ac7-7969-1494-28c874c4f668"),
            new UUID("9ba1c942-08be-e43a-fb29-16ad440efc50"),
            new UUID("201f3fdf-cb1f-dbec-201f-7333e328ae7c"),
            new UUID("47f5f6fb-22e5-ae44-f871-73aaaf4a6022"),
            new UUID("92624d3e-1068-f1aa-a5ec-8244585193ed"),
            new UUID("038fcec9-5ebd-8a8e-0e2e-6e71a0a1ac53"),
            new UUID("6883a61a-b27b-5914-a61e-dda118a9ee2c"),
            new UUID("b68a3d7c-de9e-fc87-eec8-543d787e5b0d"),
            new UUID("928cae18-e31d-76fd-9cc9-2f55160ff818"),
            new UUID("30047778-10ea-1af7-6881-4db7a3a5a114"),
            new UUID("951469f4-c7b2-c818-9dee-ad7eea8c30b7"),
            new UUID("4bd69a1d-1114-a0b4-625f-84e0a5237155"),
            new UUID("cd28b69b-9c95-bb78-3f94-8d605ff1bb12"),
            new UUID("a54d8ee2-28bb-80a9-7f0c-7afbbe24a5d6"),
            new UUID("b0dc417c-1f11-af36-2e80-7e7489fa7cdc"),
            new UUID("57abaae6-1d17-7b1b-5f98-6d11a6411276"),
            new UUID("0f86e355-dd31-a61c-fdb0-3a96b9aad05f"),
            new UUID("514af488-9051-044a-b3fc-d4dbf76377c6"),
            new UUID("aa2df84d-cf8f-7218-527b-424a52de766e"),
            new UUID("1a03b575-9634-b62a-5767-3a679e81f4de"),
            new UUID("214aa6c1-ba6a-4578-f27c-ce7688f61d0d"),
            new UUID("d535471b-85bf-3b4d-a542-93bea4f59d33"),
            new UUID("d4416ff1-09d3-300f-4183-1b68a19b9fc1"),
            new UUID("0b8c8211-d78c-33e8-fa28-c51a9594e424"),
            new UUID("fee3df48-fa3d-1015-1e26-a205810e3001"),
            new UUID("1e8d90cc-a84e-e135-884c-7c82c8b03a14"),
            new UUID("62570842-0950-96f8-341c-809e65110823"),
            new UUID("d63bc1f9-fc81-9625-a0c6-007176d82eb7"),
            new UUID("f76cda94-41d4-a229-2872-e0296e58afe1"),
            new UUID("eb6ebfb2-a4b3-a19c-d388-4dd5c03823f7"),
            new UUID("a351b1bc-cc94-aac2-7bea-a7e6ebad15ef"),
            new UUID("b7c7c833-e3d3-c4e3-9fc0-131237446312"),
            new UUID("728646d9-cc79-08b2-32d6-937f0a835c24"),
            new UUID("835965c6-7f2f-bda2-5deb-2478737f91bf"),
            new UUID("b92ec1a5-e7ce-a76b-2b05-bcdb9311417e"),
            new UUID("da020525-4d94-59d6-23d7-81fdebf33148"),
            new UUID("9c05e5c7-6f07-6ca4-ed5a-b230390c3950"),
            new UUID("666307d9-a860-572d-6fd4-c3ab8865c094"),
            new UUID("85995026-eade-5d78-d364-94a64512cb66"),
            new UUID("f5fc7433-043d-e819-8298-f519a119b688"),
            new UUID("d60c41d2-7c24-7074-d3fa-6101cea22a51"),
            new UUID("c1bc7f36-3ba0-d844-f93c-93be945d644f"),
            new UUID("7db00ccd-f380-f3ee-439d-61968ec69c8a"),
            new UUID("aec4610c-757f-bc4e-c092-c6e9caf18daf"),
            new UUID("2b5a38b2-5e00-3a97-a495-4c826bc443e6"),
            new UUID("9b29cd61-c45b-5689-ded2-91756b8d76a9"),
            new UUID("ef62d355-c815-4816-2474-b1acc21094a6"),
            new UUID("8b102617-bcba-037b-86c1-b76219f90c88"),
            new UUID("efdc1727-8b8a-c800-4077-975fc27ee2f2"),
            new UUID("3d94bad0-c55b-7dcc-8763-033c59405d33"),
            new UUID("7570c7b5-1f22-56dd-56ef-a9168241bbb6"),
            new UUID("4ae8016b-31b9-03bb-c401-b1ea941db41d"),
            new UUID("20f063ea-8306-2562-0b07-5c853b37b31e"),
            new UUID("62c5de58-cb33-5743-3d07-9e4cd4352864"),
            new UUID("5ea3991f-c293-392e-6860-91dfa01278a3"),
            new UUID("2305bd75-1ca9-b03b-1faa-b176b8a8c49e"),
            new UUID("709ea28e-1573-c023-8bf8-520c8bc637fa"),
            new UUID("19999406-3a3a-d58c-a2ac-d72e555dcf51"),
            new UUID("7a17b059-12b2-41b1-570a-186368b6aa6f"),
            new UUID("ca5b3f14-3194-7a2b-c894-aa699b718d1f"),
            new UUID("f4f00d6e-b9fe-9292-f4cb-0ae06ea58d57"),
            new UUID("08464f78-3a8e-2944-cba5-0c94aff3af29"),
            new UUID("315c3a41-a5f3-0ba4-27da-f893f769e69b"),
            new UUID("5a977ed9-7f72-44e9-4c4c-6e913df8ae74"),
            new UUID("d83fa0e5-97ed-7eb2-e798-7bd006215cb4"),
            new UUID("f061723d-0a18-754f-66ee-29a44795a32f"),
            new UUID("eefc79be-daae-a239-8c04-890f5d23654a"),
            new UUID("b312b10e-65ab-a0a4-8b3c-1326ea8e3ed9"),
            new UUID("17c024cc-eef2-f6a0-3527-9869876d7752"),
            new UUID("ec952cca-61ef-aa3b-2789-4d1344f016de"),
            new UUID("7a4e87fe-de39-6fcb-6223-024b00893244"),
            new UUID("f3300ad9-3462-1d07-2044-0fef80062da0"),
            new UUID("c8e42d32-7310-6906-c903-cab5d4a34656"),
            new UUID("36f81a92-f076-5893-dc4b-7c3795e487cf"),
            new UUID("49aea43b-5ac3-8a44-b595-96100af0beda"),
            new UUID("35db4f7e-28c2-6679-cea9-3ee108f7fc7f"),
            new UUID("0836b67f-7f7b-f37b-c00a-460dc1521f5a"),
            new UUID("42dd95d5-0bc6-6392-f650-777304946c0f"),
            new UUID("16803a9f-5140-e042-4d7b-d28ba247c325"),
            new UUID("05ddbff8-aaa9-92a1-2b74-8fe77a29b445"),
            new UUID("1ab1b236-cd08-21e6-0cbc-0d923fc6eca2"),
            new UUID("0eb702e2-cc5a-9a88-56a5-661a55c0676a"),
            new UUID("cd7668a6-7011-d7e2-ead8-fc69eff1a104"),
            new UUID("e04d450d-fdb5-0432-fd68-818aaf5935f8"),
            new UUID("6bd01860-4ebd-127a-bb3d-d1427e8e0c42"),
            new UUID("70ea714f-3a97-d742-1b01-590a8fcd1db5"),
            new UUID("1a5fe8ac-a804-8a5d-7cbd-56bd83184568"),
            new UUID("b1709c8d-ecd3-54a1-4f28-d55ac0840782"),
            new UUID("245f3c54-f1c0-bf2e-811f-46d8eeb386e7"),
            new UUID("1c7600d6-661f-b87b-efe2-d7421eb93c86"),
            new UUID("1a2bd58e-87ff-0df8-0b4c-53e047b0bb6e"),
            new UUID("a8dee56f-2eae-9e7a-05a2-6fb92b97e21e"),
            new UUID("f2bed5f9-9d44-39af-b0cd-257b2a17fe40"),
            new UUID("d2f2ee58-8ad1-06c9-d8d3-3827ba31567a"),
            new UUID("6802d553-49da-0778-9f85-1599a2266526"),
            new UUID("0a9fb970-8b44-9114-d3a9-bf69cfe804d6"),
            new UUID("eae8905b-271a-99e2-4c0e-31106afd100c"),
            new UUID("2408fe9e-df1d-1d7d-f4ff-1384fa7b350f"),
            new UUID("3da1d753-028a-5446-24f3-9c9b856d9422"),
            new UUID("15468e00-3400-bb66-cecc-646d7c14458e"),
            new UUID("370f3a20-6ca6-9971-848c-9a01bc42ae3c"),
            new UUID("42b46214-4b44-79ae-deb8-0df61424ff4b"),
            new UUID("f22fed8b-a5ed-2c93-64d5-bdd8b93c889f"),
            new UUID("80700431-74ec-a008-14f8-77575e73693f"),
            new UUID("1cb562b0-ba21-2202-efb3-30f82cdf9595"),
            new UUID("41426836-7437-7e89-025d-0aa4d10f1d69"),
            new UUID("313b9881-4302-73c0-c7d0-0e7a36b6c224"),
            new UUID("85428680-6bf9-3e64-b489-6f81087c24bd"),
            new UUID("5c682a95-6da4-a463-0bf6-0f5b7be129d1"),
            new UUID("11000694-3f41-adc2-606b-eee1d66f3724"),
            new UUID("aa134404-7dac-7aca-2cba-435f9db875ca"),
            new UUID("83ff59fe-2346-f236-9009-4e3608af64c1"),
            new UUID("56e0ba0d-4a9f-7f27-6117-32f2ebbf6135"),
            new UUID("2d6daa51-3192-6794-8e2e-a15f8338ec30"),
            new UUID("c541c47f-e0c0-058b-ad1a-d6ae3a4584d9"),
            new UUID("6ed24bd8-91aa-4b12-ccc7-c97c857ab4e0"),
            new UUID("33339176-7ddc-9397-94a4-bf3403cbc8f5"),
            new UUID("7693f268-06c7-ea71-fa21-2b30d6533f8f"),
            new UUID("b1ed7982-c68e-a982-7561-52a88a5298c0"),
            new UUID("869ecdad-a44b-671e-3266-56aef2e3ac2e"),
            new UUID("c0c4030f-c02b-49de-24ba-2331f43fe41c"),
            new UUID("9f496bd2-589a-709f-16cc-69bf7df1d36c"),
            new UUID("15dd911d-be82-2856-26db-27659b142875"),
            new UUID("b8c8b2a3-9008-1771-3bfc-90924955ab2d"),
            new UUID("42ecd00b-9947-a97c-400a-bbc9174c7aeb"),
            new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Constants.DefaultMaterialID
        };

    /// <summary>
    /// Is gathering complete?
    /// </summary>
        public bool Complete { get { return m_assetUuidsToInspect.Count <= 0; } }

        /// <summary>
        /// The dictionary of UUIDs gathered so far.  If Complete == true then this is all the reachable UUIDs.
        /// </summary>
        /// <value>The gathered uuids.</value>
        public IDictionary<UUID, sbyte> GatheredUuids { get; private set; }
        public HashSet<UUID> FailedUUIDs { get; private set; }
        public HashSet<UUID> UncertainAssetsUUIDs { get; private set; }
        public int possibleNotAssetCount { get; set; }
        public int ErrorCount { get; private set; }
        public int AssetGetCount;
        private bool verbose = true;

        /// <summary>
        /// Gets the next UUID to inspect.
        /// </summary>
        /// <value>If there is no next UUID then returns null</value>
        public UUID? NextUuidToInspect
        {
            get
            {
                return Complete ? null : m_assetUuidsToInspect.Peek();
            }
        }

        protected IAssetService m_assetService;

        protected Queue<UUID> m_assetUuidsToInspect;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Region.Framework.Scenes.UuidGatherer"/> class.
        /// </summary>
        /// <remarks>In this case the collection of gathered assets will start out blank.</remarks>
        /// <param name="assetService">
        /// Asset service.
        /// </param>
        public UuidGatherer(IAssetService assetService) : this(assetService, new Dictionary<UUID, sbyte>(),
                new HashSet <UUID>(),new HashSet <UUID>()) {}
        public UuidGatherer(IAssetService assetService, IDictionary<UUID, sbyte> collector) : this(assetService, collector,
            new HashSet <UUID>(), new HashSet <UUID>()) {}

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Region.Framework.Scenes.UuidGatherer"/> class.
        /// </summary>
        /// <param name="assetService">
        /// Asset service.
        /// </param>
        /// <param name="collector">
        /// Gathered UUIDs will be collected in this dictionary.
        /// It can be pre-populated if you want to stop the gatherer from analyzing assets that have already been fetched and inspected.
        /// </param>
        public UuidGatherer(IAssetService assetService, IDictionary<UUID, sbyte> collector, HashSet <UUID> failedIDs, HashSet <UUID> uncertainAssetsUUIDs)
        {
            m_assetService = assetService;
            GatheredUuids = collector;

            // FIXME: Not efficient for searching, can improve.
            m_assetUuidsToInspect = new Queue<UUID>();
            FailedUUIDs = failedIDs;
            UncertainAssetsUUIDs = uncertainAssetsUUIDs;
            ErrorCount = 0;
            possibleNotAssetCount = 0;
            AssetGetCount = 0;
        }

        public bool AddGathered(UUID uuid, sbyte type)
        {
            if (uuid.IsZero())
                return false;

            if (ToSkip.Contains(uuid))
                return false;

            if (FailedUUIDs.Contains(uuid))
            {
                if (UncertainAssetsUUIDs.Contains(uuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return false;
            }
            if (GatheredUuids.ContainsKey(uuid))
                return false;
            if (m_assetUuidsToInspect.Contains(uuid))
                return false;

            //m_log.DebugFormat("[UUID GATHERER]: Adding asset {0} for inspection", uuid);

            GatheredUuids[uuid] = type; 
            return true;
        }

        /// <summary>
        /// Adds the asset uuid for inspection during the gathering process.
        /// </summary>
        /// <returns><c>true</c>, if for inspection was added, <c>false</c> otherwise.</returns>
        /// <param name="uuid">UUID.</param>
        public bool AddForInspection(UUID uuid)
        {
            if(uuid.IsZero())
                return false;

            if(ToSkip.Contains(uuid))
                return false;

            if(FailedUUIDs.Contains(uuid))
            {
                if(UncertainAssetsUUIDs.Contains(uuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return false;
            }
            if(GatheredUuids.ContainsKey(uuid))
                return false;
            if (m_assetUuidsToInspect.Contains(uuid))
                return false;

            //m_log.DebugFormat("[UUID GATHERER]: Adding asset {0} for inspection", uuid);

            m_assetUuidsToInspect.Enqueue(uuid);
            return true;
        }

        /// <summary>
        /// Gather all the asset uuids associated with a given object.
        /// </summary>
        /// <remarks>
        /// This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </remarks>
        /// <param name="sceneObject">The scene object for which to gather assets</param>
        public void AddForInspection(SceneObjectGroup sceneObject)
        {
            //m_log.DebugFormat(
            //    "[UUID GATHERER]: Getting assets for object {0}, {1}", sceneObject.Name, sceneObject.UUID);
            if(sceneObject.IsDeleted)
                return;

            SceneObjectPart[] parts = sceneObject.Parts;
            for (int i = 0; i < parts.Length; ++i)
            {
                SceneObjectPart part = parts[i];

                // m_log.DebugFormat(
                // "[UUID GATHERER]: Getting part {0}, {1} for object {2}", part.Name, part.UUID, sceneObject.UUID);

                try
                {
                    Primitive.TextureEntry textureEntry = part.Shape.Textures;
                    if (textureEntry is not null)
                    {
                        // Get the prim's default texture.  This will be used for faces which don't have their own texture
                        if (textureEntry.DefaultTexture is not null)
                            RecordTextureEntryAssetUuids(textureEntry.DefaultTexture);

                        if (textureEntry.FaceTextures is not null)
                        {
                            // Loop through the rest of the texture faces (a non-null face means the face is different from DefaultTexture)
                            int nsides = part.GetNumberOfSides();
                            foreach (Primitive.TextureEntryFace texture in textureEntry.FaceTextures)
                            {
                                if (texture is not null)
                                    RecordTextureEntryAssetUuids(texture);
                                if(--nsides <= 0)
                                    break;
                            }
                        }
                    }

                    if (part.Shape.SculptTexture.IsNotZero())
                        GatheredUuids[part.Shape.SculptTexture] = (sbyte)AssetType.Texture;

                    if (part.Shape.ProjectionTextureUUID.IsNotZero())
                        GatheredUuids[part.Shape.ProjectionTextureUUID] = (sbyte)AssetType.Texture;

                    if(part.Shape.RenderMaterials is not null)
                    {
                        if (part.Shape.RenderMaterials.entries is not null)
                        {
                            for (int j = 0; j < part.Shape.RenderMaterials.entries.Length; ++j)
                            {
                                if (part.Shape.RenderMaterials.entries[j].id.IsNotZero())
                                    AddForInspection(part.Shape.RenderMaterials.entries[j].id, (sbyte)AssetType.Material);
                            }
                        }
                        if (part.Shape.RenderMaterials.overrides is not null)
                        {
                            for (int j = 0; j < part.Shape.RenderMaterials.overrides.Length; ++j)
                            {
                                if(!string.IsNullOrEmpty(part.Shape.RenderMaterials.overrides[j].data))
                                    RecordEmbeddedAssetDataUuids(part.Shape.RenderMaterials.overrides[j].data);
                            }
                        }
                    }
                    UUID collisionSound = part.CollisionSound;
                    if (collisionSound.IsNotZero() && collisionSound.NotEqual(part.invalidCollisionSoundUUID))
                        GatheredUuids[collisionSound] = (sbyte)AssetType.Sound;

                    UUID soundID = part.Sound;
                    if (soundID.IsNotZero())
                        GatheredUuids[soundID] = (sbyte)AssetType.Sound;

                    if (part.ParticleSystem.Length > 0)
                    {
                        try
                        {
                            Primitive.ParticleSystem ps = new(part.ParticleSystem, 0);
                            if (ps.Texture.IsNotZero())
                                GatheredUuids[ps.Texture] = (sbyte)AssetType.Texture;
                        }
                        catch (Exception)
                        {
                            m_log.WarnFormat(
                                "[UUID GATHERER]: Could not check particle system for part {0} {1} in object {2} {3} since it is corrupt.  Continuing.",
                                part.Name, part.UUID, sceneObject.Name, sceneObject.UUID);
                        }
                    }

                    List<TaskInventoryItem> items = part.TaskInventory.GetItems();
                    // Now analyze this prim's inventory items to preserve all the uuids that they reference
                    for(int j = 0; j < items.Count; ++j)
                    {
                        TaskInventoryItem tii = items[j];
                        AddForInspection(tii.AssetID, (sbyte)tii.Type);
                    }

                    if(part.Animations is not null && part.Animations.Count > 0)
                    {
                        foreach(UUID id in part.Animations.Keys)
                        {
                            if(id.IsNotZero() && !ToSkip.Contains(id) && !FailedUUIDs.Contains(id))
                            {
                                GatheredUuids[id] = (sbyte)AssetType.Animation;
                            }
                        }
                    }

                    RecordMaterialsUuids(part);
                }
                catch (Exception e)
                {
                    m_log.Error($"[UUID GATHERER]: Failed to get part - {e.Message}");
                }
            }
            if(sceneObject.TemporaryInstance)
                sceneObject.Dispose();
        }

        /// <summary>
        /// Gathers the next set of assets returned by the next uuid to get from the asset service.
        /// </summary>
        /// <returns>false if gathering is already complete, true otherwise</returns>
        public bool GatherNext()
        {
            if (Complete)
                return false;

            UUID nextToInspect = m_assetUuidsToInspect.Dequeue();

//            m_log.DebugFormat("[UUID GATHERER]: Inspecting asset {0}", nextToInspect);

            GetAssetUuids(nextToInspect);

            return m_assetUuidsToInspect.Count > 0;
        }

        /// <summary>
        /// Gathers all remaining asset UUIDS no matter how many calls are required to the asset service.
        /// </summary>
        /// <returns>false if gathering is already complete, true otherwise</returns>
        public bool GatherAll(bool report = false)
        {
            if (Complete)
                return false;
            if(report)
                verbose = false;

            while (GatherNext());

            if (report && FailedUUIDs.Count > 0)
            {
                StringBuilder sb = new(512);
                int i = FailedUUIDs.Count;
                sb.Append("[UUID GATHERER]: UUIDs that are not assets or really missing assets:\n\t");
                foreach (UUID id in FailedUUIDs)
                {
                    sb.Append(id);
                    if (--i > 0)
                        sb.Append(',');
                }
                m_log.Debug(sb.ToString());
            }

            return true;
        }

        /// <summary>
        /// Gather all the asset uuids associated with the asset referenced by a given uuid
        /// </summary>
        /// <remarks>
        /// This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// This method assumes that the asset type associated with this asset in persistent storage is correct (which
        /// should always be the case).  So with this method we always need to retrieve asset data even if the asset
        /// is of a type which is known not to reference any other assets
        /// </remarks>
        /// <param name="assetUuid">The uuid of the asset for which to gather referenced assets</param>
        private void GetAssetUuids(UUID assetUuid)
        {
            if(assetUuid.IsZero())
                return;

            if(FailedUUIDs.Contains(assetUuid))
            {
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }

            if (GatheredUuids.ContainsKey(assetUuid))
                return;

            AssetBase assetBase;
            try
            {
                assetBase = GetAsset(assetUuid);
            }
            catch (Exception e)
            {
                if(verbose)
                    m_log.Error($"[UUID GATHERER]: Failed to get asset {assetUuid} : {e.Message}");
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
                return;
            }

            if(assetBase == null)
            {
//                m_log.ErrorFormat("[UUID GATHERER]: asset {0} not found", assetUuid);
                FailedUUIDs.Add(assetUuid);
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }

            ++AssetGetCount;

            if(UncertainAssetsUUIDs.Contains(assetUuid))
                UncertainAssetsUUIDs.Remove(assetUuid);


            if(assetBase.Data == null || assetBase.Data.Length == 0)
            {
//                m_log.ErrorFormat("[UUID GATHERER]: asset {0}, type {1} has no data", assetUuid, assetType);
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
                return;
            }

            sbyte assetType = assetBase.Type;
            GatheredUuids[assetUuid] = assetType;

            if (assetBase.Data.Length < 36)
                return;

            try
            {
                switch ((AssetType)assetType)
                {
                    case AssetType.Bodypart:
                    case AssetType.Clothing:
                        RecordWearableAssetUuids(assetBase);
                        break;
                    case AssetType.Gesture:
                        RecordGestureAssetUuids(assetBase);
                        break;
                    case AssetType.Notecard:
                        RecordNoteCardEmbeddedAssetUuids(assetBase);
                        break;
                    case AssetType.LSLText:
                        RecordEmbeddedAssetDataUuids(assetBase);
                        break;
                    case (AssetType)AssetType.OSMaterial:
                        RecordMaterialAssetUuids(assetBase);
                        break;
                    case AssetType.Object:
                        RecordSceneObjectAssetUuids(assetBase);
                        break;
                    case AssetType.Settings:
                        RecordEmbeddedAssetDataUuids(assetBase); // BAD to do
                        break;
                    case AssetType.Material:
                        RecordEmbeddedAssetDataUuids(assetBase);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                if(verbose)
                    m_log.Error($"[UUID GATHERER]: Failed to gather uuids for asset with id {assetUuid} type {assetType}: {e.Message}");
                GatheredUuids.Remove(assetUuid);
                ErrorCount++;
                FailedUUIDs.Add(assetUuid);
            }
        }

        private void AddForInspection(UUID assetUuid, sbyte assetType)
        {
            if(assetUuid.IsZero())
                return;

            // Here, we want to collect uuids which require further asset fetches but mark the others as gathered
            if(FailedUUIDs.Contains(assetUuid))
            {
                if(UncertainAssetsUUIDs.Contains(assetUuid))
                    possibleNotAssetCount++;
                else
                    ErrorCount++;
                return;
            }
            if(GatheredUuids.ContainsKey(assetUuid))
                return;
            try
            {
                switch (assetType)
                {
                    case (sbyte)AssetType.Bodypart:
                    case (sbyte)AssetType.Clothing:
                    case (sbyte)AssetType.Gesture:
                    case (sbyte)AssetType.Notecard:
                    case (sbyte)AssetType.LSLText:
                    case (sbyte)AssetType.OSMaterial:
                    case (sbyte)AssetType.Object:
                    case (sbyte)AssetType.Settings:
                    case (sbyte)AssetType.Material:
                        AddForInspection(assetUuid);
                        break;

                    default:
                        GatheredUuids[assetUuid] = assetType;
                        break;
                }
            }
            catch (Exception)
            {
                m_log.Error(
                    $"[UUID GATHERER]: Failed to gather uuids for asset id {assetUuid}, type {assetType}");
                throw;
            }
        }

        /// <summary>
        /// Collect all the asset uuids found in one face of a Texture Entry.
        /// </summary>
        private void RecordTextureEntryAssetUuids(Primitive.TextureEntryFace texture)
        {
            UUID teid = texture.TextureID;
            if (!teid.IsZero() &&
                !ToSkip.Contains(teid) &&
                !FailedUUIDs.Contains(teid))
            {
                GatheredUuids[teid] = (sbyte)AssetType.Texture;
            }

            if (!texture.MaterialID.IsZero())
                AddForInspection(texture.MaterialID);
        }

        /// <summary>
        /// Gather all of the texture asset UUIDs used to reference "Materials" such as normal and specular maps
        /// stored in legacy format in part.DynAttrs
        /// </summary>
        /// <param name="part"></param>
        private void RecordMaterialsUuids(SceneObjectPart part)
        {
            // scan thru the dynAttrs map of this part for any textures used as materials
            if(part.DynAttrs == null)
                return;

            lock (part.DynAttrs)
            {
                if (!part.DynAttrs.TryGetStore("OpenSim", "Materials", out OSDMap materialsStore))
                    return;

                if (materialsStore == null)
                    return;

                materialsStore.TryGetValue("Materials", out OSD osdMaterials);

                if (osdMaterials is OSDArray matsArr)
                {
                    //m_log.Info("[UUID Gatherer]: found Materials: " + OSDParser.SerializeJsonString(osd));

                    foreach (OSDMap matMap in matsArr)
                    {
                        try
                        {
                            if (matMap.ContainsKey("Material"))
                            {
                                OSDMap mat = matMap["Material"] as OSDMap;
                                if (mat.TryGetValue("NormMap", out OSD tmap))
                                {
                                    UUID normalMapId = tmap.AsUUID();
                                    if (normalMapId.IsNotZero())
                                    {
                                        GatheredUuids[normalMapId] = (sbyte)AssetType.Texture;
                                        //m_log.Info("[UUID Gatherer]: found normal map ID: " + normalMapId.ToString());
                                    }
                                }
                                if (mat.TryGetValue("SpecMap", out OSD tsmap))
                                {
                                    UUID specularMapId = tsmap.AsUUID();
                                    if (specularMapId.IsNotZero())
                                    {
                                        GatheredUuids[specularMapId] = (sbyte)AssetType.Texture;
                                        //m_log.Info("[UUID Gatherer]: found specular map ID: " + specularMapId.ToString());
                                    }
                                }
                            }

                        }
                        catch (Exception e)
                        {
                            m_log.Warn($"[UUID Gatherer]: exception getting materials: {e.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get an asset synchronously, potentially using an asynchronous callback.  If the
        /// asynchronous callback is used, we will wait for it to complete.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual AssetBase GetAsset(UUID uuid)
        {
            return m_assetService.Get(uuid.ToString());
        }

        /// <summary>
        /// Record the asset uuids embedded within the given text (e.g. a script).
        /// </summary>
        /// <param name="textAsset"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordEmbeddedAssetDataUuids(AssetBase textAsset)
        {
            RecordEmbeddedAssetDataUuids(new osUTF8(textAsset.Data));
        }

        private unsafe void RecordEmbeddedAssetDataUuids(osUTF8 data)
        {
            //if (data.Length < 36)
            //    return;

            int indx = 8;
            while(indx < data.Length - 28)
            {
                if (data[indx] == (byte)'-')
                {
                    if (osUTF8Slice.TryParseUUID(new osUTF8Slice(data.GetArray(), indx - 8 , 36), out UUID id))
                    {
                        if (id.IsNotZero())
                        {
                            UncertainAssetsUUIDs.Add(id);
                            AddForInspection(id);
                        }
                        indx += 37;
                    }
                    else
                        indx += 9;
                }
                else
                    indx++;
            }
        }

        private void RecordEmbeddedAssetDataUuids(ReadOnlySpan<char> data)
        {
            if (data.Length < 36)
                return;

            int indx = 8;
            while (indx < data.Length - 28)
            {
                if (data[indx] == '-')
                {
                    if (UUID.TryParse(data.Slice(indx - 8, 36), out UUID id))
                    {
                        if (id.IsNotZero())
                        {
                            UncertainAssetsUUIDs.Add(id);
                            AddForInspection(id);
                        }
                        indx += 37;
                    }
                    else
                        indx += 9;
                }
                else
                    indx++;
            }
        }

        private void RecordNoteCardEmbeddedAssetUuids(AssetBase textAsset)
        {
            List<UUID> ids = SLUtil.GetEmbeddedAssetIDs(textAsset.Data);
            if(ids is null)
                return;

            for(int i = 0; i < ids.Count; ++i)
            {
                if (ids[i].IsZero())
                    continue;
                UncertainAssetsUUIDs.Add(ids[i]);
                AddForInspection(ids[i]);
            }
        }

        private static readonly byte[] wearableSeps = new byte[]{(byte)' ', (byte)'\t'};
        /// <summary>
        /// Record the uuids referenced by the given wearable asset
        /// </summary>
        /// <param name="asset"></param>
        private void RecordWearableAssetUuids(AssetBase asset)
        {
            if (asset.Data is null || asset.Data.Length < 64)
                return;
            try
            {
                osUTF8Slice ostmp = new(asset.Data);
                if (!ostmp.SkipLine()) // version
                    return;
                if (!ostmp.SkipLine()) // name
                    return;
                if (!ostmp.SkipLine()) // description
                    return;
                if (!ostmp.SkipLine())
                    return;

                while (ostmp.ReadLine(out osUTF8Slice line))
                {
                    line.SelfTrim(wearableSeps);
                    osUTF8Slice[] parts = line.Split(wearableSeps);
                    if(parts[0].Length == 0)
                        continue;
                    parts[0].SelfTrim(wearableSeps);
                    if (parts[0].Equals(parametersB))
                    {
                        if (parts[1].Length == 0)
                            return;
                        parts[1].SelfTrim(wearableSeps);
                        if (!osUTF8Slice.TryParseInt(parts[1], out int count))
                            return;
                        for (int i = 0; i < count; ++i)
                        {
                            if (!ostmp.SkipLine())
                                return;
                        }
                    }
                    else if (parts[0].Equals(texturesB))
                    {
                        if(parts[1].Length == 0)
                            return;
                        parts[1].SelfTrim(wearableSeps);
                        if (!osUTF8Slice.TryParseInt(parts[1], out int count) || count == 0)
                            return;
                        for(int i = 0; i < count; ++i)
                        {
                            if(!ostmp.ReadLine(out osUTF8Slice texline))
                                return;
                            texline.SelfTrim(wearableSeps);
                            osUTF8Slice[] texparts = texline.Split(wearableSeps);
                            if(texparts.Length <2 || texparts[1].Length < 36)
                                continue;
                            texparts[1].SelfTrim(wearableSeps);
                            if (UUID.TryParse(texparts[1].ToString(), out UUID id) && id.IsNotZero())
                                GatheredUuids[id] = (sbyte)AssetType.Texture;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private int getxmlNode(osUTF8Slice data, out osUTF8Slice h)
        {
            h = data;
            int st;
            while ((st = data.IndexOf('<')) >= 0)
            {
                if (st > 0 && data[st - 1] == (byte)'\\')
                    data.SubUTF8Self(st + 1);
                break;
            }
            if (st < 0)
                return -1;
            ++st;
            int ed;
            while ((ed = data.IndexOf('>')) >= 0)
            {
                if (data[st - 1] == (byte)'\\')
                    data.SubUTF8Self(st + 1);
                break;
            }
            if (ed < 0)
                return -1;
            h = data.SubUTF8(st, ed - st);
            h.SelfTrim();
            ++ed;
            data.SubUTF8Self(ed);
            return ed;
        }

        private bool TryGetxmlUUIDValue(osUTF8Slice data, out UUID id)
        {
            id = UUID.Zero;
            if(getxmlNode(data, out osUTF8Slice h) < 0)
                return false;

            if (h.StartsWith(UUIDB))
            {
                if (h.EndsWith((byte)'/'))
                    return true;
                int indx = data.IndexOf((byte)'<');
                if (indx < 0)
                    return false;

                osUTF8Slice tmp = data.SubUTF8(0, indx);
                data.SubUTF8Self(indx);

                return osUTF8Slice.TryParseUUID(tmp, out id);
            }

            if (h.StartsWith(uuidB))
            {
                if (h.EndsWith((byte)'/'))
                    return true;
                int indx = data.IndexOf((byte)'<');
                if (indx < 0)
                    return false;

                osUTF8Slice tmp = data.SubUTF8(0, indx);
                data.SubUTF8Self(indx);

                return osUTF8Slice.TryParseUUID(tmp, out id);
            }

            return false;
        }

        private bool TryGetXMLBinary(osUTF8Slice data, out byte[] te)
        {
            te = null;
            int indx = data.IndexOf((byte)'<');
            if(indx <= 0)
                return false;

            osUTF8Slice tmp = data.SubUTF8(0, indx);
            data.SubUTF8Self(indx);

            tmp.SelfTrim();
            if(tmp.Length == 0)
                return false;
            try
            {
                te = Convert.FromBase64String(tmp.ToString()); // need to replace
                return true;
            }
            catch { }

            return false;
        }

        // bad ugly
        private static readonly byte[] UUIDB = osUTF8.GetASCIIBytes("UUID");
        private static readonly byte[] uuidB = osUTF8.GetASCIIBytes("uuid");
        private static readonly byte[] SOPAnimsB = osUTF8.GetASCIIBytes("SOPAnims");
        private static readonly byte[] CollisionSoundB = osUTF8.GetASCIIBytes("CollisionSound");
        private static readonly byte[] SoundIDB = osUTF8.GetASCIIBytes("SoundID");
        private static readonly byte[] SculptTextureB = osUTF8.GetASCIIBytes("SculptTexture");
        private static readonly byte[] ExtraParamsB = osUTF8.GetASCIIBytes("ExtraParams");
        private static readonly byte[] ParticleSystemB = osUTF8.GetASCIIBytes("ParticleSystem");
        private static readonly byte[] TextureEntryB = osUTF8.GetASCIIBytes("TextureEntry");
        private static readonly byte[] TaskInventoryB = osUTF8.GetASCIIBytes("TaskInventory");
        private static readonly byte[] endTaskInventoryB = osUTF8.GetASCIIBytes("/TaskInventory");
        private static readonly byte[] AssetIDB = osUTF8.GetASCIIBytes("AssetID");
        private static readonly byte[] texturesB = osUTF8.GetASCIIBytes("textures");
        private static readonly byte[] parametersB = osUTF8.GetASCIIBytes("parameters");
        private static readonly byte[] MatOvrdB = osUTF8.GetASCIIBytes("MatOvrd");

        /// <summary>
        /// Get all the asset uuids associated with a given object.  This includes both those directly associated with
        /// it (e.g. face textures) and recursively, those of items within it's inventory (e.g. objects contained
        /// within this object).
        /// </summary>
        /// <param name="sceneObjectAsset"></param>
        private void RecordSceneObjectAssetUuids(AssetBase sceneObjectAsset)
        {
            osUTF8Slice data = new(sceneObjectAsset.Data);

            while (getxmlNode(data, out osUTF8Slice nodeName) > 0)
            {
                if (nodeName.StartsWith((byte)'/'))
                    continue;
                if (nodeName.StartsWith(SOPAnimsB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;

                    if (TryGetXMLBinary(data, out byte[] abytes) && abytes != null && abytes.Length > 16)
                    {
                        try
                        {
                            int count = Utils.BytesToUInt16(abytes, 0);
                            if (count  >0)
                            {
                                int pos = 2;
                                while (--count >= 0)
                                {
                                    UUID id = new(abytes, pos);
                                    if (id.IsZero())
                                        break;
                                    if (!ToSkip.Contains(id) &&
                                        !FailedUUIDs.Contains(id))
                                    {
                                        GatheredUuids[id] = (sbyte)AssetType.Animation;
                                    }
                                    pos += 16;
                                    int strlen = data[pos++];
                                    pos += strlen;
                                }
                            }
                            abytes = null;
                        }
                        catch { }
                    }
                }
                else if (nodeName.StartsWith(CollisionSoundB))
                {
                    if (!nodeName.EndsWith((byte)'d'))
                        continue;
                    if (TryGetxmlUUIDValue(data, out UUID id) && id.IsNotZero())
                        GatheredUuids[id] = (sbyte)AssetType.Sound;
                }
                else if (nodeName.StartsWith(SoundIDB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;
                    if (TryGetxmlUUIDValue(data, out UUID id) && id.IsNotZero())
                        GatheredUuids[id] = (sbyte)AssetType.Sound;
                }
                else if (nodeName.StartsWith(SculptTextureB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;
                    if (TryGetxmlUUIDValue(data, out UUID id) && id.IsNotZero())
                        GatheredUuids[id] = (sbyte)AssetType.Texture; // can be mesh but no prob
                }
                else if (nodeName.StartsWith(ExtraParamsB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;

                    if (TryGetXMLBinary(data, out byte[] exbytes) && exbytes != null && exbytes.Length > 16)
                    {
                        try
                        {
                            PrimitiveBaseShape ps = new();
                            ps.ReadInExtraParamsBytes(exbytes);
                            UUID teid = ps.ProjectionTextureUUID;
                            if (teid.IsNotZero() &&
                                !ToSkip.Contains(teid) &&
                                !FailedUUIDs.Contains(teid))
                            {
                                GatheredUuids[teid] = (sbyte)AssetType.Texture;
                            }
                            if(ps.RenderMaterials is not null)
                            { 
                                if (ps.RenderMaterials.entries is not null)
                                {   
                                    for (int j = 0; j < ps.RenderMaterials.entries.Length; ++j)
                                    {
                                        if (ps.RenderMaterials.entries[j].id.IsNotZero())
                                            AddForInspection(ps.RenderMaterials.entries[j].id, (sbyte)AssetType.Material);
                                    }
                                }
                            }
                            /* multiple store
                            teid = ps.SculptTexture; //??
                            if (teid != UUID.Zero &&
                                !ToSkip.Contains(teid) &&
                                !FailedUUIDs.Contains(teid))
                            {
                                GatheredUuids[teid] = (sbyte)AssetType.Texture;
                            }
                            */
                            ps = null;
                            exbytes = null;
                        }
                        catch { }
                    }
                }
                else if (nodeName.StartsWith(ParticleSystemB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;

                    if (TryGetXMLBinary(data, out byte[] psbytes) && psbytes != null && psbytes.Length > 16)
                    {
                        try
                        {
                            Primitive.ParticleSystem ps = new(psbytes, 0);
                            UUID teid = ps.Texture;
                            if (teid.IsNotZero() &&
                                !ToSkip.Contains(teid) &&
                                !FailedUUIDs.Contains(teid))
                            {
                                GatheredUuids[teid] = (sbyte)AssetType.Texture;
                            }
                            psbytes = null;
                        }
                        catch { }
                    }
                }
                else if (nodeName.StartsWith(TextureEntryB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;

                    if (TryGetXMLBinary(data, out byte[] tebytes) && tebytes != null && tebytes.Length > 16)
                    {
                        try
                        {
                            Primitive.TextureEntry te = new(tebytes, 0, tebytes.Length);
                            if (te is not null)
                            {
                                // Get the prim's default texture.  This will be used for faces which don't have their own texture
                                if (te.DefaultTexture is not null)
                                    RecordTextureEntryAssetUuids(te.DefaultTexture);

                                if (te.FaceTextures is not null)
                                {
                                    // Loop through the rest of the texture faces (a non-null face means the face is different from DefaultTexture)
                                    foreach (Primitive.TextureEntryFace texture in te.FaceTextures)
                                    {
                                        if (texture is not null)
                                            RecordTextureEntryAssetUuids(texture);
                                    }
                                }
                            }
                            te = null;
                        }
                        catch { }
                    }
                }
                else if (nodeName.StartsWith(TaskInventoryB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;
                    while (getxmlNode(data, out nodeName) > 0)
                    {
                        if (nodeName.StartsWith(AssetIDB))
                        {
                            if (TryGetxmlUUIDValue(data, out UUID id) && id.IsNotZero())
                                AddForInspection(id);
                        }
                        else if (nodeName.StartsWith(endTaskInventoryB))
                            break;
                    }
                }
                else if (nodeName.StartsWith(MatOvrdB))
                {
                    if (nodeName.EndsWith((byte)'/'))
                        continue;

                    if (TryGetXMLBinary(data, out byte[] ovrbytes) && ovrbytes != null && ovrbytes.Length > 36)
                    {
                        RecordEmbeddedAssetDataUuids(new osUTF8(ovrbytes));
                    }
                }
            }
        }

        /// <summary>
        /// Get the asset uuid associated with a gesture
        /// </summary>
        /// <param name="gestureAsset"></param>
        private void RecordGestureAssetUuids(AssetBase gestureAsset)
        {
            osUTF8Slice osdata = new(gestureAsset.Data);

            if (!osdata.SkipLine()) // version
                return;
            if (!osdata.SkipLine()) // key
                return;
            if (!osdata.SkipLine()) // mask
                return;
            if (!osdata.SkipLine()) // trigger
                return;
            if (!osdata.SkipLine()) // replace
                return;

            if (!osdata.ReadLine(out osUTF8Slice line))
                return;

            if(!osUTF8Slice.TryParseInt(line, out int scount) || scount == 0)
                return;

            for(int i = 0; i < scount; ++i)
            {
                if (!osdata.ReadLine(out osUTF8Slice typeline)) // type
                    return;
                typeline.SelfTrim();
                if (!osUTF8Slice.TryParseInt(typeline, out int type))
                    return;

                osUTF8Slice id;
                switch(type)
                {
                    case 0: // animation
                    case 1: // sound
                        if (!osdata.SkipLine()) // name
                            return;
                        if (!osdata.ReadLine(out id)) // uuid
                            return;
                        if (osUTF8Slice.TryParseUUID(id, out UUID uid) && uid.IsNotZero())
                            GatheredUuids[uid] = type == 0 ? (sbyte)AssetType.Animation : (sbyte)AssetType.Sound;
                        if (!osdata.SkipLine()) // flags 
                            return;
                        break;
                    case 2: // chat
                    case 3: // wait
                        if (!osdata.SkipLine()) // chat text or wait time
                            return;
                        if (!osdata.SkipLine()) // flags 
                            return;
                        break;
                    default:
                        return; // no idea
                }
            }
        }

        /// <summary>
        /// Get the asset uuid's referenced in a material.
        /// </summary>
        private void RecordMaterialAssetUuids(AssetBase materialAsset)
        {
            osUTF8Slice data = new(materialAsset.Data);
            while (getxmlNode(data, out osUTF8Slice header) > 0)
            {
                if (header.StartsWith((byte)'/'))
                    continue;
                if (header.StartsWith(uuidB))
                {
                    if(header.EndsWith((byte)'/'))
                        continue;
                    int indx = data.IndexOf((byte)'<');
                    if(indx < 0)
                        continue;
                    osUTF8Slice tmp = data.SubUTF8(0, indx);
                    if(osUTF8Slice.TryParseUUID(tmp, out UUID id) && id.IsNotZero())
                        GatheredUuids[id] = (sbyte)AssetType.Texture;
                    data.SubUTF8Self(indx);
                }
            }
        }
    }

    public class HGUuidGatherer : UuidGatherer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_assetServerURL;

        public HGUuidGatherer(IAssetService assetService, string assetServerURL)
            : this(assetService, assetServerURL, new Dictionary<UUID, sbyte>()) {}

        public HGUuidGatherer(IAssetService assetService, string assetServerURL, IDictionary<UUID, sbyte> collector)
            : base(assetService, collector)
        {
            m_assetServerURL = assetServerURL;
        }

        protected override AssetBase GetAsset(UUID uuid)
        {
            if (string.IsNullOrWhiteSpace(m_assetServerURL))
                return base.GetAsset(uuid);
            else
                return FetchAsset(uuid);
        }

        public AssetBase FetchAsset(UUID assetID)
        {
            string IDstr = assetID.ToString();
            AssetBase asset = m_assetService.Get(IDstr, m_assetServerURL, true);
            if (asset is null)
                m_log.Debug($"[HGUUIDGatherer]: Failed to fetch asset {IDstr} from {m_assetServerURL}");
            else
                m_log.Debug($"[HGUUIDGatherer]: Copied asset {IDstr} from {m_assetServerURL} to local asset server");

            return asset;
        }
    }
}
