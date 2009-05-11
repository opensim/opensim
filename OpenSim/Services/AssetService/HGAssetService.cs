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
 */

using log4net;
using Nini.Config;
using System;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.ServiceConnectors.Asset
{
    public class HGAssetService : IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public HGAssetService(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetServices", "");

                IConfig assetConfig = source.Configs["AssetService"];
                if (assetConfig == null)
                {
                    m_log.Error("[ASSET CONNECTOR]: AssetService missing from OpanSim.ini");
                    return;
                }

                m_log.Info("[ASSET CONNECTOR]: HG asset service enabled");
            }
        }

        //
        // Note to Diva:
        //
        // This is not the broker!
        // This module is not supposed to route anything anywhere. This is where
        // the code to access remote assets (by URL) goes. It can be assumed
        // that the ID is a URL. The broker makes sure of that.
        //
        // This is a disposable comment :) Feel free to remove it
        //

        public AssetBase Get(string id)
        {
            return null;
        }

        public AssetMetadata GetMetadata(string id)
        {
            return null;
        }

        public byte[] GetData(string id)
        {
            return null;
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            return false;
        }

        public string Store(AssetBase asset)
        {
            return String.Empty;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public bool Delete(string id)
        {
            return false;
        }
    }
}
