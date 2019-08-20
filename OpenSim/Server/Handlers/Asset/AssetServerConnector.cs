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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Console;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Asset
{
    public class AssetServiceConnector : ServiceConnector
    {
        private IAssetService m_AssetService;
        private string m_ConfigName = "AssetService";

        public AssetServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            if (configName != String.Empty)
                m_ConfigName = configName;

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string assetService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (assetService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config, m_ConfigName };
            m_AssetService =
                    ServerUtils.LoadPlugin<IAssetService>(assetService, args);

            if (m_AssetService == null)
                throw new Exception(String.Format("Failed to load AssetService from {0}; config is {1}", assetService, m_ConfigName));

            bool allowDelete = serverConfig.GetBoolean("AllowRemoteDelete", false);
            bool allowDeleteAllTypes = serverConfig.GetBoolean("AllowRemoteDeleteAllTypes", false);

            string redirectURL = serverConfig.GetString("RedirectURL", string.Empty);

            AllowedRemoteDeleteTypes allowedRemoteDeleteTypes;

            if (!allowDelete)
            {
                allowedRemoteDeleteTypes = AllowedRemoteDeleteTypes.None;
            }
            else
            {
                if (allowDeleteAllTypes)
                    allowedRemoteDeleteTypes = AllowedRemoteDeleteTypes.All;
                else
                    allowedRemoteDeleteTypes = AllowedRemoteDeleteTypes.MapTile;
            }

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddStreamHandler(new AssetServerGetHandler(m_AssetService, auth, redirectURL));
            server.AddStreamHandler(new AssetServerPostHandler(m_AssetService, auth));
            server.AddStreamHandler(new AssetServerDeleteHandler(m_AssetService, allowedRemoteDeleteTypes, auth));
            server.AddStreamHandler(new AssetsExistHandler(m_AssetService));

            MainConsole.Instance.Commands.AddCommand("Assets", false,
                    "show asset",
                    "show asset <ID>",
                    "Show asset information",
                    HandleShowAsset);

            MainConsole.Instance.Commands.AddCommand("Assets", false,
                    "delete asset",
                    "delete asset <ID>",
                    "Delete asset from database",
                    HandleDeleteAsset);

            MainConsole.Instance.Commands.AddCommand("Assets", false,
                    "dump asset",
                    "dump asset <ID>",
                    "Dump asset to a file",
                    "The filename is the same as the ID given.",
                    HandleDumpAsset);
        }

        void HandleDeleteAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = m_AssetService.Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Could not find asset with ID {0}", null, args[2]);
                return;
            }

            if (!m_AssetService.Delete(asset.ID))
                MainConsole.Instance.Output("ERROR: Could not delete asset {0} {1}", null, asset.ID, asset.Name);
            else
                MainConsole.Instance.Output("Deleted asset {0} {1}", null, asset.ID, asset.Name);
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

            AssetBase asset = m_AssetService.Get(assetId.ToString());
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

            AssetBase asset = m_AssetService.Get(args[2]);

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
