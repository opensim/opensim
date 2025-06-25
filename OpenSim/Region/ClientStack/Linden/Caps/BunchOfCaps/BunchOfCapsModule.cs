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

using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

[assembly: Addin("LindenCaps", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
namespace OpenSim.Region.ClientStack.Linden
{
    public class BunchOfCapsConfigOptions
    {
        public ModelCost ModelCost;

        public bool persistBakedTextures = false;
        public bool enableModelUploadTextureToInventory = true; // place uploaded textures also in inventory
                                                                // may not be visible till relog
        public bool enableFreeTestUpload = false; // allows "TEST-" prefix hack
        public bool ForceFreeTestUpload = false; // forces all uploads to be test

        public bool RestrictFreeTestUploadPerms = false; // reduces also the permitions. Needs a creator defined!!

        public int levelUpload = 0;

        public bool AllowCapHomeLocation = true;
        public bool AllowCapGroupMemberData = true;
        public bool AllowCapLandResources = true;
        public bool AllowCapAttachmentResources = true;

        public UUID testAssetsCreatorID = UUID.Zero;
    }

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BunchOfCapsModule")]
    public class BunchOfCapsModule : INonSharedRegionModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;
        private BunchOfCapsConfigOptions ConfigOptions = new();

        #region INonSharedRegionModule

        public string Name { get { return "BunchOfCapsModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
        }

        public void Close() { }

        public void AddRegion(Scene scene)
        {
            m_Scene = scene;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            ConfigOptions.ModelCost = new ModelCost(m_Scene);

            IConfigSource config = m_Scene.Config;
            if (config is not null)
            {
                IConfig sconfig = config.Configs["Startup"];
                if (sconfig is not null)
                    ConfigOptions.levelUpload = sconfig.GetInt("LevelUpload", 0);

                if (ConfigOptions.levelUpload == 0)
                {
                    IConfig pconfig = config.Configs["Permissions"];
                    if (pconfig is not null)
                        ConfigOptions.levelUpload = pconfig.GetInt("LevelUpload", 0);
                }

                IConfig appearanceConfig = config.Configs["Appearance"];
                if (appearanceConfig is not null)
                {
                    ConfigOptions.persistBakedTextures = appearanceConfig.GetBoolean("PersistBakedTextures", ConfigOptions.persistBakedTextures);
                }
                // economy for model upload
                IConfig EconomyConfig = config.Configs["Economy"];
                if (EconomyConfig is not null)
                {
                    ConfigOptions.ModelCost.Econfig(EconomyConfig);

                    ConfigOptions.enableModelUploadTextureToInventory =
                        EconomyConfig.GetBoolean("MeshModelAllowTextureToInventory", ConfigOptions.enableModelUploadTextureToInventory);

                    ConfigOptions.RestrictFreeTestUploadPerms =
                        EconomyConfig.GetBoolean("m_RestrictFreeTestUploadPerms", ConfigOptions.RestrictFreeTestUploadPerms);

                    ConfigOptions.enableFreeTestUpload = EconomyConfig.GetBoolean("AllowFreeTestUpload", ConfigOptions.enableFreeTestUpload);

                    ConfigOptions.ForceFreeTestUpload =
                        EconomyConfig.GetBoolean("ForceFreeTestUpload", ConfigOptions.ForceFreeTestUpload);

                    string testcreator = EconomyConfig.GetString("TestAssetsCreatorID", "");
                    if (!string.IsNullOrEmpty(testcreator))
                    {
                        if (UUID.TryParse(testcreator, out UUID id))
                            ConfigOptions.testAssetsCreatorID = id;
                    }
                }

                IConfig CapsConfig = config.Configs["ClientStack.LindenCaps"];
                if (CapsConfig is not null)
                {
                    string homeLocationUrl = CapsConfig.GetString("Cap_HomeLocation", "localhost");
                    ConfigOptions.AllowCapHomeLocation = !string.IsNullOrEmpty(homeLocationUrl);

                    string GroupMemberDataUrl = CapsConfig.GetString("Cap_GroupMemberData", "localhost");
                    ConfigOptions.AllowCapGroupMemberData = !string.IsNullOrEmpty(GroupMemberDataUrl);

                    string LandResourcesUrl = CapsConfig.GetString("Cap_LandResources", "localhost");
                    ConfigOptions.AllowCapLandResources = !string.IsNullOrEmpty(LandResourcesUrl);

                    string AttachmentResourcesUrl = CapsConfig.GetString("Cap_AttachmentResources", "localhost");
                    ConfigOptions.AllowCapAttachmentResources = !string.IsNullOrEmpty(AttachmentResourcesUrl);
                }

                m_Scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            }
        }

        public void PostInitialise() { }
        #endregion

        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            _ = new BunchOfCaps(m_Scene, agentID, caps, ConfigOptions);
        }
    }
}
