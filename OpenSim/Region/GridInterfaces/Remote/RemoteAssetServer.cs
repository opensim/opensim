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
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.GridInterfaces.Remote
{
    public class RemoteAssetServer : IAssetServer
    {
        private IAssetReceiver _receiver;
        private BlockingQueue<ARequest> _assetRequests;
        private Thread _remoteAssetServerThread;
        private string AssetServerUrl;
        private string AssetSendKey;

        public RemoteAssetServer()
        {
            this._assetRequests = new BlockingQueue<ARequest>();
            this._remoteAssetServerThread = new Thread(new ThreadStart(RunRequests));
            this._remoteAssetServerThread.IsBackground = true;
            this._remoteAssetServerThread.Start();
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Remote Asset Server class created");
        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            this._receiver = receiver;
        }

        public void RequestAsset(LLUUID assetID, bool isTexture)
        {
            ARequest req = new ARequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            this._assetRequests.Enqueue(req);
        }

        public void UpdateAsset(AssetBase asset)
        {

        }

        public void UploadNewAsset(AssetBase asset)
        {
            Encoding Windows1252Encoding = Encoding.GetEncoding(1252);
            string ret = Windows1252Encoding.GetString(asset.Data);
            byte[] buffer = Windows1252Encoding.GetBytes(ret);
            WebClient client = new WebClient();
            client.UploadData(this.AssetServerUrl + "assets/" + asset.FullID, buffer);

        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
            this.AssetServerUrl = ServerUrl;
            this.AssetSendKey = ServerKey;
        }

        private void RunRequests()
        {
            while (true)
            {
                //we need to add support for the asset server not knowing about a requested asset
		// 404... THE MAGIC FILE NOT FOUND ERROR, very useful for telling you things such as a file (or asset ;) ) not being found!!!!!!!!!!! it's 2:22AM
                ARequest req = this._assetRequests.Dequeue();
                LLUUID assetID = req.AssetID;
              //  OpenSim.Framework.Console.MainLog.Instance.Verbose(" RemoteAssetServer- Got a AssetServer request, processing it - " + this.AssetServerUrl + "assets/" + assetID);
                WebRequest AssetLoad = WebRequest.Create(this.AssetServerUrl + "assets/" + assetID);
                WebResponse AssetResponse = AssetLoad.GetResponse();
                byte[] idata = new byte[(int)AssetResponse.ContentLength];
                BinaryReader br = new BinaryReader(AssetResponse.GetResponseStream());
                idata = br.ReadBytes((int)AssetResponse.ContentLength);
                br.Close();
                
                AssetBase asset = new AssetBase();
                asset.FullID = assetID;
                asset.Data = idata;
                _receiver.AssetReceived(asset, req.IsTexture);
            }
        }

        public void Close()
        {

        }
    }

    public class RemoteAssetPlugin : IAssetPlugin
    {
        public RemoteAssetPlugin()
        {

        }

        public IAssetServer GetAssetServer()
        {
            return (new RemoteAssetServer());
        }
    }

}
