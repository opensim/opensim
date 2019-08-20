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
using System.IO;
using System.Reflection;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Asset
{
    /// <summary>
    /// A module that just holds commands for inspecting assets.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AssetInfoModule")]
    public class AssetInfoModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public string Name { get { return "Asset Information Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: INITIALIZED MODULE");
        }

        public void PostInitialise()
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: POST INITIALIZED MODULE");
        }

        public void Close()
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: CLOSED MODULE");
        }

        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ASSET INFO MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            if (m_scene == null)
                m_scene = scene;

            MainConsole.Instance.Commands.AddCommand(
                "Assets",
                false,
                "show asset",
                "show asset <ID>",
                "Show asset information",
                HandleShowAsset);

            MainConsole.Instance.Commands.AddCommand(
                "Assets", false, "dump asset",
                "dump asset <id>",
                "Dump an asset",
                HandleDumpAsset);
        }

        void HandleDumpAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage is dump asset <ID>");
                return;
            }

            UUID assetId;
            string rawAssetId = args[2];

            if (!UUID.TryParse(rawAssetId, out assetId))
            {
                MainConsole.Instance.Output("ERROR: {0} is not a valid ID format", null, rawAssetId);
                return;
            }

            AssetBase asset = m_scene.AssetService.Get(assetId.ToString());
            if (asset == null)
            {
                MainConsole.Instance.Output("ERROR: No asset found with ID {0}", null, assetId);
                return;
            }

            string fileName = rawAssetId;

            if (!ConsoleUtil.CheckFileDoesNotExist(MainConsole.Instance, fileName))
                return;

            using (FileStream fs = new FileStream(fileName, FileMode.CreateNew))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(asset.Data);
                }
            }

            MainConsole.Instance.Output("Asset dumped to file {0}", null, fileName);
        }

        void HandleShowAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: show asset <ID>");
                return;
            }

            AssetBase asset = m_scene.AssetService.Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Asset not found");
                return;
            }

            int i;

            MainConsole.Instance.Output("Name: {0}", null, asset.Name);
            MainConsole.Instance.Output("Description: {0}", null, asset.Description);
            MainConsole.Instance.Output("Type: {0} (type number = {1})", null, (AssetType)asset.Type, asset.Type);
            MainConsole.Instance.Output("Content-type: {0}", null, asset.Metadata.ContentType);
            MainConsole.Instance.Output("Size: {0} bytes", null, asset.Data.Length);
            MainConsole.Instance.Output("Temporary: {0}", null, asset.Temporary ? "yes" : "no");
            MainConsole.Instance.Output("Flags: {0}", null, asset.Metadata.Flags);

            for (i = 0 ; i < 5 ; i++)
            {
                int off = i * 16;
                if (asset.Data.Length <= off)
                    break;
                int len = 16;
                if (asset.Data.Length < off + len)
                    len = asset.Data.Length - off;

                byte[] line = new byte[len];
                Array.Copy(asset.Data, off, line, 0, len);

                string text = BitConverter.ToString(line);
                MainConsole.Instance.Output(String.Format("{0:x4}: {1}", off, text));
            }
        }
    }
}