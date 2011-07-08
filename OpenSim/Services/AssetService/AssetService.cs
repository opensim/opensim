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
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AssetService
{
    public class AssetService : AssetServiceBase, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected static AssetService m_RootInstance;

        public AssetService(IConfigSource config) : base(config)
        {
            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                MainConsole.Instance.Commands.AddCommand("kfs", false,
                        "show digest",
                        "show digest <ID>",
                        "Show asset digest", HandleShowDigest);

                MainConsole.Instance.Commands.AddCommand("kfs", false,
                        "delete asset",
                        "delete asset <ID>",
                        "Delete asset from database", HandleDeleteAsset);
                
                MainConsole.Instance.Commands.AddCommand("kfs", false,
                        "dump asset",
                        "dump asset <ID>",
                        "Dump asset to a file", 
                        "The filename is the same as the ID given.", 
                        HandleDumpAsset);

                if (m_AssetLoader != null)
                {
                    IConfig assetConfig = config.Configs["AssetService"];
                    if (assetConfig == null)
                        throw new Exception("No AssetService configuration");

                    string loaderArgs = assetConfig.GetString("AssetLoaderArgs",
                            String.Empty);

                    bool assetLoaderEnabled = assetConfig.GetBoolean("AssetLoaderEnabled", true);

                    if (assetLoaderEnabled)
                    {
                        m_log.InfoFormat("[ASSET]: Loading default asset set from {0}", loaderArgs);

                        m_AssetLoader.ForEachDefaultXmlAsset(
                            loaderArgs,
                            delegate(AssetBase a)
                            {
                                AssetBase existingAsset = Get(a.ID);
//                                AssetMetadata existingMetadata = GetMetadata(a.ID);

                                if (existingAsset == null || Util.SHA1Hash(existingAsset.Data) != Util.SHA1Hash(a.Data))
                                {
//                                    m_log.DebugFormat("[ASSET]: Storing {0} {1}", a.Name, a.ID);
                                    Store(a);
                                }
                            });
                    }

                    m_log.Info("[ASSET SERVICE]: Local asset service enabled");
                }
            }
        }

        public virtual AssetBase Get(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset for {0}", id);
            
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
            {
                m_log.WarnFormat("[ASSET SERVICE]: Could not parse requested asset id {0}", id);
                return null;
            }

            return m_Database.GetAsset(assetID);
        }

        public virtual AssetBase GetCached(string id)
        {
            return Get(id);
        }

        public virtual AssetMetadata GetMetadata(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset metadata for {0}", id);
            
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return null;

            AssetBase asset = m_Database.GetAsset(assetID);
            if (asset != null)
                return asset.Metadata;

            return null;
        }

        public virtual byte[] GetData(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset data for {0}", id);
            
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return null;

            AssetBase asset = m_Database.GetAsset(assetID);
            return asset.Data;
        }

        public virtual bool Get(string id, Object sender, AssetRetrieved handler)
        {
            //m_log.DebugFormat("[AssetService]: Get asset async {0}", id);
            
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return false;

            AssetBase asset = m_Database.GetAsset(assetID);

            //m_log.DebugFormat("[AssetService]: Got asset {0}", asset);
            
            handler(id, sender, asset);

            return true;
        }

        public virtual string Store(AssetBase asset)
        {
//            m_log.DebugFormat(
//                "[ASSET SERVICE]: Storing asset {0} {1}, bytes {2}", asset.Name, asset.ID, asset.Data.Length);
            
            m_Database.StoreAsset(asset);

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public virtual bool Delete(string id)
        {
            m_log.DebugFormat("[ASSET SERVICE]: Deleting asset {0}", id);
            UUID assetID;
            if (!UUID.TryParse(id, out assetID))
                return false;

            AssetBase asset = m_Database.GetAsset(assetID);
            if (asset == null)
                return false;

            if ((int)(asset.Flags & AssetFlags.Maptile) != 0)
            {
                return m_Database.Delete(id);
            }
            else
                m_log.DebugFormat("[ASSET SERVICE]: Request to delete asset {0}, but flags are not Maptile", id);

            return false;
        }
        
        void HandleDumpAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage is dump asset <ID>");
                return;
            }
            
            string rawAssetId = args[2];
            UUID assetId;
            
            if (!UUID.TryParse(rawAssetId, out assetId))
            {
                MainConsole.Instance.OutputFormat("ERROR: {0} is not a valid ID format", rawAssetId);
                return;
            }
            
            AssetBase asset = m_Database.GetAsset(assetId);
            if (asset == null)
            {                
                MainConsole.Instance.OutputFormat("ERROR: No asset found with ID {0}", assetId);
                return;                
            }
            
            string fileName = rawAssetId;
            
            using (FileStream fs = new FileStream(fileName, FileMode.CreateNew))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(asset.Data);
                }
            }   
            
            MainConsole.Instance.OutputFormat("Asset dumped to file {0}", fileName);
        }

        void HandleShowDigest(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: show digest <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Asset not found");
                return;
            }

            int i;

            MainConsole.Instance.OutputFormat("Name: {0}", asset.Name);
            MainConsole.Instance.OutputFormat("Description: {0}", asset.Description);
            MainConsole.Instance.OutputFormat("Type: {0} (type number = {1})", (AssetType)asset.Type, asset.Type);
            MainConsole.Instance.OutputFormat("Content-type: {0}", asset.Metadata.ContentType);
            MainConsole.Instance.OutputFormat("Flags: {0}", asset.Metadata.Flags);

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

        void HandleDeleteAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Asset not found");
                return;
            }

            Delete(args[2]);

            //MainConsole.Instance.Output("Asset deleted");
            // TODO: Implement this

            MainConsole.Instance.Output("Asset deletion not supported by database");
        }
    }
}
