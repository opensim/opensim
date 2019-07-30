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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
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

        private static readonly Dictionary<string, AssetType> queryTypes = new Dictionary<string, AssetType>()
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
            {"mesh_id", AssetType.Mesh}
        };

        private IAssetService m_assetService;

        public GetAssetsHandler(IAssetService assService)
        {
            m_assetService = assService;
        }

        public Hashtable Handle(Hashtable request)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/plain";
            responsedata["int_bytes"] = 0;

            if (m_assetService == null)
            {
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.ServiceUnavailable;
                responsedata["str_response_string"] = "The asset service is unavailable";
                responsedata["keepalive"] = false;
                return responsedata;
            }

            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.BadRequest;

            string[] queries = null;
            if(request.Contains("querystringkeys"))
                queries = (string[])request["querystringkeys"];
            
            if(queries == null || queries.Length == 0)
                return responsedata;

            string query = queries[0];
            if(!queryTypes.ContainsKey(query))
            {
                m_log.Warn("[GETASSET]: Unknown type: " + query);
                return responsedata;
            }

            AssetType type = queryTypes[query];

            string assetStr = string.Empty;
            if (request.ContainsKey(query))
                assetStr = request[query].ToString();

            if (String.IsNullOrEmpty(assetStr))
                return responsedata;

            UUID assetID = UUID.Zero;
            if(!UUID.TryParse(assetStr, out assetID))
                return responsedata;

            AssetBase asset = m_assetService.Get(assetID.ToString());
            if(asset == null)
            {
                // m_log.Warn("[GETASSET]: not found: " + query + " " + assetStr);
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
                responsedata["str_response_string"] = "Asset not found.";
                return responsedata;
            }

            if (asset.Type != (sbyte)type)
            {
                responsedata["str_response_string"] = "Got wrong asset type";
                return responsedata;
            }

            if(type == AssetType.Mesh || type == AssetType.Texture)
                responsedata["throttle"] = true;

            responsedata["content_type"] = asset.Metadata.ContentType;
            responsedata["bin_response_data"] = asset.Data;
            responsedata["int_bytes"] = asset.Data.Length;
            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;

            string range = String.Empty;
            if (((Hashtable)request["headers"])["range"] != null)
               range = (string)((Hashtable)request["headers"])["range"];
            else if (((Hashtable)request["headers"])["Range"] != null)
                range = (string)((Hashtable)request["headers"])["Range"];
            else
                return responsedata; // full asset

            if (String.IsNullOrEmpty(range))
                return responsedata; // full asset

            // range request
            int start, end;
            if (Util.TryParseHttpRange(range, out start, out end))
            {
                // Before clamping start make sure we can satisfy it in order to avoid
                // sending back the last byte instead of an error status
                if (start >= asset.Data.Length)
                {
                    responsedata["str_response_string"] = "This range doesnt exist.";
                    return responsedata;
                }

                if (end == -1)
                    end = asset.Data.Length - 1;
                else
                    end = Utils.Clamp(end, 0, asset.Data.Length - 1);

                start = Utils.Clamp(start, 0, end);
                int len = end - start + 1;

                //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);
                Hashtable headers = new Hashtable();
                headers["Content-Range"] = String.Format("bytes {0}-{1}/{2}", start, end, asset.Data.Length);
                responsedata["headers"] = headers;
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.PartialContent;
                responsedata["bin_start"] = start;
                responsedata["int_bytes"] = len;
                return responsedata;
            }

            m_log.Warn("[GETASSETS]: Failed to parse a range, sending full asset: " + assetStr);
            return responsedata;
        }
    }
}