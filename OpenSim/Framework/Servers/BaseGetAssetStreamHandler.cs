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
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;

namespace OpenSim.Framework.Servers
{
    public abstract class BaseGetAssetStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected BaseGetAssetStreamHandler(string httpMethod, string path) : base(httpMethod, path)
        {
        }

        protected abstract AssetBase GetAsset(UUID assetID);

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            byte[] result = new byte[] { };

            string[] p = SplitParams(path);

            if (p.Length > 0)
            {
                UUID assetID;
                
                if (!UUID.TryParse(p[0], out assetID))
                {
                    m_log.DebugFormat(
                        "[REST]: GET:/asset ignoring request with malformed UUID {0}", p[0]);
                    return result;
                }

                if (StatsManager.AssetStats != null)
                {
                    StatsManager.AssetStats.AddRequest();
                }

                AssetBase asset = GetAsset(assetID); 

                if (asset != null)
                {
                    if (p.Length > 1 && p[1] == "data")
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = SLAssetTypeToContentType(asset.Type);
                        result = asset.Data;
                    }
                    else
                    {
                        result = GetXml(asset);
                    }
                }
                else
                {
                    m_log.DebugFormat("[REST]: GET:/asset failed to find {0}", assetID);
                    
                    httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                    
                    if (StatsManager.AssetStats != null)
                    {
                        StatsManager.AssetStats.AddNotFoundRequest();
                    }
                }
            }

            return result;
        }

        public static byte[] GetXml(AssetBase asset)
        {
            byte[] result;
            XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
            xw.Formatting = Formatting.Indented;
            xs.Serialize(xw, asset);
            xw.Flush();

            ms.Seek(0, SeekOrigin.Begin);
            //StreamReader sr = new StreamReader(ms);

            result = ms.GetBuffer();

            Array.Resize<byte>(ref result, (int)ms.Length);
            return result;
        }

        public string ProcessAssetDataString(string data)
        {
            Regex regex = new Regex("(creator_id|owner_id)\\s+(\\S+)");

            // IUserService userService = null;

            data = regex.Replace(data, delegate(Match m)
                                           {
                                               string result = String.Empty;

//                string key = m.Groups[1].Captures[0].Value;
//
//                string value = m.Groups[2].Captures[0].Value;
//
//                Guid userUri;
//
//                switch (key)
//                {
//                    case "creator_id":
//                        userUri = new Guid(value);
//                        //         result = "creator_url " + userService(userService, userUri);
//                        break;
//
//                    case "owner_id":
//                        userUri = new Guid(value);
//                        //       result = "owner_url " + ResolveUserUri(userService, userUri);
//                        break;
//                }

                                               return result;
                                           });

            return data;
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
