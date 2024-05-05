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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    public class GetAssetsHandler
    {
        private static readonly ILog m_log =
                   LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Dictionary<string, AssetType> queryTypes = new()
        {
            {"texture_id", AssetType.Texture},
            {"sound_id", AssetType.Sound},
            {"callcard_id", AssetType.CallingCard},
            {"landmark_id", AssetType.Landmark},
            {"script_id", AssetType.LSLText},
            {"clothing_id", AssetType.Clothing},
            {"object_id", AssetType.Object},
            {"notecard_id", AssetType.Notecard},
            {"lsltext_id", AssetType.LSLText},
            {"lslbyte_id", AssetType.LSLBytecode},
            {"txtr_tga_id", AssetType.TextureTGA},
            {"bodypart_id", AssetType.Bodypart},
            {"snd_wav_id", AssetType.SoundWAV},
            {"img_tga_id", AssetType.ImageTGA},
            {"jpeg_id", AssetType.ImageJPEG},
            {"animatn_id", AssetType.Animation},
            {"gesture_id", AssetType.Gesture},
            {"mesh_id", AssetType.Mesh},
            {"settings_id", AssetType.Settings},
            {"material_id", AssetType.Material}
        };

        private IAssetService m_assetService;

        public GetAssetsHandler(IAssetService assService)
        {
            m_assetService = assService;
        }

        public void Handle(OSHttpRequest req, OSHttpResponse response, string serviceURL = null)
        {
            response.ContentType = "text/plain";

            if (m_assetService == null)
            {
                //m_log.Warn("[GETASSET]: no service"); 
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                response.KeepAlive = false;
                return;
            }

            response.StatusCode = (int)HttpStatusCode.BadRequest;

            var queries = req.QueryAsDictionary;
            if(queries.Count == 0)
                return;

            AssetType type = AssetType.Unknown;
            string assetStr = string.Empty;
            foreach (KeyValuePair<string,string> kvp in queries)
            {
                if (queryTypes.TryGetValue(kvp.Key, out type))
                {
                    assetStr = kvp.Value;
                    break;
                }
            }

            if(type == AssetType.Unknown)
            {
                //m_log.Warn("[GETASSET]: Unknown type: " + query);
                m_log.Warn("[GETASSET]: Unknown type");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (string.IsNullOrEmpty(assetStr))
                return;

            if(!UUID.TryParse(assetStr, out UUID assetID))
                return;

            ManualResetEventSlim done = new ManualResetEventSlim(false);
            AssetBase asset = null;
            m_assetService.Get(assetID.ToString(), serviceURL, false, (AssetBase a) =>
                {
                    asset = a;
                    done.Set();
                });

            done.Wait();
            done.Dispose();
            done = null;

            if (asset == null)
            {
                // m_log.Warn("[GETASSET]: not found: " + query + " " + assetStr);
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            int len = asset.Data.Length;

            if (len == 0)
            {
                m_log.Warn("[GETASSET]: asset with empty data: " + assetStr + " type " + asset.Type.ToString());
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (asset.Type != (sbyte)type)
            {
                m_log.Warn("[GETASSET]: asset with wrong type: " + assetStr + " " + asset.Type.ToString() + " != " + ((sbyte)type).ToString());
                //response.StatusCode = (int)HttpStatusCode.NotFound;
                //return;
            }

            // range request
            if (Util.TryParseHttpRange(req.Headers["range"], out int start, out int end))
            {
                // viewers do send broken start, then flag good assets as bad
                if (start >= len)
                {
                    //m_log.Warn("[GETASSET]: bad start: " + range);
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    if (end == -1)
                        end = len - 1;
                    else
                        end = Utils.Clamp(end, 0, len - 1);

                    start = Utils.Clamp(start, 0, end);
                    len = end - start + 1;

                    //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);
                    response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", start, end, asset.Data.Length));
                    response.StatusCode = (int)HttpStatusCode.PartialContent;
                    response.RawBufferStart = start;
                }
            }
            else
                response.StatusCode = (int)HttpStatusCode.OK;

            response.ContentType = asset.Metadata.ContentType;
            response.RawBuffer = asset.Data;
            response.RawBufferLen = len;
            if (type == AssetType.Mesh || type == AssetType.Texture)
                response.Priority = 2;
            else
                response.Priority = 1;
        }
    }
}