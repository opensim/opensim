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
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Agent.TextureSender
{
    /// <summary>
    /// Commands for the J2KDecoder module.  For debugging purposes.
    /// </summary>
    /// <remarks>
    /// Placed here so that they can be removed if not required and to keep the J2KDecoder module itself simple.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "J2KDecoderCommandModule")]
    public class J2KDecoderCommandModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public string Name { get { return "Asset Information Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: INITIALIZED MODULE");
        }

        public void PostInitialise()
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: POST INITIALIZED MODULE");
        }

        public void Close()
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: CLOSED MODULE");
        }

        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[J2K DECODER COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            if (m_scene == null)
                m_scene = scene;

            MainConsole.Instance.Commands.AddCommand(
                "Assets",
                false,
                "j2k decode",
                "j2k decode <ID>",
                "Do JPEG2000 decoding of an asset.",
                "This is for debugging purposes.  The asset id given must contain JPEG2000 data.",
                HandleDecode);
        }

        void HandleDecode(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage is j2k decode <ID>");
                return;
            }

            UUID assetId;
            string rawAssetId = args[2];

            if (!UUID.TryParse(rawAssetId, out assetId))
            {
                MainConsole.Instance.OutputFormat("ERROR: {0} is not a valid ID format", rawAssetId);
                return;
            }

            AssetBase asset = m_scene.AssetService.Get(assetId.ToString());
            if (asset == null)
            {
                MainConsole.Instance.OutputFormat("ERROR: No asset found with ID {0}", assetId);
                return;
            }

            if (asset.Type != (sbyte)AssetType.Texture)
            {
                MainConsole.Instance.OutputFormat("ERROR: Asset {0} is not a texture type", assetId);
                return;
            }

            IJ2KDecoder decoder = m_scene.RequestModuleInterface<IJ2KDecoder>();
            if (decoder == null)
            {
                MainConsole.Instance.OutputFormat("ERROR: No IJ2KDecoder module available");
                return;
            }

            OpenJPEG.J2KLayerInfo[] layers;
            int components;
            if (decoder.Decode(assetId, asset.Data, out layers, out components))
            {
                MainConsole.Instance.OutputFormat(
                    "Successfully decoded asset {0} with {1} layers and {2} components",
                    assetId, layers.Length, components);
            }
            else
            {
                MainConsole.Instance.OutputFormat("Decode of asset {0} failed", assetId);
            }
        }
    }
}