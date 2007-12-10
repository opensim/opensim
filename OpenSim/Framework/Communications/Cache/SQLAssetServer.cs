/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Reflection;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    public class SQLAssetServer : AssetServerBase
    {
        public SQLAssetServer(string pluginName)
        {
            AddPlugin(pluginName);
        }

        public SQLAssetServer(IAssetProvider assetProvider)
        {
            m_assetProviderPlugin = assetProvider;
        }

        public void AddPlugin(string FileName)
        {
            MainLog.Instance.Verbose("SQLAssetServer", "AssetStorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IAssetProvider", true);

                    if (typeInterface != null)
                    {
                        IAssetProvider plug =
                            (IAssetProvider) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        m_assetProviderPlugin = plug;
                        m_assetProviderPlugin.Initialise();

                        MainLog.Instance.Verbose("AssetStorage",
                                                 "Added " + m_assetProviderPlugin.Name + " " +
                                                 m_assetProviderPlugin.Version);
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }


        public override void Close()
        {
            base.Close();

            m_assetProviderPlugin.CommitAssets();
        }

        protected override void RunRequests()
        {
            while (true)
            {
                ARequest req = _assetRequests.Dequeue();

                //MainLog.Instance.Verbose("AssetStorage","Requesting asset: " + req.AssetID);

                AssetBase asset = null;
                lock (syncLock)
                {
                    asset = m_assetProviderPlugin.FetchAsset(req.AssetID);
                }
                if (asset != null)
                {
                    _receiver.AssetReceived(asset, req.IsTexture);
                }
                else
                {
                    _receiver.AssetNotFound(req.AssetID);
                }
            }
        }

        protected override void StoreAsset(AssetBase asset)
        {
            m_assetProviderPlugin.CreateAsset(asset);
        }

        protected override void CommitAssets()
        {
            m_assetProviderPlugin.CommitAssets();
        }
    }
}
