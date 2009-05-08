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

using Nini.Config;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Services.AssetService;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Servers.AssetServer
{
    public class AssetServerGetHandler : BaseStreamHandler
    {
        private IAssetService m_AssetService;

        public AssetServerGetHandler(IAssetService service) :
                base("GET", "/assets")
        {
            m_AssetService = service;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            byte[] result = new byte[0];

            string[] p = SplitParams(path);

            if (p.Length == 0)
                return result;

            AssetBase asset = m_AssetService.Get(p[0]);

            if (asset != null)
            {
                if (p.Length > 1 && p[1] == "data")
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    httpResponse.ContentType =
                            SLAssetTypeToContentType(asset.Type);

                    result = asset.Data;
                }
                else
                {
                    XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                    MemoryStream ms = new MemoryStream();
                    XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                    xw.Formatting = Formatting.Indented;
                    xs.Serialize(xw, asset);
                    xw.Flush();

                    ms.Seek(0, SeekOrigin.Begin);
                    result = ms.GetBuffer();

                    Array.Resize<byte>(ref result, (int)ms.Length);
                }
            }
            return result;
        }

        private string SLAssetTypeToContentType(int assetType)
        {
            switch (assetType)
            {
                case 0:
                    return "image/jp2";
                case 1:
                    return "application/ogg";
                case 2:
                    return "application/x-metaverse-callingcard";
                case 3:
                    return "application/x-metaverse-landmark";
                case 5:
                    return "application/x-metaverse-clothing";
                case 6:
                    return "application/x-metaverse-primitive";
                case 7:
                    return "application/x-metaverse-notecard";
                case 8:
                    return "application/x-metaverse-folder";
                case 10:
                    return "application/x-metaverse-lsl";
                case 11:
                    return "application/x-metaverse-lso";
                case 12:
                    return "image/tga";
                case 13:
                    return "application/x-metaverse-bodypart";
                case 17:
                    return "audio/x-wav";
                case 19:
                    return "image/jpeg";
                case 20:
                    return "application/x-metaverse-animation";
                case 21:
                    return "application/x-metaverse-gesture";
                case 22:
                    return "application/x-metaverse-simstate";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
