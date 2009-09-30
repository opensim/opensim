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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using Nini.Config;

namespace OpenSim.Tests.Common.Mock
{
    public class TestAssetService : IAssetService
    {
        private readonly Dictionary<string, AssetBase> Assets = new Dictionary<string, AssetBase>();

        public TestAssetService(IConfigSource config)
        {
        }
        
        public AssetBase Get(string id)
        {
            AssetBase asset;
            if (Assets.ContainsKey(id))
                asset = Assets[id];
            else
                asset = null;
            
            return asset;
        }

        public AssetMetadata GetMetadata(string id)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetData(string id)
        {
            throw new System.NotImplementedException();
        }

        public bool Get(string id, object sender, AssetRetrieved handler)
        {
            handler(id, sender, Get(id));
            
            return true;
        }

        public string Store(AssetBase asset)
        {
            Assets[asset.ID] = asset;

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            throw new System.NotImplementedException();
        }

        public bool Delete(string id)
        {
            throw new System.NotImplementedException();
        }
    }
}