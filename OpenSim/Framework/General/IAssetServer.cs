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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Framework
{
    /// <summary>
    /// Description of IAssetServer.
    /// </summary>

    public interface IAssetServer
    {
        void SetReceiver(IAssetReceiver receiver);
        void FetchAsset(LLUUID assetID, bool isTexture);
        void UpdateAsset(AssetBase asset);
        void StoreAndCommitAsset(AssetBase asset);
        void Close();
        void LoadAsset(AssetBase info, bool image, string filename);
        List<AssetBase> GetDefaultAssets();
        AssetBase CreateImageAsset(string assetIdStr, string name, string filename);
        void ForEachDefaultAsset(Action<AssetBase> action);
        AssetBase CreateAsset(string assetIdStr, string name, string filename, bool isImage);
        void ForEachXmlAsset(Action<AssetBase> action);
    }

    // could change to delegate?
    public interface IAssetReceiver
    {
        void AssetReceived(AssetBase asset, bool IsTexture);
        void AssetNotFound(LLUUID assetID);
    }

    public interface IAssetPlugin
    {
        IAssetServer GetAssetServer();
    }

    public struct ARequest
    {
        public LLUUID AssetID;
        public bool IsTexture;
    }
}
