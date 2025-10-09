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
using System.Linq;
using System.Reflection;
using System.Xml;
using log4net;
using Nini.Config;
using OpenMetaverse;


namespace OpenSim.Framework.AssetLoader.Filesystem
{
    /// <summary>
    /// Loads assets from the filesystem location.  Not yet a plugin, though it should be.
    /// </summary>
    public class AssetLoaderFileSystem : IAssetLoader
    {
        private const string DEFAULT_LIBRARY_OWNER_ID = "11111111-1111-0000-0000-000100bba000";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);

        protected static AssetBase CreateAsset(string assetIdStr, string name, string path, sbyte type)
        {
            var asset = new AssetBase(new UUID(assetIdStr), name, type, DEFAULT_LIBRARY_OWNER_ID);

            if (!string.IsNullOrEmpty(path))
            {
                //m_log.InfoFormat("[ASSETS]: Loading: [{0}][{1}]", name, path);
                LoadAsset(asset, path);
            }
            else
            {
                asset.Data = [];
                m_log.InfoFormat("[ASSETS]: Instantiated: [{0}]", name);
            }

            return asset;
        }

        protected static void LoadAsset(AssetBase info, string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists)
            {
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var binaryReader = new BinaryReader(fileStream);
                info.Data = binaryReader.ReadBytes((int)fileInfo.Length);
            }
            else
            {
                m_log.ErrorFormat("[ASSETS]: file: [{0}] not found !", path);
            }
        }

        public void ForEachDefaultXmlAsset(string assetSetFilename, Action<AssetBase> action)
        {
            if (!File.Exists(assetSetFilename))
            {
                m_log.ErrorFormat("[ASSETS]: Asset set control file {0} does not exist! No assets loaded.", assetSetFilename);
                return;           
            }
            
            var assets = new List<AssetBase>();
            
            var assetSetPath = "ERROR";
            try
            {
                var source = new XmlConfigSource(assetSetFilename);
                var assetRootPath = Path.GetFullPath(source.SavePath);
                assetRootPath = Path.GetDirectoryName(assetRootPath);

                foreach (IConfig cfg in source.Configs)
                {
                    assetSetPath = cfg.GetString("file", string.Empty);
                    var assetFinalPath = (assetRootPath is null) ? assetSetPath : Path.Combine(assetRootPath, assetSetPath);
                    LoadXmlAssetSet(assetFinalPath, assets);
                }
            }
            catch (XmlException e)
            {
                m_log.ErrorFormat("[ASSETS]: Error loading {0} : {1}", assetSetPath, e.Message);
            }
            
            assets.ForEach(action);
        }

        /// <summary>
        /// Use the asset set information at path to load assets
        /// </summary>
        /// <param name="assetSetPath"></param>
        /// <param name="assets"></param>
        protected static void LoadXmlAssetSet(string assetSetPath, List<AssetBase> assets)
        {
            //m_log.InfoFormat("[ASSETS]: Loading asset set {0}", assetSetPath);

            if (!File.Exists(assetSetPath))
            {
                m_log.ErrorFormat("[ASSETS]: Asset set file {0} does not exist!", assetSetPath);
                return;           
            }

            try
            {
                var source = new XmlConfigSource(assetSetPath);
                var dir = Path.GetDirectoryName(assetSetPath) ?? string.Empty;
                
                assets.AddRange(
                    from IConfig cfg in source.Configs 
                    let assetPath = cfg.GetString("fileName", string.Empty) 
                    select CreateAsset(
                        cfg.GetString("assetID", UUID.Random().ToString()), 
                        cfg.GetString("name", string.Empty), 
                        string.IsNullOrEmpty(assetPath) ? null : Path.Combine(dir, assetPath), 
                        (sbyte)cfg.GetInt("assetType", 0)
                    )
                );
            }
            catch (XmlException e)
            {
                m_log.ErrorFormat("[ASSETS]: Error loading {0} : {1}", assetSetPath, e.Message);
            }
            
        }
    }
}
