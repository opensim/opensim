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
*     * Neither the name of the OpenSim Project nor the
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
* 
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using libsecondlife;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Framework.Console;

/// <summary>
/// Loads assets from the filesystem location.  Not yet a plugin, though it should be.
/// </summary>
namespace OpenSim.Framework.AssetLoader.Filesystem
{ 
    public class AssetLoaderFileSystem : IAssetLoader
    {
        protected AssetBase CreateAsset(string assetIdStr, string name, string filename, bool isImage)
        {
            AssetBase asset = new AssetBase(
                new LLUUID(assetIdStr),
                name
                );

            if (!String.IsNullOrEmpty(filename))
            {
                MainLog.Instance.Verbose("ASSETS", "Loading: [{0}][{1}]", name, filename);

                LoadAsset(asset, isImage, filename);
            }
            else
            {
                MainLog.Instance.Verbose("ASSETS", "Instantiated: [{0}]", name);
            }

            return asset;
        }
        
        protected void LoadAsset(AssetBase info, bool image, string filename)
        {
            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"); //+ folder;
            string fileName = Path.Combine(dataPath, filename);
            FileInfo fInfo = new FileInfo(fileName);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] idata = new byte[numBytes];
            BinaryReader br = new BinaryReader(fStream);
            idata = br.ReadBytes((int) numBytes);
            br.Close();
            fStream.Close();
            info.Data = idata;
            //info.loaded=true;
        }
        
        public void ForEachXmlAsset(Action<AssetBase> action)
        {
            List<AssetBase> assets = new List<AssetBase>();
            // System.Console.WriteLine("trying loading asset into database");
            string filePath = Path.Combine(Util.configDir(), "assets/OpenSimAssetSet.xml");
            if (File.Exists(filePath))
            {
                try
                {
                    XmlConfigSource source = new XmlConfigSource(filePath);

                    for (int i = 0; i < source.Configs.Count; i++)
                    {
                        // System.Console.WriteLine("loading asset into database");
                        string assetIdStr = source.Configs[i].GetString("assetID", LLUUID.Random().ToString());
                        string name = source.Configs[i].GetString("name", "");
                        sbyte type = (sbyte) source.Configs[i].GetInt("assetType", 0);
                        sbyte invType = (sbyte) source.Configs[i].GetInt("inventoryType", 0);
                        string fileName = source.Configs[i].GetString("fileName", "");

                        AssetBase newAsset = CreateAsset(assetIdStr, name, fileName, false);

                        newAsset.Type = type;
                        newAsset.InvType = invType;
                        assets.Add(newAsset);
                    }
                }
                catch (XmlException e)
                {
                    MainLog.Instance.Error("ASSETS", "Error loading " + filePath + ": " + e.ToString());
                }
            }
            assets.ForEach(action);
        }        
    }
}
